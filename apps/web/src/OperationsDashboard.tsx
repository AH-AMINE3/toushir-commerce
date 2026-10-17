import { useEffect, useId, useState } from "react";
import { api, date, labels, money, number } from "./api";

type Navigate = (route: string, status?: string) => void;
const steps = [
  {
    status: "REQUESTED",
    name: "À vérifier",
    team: "Commerce",
    action: "Vérifier les lignes",
    route: "orders",
  },
  {
    status: "QUOTED",
    name: "Accord magasin",
    team: "Magasin",
    action: "Consulter l’offre",
    route: "orders",
  },
  {
    status: "CONFIRMED",
    name: "Confirmées",
    team: "Entrepôt",
    action: "Préparer la commande",
    route: "picking",
  },
  {
    status: "PREPARING",
    name: "En préparation",
    team: "Entrepôt",
    action: "Vérifier les prélèvements",
    route: "picking",
  },
  {
    status: "READY",
    name: "Prêtes au départ",
    team: "Expédition",
    action: "Affecter le chauffeur",
    route: "delivery",
  },
  {
    status: "OUT_FOR_DELIVERY",
    name: "En livraison",
    team: "Livraison",
    action: "Suivre la remise",
    route: "delivery",
  },
];
const metricTitles: Record<string, string> = {
  revenue: "Ventes réceptionnées",
  sales: "Ventes réceptionnées",
  collected: "Encaissements nets",
  orders: "Demandes reçues",
};

export async function allPages(path: string): Promise<any[]> {
  const first = await api(
    path + (path.includes("?") ? "&" : "?") + "pageSize=100&page=1",
  );
  const rows = [...first.items];
  for (let page = 2; page <= first.totalPages; page++) {
    const result = await api(
      path + (path.includes("?") ? "&" : "?") + "pageSize=100&page=" + page,
    );
    rows.push(...result.items);
  }
  return rows;
}

export function DownloadList({
  rows,
  name = "magasins-toushir",
}: {
  rows: any[];
  name?: string;
}) {
  const download = () => {
    const cell = (value: unknown) => {
      let text = String(value ?? "");
      if (/^[=+\-@\t\r]/.test(text)) text = "'" + text;
      return '"' + text.replaceAll('"', '""') + '"';
    };
    const csv = [
      ["Magasin", "Contact", "Téléphone", "Région", "Commune", "Statut"],
      ...rows.map((m) => [
        m.shopName || m.name,
        m.contact || m.name,
        m.phone,
        m.region,
        m.commune,
        labels[m.approvalStatus] || m.approvalStatus,
      ]),
    ]
      .map((row) => row.map(cell).join(","))
      .join("\r\n");
    const url = URL.createObjectURL(
      new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8" }),
    );
    const link = document.createElement("a");
    link.href = url;
    link.download = name + ".csv";
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };
  return (
    <button
      className="button secondary"
      type="button"
      disabled={!rows.length}
      onClick={download}
    >
      Exporter la liste · {rows.length}
    </button>
  );
}

