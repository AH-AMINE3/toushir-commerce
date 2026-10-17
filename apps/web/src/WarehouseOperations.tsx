import { useEffect, useState } from "react";
import { api, date, key, post } from "./api";
import { ProductPicker } from "./Business";
import type { Run } from "./Business";

export function PickWaves({
  orders,
  revision,
  run,
  role,
}: {
  orders: any[];
  revision: number;
  run: Run;
  role: string;
}) {
  const [waves, setWaves] = useState<any[]>([]),
    [active, setActive] = useState(""),
    [detail, setDetail] = useState<any>(null),
    [creating, setCreating] = useState(false),
    [error, setError] = useState("");
  useEffect(() => {
    api("/admin/warehouse/waves")
      .then((d) => {
        setWaves(d.items);
        setError("");
      })
      .catch((e) => setError(e.message));
  }, [revision]);
  useEffect(() => {
    if (active)
      api("/admin/warehouse/waves/" + active)
        .then(setDetail)
        .catch((e) => setError(e.message));
  }, [active, revision]);
  const canPick = ["ADMIN", "WAREHOUSE"].includes(role);
  return (
    <section className="panel module-section">
      <div className="panel-heading">
        <div>
          <h2>Vagues de prélèvement & scans</h2>
          <p>
            Chaque scan valide le produit, le lot réservé et la quantité réelle.
            Les lots expirés ne peuvent pas partir.
          </p>
        </div>
        {canPick && (
          <button className="button" onClick={() => setCreating(!creating)}>
            {creating ? "Fermer la création" : "Créer une vague"}
          </button>
        )}
      </div>
      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}
      {creating && (
        <form
          className="action-form"
          onSubmit={(e) => {
            e.preventDefault();
            const d = new FormData(e.currentTarget);
            void run(async () => {
              const result = await post("/admin/warehouse/waves", {
                name: d.get("name"),
                orderIds: d.getAll("orders"),
                idempotencyKey: key(),
              });
              setActive(result.wave.id);
              setCreating(false);
            }, "Vague créée. Le départ exige la validation des scans.");
          }}
        >
          <label>
            Nom de la vague
            <input
              name="name"
              required
              maxLength={120}
              placeholder="Allée A · préparation du matin"
            />
          </label>
          <div className="manifest-select">
            {orders
              .filter((o) =>
                ["CONFIRMED", "PREPARING", "READY"].includes(o.status),
              )
              .map((o) => (
                <label key={o.id}>
                  <input type="checkbox" name="orders" value={o.id} />
                  <span>
                    <strong>{o.number}</strong>
                    <small>
                      {o.merchant.shopName} · {o.quantity} unités
                    </small>
                  </span>
                </label>
              ))}
          </div>
          <button className="button" type="submit">
            Créer les tâches de prélèvement
          </button>
        </form>
      )}
      <div className="wave-tabs">
        {waves.map((w) => (
          <button
            className={
              "button secondary " + (active === w.id ? "selected" : "")
            }
            key={w.id}
            onClick={() => setActive(w.id)}
          >
            {w.name}
            <span className="badge neutral">
              {w.status === "COMPLETE" ? "Terminée" : "Scans en cours"}
            </span>
          </button>
        ))}
      </div>
      {!waves.length && !creating && (
        <div className="empty">
          <p>
            Créez une vague à partir de commandes confirmées. Un lecteur
            code-barres peut saisir directement le code dans le champ de scan.
          </p>
        </div>
      )}
      {detail && active && (
        <>
          <div className="section-heading spaced">
            <h3>{detail.wave.name}</h3>
            <span>
              {detail.picked} / {detail.total} unités vérifiées
            </span>
          </div>
          <progress max={Math.max(1, detail.total)} value={detail.picked} />
          <div className="pick-task-grid">
            {detail.items.map((t: any) => (
              <article key={t.id} data-task-id={t.id} className="pick-task">
                <div className="section-heading">
                  <h3>{t.productName}</h3>
                  <span
                    className={
                      "badge " +
                      (t.pickedQuantity === t.quantity ? "green" : "neutral")
                    }
                  >
                    {t.pickedQuantity} / {t.quantity}
                  </span>
                </div>
                <p>
                  {t.fefoManaged
                    ? `Lot réservé ${t.lotNumber} · ${t.expiresAt ? "péremption " + date(t.expiresAt) : "date non renseignée"}`
                    : "Stock sans traçabilité de lot · contrôle du produit requis"}
                  {t.isGift ? " · Gratuité distincte" : ""}
                </p>
                {canPick &&
                  t.status === "RESERVED" &&
                  t.pickedQuantity < t.quantity && (
                    <form
                      className="action-form"
                      key={t.version}
                      onSubmit={(e) => {
                        e.preventDefault();
                        const form = e.currentTarget,
                          d = new FormData(form);
                        void run(async () => {
                          const updated = await post(
                            "/admin/warehouse/tasks/" + t.id + "/scan",
                            {
                              version: t.version,
                              barcode: d.get("barcode"),
                              lotNumber: d.get("lot") || null,
                              quantity: Number(d.get("qty")),
                              idempotencyKey: key(),
                            },
                          );
                          setDetail(updated);
                          form.reset();
                        }, "Scan validé et préparation enregistrée.");
                      }}
                    >
                      <label>
                        Scanner ou saisir le code-barres
                        <input
                          name="barcode"
                          autoComplete="off"
                          inputMode="numeric"
                          required
                          maxLength={100}
                        />
                      </label>
                      <div className="form-grid">
                        {t.fefoManaged && (
                          <label>
                            Lot lu sur le produit
                            <input name="lot" required maxLength={100} />
                          </label>
                        )}
                        <label>
                          Quantité contrôlée
                          <input
                            name="qty"
                            type="number"
                            min="1"
                            max={t.quantity - t.pickedQuantity}
                            defaultValue="1"
                            required
                          />
                        </label>
                      </div>
                      <button type="submit" className="button">
                        Valider le scan
                      </button>
                    </form>
                  )}
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}

export function LotInventory({
  revision,
  run,
}: {
  revision: number;
  run: Run;
}) {
  const [data, setData] = useState<any>(null),
    [productId, setProductId] = useState(""),
    [selected, setSelected] = useState<any>(null),
    [activating, setActivating] = useState(false),
    [lotCount, setLotCount] = useState(1),
    [error, setError] = useState("");
  useEffect(() => {
    api(
      "/admin/warehouse/lots" +
        (productId ? "?productId=" + encodeURIComponent(productId) : ""),
    )
      .then((d) => {
        setData(d);
        setSelected((old: any) =>
          old ? d.items.find((l: any) => l.id === old.id) : null,
        );
        setError("");
      })
      .catch((e) => setError(e.message));
  }, [revision, productId]);
  return (
    <section className="panel module-section">
      <div className="panel-heading">
        <div>
          <h2>Lots, péremption & comptages physiques</h2>
          <p>
            La gestion FEFO s’active après un comptage réel. Un stock existant
            n’est jamais transformé en lots inventés.
          </p>
        </div>
        <button
          className="button secondary"
          onClick={() => setActivating(!activating)}
        >
          {activating
            ? "Fermer le comptage"
            : "Activer un produit par comptage"}
        </button>
      </div>
      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}
      {activating && (
        <form
          className="action-form"
          onChange={(e) => {
            const target = e.target;
            if (
              target instanceof HTMLSelectElement &&
              target.name === "productId"
            )
              setProductId(target.value);
          }}
          onSubmit={(e) => {
            e.preventDefault();
            const d = new FormData(e.currentTarget);
            void run(async () => {
              await post("/admin/warehouse/activate", {
                productId: d.get("productId"),
                expectedPhysicalStock: data?.stock?.onHand ?? null,
                reason: d.get("reason"),
                proofReference: d.get("proof"),
                idempotencyKey: key(),
                lots: Array.from({ length: lotCount }, (_, i) => ({
                  lotNumber: d.get("lot-" + i),
                  expiresAt: d.get("expiry-" + i)
                    ? new Date(
                        String(d.get("expiry-" + i)) + "T23:59:00+01:00",
                      ).toISOString()
                    : null,
                  quantity: Number(d.get("qty-" + i)),
                  quarantined: 0,
                })),
              });
              setActivating(false);
            }, "Lots comptés et gestion FEFO activée. Les futures réservations suivent les péremptions.");
          }}
        >
          <ProductPicker />
          {productId && (
            <div className="note">
              <p>
                Stock physique précédemment enregistré :{" "}
                {data?.stock?.onHand ?? "Non confirmé"} · réservé :{" "}
                {data?.stock?.reserved ?? 0} · en quarantaine :{" "}
                {data?.stock?.quarantined ?? 0}. L’activation exige aucune
                réservation ni ancienne quarantaine ouverte.
              </p>
              {data?.controls?.length > 0 && (
                <strong>
                  Ce produit est déjà géré par lots. Utilisez ses comptages
                  individuels.
                </strong>
              )}
            </div>
          )}
          {Array.from({ length: lotCount }, (_, i) => (
            <fieldset key={i}>
              <legend>Lot physiquement identifié {i + 1}</legend>
              <div className="form-grid">
                <label>
                  Numéro de lot
                  <input name={"lot-" + i} required maxLength={100} />
                </label>
                <label>
                  Date de péremption
                  <input name={"expiry-" + i} type="date" />
                </label>
                <label>
                  Unités vendables réellement comptées
                  <input
                    name={"qty-" + i}
                    type="number"
                    min="0"
                    max="1000000"
                    required
                  />
                </label>
              </div>
            </fieldset>
          ))}
          <button
            className="button secondary"
            type="button"
            disabled={lotCount >= 200}
            onClick={() => setLotCount(lotCount + 1)}
          >
            Ajouter un lot identifié
          </button>
          <div className="form-grid">
            <label>
              Référence du relevé de comptage
              <input name="proof" required maxLength={300} />
            </label>
            <label>
              Motif et constat
              <input name="reason" required maxLength={1000} />
            </label>
          </div>
          <button
            className="button"
            disabled={
              !productId ||
              data?.product?.id !== productId ||
              !!data?.controls?.length
            }
            type="submit"
          >
            Enregistrer le comptage et activer FEFO
          </button>
        </form>
      )}
      {data?.items?.length ? (
        <>
          <p className="metrics-note">{data.policy}</p>
          <div className="table-wrap module-section">
            <table>
              <thead>
                <tr>
                  <th>Produit / lot</th>
                  <th>Péremption</th>
                  <th>Physique</th>
                  <th>Réservé</th>
                  <th>Éligible</th>
                  <th>Quarantaine</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((l: any) => (
                  <tr
                    key={l.id}
                    tabIndex={0}
                    onClick={() => setSelected(l)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") setSelected(l);
                    }}
                  >
                    <td>
                      <strong>{l.productName}</strong>
                      <small>
                        {l.lotNumber} · {l.barcode}
                      </small>
                    </td>
                    <td>
                      <span
                        className={"badge " + (l.expired ? "red" : "neutral")}
                      >
                        {l.expiresAt
                          ? date(l.expiresAt)
                          : "Date non renseignée"}
                      </span>
                    </td>
                    <td>{l.onHand}</td>
                    <td>{l.reserved}</td>
                    <td>{l.eligibleAvailable}</td>
                    <td>{l.quarantined}</td>
                    <td>Compter / contrôler ↗</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <div className="empty">
          <p>
            Aucun lot compté dans cette sélection. Le catalogue reste
            explicitement en stock non tracé tant que le comptage n’a pas été
            réalisé.
          </p>
        </div>
      )}
      {selected && (
        <div className="split-panels module-section">
          <form
            className="action-form"
            key={"count-" + selected.id + "-" + selected.version}
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(
                () =>
                  post("/admin/warehouse/lots/" + selected.id + "/count", {
                    version: selected.version,
                    quantity: Number(d.get("qty")),
                    reason: d.get("reason"),
                    proofReference: d.get("proof"),
                    idempotencyKey: key(),
                  }),
                "Comptage de lot enregistré avec sa preuve.",
              );
            }}
          >
            <h3>Recompter le lot {selected.lotNumber}</h3>
            <label>
              Unités vendables constatées
              <input
                name="qty"
                type="number"
                min={selected.reserved}
                defaultValue={selected.onHand}
                required
              />
            </label>
            <label>
              Référence du comptage
              <input name="proof" required maxLength={300} />
            </label>
            <label>
              Motif de correction
              <input name="reason" required maxLength={1000} />
            </label>
            <button className="button secondary" type="submit">
              Enregistrer le comptage
            </button>
          </form>
          <form
            className="action-form"
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(
                () =>
                  post("/admin/warehouse/lots/" + selected.id + "/quality", {
                    version: selected.version,
                    quantity: Number(d.get("qty")),
                    disposition: d.get("disposition"),
                    reason: d.get("reason"),
                    idempotencyKey: key(),
                  }),
                "Décision qualité enregistrée sur le lot.",
              );
            }}
          >
            <h3>Contrôle qualité du lot</h3>
            <p>
              Une quarantaine fournisseur ou un retour se contrôle depuis son
              dossier d’origine.
            </p>
            <label>
              Quantité
              <input name="qty" type="number" min="1" required />
            </label>
            <label>
              Décision
              <select name="disposition">
                <option value="QUARANTINE">Isoler du stock vendable</option>
                {/^(COUNT|QUALITY):/.test(selected.sourceKey) && (
                  <>
                    <option value="RELEASE">Libérer après contrôle</option>
                    <option value="DESTROY">Déclasser / détruire</option>
                  </>
                )}
              </select>
            </label>
            <label>
              Motif
              <input name="reason" required maxLength={1000} />
            </label>
            <button className="button secondary" type="submit">
              Valider le contrôle qualité
            </button>
          </form>
        </div>
      )}
    </section>
  );
}

export function ReturnLotIdentification({
  sourceType,
  sourceId,
  max,
  run,
}: {
  sourceType: "CLAIM" | "REFUSAL" | "PROMOTION";
  sourceId: string;
  max: number;
  run: Run;
}) {
  return (
    <details className="return-lot-identification">
      <summary>Identifier le lot retourné · produit géré par lots</summary>
      <form
        className="action-form"
        onSubmit={(e) => {
          e.preventDefault();
          const d = new FormData(e.currentTarget);
          void run(
            () =>
              post("/admin/warehouse/returns/identify", {
                sourceType,
                sourceId,
                quantity: Number(d.get("quantity")),
                lotNumber: d.get("lot"),
                expiresAt: d.get("expiry")
                  ? new Date(
                      String(d.get("expiry")) + "T23:59:00+01:00",
                    ).toISOString()
                  : null,
                proofReference: d.get("proof"),
                idempotencyKey: key(),
              }),
            "Lot du retour identifié. Le contrôle qualité reste à effectuer.",
          );
        }}
      >
        <p>
          Pour un produit géré par lots, le numéro et la date doivent
          correspondre à un lot réellement expédié. Cette identification ne
          remet aucune marchandise en vente.
        </p>
        <div className="form-grid">
          <label>
            Lot lu sur le retour
            <input name="lot" required maxLength={100} />
          </label>
          <label>
            Péremption d’origine
            <input name="expiry" type="date" />
          </label>
          <label>
            Quantité identifiée
            <input name="quantity" type="number" min="1" max={max} required />
          </label>
          <label>
            Preuve de réception et d’identification
            <input name="proof" required maxLength={300} />
          </label>
        </div>
        <button className="button secondary" type="submit">
          Enregistrer l’identification du lot
        </button>
      </form>
    </details>
  );
}
