import { useEffect, useRef, useState } from "react";
import { api, date, key, money, post } from "./api";
import type { Run } from "./Business";

const statusNames: Record<string, string> = {
  PENDING: "Accord du magasin attendu",
  ACCEPTED: "Modification acceptée",
  REJECTED: "Modification refusée",
  WITHDRAWN: "Proposition retirée",
  EXPIRED: "Proposition expirée",
  STALE: "Commande modifiée depuis",
  CLOSED: "Décision enregistrée",
};
const localExpiry = () => {
  const value = new Date(Date.now() + 3600000);
  return new Date(value.getTime() - value.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
};

function BasketTerms({ terms, title }: { terms: any; title: string }) {
  return (
    <section className="amendment-basket">
      <h4>{title}</h4>
      {terms.items.map((line: any) => (
        <div key={line.productId}>
          <span>
            <strong>{line.name}</strong>
            <small>
              {line.quantity} unités · {money(line.unitPrice)} / unité
              {line.caseSize > 1 ? ` · colis de ${line.caseSize}` : ""}
            </small>
            {line.campaignTitle && (
              <small>
                {line.campaignTitle}
                {line.giftQuantity
                  ? ` · ${line.giftQuantity} × ${line.giftProductName} offert(s)`
                  : ""}
              </small>
            )}
          </span>
          <b>{money(line.lineTotal)}</b>
        </div>
      ))}
      {terms.promotions?.items
        ?.filter(
          (promotion: any) =>
            promotion.quoteVersion === terms.quoteVersion &&
            !["SUPERSEDED", "CANCELLED"].includes(promotion.status),
        )
        .map((promotion: any) => (
          <p className="metrics-note" key={promotion.id}>
            {promotion.campaignTitle}
            {promotion.kind === "FREE_GOODS" && promotion.giftQuantity
              ? ` · ${promotion.giftQuantity} × ${promotion.giftProductName} offert(s)`
              : " · remise incluse dans le prix"}
          </p>
        ))}
      <dl>
        <div>
          <dt>Marchandises</dt>
          <dd>{money(terms.subtotal)}</dd>
        </div>
        <div>
          <dt>Livraison</dt>
          <dd>{money(terms.deliveryFee)}</dd>
        </div>
        <div>
          <dt>Total</dt>
          <dd>{money(terms.total)}</dd>
        </div>
      </dl>
    </section>
  );
}

export default function OrderAmendments({
  order,
  revision,
  role,
  run,
  busy,
}: {
  order: any;
  revision: number;
  role: string;
  run: Run;
  busy: boolean;
}) {
  const [data, setData] = useState<any>(null),
    [error, setError] = useState(""),
    [creating, setCreating] = useState(false),
    [draft, setDraft] = useState<any[]>([]),
    [baseVersion, setBaseVersion] = useState(0),
    [search, setSearch] = useState(""),
    [products, setProducts] = useState<any[]>([]),
    [reason, setReason] = useState(""),
    [expires, setExpires] = useState(localExpiry),
    [withdraw, setWithdraw] = useState<any>(null),
    [withdrawReason, setWithdrawReason] = useState("");
  const createKey = useRef(""),
    withdrawKeys = useRef<Record<string, string>>({});
  const load = async () => {
    const result = await api(`/orders/${order.id}/amendments`);
    setData(result);
  };
  useEffect(() => {
    let active = true;
    api(`/orders/${order.id}/amendments`)
      .then((result) => {
        if (active) setData(result);
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [order.id, revision]);
  useEffect(() => {
    if (!creating) return;
    let active = true;
    const timer = setTimeout(() => {
      api("/products?pageSize=25&q=" + encodeURIComponent(search))
        .then((result) => {
          if (active) setProducts(result.items);
        })
        .catch((e) => {
          if (active) setError(e.message);
        });
    }, 200);
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [search, creating]);
  if (role !== "ADMIN") return null;
  const submit = async (action: () => Promise<unknown>, message: string) => {
    setError("");
    await run(async () => {
      try {
        await action();
        await load();
      } catch (e: any) {
        setError(e.message);
        throw e;
      }
    }, message);
  };
  return (
    <section
      className="amendments-section"
      aria-labelledby="amendments-heading"
    >
      <div className="panel-heading">
        <div>
          <h3 id="amendments-heading">Modifier avec l’accord du magasin</h3>
          <p>
            Une proposition conserve la commande actuelle jusqu’à son
            acceptation explicite dans l’app.
          </p>
        </div>
      </div>
      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}
      {!data ? (
        <p role="status">Chargement des propositions…</p>
      ) : (
        <>
          {!creating && data.canPropose && (
            <button
              className="button secondary"
              disabled={busy}
              onClick={() => {
                setDraft(
                  order.items.map((line: any) => ({
                    productId: line.productId,
                    name: line.name,
                    quantity: String(line.quantity),
                    caseSize: line.caseSize || 1,
                  })),
                );
                setBaseVersion(data.orderVersion);
                setReason("");
                setExpires(localExpiry());
                createKey.current = key();
                setCreating(true);
                setError("");
              }}
            >
              Proposer des produits ou quantités
            </button>
          )}
          {!data.canPropose && (
            <p className="metrics-note">
              {data.eligibilityReason ||
                "La modification du panier n’est plus disponible à ce stade. Consultez les propositions et l’historique ci-dessous."}
            </p>
          )}
          {creating && (
            <form
              className="action-form amendment-form"
              onSubmit={(event) => {
                event.preventDefault();
                if (
                  !draft.length ||
                  draft.some(
                    (line) =>
                      !Number.isInteger(Number(line.quantity)) ||
                      Number(line.quantity) <= 0,
                  )
                ) {
                  setError(
                    "Conservez au moins un produit avec une quantité entière positive.",
                  );
                  return;
                }
                void submit(async () => {
                  await post(`/admin/orders/${order.id}/amendments`, {
                    version: baseVersion,
                    items: draft.map((line) => ({
                      productId: line.productId,
                      quantity: Number(line.quantity),
                    })),
                    reason,
                    expiresAt: new Date(expires).toISOString(),
                    idempotencyKey: createKey.current,
                  });
                  setCreating(false);
                }, "Proposition transmise au magasin. La commande actuelle reste en vigueur jusqu’à son accord.");
              }}
            >
              <h4>Le panier proposé</h4>
              <p>
                Les prix et avantages sont recalculés par le serveur pour ce
                magasin à l’envoi. Les frais de livraison actuels sont
                conservés. Aucun tarif libre ne peut être saisi ici.
              </p>
              {draft.map((line, index) => (
                <div className="amendment-draft-line" key={line.productId}>
                  <span>
                    <strong>{line.name}</strong>
                    <small>
                      Conditionnement connu : {line.caseSize} unité(s)
                    </small>
                  </span>
                  <label>
                    Quantité
                    <input
                      type="number"
                      min="1"
                      step="1"
                      max="100000"
                      required
                      value={line.quantity}
                      onChange={(e) =>
                        setDraft((rows) =>
                          rows.map((row, i) =>
                            i === index
                              ? { ...row, quantity: e.target.value }
                              : row,
                          ),
                        )
                      }
                      aria-label={"Quantité proposée pour " + line.name}
                    />
                  </label>
                  <button
                    className="text-action"
                    type="button"
                    disabled={busy}
                    onClick={() =>
                      setDraft((rows) => rows.filter((_, i) => i !== index))
                    }
                  >
                    Retirer<span className="sr-only"> {line.name}</span>
                  </button>
                </div>
              ))}
              <label>
                Ajouter un produit du catalogue
                <input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Nom ou code-barres…"
                />
              </label>
              <select
                aria-label="Produit à ajouter à la modification"
                value=""
                onChange={(e) => {
                  const product = products.find(
                    (item) => item.id === e.target.value,
                  );
                  if (product)
                    setDraft((rows) => [
                      ...rows,
                      {
                        productId: product.id,
                        name: product.name,
                        quantity: String(
                          product.minimumQuantity || product.caseSize || 1,
                        ),
                        caseSize: product.caseSize || 1,
                      },
                    ]);
                }}
              >
                <option value="">Choisir une référence à ajouter</option>
                {products
                  .filter(
                    (product) =>
                      !draft.some((line) => line.productId === product.id),
                  )
                  .map((product) => (
                    <option value={product.id} key={product.id}>
                      {product.name} · {product.barcode}
                    </option>
                  ))}
              </select>
              <label>
                Motif présenté au magasin
                <textarea
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  minLength={8}
                  maxLength={800}
                  required
                  placeholder="Précisez le remplacement ou l’ajustement proposé."
                />
              </label>
              <label>
                Valable jusqu’au · heure locale
                <input
                  type="datetime-local"
                  value={expires}
                  onChange={(e) => setExpires(e.target.value)}
                  required
                />
              </label>
              <div className="note">
                <strong>Le magasin décide.</strong>
                <p>
                  La proposition affichera les produits, quantités, prix,
                  cadeaux éventuels et le nouveau total. L’accord sera refusé si
                  les conditions, le stock ou la commande ont changé
                  entre-temps.
                </p>
              </div>
              <div className="heading-actions">
                <button
                  className="button"
                  type="submit"
                  disabled={busy || !draft.length}
                >
                  Envoyer la proposition au magasin
                </button>
                <button
                  className="button secondary"
                  type="button"
                  disabled={busy}
                  onClick={() => setCreating(false)}
                >
                  Annuler la saisie
                </button>
              </div>
            </form>
          )}
          {data.items.map((proposal: any) => (
            <article className="amendment-record" key={proposal.id}>
              <div className="panel-heading">
                <div>
                  <h4>
                    {statusNames[
                      proposal.status !== "PENDING"
                        ? proposal.status
                        : proposal.applicability
                    ] ||
                      statusNames[proposal.status] ||
                      proposal.status}
                  </h4>
                  <p>
                    {proposal.proposerName || "Grossiste"} ·{" "}
                    {date(proposal.proposedAt, true)}
                  </p>
                </div>
                <span className="badge neutral">
                  Version {proposal.version}
                </span>
              </div>
              <p>{proposal.reason}</p>
              <p className="metrics-note">
                Expiration : {date(proposal.expiresAt, true)} · proposition sur
                la version {proposal.baseOrderVersion} de la commande.
              </p>
              <details open={proposal.applicability === "PENDING"}>
                <summary>Comparer les produits et les conditions</summary>
                <div className="amendment-comparison">
                  <BasketTerms
                    title="Commande actuelle à la proposition"
                    terms={proposal.before}
                  />
                  <BasketTerms
                    title="Conditions proposées"
                    terms={proposal.after}
                  />
                </div>
              </details>
              {proposal.decidedAt && (
                <p className="metrics-note">
                  {proposal.decisionName || "Décision enregistrée"} ·{" "}
                  {date(proposal.decidedAt, true)}
                  {proposal.decisionReason
                    ? " · " + proposal.decisionReason
                    : ""}
                </p>
              )}
              {proposal.canWithdraw && (
                <button
                  className="button secondary"
                  disabled={busy}
                  onClick={() => {
                    setWithdraw(proposal);
                    setWithdrawReason("");
                  }}
                >
                  Retirer cette proposition
                </button>
              )}
              {withdraw?.id === proposal.id && (
                <form
                  className="action-form"
                  onSubmit={(event) => {
                    event.preventDefault();
                    void submit(async () => {
                      withdrawKeys.current[proposal.id] ||= key();
                      await post(
                        `/admin/orders/${order.id}/amendments/${proposal.id}/withdraw`,
                        {
                          version: proposal.version,
                          reason: withdrawReason,
                          idempotencyKey: withdrawKeys.current[proposal.id],
                        },
                      );
                      setWithdraw(null);
                    }, "Proposition retirée. La commande initiale est conservée.");
                  }}
                >
                  <label>
                    Motif du retrait
                    <textarea
                      minLength={8}
                      maxLength={800}
                      required
                      value={withdrawReason}
                      onChange={(e) => setWithdrawReason(e.target.value)}
                    />
                  </label>
                  <div className="heading-actions">
                    <button className="button" disabled={busy}>
                      Confirmer le retrait
                    </button>
                    <button
                      className="button secondary"
                      type="button"
                      onClick={() => setWithdraw(null)}
                    >
                      Conserver la proposition
                    </button>
                  </div>
                </form>
              )}
            </article>
          ))}
          {!data.items.length && (
            <p className="metrics-note">
              Aucune proposition de modification pour cette commande.
            </p>
          )}
        </>
      )}
    </section>
  );
}