export function ShopAudience({
  shops,
  selected,
  onChange,
  title = "Choisir les magasins",
}: {
  shops: any[];
  selected: string[];
  onChange: (ids: string[]) => void;
  title?: string;
}) {
  const [query, setQuery] = useState(""),
    [region, setRegion] = useState("");
  const regions = [
    ...new Set<string>(shops.map((shop) => shop.region).filter(Boolean)),
  ].sort();
  const visible = shops.filter(
    (shop) =>
      (!region || shop.region === region) &&
      `${shop.shopName || shop.name} ${shop.contact || shop.name} ${shop.phone || ""} ${shop.region || ""} ${shop.commune || ""}`
        .toLocaleLowerCase("fr")
        .includes(query.toLocaleLowerCase("fr")),
  );
  return (
    <div className="audience-selector">
      <h3>{title}</h3>
      <p>
        Les filtres servent à constituer une liste explicite de magasins. La
        sélection reste la même si une adresse change.
      </p>
      <div className="directory-toolbar">
        <label>
          Nom ou téléphone
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Rechercher un magasin…"
          />
        </label>
        <label>
          Région
          <select value={region} onChange={(e) => setRegion(e.target.value)}>
            <option value="">Toutes les régions</option>
            {regions.map((name) => (
              <option key={name}>{name}</option>
            ))}
          </select>
        </label>
        <DownloadList rows={visible} name="audience-magasins-toushir" />
      </div>
      <div className="heading-actions">
        <button
          className="button secondary"
          type="button"
          disabled={!visible.length}
          onClick={() =>
            onChange([
              ...new Set([...selected, ...visible.map((shop) => shop.id)]),
            ])
          }
        >
          Sélectionner les {visible.length} magasins affichés
        </button>
        <button
          className="text-action"
          type="button"
          disabled={!selected.length}
          onClick={() =>
            onChange(
              selected.filter((id) => !visible.some((shop) => shop.id === id)),
            )
          }
        >
          Retirer les magasins affichés
        </button>
      </div>
      <div className="audience-shops">
        {visible.map((shop) => (
          <label className="audience-shop" key={shop.id}>
            <input
              type="checkbox"
              checked={selected.includes(shop.id)}
              onChange={(e) =>
                onChange(
                  e.target.checked
                    ? [...selected, shop.id]
                    : selected.filter((id) => id !== shop.id),
                )
              }
            />
            <span>
              <strong>{shop.shopName || shop.name}</strong>
              <small>
                {shop.region || "Région non renseignée"}
                {shop.commune ? " · " + shop.commune : ""} ·{" "}
                {shop.phone || "Téléphone non renseigné"}
              </small>
            </span>
          </label>
        ))}
        {!visible.length && (
          <div className="insight-empty">
            Aucun magasin ne correspond à ces filtres.
          </div>
        )}
      </div>
      <p className="audience-selection-summary" role="status">
        {selected.length} magasin{selected.length === 1 ? "" : "s"} sélectionné
        {selected.length === 1 ? "" : "s"} · {visible.length} affiché
        {visible.length === 1 ? "" : "s"}
      </p>
    </div>
  );
}

