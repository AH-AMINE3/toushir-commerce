import { useEffect, useRef, useState } from "react";
import QRCode from "qrcode";
import { api, post, money, date } from "./api";
type Run = (fn: () => Promise<unknown>, message?: string) => Promise<void>;
function Qr({ proof }: { proof: any }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    if (canvas.current)
      void QRCode.toCanvas(canvas.current, proof.qrPayload, {
        width: 230,
        margin: 2,
        errorCorrectionLevel: "M",
      });
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [proof.challenge]);
  const seconds = Math.max(
    0,
    Math.ceil((new Date(proof.expiresAt).getTime() - now) / 1000),
  );
  return (
    <div className="custody-qr">
      <canvas
        ref={canvas}
        aria-label="Code QR de transfert de garde du manifeste"
      />
      <div>
        <strong>
          {seconds
            ? `Code valable encore ${seconds} s`
            : "Code expiré · générez un nouveau code"}
        </strong>
        <p>
          Le chauffeur affecté scanne ce code dans son application. Le
          manifeste, les quantités et le fond de caisse sont liés à ce
          transfert.
        </p>
        <input
          readOnly
          value={proof.challenge}
          aria-label="Code de garde à copier manuellement"
        />
        <button
          className="button secondary"
          onClick={() => void navigator.clipboard.writeText(proof.qrPayload)}
        >
          Copier le code de prise en charge
        </button>
      </div>
    </div>
  );
}
export default function RouteConsole({
  orders,
  team,
  revision,
  run,
  cashOnly = false,
  open,
  role,
}: {
  orders: any[];
  team: any[];
  revision: number;
  run: Run;
  cashOnly?: boolean;
  open: (id: string) => void;
  role: string;
}) {
  const [routes, setRoutes] = useState<any[]>([]),
    [active, setActive] = useState<string | null>(null),
    [planning, setPlanning] = useState(false),
    [proof, setProof] = useState<any>(null),
    [error, setError] = useState("");
  useEffect(() => {
    api("/operations/routes")
      .then((d) => setRoutes(d.items))
      .catch((e) => setError(e.message));
  }, [revision]);
  const route = routes.find((r) => r.id === active);
  const eligible = orders.filter(
    (o) =>
      ["CONFIRMED", "PREPARING", "READY"].includes(o.status) &&
      !routes.some((r) => r.stops.some((s: any) => s.id === o.id)),
  );
  const drivers = team.filter((t) => t.role === "DRIVER");
  const action = (fn: () => Promise<unknown>, message?: string) =>
    void run(fn, message);
  const reorder = (index: number, delta: number) => {
    if (!route) return;
    const ids = route.stops.map((s: any) => s.id);
    [ids[index], ids[index + delta]] = [ids[index + delta], ids[index]];
    action(
      () =>
        post("/operations/routes/" + route.id + "/reorder", {
          version: route.version,
          orderIds: ids,
          capacityUnits: route.capacityUnits,
        }),
      "Ordre des arrêts enregistré. Tout ancien code de garde est invalidé.",
    );
  };
  return (
    <section className="panel module-section">
      <div className="panel-heading">
        <div>
          <h2>
            {cashOnly ? "Caisses de tournée" : "Tournées & transfert de garde"}
          </h2>
          <p>
            {cashOnly
              ? "Fond initial, espèces nettes et comptage au retour. Un écart reste interne."
              : "Un manifeste partagé, un chauffeur affecté et une prise en charge vérifiée."}
          </p>
        </div>
        {!cashOnly && ["ADMIN", "DISPATCHER"].includes(role) && (
          <button className="button" onClick={() => setPlanning(!planning)}>
            {planning ? "Fermer la planification" : "Planifier une tournée"}
          </button>
        )}
      </div>
      {error && (
        <div role="alert" className="error-banner">
          {error}
        </div>
      )}
      {planning && (
        <form
          className="action-form route-plan"
          onSubmit={(e) => {
            e.preventDefault();
            const d = new FormData(e.currentTarget);
            action(async () => {
              const r = await post("/operations/routes", {
                name: d.get("name"),
                driverId: d.get("driver"),
                vehicleReference: d.get("vehicle"),
                openingFloat: Number(d.get("float")),
                capacityUnits: Number(d.get("capacity")),
                orderIds: d.getAll("orders"),
              });
              setActive(r.id);
              setPlanning(false);
            }, "Tournée planifiée. Son manifeste apparaît dans l’application du chauffeur.");
          }}
        >
          <div className="form-grid">
            <label>
              Nom de tournée
              <input
                name="name"
                placeholder="Alger Est · Matin"
                required
                maxLength={120}
              />
            </label>
            <label>
              Chauffeur
              <select name="driver" required>
                <option value="">Choisir un chauffeur</option>
                {drivers.map((d) => (
                  <option value={d.id} key={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Véhicule / immatriculation
              <input name="vehicle" maxLength={100} />
            </label>
            <label>
              Capacité du véhicule · unités commerciales
              <input
                name="capacity"
                type="number"
                min="1"
                max="10000000"
                required
                placeholder="Capacité vérifiée du véhicule"
              />
            </label>
            <label>
              Fond initial de monnaie · DA
              <input
                name="float"
                type="number"
                min="0"
                step="0.01"
                defaultValue="0"
                required
              />
            </label>
          </div>
          <h3>Commandes à intégrer</h3>
          {eligible.length ? (
            <div className="manifest-select">
              {eligible.map((o) => (
                <label key={o.id}>
                  <input name="orders" type="checkbox" value={o.id} />
                  <span>
                    <strong>
                      {o.number} · {o.merchant.shopName}
                    </strong>
                    <small>
                      {o.quantity} unités · {money(o.total)}
                    </small>
                  </span>
                  <span className="badge neutral">
                    {o.status === "READY" ? "Prête" : "Préparation à terminer"}
                  </span>
                </label>
              ))}
            </div>
          ) : (
            <p>
              Aucune commande disponible. Confirmez une commande avant de
              planifier son départ.
            </p>
          )}
          <button
            className="button"
            type="submit"
            disabled={!eligible.length || !drivers.length}
          >
            Enregistrer le manifeste
          </button>
        </form>
      )}
      {routes.length ? (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Tournée</th>
                <th>Chauffeur</th>
                <th>Arrêts</th>
                <th>Garde</th>
                <th>Caisse attendue</th>
                <th>État de caisse</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {routes.map((r) => (
                <tr
                  key={r.id}
                  tabIndex={0}
                  onClick={() => {
                    setActive(active === r.id ? null : r.id);
                    setProof(null);
                  }}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      setActive(r.id);
                      setProof(null);
                    }
                  }}
                >
                  <td>
                    <strong>{r.name}</strong>
                    <small>{r.vehicleReference || "Véhicule à préciser"}</small>
                  </td>
                  <td>{r.driver.name}</td>
                  <td>{r.stops.length}</td>
                  <td>
                    <span
                      className={"badge " + (r.custody ? "green" : "neutral")}
                    >
                      {r.custody ? "Acceptée par le chauffeur" : "À transférer"}
                    </span>
                  </td>
                  <td>{money(r.cash.expected)}</td>
                  <td>
                    <span className="badge neutral">
                      {(
                        {
                          OPEN: "Ouverte",
                          DECLARED: "Déclarée",
                          COUNTED: "Comptée",
                          RECONCILED: "Rapprochée",
                        } as any
                      )[r.cash.state] || r.cash.state}
                    </span>
                  </td>
                  <td>Ouvrir ↗</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="empty">
          <p>
            {cashOnly
              ? "Les caisses apparaîtront après la création des tournées."
              : "Planifiez une tournée pour partager un manifeste avec un chauffeur."}
          </p>
        </div>
      )}
      {route && (
        <div className="route-detail">
          <div className="section-heading">
            <h3>{route.name}</h3>
            <span>
              Version {route.version} · {route.driver.name}
            </span>
          </div>
          <p className="metrics-note">
            Charge planifiée :{" "}
            {route.loadUnits ??
              route.stops.reduce(
                (n: number, o: any) =>
                  n + o.items.reduce((a: number, l: any) => a + l.quantity, 0),
                0,
              )}{" "}
            / {route.capacityUnits ?? "à vérifier"} unités commerciales · poids
            et volume à vérifier au chargement.
          </p>
          <div className="manifest-stops">
            {route.stops.map((o: any, index: number) => (
              <div className="ranked-stop" key={o.id}>
                <span className="stop-rank">{index + 1}</span>
                <button onClick={() => open(o.id)}>
                  <strong>{o.number}</strong>
                  <span>{o.merchant.shopName}</span>
                  <small>
                    {o.items.reduce((a: number, l: any) => a + l.quantity, 0)}{" "}
                    unités · {money(o.total)}
                  </small>
                  <span>Ouvrir le dossier ↗</span>
                </button>
                {route.status === "PLANNED" &&
                  ["ADMIN", "DISPATCHER"].includes(role) && (
                    <div className="stop-controls">
                      <button
                        aria-label={"Avancer l’arrêt " + o.number}
                        disabled={index === 0}
                        onClick={() => reorder(index, -1)}
                      >
                        ↑
                      </button>
                      <button
                        aria-label={"Reculer l’arrêt " + o.number}
                        disabled={index === route.stops.length - 1}
                        onClick={() => reorder(index, 1)}
                      >
                        ↓
                      </button>
                    </div>
                  )}
              </div>
            ))}
          </div>
          {!cashOnly &&
            ["ADMIN", "WAREHOUSE"].includes(role) &&
            route.status === "PLANNED" && (
              <>
                <p className="metrics-note">
                  Contrôlez chaque colis. Le code n’est disponible que lorsque
                  toutes les lignes de toutes les commandes sont préparées.
                </p>
                <button
                  className="button module-section"
                  onClick={() =>
                    action(
                      async () =>
                        setProof(
                          await post(
                            "/operations/routes/" +
                              route.id +
                              "/manifest-challenge",
                            { version: route.version },
                          ),
                        ),
                      "Code de garde créé pour ce manifeste.",
                    )
                  }
                >
                  Afficher le QR de prise en charge
                </button>
                {proof && <Qr proof={proof} />}
              </>
            )}
          {["ADMIN", "WAREHOUSE"].includes(role) &&
            route.returns
              ?.filter((r: any) => r.requiresReceipt)
              .map((r: any) => (
                <form
                  key={r.orderId}
                  className="action-form"
                  onSubmit={(e) => {
                    e.preventDefault();
                    const d = new FormData(e.currentTarget);
                    action(
                      () =>
                        post("/operations/routes/" + route.id + "/returns", {
                          orderId: r.orderId,
                          version: r.version,
                          items: r.items.map((l: any) => ({
                            productId: l.productId,
                            quantity: Number(d.get(l.productId)),
                          })),
                          note: d.get("note"),
                        }),
                      "Retour physique reçu au dépôt. La quarantaine reste soumise au contrôle qualité.",
                    );
                  }}
                >
                  <h3>Recevoir les retours · {r.number}</h3>
                  <p>
                    Comptez les marchandises réellement revenues. La caisse ne
                    peut être déclarée tant que la garde n’est pas soldée. Cette
                    réception ne remet rien en vente.
                  </p>
                  {r.items.map((l: any) => (
                    <label className="prepare-line" key={l.productId}>
                      <span>
                        <strong>{l.name}</strong>
                        <small>
                          {l.remainingQuantity ?? l.quantity} unités sous garde
                          attendues
                        </small>
                      </span>
                      <input
                        type="number"
                        name={l.productId}
                        min="0"
                        max={l.remainingQuantity ?? l.quantity}
                        required
                        defaultValue="0"
                      />
                    </label>
                  ))}
                  <label>
                    Constat au dépôt
                    <input name="note" required minLength={3} maxLength={800} />
                  </label>
                  <button className="button" type="submit">
                    Confirmer la réception physique
                  </button>
                </form>
              ))}
          <div className="cash-grid">
            <div>
              <h3>Rapprochement des fonds</h3>
              <dl className="totals">
                <div>
                  <dt>Fond initial de monnaie</dt>
                  <dd>{money(route.cash.openingFloat)}</dd>
                </div>
                <div>
                  <dt>Espèces reçues des clients</dt>
                  <dd>{money(route.cash.tendered)}</dd>
                </div>
                <div>
                  <dt>Monnaie rendue</dt>
                  <dd>{money(route.cash.change)}</dd>
                </div>
                <div>
                  <dt>Encaissements nets</dt>
                  <dd>{money(route.cash.netReceipts)}</dd>
                </div>
                <div>
                  <dt>Remise attendue</dt>
                  <dd>{money(route.cash.expected)}</dd>
                </div>
                <div>
                  <dt>Remise déclarée</dt>
                  <dd>
                    {route.cash.declared == null
                      ? "À déclarer par le chauffeur"
                      : money(route.cash.declared)}
                  </dd>
                </div>
                <div>
                  <dt>Remise comptée</dt>
                  <dd>
                    {route.cash.counted == null
                      ? "À compter par la finance"
                      : money(route.cash.counted)}
                  </dd>
                </div>
              </dl>
            </div>
            <div className="cash-integrity">
              <h3>Les soldes contractuels des magasins</h3>
              <strong>
                {money(
                  route.stops.reduce(
                    (a: number, o: any) => a + (o.balance.remaining || 0),
                    0,
                  ),
                )}
              </strong>
              <p>Solde à encaisser sur les commandes de cette tournée.</p>
              {route.cash.shortage > 0 ? (
                <>
                  <h3 className="spaced">Écart interne à expliquer</h3>
                  <strong className="cash-shortage">
                    − {money(route.cash.shortage)}
                  </strong>
                </>
              ) : route.cash.surplus > 0 ? (
                <>
                  <h3 className="spaced">Excédent à expliquer</h3>
                  <strong>{money(route.cash.surplus)}</strong>
                </>
              ) : null}
              <p>
                Un écart de caisse ne recrée jamais une dette du commerçant.
              </p>
            </div>
          </div>
          {["ADMIN", "FINANCE"].includes(role) &&
            route.cash.state === "DECLARED" && (
              <form
                className="action-form"
                onSubmit={(e) => {
                  e.preventDefault();
                  const d = new FormData(e.currentTarget);
                  action(
                    () =>
                      post("/operations/routes/" + route.id + "/cash-count", {
                        version: route.version,
                        countedAmount: Number(d.get("counted")),
                        note: d.get("note"),
                      }),
                    "Comptage de caisse enregistré. Les soldes clients restent inchangés.",
                  );
                }}
              >
                <h3>Comptage physique par la finance</h3>
                <div className="form-grid">
                  <label>
                    Espèces réellement comptées · DA
                    <input
                      type="number"
                      name="counted"
                      min="0"
                      step="0.01"
                      required
                    />
                  </label>
                  <label>
                    Constat de comptage
                    <input name="note" required maxLength={800} />
                  </label>
                </div>
                <button className="button" type="submit">
                  Enregistrer le comptage
                </button>
              </form>
            )}
          {["ADMIN", "FINANCE"].includes(role) &&
            route.cash.state === "COUNTED" && (
              <form
                className="action-form"
                onSubmit={(e) => {
                  e.preventDefault();
                  const d = new FormData(e.currentTarget);
                  action(
                    () =>
                      post("/operations/routes/" + route.id + "/reconcile", {
                        version: route.version,
                        note: d.get("note"),
                      }),
                    "Tournée rapprochée. L’écart et son explication restent dans l’historique.",
                  );
                }}
              >
                <label>
                  Explication / référence du rapprochement
                  <textarea name="note" required maxLength={800} />
                </label>
                <button className="button" type="submit">
                  Clôturer le rapprochement
                </button>
              </form>
            )}
          {route.reconciledAt && (
            <p className="metrics-note">
              Rapprochée le {date(route.reconciledAt, true)} ·{" "}
              {route.cash.financeNote}
            </p>
          )}
        </div>
      )}
    </section>
  );
}
