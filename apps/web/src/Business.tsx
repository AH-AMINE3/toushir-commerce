import { useEffect, useState } from "react";
import { api, post, patch, key, date, money } from "./api";
import type { Session } from "./api";
export type Run = (f: () => Promise<unknown>, m?: string) => Promise<void>;
export async function approve(session: Session, path: string, body: unknown) {
  if (!session.workspace.isDemo) return post(path, body);
  const workspaceToken = localStorage.getItem("toushir-demo-workspace");
  if (!workspaceToken)
    throw new Error(
      "Rouvrez votre espace de démonstration pour utiliser son approbateur.",
    );
  const reviewer = await post<Session>("/demo/session", {
    role: "FINANCE",
    workspaceToken,
  });
  return api(path, {
    method: "POST",
    body: JSON.stringify(body),
    headers: { Authorization: "Bearer " + reviewer.token },
  });
}
export function ProductPicker({
  name = "productId",
  label = "Produit",
  required = true,
}: {
  name?: string;
  label?: string;
  required?: boolean;
}) {
  const [q, setQ] = useState(""),
    [data, setData] = useState<any[]>([]);
  useEffect(() => {
    let active = true;
    const t = setTimeout(() => {
      api("/products?pageSize=50&q=" + encodeURIComponent(q)).then((d) => {
        if (active) setData(d.items);
      });
    }, 200);
    return () => {
      active = false;
      clearTimeout(t);
    };
  }, [q]);
  return (
    <label>
      {label}
      <input
        aria-label={"Rechercher " + label.toLowerCase()}
        placeholder="Rechercher un nom ou code-barres…"
        value={q}
        onChange={(e) => setQ(e.target.value)}
      />
      <select name={name} required={required} defaultValue="">
        <option value="">Choisir une référence</option>
        {data.map((p) => (
          <option key={p.id} value={p.id}>
            {p.name} · {p.barcode}
          </option>
        ))}
      </select>
    </label>
  );
}
export function Messages({
  revision,
  run,
  team,
  session,
  orders,
}: {
  revision: number;
  run: Run;
  team: any[];
  session: Session;
  orders: any[];
}) {
  const [threads, setThreads] = useState<any[]>([]),
    [shops, setShops] = useState<any[]>([]),
    [active, setActive] = useState(""),
    [messages, setMessages] = useState<any>(null),
    [broadcast, setBroadcast] = useState(false),
    [newThread, setNewThread] = useState(false),
    [broadcasts, setBroadcasts] = useState<any[]>([]),
    [error, setError] = useState("");
  useEffect(() => {
    Promise.all([
      api("/conversations"),
      session.user.role === "ADMIN"
        ? api("/admin/merchants?pageSize=100")
        : Promise.resolve({
            items: [
              ...new Map(
                orders.map((o) => [o.merchant.id, o.merchant]),
              ).values(),
            ],
          }),
      session.user.role === "ADMIN"
        ? api("/admin/broadcasts")
        : Promise.resolve({ items: [] }),
    ])
      .then(([c, m, b]) => {
        setThreads(c.items);
        setShops(m.items);
        setBroadcasts(b.items);
        if (!active && c.items.length) setActive(c.items[0].id);
      })
      .catch((e) => setError(e.message));
  }, [revision]);
  useEffect(() => {
    if (active)
      api("/conversations/" + active + "/messages")
        .then(setMessages)
        .catch((e) => setError(e.message));
  }, [active, revision]);
  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      <div className="panel-heading">
        <div />
        <div className="heading-actions">
          <button
            className="button secondary"
            onClick={() => setNewThread(!newThread)}
          >
            Nouvelle conversation
          </button>
          {session.user.role === "ADMIN" && (
            <button className="button" onClick={() => setBroadcast(!broadcast)}>
              Diffuser un message
            </button>
          )}
        </div>
      </div>
      {newThread && (
        <form
          className="panel action-form"
          onSubmit={(e) => {
            e.preventDefault();
            const d = new FormData(e.currentTarget);
            void run(async () => {
              const c = await post("/conversations", {
                contextType: "GENERAL",
                subject: d.get("subject"),
                merchantId: d.get("merchant"),
                contextId: null,
              });
              setActive(c.id);
              setNewThread(false);
            }, "Conversation ouverte dans la messagerie du commerce.");
          }}
        >
          <h3>Ouvrir une conversation</h3>
          <label>
            Commerce
            <select name="merchant" required>
              <option value="">Choisir un commerce</option>
              {shops.map((m) => (
                <option value={m.id} key={m.id}>
                  {m.shopName}
                </option>
              ))}
            </select>
          </label>
          <label>
            Sujet
            <input name="subject" required maxLength={180} />
          </label>
          <button className="button" type="submit">
            Ouvrir la conversation
          </button>
        </form>
      )}
      <div className="inbox-grid">
        <section className="panel inbox-list">
          <h2>Conversations</h2>
          <p>{threads.filter((t) => t.status === "OPEN").length} ouvertes</p>
          {threads.map((t) => (
            <button
              key={t.id}
              className={active === t.id ? "selected" : ""}
              onClick={() => setActive(t.id)}
            >
              <span className="avatar">
                {(t.merchantName || t.subject || "TS")
                  .slice(0, 2)
                  .toUpperCase()}
              </span>
              <span>
                <strong>{t.merchantName || t.subject}</strong>
                <small>{t.subject}</small>
                <small>
                  {date(t.updatedAt, true)} ·{" "}
                  {t.status === "OPEN" ? "Ouverte" : "Clôturée"}
                </small>
              </span>
            </button>
          ))}
          {!threads.length && (
            <div className="empty">
              <p>
                Les conversations liées aux commandes et aux commerces
                apparaîtront ici.
              </p>
            </div>
          )}
        </section>
        <section className="panel chat-panel">
          {messages && active ? (
            <>
              <div className="panel-heading">
                <div>
                  <h2>{messages.conversation.subject}</h2>
                  <p>
                    {threads.find((t) => t.id === active)?.merchantName} ·{" "}
                    {messages.deliveryChannel === "IN_APP"
                      ? "Messagerie de l’application"
                      : ""}
                  </p>
                </div>
                <button
                  className="button secondary"
                  onClick={() =>
                    void run(
                      () =>
                        patch("/conversations/" + active, {
                          version: messages.conversation.version,
                          status:
                            messages.conversation.status === "OPEN"
                              ? "CLOSED"
                              : "OPEN",
                          assignedTo: messages.conversation.assignedTo || null,
                        }),
                      "État de conversation enregistré.",
                    )
                  }
                >
                  {messages.conversation.status === "OPEN"
                    ? "Clôturer"
                    : "Rouvrir"}
                </button>
              </div>
              <div className="chat-messages">
                {messages.items.map((m: any) => (
                  <article
                    key={m.id}
                    className={m.internal ? "internal" : "message"}
                  >
                    <small>
                      {m.senderName} · {date(m.at, true)}
                      {m.internal ? " · Note interne" : ""}
                    </small>
                    <p>{m.text}</p>
                  </article>
                ))}
                {!messages.items.length && (
                  <p className="metrics-note">
                    Écrivez le premier message pour commencer cet échange.
                  </p>
                )}
              </div>
              {messages.conversation.status === "OPEN" && (
                <form
                  className="chat-compose"
                  onSubmit={(e) => {
                    e.preventDefault();
                    const form = e.currentTarget,
                      d = new FormData(form);
                    void run(async () => {
                      await post("/conversations/" + active + "/messages", {
                        text: d.get("text"),
                        internal: d.get("internal") === "on",
                        idempotencyKey: key(),
                      });
                      form.reset();
                    }, "Message enregistré dans la conversation.");
                  }}
                >
                  <label>
                    Votre message
                    <textarea
                      name="text"
                      placeholder="Écrire au magasin…"
                      maxLength={5000}
                      required
                    />
                  </label>
                  <div>
                    <label className="checkbox">
                      <input type="checkbox" name="internal" />
                      Note interne · visible par l’équipe
                    </label>
                    <button className="button" type="submit">
                      Envoyer dans l’app
                    </button>
                  </div>
                </form>
              )}
            </>
          ) : (
            <div className="empty">
              <p>
                Sélectionnez une conversation ou ouvrez un échange avec un
                commerce.
              </p>
            </div>
          )}
        </section>
        <section className="panel inbox-context">
          <h2>Le contexte utile</h2>
          <p>
            Un message ne modifie ni le prix, ni les quantités, ni l’accord du
            commerçant.
          </p>
          {messages && (
            <>
              <dl className="detail-list">
                <div>
                  <dt>Contexte</dt>
                  <dd>{messages.conversation.contextType}</dd>
                </div>
                <div>
                  <dt>État</dt>
                  <dd>
                    {messages.conversation.status === "OPEN"
                      ? "Ouverte"
                      : "Clôturée"}
                  </dd>
                </div>
              </dl>
              <label className="field">
                Responsable
                <select
                  value={messages.conversation.assignedTo || ""}
                  onChange={(e) =>
                    void run(
                      () =>
                        patch("/conversations/" + active, {
                          version: messages.conversation.version,
                          status: messages.conversation.status,
                          assignedTo: e.target.value || null,
                        }),
                      "Responsable affecté.",
                    )
                  }
                >
                  <option value="">Non affecté</option>
                  {team.map((t) => (
                    <option value={t.id} key={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </label>
              <div className="note">
                <strong>Lu ≠ accepté</strong>
                <p>
                  L’accord commercial porte sur la version de la proposition
                  dans le dossier de commande.
                </p>
              </div>
            </>
          )}
        </section>
      </div>
      {broadcast && (
        <section className="panel module-section">
          <h2>Message aux magasins</h2>
          <p>
            Les communications promotionnelles respectent le consentement de
            chaque commerce.
          </p>
          <form
            className="action-form"
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(
                () =>
                  post("/admin/broadcasts", {
                    purpose: d.get("purpose"),
                    title: d.get("title"),
                    body: d.get("body"),
                    merchantIds: d.getAll("merchants"),
                    idempotencyKey: key(),
                  }),
                "Brouillon créé. Vérifiez-le avant de le diffuser.",
              );
            }}
          >
            <label>
              Objet
              <input name="title" required maxLength={180} />
            </label>
            <label>
              Finalité
              <select name="purpose">
                <option value="PROMOTIONAL">
                  Promotion · consentement requis
                </option>
                <option value="SERVICE">Information de service</option>
              </select>
            </label>
            <label>
              Message
              <textarea name="body" required maxLength={5000} />
            </label>
            <div className="manifest-select">
              {shops.map((m) => (
                <label key={m.id}>
                  <input type="checkbox" name="merchants" value={m.id} />
                  <span>{m.shopName}</span>
                </label>
              ))}
            </div>
            <button className="button" type="submit">
              Préparer la diffusion
            </button>
          </form>
          <div className="simple-list">
            {broadcasts.map((b) => (
              <div key={b.id}>
                <span>
                  <strong>{b.title}</strong>
                  <small>
                    {b.purpose === "PROMOTIONAL" ? "Promotion" : "Service"} ·{" "}
                    {b.sent} messages · {b.excluded} exclus
                  </small>
                  <small>{b.body}</small>
                </span>
                {b.status === "DRAFT" ? (
                  <button
                    className="button secondary"
                    onClick={() =>
                      void run(
                        () => post("/admin/broadcasts/" + b.id + "/dispatch"),
                        "Diffusion terminée dans l’app selon les consentements actuels.",
                      )
                    }
                  >
                    Diffuser dans l’app
                  </button>
                ) : (
                  <span className="badge green">Diffusée</span>
                )}
              </div>
            ))}
          </div>
        </section>
      )}
    </>
  );
}
export function Supply({
  revision,
  run,
  session,
}: {
  revision: number;
  run: Run;
  session: Session;
}) {
  const [suppliers, setSuppliers] = useState<any[]>([]),
    [purchases, setPurchases] = useState<any[]>([]),
    [active, setActive] = useState(""),
    [detail, setDetail] = useState<any>(null),
    [form, setForm] = useState(""),
    [lineCount, setLineCount] = useState(1),
    [error, setError] = useState("");
  useEffect(() => {
    Promise.all([api("/admin/suppliers"), api("/admin/purchases")])
      .then(([s, p]) => {
        setSuppliers(s.items);
        setPurchases(p.items);
      })
      .catch((e) => setError(e.message));
  }, [revision]);
  useEffect(() => {
    if (active)
      api("/admin/purchases/" + active)
        .then(setDetail)
        .catch((e) => setError(e.message));
  }, [active, revision]);
  const submit = (
    e: React.FormEvent<HTMLFormElement>,
    path: string,
    body: (d: FormData) => any,
    message?: string,
  ) => {
    e.preventDefault();
    const d = new FormData(e.currentTarget);
    void run(() => post(path, body(d)), message);
  };
  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      <div className="stats">
        <div className="stat">
          <span>Achats ouverts</span>
          <strong>
            {purchases.filter((p) => p.status !== "RECEIVED").length}
          </strong>
          <small>Brouillons, approuvés et partiels</small>
        </div>
        <div className="stat">
          <span>Quantité à réceptionner</span>
          <strong>
            {purchases.reduce(
              (a, p) => a + p.orderedQuantity - p.receivedQuantity,
              0,
            )}
          </strong>
          <small>Reliquats des achats enregistrés</small>
        </div>
        <div className="stat">
          <span>Fournisseurs</span>
          <strong>{suppliers.length}</strong>
          <small>Partenaires de cet espace</small>
        </div>
        <div className="stat">
          <span>Valeur engagée</span>
          <strong>
            {money(
              purchases
                .filter((p) => p.status !== "DRAFT")
                .reduce((a, p) => a + p.totalMinor, 0) / 100,
            )}
          </strong>
          <small>Achats approuvés · ce montant n’est pas un paiement</small>
        </div>
      </div>
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>Commandes fournisseurs</h2>
            <p>Une confirmation fournisseur ne constitue pas une réception.</p>
          </div>
          {session.user.role === "ADMIN" && (
            <div className="heading-actions">
              <button
                className="button secondary"
                onClick={() => setForm(form === "supplier" ? "" : "supplier")}
              >
                Nouveau fournisseur
              </button>
              <button
                className="button"
                onClick={() => setForm(form === "purchase" ? "" : "purchase")}
              >
                Créer un achat
              </button>
            </div>
          )}
        </div>
        {form === "supplier" && (
          <form
            className="action-form"
            onSubmit={(e) =>
              submit(
                e,
                "/admin/suppliers",
                (d) => ({
                  name: d.get("name"),
                  contact: d.get("contact"),
                  phone: d.get("phone"),
                  note: d.get("note"),
                }),
                "Fournisseur enregistré.",
              )
            }
          >
            <div className="form-grid">
              <label>
                Fournisseur
                <input name="name" required maxLength={180} />
              </label>
              <label>
                Contact
                <input name="contact" required maxLength={180} />
              </label>
              <label>
                Téléphone
                <input name="phone" required maxLength={40} />
              </label>
              <label>
                Note
                <input name="note" maxLength={2000} />
              </label>
            </div>
            <button type="submit" className="button">
              Enregistrer le fournisseur
            </button>
          </form>
        )}
        {form === "purchase" && (
          <form
            className="action-form"
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(async () => {
                const p = await post("/admin/purchases", {
                  supplierId: d.get("supplier"),
                  expectedAt: new Date(String(d.get("expected"))).toISOString(),
                  items: Array.from({ length: lineCount }, (_, i) => ({
                    productId: d.get("product-" + i),
                    quantity: Number(d.get("qty-" + i)),
                    unitCostMinor: Math.round(Number(d.get("cost-" + i)) * 100),
                  })),
                  note: d.get("note"),
                  idempotencyKey: key(),
                });
                setActive(p.order.id);
                setForm("");
              }, "Achat créé. Une autre personne habilitée doit l’approuver.");
            }}
          >
            <div className="form-grid">
              <label>
                Fournisseur
                <select name="supplier" required>
                  <option value="">Choisir un fournisseur</option>
                  {suppliers.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Réception attendue
                <input name="expected" type="datetime-local" required />
              </label>
            </div>
            {Array.from({ length: lineCount }, (_, i) => (
              <fieldset key={i}>
                <legend>Ligne {i + 1}</legend>
                <ProductPicker name={"product-" + i} />
                <div className="form-grid">
                  <label>
                    Quantité commandée
                    <input
                      name={"qty-" + i}
                      type="number"
                      min="1"
                      max="1000000"
                      required
                    />
                  </label>
                  <label>
                    Coût unitaire · DA
                    <input
                      name={"cost-" + i}
                      type="number"
                      min="0"
                      max="100000000"
                      step="0.01"
                      required
                    />
                  </label>
                </div>
              </fieldset>
            ))}
            <button
              className="button secondary"
              type="button"
              onClick={() => setLineCount(lineCount + 1)}
            >
              Ajouter une référence
            </button>
            <label>
              Note
              <textarea name="note" maxLength={2000} />
            </label>
            <button className="button" type="submit">
              Créer le brouillon d’achat
            </button>
          </form>
        )}
        {purchases.length ? (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Achat</th>
                  <th>Fournisseur</th>
                  <th>Attendu</th>
                  <th>Reçu</th>
                  <th>Date attendue</th>
                  <th>État</th>
                  <th>Facture</th>
                </tr>
              </thead>
              <tbody>
                {purchases.map((p) => (
                  <tr
                    key={p.id}
                    tabIndex={0}
                    onClick={() => setActive(p.id)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") setActive(p.id);
                    }}
                  >
                    <td>
                      <strong>{p.number}</strong>
                      <small>{money(p.totalMinor / 100)}</small>
                    </td>
                    <td>{p.supplierName}</td>
                    <td>{p.orderedQuantity}</td>
                    <td>{p.receivedQuantity}</td>
                    <td>{date(p.expectedAt, true)}</td>
                    <td>
                      <span className="badge neutral">
                        {
                          (
                            {
                              DRAFT: "À approuver",
                              APPROVED: "Approuvé",
                              PARTIAL: "Partiel",
                              RECEIVED: "Réceptionné",
                            } as any
                          )[p.status]
                        }
                      </span>
                    </td>
                    <td>
                      {
                        (
                          {
                            UNREVIEWED: "À rapprocher",
                            MATCHED: "Rapprochée",
                            DISCREPANCY: "Écart constaté",
                          } as any
                        )[p.invoiceMatchStatus]
                      }
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="empty">
            <p>
              Les achats créés apparaîtront ici avec leurs réceptions réelles.
            </p>
          </div>
        )}
      </section>
      {detail && active && (
        <section className="panel module-section">
          <div className="panel-heading">
            <div>
              <h2>
                {detail.order.number} · {detail.supplier.name}
              </h2>
              <p>{detail.order.note}</p>
            </div>
            <button className="button secondary" onClick={() => setActive("")}>
              Fermer le détail
            </button>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Produit</th>
                  <th>Commandé</th>
                  <th>Reçu</th>
                  <th>En quarantaine</th>
                  <th>Coût / unité</th>
                </tr>
              </thead>
              <tbody>
                {detail.items.map((l: any) => (
                  <tr key={l.id}>
                    <td>{l.productName}</td>
                    <td>{l.orderedQuantity}</td>
                    <td>{l.receivedQuantity}</td>
                    <td>{l.quarantineQuantity}</td>
                    <td>{money(l.unitCostMinor / 100)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {["ADMIN", "FINANCE"].includes(session.user.role) &&
            detail.order.status === "DRAFT" && (
              <div className="note">
                <p>
                  L’auteur de l’achat et son approbateur doivent être deux
                  comptes différents.
                </p>
                <button
                  className="button module-section"
                  onClick={() =>
                    void run(
                      () =>
                        approve(
                          session,
                          "/admin/purchases/" + active + "/approve",
                          { version: detail.order.version },
                        ),
                      "Achat approuvé par le compte habilité distinct.",
                    )
                  }
                >
                  {session.workspace.isDemo
                    ? "Approuver avec le compte Finance de démonstration"
                    : "Approuver cet achat"}
                </button>
              </div>
            )}
          {["ADMIN", "WAREHOUSE"].includes(session.user.role) &&
            ["APPROVED", "PARTIAL"].includes(detail.order.status) && (
              <form
                className="action-form"
                onSubmit={(e) =>
                  submit(
                    e,
                    "/admin/purchases/" + active + "/receive",
                    (d) => ({
                      version: detail.order.version,
                      idempotencyKey: key(),
                      note: d.get("note"),
                      lines: detail.items
                        .filter(
                          (l: any) => Number(d.get("qty-" + l.productId)) > 0,
                        )
                        .map((l: any) => ({
                          productId: l.productId,
                          receivedQuantity: Number(d.get("qty-" + l.productId)),
                          quarantineQuantity: Number(
                            d.get("quarantine-" + l.productId),
                          ),
                          lotNumber: d.get("lot-" + l.productId),
                          expiresAt: d.get("expiry-" + l.productId)
                            ? new Date(
                                String(d.get("expiry-" + l.productId)) +
                                  "T23:59:00+01:00",
                              ).toISOString()
                            : null,
                        })),
                    }),
                    "Réception enregistrée. Le stock vendable et la quarantaine ont été mis à jour une seule fois.",
                  )
                }
              >
                <h3>Réception physique</h3>
                {detail.items
                  .filter((l: any) => l.outstandingQuantity > 0)
                  .map((l: any) => (
                    <fieldset key={l.id}>
                      <legend>
                        {l.productName} · reliquat {l.outstandingQuantity}
                      </legend>
                      <div className="form-grid four">
                        <label>
                          Reçu
                          <input
                            type="number"
                            name={"qty-" + l.productId}
                            min="0"
                            max={l.outstandingQuantity}
                            defaultValue="0"
                            required
                          />
                        </label>
                        <label>
                          Dont quarantaine
                          <input
                            type="number"
                            name={"quarantine-" + l.productId}
                            min="0"
                            max={l.outstandingQuantity}
                            defaultValue="0"
                            required
                          />
                        </label>
                        <label>
                          Lot
                          <input name={"lot-" + l.productId} maxLength={100} />
                        </label>
                        <label>
                          Péremption
                          <input type="date" name={"expiry-" + l.productId} />
                        </label>
                      </div>
                    </fieldset>
                  ))}
                <label>
                  Constat de réception
                  <textarea name="note" maxLength={2000} />
                </label>
                <button type="submit" className="button">
                  Enregistrer les marchandises reçues
                </button>
              </form>
            )}
          {["ADMIN", "WAREHOUSE"].includes(session.user.role) &&
            detail.receiptLines
              .filter((l: any) => l.quarantineQuantity > l.inspectedQuantity)
              .map((l: any) => (
                <form
                  key={l.id}
                  className="action-form"
                  onSubmit={(e) =>
                    submit(
                      e,
                      "/admin/purchases/" + active + "/inspect",
                      (d) => ({
                        receiptLineId: l.id,
                        version: l.version,
                        quantity: Number(d.get("quantity")),
                        disposition: d.get("disposition"),
                        reason: d.get("reason"),
                        idempotencyKey: key(),
                      }),
                      "Contrôle de lot enregistré.",
                    )
                  }
                >
                  <h3>Contrôler le lot {l.lotNumber}</h3>
                  <p>
                    {l.quarantineQuantity - l.inspectedQuantity} unités isolées
                    ·{" "}
                    {l.expiresAt
                      ? "péremption " + date(l.expiresAt)
                      : "péremption non renseignée"}
                  </p>
                  <div className="form-grid">
                    <label>
                      Quantité
                      <input
                        type="number"
                        name="quantity"
                        min="1"
                        max={l.quarantineQuantity - l.inspectedQuantity}
                        required
                      />
                    </label>
                    <label>
                      Décision
                      <select name="disposition">
                        <option value="RELEASE">
                          Libérer vers le stock vendable
                        </option>
                        <option value="SUPPLIER_RETURN">
                          Retour fournisseur
                        </option>
                        <option value="DESTROY">Déclasser / détruire</option>
                      </select>
                    </label>
                  </div>
                  <label>
                    Motif
                    <input name="reason" required maxLength={1000} />
                  </label>
                  <button className="button" type="submit">
                    Valider le contrôle du lot
                  </button>
                </form>
              ))}
          {["ADMIN", "FINANCE"].includes(session.user.role) &&
            ["PARTIAL", "RECEIVED"].includes(detail.order.status) && (
              <form
                className="action-form"
                onSubmit={(e) =>
                  submit(
                    e,
                    "/admin/purchases/" + active + "/invoice-review",
                    (d) => ({
                      version: detail.order.version,
                      invoiceReference: d.get("ref"),
                      invoiceMinor: Math.round(Number(d.get("amount")) * 100),
                    }),
                    "Facture comparée aux marchandises effectivement reçues. Aucun paiement automatique.",
                  )
                }
              >
                <h3>Rapprocher la facture fournisseur</h3>
                <p>Valeur reçue : {money(detail.receivedMinor / 100)}</p>
                <div className="form-grid">
                  <label>
                    Référence facture
                    <input name="ref" required />
                  </label>
                  <label>
                    Montant facture · DA
                    <input
                      name="amount"
                      type="number"
                      min="0"
                      step="0.01"
                      required
                    />
                  </label>
                </div>
                <button className="button" type="submit">
                  Comparer avec la réception
                </button>
              </form>
            )}
        </section>
      )}
    </>
  );
}