/** Values remain available as text; pointer, keyboard and touch select the same server day. */
export function ActivityChart({
  points,
  previous = [],
  field,
  title,
}: {
  points: any[];
  previous?: any[];
  field: string;
  title?: string;
}) {
  const [selected, setSelected] = useState<number | null>(null);
  const id = useId().replaceAll(":", "");
  useEffect(() => {
    setSelected(null);
  }, [points.length, field, points[0]?.date]);
  if (!points.length)
    return (
      <div className="insight-empty">
        Les mouvements enregistrés apparaîtront ici.
      </div>
    );
  const values = points.map((p) => Number(p[field] || 0)),
    prior = previous.map((p) => Number(p[field] || 0));
  const low = Math.min(0, ...values, ...prior),
    high = Math.max(1, ...values, ...prior),
    span = high - low;
  const x = (index: number) =>
    64 + index * (712 / Math.max(1, points.length - 1));
  const y = (value: number) => 30 + ((high - value) / span) * 188;
  const coords = values.map((value, index) => `${x(index)},${y(value)}`);
  const index =
    selected == null
      ? points.length - 1
      : Math.min(selected, points.length - 1);
  const total = values.reduce((sum, value) => sum + value, 0);
  const format = (value: number) =>
    field === "orders" ? number(value) : money(value);
  const caption = title || metricTitles[field] || "Activité";
  return (
    <div className="activity-chart">
      <div className="chart-reading" aria-live="polite" aria-atomic="true">
        <div>
          <span>
            {selected == null
              ? "Total sur la période"
              : date(points[index].date)}
          </span>
          <strong>{format(selected == null ? total : values[index])}</strong>
        </div>
        <div className="chart-key">
          <span>
            <i />
            {caption}
          </span>
          {prior.length > 0 && (
            <span>
              <i className="previous" />
              Période précédente
            </span>
          )}
        </div>
      </div>
      <svg
        viewBox="0 0 800 255"
        role="img"
        aria-label={
          caption +
          " par jour. Utilisez le curseur sous le graphique pour lire les valeurs."
        }
        onPointerMove={(event) => {
          const rect = event.currentTarget.getBoundingClientRect();
          const position =
            (((event.clientX - rect.left) / rect.width) * 800 - 64) / 712;
          setSelected(
            Math.max(
              0,
              Math.min(
                points.length - 1,
                Math.round(position * (points.length - 1)),
              ),
            ),
          );
        }}
        onPointerLeave={() => setSelected(null)}
      >
        <defs>
          <linearGradient id={id} x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" stopColor="var(--green)" stopOpacity=".19" />
            <stop offset="100%" stopColor="var(--green)" stopOpacity=".015" />
          </linearGradient>
        </defs>
        {[0, 1, 2, 3].map((tick) => {
          const value = high - (tick * span) / 3;
          return (
            <g key={tick}>
              <line
                x1="64"
                x2="776"
                y1={y(value)}
                y2={y(value)}
                stroke="var(--line)"
              />
              <text
                x="54"
                y={y(value) + 4}
                textAnchor="end"
                fontSize="11"
                fill="var(--muted)"
              >
                {Intl.NumberFormat("fr-DZ", {
                  notation: "compact",
                  maximumFractionDigits: 1,
                }).format(value)}
              </text>
            </g>
          );
        })}
        {prior.length > 0 && (
          <polyline
            points={prior.map((value, i) => `${x(i)},${y(value)}`).join(" ")}
            fill="none"
            stroke="var(--muted)"
            strokeDasharray="5 6"
            strokeWidth="1.6"
          />
        )}
        <path
          d={`M${coords.join(" L")} L${x(points.length - 1)},${y(0)} L${x(0)},${y(0)} Z`}
          fill={`url(#${id})`}
        />
        <polyline
          className="activity-curve"
          points={coords.join(" ")}
          fill="none"
          stroke="var(--green)"
          strokeWidth="3"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        {selected != null && (
          <g>
            <line
              x1={x(index)}
              x2={x(index)}
              y1="24"
              y2="220"
              stroke="var(--green)"
              strokeDasharray="3 4"
            />
            <circle
              cx={x(index)}
              cy={y(values[index])}
              r="5"
              fill="var(--surface)"
              stroke="var(--green)"
              strokeWidth="3"
            />
          </g>
        )}
        {[0, Math.floor((points.length - 1) / 2), points.length - 1]
          .filter((v, i, arr) => arr.indexOf(v) === i)
          .map((i) => (
            <text
              key={i}
              x={x(i)}
              y="248"
              textAnchor={
                i === 0 ? "start" : i === points.length - 1 ? "end" : "middle"
              }
              fontSize="11"
              fill="var(--muted)"
            >
              {date(points[i].date)}
            </text>
          ))}
      </svg>
      <div className="chart-day-control">
        <label htmlFor={id + "-day"}>Explorer les jours</label>
        <input
          id={id + "-day"}
          type="range"
          min="0"
          max={points.length - 1}
          value={index}
          onChange={(e) => setSelected(Number(e.target.value))}
          aria-valuetext={`${date(points[index].date)} : ${format(values[index])}`}
        />
        <span>
          {date(points[index].date)} · {format(values[index])}
        </span>
      </div>
      {selected != null && prior[index] != null && (
        <p className="chart-comparison">
          Même jour de la période précédente : {format(prior[index])} ·{" "}
          {date(previous[index].date)}
        </p>
      )}
    </div>
  );
}

