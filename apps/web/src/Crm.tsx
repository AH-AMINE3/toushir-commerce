import { useEffect, useRef, useState } from "react";
import { api, post, key, money, labels } from "./api";
import type { Session } from "./api";
import { DownloadList } from "./OperationsDashboard";

const instant = (value: string) =>
  new Date(value).toLocaleString("fr-DZ", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Africa/Algiers",
    hourCycle: "h23",
  });
const localTime = (hours = 0) => {
  const d = new Date(Date.now() + hours * 3600000);
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
};
const utc = (value: string) => new Date(value).toISOString();
const statusNames: Record<string, string> = {
  AVAILABLE: "À transmettre",
  USED: "Demande transmise",
  REVOKED: "Révoquée",
  EXPIRED: "Expirée",
  AUTHORITY_ENDED: "Accès modifié",
  ADDRESS_CHANGED: "Adresse modifiée",
};
const outcomeNames: Record<string, string> = {
  RESPONDED: "Répondu",
  CALL_BACK: "À rappeler",
  UNAVAILABLE: "Indisponible",
  VISIT_COMPLETED: "Visite effectuée · déclarée",
  VISIT_MISSED: "Visite non effectuée",
};
const states: Record<string, string> = {
  OPEN: "À faire",
  DONE: "Terminé",
  CANCELLED: "Annulé",
};

