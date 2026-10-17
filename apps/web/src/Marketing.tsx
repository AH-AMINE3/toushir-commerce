import { useEffect, useState } from "react";
import { api, post, patch, key, date, money } from "./api";
import type { Session } from "./api";
import { approve, ProductPicker } from "./Business";
import type { Run } from "./Business";
const kindLabel: Record<string, string> = {
  DISCOUNT: "Remise immédiate",
  FREE_GOODS: "Cadeau sur achat",
  CHALLENGE: "Challenge",
};
export function Campaigns({
  revision,
  run,
  session,
  orders,
}: {
  revision: number;
  run: Run;
  session: Session;
  orders: any[];
}) {
  const [campaigns, setCampaigns] = useState<any[]>([]),
    [rewards, setRewards] = useState<any[]>([]),
    [shops, setShops] = useState<any[]>([]),
    [creating, setCreating] = useState(false),
    [kind, setKind] = useState("DISCOUNT"),
    [active, setActive] = useState<any>(null),
    [preview, setPreview] = useState<any>(null),
    [error, setError] = useState("");
  useEffect(() => {
    Promise.all([
      api("/admin/campaigns"),
      api("/rewards"),
      session.user.role === "ADMIN"
        ? api("/admin/merchants?pageSize=100")
        : Promise.resolve({
            items: [
              ...new Map(
                orders.map((o) => [o.merchant.id, o.merchant]),
              ).values(),
            ],
          }),
    ])
      .then(([c, r, m]) => {
        setCampaigns(c.items);
        setRewards(r.items);
        setShops(m.items);
        setActive((old: any) =>
          old ? c.items.find((x: any) => x.id === old.id) : null,
        );
      })
      .catch((e) => setError(e.message));
  }, [revision]);
  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      <div className="panel-heading">
        <p>
          Remises, gratuités et challenges : trois mécanismes distincts, des
          règles vérifiables.
        </p>
        {session.user.role === "ADMIN" && (
          <button className="button" onClick={() => setCreating(!creating)}>
            {creating ? "Fermer le formulaire" : "Nouvelle campagne"}
          </button>
        )}
      </div>
      <div className="campaign-grid">
        {campaigns
          .filter((c) => c.status === "PUBLISHED")
          .slice(0, 3)
          .map((c) => (
            <button
              className={"campaign-card " + c.kind.toLowerCase()}
              key={c.id}
              onClick={() => {
                setActive(c);
                setPreview(null);
              }}
            >
              <small>{kindLabel[c.kind]}</small>
              <strong>
                {c.kind === "DISCOUNT"
                  ? `−${c.discountPercent} %`
                  : c.kind === "FREE_GOODS"
                    ? `${c.minimumQuantity} + ${c.giftQuantity}`
                    : c.goalQuantity}
              </strong>
              <h3>{c.title}</h3>
              <p>
                {date(c.startsAt)} — {date(c.endsAt)}
              </p>
              <span>Voir les règles ↗</span>
            </button>
          ))}
      </div>
      {creating && (
        <section className="panel module-section">
          <h2>Définir des conditions explicites</h2>
          <form
            className="action-form"
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(async () => {
                const c = await post("/admin/campaigns", {
                  title: d.get("title"),
                  description: d.get("description"),
                  kind,
                  productId: d.get("productId"),
                  minimumQuantity: Number(d.get("minimum")),
                  discountPercent: Number(d.get("percent") || 0),
                  giftProductId: d.get("giftProductId") || null,
                  giftQuantity: Number(d.get("giftQuantity") || 1),
                  goalQuantity: Number(d.get("goal") || 0),
                  unitFactor: Number(d.get("factor") || 1),
                  rewardId: d.get("reward") || null,
                  awardLimit: Number(d.get("awardLimit") || 1),
                  startsAt: new Date(String(d.get("start"))).toISOString(),
                  endsAt: new Date(String(d.get("end"))).toISOString(),
                  claimUntil: d.get("claim")
                    ? new Date(String(d.get("claim"))).toISOString()
                    : null,
                });
                setActive(c);
                setCreating(false);
              }, "Brouillon créé. Les conditions doivent être revues par une autre personne habilitée.");
            }}
          >
            <div className="form-grid">
              <label>
                Nom de la campagne
                <input name="title" maxLength={180} required />
              </label>
              <label>
                Mécanisme
                <select value={kind} onChange={(e) => setKind(e.target.value)}>
                  <option value="DISCOUNT">Remise immédiate</option>
                  <option value="FREE_GOODS">
                    Gratuité physique sur achat
                  </option>
                  <option value="CHALLENGE">
                    Challenge sur unités acceptées et réglées
                  </option>
                </select>
              </label>
            </div>
            <label>
              Conditions à présenter au commerçant
              <textarea name="description" required maxLength={3000} />
            </label>
            <ProductPicker />
            <div className="form-grid">
              <label>
                Seuil de quantité
                <input
                  name="minimum"
                  type="number"
                  min="1"
                  max="1000000"
                  defaultValue="5"
                  required
                />
              </label>
              {kind === "DISCOUNT" && (
                <label>
                  Remise · %
                  <input
                    name="percent"
                    type="number"
                    min="0.01"
                    max="100"
                    step="0.01"
                    required
                  />
                </label>
              )}
              {kind === "FREE_GOODS" && (
                <label>
                  Quantité offerte
                  <input
                    name="giftQuantity"
                    type="number"
                    min="1"
                    max="1000"
                    defaultValue="1"
                    required
                  />
                </label>
              )}
            </div>
            {kind === "FREE_GOODS" && (
              <ProductPicker name="giftProductId" label="Produit offert" />
            )}
            {kind === "CHALLENGE" && (
              <>
                <div className="form-grid">
                  <label>
                    Objectif en unités éligibles
                    <input name="goal" type="number" min="1" required />
                  </label>
                  <label>
                    Unités par carton
                    <input
                      name="factor"
                      type="number"
                      min="1"
                      max="1000"
                      defaultValue="1"
                      required
                    />
                  </label>
                  <label>
                    Dotation
                    <select name="reward" required>
                      <option value="">Choisir une récompense</option>
                      {rewards.map((r) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Nombre d’attributions maximum
                    <input
                      name="awardLimit"
                      type="number"
                      min="1"
                      defaultValue="1"
                      required
                    />
                  </label>
                </div>
                <label>
                  Réclamation possible jusqu’au
                  <input name="claim" type="datetime-local" required />
                </label>
              </>
            )}
            <div className="form-grid">
              <label>
                Début
                <input name="start" type="datetime-local" required />
              </label>
              <label>
                Fin
                <input name="end" type="datetime-local" required />
              </label>
            </div>
            <button type="submit" className="button">
              Enregistrer le brouillon
            </button>
          </form>
        </section>
      )}
      <section className="panel module-section">
        <h2>Campagnes et responsabilités</h2>
        <p>
          Les conditions publiées restent immuables. Les devis acceptés
          conservent leur version.
        </p>
        {campaigns.length ? (
          <div className="table-wrap module-section">
            <table>
              <thead>
                <tr>
                  <th>Campagne</th>
                  <th>Mécanisme</th>
                  <th>Période</th>
                  <th>Seuil</th>
                  <th>État</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {campaigns.map((c) => (
                  <tr
                    key={c.id}
                    onClick={() => {
                      setActive(c);
                      setPreview(null);
                    }}
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") setActive(c);
                    }}
                  >
                    <td>
                      <strong>{c.title}</strong>
                      <small>Version {c.version}</small>
                    </td>
                    <td>{kindLabel[c.kind]}</td>
                    <td>
                      {date(c.startsAt)} — {date(c.endsAt)}
                    </td>
                    <td>{c.minimumQuantity}</td>
                    <td>
                      <span
                        className={
                          "badge " +
                          (c.status === "PUBLISHED" ? "green" : "neutral")
                        }
                      >
                        {c.status === "PUBLISHED" ? "Publiée" : "Brouillon"}
                      </span>
                    </td>
                    <td>Consulter les règles ↗</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="empty">
            <p>
              Créez votre première campagne après avoir confirmé ses conditions
              et son financement.
            </p>
          </div>
        )}
      </section>
      {active && (
        <section className="panel module-section">
          <div className="panel-heading">
            <div>
              <h2>{active.title}</h2>
              <p>{active.description}</p>
            </div>
            <button
              className="button secondary"
              onClick={() => setActive(null)}
            >
              Fermer les règles
            </button>
          </div>
          <dl className="detail-list">
            <div>
              <dt>Mécanisme</dt>
              <dd>{kindLabel[active.kind]}</dd>
            </div>
            <div>
              <dt>Seuil d’éligibilité</dt>
              <dd>{active.minimumQuantity} unités</dd>
            </div>
            <div>
              <dt>Conditions</dt>
              <dd>
                {active.kind === "DISCOUNT"
                  ? `${active.discountPercent} % de remise`
                  : active.kind === "FREE_GOODS"
                    ? `${active.giftQuantity} unité(s) gratuite(s), une dotation par commande`
                    : `${active.goalQuantity} unités acceptées et réglées`}
              </dd>
            </div>
          </dl>
          {active.status === "DRAFT" ? (
            <div className="note">
              <strong>Relecture indépendante</strong>
              <p>
                Une autre personne habilitée doit valider les règles. La
                publication ne modifie aucune commande déjà acceptée.
              </p>
              <button
                className="button module-section"
                onClick={() =>
                  void run(
                    () =>
                      approve(
                        session,
                        "/admin/campaigns/" + active.id + "/publish",
                        { version: active.version },
                      ),
                    "Campagne revue et publiée. Ses conditions sont visibles dans l’app.",
                  )
                }
              >
                {session.workspace.isDemo
                  ? "Publier avec l’approbateur Finance de démonstration"
                  : "Approuver et publier"}
              </button>
            </div>
          ) : (
            <form
              className="action-form"
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                void run(
                  async () =>
                    setPreview(
                      await post("/campaigns/" + active.id + "/preview", {
                        quantity: Number(d.get("qty")),
                      }),
                    ),
                  "Calcul d’éligibilité effectué par le serveur.",
                );
              }}
            >
              <h3>Vérifier l’éligibilité</h3>
              <label>
                Quantité envisagée
                <input
                  name="qty"
                  type="number"
                  min="1"
                  defaultValue={active.minimumQuantity}
                  required
                />
              </label>
              <button className="button secondary" type="submit">
                Calculer l’avantage
              </button>
              {preview && (
                <div className="note">
                  <strong>
                    {preview.active && preview.qualifies
                      ? "Conditions remplies"
                      : "Conditions non remplies"}
                  </strong>
                  <p>
                    {money(preview.goodsBefore)} avant remise ·{" "}
                    {money(preview.discount)} de remise · {preview.freeQuantity}{" "}
                    unité(s) offerte(s)
                  </p>
                </div>
              )}
            </form>
          )}
          {active.kind === "CHALLENGE" && active.status === "PUBLISHED" && (
            <form
              className="action-form"
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                void run(
                  () =>
                    post("/admin/campaigns/" + active.id + "/award", {
                      merchantId: d.get("merchant"),
                    }),
                  "Attribution vérifiée et enregistrée sans débit de points.",
                );
              }}
            >
              <h3>Valider une attribution après la campagne</h3>
              <label>
                Commerce
                <select name="merchant" required>
                  <option value="">Choisir un commerce</option>
                  {shops.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.shopName}
                    </option>
                  ))}
                </select>
              </label>
              <button className="button" type="submit">
                Vérifier l’objectif et attribuer
              </button>
            </form>
          )}
        </section>
      )}
    </>
  );
}
export function Rewards({
  revision,
  run,
  session,
  orders,
}: {
  revision: number;
  run: Run;
  session: Session;
  orders: any[];
}) {
  const [rewards, setRewards] = useState<any[]>([]),
    [gifts, setGifts] = useState<any[]>([]),
    [shops, setShops] = useState<any[]>([]),
    [merchant, setMerchant] = useState(""),
    [wallet, setWallet] = useState<any>(null),
    [creating, setCreating] = useState(false),
    [selected, setSelected] = useState<any>(null),
    [error, setError] = useState("");
  useEffect(() => {
    Promise.all([
      session.user.role === "WAREHOUSE"
        ? Promise.resolve({ items: [] })
        : api("/rewards"),
      api("/gift-requests"),
      session.user.role === "ADMIN"
        ? api("/admin/merchants?pageSize=100")
        : Promise.resolve({
            items: [
              ...new Map(
                orders.map((o) => [o.merchant.id, o.merchant]),
              ).values(),
            ],
          }),
    ])
      .then(([r, g, m]) => {
        setRewards(r.items);
        setGifts(g.items);
        setShops(m.items);
        if (!merchant && m.items.length) setMerchant(m.items[0].id);
        setSelected((old: any) =>
          old ? g.items.find((x: any) => x.id === old.id) : null,
        );
      })
      .catch((e) => setError(e.message));
  }, [revision]);
  useEffect(() => {
    if (merchant && session.user.role !== "WAREHOUSE")
      api("/loyalty?merchantId=" + merchant)
        .then(setWallet)
        .catch((e) => setError(e.message));
  }, [merchant, revision]);
  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      {session.user.role !== "WAREHOUSE" && (
        <div className="panel-heading">
          <label className="field">
            Commerce
            <select
              value={merchant}
              onChange={(e) => setMerchant(e.target.value)}
            >
              <option value="">Choisir un commerce</option>
              {shops.map((m) => (
                <option value={m.id} key={m.id}>
                  {m.shopName || m.name}
                </option>
              ))}
            </select>
          </label>
          <div className="heading-actions">
            <button
              className="button secondary"
              disabled={!merchant}
              onClick={() =>
                void run(
                  () => post("/loyalty/sync", { merchantId: merchant }),
                  "Points rapprochés avec les marchandises acceptées et réglées.",
                )
              }
            >
              Rapprocher les points
            </button>
            {session.user.role === "ADMIN" && (
              <button className="button" onClick={() => setCreating(!creating)}>
                Ajouter une récompense
              </button>
            )}
          </div>
        </div>
      )}
      {wallet && (
        <>
          <div className="stats">
            <div className="stat mint">
              <span>Disponibles</span>
              <strong>{wallet.available}</strong>
              <small>Utilisables pour une nouvelle réservation</small>
            </div>
            <div className="stat">
              <span>Réservés</span>
              <strong>{wallet.reserved}</strong>
              <small>Cadeaux réservés, pas encore remis</small>
            </div>
            <div className="stat">
              <span>Dépensés</span>
              <strong>{wallet.spent}</strong>
              <small>Remises effectivement prouvées</small>
            </div>
            <div className="stat">
              <span>Régularisation à examiner</span>
              <strong>{wallet.correctionDue}</strong>
              <small>Corrections après retours ou avoirs</small>
            </div>
          </div>
          <section className="panel">
            <h2>Journal immuable des points</h2>
            <p>{wallet.rule}</p>
            {wallet.entries.length ? (
              <div className="table-wrap module-section">
                <table>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Événement</th>
                      <th>Source</th>
                      <th>Disponibles</th>
                      <th>Réservés</th>
                      <th>Dépensés</th>
                      <th>Régularisation</th>
                    </tr>
                  </thead>
                  <tbody>
                    {wallet.entries.map((l: any) => (
                      <tr key={l.id}>
                        <td>{date(l.at, true)}</td>
                        <td>{l.kind.replaceAll("_", " ")}</td>
                        <td>{l.sourceRef}</td>
                        <td>
                          {l.availableDelta > 0 ? "+" : ""}
                          {l.availableDelta}
                        </td>
                        <td>
                          {l.reservedDelta > 0 ? "+" : ""}
                          {l.reservedDelta}
                        </td>
                        <td>{l.spentDelta}</td>
                        <td>{l.correctionDueDelta}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="empty">
                <p>
                  Les points proviennent de marchandises réceptionnées et
                  réglées. Lancez le rapprochement pour calculer les écritures.
                </p>
              </div>
            )}
          </section>
        </>
      )}
      {creating && (
        <section className="panel module-section">
          <h2>Catalogue de récompenses</h2>
          <form
            className="action-form"
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(async () => {
                await post("/admin/rewards", {
                  name: d.get("name"),
                  description: d.get("description"),
                  imageUrl: d.get("image") || null,
                  pointsCost: Number(d.get("cost")),
                  availableStock: Number(d.get("stock")),
                  isActive: true,
                });
                setCreating(false);
              }, "Récompense publiée dans l’application commerçant.");
            }}
          >
            <div className="form-grid">
              <label>
                Récompense
                <input name="name" required maxLength={180} />
              </label>
              <label>
                Coût en points
                <input name="cost" type="number" min="1" required />
              </label>
              <label>
                Stock physique disponible
                <input name="stock" type="number" min="0" required />
              </label>
              <label>
                Image · URL HTTPS ou chemin local
                <input name="image" />
              </label>
            </div>
            <label>
              Conditions
              <textarea name="description" required maxLength={2000} />
            </label>
            <button className="button" type="submit">
              Publier la récompense
            </button>
          </form>
        </section>
      )}
      <section className="panel module-section">
        <h2>Demandes de cadeaux</h2>
        <p>
          Les points et le stock sont réservés une seule fois. La dépense
          définitive suit la remise prouvée.
        </p>
        {gifts.length ? (
          <div className="table-wrap module-section">
            <table>
              <thead>
                <tr>
                  <th>Cadeau</th>
                  <th>Commerce</th>
                  <th>Points réservés</th>
                  <th>Date</th>
                  <th>État</th>
                  <th>Preuve</th>
                </tr>
              </thead>
              <tbody>
                {gifts.map((g) => (
                  <tr
                    key={g.id}
                    onClick={() => setSelected(g)}
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") setSelected(g);
                    }}
                  >
                    <td>
                      <strong>{g.rewardName}</strong>
                      <small>{g.id.slice(0, 12)}</small>
                    </td>
                    <td>
                      {shops.find((m) => m.id === g.merchantId)?.shopName ||
                        "Commerce"}
                    </td>
                    <td>{g.pointsCost}</td>
                    <td>{date(g.createdAt, true)}</td>
                    <td>
                      <span
                        className={
                          "badge " +
                          (g.status === "FULFILLED" ? "green" : "neutral")
                        }
                      >
                        {
                          (
                            {
                              RESERVED: "Réservé",
                              READY: "Prêt à remettre",
                              FULFILLED: "Remis",
                              CANCELLED: "Annulé",
                            } as any
                          )[g.status]
                        }
                      </span>
                    </td>
                    <td>{g.proofReference || "À enregistrer"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="empty">
            <p>Les réservations faites dans l’app apparaîtront ici.</p>
          </div>
        )}
        {selected &&
          ["RESERVED", "READY"].includes(selected.status) &&
          ["ADMIN", "WAREHOUSE"].includes(session.user.role) && (
            <form
              className="action-form"
              onSubmit={(e) => {
                e.preventDefault();
                const d = new FormData(e.currentTarget);
                void run(
                  () =>
                    post(
                      "/admin/gift-requests/" + selected.id + "/transition",
                      {
                        version: selected.version,
                        status: d.get("status"),
                        proofReference: d.get("proof") || null,
                        reason: d.get("reason") || null,
                      },
                    ),
                  "État de cadeau enregistré avec sa preuve.",
                );
              }}
            >
              <h3>{selected.rewardName} · traiter la demande</h3>
              <div className="form-grid">
                <label>
                  Prochaine action
                  <select name="status">
                    {selected.status === "RESERVED" && (
                      <option value="READY">Préparer le cadeau</option>
                    )}
                    {selected.status === "READY" && (
                      <option value="FULFILLED">
                        Confirmer la remise physique
                      </option>
                    )}
                    <option value="CANCELLED">
                      Annuler et libérer la réservation
                    </option>
                  </select>
                </label>
                <label>
                  Référence de preuve de remise
                  <input name="proof" maxLength={300} />
                </label>
              </div>
              <label>
                Motif d’annulation / observation
                <input name="reason" maxLength={1000} />
              </label>
              <button className="button" type="submit">
                Enregistrer cette action
              </button>
            </form>
          )}
      </section>
      {session.user.role !== "WAREHOUSE" && (
        <section className="panel module-section">
          <h2>Récompenses disponibles dans l’app</h2>
          <div className="reward-grid">
            {rewards.map((r) => (
              <article key={r.id}>
                {r.imageUrl && <img src={r.imageUrl} alt={r.name} />}
                <div>
                  <h3>{r.name}</h3>
                  <p>{r.description}</p>
                  <strong>{r.pointsCost} points</strong>
                  <small>{r.availableStock} disponible(s)</small>
                  {session.user.role === "ADMIN" && (
                    <button
                      className="button secondary"
                      onClick={() =>
                        void run(
                          () =>
                            patch("/admin/rewards/" + r.id, {
                              ...r,
                              isActive: !r.isActive,
                            }),
                          "Disponibilité de récompense modifiée.",
                        )
                      }
                    >
                      {r.isActive ? "Mettre en pause" : "Réactiver"}
                    </button>
                  )}
                </div>
              </article>
            ))}
          </div>
          {!rewards.length && (
            <div className="empty">
              <p>
                Ajoutez des récompenses pour permettre aux commerces d’utiliser
                leurs points.
              </p>
            </div>
          )}
        </section>
      )}
    </>
  );
}
