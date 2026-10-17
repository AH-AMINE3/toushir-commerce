import { useEffect, useRef, useState } from "react";
import { api, post, key, date, money } from "./api";
import type { Session } from "./api";
import { ProductPicker } from "./Business";
import type { Run } from "./Business";
import { approve } from "./approvals";

const scopes: Record<string, string> = {
  MERCHANT: "Contrat du magasin",
  SEGMENT: "Segment commercial",
  STANDARD: "Barème standard",
  OFFER: "Offre approuvée",
  ON_REQUEST: "Sur devis",
  MANUAL_QUOTE: "Prix négocié",
  UNAVAILABLE: "Non proposé",
};
const states: Record<string, string> = {
  DRAFT: "À approuver",
  PUBLISHED: "Publié",
  ACTIVE: "Actif",
  SCHEDULED: "Programmé",
  EXPIRED: "Expiré",
  RETIRED: "Retiré",
  REJECTED: "Rejeté",
};
const localInput = (value: string | number) => {
  const d = new Date(value);
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
};
const amount = (minor: number | null) =>
  minor == null ? "Sur devis" : money(minor / 100);
const instant = (value: string) =>
  new Date(value).toLocaleString("fr-DZ", {
    dateStyle: "medium",
    timeStyle: "short",
  });

export function Pricing({
  revision,
  run,
  session,
  campaigns,
}: {
  revision: number;
  run: Run;
  session: Session;
  campaigns: () => void;
}) {
  const [data, setData] = useState<any>(null),
    [error, setError] = useState(""),
    [form, setForm] = useState<any>(null),
    [selected, setSelected] = useState(""),
    [filter, setFilter] = useState("ALL"),
    [page, setPage] = useState(1);
  useEffect(() => {
    let current = true;
    api("/admin/pricing")
      .then((value) => {
        if (current) {
          setData(value);
          setError("");
        }
      })
      .catch((e) => {
        if (current) setError(e.message);
      });
    return () => {
      current = false;
    };
  }, [revision]);
  const admin = session.user.role === "ADMIN";
  if (!data)
    return (
      <div
        className={error ? "error-banner" : "boot-inline"}
        role={error ? "alert" : "status"}
      >
        {error || "Chargement des conditions tarifaires…"}
      </div>
    );
  const rows = data.rules.filter(
    (r: any) =>
      filter === "ALL" ||
      (filter === "DRAFT"
        ? r.status === filter
        : ["ACTIVE", "SCHEDULED"].includes(r.state)),
  );
  const active = data.rules.find((r: any) => r.id === selected);
  const targetName = (rule: any) =>
    rule.scope === "STANDARD"
      ? "Tous les magasins"
      : rule.scope === "SEGMENT"
        ? data.segments.find((s: any) => s.id === rule.targetId)?.name ||
          "Segment"
        : data.merchants.find((s: any) => s.id === rule.targetId)?.shopName ||
          "Magasin";
  return (
    <div className="pricing-workspace">
      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}
      <section
        className="panel pricing-priority"
        aria-labelledby="pricing-priority-title"
      >
        <div className="panel-heading">
          <div>
            <h2 id="pricing-priority-title">La priorité s’explique</h2>
            <p>
              Le premier tarif valide s’applique. Un prix absent reste à
              confirmer.
            </p>
          </div>
          <button className="button secondary" onClick={campaigns}>
            Voir les campagnes
          </button>
        </div>
        <ol className="pricing-priority-list">
          {["MERCHANT", "SEGMENT", "STANDARD"].map((scope, i) => (
            <li key={scope}>
              <span>{String(i + 1).padStart(2, "0")}</span>
              <strong>{scopes[scope]}</strong>
              <p>
                {
                  [
                    "Conditions négociées avec un magasin",
                    "Accord partagé par un groupe de magasins",
                    "Puis, à défaut, l’offre approuvée du dépôt",
                  ][i]
                }
              </p>
            </li>
          ))}
        </ol>
      </section>
      <div className="pricing-columns">
        <section className="panel module-section pricing-rules">
          <div className="panel-heading">
            <div>
              <h2>Règles & accords</h2>
              <p>
                Quantités en unités · Montants en DA · Deux personnes pour
                publier.
              </p>
            </div>
            {admin && (
              <button
                className="button"
                onClick={() => setForm(form ? null : {})}
              >
                {form ? "Fermer le formulaire" : "Proposer un tarif"}
              </button>
            )}
          </div>
          <div
            className="pricing-filters"
            role="group"
            aria-label="Filtrer les tarifs"
          >
            {[
              ["ALL", "Tous"],
              ["DRAFT", "À approuver"],
              ["CURRENT", "Actifs & programmés"],
            ].map(([value, label]) => (
              <button
                key={value}
                className={filter === value ? "active" : ""}
                aria-pressed={filter === value}
                onClick={() => {
                  setFilter(value);
                  setPage(1);
                }}
              >
                {label}
              </button>
            ))}
          </div>
          {form && (
            <TariffForm
              key={form.id || "new"}
              data={data}
              previous={form.id ? form : null}
              run={run}
              done={(id: string) => {
                setSelected(id);
                setForm(null);
              }}
            />
          )}
          {rows.length ? (
            <>
              <div className="table-scroll pricing-table-wrap">
                <table className="data-table pricing-table">
                  <caption className="sr-only">
                    Tarifs proposés et publiés du dépôt
                  </caption>
                  <thead>
                    <tr>
                      <th>Produit & portée</th>
                      <th>Quantité</th>
                      <th>Prix / unité</th>
                      <th>État</th>
                      <th>
                        <span className="sr-only">Dossier</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.slice((page - 1) * 12, page * 12).map((r: any) => (
                      <tr
                        key={r.id}
                        className={selected === r.id ? "pricing-selected" : ""}
                      >
                        <td>
                          <strong>{r.productName}</strong>
                          <small>
                            {scopes[r.scope]} · {targetName(r)}
                          </small>
                          <small>{r.reference}</small>
                        </td>
                        <td>
                          {r.minimumQuantity}–{r.maximumQuantity ?? "∞"}
                          <small>unités</small>
                        </td>
                        <td className="numeric">{amount(r.unitPriceMinor)}</td>
                        <td>
                          <span
                            className={
                              "badge " +
                              (r.state === "ACTIVE"
                                ? "green"
                                : r.state === "DRAFT"
                                  ? "amber"
                                  : "neutral")
                            }
                          >
                            {states[r.state]}
                          </span>
                        </td>
                        <td>
                          <button
                            className="button secondary"
                            aria-expanded={selected === r.id}
                            onClick={() =>
                              setSelected(selected === r.id ? "" : r.id)
                            }
                          >
                            Examiner
                            <span className="sr-only"> {r.reference}</span>
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div
                className="pricing-mobile-list"
                aria-label="Liste des tarifs"
              >
                {rows.slice((page - 1) * 12, page * 12).map((r: any) => (
                  <article
                    key={r.id}
                    className={selected === r.id ? "pricing-selected" : ""}
                  >
                    <h3>{r.productName}</h3>
                    <p>
                      {scopes[r.scope]} · {targetName(r)}
                    </p>
                    <small>{r.reference}</small>
                    <dl className="pricing-terms">
                      <div>
                        <dt>Quantité · unités</dt>
                        <dd>
                          {r.minimumQuantity} à{" "}
                          {r.maximumQuantity ?? "sans maximum"}
                        </dd>
                      </div>
                      <div>
                        <dt>Prix / unité</dt>
                        <dd>{amount(r.unitPriceMinor)}</dd>
                      </div>
                      <div>
                        <dt>État</dt>
                        <dd>{states[r.state]}</dd>
                      </div>
                    </dl>
                    <button
                      className="button secondary"
                      aria-expanded={selected === r.id}
                      onClick={() => setSelected(selected === r.id ? "" : r.id)}
                    >
                      Examiner {r.reference}
                    </button>
                  </article>
                ))}
              </div>
              <div className="pricing-pagination">
                <span>
                  {rows.length} règle{rows.length > 1 ? "s" : ""}
                </span>
                <button
                  className="button secondary"
                  disabled={page <= 1}
                  onClick={() => setPage(page - 1)}
                >
                  Précédent
                </button>
                <span>
                  {page} / {Math.max(1, Math.ceil(rows.length / 12))}
                </span>
                <button
                  className="button secondary"
                  disabled={page >= Math.ceil(rows.length / 12)}
                  onClick={() => setPage(page + 1)}
                >
                  Suivant
                </button>
              </div>
            </>
          ) : (
            <div className="module-empty">
              <h3>
                {filter === "DRAFT"
                  ? "Aucun tarif en attente"
                  : "Vos accords commencent ici"}
              </h3>
              <p>
                L’offre approuvée du dépôt reste la référence jusqu’à la
                publication d’un accord. Proposez une règle, puis faites-la
                vérifier par une autre personne habilitée.
              </p>
            </div>
          )}
          {active && (
            <section
              className="pricing-rule-detail"
              aria-label={"Conditions " + active.reference}
            >
              <div className="panel-heading">
                <h3>{active.reference}</h3>
                <button
                  className="button secondary"
                  onClick={() => setSelected("")}
                >
                  Fermer le détail
                </button>
              </div>
              <p>
                {active.productName} · {scopes[active.scope]} ·{" "}
                {targetName(active)}
              </p>
              <dl className="pricing-terms">
                <div>
                  <dt>Prix de base</dt>
                  <dd>{amount(active.unitPriceMinor)} / unité</dd>
                </div>
                <div>
                  <dt>Quantités incluses</dt>
                  <dd>
                    {active.minimumQuantity} à{" "}
                    {active.maximumQuantity ?? "sans maximum"} unités
                  </dd>
                </div>
                <div>
                  <dt>Début</dt>
                  <dd>{instant(active.startsAt)}</dd>
                </div>
                <div>
                  <dt>Fin exclue</dt>
                  <dd>{instant(active.endsAt)}</dd>
                </div>
                <div>
                  <dt>Cumul avec une campagne</dt>
                  <dd>{active.allowCampaigns ? "Autorisé" : "Interdit"}</dd>
                </div>
                <div>
                  <dt>Version</dt>
                  <dd>
                    {active.version} · {states[active.state]}
                  </dd>
                </div>
              </dl>
              {active.note && <p>{active.note}</p>}
              {active.withdrawnAt && (
                <p className="notice">
                  Retrait effectif : {instant(active.withdrawnAt)} ·{" "}
                  {active.withdrawalReason || "Remplacement programmé"}.
                </p>
              )}
              {active.status === "DRAFT" ? (
                <form
                  className="action-form"
                  onSubmit={(e) => {
                    e.preventDefault();
                    const d = new FormData(e.currentTarget);
                    void run(async () => {
                      await approve(
                        session,
                        "/admin/pricing/rules/" + active.id + "/review",
                        {
                          version: active.version,
                          decision: d.get("decision"),
                          reason: d.get("reason"),
                        },
                      );
                    }, "Décision enregistrée par l’approbateur.");
                  }}
                >
                  <p>
                    {session.workspace.isDemo && admin
                      ? "L’approbation utilise le compte Finance distinct de cet espace de démonstration."
                      : "L’auteur du tarif ne peut pas approuver sa propre proposition."}
                  </p>
                  <div className="form-grid">
                    <label>
                      Décision
                      <select name="decision">
                        <option value="PUBLISH">Publier ces conditions</option>
                        <option value="REJECT">Rejeter la proposition</option>
                      </select>
                    </label>
                    <label>
                      Motif de la décision
                      <input name="reason" required maxLength={1000} />
                    </label>
                  </div>
                  <button
                    className="button"
                    disabled={
                      !session.workspace.isDemo &&
                      active.createdBy === session.user.id
                    }
                  >
                    Enregistrer la décision
                    {session.workspace.isDemo && admin ? " avec Finance" : ""}
                  </button>
                </form>
              ) : active.status === "PUBLISHED" &&
                active.state !== "RETIRED" &&
                active.state !== "EXPIRED" ? (
                <>
                  <div className="pricing-actions">
                    {admin && (
                      <button
                        className="button secondary"
                        onClick={() => {
                          setForm(active);
                          window.requestAnimationFrame(() =>
                            document
                              .querySelector(".pricing-rule-form")
                              ?.scrollIntoView({
                                behavior: "smooth",
                                block: "start",
                              }),
                          );
                        }}
                      >
                        Proposer un remplacement
                      </button>
                    )}
                  </div>
                  <details>
                    <summary>Retirer ce tarif</summary>
                    <form
                      className="action-form"
                      onSubmit={(e) => {
                        e.preventDefault();
                        const d = new FormData(e.currentTarget);
                        void run(
                          () =>
                            approve(
                              session,
                              "/admin/pricing/rules/" + active.id + "/retire",
                              {
                                version: active.version,
                                reason: d.get("retireReason"),
                              },
                            ),
                          "Retrait enregistré. Les devis déjà proposés conservent leurs conditions.",
                        );
                      }}
                    >
                      <p>
                        Une autre personne habilitée doit valider le retrait.
                        Les commandes et les versions de devis existantes sont
                        conservées.
                      </p>
                      <label>
                        Motif du retrait
                        <input name="retireReason" maxLength={1000} required />
                      </label>
                      <button
                        className="button secondary"
                        disabled={
                          !session.workspace.isDemo &&
                          active.createdBy === session.user.id
                        }
                      >
                        Confirmer le retrait
                        {session.workspace.isDemo && admin
                          ? " avec Finance"
                          : ""}
                      </button>
                    </form>
                  </details>
                </>
              ) : null}
            </section>
          )}
        </section>
        <PricingSimulation merchants={data.merchants} />
      </div>
      <section className="panel module-section">
        <div className="panel-heading">
          <div>
            <h2>Segments commerciaux</h2>
            <p>
              Un seul segment par magasin. Toute modification conserve son
              motif.
            </p>
          </div>
        </div>
        {admin ? (
          <div className="pricing-segments">
            <form
              className="action-form"
              onSubmit={(e) => {
                e.preventDefault();
                const form = e.currentTarget;
                const d = new FormData(form);
                void run(async () => {
                  await post("/admin/pricing/segments", {
                    name: d.get("segmentName"),
                  });
                  form.reset();
                }, "Segment créé.");
              }}
            >
              <h3>Créer un groupe</h3>
              <label>
                Nom du segment
                <input
                  name="segmentName"
                  required
                  maxLength={100}
                  placeholder="Ex. Épiceries de proximité"
                />
              </label>
              <button className="button secondary">Créer le segment</button>
            </form>
            <form
              className="action-form"
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                const merchantId = String(d.get("merchantId"));
                const assignment = data.assignments.find(
                  (a: any) => a.merchantId === merchantId,
                );
                void run(
                  () =>
                    post("/admin/pricing/assignments/" + merchantId, {
                      segmentId: d.get("segmentId") || null,
                      version: assignment?.version ?? 0,
                      reason: d.get("assignmentReason"),
                    }),
                  "Affectation du magasin enregistrée.",
                );
              }}
            >
              <h3>Affecter un magasin</h3>
              <div className="form-grid">
                <label>
                  Magasin
                  <select name="merchantId" required defaultValue="">
                    <option value="">Choisir un magasin</option>
                    {data.merchants.map((m: any) => (
                      <option key={m.id} value={m.id}>
                        {m.shopName} ·{" "}
                        {data.segments.find(
                          (s: any) =>
                            s.id ===
                            data.assignments.find(
                              (a: any) => a.merchantId === m.id,
                            )?.segmentId,
                        )?.name || "Sans segment"}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Segment
                  <select name="segmentId">
                    <option value="">Sans segment</option>
                    {data.segments
                      .filter((s: any) => s.isActive)
                      .map((s: any) => (
                        <option key={s.id} value={s.id}>
                          {s.name}
                        </option>
                      ))}
                  </select>
                </label>
              </div>
              <label>
                Motif de l’affectation
                <input name="assignmentReason" required maxLength={1000} />
              </label>
              <button className="button secondary">
                Enregistrer l’affectation
              </button>
            </form>
          </div>
        ) : (
          <p>
            {data.segments.length
              ? data.segments.map((s: any) => s.name).join(" · ")
              : "Aucun segment défini."}{" "}
            Les affectations sont gérées par le grossiste.
          </p>
        )}
      </section>
      <div className="notice">
        <strong>Les versions déjà proposées restent figées.</strong>
        <p>
          Un nouveau tarif ou une nouvelle campagne ne recalcule pas une
          commande existante. Le commerçant accepte une version précise du
          devis.
        </p>
      </div>
      <details className="panel module-section">
        <summary>
          Historique tarifaire · {data.events.length} derniers événements
        </summary>
        {data.events.length ? (
          <ul className="pricing-events">
            {data.events.slice(0, 30).map((event: any) => (
              <li key={event.id}>
                <strong>
                  {(
                    {
                      PRICE_SEGMENT_CREATED: "Segment créé",
                      MERCHANT_PRICE_SEGMENT_CHANGED: "Affectation modifiée",
                      TARIFF_PROPOSED: "Tarif proposé",
                      TARIFF_PUBLISHED: "Tarif publié",
                      TARIFF_REJECTED: "Proposition rejetée",
                      TARIFF_WITHDRAWN: "Tarif retiré",
                      TARIFF_REPLACEMENT_SCHEDULED: "Remplacement programmé",
                    } as Record<string, string>
                  )[event.kind] || event.kind}
                </strong>
                <span>
                  {date(event.at)} · Compte …{event.actorId.slice(-8)}
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p>Les décisions apparaîtront ici après votre première action.</p>
        )}
      </details>
    </div>
  );
}

function TariffForm({
  data,
  previous,
  run,
  done,
}: {
  data: any;
  previous: any;
  run: Run;
  done: (id: string) => void;
}) {
  const [scope, setScope] = useState(previous?.scope || "MERCHANT");
  const idempotency = useRef(key());
  return (
    <form
      className="action-form pricing-rule-form"
      onSubmit={(e) => {
        e.preventDefault();
        const d = new FormData(e.currentTarget);
        void run(async () => {
          const rule = await post<any>("/admin/pricing/rules", {
            productId: previous?.productId || d.get("productId"),
            scope,
            targetId: previous?.targetId || d.get("targetId") || "",
            minimumQuantity: Number(d.get("minimumQuantity")),
            maximumQuantity: d.get("maximumQuantity")
              ? Number(d.get("maximumQuantity"))
              : null,
            unitPriceMinor: Math.round(Number(d.get("price")) * 100),
            startsAt: new Date(String(d.get("startsAt"))).toISOString(),
            endsAt: new Date(String(d.get("endsAt"))).toISOString(),
            allowCampaigns: d.get("allowCampaigns") === "on",
            reference: d.get("reference"),
            note: d.get("note"),
            idempotencyKey: idempotency.current,
            supersedesId: previous?.id || null,
            supersedesVersion: previous?.version ?? null,
          });
          done(rule.id);
        }, "Proposition créée. Une autre personne doit approuver ces conditions.");
      }}
    >
      <h3>
        {previous
          ? "Remplacer " + previous.reference
          : "Nouvelle proposition tarifaire"}
      </h3>
      {previous ? (
        <p>
          {previous.productName} · {scopes[previous.scope]} · Le périmètre et la
          tranche restent identiques. Le tarif actuel demeure valable jusqu’au
          début du remplacement publié.
        </p>
      ) : (
        <ProductPicker label="Produit à tarifer" />
      )}
      <div className="form-grid">
        <label>
          Portée
          <select
            value={scope}
            onChange={(e) => setScope(e.target.value)}
            disabled={!!previous}
          >
            <option value="MERCHANT">Contrat du magasin</option>
            <option value="SEGMENT">Segment commercial</option>
            <option value="STANDARD">Barème standard</option>
          </select>
        </label>
        {scope !== "STANDARD" && !previous && (
          <label>
            {scope === "MERCHANT" ? "Magasin concerné" : "Segment concerné"}
            <select name="targetId" required defaultValue="">
              <option value="">Choisir</option>
              {(scope === "MERCHANT"
                ? data.merchants
                : data.segments.filter((s: any) => s.isActive)
              ).map((item: any) => (
                <option value={item.id} key={item.id}>
                  {item.shopName || item.name}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>
      <div className="form-grid">
        <label>
          Minimum · unités
          <input
            name="minimumQuantity"
            type="number"
            min={1}
            max={100000}
            required
            defaultValue={previous?.minimumQuantity || 1}
            readOnly={!!previous}
          />
        </label>
        <label>
          Maximum inclus · unités
          <input
            name="maximumQuantity"
            type="number"
            min={1}
            max={100000}
            defaultValue={previous?.maximumQuantity ?? ""}
            placeholder="Sans maximum"
            readOnly={!!previous}
          />
        </label>
        <label>
          Prix de base / unité · DA
          <input
            name="price"
            type="number"
            min="0.01"
            max="100000000"
            step="0.01"
            defaultValue={previous ? previous.unitPriceMinor / 100 : ""}
            required
          />
        </label>
        <label>
          Référence de l’accord
          <input
            name="reference"
            required
            maxLength={180}
            placeholder="Référence commerciale interne"
          />
        </label>
        <label>
          Début · heure locale
          <input
            name="startsAt"
            type="datetime-local"
            defaultValue={localInput(Date.now())}
            required
          />
        </label>
        <label>
          Fin exclue · heure locale
          <input
            name="endsAt"
            type="datetime-local"
            defaultValue={localInput(Date.now() + 30 * 86400000)}
            required
          />
        </label>
      </div>
      <label className="pricing-checkbox">
        <input
          type="checkbox"
          name="allowCampaigns"
          defaultChecked={previous?.allowCampaigns || false}
        />
        Autoriser le cumul avec une campagne éligible
      </label>
      <label>
        Note de l’accord
        <textarea name="note" maxLength={1000} rows={2} />
      </label>
      <p className="muted">
        Les heures locales sont converties en UTC. Deux règles de même portée ne
        peuvent pas couvrir la même quantité pendant la même période.
      </p>
      <button className="button">Soumettre à l’approbation</button>
    </form>
  );
}

function PricingSimulation({ merchants }: { merchants: any[] }) {
  const [result, setResult] = useState<any>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const attempt = useRef(0);
  return (
    <section className="panel module-section pricing-simulation">
      <h2>Simuler un panier</h2>
      <p>
        Les règles publiées, la campagne retenue et les frais connus, pour un
        magasin précis.
      </p>
      <form
        className="action-form"
        onChange={() => {
          attempt.current++;
          setResult(null);
          setError("");
          setBusy(false);
        }}
        onSubmit={async (e) => {
          e.preventDefault();
          const id = ++attempt.current;
          const d = new FormData(e.currentTarget);
          setBusy(true);
          setError("");
          try {
            const value = await post("/pricing/simulate", {
              merchantId: d.get("merchantId"),
              items: [
                {
                  productId: d.get("simulationProduct"),
                  quantity: Number(d.get("simulationQuantity")),
                },
              ],
              at: d.get("simulationAt")
                ? new Date(String(d.get("simulationAt"))).toISOString()
                : null,
            });
            if (attempt.current === id) setResult(value);
          } catch (e: any) {
            if (attempt.current === id) setError(e.message);
          } finally {
            if (attempt.current === id) setBusy(false);
          }
        }}
      >
        <label>
          Magasin à simuler
          <select name="merchantId" required defaultValue="">
            <option value="">Choisir un magasin</option>
            {merchants.map((m) => (
              <option key={m.id} value={m.id}>
                {m.shopName}
              </option>
            ))}
          </select>
        </label>
        <ProductPicker name="simulationProduct" label="Produit à simuler" />
        <label>
          Quantité · unités
          <input
            name="simulationQuantity"
            type="number"
            min={1}
            max={100000}
            defaultValue={1}
            required
          />
        </label>
        <label>
          À la date · heure locale
          <input name="simulationAt" type="datetime-local" />
          <small>
            Vide : maintenant. Les stocks restent ceux connus aujourd’hui.
          </small>
        </label>
        <button className="button secondary" disabled={busy}>
          {busy ? "Calcul en cours…" : "Simuler les conditions"}
        </button>
      </form>
      {error && (
        <div role="alert" className="error-banner">
          {error}
        </div>
      )}
      {result && (
        <div className="pricing-simulation-result" aria-live="polite">
          <h3>Conditions calculées</h3>
          {result.items.map((item: any) => (
            <dl className="pricing-terms" key={item.productId}>
              <div>
                <dt>Origine</dt>
                <dd>{scopes[item.price.source]}</dd>
              </div>
              <div>
                <dt>Prix de base / unité</dt>
                <dd>{amount(item.price.unitPriceMinor)}</dd>
              </div>
              <div>
                <dt>Prix retenu / unité</dt>
                <dd>{money(item.unitPrice)}</dd>
              </div>
              {item.promotion?.campaignId && (
                <div>
                  <dt>Campagne retenue</dt>
                  <dd>
                    {item.promotion.campaignTitle}
                    {item.promotion.kind === "FREE_GOODS"
                      ? ` · ${item.promotion.giftQuantity} × ${item.promotion.giftProductName}`
                      : ""}
                  </dd>
                </div>
              )}
              <div>
                <dt>Marchandises</dt>
                <dd>{money(item.lineTotal)}</dd>
              </div>
            </dl>
          ))}
          <div className="pricing-total">
            <span>Total avec livraison</span>
            <strong>{money(result.quote.total)}</strong>
          </div>
          {result.quote.needsQuote && (
            <p className="notice">
              Prix, stock, conditionnement, cadeau ou livraison à confirmer. Ce
              panier nécessite un devis.
            </p>
          )}
          <p className="muted">{result.notice}</p>
        </div>
      )}
    </section>
  );
}

export function QuotePricingControls({
  order,
  useTariffs,
  setUseTariffs,
}: {
  order: any;
  useTariffs: boolean;
  setUseTariffs: (value: boolean) => void;
}) {
  const [preview, setPreview] = useState<any>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  return (
    <div className="quote-pricing-controls">
      <label>
        Origine des prix
        <select
          name="pricingMode"
          aria-label="Origine des prix"
          value={useTariffs ? "TARIFF" : "MANUAL"}
          onChange={(e) => setUseTariffs(e.target.value === "TARIFF")}
        >
          <option value="MANUAL">Saisir une proposition explicite</option>
          <option value="TARIFF">
            Appliquer les tarifs publiés du magasin
          </option>
        </select>
      </label>
      <button
        type="button"
        className="button secondary"
        disabled={busy}
        onClick={async (e) => {
          const form = e.currentTarget.form;
          if (!form) return;
          const d = new FormData(form);
          setBusy(true);
          setError("");
          try {
            setPreview(
              await post("/pricing/simulate", {
                merchantId: order.merchant.id,
                items: order.items
                  .map((l: any) => ({
                    productId: l.productId,
                    quantity: Number(d.get("qty-" + l.productId)),
                  }))
                  .filter((l: any) => l.quantity > 0),
              }),
            );
          } catch (e: any) {
            setError(e.message);
          } finally {
            setBusy(false);
          }
        }}
      >
        Consulter les tarifs actuels
      </button>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {preview && (
        <div className="notice">
          <strong>Référence actuelle · simulation</strong>
          <ul>
            {preview.items.map((item: any) => (
              <li key={item.productId}>
                {
                  order.items.find((l: any) => l.productId === item.productId)
                    ?.name
                }{" "}
                : {item.quantity} unités à {amount(item.price.unitPriceMinor)} /
                unité · {scopes[item.price.source]}
              </li>
            ))}
          </ul>
          <p>
            Référence indicative. Les prix seront vérifiés à l’envoi de cette
            nouvelle version.
          </p>
        </div>
      )}
      {!useTariffs && (
        <label>
          Motif d’un écart au tarif publié
          <textarea
            name="pricingOverrideReason"
            maxLength={1000}
            rows={2}
            placeholder="Obligatoire si le prix négocié diffère d’un tarif publié (8 caractères minimum)."
          />
        </label>
      )}
    </div>
  );
}

export function FrozenPricing({
  order,
  revision,
}: {
  order: any;
  revision: number;
}) {
  const [history, setHistory] = useState<any>(null),
    [error, setError] = useState("");
  const [open, setOpen] = useState(false);
  useEffect(() => {
    if (!open) return;
    let active = true;
    api("/orders/" + order.id + "/pricing-history?pageSize=100")
      .then((value) => {
        if (active) {
          setHistory(value);
          setError("");
        }
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [order.id, revision, open]);
  return (
    <details
      className="pricing-frozen"
      onToggle={(e) => setOpen(e.currentTarget.open)}
    >
      <summary>Origine & versions des prix</summary>
      <p>{order.pricing?.notice}</p>
      {error && (
        <p role="alert" className="error-banner">
          {error}
        </p>
      )}
      {history ? (
        history.items.length ? (
          <>
            <div className="table-scroll">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Version / produit</th>
                    <th>Origine</th>
                    <th>Base / unité</th>
                    <th>Retenu / unité</th>
                  </tr>
                </thead>
                <tbody>
                  {history.items.map((item: any) => (
                    <tr key={item.id}>
                      <td>
                        v{item.quoteVersion} ·{" "}
                        {
                          order.items.find(
                            (l: any) => l.productId === item.productId,
                          )?.name
                        }
                        <small>
                          {item.quantity} unités · {date(item.resolvedAt)}
                        </small>
                        {item.overrideReason && (
                          <small>{item.overrideReason}</small>
                        )}
                      </td>
                      <td>{scopes[item.source] || item.source}</td>
                      <td>{amount(item.baseUnitPriceMinor)}</td>
                      <td>{amount(item.appliedUnitPriceMinor)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {history.total > history.items.length && (
              <p>
                {history.items.length} dernières lignes sur {history.total}.
                L’historique complet est conservé.
              </p>
            )}
          </>
        ) : (
          <p>
            Aucune provenance structurée pour les versions antérieures à cette
            fonctionnalité.
          </p>
        )
      ) : (
        <p role="status">Chargement de l’historique…</p>
      )}
    </details>
  );
}
