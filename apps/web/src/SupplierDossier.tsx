import { useEffect, useId, useRef, useState } from "react";
import { api, date, downloadPrivatePdf, key, money, post } from "./api";
import type { Session } from "./api";
import { approve } from "./approvals";
import type { Run } from "./Business";

const kinds: Record<string, string> = {
  PURCHASE_ORDER: "Bon d’achat",
  SUPPLIER_CONFIRMATION: "Confirmation fournisseur",
  RECEIPT: "Bon de réception",
  INVOICE: "Facture fournisseur",
  CREDIT_NOTE: "Avoir fournisseur",
};
const states: Record<string, string> = {
  ACTIVE: "Active",
  SUPERSEDED: "Remplacée",
  VOIDED: "Annulée",
  RECORDED: "À examiner",
  ACCEPTED: "Acceptée",
  REJECTED: "Rejetée",
};
function businessDate(value: string) {
  return new Intl.DateTimeFormat("sv-SE", {
    timeZone: "Africa/Algiers",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  })
    .format(new Date(value))
    .replace(" ", "T");
}
function fileBase64(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).split(",")[1]);
    reader.onerror = () => reject(new Error("Le fichier n’a pas pu être lu."));
    reader.readAsDataURL(file);
  });
}

export function SupplierDossier({
  purchase,
  session,
  revision,
  run,
  onUpdated,
}: {
  purchase: any;
  session: Session;
  revision: number;
  run: Run;
  onUpdated: (purchase: any) => void;
}) {
  const [data, setData] = useState<any>(null),
    [panel, setPanel] = useState(""),
    [kind, setKind] = useState("PURCHASE_ORDER"),
    [replacing, setReplacing] = useState<any>(null),
    [selectedFileName, setSelectedFileName] = useState(""),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const operation = useRef(key()),
    running = useRef(false),
    formVersion = useRef(0),
    closureStatus = useRef("");
  const currentOrder = useRef(purchase.order.id);
  const fileInputId = useId();
  currentOrder.current = purchase.order.id;
  const path = "/admin/purchases/" + purchase.order.id;
  const financial = ["ADMIN", "FINANCE"].includes(session.user.role);
  const canRecord = ["ADMIN", "WAREHOUSE"].includes(session.user.role);
  async function refresh() {
    const requestedOrder = purchase.order.id;
    try {
      const next = await api(path + "/dossier");
      if (currentOrder.current !== requestedOrder) return;
      setData((current: any) =>
        current?.orderId === next.orderId &&
        current.orderVersion > next.orderVersion
          ? current
          : next,
      );
      setError("");
    } catch (e: any) {
      if (currentOrder.current === requestedOrder) setError(e.message);
    }
  }
  useEffect(() => {
    setData(null);
    setPanel("");
    setReplacing(null);
  }, [purchase.order.id]);
  useEffect(() => {
    refresh();
  }, [purchase.order.id, revision]);
  async function mutate(action: () => Promise<unknown>, message: string) {
    if (running.current) return;
    running.current = true;
    setBusy(true);
    setError("");
    try {
      await run(async () => {
        await action();
        onUpdated(await api(path));
        await refresh();
        setPanel("");
        operation.current = key();
      }, message);
    } finally {
      running.current = false;
      setBusy(false);
    }
  }
  function open(next: string, document: any = null) {
    operation.current = key();
    setReplacing(document);
    setSelectedFileName("");
    setKind(document?.kind || "PURCHASE_ORDER");
    formVersion.current = purchase.order.version;
    closureStatus.current = purchase.order.status;
    setPanel(panel === next && !document ? "" : next);
  }
  const editable = ["APPROVED", "PARTIAL", "RECEIVED", "SHORT_CLOSED"].includes(
    purchase.order.status,
  );
  const evidenceOnly = purchase.receipts.length > 0;
  return (
    <div className="module-section supplier-dossier">
      <div className="panel-heading">
        <div>
          <h3>Pièces et accord fournisseur</h3>
          <p>
            Conservez les originaux et faites examiner les modifications avant
            réception.
          </p>
        </div>
        <div className="heading-actions">
          <button
            className="button secondary"
            disabled={busy}
            onClick={() => open("upload")}
          >
            Ajouter une pièce
          </button>
          {editable && canRecord ? (
            <button
              className="button secondary"
              disabled={busy}
              onClick={() => open("confirmation")}
            >
              {evidenceOnly
                ? "Compléter la preuve fournisseur"
                : "Saisir l’accord fournisseur"}
            </button>
          ) : null}
        </div>
      </div>
      {error ? (
        <div className="error-banner" role="alert">
          {error}
          <button onClick={refresh}>Réessayer</button>
        </div>
      ) : null}
      {!data ? (
        <p aria-live="polite">Chargement des pièces…</p>
      ) : (
        <>
          <div className="supplier-evidence">
            <span
              className={
                "badge " +
                (data.evidence.completeDocumentChain ? "green" : "neutral")
              }
            >
              {data.evidence.completeDocumentChain
                ? "Chaîne des pièces complète"
                : "Pièces à compléter"}
            </span>
            <p>{data.evidence.notice}</p>
            <ul>
              <li>
                Bon d’achat :{" "}
                {data.evidence.purchaseOrder ? "joint" : "à joindre"}
              </li>
              <li>
                Accord fournisseur :{" "}
                {data.evidence.acceptedSupplierConfirmation
                  ? "accepté avec pièce active"
                  : "à examiner ou non joint"}
              </li>
              <li>
                Réceptions : {data.evidence.goodsReceipts} ·{" "}
                {data.evidence.receiptDocument
                  ? "toutes documentées"
                  : "pièces à compléter"}
              </li>
              <li>
                Facture correspondant à la référence rapprochée :{" "}
                {data.evidence.invoiceDocument ? "jointe" : "à joindre"}
              </li>
            </ul>
          </div>
          {panel === "upload" ? (
            <form
              className="action-form"
              onChange={() => {
                operation.current = key();
              }}
              onSubmit={(e) => {
                e.preventDefault();
                const form = new FormData(e.currentTarget);
                const file = form.get("file") as File;
                void mutate(async () => {
                  if (!file?.size)
                    throw new Error("Choisissez un fichier PDF.");
                  const max = session.workspace.isDemo ? 524288 : 2097152;
                  if (file.size > max)
                    throw new Error(
                      session.workspace.isDemo
                        ? "Le PDF dépasse 512 Ko, la limite de démonstration."
                        : "Le PDF dépasse 2 Mo.",
                    );
                  await post(path + "/documents", {
                    orderVersion: formVersion.current,
                    kind,
                    reference: form.get("reference"),
                    documentDate:
                      String(form.get("documentDate")) + "T12:00:00+01:00",
                    fileName: file.name,
                    contentBase64: await fileBase64(file),
                    idempotencyKey: operation.current,
                    receiptId: form.get("receiptId") || null,
                    supersedesId: replacing?.id || null,
                    supersededVersion: replacing?.version ?? null,
                  });
                }, "Pièce conservée avec son empreinte et son historique.");
              }}
            >
              <h4>
                {replacing ? "Nouvelle révision de la pièce" : "Ajouter un PDF"}
              </h4>
              <div className="form-grid">
                <label>
                  Type de pièce
                  <select
                    value={kind}
                    onChange={(e) => setKind(e.target.value)}
                    disabled={!!replacing}
                  >
                    {Object.entries(kinds)
                      .filter(
                        ([value]) =>
                          financial ||
                          !["INVOICE", "CREDIT_NOTE"].includes(value),
                      )
                      .map(([value, label]) => (
                        <option value={value} key={value}>
                          {label}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Référence de la pièce
                  <input
                    name="reference"
                    defaultValue={replacing?.reference || ""}
                    required
                    maxLength={120}
                  />
                </label>
                <label>
                  Date de la pièce
                  <input name="documentDate" type="date" required />
                </label>
                <div className="supplier-pdf-field">
                  <label htmlFor={fileInputId}>Fichier PDF</label>
                  <div className="supplier-pdf-choice">
                    <input
                      id={fileInputId}
                      name="file"
                      type="file"
                      accept=".pdf,application/pdf"
                      aria-describedby={fileInputId + "-name"}
                      onChange={(event) =>
                        setSelectedFileName(event.target.files?.[0]?.name || "")
                      }
                      required
                    />
                    <span className="button secondary" aria-hidden="true">
                      Choisir un PDF
                    </span>
                    <span
                      id={fileInputId + "-name"}
                      className="supplier-pdf-name"
                      aria-live="polite"
                    >
                      {selectedFileName || "Aucun fichier choisi"}
                    </span>
                  </div>
                  <small>
                    {session.workspace.isDemo
                      ? "512 Ko maximum en démonstration"
                      : "2 Mo maximum"}
                  </small>
                </div>
              </div>
              {kind === "RECEIPT" ? (
                <label>
                  Réception physique concernée
                  <select
                    name="receiptId"
                    required
                    defaultValue={replacing?.receiptId || ""}
                  >
                    <option value="">Choisir la réception</option>
                    {purchase.receipts.map((r: any) => (
                      <option key={r.id} value={r.id}>
                        {date(r.at, true)} · {r.id.slice(0, 8)}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}
              {replacing ? (
                <p>
                  L’ancien PDF restera consultable avec l’état « Remplacée ».
                  Les confirmations déjà examinées conservent leur pièce
                  d’origine.
                </p>
              ) : null}
              <div className="heading-actions">
                <button className="button" disabled={busy} type="submit">
                  {busy ? "Enregistrement…" : "Conserver la pièce"}
                </button>
                <button
                  className="button secondary"
                  type="button"
                  disabled={busy}
                  onClick={() => setPanel("")}
                >
                  Annuler
                </button>
              </div>
            </form>
          ) : null}
          {panel === "confirmation" && editable ? (
            <form
              className="action-form"
              onChange={() => {
                operation.current = key();
              }}
              onSubmit={(e) => {
                e.preventDefault();
                const form = new FormData(e.currentTarget);
                void mutate(
                  () =>
                    post(path + "/confirmations", {
                      orderVersion: formVersion.current,
                      documentId: form.get("documentId"),
                      supplierReference: form.get("reference"),
                      expectedAt: evidenceOnly
                        ? purchase.order.expectedAt
                        : String(form.get("expectedAt")) + ":00+01:00",
                      items: purchase.items.map((line: any) => ({
                        productId: line.productId,
                        quantity: Number(form.get("qty-" + line.productId)),
                        unitCostMinor: Math.round(
                          Number(form.get("cost-" + line.productId)) * 100,
                        ),
                      })),
                      idempotencyKey: operation.current,
                    }),
                  "Accord enregistré pour examen par une autre personne habilitée.",
                );
              }}
            >
              <h4>
                {evidenceOnly
                  ? "Compléter les pièces après réception"
                  : "Termes reçus du fournisseur"}
              </h4>
              <p>
                {evidenceOnly
                  ? "Les termes sont figés après réception. Seule la preuve peut être complétée, avec un examen distinct."
                  : "La saisie ne modifie pas l’achat. Une acceptation distincte est requise. Saisissez zéro pour une référence indisponible."}
              </p>
              <div className="form-grid">
                <label>
                  Pièce justificative
                  <select name="documentId" required>
                    <option value="">Choisir la confirmation active</option>
                    {data.documents
                      .filter(
                        (d: any) =>
                          d.kind === "SUPPLIER_CONFIRMATION" &&
                          d.status === "ACTIVE",
                      )
                      .map((d: any) => (
                        <option key={d.id} value={d.id}>
                          {d.reference} · {d.fileName}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Référence fournisseur
                  <input name="reference" maxLength={120} required />
                </label>
                <label>
                  Réception attendue · heure d’Alger
                  <input
                    type="datetime-local"
                    name="expectedAt"
                    required
                    defaultValue={businessDate(purchase.order.expectedAt)}
                    readOnly={evidenceOnly}
                  />
                </label>
              </div>
              {purchase.items.map((line: any) => (
                <fieldset key={line.id}>
                  <legend>{line.productName}</legend>
                  <div className="form-grid">
                    <label>
                      Quantité confirmée
                      <input
                        name={"qty-" + line.productId}
                        type="number"
                        min="0"
                        max="1000000"
                        defaultValue={line.orderedQuantity}
                        readOnly={evidenceOnly}
                        required
                      />
                    </label>
                    <label>
                      Coût unitaire confirmé · DA
                      <input
                        name={"cost-" + line.productId}
                        type="number"
                        min="0"
                        max="100000000"
                        step="0.01"
                        defaultValue={line.unitCostMinor / 100}
                        readOnly={evidenceOnly}
                        required
                      />
                    </label>
                  </div>
                </fieldset>
              ))}
              <button type="submit" className="button" disabled={busy}>
                Soumettre la confirmation à examen
              </button>
            </form>
          ) : null}
          {data.documents.length ? (
            <ul
              className="supplier-files module-section"
              aria-label="Pièces du dossier"
            >
              {data.documents.map((document: any) => (
                <li className="supplier-file" key={document.id}>
                  <div className="supplier-file-name">
                    <strong>{kinds[document.kind]}</strong>
                    <small>
                      {document.fileName} ·{" "}
                      {Math.ceil(document.byteLength / 1024)} Ko
                    </small>
                  </div>
                  <div className="supplier-file-meta">
                    <span>{document.reference}</span>
                    <small>Datée du {date(document.documentDate)}</small>
                  </div>
                  <span className="badge neutral">
                    {states[document.status]}
                  </span>
                  <div className="heading-actions supplier-file-actions">
                    <button
                      className="button secondary small"
                      onClick={() =>
                        downloadPrivatePdf(
                          path + "/documents/" + document.id + "/content",
                          document.fileName,
                        ).catch((e) => setError(e.message))
                      }
                    >
                      Télécharger
                    </button>
                    {document.status === "ACTIVE" ? (
                      <button
                        className="button secondary small"
                        disabled={busy}
                        onClick={() => open("upload", document)}
                      >
                        Remplacer
                      </button>
                    ) : null}
                    {financial &&
                    document.status === "ACTIVE" &&
                    !data.confirmations.some(
                      (c: any) =>
                        c.documentId === document.id && c.status === "ACCEPTED",
                    ) ? (
                      <button
                        className="button secondary small"
                        disabled={busy}
                        onClick={() => {
                          setReplacing(document);
                          setPanel("void");
                        }}
                      >
                        Annuler la pièce
                      </button>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <p className="module-section">
              Aucune pièce jointe pour cet achat.
            </p>
          )}
          {panel === "void" && replacing ? (
            <form
              className="action-form"
              onSubmit={(e) => {
                e.preventDefault();
                const form = new FormData(e.currentTarget);
                void mutate(
                  () =>
                    post(path + "/documents/" + replacing.id + "/void", {
                      version: replacing.version,
                      reason: form.get("reason"),
                    }),
                  "Annulation tracée. Le fichier d’origine est conservé.",
                );
              }}
            >
              <h4>Annuler {replacing.reference}</h4>
              <label>
                Motif
                <input name="reason" maxLength={1000} required />
              </label>
              <button className="button" type="submit" disabled={busy}>
                Confirmer l’annulation de la pièce
              </button>
            </form>
          ) : null}
          {data.confirmations.map((confirmation: any) => (
            <section className="action-form" key={confirmation.id}>
              <div className="panel-heading">
                <div>
                  <h4>Accord {confirmation.supplierReference}</h4>
                  <p>Attendu le {date(confirmation.expectedAt, true)}</p>
                  {confirmation.evidenceOnly ? (
                    <p>Pièce complétée après réception · termes inchangés</p>
                  ) : null}
                </div>
                <span className="badge neutral">
                  {states[confirmation.status]}
                </span>
              </div>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Référence</th>
                      <th>Quantité proposée</th>
                      <th>Coût / unité</th>
                    </tr>
                  </thead>
                  <tbody>
                    {confirmation.items.map((line: any) => (
                      <tr key={line.productId}>
                        <td>
                          {purchase.items.find(
                            (p: any) => p.productId === line.productId,
                          )?.productName || line.productId}
                        </td>
                        <td>{line.quantity}</td>
                        <td>{money(line.unitCostMinor / 100)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {confirmation.reviewReason ? (
                <p>Décision : {confirmation.reviewReason}</p>
              ) : null}
              {confirmation.status === "RECORDED" && financial ? (
                <form
                  className="supplier-confirmation-review"
                  onSubmit={(e) => {
                    e.preventDefault();
                    const form = new FormData(e.currentTarget);
                    void mutate(
                      () =>
                        approve(
                          session,
                          path +
                            "/confirmations/" +
                            confirmation.id +
                            "/review",
                          {
                            orderVersion: data.orderVersion,
                            version: confirmation.version,
                            decision: form.get("decision"),
                            reason: form.get("reason"),
                          },
                        ),
                      "Examen fournisseur enregistré.",
                    );
                  }}
                >
                  <p>
                    Une autre personne que l’auteur de la saisie doit examiner
                    ces termes.
                    {session.workspace.isDemo
                      ? " En démo, le compte Finance distinct effectue cet examen."
                      : ""}
                  </p>
                  <div className="form-grid">
                    <label>
                      Décision
                      <select name="decision">
                        <option value="ACCEPT">Accepter ces termes</option>
                        <option value="REJECT">Rejeter ces termes</option>
                      </select>
                    </label>
                    <label>
                      Motif de la décision
                      <input name="reason" required maxLength={1000} />
                    </label>
                  </div>
                  <button
                    className="button"
                    type="submit"
                    disabled={
                      busy ||
                      (!session.workspace.isDemo &&
                        confirmation.recordedBy === session.user.id)
                    }
                  >
                    Enregistrer l’examen distinct
                  </button>
                </form>
              ) : null}
            </section>
          ))}
          {financial &&
          ["DRAFT", "APPROVED", "PARTIAL"].includes(purchase.order.status) ? (
            <div className="module-section">
              <button
                className="button secondary"
                disabled={busy}
                onClick={() => open("close")}
              >
                {purchase.order.status === "PARTIAL"
                  ? "Clôturer le reliquat"
                  : "Annuler cet achat"}
              </button>
              {panel === "close" ? (
                <form
                  className="action-form"
                  onSubmit={(e) => {
                    e.preventDefault();
                    const form = new FormData(e.currentTarget);
                    const suffix =
                      closureStatus.current === "PARTIAL"
                        ? "/close-remainder"
                        : "/cancel";
                    void mutate(
                      () =>
                        closureStatus.current === "DRAFT"
                          ? post(path + suffix, {
                              version: formVersion.current,
                              reason: form.get("reason"),
                            })
                          : approve(session, path + suffix, {
                              version: formVersion.current,
                              reason: form.get("reason"),
                            }),
                      "Clôture tracée sans réception ni paiement inventé.",
                    );
                  }}
                >
                  <h4>
                    {closureStatus.current === "PARTIAL"
                      ? "Clôturer le reliquat non reçu"
                      : "Annuler cet achat"}
                  </h4>
                  <p>
                    Les réceptions et pièces restent conservées. Une clôture ne
                    rembourse aucun paiement.
                  </p>
                  <label>
                    Motif de clôture
                    <input name="reason" required maxLength={1000} />
                  </label>
                  <button
                    className="button secondary"
                    type="submit"
                    disabled={
                      busy ||
                      (!session.workspace.isDemo &&
                        closureStatus.current !== "DRAFT" &&
                        purchase.order.createdBy === session.user.id)
                    }
                  >
                    Confirmer la clôture
                  </button>
                </form>
              ) : null}
            </div>
          ) : null}
          {data.events.length ? (
            <details className="module-section">
              <summary>Historique des pièces et accords</summary>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Date</th>
                      <th>Opération</th>
                      <th>Détail</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.events.map((event: any) => (
                      <tr key={event.id}>
                        <td>{date(event.at, true)}</td>
                        <td>
                          {(
                            {
                              SUPPLIER_DOCUMENT_RECORDED: "Pièce enregistrée",
                              SUPPLIER_DOCUMENT_VOIDED: "Pièce annulée",
                              SUPPLIER_CONFIRMATION_RECORDED: "Accord saisi",
                              SUPPLIER_CONFIRMATION_EVIDENCE_RECORDED:
                                "Preuve ajoutée après réception",
                              SUPPLIER_CONFIRMATION_ACCEPTED: "Accord accepté",
                              SUPPLIER_CONFIRMATION_REJECTED: "Accord rejeté",
                              SUPPLY_REMAINDER_CLOSED: "Reliquat clôturé",
                              SUPPLY_CANCELLED: "Achat annulé",
                            } as Record<string, string>
                          )[event.kind] || "Mise à jour du dossier"}
                        </td>
                        <td>{event.details}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </details>
          ) : null}
        </>
      )}
    </div>
  );
}
