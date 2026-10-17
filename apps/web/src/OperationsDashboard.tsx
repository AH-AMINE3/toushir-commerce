import { useEffect, useId, useRef, useState } from "react";
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
  cumulative = false,
  companionField,
  compact = false,
}: {
  points: any[];
  previous?: any[];
  field: string;
  title?: string;
  cumulative?: boolean;
  companionField?: string;
  compact?: boolean;
}) {
  const [selected, setSelected] = useState<number | null>(null);
  const chartElement = useRef<HTMLDivElement>(null);
  const [measuredWidth, setMeasuredWidth] = useState(800);
  const id = useId().replaceAll(":", "");
  useEffect(() => {
    if (!compact || !chartElement.current) return;
    const element = chartElement.current;
    const measure = () =>
      setMeasuredWidth(Math.max(180, element.getBoundingClientRect().width));
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [compact, points.length]);
  useEffect(() => {
    setSelected(null);
  }, [points.length, field, points[0]?.date]);
  if (!points.length)
    return (
      <div className="insight-empty">
        Les mouvements enregistrés apparaîtront ici.
      </div>
    );
  const series = (rows: any[], key: string) => {
    let running = 0;
    return rows.map((point) => {
      const value = Number(point[key] || 0);
      running += value;
      return cumulative ? running : value;
    });
  };
  const values = series(points, field),
    prior = series(previous, field),
    companion = companionField ? series(points, companionField) : [];
  const low = Math.min(0, ...values, ...prior, ...companion),
    high = Math.max(1, ...values, ...prior, ...companion),
    span = high - low;
  const plotWidth = compact ? measuredWidth : 800;
  const plotLeft = compact && plotWidth < 480 ? 40 : 64;
  const plotRight = plotWidth - (compact && plotWidth < 480 ? 8 : 24);
  const x = (index: number) =>
    plotLeft +
    index * ((plotRight - plotLeft) / Math.max(1, points.length - 1));
  const y = (value: number) =>
    (compact ? 12 : 30) + ((high - value) / span) * (compact ? 130 : 188);
  const coords = values.map((value, index) => `${x(index)},${y(value)}`);
  const index =
    selected == null
      ? points.length - 1
      : Math.min(selected, points.length - 1);
  const total = points.reduce(
    (sum, point) => sum + Number(point[field] || 0),
    0,
  );
  const format = (value: number) =>
    field === "orders" ? number(value) : money(value);
  const caption = title || metricTitles[field] || "Activité";
  return (
    <div
      ref={chartElement}
      className={
        "activity-chart" +
        (compact ? " activity-chart-compact activity-field-" + field : "")
      }
    >
      <div className="chart-reading" aria-live="polite" aria-atomic="true">
        <div>
          <span>
            {selected == null
              ? "Total sur la période"
              : date(points[index].date) + (cumulative ? " · cumul" : "")}
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
          {companionField && (
            <span>
              <i className="companion" />
              {metricTitles[companionField]}
            </span>
          )}
        </div>
      </div>
      <svg
        viewBox={compact ? `0 0 ${plotWidth} 180` : "0 0 800 255"}
        preserveAspectRatio={compact ? "none" : undefined}
        role="img"
        aria-label={
          caption +
          (companionField ? " et " + metricTitles[companionField] : "") +
          (cumulative ? " en cumul par jour." : " par jour.") +
          " Utilisez le curseur sous le graphique pour lire les valeurs."
        }
        onPointerMove={(event) => {
          const rect = event.currentTarget.getBoundingClientRect();
          const position =
            (((event.clientX - rect.left) / rect.width) * plotWidth -
              plotLeft) /
            (plotRight - plotLeft);
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
            <stop
              offset="0%"
              stopColor="var(--chart-primary, var(--green))"
              stopOpacity=".19"
            />
            <stop
              offset="100%"
              stopColor="var(--chart-primary, var(--green))"
              stopOpacity=".015"
            />
          </linearGradient>
        </defs>
        {[0, 1, 2, 3].map((tick) => {
          const value = high - (tick * span) / 3;
          return (
            <g key={tick}>
              <line
                x1={plotLeft}
                x2={plotRight}
                y1={y(value)}
                y2={y(value)}
                stroke="var(--line)"
              />
              <text
                x={plotLeft - 10}
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
        {companion.length > 0 && (
          <polyline
            points={companion
              .map((value, i) => `${x(i)},${y(value)}`)
              .join(" ")}
            fill="none"
            stroke="var(--info)"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
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
          stroke="var(--chart-primary, var(--green))"
          strokeWidth="3"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        {selected != null && (
          <g>
            <line
              x1={x(index)}
              x2={x(index)}
              y1={compact ? 10 : 24}
              y2={compact ? 145 : 220}
              stroke="var(--chart-primary, var(--green))"
              strokeDasharray="3 4"
            />
            <circle
              cx={x(index)}
              cy={y(values[index])}
              r="5"
              fill="var(--surface)"
              stroke="var(--chart-primary, var(--green))"
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
              y={compact ? 172 : 248}
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
          aria-valuetext={`${date(points[index].date)} : ${format(values[index])}${cumulative ? " en cumul" : ""}`}
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

/** Small charts share the server series; no decorative trend or inferred conversion. */
function MetricSpark({
  points,
  field,
  bars = false,
}: {
  points: any[];
  field: string;
  bars?: boolean;
}) {
  const values = points.map((point) => Number(point[field] || 0));
  if (!values.length) return null;
  let running = 0;
  const cumulative = values.map((value) => (running += value));
  const bins = Array.from({ length: 8 }, (_, i) =>
    values
      .slice(
        Math.floor((i * values.length) / 8),
        Math.floor(((i + 1) * values.length) / 8),
      )
      .reduce((sum, value) => sum + value, 0),
  );
  const series = bars ? bins : cumulative;
  const low = Math.min(0, ...series),
    high = Math.max(1, ...series),
    span = high - low;
  const y = (value: number) => 4 + ((high - value) / span) * 36;
  return (
    <svg
      className={"metric-spark metric-spark-" + field}
      viewBox="0 0 76 46"
      aria-hidden="true"
    >
      {bars ? (
        bins.map((value, i) => (
          <rect
            key={i}
            x={i * 9 + 3}
            y={Math.min(y(value), y(0))}
            width="5"
            height={Math.max(value === 0 ? 1 : 2, Math.abs(y(value) - y(0)))}
            rx="1.5"
            fill={value === 0 ? "var(--line)" : "currentColor"}
          />
        ))
      ) : (
        <>
          <polyline
            points={cumulative
              .map(
                (value, i) =>
                  `${3 + (i / Math.max(1, cumulative.length - 1)) * 70},${y(value)}`,
              )
              .join(" ")}
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinejoin="round"
          />
          <circle
            cx="73"
            cy={y(cumulative[cumulative.length - 1])}
            r="2.5"
            fill="currentColor"
          />
        </>
      )}
    </svg>
  );
}

export function Overview({
  dashboard,
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
    counts = dashboard.statusCounts || [],
    daily = dashboard.daily || [];
  const count = (...states: string[]) =>
    counts.reduce(
      (sum: number, item: any) =>
        sum + (states.includes(item.status) ? Number(item.count || 0) : 0),
      0,
    );
  const received = count("REQUESTED", "SUBMITTED"),
    quoted = count("QUOTED"),
    preparation = count("CONFIRMED", "PREPARING", "READY"),
    transit = count("OUT_FOR_DELIVERY"),
    delivered = count("DELIVERED", "PARTIALLY_DELIVERED");
  const total = counts.reduce(
    (sum: number, item: any) => sum + Number(item.count || 0),
    0,
  );
  const other = Math.max(
    0,
    total - received - quoted - preparation - transit - delivered,
  );
  const days = dashboard.period?.days || daily.length || 30;
  const distribution = [
    { label: "Reçues", count: received, tone: "received" },
    { label: "Accord attendu", count: quoted, tone: "pending" },
    { label: "Préparation", count: preparation, tone: "preparation" },
    { label: "En livraison", count: transit, tone: "info" },
    { label: "Réceptionnées", count: delivered, tone: "delivered" },
    { label: "Autres états", count: other, tone: "closed" },
  ];
  const priorities = [
    {
      count: received,
      label: "Commandes à confirmer",
      detail: "Demandes en attente de votre décision",
      route: "orders",
      status: "RECEIVED",
      tone: "received",
    },
    {
      count: quoted,
      label: "Réponses marchands",
      detail: "Versions proposées en attente d’accord",
      route: "orders",
      status: "QUOTED",
      tone: "pending",
    },
    {
      count: summary.pendingMerchants || 0,
      label: "Magasins à vérifier",
      detail: "Contrôler les informations du magasin",
      route: "merchants",
      status: "PENDING",
      tone: "info",
    },
    {
      count: summary.lowStock || 0,
      label: "Références sous le seuil",
      detail: "Vérifier le réassort",
      route: "stock",
      status: "",
      tone: "closed",
    },
  ];
  return (
    <div className="operations-overview overview-v2">
      <div className="overview-metrics">
        <button
          className="overview-metric"
          onClick={() => navigate("analytics")}
        >
          <span>Ventes réceptionnées</span>
          <div className="overview-metric-reading">
            <strong>
              {number(Number(summary.sales || 0))}
              <small>DA</small>
            </strong>
            <MetricSpark points={daily} field="sales" />
          </div>
          <div className="overview-metric-note">
            <b>{number(summary.deliveredOrders || 0)} commandes</b>
            <small>sur {days} jours</small>
          </div>
        </button>
        <button
          className="overview-metric"
          onClick={() => navigate("orders", "RECEIVED")}
        >
          <span>Demandes à confirmer</span>
          <div className="overview-metric-reading">
            <strong>{number(received)}</strong>
            <MetricSpark points={daily} field="orders" bars />
          </div>
          <div className="overview-metric-note">
            <b>{number(summary.newOrders || 0)} demandes</b>
            <small>sur {days} jours</small>
          </div>
        </button>
        <button
          className="overview-metric"
          onClick={() => navigate("analytics")}
        >
          <span>Réceptions enregistrées</span>
          <div className="overview-metric-reading">
            <strong>{number(summary.deliveredOrders || 0)}</strong>
            <span className="overview-receipt-icon">
              <img src="/figma/overview-v2/package.svg" alt="" />
            </span>
          </div>
          <div className="overview-metric-note">
            <b>Réceptions</b>
            <small>sur {days} jours</small>
          </div>
        </button>
        <button className="overview-metric" onClick={() => navigate("finance")}>
          <span>Encaissements nets</span>
          <div className="overview-metric-reading">
            <strong>
              {number(Number(summary.collected || 0))}
              <small>DA</small>
            </strong>
            <MetricSpark points={daily} field="collected" bars />
          </div>
          <div className="overview-metric-note">
            <b>Sur {days} jours</b>
            <small>moins remboursements</small>
          </div>
        </button>
      </div>
      <div className="overview-main-grid">
        <section className="panel overview-activity">
          <div className="panel-heading">
            <div>
              <h2>
                {metric === "orders"
                  ? "Demandes reçues"
                  : "Ventes et encaissements"}
              </h2>
              <p>
                {metric === "orders"
                  ? "Demandes cumulées"
                  : "Montants cumulés · DA"}{" "}
                · {days} derniers jours
              </p>
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
          <ActivityChart
            points={daily}
            field={metric}
            cumulative
            compact
            companionField={metric === "sales" ? "collected" : undefined}
          />
        </section>
        <section className="panel overview-priorities">
          <h2>À traiter maintenant</h2>
          <p>Une décision, un responsable, une suite.</p>
          <div className="overview-priority-list">
            {priorities.map((priority) => (
              <button
                key={priority.label}
                onClick={() => navigate(priority.route, priority.status)}
              >
                <strong className={"overview-count tone-" + priority.tone}>
                  {number(priority.count)}
                </strong>
                <span>
                  <b>{priority.label}</b>
                  <small>{priority.detail}</small>
                </span>
              </button>
            ))}
          </div>
        </section>
      </div>
      <div className="overview-bottom-grid">
        <section className="panel overview-distribution">
          <h2>{number(total)} dossiers, 6 groupes</h2>
          <p>État actuel · décision commerciale ≠ paiement</p>
          <div
            className="overview-distribution-bar"
            role="img"
            aria-label={distribution
              .map((item) => `${item.label} : ${item.count}`)
              .join(", ")}
          >
            {distribution
              .filter((item) => item.count > 0)
              .map((item) => (
                <span
                  key={item.label}
                  className={"tone-" + item.tone}
                  style={{ flexGrow: item.count }}
                />
              ))}
          </div>
          <div className="overview-state-list">
            {distribution.map((item) => (
              <div key={item.label}>
                <i className={"tone-" + item.tone} />
                <span>{item.label}</span>
                <b>{number(item.count)}</b>
              </div>
            ))}
          </div>
          <button
            className="overview-card-link"
            onClick={() => navigate("orders")}
          >
            Voir les commandes
          </button>
        </section>
        <section className="panel overview-events">
          <h2>Ce qui arrive de l’app</h2>
          <p>Même dossier. Même historique.</p>
          <div className="overview-event-list">
            {dashboard.recentOrders?.length ? (
              dashboard.recentOrders.slice(0, 3).map((order: any) => (
                <button key={order.id} onClick={() => open(order.id)}>
                  <time dateTime={order.createdAt}>
                    {date(order.createdAt)}
                  </time>
                  <span>
                    <b>
                      {order.number} · {labels[order.status] || order.status}
                    </b>
                    <small>
                      {order.shopName} ·{" "}
                      {order.total == null
                        ? "Prix à confirmer"
                        : money(order.total)}
                    </small>
                  </span>
                </button>
              ))
            ) : (
              <div className="insight-empty">
                La prochaine commande apparaîtra ici.
              </div>
            )}
          </div>
        </section>
        <section className="panel overview-delivery">
          <h2>Livraisons en cours</h2>
          <p>
            {number(transit)} commande{transit === 1 ? "" : "s"} sur la route
          </p>
          <div
            className="overview-dispatch"
            aria-label="Commandes par étape d’expédition"
          >
            {[
              {
                label: "À préparer",
                value: count("CONFIRMED", "PREPARING"),
                tone: "preparation",
              },
              { label: "Au départ", value: count("READY"), tone: "pending" },
              { label: "En route", value: transit, tone: "info" },
            ].map((item) => (
              <div key={item.label}>
                <i className={"tone-" + item.tone} />
                <strong>{number(item.value)}</strong>
                <span>{item.label}</span>
              </div>
            ))}
          </div>
          <small>Consulter les affectations et le suivi de remise.</small>
          <button
            className="button secondary"
            onClick={() => navigate("delivery")}
          >
            Ouvrir le centre de livraison
          </button>
        </section>
      </div>
      <p className="overview-footnote">
        Demandes, réceptions et encaissements nets restent distincts.{" "}
        {dashboard.basis?.[metric] ||
          "Les demandes sont comptées à leur date de création."}
      </p>
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