export function Crm({
  revision,
  session,
  query,
  onOrder,
  initialShopId = "",
}: {
  revision: number;
  session: Session;
  query: string;
  onOrder: (id: string) => void;
  initialShopId?: string;
}) {
  const [data, setData] = useState<any>(null),
    [shopId, setShopId] = useState(initialShopId),
    [dossier, setDossier] = useState<any>(null);
  const [error, setError] = useState(""),
    [formError, setFormError] = useState(""),
    [form, setForm] = useState<any>(null),
    [busy, setBusy] = useState(false),
    [filter, setFilter] = useState("ALL"),
    [notice, setNotice] = useState(""),
    [localQuery, setLocalQuery] = useState(""),
    [region, setRegion] = useState(""),
    [advisorFilter, setAdvisorFilter] = useState("");
  const pending = useRef(false),
    heading = useRef<HTMLHeadingElement>(null),
    requestKeys = useRef<Record<string, string>>({}),
    selectedShop = useRef(initialShopId),
    loadVersion = useRef(0);
  const clearInaccessibleShop = () => {
    ++loadVersion.current;
    selectedShop.current = "";
    setShopId("");
    setDossier(null);
    setForm(null);
    setFormError("");
    setError("");
    setNotice(
      "L’accès à ce magasin a changé. Sélectionnez un magasin de votre portefeuille actuel.",
    );
  };
  async function load() {
    const version = ++loadVersion.current,
      selected = selectedShop.current;
    try {
      const overview = await api("/crm");
      if (version !== loadVersion.current) return;
      setData(overview);
      setError("");
      if (!selected) return;
      if (!overview.shops.some((shop: any) => shop.id === selected)) {
        clearInaccessibleShop();
        return;
      }
      const detail = await api("/crm/shops/" + selected);
      if (version === loadVersion.current && selectedShop.current === selected)
        setDossier(detail);
    } catch (e: any) {
      if (version !== loadVersion.current) return;
      if (selected && [401, 403, 404].includes(e.status)) {
        clearInaccessibleShop();
        return;
      }
      setError(e.message);
      throw e;
    }
  }
  useEffect(() => {
    void load().catch(() => {});
    return () => {
      ++loadVersion.current;
    };
  }, [shopId, revision]);
  useEffect(() => {
    if (form) heading.current?.focus();
  }, [form?.type, form?.id, dossier?.shop.id]);
  async function save(action: () => Promise<unknown>, message: string) {
    if (pending.current) return;
    pending.current = true;
    setBusy(true);
    setFormError("");
    setNotice("");
    try {
      await action();
      setForm(null);
      setNotice(message);
      try {
        await load();
      } catch (e: any) {
        setError(
          "Enregistrement effectué. Actualisez les données : " + e.message,
        );
      }
    } catch (e: any) {
      setFormError(e.message);
    } finally {
      pending.current = false;
      setBusy(false);
    }
  }
  const open = (value: any) => {
    setFormError("");
    setNotice("");
    setForm(value);
  };
  const chooseShop = (id: string) => {
    setForm(null);
    setFormError("");
    if (id !== shopId) {
      ++loadVersion.current;
      selectedShop.current = id;
      setDossier(null);
      setShopId(id);
    }
  };
  const field = (name: string, value: string) =>
    setForm((old: any) => ({ ...old, [name]: value }));
  if (!data)
    return (
      <div
        className={error ? "error-banner" : "boot-inline"}
        role={error ? "alert" : "status"}
      >
        {error || "Chargement du suivi commercial…"}
      </div>
    );
  const regions = [
    ...new Set<string>(data.shops.map((s: any) => s.region).filter(Boolean)),
  ].sort();
  const shops = data.shops.filter(
    (s: any) =>
      (!region || s.region === region) &&
      (!advisorFilter || s.portfolio?.advisorId === advisorFilter) &&
      [query, localQuery].every(
        (term) =>
          !term ||
          `${s.name} ${s.contact} ${s.phone} ${s.region || ""} ${s.commune || ""}`
            .toLocaleLowerCase("fr")
            .includes(term.toLocaleLowerCase("fr")),
      ),
  );
  const tasks = data.tasks.filter(
    (t: any) =>
      (!shopId || t.shopId === shopId) &&
      (filter !== "VISIT" || t.kind === "VISIT") &&
      shops.some((s: any) => s.id === t.shopId),
  );
  const advisorName = (id: string) =>
    data.advisors.find((a: any) => a.id === id)?.name ||
    (id === session.user.id ? session.user.name : "Conseiller précédent");
  const startTask = (kind = "CONTACT") =>
    open({
      type: "task",
      kind,
      title: "",
      priority: "NORMAL",
      dueAt: localTime(1),
      visitEnd: localTime(2),
    });
  const finish = (task: any) => {
    if (shopId !== task.shopId) {
      ++loadVersion.current;
      selectedShop.current = task.shopId;
      setDossier(null);
      setShopId(task.shopId);
    }
    open({
      type: "finish",
      id: task.id,
      version: task.version,
      kind: task.kind,
      title: task.title,
      outcome: task.kind === "VISIT" ? "VISIT_COMPLETED" : "RESPONDED",
      note: "",
      nextAction: "",
      observedAt: localTime(),
      followUpAt: "",
    });
  };
  return (
    <fieldset
      className="crm-workspace"
      disabled={busy}
      aria-busy={busy}
      aria-label="Actions du suivi commercial"
    >
      {error && (
        <div className="error-banner" role="alert">
          {error}
          <button
            className="button secondary"
            onClick={() => {
              setError("");
              void load().catch((e) => setError(e.message));
            }}
          >
            Réessayer
          </button>
        </div>
      )}
      {notice && (
        <div className="notice" role="status">
          {notice}
        </div>
      )}
      <div className="directory-toolbar">
        <label>
          Rechercher dans le portefeuille
          <input
            value={localQuery}
            onChange={(e) => setLocalQuery(e.target.value)}
            placeholder="Magasin, téléphone, commune…"
            aria-label="Rechercher un magasin dans le CRM"
          />
        </label>
        <label>
          Région
          <select
            value={region}
            onChange={(e) => setRegion(e.target.value)}
            aria-label="Région du portefeuille"
          >
            <option value="">Toutes les régions</option>
            {regions.map((r) => (
              <option key={r}>{r}</option>
            ))}
          </select>
        </label>
        <label>
          Conseiller
          <select
            value={advisorFilter}
            onChange={(e) => setAdvisorFilter(e.target.value)}
          >
            <option value="">Tous les conseillers</option>
            {data.advisors.map((advisor: any) => (
              <option value={advisor.id} key={advisor.id}>
                {advisor.name}
              </option>
            ))}
          </select>
        </label>
        <DownloadList rows={shops} name="portefeuille-toushir" />
      </div>
      <div className="crm-toolbar">
        <label className="field">
          <span>Magasin du portefeuille</span>
          <select value={shopId} onChange={(e) => chooseShop(e.target.value)}>
            <option value="">Tous les magasins autorisés</option>
            {shopId && !shops.some((s: any) => s.id === shopId) && (
              <option value={shopId}>
                {data.shops.find((s: any) => s.id === shopId)?.name} · sélection
                actuelle
              </option>
            )}
            {shops.map((s: any) => (
              <option key={s.id} value={s.id}>
                {s.name}
                {s.portfolio ? "" : " · à affecter"}
              </option>
            ))}
          </select>
        </label>
        <div
          className="pricing-filters"
          role="group"
          aria-label="Type de suivi"
        >
          <button
            aria-pressed={filter === "ALL"}
            className={filter === "ALL" ? "active" : ""}
            onClick={() => setFilter("ALL")}
          >
            Tous les suivis
          </button>
          <button
            aria-pressed={filter === "VISIT"}
            className={filter === "VISIT" ? "active" : ""}
            onClick={() => setFilter("VISIT")}
          >
            Visites
          </button>
        </div>
      </div>
      <div className="crm-columns">
        <section className="panel crm-tasks" aria-labelledby="crm-tasks-title">
          <div className="panel-heading">
            <div>
              <h2 id="crm-tasks-title">Les prochaines actions</h2>
              <p>
                Responsable, échéance et suite à donner · {data.totalOpenTasks}{" "}
                tâche{data.totalOpenTasks !== 1 ? "s" : ""} ouverte
                {data.totalOpenTasks !== 1 ? "s" : ""}.
              </p>
            </div>
            {dossier?.portfolio && (
              <button className="button secondary" onClick={() => startTask()}>
                Planifier un suivi
              </button>
            )}
          </div>
          {!tasks.length ? (
            <div className="crm-empty">
              <h3>
                {data.shops.length
                  ? "Une prochaine action claire."
                  : "Votre portefeuille commence ici."}
              </h3>
              <p>
                {data.shops.length
                  ? "Choisissez un magasin, puis planifiez un contact ou une visite. Un compte rendu garde le résultat et la suite à donner."
                  : "Un administrateur doit vous affecter un magasin. Ses contacts, visites et demandes autorisées apparaîtront ici."}
              </p>
            </div>
          ) : (
            <div className="crm-task-list">
              {tasks.map((t: any) => (
                <article key={t.id} className="crm-task-row">
                  <div>
                    <span
                      className={
                        "badge " +
                        (t.priority === "HIGH" ? "warning" : "neutral")
                      }
                    >
                      {t.priority === "HIGH" ? "Haute" : "Normale"}
                    </span>
                    <small>{t.kind === "VISIT" ? "Visite" : "Contact"}</small>
                  </div>
                  <div>
                    <button
                      className="crm-text-button"
                      onClick={() => chooseShop(t.shopId)}
                    >
                      {data.shops.find((s: any) => s.id === t.shopId)?.name ||
                        "Magasin autorisé"}
                    </button>
                    <p>{t.title}</p>
                    <small>
                      {advisorName(t.assignedTo)} · {instant(t.dueAt)}
                    </small>
                  </div>
                  <button
                    className="button secondary"
                    onClick={() => finish(t)}
                  >
                    Consigner le résultat
                  </button>
                </article>
              ))}
            </div>
          )}
          {data.totalOpenTasks > 100 && (
            <p className="crm-footnote">
              Les 100 premières tâches sont affichées. Ouvrez le dossier d'un
              magasin pour consulter ses suivis.
            </p>
          )}
        </section>
        <aside
          className="panel crm-qualification"
          aria-labelledby="crm-qualification-title"
        >
          <h2 id="crm-qualification-title">Qualifier, puis agir</h2>
          <p>Une note n’est jamais une commande.</p>
          {!shopId ? (
            <div className="crm-empty">
              <h3>Choisissez un magasin</h3>
              <p>
                Retrouvez son conseiller et ses actions. Toute demande assistée
                exige une autorisation explicite du magasin.
              </p>
            </div>
          ) : !dossier ? (
            <p role="status">Chargement du dossier…</p>
          ) : (
            <>
              <div className="crm-shop-heading">
                <h3>{dossier.shop.name}</h3>
                <p>
                  {dossier.shop.contact} ·{" "}
                  {dossier.shop.phone || "Téléphone non renseigné"}
                </p>
                <span className="badge neutral">
                  {dossier.portfolio
                    ? dossier.portfolio.advisorName
                    : "Conseiller à affecter"}
                </span>
              </div>
              {dossier.shop.region && (
                <p className="directory-region">
                  {dossier.shop.region}
                  {dossier.shop.commune ? " · " + dossier.shop.commune : ""}
                </p>
              )}
              <div className="crm-actions">
                {data.canAssign && (
                  <button
                    className="button secondary"
                    onClick={() =>
                      open({
                        type: "portfolio",
                        version: dossier.portfolio?.version || 0,
                        advisorId:
                          dossier.portfolio?.advisorId ||
                          data.advisors[0]?.id ||
                          "",
                        reason: "",
                      })
                    }
                  >
                    {dossier.portfolio
                      ? "Changer de conseiller"
                      : "Affecter un conseiller"}
                  </button>
                )}
                {dossier.portfolio && (
                  <>
                    <button
                      className="button secondary"
                      onClick={() => startTask("VISIT")}
                    >
                      Planifier une visite
                    </button>
                    <button
                      className="button"
                      onClick={() =>
                        open({
                          type: "contact",
                          kind: "CONTACT",
                          outcome: "RESPONDED",
                          note: "",
                          nextAction: "",
                          observedAt: localTime(),
                          followUpAt: "",
                        })
                      }
                    >
                      Consigner un contact
                    </button>
                  </>
                )}
              </div>
              <p className="crm-footnote">
                Contacts et visites déclarés par la personne connectée. Aucun
                appel externe ni présence physique n’est vérifié par cet écran.
              </p>
            </>
          )}
        </aside>
      </div>
      {form && dossier && (
        <section
          className="panel crm-form-panel"
          aria-labelledby="crm-form-title"
        >
          <h2 id="crm-form-title" ref={heading} tabIndex={-1}>
            {
              (
                {
                  portfolio: "Le bon conseiller pour ce magasin",
                  task: "Planifier la prochaine action",
                  reschedule: "Replanifier le suivi",
                  finish: "Le résultat et la suite",
                  contact: "Consigner un contact",
                  cancel: "Annuler ce suivi",
                  use: "Transmettre cette demande de devis",
                } as Record<string, string>
              )[form.type]
            }
          </h2>
          <p>
            {dossier.shop.name}
            {form.title ? " · " + form.title : ""}
          </p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (form.type === "portfolio")
                void save(
                  () =>
                    post("/crm/shops/" + shopId + "/portfolio", {
                      advisorId: form.advisorId,
                      version: form.version,
                      reason: form.reason,
                    }),
                  "Portefeuille affecté. Les anciennes autorisations inutilisées sont révoquées.",
                );
              else if (form.type === "task" || form.type === "reschedule")
                void save(
                  () =>
                    post(
                      form.type === "task"
                        ? "/crm/shops/" + shopId + "/tasks"
                        : "/crm/tasks/" + form.id + "/reschedule",
                      {
                        kind: form.kind,
                        title: form.title,
                        priority: form.priority,
                        dueAt: utc(form.dueAt),
                        visitEnd:
                          form.kind === "VISIT" ? utc(form.visitEnd) : null,
                        version: form.version,
                        reason: form.reason,
                      },
                    ),
                  "Suivi enregistré avec son responsable et son échéance.",
                );
              else if (form.type === "contact" || form.type === "finish")
                void save(
                  () =>
                    post(
                      form.type === "contact"
                        ? "/crm/shops/" + shopId + "/contacts"
                        : "/crm/tasks/" + form.id + "/finish",
                      {
                        version: form.version,
                        kind: form.kind,
                        outcome: form.outcome,
                        note: form.note,
                        nextAction: form.nextAction,
                        observedAt: utc(form.observedAt),
                        followUpAt: form.followUpAt
                          ? utc(form.followUpAt)
                          : null,
                      },
                    ),
                  "Compte rendu conservé. La prochaine action est enregistrée séparément.",
                );
              else if (form.type === "cancel")
                void save(
                  () =>
                    post("/crm/tasks/" + form.id + "/cancel", {
                      version: form.version,
                      reason: form.reason,
                    }),
                  "Suivi annulé ; son historique est conservé.",
                );
              else if (form.type === "use")
                void save(
                  () =>
                    post("/crm/assistance/" + form.id + "/use", {
                      version: form.version,
                      idempotencyKey: (requestKeys.current[form.id] ||= key()),
                    }),
                  "Demande de devis transmise. Le magasin devra encore accepter le devis chiffré.",
                );
            }}
          >
            <fieldset
              disabled={busy}
              aria-busy={busy}
              className="crm-form-fields"
            >
              {form.type === "portfolio" && (
                <label className="field">
                  <span>Conseiller responsable</span>
                  <select
                    value={form.advisorId}
                    required
                    onChange={(e) => field("advisorId", e.target.value)}
                  >
                    {data.advisors.map((a: any) => (
                      <option key={a.id} value={a.id}>
                        {a.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {["task", "reschedule"].includes(form.type) && (
                <div className="form-grid">
                  <label className="field">
                    <span>Type de suivi</span>
                    <select
                      value={form.kind}
                      disabled={form.type === "reschedule"}
                      onChange={(e) => field("kind", e.target.value)}
                    >
                      <option value="CONTACT">Contact</option>
                      <option value="VISIT">Visite</option>
                    </select>
                  </label>
                  <label className="field">
                    <span>Objet du suivi</span>
                    <input
                      value={form.title}
                      maxLength={160}
                      required
                      onChange={(e) => field("title", e.target.value)}
                    />
                  </label>
                  <label className="field">
                    <span>Priorité</span>
                    <select
                      value={form.priority}
                      onChange={(e) => field("priority", e.target.value)}
                    >
                      <option value="NORMAL">Normale</option>
                      <option value="HIGH">Haute</option>
                    </select>
                  </label>
                  <label className="field">
                    <span>
                      {form.kind === "VISIT" ? "Début du créneau" : "Échéance"}
                    </span>
                    <input
                      type="datetime-local"
                      value={form.dueAt}
                      required
                      onChange={(e) => field("dueAt", e.target.value)}
                    />
                  </label>
                  {form.kind === "VISIT" && (
                    <label className="field">
                      <span>Fin du créneau</span>
                      <input
                        type="datetime-local"
                        value={form.visitEnd}
                        required
                        onChange={(e) => field("visitEnd", e.target.value)}
                      />
                    </label>
                  )}
                  <p className="crm-footnote">
                    Responsable : {dossier.portfolio?.advisorName} · Heures
                    locales du navigateur.
                  </p>
                </div>
              )}
              {["finish", "contact"].includes(form.type) && (
                <>
                  <div className="form-grid">
                    {form.type === "contact" && (
                      <label className="field">
                        <span>Type de compte rendu</span>
                        <select
                          value={form.kind}
                          onChange={(e) =>
                            setForm({
                              ...form,
                              kind: e.target.value,
                              outcome:
                                e.target.value === "VISIT"
                                  ? "VISIT_COMPLETED"
                                  : "RESPONDED",
                            })
                          }
                        >
                          <option value="CONTACT">Contact</option>
                          <option value="VISIT">Visite déclarée</option>
                        </select>
                      </label>
                    )}
                    <label className="field">
                      <span>Issue du contact</span>
                      <select
                        value={form.outcome}
                        onChange={(e) => field("outcome", e.target.value)}
                      >
                        {(form.kind === "VISIT"
                          ? ["VISIT_COMPLETED", "VISIT_MISSED"]
                          : ["RESPONDED", "CALL_BACK", "UNAVAILABLE"]
                        ).map((v) => (
                          <option key={v} value={v}>
                            {outcomeNames[v]}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="field">
                      <span>Observation déclarée à</span>
                      <input
                        type="datetime-local"
                        required
                        value={form.observedAt}
                        onChange={(e) => field("observedAt", e.target.value)}
                      />
                    </label>
                  </div>
                  <label className="field">
                    <span>Compte rendu</span>
                    <textarea
                      required
                      maxLength={1500}
                      value={form.note}
                      onChange={(e) => field("note", e.target.value)}
                    />
                  </label>
                  <label className="field">
                    <span>Prochaine action</span>
                    <input
                      required
                      maxLength={300}
                      value={form.nextAction}
                      onChange={(e) => field("nextAction", e.target.value)}
                    />
                  </label>
                  <label className="field">
                    <span>Échéance du rappel · facultative</span>
                    <input
                      type="datetime-local"
                      value={form.followUpAt}
                      onChange={(e) => field("followUpAt", e.target.value)}
                    />
                  </label>
                  <p className="crm-footnote">
                    Un rappel crée une tâche distincte. Ce compte rendu ne
                    valide aucune commande ou réception.
                  </p>
                </>
              )}
              {["portfolio", "reschedule", "cancel"].includes(form.type) && (
                <label className="field">
                  <span>Motif du changement</span>
                  <textarea
                    required
                    minLength={8}
                    maxLength={800}
                    value={form.reason || ""}
                    onChange={(e) => field("reason", e.target.value)}
                  />
                </label>
              )}
              {form.type === "use" && (
                <>
                  <AssistanceSummary grant={form} />
                  <div className="notice">
                    Cette autorisation nomme {form.advisorName}. Les produits,
                    quantités et l’adresse sont figés par {form.issuerName}.
                    Aucune acceptation du devis ni réservation n’est créée par
                    cet envoi.
                  </div>
                </>
              )}
              {formError && (
                <div className="error-banner" role="alert">
                  {formError}
                </div>
              )}

              <div className="crm-actions">
                <button className="button" type="submit">
                  {busy
                    ? "Enregistrement…"
                    : form.type === "use"
                      ? "Transmettre la demande de devis"
                      : form.type === "cancel"
                        ? "Confirmer l’annulation du suivi"
                        : form.type === "portfolio"
                          ? "Affecter ce conseiller"
                          : ["contact", "finish"].includes(form.type)
                            ? "Enregistrer le compte rendu"
                            : "Enregistrer le suivi"}
                </button>
                <button
                  className="button secondary"
                  type="button"
                  onClick={() => setForm(null)}
                >
                  Fermer le formulaire
                </button>
              </div>
            </fieldset>
          </form>
        </section>
      )}
      {dossier && (
        <div className="crm-dossier-grid">
          <section className="panel">
            <div className="panel-heading">
              <div>
                <h2>Demandes assistées</h2>
                <p>Autorisation du magasin · une seule demande de devis.</p>
              </div>
            </div>
            {!dossier.assistance.length ? (
              <div className="crm-empty">
                <h3>L’accord vient du magasin.</h3>
                <p>
                  Le propriétaire ou un responsable prépare les produits dans
                  son app et autorise son conseiller. Aucune demande n’est créée
                  à partir d’une note libre.
                </p>
              </div>
            ) : (
              dossier.assistance.map((g: any) => (
                <article key={g.id} className="crm-record">
                  <div className="crm-record-heading">
                    <h3>
                      {g.issuerName || "Compte du magasin"} →{" "}
                      {g.advisorName || "Conseiller"}
                    </h3>
                    <span className="badge neutral">
                      {statusNames[g.status]}
                    </span>
                  </div>
                  <AssistanceSummary grant={g} />
                  {g.status === "AVAILABLE" &&
                    g.advisorId === session.user.id && (
                      <button
                        className="button"
                        onClick={() => open({ ...g, type: "use" })}
                      >
                        Vérifier avant de transmettre
                      </button>
                    )}
                  {g.orderId && (
                    <p>
                      {session.user.role === "ADMIN" ? (
                        <button
                          className="button secondary"
                          onClick={() => onOrder(g.orderId)}
                        >
                          Ouvrir la demande liée
                        </button>
                      ) : (
                        "La demande liée reste consultable dans les commandes du magasin ci-dessous."
                      )}
                    </p>
                  )}
                </article>
              ))
            )}
          </section>
          <section className="panel">
            <div className="panel-heading">
              <div>
                <h2>Contacts et visites</h2>
                <p>Comptes rendus conservés · dernières observations.</p>
              </div>
            </div>
            {!dossier.contacts.length ? (
              <p className="crm-empty">
                Aucun contact consigné pour ce magasin.
              </p>
            ) : (
              dossier.contacts.map((c: any) => (
                <article key={c.id} className="crm-record">
                  <h3>{outcomeNames[c.outcome]}</h3>
                  <small>
                    {instant(c.observedAt)} ·{" "}
                    {c.actorName ||
                      (c.actorId === session.user.id
                        ? session.user.name
                        : "Compte du dossier")}
                  </small>
                  <p>{c.note}</p>
                  <strong>Suite : {c.nextAction}</strong>
                </article>
              ))
            )}
          </section>
          <section className="panel">
            <div className="panel-heading">
              <div>
                <h2>Historique des suivis</h2>
                <p>Les tâches terminées et annulées restent visibles.</p>
              </div>
            </div>
            {!dossier.tasks.length ? (
              <p className="crm-empty">Aucun suivi enregistré.</p>
            ) : (
              dossier.tasks.map((t: any) => (
                <article className="crm-record" key={t.id}>
                  <div className="crm-record-heading">
                    <h3>{t.title}</h3>
                    <span className="badge neutral">{states[t.status]}</span>
                  </div>
                  <p>
                    {t.kind === "VISIT" ? "Visite" : "Contact"} ·{" "}
                    {instant(t.dueAt)}
                  </p>
                  <small>{advisorName(t.assignedTo)}</small>
                  {t.status === "OPEN" && (
                    <div className="crm-actions">
                      <button
                        className="button secondary"
                        onClick={() => finish(t)}
                      >
                        Consigner le résultat
                      </button>
                      <button
                        className="button secondary"
                        onClick={() => {
                          const local = (value: string) => {
                            const d = new Date(value);
                            return new Date(
                              d.getTime() - d.getTimezoneOffset() * 60000,
                            )
                              .toISOString()
                              .slice(0, 16);
                          };
                          open({
                            ...t,
                            type: "reschedule",
                            dueAt: local(t.dueAt),
                            visitEnd: t.visitEnd
                              ? local(t.visitEnd)
                              : localTime(2),
                            reason: "",
                          });
                        }}
                      >
                        Replanifier
                      </button>
                      <button
                        className="button secondary"
                        onClick={() =>
                          open({ ...t, type: "cancel", reason: "" })
                        }
                      >
                        Annuler ce suivi
                      </button>
                    </div>
                  )}
                </article>
              ))
            )}
          </section>
          <section className="panel">
            <div className="panel-heading">
              <div>
                <h2>Commandes du magasin</h2>
                <p>
                  Les 20 dossiers les plus récents · aucun encaissement depuis
                  le CRM.
                </p>
              </div>
            </div>
            {!dossier.orders.length ? (
              <p className="crm-empty">Aucune demande enregistrée.</p>
            ) : (
              dossier.orders.map((o: any) => (
                <article className="crm-record" key={o.id}>
                  <h3>{o.number}</h3>
                  <p>
                    {labels[o.status] || o.status} · {money(o.total)}
                  </p>
                  <small>{instant(o.createdAt)}</small>
                  {session.user.role === "ADMIN" && (
                    <button
                      className="button secondary"
                      onClick={() => onOrder(o.id)}
                    >
                      Ouvrir le dossier
                    </button>
                  )}
                </article>
              ))
            )}
          </section>
        </div>
      )}
    </fieldset>
  );
}

function AssistanceSummary({ grant }: { grant: any }) {
  return (
    <div className="crm-assistance-summary">
      <ul>
        {grant.items.map((i: any) => (
          <li key={i.productId}>
            <span>{i.name}</span>
            <strong>
              {i.quantity} unité{i.quantity !== 1 ? "s" : ""}
            </strong>
          </li>
        ))}
      </ul>
      <p>
        {grant.address.label} · {grant.address.street}, {grant.address.commune},{" "}
        {grant.address.wilaya}
      </p>
      {grant.note && <p>{grant.note}</p>}
      <small>
        Valable jusqu’au {instant(grant.expiresAt)} · Prix à confirmer dans le
        devis.
      </small>
    </div>
  );
}