export function WorkflowQueue({
  orders,
  open,
}: {
  orders: any[];
  open: (id: string) => void;
}) {
  const [team, setTeam] = useState("ALL");
  const groups = [
    "ALL",
    "Commerce",
    "Magasin",
    "Entrepôt",
    "Expédition",
    "Livraison",
  ];
  const pending = orders.filter(
    (o) =>
      steps.some((step) => step.status === o.status) ||
      o.status === "SUBMITTED",
  );
  const selected = pending
    .filter(
      (o) =>
        team === "ALL" ||
        (steps.find((step) => step.status === o.status)?.team || "Commerce") ===
          team,
    )
    .sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt));
  return (
    <section className="panel workflow-queue">
      <div className="panel-heading">
        <div>
          <h2>Le relais entre les équipes</h2>
          <p>
            La prochaine action dépend de l’état réel du dossier. Ouvrez-le pour
            la réaliser.
          </p>
        </div>
        <span className="badge aqua">{pending.length} en cours</span>
      </div>
      <div
        className="insight-tabs"
        role="group"
        aria-label="Équipe responsable de la prochaine action"
      >
        {groups.map((name) => (
          <button
            key={name}
            className={team === name ? "active" : ""}
            aria-pressed={team === name}
            onClick={() => setTeam(name)}
          >
            {name === "ALL" ? "Toutes les équipes" : name}
            <span>
              {
                pending.filter(
                  (o) =>
                    name === "ALL" ||
                    (steps.find((step) => step.status === o.status)?.team ||
                      "Commerce") === name,
                ).length
              }
            </span>
          </button>
        ))}
      </div>
      {!selected.length ? (
        <div className="insight-empty">
          Aucune commande en attente dans cette équipe.
        </div>
      ) : (
        <div className="handoff-list">
          {selected.slice(0, 8).map((o) => {
            const step = steps.find((s) => s.status === o.status);
            return (
              <button key={o.id} onClick={() => open(o.id)}>
                <span className="handoff-symbol" aria-hidden="true">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.6"
                  >
                    <path d="m4 7 8-4 8 4v10l-8 4-8-4V7Zm0 0 8 4 8-4M12 11v10" />
                  </svg>
                </span>
                <span>
                  <strong>{o.number}</strong>
                  <small>
                    {o.merchant?.shopName || o.shopName || "Magasin"} ·{" "}
                    {date(o.createdAt, true)}
                  </small>
                </span>
                <span className="handoff-state">
                  <strong>{step?.action || "Vérifier et confirmer"}</strong>
                  <small>
                    {step?.team || "Commerce"} · {labels[o.status] || o.status}
                  </small>
                </span>
                <span className="handoff-amount">
                  {money(o.total)}
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.8"
                    aria-hidden="true"
                  >
                    <path d="m9 6 6 6-6 6" />
                  </svg>
                </span>
              </button>
            );
          })}
        </div>
      )}
      {selected.length > 8 && (
        <p className="metrics-note">
          Les 8 dossiers les plus anciens sont affichés. La liste des commandes
          contient les {selected.length} dossiers de cette équipe.
        </p>
      )}
    </section>
  );
}

export function Overview({
  dashboard,
  orders,
  navigate,
  open,
}: {
  dashboard: any;
  orders: any[];
  navigate: Navigate;
  open: (id: string) => void;
}) {
  const [metric, setMetric] = useState("sales");
  const summary = dashboard.summary || {},
    counts = dashboard.statusCounts || [];
  const pending =
    Number(summary.requests || 0) +
    Number(summary.quotes || 0) +
    Number(summary.activeOrders || 0);
  return (
    <div className="operations-overview">
      <div className="operating-headline">
        <div>
          <h2>Chaque commande avance.</h2>
          <p>Du magasin à la remise, votre équipe retrouve le même dossier.</p>
        </div>
        <div className="operating-live">
          <i />
          Actualisation des opérations
        </div>
      </div>
      <div className="operating-ledger">
        <button onClick={() => navigate("analytics")}>
          <span>Ventes réceptionnées · 30 jours</span>
          <strong>{money(summary.sales)}</strong>
          <small>
            {number(summary.deliveredOrders)} réceptions enregistrées
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.8"
              aria-hidden="true"
            >
              <path d="M5 19 19 5M5 5h14v14" />
            </svg>
          </small>
        </button>
        <button onClick={() => navigate("orders", "REQUESTED")}>
          <span>Demandes à traiter</span>
          <strong>{number(summary.requests)}</strong>
          <small>{number(summary.quotes)} offres attendent un accord</small>
        </button>
        <button onClick={() => navigate("picking")}>
          <span>Commandes en cours</span>
          <strong>{number(summary.activeOrders)}</strong>
          <small>Préparation, départ et livraison</small>
        </button>
        <button onClick={() => navigate("finance")}>
          <span>Encaissements · 30 jours</span>
          <strong>{money(summary.collected)}</strong>
          <small>Paiements enregistrés moins remboursements</small>
        </button>
      </div>
      <div className="operating-main-grid">
        <section className="panel operating-activity">
          <div className="panel-heading">
            <div>
              <h2>Le rythme de votre activité</h2>
              <p>30 derniers jours · heure d’Alger</p>
            </div>
            <div
              className="insight-tabs"
              role="group"
              aria-label="Indicateur du graphique"
            >
              {[
                ["sales", "Ventes"],
                ["collected", "Encaissements"],
                ["orders", "Demandes"],
              ].map(([field, label]) => (
                <button
                  key={field}
                  aria-pressed={metric === field}
                  className={metric === field ? "active" : ""}
                  onClick={() => setMetric(field)}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
          <ActivityChart points={dashboard.daily || []} field={metric} />
          <p className="metrics-note">
            {dashboard.basis?.[metric] ||
              (metric === "orders"
                ? "Demandes et commandes créées à leur date d’enregistrement."
                : "Montants réellement enregistrés dans cet espace.")}
          </p>
        </section>
        <section className="operating-attention">
          <h2>Les décisions du jour</h2>
          <p>Ouvrir, vérifier, transmettre.</p>
          {[
            [
              summary.requests,
              "Commandes à vérifier",
              "Contrôler les lignes et les conditions",
              "orders",
              "REQUESTED",
            ],
            [
              summary.pendingMerchants,
              "Magasins à approuver",
              "Vérifier leurs informations",
              "merchants",
              "PENDING",
            ],
            [
              summary.lowStock,
              "Stocks sous le seuil",
              "Revoir la disponibilité publiée",
              "stock",
              "",
            ],
          ].map(([count, label, detail, route, status]) => (
            <button
              key={String(label)}
              onClick={() => navigate(String(route), String(status))}
            >
              <strong>{number(Number(count || 0))}</strong>
              <span>
                <b>{label}</b>
                <small>{detail}</small>
              </span>
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.6"
                aria-hidden="true"
              >
                <path d="m9 6 6 6-6 6" />
              </svg>
            </button>
          ))}
          <button className="attention-link" onClick={() => navigate("crm")}>
            Ouvrir le suivi des magasins
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.6"
              aria-hidden="true"
            >
              <path d="M5 12h14m-5-5 5 5-5 5" />
            </svg>
          </button>
        </section>
      </div>
      <section className="panel operational-pipeline">
        <div className="panel-heading">
          <div>
            <h2>De la commande à la livraison</h2>
            <p>{pending} dossiers attendent une action ou une exécution.</p>
          </div>
          <button className="text-action" onClick={() => navigate("orders")}>
            Toutes les commandes <span aria-hidden="true">↗</span>
          </button>
        </div>
        <div className="pipeline-stages">
          {steps.map((step, i) => {
            const count =
              counts.find((c: any) => c.status === step.status)?.count || 0;
            return (
              <button
                key={step.status}
              onClick={() => navigate("orders", step.status)}
              >
                <span className="pipeline-track">
                  <i className={count ? "occupied" : ""} />
                  {i < steps.length - 1 && <b />}
                </span>
                <strong>{count}</strong>
                <span>{step.name}</span>
                <small>{step.team}</small>
              </button>
            );
          })}
        </div>
      </section>
      <WorkflowQueue orders={orders} open={open} />
      <div className="operating-bottom-grid">
        <section className="panel">
          <div className="panel-heading">
            <div>
              <h2>Ce qui arrive de l’app</h2>
              <p>Même dossier. Même historique.</p>
            </div>
            <button className="text-action" onClick={() => navigate("orders")}>
              Consulter
            </button>
          </div>
          <div className="recent-order-list">
            {dashboard.recentOrders?.length ? (
              dashboard.recentOrders.slice(0, 5).map((order: any) => (
                <button key={order.id} onClick={() => open(order.id)}>
                  <span>
                    <strong>{order.shopName}</strong>
                    <small>
                      {order.number} · {date(order.createdAt, true)}
                    </small>
                  </span>
                  <span>
                    <b>{money(order.total)}</b>
                    <small>{labels[order.status] || order.status}</small>
                  </span>
                </button>
              ))
            ) : (
              <div className="insight-empty">
                La prochaine commande de l’application apparaîtra ici.
              </div>
            )}
          </div>
        </section>
        <section className="panel operating-delivery">
          <div>
            <svg
              viewBox="0 0 48 48"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.7"
              aria-hidden="true"
            >
              <path d="M4 12h25v24H4V12Zm25 10h9l7 9v5H29V22ZM10 36a5 5 0 1 0 10 0m14 0a5 5 0 1 0 10 0M4 20h14m-14 6h9" />
            </svg>
            <h2>Du dépôt au commerce</h2>
            <p>
              {counts.find((c: any) => c.status === "OUT_FOR_DELIVERY")
                ?.count || 0}{" "}
              commandes en cours de livraison. Préparation, garde, position et
              réception restent traçables.
            </p>
          </div>
          <button className="button" onClick={() => navigate("delivery")}>
            Ouvrir le centre de livraison
          </button>
        </section>
      </div>
    </div>
  );
}

export function OrderJourney({
  order,
  assignment,
}: {
  order: any;
  assignment?: any;
}) {
  const path = [
    "REQUESTED",
    "CONFIRMED",
    "PREPARING",
    "READY",
    "OUT_FOR_DELIVERY",
    "DELIVERED",
  ];
  const status =
    order.status === "SUBMITTED"
      ? "REQUESTED"
      : order.status === "QUOTED"
        ? "REQUESTED"
        : ["PARTIALLY_DELIVERED", "DELIVERY_REFUSED"].includes(order.status)
          ? "DELIVERED"
          : order.status;
  const index = path.indexOf(status);
  return (
    <div className="order-journey" aria-label="Parcours de la commande">
      <ol>
        {path.map((step, i) => (
          <li
            key={step}
            className={index === i ? "current" : index > i ? "done" : ""}
            aria-current={index === i ? "step" : undefined}
          >
            <span aria-hidden="true">
              {index > i ? (
                <svg
                  viewBox="0 0 20 20"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                >
                  <path d="m4 10 4 4 8-8" />
                </svg>
              ) : (
                i + 1
              )}
            </span>
            <b>
              {step === "REQUESTED"
                ? "Reçue"
                : step === "CONFIRMED"
                  ? "Confirmée"
                  : step === "PREPARING"
                    ? "Préparation"
                    : step === "READY"
                      ? "Prête"
                      : step === "OUT_FOR_DELIVERY"
                        ? "Livraison"
                        : "Remise"}
            </b>
          </li>
        ))}
      </ol>
      <p>
        {["CANCELLED", "REJECTED", "DECLINED"].includes(order.status)
          ? labels[order.status]
          : order.status === "QUOTED"
            ? "L’offre attend l’accord explicite du magasin."
            : order.status === "DELIVERY_REFUSED"
              ? "Remise refusée : consultez le détail et le suivi des marchandises."
              : order.status === "PARTIALLY_DELIVERED"
                ? "Remise partielle : les quantités acceptées et refusées restent distinctes."
                : `Équipe en charge de la prochaine action : ${steps.find((s) => s.status === order.status)?.team || (order.status === "DELIVERED" ? "Finance si un solde reste dû" : "Commerce")}${assignment?.driverName ? ` · Chauffeur : ${assignment.driverName}` : ""}.`}
      </p>
    </div>
  );
}

export function AnalyticsView({
  revision,
  navigate,
  open,
}: {
  revision: number;
  navigate: Navigate;
  open: (id: string) => void;
}) {
  const [days, setDays] = useState(30),
    [range, setRange] = useState<any>(null),
    [data, setData] = useState<any>(null),
    [error, setError] = useState(""),
    [loading, setLoading] = useState(false),
    [metric, setMetric] = useState("revenue"),
    [comparison, setComparison] = useState(true),
    [breakdown, setBreakdown] = useState("products");
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    api(
      "/admin/analytics?" +
        (range ? `from=${range.from}&to=${range.to}` : "days=" + days),
    )
      .then((result) => {
        if (active) setData(result);
      })
      .catch((e) => {
        if (active) setError(e.message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [days, range, revision]);
  const basis =
    metric === "revenue"
      ? data?.basis.revenue
      : metric === "collected"
        ? data?.basis.collected
        : data?.basis.orders;
  return (
    <div className="analytics-workspace" aria-busy={loading}>
      <div className="analytics-controls">
        <div
          className="insight-tabs"
          role="group"
          aria-label="Période d’analyse"
        >
          {[7, 30, 90].map((value) => (
            <button
              key={value}
              aria-pressed={!range && days === value}
              className={!range && days === value ? "active" : ""}
              onClick={() => {
                setRange(null);
                setDays(value);
              }}
            >
              {value} jours
            </button>
          ))}
        </div>
        <form
          className="analytics-range"
          onSubmit={(e) => {
            e.preventDefault();
            const form = new FormData(e.currentTarget);
            setRange({ from: form.get("from"), to: form.get("to") });
          }}
        >
          <label>
            Du
            <input
              name="from"
              type="date"
              required
              max={new Date().toLocaleDateString("en-CA", {
                timeZone: "Africa/Algiers",
              })}
            />
          </label>
          <label>
            Au
            <input
              name="to"
              type="date"
              required
              max={new Date().toLocaleDateString("en-CA", {
                timeZone: "Africa/Algiers",
              })}
            />
          </label>
          <button className="button secondary" type="submit">
            Appliquer
          </button>
        </form>
      </div>
      {error && (
        <div className="error-banner" role="alert">
          {error}
        </div>
      )}
      {!data ? (
        <div className="boot-inline">
          <span className="spinner" />
          Calcul des indicateurs…
        </div>
      ) : (
        <>
          <div className="analytics-period">
            <span>
              {date(data.period.from)} — {date(data.period.to)} · heure d’Alger
            </span>
            <span>
              {loading
                ? "Actualisation…"
                : "Calculé à " +
                  new Date(data.generatedAt).toLocaleTimeString("fr-DZ", {
                    timeZone: "Africa/Algiers",
                    hour: "2-digit",
                    minute: "2-digit",
                    hourCycle: "h23",
                  })}
            </span>
          </div>
          <div className="analytics-metrics">
            {[
              ["revenue", "Ventes réceptionnées"],
              ["collected", "Encaissements nets"],
              ["orders", "Demandes reçues"],
              ["customers", "Commerces actifs"],
            ].map(([key, title]) => {
              const value = data.metrics[key];
              return (
                <button
                  key={key}
                  className={metric === key ? "active" : ""}
                  onClick={() =>
                    key === "customers" ? navigate("merchants") : setMetric(key)
                  }
                  aria-pressed={
                    key === "customers" ? undefined : metric === key
                  }
                >
                  <span>{title}</span>
                  <strong>
                    {["revenue", "collected"].includes(key)
                      ? money(value.value)
                      : number(value.value)}
                  </strong>
                  <small>
                    {value.changePercent == null
                      ? "Pas de base de comparaison"
                      : `${value.changePercent > 0 ? "+" : ""}${value.changePercent} % vs période précédente`}
                  </small>
                </button>
              );
            })}
          </div>
          <section className="panel analytical-chart-panel">
            <div className="panel-heading">
              <div>
                <h2>{metricTitles[metric]}</h2>
                <p>Comparer les mouvements sur des périodes de même durée.</p>
              </div>
              <label className="comparison-switch">
                <input
                  type="checkbox"
                  checked={comparison}
                  onChange={(e) => setComparison(e.target.checked)}
                />
                Période précédente
              </label>
            </div>
            <ActivityChart
              points={data.daily}
              previous={comparison ? data.previousDaily : []}
              field={metric}
            />
            <p className="metrics-note">{basis}</p>
          </section>
          <div className="analytics-secondary-grid">
            <section className="panel performance-panel">
              <h2>Ce que disent les opérations</h2>
              <p>
                Mesures issues des réceptions et des décisions enregistrées.
              </p>
              <dl>
                <div>
                  <dt>Unités acceptées à réception</dt>
                  <dd>
                    {number(data.performance.acceptedUnits)}
                    <small>
                      sur {number(data.performance.shippedUnits)} expédiées
                    </small>
                  </dd>
                </div>
                <div>
                  <dt>Taux d’acceptation des unités</dt>
                  <dd>
                    {data.performance.fillRate == null
                      ? "À mesurer"
                      : `${data.performance.fillRate} %`}
                  </dd>
                </div>
                <div>
                  <dt>Devis ayant été confirmés</dt>
                  <dd>
                    {data.performance.quoteConversion == null
                      ? "À mesurer"
                      : `${data.performance.quoteConversion} %`}
                    <small>
                      {data.performance.confirmedQuotes}/
                      {data.performance.quotedOrders} devis de la cohorte
                    </small>
                  </dd>
                </div>
                <div>
                  <dt>Panier moyen réceptionné</dt>
                  <dd>
                    {data.performance.averageDelivered == null
                      ? "À mesurer"
                      : money(data.performance.averageDelivered)}
                  </dd>
                </div>
              </dl>
              <p className="metrics-note">
                {data.basis.fillRate} {data.basis.quoteConversion}
              </p>
            </section>
            <section className="panel balances-panel">
              <div className="panel-heading">
                <div>
                  <h2>Les soldes à encaisser</h2>
                  <p>Situation actuelle · indépendante du filtre de dates</p>
                </div>
                <button
                  className="text-action"
                  onClick={() => navigate("finance")}
                >
                  Traiter les règlements
                </button>
              </div>
              <strong className="balance-total">
                {money(data.operations.remaining)}
              </strong>
              <div className="aging-columns">
                {data.aging.map((item: any) => (
                  <div key={item.label}>
                    <div>
                      <i
                        style={{
                          height: `${Math.max(2, (90 * item.amount) / Math.max(1, ...data.aging.map((a: any) => a.amount)))}px`,
                        }}
                      />
                    </div>
                    <strong>{money(item.amount)}</strong>
                    <span>{item.label}</span>
                  </div>
                ))}
              </div>
              <p className="metrics-note">{data.basis.aging}</p>
              <button
                className="reconciliation-action"
                onClick={() => navigate("finance")}
              >
                <span>
                  {data.operations.unreconciledCount} écritures à rapprocher
                </span>
                <strong>{money(data.operations.unreconciled)}</strong>
              </button>
            </section>
          </div>
          <section className="panel analytics-breakdown">
            <div className="panel-heading">
              <div>
                <h2>Ce qui porte votre activité</h2>
                <p>{data.basis.categories}</p>
              </div>
              <div
                className="insight-tabs"
                role="group"
                aria-label="Détail de l’activité"
              >
                {[
                  ["products", "Produits"],
                  ["categories", "Catégories"],
                  ["merchants", "Magasins"],
                ].map(([value, label]) => (
                  <button
                    key={value}
                    className={breakdown === value ? "active" : ""}
                    aria-pressed={breakdown === value}
                    onClick={() => setBreakdown(value)}
                  >
                    {label}
                  </button>
                ))}
              </div>
            </div>
            {breakdown === "products" ? (
              <div className="product-rankings">
                {data.topProducts.length ? (
                  data.topProducts.map((product: any, index: number) => (
                    <div key={product.productId}>
                      <span className="ranking-index">{index + 1}</span>
                      {product.imageUrl && (
                        <img src={product.imageUrl} alt="" loading="lazy" />
                      )}
                      <span>
                        <strong>{product.name}</strong>
                        <small>
                          {product.category} · {number(product.quantity)} unités
                          acceptées
                        </small>
                        <progress
                          max={Math.max(
                            1,
                            ...data.topProducts.map((p: any) => p.revenue),
                          )}
                          value={Math.max(0, product.revenue)}
                          aria-label={
                            product.name + " : " + money(product.revenue)
                          }
                        />
                      </span>
                      <b>{money(product.revenue)}</b>
                    </div>
                  ))
                ) : (
                  <div className="insight-empty">
                    Les produits apparaîtront après leur réception.
                  </div>
                )}
              </div>
            ) : breakdown === "categories" ? (
              <div className="category-rankings">
                {data.categories.length ? (
                  data.categories.map((category: any) => (
                    <div key={category.name}>
                      <span>
                        <strong>{category.name}</strong>
                        <small>
                          {number(category.quantity)} unités acceptées
                        </small>
                      </span>
                      <progress
                        max={Math.max(
                          1,
                          ...data.categories.map((c: any) => c.revenue),
                        )}
                        value={Math.max(0, category.revenue)}
                        aria-label={category.name}
                      />
                      <b>{money(category.revenue)}</b>
                    </div>
                  ))
                ) : (
                  <div className="insight-empty">
                    Aucune réception par catégorie sur cette période.
                  </div>
                )}
              </div>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Magasin</th>
                      <th>Demandes de la période</th>
                      <th>Ventes réceptionnées</th>
                      <th>Solde actuel</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.merchants.map((merchant: any) => (
                      <tr key={merchant.id}>
                        <td>
                          <strong>{merchant.name}</strong>
                          <small>{merchant.contact}</small>
                        </td>
                        <td>{merchant.orders}</td>
                        <td>{money(merchant.revenue)}</td>
                        <td>{money(merchant.remaining)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {!data.merchants.length && (
                  <div className="insight-empty">
                    Aucun magasin avec activité ou solde dans cette période.
                  </div>
                )}
              </div>
            )}
          </section>
          <WorkflowQueue orders={data.tasks} open={open} />
        </>
      )}
    </div>
  );
}
