import React, { useCallback, useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import "@fontsource/dm-sans/400.css";
import "@fontsource/dm-sans/500.css";
import "@fontsource/dm-sans/600.css";
import "@fontsource/dm-sans/700.css";
import {
  api,
  post,
  patch,
  setToken,
  connect,
  money,
  number,
  date,
  labels,
  key,
  openDocument,
} from "./api";
import type { Session } from "./api";
const Warehouse = React.lazy(() => import("./Warehouse"));
const LiveMap = React.lazy(() => import("./LiveMap"));
import "./style.css";
import RouteConsole from "./Routes";
import {
  PickWaves,
  LotInventory,
  ReturnLotIdentification,
} from "./WarehouseOperations";
import { CampaignSelection, OrderPromotions } from "./Promotions";
import { Messages, Supply } from "./Business";
import { Campaigns, Rewards } from "./Marketing";
import { Pricing, QuotePricingControls, FrozenPricing } from "./Pricing";
import { Crm } from "./Crm";
import {
  startIdentityLogin,
  finishIdentityLogin,
  restoreIdentityToken,
  clearIdentity,
  identity,
} from "./oidc";

const icons: Record<string, string> = {
  overview: "8698b.svg",
  orders: "f449e.svg",
  picking: "4656b.svg",
  delivery: "88da3.svg",
  returns: "3ebf8.svg",
  products: "2b41c.svg",
  pricing: "2b41c.svg",
  stock: "9e889.svg",
  purchases: "c9f30.svg",
  merchants: "f1064.svg",
  crm: "f1064.svg",
  messages: "64c5d.svg",
  campaigns: "aa2a3.svg",
  rewards: "69b9b.svg",
  finance: "2cd73.svg",
  analytics: "de3a6.svg",
  settings: "4d5c5.svg",
  search: "e032b.svg",
  bell: "e15a2.svg",
  warehouse: "cc1ad.svg",
};
const groups = [
  {
    label: "Opérations",
    items: [
      ["overview", "Vue d’ensemble"],
      ["orders", "Commandes"],
      ["picking", "Préparation"],
      ["delivery", "Livraisons"],
      ["returns", "Retours & litiges"],
    ],
  },
  {
    label: "Assortiment",
    items: [
      ["products", "Catalogue"],
      ["pricing", "Tarifs & conditions"],
      ["stock", "Stock & qualité"],
      ["purchases", "Achats fournisseurs"],
    ],
  },
  {
    label: "Relation client",
    items: [
      ["merchants", "Magasins"],
      ["crm", "Portefeuilles & visites"],
      ["messages", "Messages"],
      ["campaigns", "Campagnes"],
      ["rewards", "Points & cadeaux"],
    ],
  },
  {
    label: "Pilotage",
    items: [
      ["finance", "Finance"],
      ["analytics", "Analyses"],
      ["settings", "Équipe & paramètres"],
    ],
  },
];
const operational = ["CONFIRMED", "PREPARING", "READY", "OUT_FOR_DELIVERY"];
const staffRoutes: Record<string, string[]> = {
  WAREHOUSE: [
    "picking",
    "stock",
    "delivery",
    "purchases",
    "rewards",
    "messages",
  ],
  DISPATCHER: ["orders", "delivery", "messages"],
  FINANCE: ["finance", "purchases", "pricing", "campaigns", "rewards"],
  SALES: ["crm"],
};
function ControlIcon({ kind }: { kind: "close" | "check" }) {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      style={{ verticalAlign: "middle" }}
    >
      {kind === "close" ? (
        <path d="m6 6 12 12M18 6 6 18" />
      ) : (
        <path d="m5 12 4 4L19 6" />
      )}
    </svg>
  );
}
function Icon({ name }: { name: string }) {
  return (
    <img
      className="icon"
      src={"/figma/0/" + (icons[name] || icons.orders)}
      alt=""
    />
  );
}
function Badge({ value }: { value: string }) {
  return (
    <span
      className={
        "badge " +
        ([
          "DELIVERED",
          "COLLECTED",
          "RECONCILED",
          "APPROVED",
          "CONFIRMED",
        ].includes(value)
          ? "green"
          : ["QUOTED", "PENDING", "PARTIAL"].includes(value)
            ? "amber"
            : [
                  "CANCELLED",
                  "REJECTED",
                  "DELIVERY_REFUSED",
                  "SUSPENDED",
                  "REFUND_DUE",
                ].includes(value)
              ? "red"
              : ["REQUESTED", "SUBMITTED"].includes(value)
                ? "aqua"
                : "neutral")
      }
    >
      {labels[value] || value}
    </span>
  );
}
function Button({
  children,
  onClick,
  secondary = false,
  disabled = false,
  type = "button",
}: {
  children: React.ReactNode;
  onClick?: () => void;
  secondary?: boolean;
  disabled?: boolean;
  type?: "button" | "submit";
}) {
  return (
    <button
      className={secondary ? "button secondary" : "button"}
      onClick={onClick}
      disabled={disabled}
      type={type}
    >
      {children}
    </button>
  );
}
function Field({
  label,
  value,
  onChange,
  type = "text",
  min,
  max,
  required = false,
}: {
  label: string;
  value: any;
  onChange: (v: string) => void;
  type?: string;
  min?: number;
  max?: number;
  required?: boolean;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <input
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value)}
        type={type}
        min={min}
        max={max}
        required={required}
        step={type === "number" ? "any" : undefined}
      />
    </label>
  );
}
function Empty({ children }: { children: React.ReactNode }) {
  return (
    <div className="empty">
      <Icon name="orders" />
      <p>{children}</p>
    </div>
  );
}
function Chart({ points, field = "sales" }: { points: any[]; field?: string }) {
  const vals = points.map((p) => Number(p[field] || 0));
  const max = Math.max(...vals, 1);
  const xy = vals.map(
    (v, i) =>
      `${30 + i * (720 / Math.max(vals.length - 1, 1))},${190 - (v / max) * 155}`,
  );
  return (
    <div className="chart">
      <svg
        viewBox="0 0 780 225"
        role="img"
        aria-label="Évolution des ventes calculée sur les commandes réceptionnées"
      >
        {[0, 1, 2, 3].map((i) => (
          <g key={i}>
            <line
              x1="30"
              x2="750"
              y1={35 + i * 52}
              y2={35 + i * 52}
              stroke="var(--line)"
            />
            <text x="4" y={39 + i * 52} fontSize="10" fill="var(--muted)">
              {number(Math.round(max * (1 - i / 3)))}
            </text>
          </g>
        ))}
        <path
          d={"M" + xy.join(" L") + " L750,190 L30,190 Z"}
          fill="var(--mint)"
          opacity=".55"
        />
        <polyline
          points={xy.join(" ")}
          fill="none"
          stroke="var(--green)"
          strokeWidth="3"
          strokeLinejoin="round"
        />
        {points
          .filter(
            (_, i) =>
              i === 0 ||
              i === points.length - 1 ||
              i === Math.floor(points.length / 2),
          )
          .map((p, i, arr) => (
            <text
              key={p.date}
              x={30 + (i * 720) / Math.max(arr.length - 1, 1)}
              y="218"
              textAnchor={
                i === 0 ? "start" : i === arr.length - 1 ? "end" : "middle"
              }
              fontSize="11"
              fill="var(--muted)"
            >
              {date(p.date)}
            </text>
          ))}
      </svg>
    </div>
  );
}
function Stat({
  label,
  value,
  detail,
  onClick,
}: {
  label: string;
  value: React.ReactNode;
  detail?: string;
  onClick?: () => void;
}) {
  return (
    <button className="stat" onClick={onClick} disabled={!onClick}>
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </button>
  );
}
function App() {
  const [session, setSession] = useState<Session | null>(null),
    [boot, setBoot] = useState(true),
    [route, setRoute] = useState(location.hash.slice(1) || "overview"),
    [error, setError] = useState(""),
    [toast, setToast] = useState(""),
    [busy, setBusy] = useState(false),
    [live, setLive] = useState("Connexion"),
    [dark, setDark] = useState(
      localStorage.getItem("toushir-theme") === "dark",
    ),
    [menu, setMenu] = useState(false);
  const [dashboard, setDashboard] = useState<any>(null),
    [orders, setOrders] = useState<any[]>([]),
    [team, setTeam] = useState<any[]>([]),
    [revision, setRevision] = useState(0),
    [query, setQuery] = useState(""),
    [filter, setFilter] = useState(""),
    [selected, setSelected] = useState<string | null>(null),
    [product, setProduct] = useState<any>(null),
    [login, setLogin] = useState({ email: "", password: "" });
  const navigate = (next: string, status = "") => {
    const target = session?.user.role === "SALES" ? "crm" : next;
    setRoute(target);
    setFilter(status);
    setQuery("");
    location.hash = target;
    setMenu(false);
  };
  const refresh = useCallback(async () => {
    if (!session) return;
    try {
      if (session.user.role === "SALES") {
        await api("/crm");
        setDashboard({ summary: {}, daily: [], recentOrders: [] });
        setOrders([]);
        setTeam([]);
        setRevision((x) => x + 1);
        return;
      }
      const admin = session.user.role === "ADMIN";
      const [d, o, t] = await Promise.all([
        api(admin ? "/admin/dashboard?days=30" : "/operations/dashboard"),
        api(admin ? "/admin/orders?pageSize=100" : "/operations/orders"),
        session.user.role === "FINANCE"
          ? Promise.resolve({ items: [] })
          : api("/operations/team"),
      ]);
      const items = admin
        ? o.items
        : o.items.map((x: any) => ({
            ...x,
            itemCount: x.items.length,
            quantity: x.items.reduce((a: number, l: any) => a + l.quantity, 0),
          }));
      setDashboard(
        admin
          ? d
          : {
              summary: {
                requests: d.awaitingQuote,
                quotes: items.filter((x: any) => x.status === "QUOTED").length,
                activeOrders: d.preparing + d.ready + d.enRoute,
                deliveredOrders: d.delivered,
              },
              daily: [],
              statusCounts: [],
              recentOrders: [],
            },
      );
      setOrders(items);
      setTeam(Array.isArray(t) ? t : t.items || t.members || []);
      setRevision((x) => x + 1);
    } catch (e) {
      setError((e as Error).message);
      if ((e as any).status === 401 && session.user.role === "SALES") {
        setToken("");
        setSession(null);
      }
    }
  }, [session?.user.id]);
  const accept = (s: Session) => {
    if (s.token) setToken(s.token);
    if (s.workspaceToken)
      localStorage.setItem("toushir-demo-workspace", s.workspaceToken);
    setSession(s);
    if (!s.workspace.canSwitchRole)
      sessionStorage.setItem("toushir-web-personal-account", "true");
    const permitted = staffRoutes[s.user.role];
    if (permitted && !permitted.includes(route)) navigate(permitted[0]);
  };
  async function demo() {
    setBusy(true);
    setError("");
    try {
      const workspaceToken = localStorage.getItem("toushir-demo-workspace");
      const s = await post<Session>("/demo/session", {
        role: "ADMIN",
        ...(workspaceToken ? { workspaceToken } : {}),
      });
      accept(s);
      await post("/admin/demo/scenario");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
      setBoot(false);
    }
  }
  useEffect(() => {
    (async () => {
      try {
        if (location.pathname === "/auth/callback") {
          sessionStorage.setItem("toushir-web-personal-account", "true");
          setToken(await finishIdentityLogin());
        } else {
          const access = await restoreIdentityToken();
          if (access) setToken(access);
        }
        const s = await api<Session>("/auth/me");
        if (["MERCHANT", "DRIVER"].includes(s.user.role)) {
          location.assign(
            "/mobile/" + (s.user.role === "DRIVER" ? "driver" : "merchant"),
          );
          return;
        } else accept(s);
      } catch {
        if (sessionStorage.getItem("toushir-web-personal-account") === "true") {
          setToken("");
          setError(
            "Votre session doit être renouvelée. Reconnectez-vous à votre compte professionnel.",
          );
          return;
        }
        try {
          const h = await api("/health");
          if (h.demoEnabled) await demo();
        } catch (e) {
          setError((e as Error).message);
        }
      } finally {
        setBoot(false);
      }
    })();
  }, []);
  useEffect(() => {
    if (!session) return;
    void refresh();
    const stop = connect(() => void refresh(), setLive);
    const timer = setInterval(() => void refresh(), 15000);
    return () => {
      stop();
      clearInterval(timer);
    };
  }, [session?.user.id, refresh]);
  useEffect(() => {
    const update = (user: any) => setToken(user.access_token);
    identity().events.addUserLoaded(update);
    return () => identity().events.removeUserLoaded(update);
  }, []);
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        document
          .querySelector<HTMLInputElement>(".global-search input")
          ?.focus();
      }
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, []);
  useEffect(() => {
    document.documentElement.dataset.theme = dark ? "dark" : "light";
    localStorage.setItem("toushir-theme", dark ? "dark" : "light");
  }, [dark]);
  useEffect(() => {
    if (toast) {
      const timer = setTimeout(() => setToast(""), 5000);
      return () => clearTimeout(timer);
    }
  }, [toast]);
  const mutationPending = useRef(false);
  const run = async (
    fn: () => Promise<unknown>,
    message = "Enregistré. Les autres interfaces sont actualisées.",
  ) => {
    if (mutationPending.current) return;
    mutationPending.current = true;
    setBusy(true);
    setError("");
    try {
      await fn();
      setToast(message);
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      mutationPending.current = false;
      setBusy(false);
    }
  };
  const summary = dashboard?.summary || {};
  const counts = dashboard?.statusCounts || [];
  if (boot)
    return (
      <div className="boot">
        <img src="/figma/0/54083.svg" alt="TOUSHIR" />
        <span className="spinner" />
        <p>Ouverture de votre espace…</p>
      </div>
    );
  if (!session)
    return (
      <main className="login">
        <img src="/figma/0/54083.svg" alt="TOUSHIR" />
        <h1>
          Votre commerce,
          <br />
          en mouvement.
        </h1>
        <p>Connectez-vous à votre espace grossiste.</p>
        <div className="action-form">
          <Button
            onClick={() => {
              sessionStorage.setItem("toushir-web-personal-account", "true");
              void startIdentityLogin();
            }}
          >
            Se connecter avec mon compte professionnel
          </Button>
        </div>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <Button secondary onClick={() => void demo()} disabled={busy}>
          Explorer un espace de démonstration
        </Button>
        <a href="/mobile/merchant">Ouvrir l’application commerçant</a>
      </main>
    );
  return (
    <div className="shell">
      <aside className={"sidebar " + (menu ? "open" : "")}>
        <a
          className="brand"
          href={session.user.role === "SALES" ? "#crm" : "#overview"}
          onClick={() => navigate("overview")}
        >
          <img src="/figma/0/54083.svg" alt="TOUSHIR" />
        </a>
        <small className="space-label">Espace grossiste</small>
        <div className="depot-switch">
          <Icon name="warehouse" />
          <div>
            <strong>{session.workspace.name.replace("· EXEMPLE", "")}</strong>
            <small>
              {session.workspace.isDemo
                ? "Espace de démonstration"
                : "Dépôt principal"}
            </small>
          </div>
        </div>
        <nav id="workspace-navigation" aria-label="Navigation principale">
          {groups
            .filter(
              (g) =>
                !staffRoutes[session.user.role] ||
                g.items.some(([id]) =>
                  staffRoutes[session.user.role].includes(id),
                ),
            )
            .map((g) => (
              <div className="nav-group" key={g.label}>
                <span>{g.label}</span>
                {g.items
                  .filter(
                    ([id]) =>
                      !staffRoutes[session.user.role] ||
                      staffRoutes[session.user.role].includes(id),
                  )
                  .map(([id, label]) => (
                    <button
                      key={id}
                      aria-label={label}
                      className={route === id ? "active" : ""}
                      onClick={() => navigate(id)}
                    >
                      <Icon name={id} />
                      {label}
                      {id === "orders" && summary.requests > 0 && (
                        <b>{summary.requests}</b>
                      )}
                    </button>
                  ))}
              </div>
            ))}
        </nav>
        <footer>
          <span className="avatar">TS</span>
          <div>
            <strong>{session.user.name}</strong>
            <small>
              {
                (
                  {
                    ADMIN: "Administration",
                    WAREHOUSE: "Préparation",
                    DISPATCHER: "Dispatch",
                    FINANCE: "Finance",
                    SALES: "Conseil commercial",
                  } as any
                )[session.user.role]
              }
              {session.workspace.isDemo ? " · démo" : ""}
            </small>
          </div>
          <button
            className="icon-button"
            title="Se déconnecter"
            disabled={busy}
            onClick={() =>
              void run(async () => {
                await post("/auth/logout");
                await clearIdentity();
                setToken("");
                setSession(null);
              }, "Déconnecté")
            }
          >
            <svg
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="M9 4H5a1 1 0 0 0-1 1v14a1 1 0 0 0 1 1h4M14 8l4 4-4 4M9 12h9" />
            </svg>
          </button>
        </footer>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <button
            className="mobile-menu icon-button"
            aria-label={menu ? "Fermer le menu" : "Ouvrir le menu"}
            aria-expanded={menu}
            aria-controls="workspace-navigation"
            onClick={() => setMenu(!menu)}
          >
            <svg
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
              aria-hidden="true"
            >
              <path
                d={menu ? "m6 6 12 12M6 18 18 6" : "M4 6h16M4 12h16M4 18h16"}
              />
            </svg>
          </button>
          <div className="breadcrumb">
            Opérations <span>/</span> TOUSHIR
          </div>
          <div className="global-search">
            <Icon name="search" />
            <input
              aria-label="Rechercher une commande, un produit ou un magasin"
              placeholder="Rechercher une commande, un magasin, un produit…"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                if (session.user.role === "SALES") {
                  setRoute("crm");
                  location.hash = "crm";
                } else if (
                  !["orders", "products", "merchants", "crm"].includes(route)
                ) {
                  setRoute("orders");
                  setFilter("");
                  location.hash = "orders";
                }
              }}
            />
            <kbd>⌘ K</kbd>
          </div>
          <span
            className={
              "connection " + (live === "En direct" ? "connected" : "")
            }
          >
            <i />
            {live}
          </span>
          <button
            className="icon-button"
            title="Actualiser les données"
            onClick={() => void refresh()}
          >
            <Icon name="bell" />
          </button>
          <button
            className="theme-switch"
            onClick={() => setDark(!dark)}
            aria-label={
              dark ? "Activer le thème clair" : "Activer le thème sombre"
            }
          >
            {dark ? "Clair" : "Sombre"}
          </button>
          <span className="avatar">TS</span>
        </header>
        <main className="content">
          {session.workspace.isDemo && (
            <div className="demo-strip">
              <span>
                Démonstration isolée · prix, stocks et opérations fictifs
              </span>
              <div>
                <a target="_blank" href="/mobile/merchant" rel="noreferrer">
                  App commerçant ↗
                </a>
                <a target="_blank" href="/mobile/driver" rel="noreferrer">
                  App livreur ↗
                </a>
              </div>
            </div>
          )}
          <div className="page-heading">
            <div>
              <h1>
                {(
                  {
                    overview: "Votre activité, en clair.",
                    orders: "Commandes",
                    picking: "Chaque carton, sous contrôle.",
                    delivery: "La livraison, sous contrôle.",
                    products: "Un catalogue clair, jusqu’au carton.",
                    stock: "Le bon stock. Au bon endroit.",
                    merchants: "Vos magasins partenaires.",
                    crm: "Le suivi commercial qui fait avancer.",
                    finance: "Les règlements, sans ambiguïté.",
                    returns: "Retours & litiges",
                    analytics: "Comprendre pour mieux décider.",
                    settings: "Votre équipe, votre espace.",
                    purchases: "Approvisionner sans surstocker.",
                    pricing: "Un seul prix de référence, partout.",
                    messages: "La relation client, sans perdre le fil.",
                    campaigns: "Des offres utiles. Des règles solides.",
                    rewards: "Les points se justifient, ligne par ligne.",
                  } as Record<string, string>
                )[route] || "TOUSHIR"}
              </h1>
              <p>
                {route === "overview"
                  ? "Demandes, préparation, livraison et encaissement · Les mêmes données sur les trois interfaces."
                  : route === "crm"
                    ? "Portefeuille autorisé · rappels, visites et demandes assistées restent des objets distincts."
                    : route === "delivery"
                      ? "Planifier, suivre et traiter les exceptions · Vue 3D illustrative · GPS autorisé séparé."
                      : route === "orders"
                        ? "Ouvrez un dossier pour voir son historique et effectuer la prochaine action."
                        : route === "products"
                          ? "Identités produits sourcées · Prix, conditionnement et stock confirmés par le grossiste."
                          : "Les changements sont validés et enregistrés sur le serveur."}
              </p>
            </div>
            <div className="heading-actions">
              {route === "overview" ? (
                <>
                  <span className="today">
                    {new Intl.DateTimeFormat("fr-DZ", {
                      dateStyle: "medium",
                      timeZone: "Africa/Algiers",
                    }).format(new Date())}
                  </span>
                  <Button onClick={() => navigate("orders", "REQUESTED")}>
                    Voir les priorités
                  </Button>
                </>
              ) : (
                <Button
                  secondary
                  onClick={() => void refresh()}
                  disabled={busy}
                >
                  Actualiser
                </Button>
              )}
            </div>
          </div>
          {error && (
            <div className="error-banner" role="alert">
              <span>{error}</span>
              <button onClick={() => setError("")} aria-label="Fermer l’erreur">
                <ControlIcon kind="close" />
              </button>
            </div>
          )}
          <fieldset
            className="workspace-actions"
            disabled={busy}
            aria-busy={busy}
            aria-label="Actions de l’espace"
          >
            {!dashboard ? (
              <div className="boot-inline">
                <span className="spinner" />
                Chargement des opérations…
              </div>
            ) : (
              <>
                {route === "overview" && (
                  <>
                    <div className="stats">
                      <Stat
                        label="Ventes réceptionnées · 30 jours"
                        value={money(summary.sales)}
                        detail={`${summary.deliveredOrders} réceptions enregistrées`}
                        onClick={() => navigate("analytics")}
                      />
                      <Stat
                        label="Demandes à traiter"
                        value={number(summary.requests)}
                        detail={`${summary.quotes} offres attendent un accord`}
                        onClick={() => navigate("orders", "REQUESTED")}
                      />
                      <Stat
                        label="Commandes en cours"
                        value={number(summary.activeOrders)}
                        detail="Confirmées, préparées ou en livraison"
                        onClick={() => navigate("picking")}
                      />
                      <Stat
                        label="Encaissements · 30 jours"
                        value={money(summary.collected)}
                        detail="Paiements enregistrés moins remboursements"
                        onClick={() => navigate("finance")}
                      />
                    </div>
                    <div className="overview-grid">
                      <section className="panel sales-panel">
                        <div className="panel-heading">
                          <div>
                            <h2>Les ventes au fil des jours</h2>
                            <p>Montants des commandes réceptionnées · DA</p>
                          </div>
                          <span className="badge neutral">
                            30 derniers jours
                          </span>
                        </div>
                        <Chart points={dashboard.daily} />
                        <div className="chart-legend">
                          <i />
                          Marchandises acceptées et frais de livraison
                          contractuels
                        </div>
                      </section>
                      <section className="panel priorities">
                        <h2>À traiter maintenant</h2>
                        <p>Une décision, un responsable, une suite.</p>
                        {[
                          [
                            summary.requests,
                            "Commandes à chiffrer",
                            "Vérifier les lignes et proposer un devis",
                            "orders",
                            "REQUESTED",
                          ],
                          [
                            summary.quotes,
                            "Réponses marchands",
                            "La version proposée attend leur accord",
                            "orders",
                            "QUOTED",
                          ],
                          [
                            summary.pendingMerchants,
                            "Magasins à vérifier",
                            "Autoriser leur première demande",
                            "merchants",
                            "PENDING",
                          ],
                          [
                            summary.lowStock,
                            "Références sous le seuil",
                            "Contrôler les disponibilités publiées",
                            "stock",
                            "",
                          ],
                        ].map(([n, title, sub, r, f], i) => (
                          <button
                            className="priority"
                            key={String(title)}
                            onClick={() => navigate(String(r), String(f))}
                          >
                            <b
                              className={["mint", "yellow", "cyan", "pink"][i]}
                            >
                              {n}
                            </b>
                            <span>
                              <strong>{title}</strong>
                              <small>{sub}</small>
                            </span>
                            <svg
                              width="20"
                              height="20"
                              viewBox="0 0 24 24"
                              fill="none"
                              stroke="currentColor"
                              strokeWidth="1.75"
                              strokeLinecap="round"
                              strokeLinejoin="round"
                              aria-hidden="true"
                            >
                              <path d="m9 6 6 6-6 6" />
                            </svg>
                          </button>
                        ))}
                      </section>
                      <section className="panel">
                        <h2>{orders.length} dossiers, des états distincts</h2>
                        <p>Décision commerciale, remise et paiement.</p>
                        <div className="stacked-bar">
                          {counts.map((s: any, i: number) => (
                            <span
                              key={s.status}
                              style={{
                                flex: s.count,
                                background: [
                                  "#087f4f",
                                  "#d9eddf",
                                  "#0f2c2c",
                                  "#06dd81",
                                  "#bf9352",
                                  "#a9c6b7",
                                ][i % 6],
                              }}
                            />
                          ))}
                        </div>
                        <div className="status-legend">
                          {counts.map((s: any) => (
                            <button
                              key={s.status}
                              onClick={() => navigate("orders", s.status)}
                            >
                              <span>{labels[s.status] || s.status}</span>
                              <strong>{s.count}</strong>
                            </button>
                          ))}
                        </div>
                      </section>
                      <section className="panel">
                        <h2>Ce qui arrive de l’app</h2>
                        <p>Même dossier. Même historique.</p>
                        <div className="event-list">
                          {dashboard.recentOrders.slice(0, 4).map((o: any) => (
                            <button
                              key={o.id}
                              onClick={() => setSelected(o.id)}
                            >
                              <time>
                                {new Date(o.createdAt).toLocaleTimeString(
                                  "fr-DZ",
                                  {
                                    hour: "2-digit",
                                    minute: "2-digit",
                                    hourCycle: "h23",
                                  },
                                )}
                              </time>
                              <span>
                                <strong>{o.number}</strong>
                                <small>
                                  {o.shopName} · {money(o.total)}
                                </small>
                              </span>
                            </button>
                          ))}
                        </div>
                      </section>
                      <section className="panel route-preview">
                        <h2>Du dépôt au commerce</h2>
                        <p>
                          {
                            orders.filter(
                              (o) => o.status === "OUT_FOR_DELIVERY",
                            ).length
                          }{" "}
                          commandes en cours de livraison
                        </p>
                        <div className="route-line">
                          <i />
                          <span />
                          <i />
                          <span />
                          <i />
                          <span />
                          <i />
                        </div>
                        <p>
                          Préparation, garde, position et réception restent
                          traçables.
                        </p>
                        <Button secondary onClick={() => navigate("delivery")}>
                          Ouvrir le centre de livraison
                        </Button>
                      </section>
                    </div>
                  </>
                )}
                {["orders", "picking", "finance"].includes(route) && (
                  <>
                    <div className="stats compact">
                      {(route === "finance"
                        ? [
                            "UNPAID",
                            "PARTIAL",
                            "COLLECTED",
                            "RECONCILED",
                            "REFUND_DUE",
                          ]
                        : [
                            "REQUESTED",
                            "QUOTED",
                            "CONFIRMED",
                            "PREPARING",
                            "READY",
                            "OUT_FOR_DELIVERY",
                          ]
                      ).map((s) => (
                        <Stat
                          key={s}
                          label={labels[s]}
                          value={
                            orders.filter(
                              (o) =>
                                (route === "finance"
                                  ? o.paymentStatus
                                  : o.status) === s,
                            ).length
                          }
                          onClick={() => setFilter(filter === s ? "" : s)}
                        />
                      ))}
                    </div>
                    <section className="panel">
                      <div className="panel-heading">
                        <div>
                          <h2>
                            {route === "picking"
                              ? "File de préparation"
                              : route === "finance"
                                ? "Encaissements et rapprochement"
                                : "Toutes les commandes"}
                          </h2>
                          <p>
                            {route === "finance"
                              ? "Une remise de marchandises ne crée jamais automatiquement un paiement."
                              : "Cliquez sur un dossier pour traiter sa prochaine action."}
                          </p>
                        </div>
                        <select
                          aria-label="Filtrer par état"
                          value={filter}
                          onChange={(e) => setFilter(e.target.value)}
                        >
                          <option value="">Tous les états</option>
                          {Object.entries(labels)
                            .filter(([s]) =>
                              route === "finance"
                                ? [
                                    "UNPAID",
                                    "PARTIAL",
                                    "COLLECTED",
                                    "RECONCILED",
                                    "REFUND_DUE",
                                  ].includes(s)
                                : ![
                                    "APPROVED",
                                    "PENDING",
                                    "OPEN",
                                    "ACCEPTED",
                                  ].includes(s),
                            )
                            .map(([s, l]) => (
                              <option key={s} value={s}>
                                {l}
                              </option>
                            ))}
                        </select>
                      </div>
                      <OrdersTable
                        orders={orders.filter(
                          (o) =>
                            (!query ||
                              (o.number + " " + o.merchant.shopName)
                                .toLowerCase()
                                .includes(query.toLowerCase())) &&
                            (!filter ||
                              (route === "finance"
                                ? o.paymentStatus
                                : o.status) === filter) &&
                            (route !== "picking" ||
                              ["CONFIRMED", "PREPARING", "READY"].includes(
                                o.status,
                              )),
                        )}
                        open={setSelected}
                        finance={route === "finance"}
                      />
                    </section>
                  </>
                )}
                {route === "delivery" && (
                  <Delivery
                    orders={orders}
                    selected={selected}
                    open={setSelected}
                  />
                )}
                {["delivery", "finance"].includes(route) && (
                  <RouteConsole
                    orders={orders}
                    team={team}
                    revision={revision}
                    run={run}
                    role={session.user.role}
                    cashOnly={route === "finance"}
                    open={setSelected}
                  />
                )}
                {session.user.role === "ADMIN" &&
                  ["products", "stock"].includes(route) && (
                    <Products
                      key={route}
                      query={query}
                      revision={revision}
                      stock={route === "stock"}
                      edit={setProduct}
                    />
                  )}
                {route === "picking" && (
                  <PickWaves
                    orders={orders}
                    revision={revision}
                    run={run}
                    role={session.user.role}
                  />
                )}
                {route === "stock" && (
                  <LotInventory revision={revision} run={run} />
                )}
                {route === "merchants" && (
                  <Merchants
                    query={query}
                    revision={revision}
                    run={run}
                    busy={busy}
                  />
                )}
                {route === "crm" && (
                  <Crm
                    revision={revision}
                    session={session}
                    query={query}
                    onOrder={(id) => {
                      navigate("orders");
                      setSelected(id);
                    }}
                  />
                )}
                {route === "analytics" && <Analytics revision={revision} />}
                {route === "messages" && (
                  <Messages
                    revision={revision}
                    run={run}
                    team={team}
                    session={session}
                    orders={orders}
                  />
                )}
                {route === "purchases" && (
                  <Supply revision={revision} run={run} session={session} />
                )}
                {route === "pricing" && (
                  <Pricing
                    revision={revision}
                    run={run}
                    session={session}
                    campaigns={() => navigate("campaigns")}
                  />
                )}
                {route === "campaigns" && (
                  <Campaigns
                    revision={revision}
                    run={run}
                    session={session}
                    orders={orders}
                  />
                )}
                {route === "rewards" && (
                  <Rewards
                    revision={revision}
                    run={run}
                    session={session}
                    orders={orders}
                  />
                )}
                {route === "returns" && (
                  <Returns revision={revision} open={setSelected} run={run} />
                )}
                {route === "settings" && (
                  <Settings
                    session={session}
                    team={team}
                    revision={revision}
                    run={run}
                  />
                )}
              </>
            )}
          </fieldset>
          <footer className="page-foot">
            TOUSHIR ·{" "}
            {session.workspace.isDemo
              ? "Environnement de test isolé"
              : "Espace professionnel"}{" "}
            · Données calculées par le serveur · Heure d’Alger
          </footer>
        </main>
      </div>
      {selected && (
        <OrderPanel
          id={selected}
          close={() => setSelected(null)}
          revision={revision}
          run={run}
          busy={busy}
          team={team}
          role={session.user.role}
        />
      )}
      {product && (
        <ProductPanel
          product={product}
          close={() => setProduct(null)}
          run={run}
          busy={busy}
        />
      )}
      {error && (selected || product) && (
        <div className="toast error-toast" role="alert">
          {error}
          <button onClick={() => setError("")} aria-label="Fermer l’erreur">
            <ControlIcon kind="close" />
          </button>
        </div>
      )}
      {toast && (
        <div className="toast" role="status">
          <ControlIcon kind="check" /> {toast}
          <button onClick={() => setToast("")} aria-label="Fermer">
            <ControlIcon kind="close" />
          </button>
        </div>
      )}
    </div>
  );
}
function OrdersTable({
  orders,
  open,
  finance = false,
}: {
  orders: any[];
  open: (id: string) => void;
  finance?: boolean;
}) {
  return orders.length ? (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Commande</th>
            <th>Magasin</th>
            <th>Cartons / unités</th>
            <th>Montant contractuel</th>
            <th>{finance ? "Paiement" : "Décision / exécution"}</th>
            <th>Prochaine action</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((o) => (
            <tr
              key={o.id}
              tabIndex={0}
              onClick={() => open(o.id)}
              onKeyDown={(e) => {
                if (e.key === "Enter") open(o.id);
              }}
            >
              <td>
                <strong>{o.number}</strong>
                <small>{date(o.createdAt, true)}</small>
              </td>
              <td>
                {o.merchant.shopName}
                <small>{o.merchant.name}</small>
              </td>
              <td>
                {o.quantity} <small>{o.itemCount} références</small>
              </td>
              <td>{money(o.total)}</td>
              <td>
                <Badge value={finance ? o.paymentStatus : o.status} />
              </td>
              <td>
                {finance
                  ? "Paiement & justificatif"
                  : (
                      {
                        REQUESTED: "Vérifier & proposer",
                        QUOTED: "Attendre la décision",
                        SUBMITTED: "Vérifier & confirmer",
                        CONFIRMED: "Affecter en préparation",
                        PREPARING: "Prélever les lignes",
                        READY: "Affecter un chauffeur",
                        OUT_FOR_DELIVERY: "Suivre la livraison",
                      } as Record<string, string>
                    )[o.status] || "Reçu & historique"}{" "}
                <span className="arrow">↗</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <Empty>
      Aucun dossier dans cette vue. Modifiez le filtre ou passez une commande
      depuis l’application commerçant.
    </Empty>
  );
}
function Delivery({
  orders,
  selected,
  open,
}: {
  orders: any[];
  selected: string | null;
  open: (id: string) => void;
}) {
  const [replay, setReplay] = useState(false),
    [mapMode, setMapMode] = useState("depot");
  const active = orders.filter((o) => operational.includes(o.status));
  const inRoute = active.filter((o) => o.status === "OUT_FOR_DELIVERY");
  return (
    <>
      <div className="stats">
        <Stat
          label="Commandes en livraison"
          value={inRoute.length}
          detail="Remise en cours"
        />
        <Stat
          label="Prêtes à charger"
          value={active.filter((o) => o.status === "READY").length}
          detail="Affectation et prise en charge requises"
        />
        <Stat
          label="En préparation"
          value={active.filter((o) => o.status === "PREPARING").length}
          detail="Quantités persistées par ligne"
        />
        <Stat
          label="Réceptions terminées"
          value={
            orders.filter((o) =>
              ["DELIVERED", "PARTIALLY_DELIVERED"].includes(o.status),
            ).length
          }
          detail="Paiement suivi séparément"
        />
      </div>
      <div className="delivery-grid">
        <section className="panel tour-list">
          <h2>Les opérations du jour</h2>
          <p>{active.length} commandes actives</p>
          {active.length ? (
            active.map((o) => (
              <button
                key={o.id}
                className={"tour " + (selected === o.id ? "selected" : "")}
                onClick={() => open(o.id)}
              >
                <div>
                  <strong>{o.number}</strong>
                  <Badge value={o.status} />
                </div>
                <span>{o.merchant.shopName}</span>
                <small>
                  {o.quantity} unités · {money(o.total)}
                </small>
              </button>
            ))
          ) : (
            <Empty>
              Les nouvelles commandes apparaîtront ici après confirmation.
            </Empty>
          )}
        </section>
        <section className="panel map-panel">
          <div className="panel-heading">
            <div>
              <h2>
                {mapMode === "depot"
                  ? "Dépôt & expédition"
                  : "Positions de livraison"}
              </h2>
              <p>
                {mapMode === "depot"
                  ? "Vue isométrique interactive · sélectionnez un véhicule"
                  : "GPS autorisé · horodatage et précision du chauffeur"}
              </p>
            </div>
            <div className="view-toggle">
              <button
                className={mapMode === "depot" ? "selected" : ""}
                onClick={() => setMapMode("depot")}
              >
                Dépôt 3D
              </button>
              <button
                className={mapMode === "gps" ? "selected" : ""}
                onClick={() => setMapMode("gps")}
              >
                Carte GPS
              </button>
            </div>
          </div>
          <React.Suspense
            fallback={
              <div className="warehouse boot-inline">
                <span className="spinner" />
                Ouverture du dépôt 3D…
              </div>
            }
          >
            {mapMode === "depot" ? (
              <Warehouse
                orders={orders}
                selected={selected || undefined}
                onSelect={open}
                replay={replay}
              />
            ) : (
              <LiveMap orders={orders} open={open} />
            )}
          </React.Suspense>
          <div className="map-footer">
            {mapMode === "depot" && (
              <Button secondary onClick={() => setReplay(!replay)}>
                {replay
                  ? "Arrêter l’animation"
                  : "Voir le mouvement illustratif"}
              </Button>
            )}
            <p>
              La géométrie représente un dépôt illustratif. La position GPS
              réelle se consulte dans le dossier de livraison.
            </p>
          </div>
        </section>
      </div>
      <section className="panel delivery-register">
        <h2>Registre des livraisons</h2>
        <p>
          Le chauffeur, la préparation, la preuve et l’encaissement sont liés au
          même dossier.
        </p>
        <OrdersTable
          orders={orders.filter((o) =>
            [
              "READY",
              "OUT_FOR_DELIVERY",
              "DELIVERED",
              "PARTIALLY_DELIVERED",
            ].includes(o.status),
          )}
          open={open}
        />
      </section>
    </>
  );
}

function Drawer({
  title,
  close,
  children,
}: {
  title: string;
  close: () => void;
  children: React.ReactNode;
}) {
  useEffect(() => {
    const old = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const h = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
      if (e.key === "Tab") {
        const els = [
          ...document.querySelectorAll<HTMLElement>(
            ".drawer button:not(:disabled),.drawer input,.drawer select,.drawer a",
          ),
        ];
        if (!els.length) return;
        const first = els[0],
          last = els.at(-1)!;
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener("keydown", h);
    document.querySelector<HTMLButtonElement>(".drawer-close")?.focus();
    return () => {
      document.body.style.overflow = old;
      document.removeEventListener("keydown", h);
    };
  }, []);
  return (
    <div className="drawer-overlay" onClick={close}>
      <section
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="drawer"
        onClick={(e) => e.stopPropagation()}
      >
        <header>
          <div>
            <small>Dossier TOUSHIR</small>
            <h2>{title}</h2>
          </div>
          <button
            className="drawer-close icon-button"
            aria-label="Fermer le dossier"
            onClick={close}
          >
            <ControlIcon kind="close" />
          </button>
        </header>
        {children}
      </section>
    </div>
  );
}
function OrderPanel({
  id,
  close,
  revision,
  run,
  busy,
  team,
  role,
}: {
  id: string;
  close: () => void;
  revision: number;
  run: (f: () => Promise<unknown>, message?: string) => Promise<void>;
  busy: boolean;
  team: any[];
  role: string;
}) {
  const [o, setO] = useState<any>(null),
    [tracking, setTracking] = useState<any>(null),
    [error, setError] = useState(""),
    [tab, setTab] = useState("detail"),
    [useTariffs, setUseTariffs] = useState(false);
  useEffect(() => {
    let active = true;
    Promise.all([
      api((role === "ADMIN" ? "/orders/" : "/operations/orders/") + id),
      role === "FINANCE"
        ? Promise.resolve(null)
        : api("/orders/" + id + "/tracking"),
    ])
      .then(([a, b]) => {
        if (active) {
          setO(a);
          setTracking(b);
        }
      })
      .catch((e) => setError(e.message));
    return () => {
      active = false;
    };
  }, [id, revision]);
  const financeBase = role === "ADMIN" ? "/admin" : "/operations";
  const submit = (
    event: React.FormEvent<HTMLFormElement>,
    fn: (data: FormData) => Promise<unknown>,
    message?: string,
  ) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    void run(() => fn(data), message);
  };
  return (
    <Drawer title={o?.number || "Chargement…"} close={close}>
      {error && <div className="error-banner">{error}</div>}
      {!o ? (
        <div className="boot-inline">
          <span className="spinner" />
          Chargement du dossier…
        </div>
      ) : (
        <>
          <div className="order-summary">
            <div>
              <Badge value={o.status} />
              <Badge value={o.paymentStatus} />
            </div>
            <h3>{o.merchant.shopName}</h3>
            <p>
              {o.merchant.name} · {o.address.commune}, {o.address.street}
            </p>
            <div className="order-money">
              <strong>{money(o.total)}</strong>
              <span>Reste à encaisser : {money(o.balance.remaining)}</span>
            </div>
          </div>
          <div className="tabs" role="tablist">
            {[
              ["detail", "Commande"],
              ["preparation", "Préparation"],
              ["tracking", "Livraison"],
              ["finance", "Règlements"],
              ["history", "Historique"],
            ]
              .filter(
                ([v]) =>
                  role === "ADMIN" ||
                  v === "detail" ||
                  v === "history" ||
                  (role === "FINANCE"
                    ? v === "finance"
                    : role === "WAREHOUSE"
                      ? ["preparation", "tracking"].includes(v)
                      : v === "tracking"),
              )
              .map(([v, l]) => (
                <button
                  key={v}
                  role="tab"
                  aria-selected={tab === v}
                  onClick={() => setTab(v)}
                >
                  {l}
                </button>
              ))}
          </div>
          <div className="drawer-content">
            {tab === "detail" && (
              <>
                <div className="section-heading">
                  <h3>Produits commandés</h3>
                  <span>{o.items.length} références</span>
                </div>
                {o.items.map((l: any) => (
                  <div className="order-line" key={l.productId}>
                    {l.imageUrl && <img src={l.imageUrl} alt={l.name} />}
                    <div>
                      <strong>{l.name}</strong>
                      <small>
                        {l.brand} · {l.quantity} unités · {money(l.unitPrice)} /
                        unité
                      </small>
                    </div>
                    <strong>{money(l.lineTotal)}</strong>
                  </div>
                ))}
                <dl className="totals">
                  <div>
                    <dt>Sous-total</dt>
                    <dd>{money(o.subtotal)}</dd>
                  </div>
                  <div>
                    <dt>Livraison</dt>
                    <dd>{money(o.deliveryFee)}</dd>
                  </div>
                  <div>
                    <dt>Total contractuel</dt>
                    <dd>{money(o.total)}</dd>
                  </div>
                </dl>
                <OrderPromotions order={o} role={role} run={run} busy={busy} />
                {role === "ADMIN" && (
                  <FrozenPricing order={o} revision={revision} />
                )}
                {o.note && (
                  <div className="note">
                    <strong>Note du commerçant</strong>
                    <p>{o.note}</p>
                  </div>
                )}
                {role === "ADMIN" &&
                  ["REQUESTED", "QUOTED"].includes(o.status) && (
                    <form
                      className="action-form"
                      onSubmit={(e) =>
                        submit(
                          e,
                          (data) =>
                            post("/admin/orders/" + id + "/quote", {
                              version: o.version,
                              useTariffPrices:
                                data.get("pricingMode") === "TARIFF",
                              pricingOverrideReason:
                                data.get("pricingOverrideReason") || null,
                              campaignIds:
                                data.get("promotionMode") === "auto"
                                  ? null
                                  : data.getAll("campaignIds"),
                              items: o.items.map((l: any) => ({
                                productId: l.productId,
                                quantity: Number(
                                  data.get("qty-" + l.productId),
                                ),
                                unitPrice: Number(
                                  data.get("price-" + l.productId),
                                ),
                                caseSize: Number(
                                  data.get("pack-" + l.productId),
                                ),
                                availableStock:
                                  data.get("stock-" + l.productId) === ""
                                    ? null
                                    : Number(data.get("stock-" + l.productId)),
                              })),
                              deliveryFee: Number(data.get("deliveryFee")),
                              validUntil: new Date(
                                Date.now() +
                                  Number(data.get("validHours")) * 3600000,
                              ).toISOString(),
                              note: data.get("note"),
                            }),
                          "Proposition envoyée. Le commerçant doit accepter cette version.",
                        )
                      }
                    >
                      <h3>
                        {o.status === "QUOTED"
                          ? "Réviser la proposition"
                          : "Préparer une proposition"}
                      </h3>
                      <p>
                        Le stock est réservé après l’acceptation du commerçant.
                        Saisissez vos prix et disponibilités confirmés.
                      </p>
                      <QuotePricingControls
                        order={o}
                        useTariffs={useTariffs}
                        setUseTariffs={setUseTariffs}
                      />
                      {o.items.map((l: any) => (
                        <fieldset key={l.productId}>
                          <legend>{l.name}</legend>
                          <div className="form-grid four">
                            <label>
                              Quantité
                              <input
                                name={"qty-" + l.productId}
                                type="number"
                                defaultValue={l.quantity}
                                min="0"
                                max={l.requestedQuantity}
                                required
                              />
                            </label>
                            <label>
                              Prix de base · DA
                              <input
                                name={"price-" + l.productId}
                                type="number"
                                defaultValue={
                                  o.promotions?.items?.find(
                                    (p: any) =>
                                      p.productId === l.productId &&
                                      p.quoteVersion === o.quoteVersion,
                                  )?.baseUnitPrice ??
                                  l.unitPrice ??
                                  ""
                                }
                                min="0"
                                step="0.01"
                                required={!useTariffs}
                                disabled={useTariffs}
                              />
                            </label>
                            <label>
                              Colisage
                              <input
                                name={"pack-" + l.productId}
                                type="number"
                                defaultValue={l.caseSize || 1}
                                min="1"
                                required
                              />
                            </label>
                            <label>
                              Stock physique
                              <input
                                name={"stock-" + l.productId}
                                type="number"
                                min="0"
                                placeholder="Conserver le stock"
                              />
                            </label>
                          </div>
                        </fieldset>
                      ))}
                      <div className="form-grid">
                        <label>
                          Frais de livraison · DA
                          <input
                            name="deliveryFee"
                            type="number"
                            min="0"
                            defaultValue={o.deliveryFee ?? 0}
                            required
                          />
                        </label>
                        <label>
                          Validité · heures
                          <input
                            name="validHours"
                            type="number"
                            min="1"
                            max="720"
                            defaultValue="48"
                            required
                          />
                        </label>
                      </div>
                      <CampaignSelection
                        productIds={o.items.map((l: any) => l.productId)}
                      />
                      <label>
                        Note pour le magasin
                        <textarea name="note" maxLength={1500} />
                      </label>
                      <Button type="submit" disabled={busy}>
                        Envoyer la proposition
                      </Button>
                    </form>
                  )}
                {role === "ADMIN" && o.status === "SUBMITTED" && (
                  <Button
                    disabled={busy}
                    onClick={() =>
                      void run(
                        () =>
                          post("/admin/orders/" + id + "/transition", {
                            status: "CONFIRMED",
                            reason: "Disponibilité vérifiée par le grossiste.",
                          }),
                        "Commande confirmée et réservée.",
                      )
                    }
                  >
                    Confirmer la commande
                  </Button>
                )}
                {["ADMIN", "WAREHOUSE"].includes(role) &&
                  o.status === "CONFIRMED" && (
                    <Button onClick={() => setTab("preparation")}>
                      Commencer la préparation
                    </Button>
                  )}
                {role === "ADMIN" &&
                  ["REQUESTED", "QUOTED", "SUBMITTED"].includes(o.status) && (
                    <form
                      className="action-form"
                      onSubmit={(e) =>
                        submit(
                          e,
                          (d) =>
                            post("/admin/orders/" + id + "/transition", {
                              status: "REJECTED",
                              reason: d.get("reason"),
                            }),
                          "Demande refusée avec son motif.",
                        )
                      }
                    >
                      <label>
                        Motif de refus
                        <input
                          name="reason"
                          required
                          minLength={3}
                          maxLength={800}
                        />
                      </label>
                      <Button secondary type="submit" disabled={busy}>
                        Refuser cette demande
                      </Button>
                    </form>
                  )}
                {role === "ADMIN" && (
                  <button
                    className="document-link"
                    onClick={() =>
                      void run(() =>
                        openDocument("/orders/" + id + "/document"),
                      )
                    }
                  >
                    Ouvrir le document de commande ↗
                  </button>
                )}
              </>
            )}
            {tab === "preparation" && (
              <>
                <h3>Prélever et contrôler</h3>
                <p>
                  La disponibilité physique et la réservation restent
                  distinctes. Les quantités ci-dessous sont enregistrées sur le
                  serveur.
                </p>
                <div className="progress-heading">
                  <strong>
                    {tracking?.preparation.prepared || 0} /{" "}
                    {tracking?.preparation.total || 0} unités
                  </strong>
                  <span>{tracking?.preparation.percentage || 0} %</span>
                </div>
                <progress
                  max="100"
                  value={tracking?.preparation.percentage || 0}
                />
                {["CONFIRMED", "PREPARING"].includes(o.status) ? (
                  <form
                    key={tracking?.version}
                    className="action-form"
                    onSubmit={(e) =>
                      submit(
                        e,
                        (data) =>
                          patch("/operations/orders/" + id + "/preparation", {
                            version: tracking.version,
                            items: o.items.map((l: any) => ({
                              productId: l.productId,
                              preparedQuantity: Number(data.get(l.productId)),
                            })),
                          }),
                        "Préparation enregistrée. La commande devient prête quand toutes les quantités sont contrôlées.",
                      )
                    }
                  >
                    {o.items.map((l: any) => (
                      <label className="prepare-line" key={l.productId}>
                        <span>
                          <strong>{l.name}</strong>
                          <small>À préparer : {l.quantity} unités</small>
                        </span>
                        <input
                          aria-label={"Quantité préparée " + l.name}
                          type="number"
                          name={l.productId}
                          defaultValue={
                            tracking?.preparation.items.find(
                              (p: any) => p.productId === l.productId,
                            )?.preparedQuantity || 0
                          }
                          min="0"
                          max={l.quantity}
                          required
                        />
                      </label>
                    ))}
                    <Button type="submit" disabled={busy}>
                      Enregistrer le prélèvement
                    </Button>
                  </form>
                ) : (
                  <div className="note">
                    <Badge value={o.status} />
                    <p>
                      {o.status === "READY"
                        ? "Les quantités sont contrôlées. Affectez le chauffeur dans l’onglet Livraison."
                        : "Le prélèvement est disponible pour les commandes confirmées en préparation."}
                    </p>
                  </div>
                )}
              </>
            )}
            {tab === "tracking" && (
              <>
                <h3>Garde et livraison</h3>
                <p>
                  Le chauffeur confirme la prise en charge depuis son
                  application.
                </p>
                <dl className="detail-list">
                  <div>
                    <dt>Chauffeur</dt>
                    <dd>{tracking?.assignment?.driverName || "À affecter"}</dd>
                  </div>
                  <div>
                    <dt>État de garde</dt>
                    <dd>
                      {tracking?.assignment ? (
                        <Badge value={tracking.assignment.status} />
                      ) : (
                        "Non prise en charge"
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt>Prise en charge</dt>
                    <dd>
                      {tracking?.assignment?.pickedUpAt
                        ? date(tracking.assignment.pickedUpAt, true)
                        : "En attente"}
                    </dd>
                  </div>
                </dl>
                {["ADMIN", "DISPATCHER"].includes(role) &&
                  ["CONFIRMED", "PREPARING", "READY"].includes(o.status) && (
                    <form
                      className="action-form"
                      onSubmit={(e) =>
                        submit(
                          e,
                          (d) =>
                            post("/operations/orders/" + id + "/assign", {
                              driverId: d.get("driverId"),
                              version: tracking.version,
                            }),
                          "Chauffeur affecté. La commande apparaît dans son manifeste.",
                        )
                      }
                    >
                      <label>
                        Chauffeur affecté
                        <select
                          name="driverId"
                          defaultValue={tracking?.assignment?.driverId || ""}
                          required
                        >
                          <option value="">Choisir un chauffeur</option>
                          {team
                            .filter((t) => t.role === "DRIVER")
                            .map((d) => (
                              <option value={d.id} key={d.id}>
                                {d.name}
                              </option>
                            ))}
                        </select>
                      </label>
                      <Button type="submit" disabled={busy}>
                        Affecter le chauffeur
                      </Button>
                    </form>
                  )}
                <h3 className="spaced">Position autorisée</h3>
                {tracking?.location ? (
                  <div className="location-card">
                    <span
                      className={
                        "badge " +
                        (tracking.location.isFresh ? "green" : "amber")
                      }
                    >
                      {tracking.location.isFresh
                        ? "Position récente"
                        : "Dernière position connue"}
                    </span>
                    <strong>
                      {tracking.location.latitude.toFixed(5)},{" "}
                      {tracking.location.longitude.toFixed(5)}
                    </strong>
                    <p>
                      Précision ± {Math.round(tracking.location.accuracy)} m ·{" "}
                      {date(tracking.location.capturedAt, true)}
                    </p>
                    <a
                      target="_blank"
                      rel="noreferrer"
                      href={`https://www.openstreetmap.org/?mlat=${tracking.location.latitude}&mlon=${tracking.location.longitude}#map=16/${tracking.location.latitude}/${tracking.location.longitude}`}
                    >
                      Ouvrir cette position sur OpenStreetMap ↗
                    </a>
                  </div>
                ) : (
                  <div className="note">
                    <p>
                      Aucune position GPS autorisée pour cette livraison. Elle
                      apparaîtra lorsque le chauffeur partagera sa position
                      pendant la tournée.
                    </p>
                  </div>
                )}
                {["ADMIN", "WAREHOUSE"].includes(role) &&
                  o.items
                    .filter((l: any) => l.refusedQuantity > l.inspectedQuantity)
                    .map((l: any) => (
                      <section className="module-section" key={l.id}>
                        <h3>Retour à contrôler · {l.name}</h3>
                        <ReturnLotIdentification
                          sourceType="REFUSAL"
                          sourceId={l.id}
                          max={l.refusedQuantity - l.inspectedQuantity}
                          run={run}
                        />
                        <form
                          className="action-form"
                          onSubmit={(e) =>
                            submit(
                              e,
                              (d) =>
                                post(
                                  "/operations/orders/" +
                                    id +
                                    "/returns/inspect",
                                  {
                                    productId: l.productId,
                                    quantity: Number(d.get("quantity")),
                                    disposition: d.get("disposition"),
                                    reason: d.get("reason"),
                                  },
                                ),
                              "Contrôle du retour enregistré sans modification automatique du paiement.",
                            )
                          }
                        >
                          <div className="form-grid">
                            <label>
                              Unités physiquement inspectées
                              <input
                                name="quantity"
                                type="number"
                                min="1"
                                max={l.refusedQuantity - l.inspectedQuantity}
                                required
                              />
                            </label>
                            <label>
                              Décision
                              <select name="disposition">
                                <option value="RESTOCK">
                                  Remettre en stock après contrôle
                                </option>
                                <option value="WRITE_OFF">
                                  Déclasser / perte
                                </option>
                              </select>
                            </label>
                          </div>
                          <label>
                            Constat de contrôle
                            <input name="reason" required maxLength={800} />
                          </label>
                          <Button type="submit" disabled={busy}>
                            Enregistrer l’inspection physique
                          </Button>
                        </form>
                      </section>
                    ))}
                <h3 className="spaced">Preuve de réception</h3>
                {tracking?.receipt ? (
                  <div className="note">
                    <strong>Réception vérifiée</strong>
                    <p>
                      {date(tracking.receipt.at, true)} · version acceptée{" "}
                      {tracking.receipt.acceptedVersion}
                    </p>
                    <p>
                      Les quantités ont été confirmées par le commerçant et le
                      code consommé par le livreur.
                    </p>
                  </div>
                ) : (
                  <p>
                    Le commerçant confirme les quantités puis présente son code
                    à usage unique. La preuve ne crée aucun encaissement.
                  </p>
                )}
              </>
            )}
            {tab === "finance" && (
              <>
                <h3>Livraison, paiement et caisse</h3>
                <dl className="totals">
                  <div>
                    <dt>Montant dû</dt>
                    <dd>{money(o.total)}</dd>
                  </div>
                  <div>
                    <dt>Encaissé net</dt>
                    <dd>{money(o.balance.collected)}</dd>
                  </div>
                  <div>
                    <dt>Reste à encaisser</dt>
                    <dd>{money(o.balance.remaining)}</dd>
                  </div>
                  <div>
                    <dt>Remboursement dû</dt>
                    <dd>{money(o.balance.refundDue)}</dd>
                  </div>
                </dl>
                {o.total != null &&
                  o.balance.remaining > 0 &&
                  [
                    "CONFIRMED",
                    "PREPARING",
                    "READY",
                    "OUT_FOR_DELIVERY",
                    "DELIVERED",
                    "PARTIALLY_DELIVERED",
                  ].includes(o.status) && (
                    <form
                      className="action-form"
                      onSubmit={(e) =>
                        submit(
                          e,
                          (d) =>
                            post(financeBase + "/orders/" + id + "/payments", {
                              amount: Number(d.get("amount")),
                              method: d.get("method"),
                              reference: d.get("reference"),
                              idempotencyKey: key(),
                            }),
                          "Encaissement enregistré une seule fois.",
                        )
                      }
                    >
                      <h3>Enregistrer des fonds réellement reçus</h3>
                      <label>
                        Montant · DA
                        <input
                          name="amount"
                          type="number"
                          min="0.01"
                          max={o.balance.remaining}
                          step="0.01"
                          defaultValue={o.balance.remaining}
                          required
                        />
                      </label>
                      <label>
                        Moyen
                        <select name="method">
                          <option value="CASH">
                            Espèces physiquement reçues
                          </option>
                          <option value="TRANSFER">Virement vérifié</option>
                        </select>
                      </label>
                      <label>
                        Référence / justificatif
                        <input name="reference" required maxLength={150} />
                      </label>
                      <Button type="submit" disabled={busy}>
                        Enregistrer l’encaissement
                      </Button>
                    </form>
                  )}
                {o.payments?.map((p: any) => (
                  <div className="payment-row" key={p.id}>
                    <div>
                      <strong>
                        {money(p.amount)} ·{" "}
                        {p.isRefund
                          ? "Remboursement"
                          : p.method === "CASH"
                            ? "Espèces"
                            : "Virement"}
                      </strong>
                      <small>
                        {date(p.at, true)} · {p.reference}
                      </small>
                    </div>
                    {p.reconciled ? (
                      <Badge value="RECONCILED" />
                    ) : (
                      <Button
                        secondary
                        disabled={busy}
                        onClick={() =>
                          void run(
                            () =>
                              post(
                                financeBase +
                                  "/payments/" +
                                  p.id +
                                  "/reconcile",
                              ),
                            "Écriture rapprochée avec la caisse.",
                          )
                        }
                      >
                        Rapprocher
                      </Button>
                    )}
                  </div>
                ))}
                {o.balance.refundDue > 0 && (
                  <form
                    className="action-form"
                    onSubmit={(e) =>
                      submit(
                        e,
                        (d) =>
                          post(financeBase + "/orders/" + id + "/refunds", {
                            amount: Number(d.get("amount")),
                            reason: d.get("reason"),
                            reference: d.get("reference"),
                            idempotencyKey: key(),
                          }),
                        "Remboursement enregistré.",
                      )
                    }
                  >
                    <h3>Rembourser le trop-perçu</h3>
                    <label>
                      Montant
                      <input
                        name="amount"
                        type="number"
                        step="0.01"
                        min="0.01"
                        max={o.balance.refundDue}
                        defaultValue={o.balance.refundDue}
                        required
                      />
                    </label>
                    <label>
                      Motif
                      <input name="reason" required />
                    </label>
                    <label>
                      Référence
                      <input name="reference" required />
                    </label>
                    <Button type="submit" disabled={busy}>
                      Enregistrer le remboursement
                    </Button>
                  </form>
                )}
              </>
            )}
            {tab === "history" && (
              <>
                <h3>Historique du dossier</h3>
                <div className="timeline">
                  {o.timeline?.map((e: any) => (
                    <div key={e.id}>
                      <span className="timeline-dot" />
                      <time>{date(e.at, true)}</time>
                      <strong>
                        {labels[e.action] || e.action.replaceAll("_", " ")}
                      </strong>
                      <p>{e.details}</p>
                      <small>{e.actor}</small>
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
        </>
      )}
    </Drawer>
  );
}
function Products({
  query,
  revision,
  stock,
  edit,
}: {
  query: string;
  revision: number;
  stock: boolean;
  edit: (p: any) => void;
}) {
  const [data, setData] = useState<any>(null),
    [page, setPage] = useState(1),
    [q, setQ] = useState(""),
    [error, setError] = useState("");
  useEffect(() => {
    setPage(1);
  }, [query, q]);
  useEffect(() => {
    let active = true;
    api(
      `/admin/products?pageSize=25&page=${page}&q=${encodeURIComponent(query || q)}`,
    )
      .then((x) => {
        if (active) setData(x);
      })
      .catch((e) => setError(e.message));
    return () => {
      active = false;
    };
  }, [query, q, page, revision]);
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <h2>
            {stock
              ? "Stock physique, réservations et quarantaine"
              : "Produits publiés dans l’app"}
          </h2>
          <p>
            {stock
              ? "Un retour doit être inspecté avant sa remise en stock."
              : "Une offre publiée exige un prix et un stock confirmés."}
          </p>
        </div>
        <input
          className="search-input"
          placeholder="Rechercher un produit…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          aria-label="Rechercher dans le catalogue"
        />
      </div>
      {error && <div className="error-banner">{error}</div>}
      {data ? (
        <>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Produit / conditionnement</th>
                  <th>Rayon / marque</th>
                  <th>Prix / unité</th>
                  <th>Disponible</th>
                  <th>Réservé</th>
                  <th>App</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((p: any) => (
                  <tr
                    key={p.id}
                    tabIndex={0}
                    onClick={() => edit(p)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") edit(p);
                    }}
                  >
                    <td>
                      <div className="product-cell">
                        {p.imageUrl && <img src={p.imageUrl} alt={p.name} />}
                        <div>
                          <strong>{p.name}</strong>
                          <small>
                            {p.barcode} ·{" "}
                            {p.unit || "Conditionnement à confirmer"}
                          </small>
                        </div>
                      </div>
                    </td>
                    <td>
                      {p.category}
                      <small>{p.brand}</small>
                    </td>
                    <td>{money(p.price)}</td>
                    <td>{p.stock ?? "À confirmer"}</td>
                    <td>{p.reserved ?? "—"}</td>
                    <td>
                      <span
                        className={
                          "badge " + (p.offerApproved ? "green" : "neutral")
                        }
                      >
                        {p.offerApproved ? "Publié" : "Sur devis"}
                      </span>
                    </td>
                    <td>Modifier ↗</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pagination">
            <span>
              {number(data.total)} références sourcées · page {page} /{" "}
              {data.totalPages}
            </span>
            <div>
              <Button
                secondary
                disabled={page === 1}
                onClick={() => setPage(page - 1)}
              >
                Précédent
              </Button>
              <Button
                secondary
                disabled={page >= data.totalPages}
                onClick={() => setPage(page + 1)}
              >
                Suivant
              </Button>
            </div>
          </div>
        </>
      ) : (
        <div className="boot-inline">
          <span className="spinner" />
          Chargement du catalogue…
        </div>
      )}
    </section>
  );
}
function ProductPanel({
  product: p,
  close,
  run,
  busy,
}: {
  product: any;
  close: () => void;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
  busy: boolean;
}) {
  return (
    <Drawer title={p.name} close={close}>
      <div className="drawer-content">
        <div className="product-detail">
          {p.imageUrl && <img src={p.imageUrl} alt={p.name} />}
          <div>
            <strong>{p.brand}</strong>
            <p>
              {p.category} · {p.unit}
            </p>
            <small>{p.barcode}</small>
          </div>
        </div>
        <form
          className="action-form"
          onSubmit={(e) => {
            e.preventDefault();
            const d = new FormData(e.currentTarget);
            void run(async () => {
              await patch("/admin/products/" + p.id, {
                price: d.get("price") === "" ? null : Number(d.get("price")),
                stock: d.get("stock") === "" ? null : Number(d.get("stock")),
                caseSize: Number(d.get("caseSize")),
                minimumQuantity: Number(d.get("minimumQuantity")),
                offerApproved: d.get("approved") === "on",
                isActive: d.get("active") === "on",
              });
              close();
            }, "Offre et stock enregistrés. Le catalogue commerçant est actualisé.");
          }}
        >
          <h3>Conditions commerciales</h3>
          <p>
            Cette offre sert de repli aux tarifs publiés. Les contrats des
            magasins et les segments restent prioritaires. Les versions déjà
            proposées conservent leurs prix.
          </p>
          {p.pricing?.ruleId && (
            <p className="note">
              Un barème standard publié est actuellement retenu :{" "}
              {money(p.price)} / unité. Ce formulaire modifie l’offre du dépôt ;
              le barème se gère dans Tarifs & conditions.
            </p>
          )}
          <div className="form-grid">
            <label>
              Prix de l’offre du dépôt · DA
              <input
                name="price"
                type="number"
                min="0"
                step="0.01"
                defaultValue={p.savedPrice ?? ""}
                placeholder="Sur devis"
              />
            </label>
            <label>
              Stock physique
              <input
                name="stock"
                type="number"
                min="0"
                defaultValue={p.physicalStock ?? ""}
                placeholder="À confirmer"
              />
            </label>
            <label>
              Unités par colis
              <input
                name="caseSize"
                type="number"
                min="1"
                defaultValue={p.caseSize || 1}
                required
              />
            </label>
            <label>
              Quantité minimale
              <input
                name="minimumQuantity"
                type="number"
                min="1"
                defaultValue={p.minimumQuantity || 1}
                required
              />
            </label>
          </div>
          <label className="checkbox">
            <input
              name="approved"
              type="checkbox"
              defaultChecked={p.offerApproved}
            />
            Publier le prix et la disponibilité dans l’app
          </label>
          <label className="checkbox">
            <input
              name="active"
              type="checkbox"
              defaultChecked={p.isActive !== false}
            />
            Autoriser ce produit à la commande
          </label>
          <Button type="submit" disabled={busy}>
            Enregistrer les conditions
          </Button>
        </form>
        <div className="note">
          <h3>Provenance</h3>
          <p>
            {p.source || "Open Food Facts"} · {p.sourceLicense || "ODbL-1.0"}
          </p>
          <p>
            Les photographies et les marques conservent leurs droits et
            proportions d’origine.
          </p>
          {p.sourceUrl && (
            <a href={p.sourceUrl} target="_blank" rel="noreferrer">
              Consulter la fiche source ↗
            </a>
          )}
        </div>
      </div>
    </Drawer>
  );
}
function Merchants({
  query,
  revision,
  run,
  busy,
}: {
  query: string;
  revision: number;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
  busy: boolean;
}) {
  const [data, setData] = useState<any>(null),
    [invites, setInvites] = useState<any[]>([]),
    [link, setLink] = useState(""),
    [error, setError] = useState("");
  useEffect(() => {
    Promise.all([
      api("/admin/merchants?pageSize=100&q=" + encodeURIComponent(query)),
      api("/admin/demo/invitations").catch(() => ({ items: [] })),
    ])
      .then(([d, i]) => {
        setData(d);
        setInvites(i.items);
      })
      .catch((e) => setError(e.message));
  }, [query, revision]);
  return (
    <>
      <section className="panel">
        <h2>Commerces et habilitations</h2>
        <p>
          L’approbation autorise la prise de commande. Chaque magasin ne voit
          que ses propres dossiers.
        </p>
        {error && <div className="error-banner">{error}</div>}
        {data ? (
          <div className="table-wrap module-section">
            <table>
              <thead>
                <tr>
                  <th>Magasin</th>
                  <th>Contact</th>
                  <th>Commandes</th>
                  <th>Ventes réceptionnées</th>
                  <th>Vérification</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((m: any) => (
                  <tr key={m.id}>
                    <td>
                      <strong>{m.shopName}</strong>
                      <small>Créé le {date(m.createdAt)}</small>
                    </td>
                    <td>
                      {m.name}
                      <small>{m.email}</small>
                      <small>{m.phone}</small>
                    </td>
                    <td>{m.orderCount}</td>
                    <td>{money(m.totalSpent)}</td>
                    <td>
                      <Badge value={m.approvalStatus} />
                    </td>
                    <td>
                      <select
                        aria-label={"Statut du magasin " + m.shopName}
                        value={m.approvalStatus}
                        disabled={busy}
                        onChange={(e) =>
                          void run(
                            () =>
                              patch("/admin/merchants/" + m.id, {
                                approvalStatus: e.target.value,
                              }),
                            "Statut du commerce enregistré.",
                          )
                        }
                      >
                        <option value="PENDING">À vérifier</option>
                        <option value="APPROVED">Approuvé</option>
                        <option value="SUSPENDED">Suspendu</option>
                      </select>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <Empty>Chargement des commerces…</Empty>
        )}
      </section>
      <div className="split-panels module-section">
        <section className="panel">
          <h2>Inviter un commerce à tester</h2>
          <p>
            Le lien donne accès à ses commandes, sans partager votre
            administration.
          </p>
          <form
            className="action-form"
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(async () => {
                const i = await post("/admin/demo/invitations", {
                  label: d.get("label"),
                  hours: Number(d.get("hours")),
                  maxUses: Number(d.get("uses")),
                });
                setLink(location.origin + "/mobile" + i.joinPath);
              }, "Invitation créée. Vous pouvez transmettre ce lien au commerce.");
            }}
          >
            <label>
              Nom de l’invitation
              <input
                name="label"
                placeholder="Test équipe commerciale"
                required
                maxLength={100}
              />
            </label>
            <div className="form-grid">
              <label>
                Durée · heures
                <input
                  name="hours"
                  type="number"
                  defaultValue="168"
                  min="1"
                  max="168"
                  required
                />
              </label>
              <label>
                Nombre de commerces
                <input
                  name="uses"
                  type="number"
                  defaultValue="10"
                  min="1"
                  max="100"
                  required
                />
              </label>
            </div>
            <Button type="submit" disabled={busy}>
              Créer un lien commerçant
            </Button>
          </form>
          {link && (
            <div className="invite-link">
              <strong>Votre lien d’invitation</strong>
              <input
                aria-label="Lien d’invitation commerçant"
                readOnly
                value={link}
              />
              <Button
                secondary
                onClick={() => void navigator.clipboard.writeText(link)}
              >
                Copier le lien
              </Button>
            </div>
          )}
        </section>
        <section className="panel">
          <h2>Invitations de cet espace</h2>
          <div className="simple-list">
            {invites.length ? (
              invites.map((i) => (
                <div key={i.id}>
                  <span>
                    <strong>{i.label}</strong>
                    <small>
                      {i.uses}/{i.maxUses} commerces · expire le{" "}
                      {date(i.expiresAt, true)}
                    </small>
                  </span>
                  {i.revokedAt ? (
                    <span className="badge neutral">Révoquée</span>
                  ) : (
                    <Button
                      secondary
                      disabled={busy}
                      onClick={() =>
                        void run(
                          () =>
                            post("/admin/demo/invitations/" + i.id + "/revoke"),
                          "Invitation révoquée. Les accès associés sont fermés.",
                        )
                      }
                    >
                      Révoquer
                    </Button>
                  )}
                </div>
              ))
            ) : (
              <Empty>Aucune invitation créée.</Empty>
            )}
          </div>
        </section>
      </div>
    </>
  );
}
function Analytics({ revision }: { revision: number }) {
  const [days, setDays] = useState(30),
    [data, setData] = useState<any>(null),
    [error, setError] = useState("");
  useEffect(() => {
    api("/admin/analytics?days=" + days)
      .then(setData)
      .catch((e) => setError(e.message));
  }, [days, revision]);
  return (
    <>
      {error && <div className="error-banner">{error}</div>}
      <div className="panel-heading">
        <span />
        <select
          aria-label="Période d’analyse"
          value={days}
          onChange={(e) => setDays(Number(e.target.value))}
        >
          <option value={7}>7 derniers jours</option>
          <option value={30}>30 derniers jours</option>
          <option value={90}>90 derniers jours</option>
        </select>
      </div>
      {data ? (
        <>
          <div className="stats">
            {[
              ["revenue", "Ventes réceptionnées"],
              ["collected", "Encaissements nets"],
              ["orders", "Demandes reçues"],
              ["customers", "Commerces actifs"],
            ].map(([k, l]) => (
              <Stat
                key={k}
                label={l}
                value={
                  ["revenue", "collected"].includes(k)
                    ? money(data.metrics[k].value)
                    : number(data.metrics[k].value)
                }
                detail={
                  data.metrics[k].changePercent == null
                    ? "Période précédente sans base de comparaison"
                    : `${data.metrics[k].changePercent > 0 ? "+" : ""}${data.metrics[k].changePercent} % vs période précédente`
                }
              />
            ))}
          </div>
          <section className="panel">
            <div className="panel-heading">
              <div>
                <h2>Ventes et réceptions</h2>
                <p>
                  {date(data.period.from)} — {date(data.period.to)}
                </p>
              </div>
              <span className="badge neutral">Heure d’Alger</span>
            </div>
            <Chart points={data.daily} field="revenue" />
            <p className="metrics-note">{data.basis.revenue}</p>
          </section>
          <div className="split-panels module-section">
            <section className="panel">
              <h2>Produits réceptionnés</h2>
              <div className="bar-list">
                {data.topProducts.map((p: any) => (
                  <div key={p.productId}>
                    <div>
                      <span>{p.name}</span>
                      <strong>{money(p.revenue)}</strong>
                    </div>
                    <progress
                      max={Math.max(
                        ...data.topProducts.map((x: any) => x.revenue),
                        1,
                      )}
                      value={p.revenue}
                    />
                    <small>{p.quantity} unités acceptées</small>
                  </div>
                ))}
              </div>
              {data.topProducts.length === 0 && (
                <Empty>Les produits apparaîtront après leur réception.</Empty>
              )}
            </section>
            <section className="panel">
              <h2>Soldes à encaisser</h2>
              <div className="bar-list">
                {data.aging.map((a: any) => (
                  <div key={a.label}>
                    <div>
                      <span>{a.label}</span>
                      <strong>{money(a.amount)}</strong>
                    </div>
                    <progress
                      max={Math.max(...data.aging.map((x: any) => x.amount), 1)}
                      value={a.amount}
                    />
                  </div>
                ))}
              </div>
              <p className="metrics-note">{data.basis.aging}</p>
              <div className="health-chip">
                <strong>{money(data.operations.unreconciled)}</strong>
                <span>écritures non rapprochées</span>
              </div>
            </section>
          </div>
        </>
      ) : (
        <div className="boot-inline">
          <span className="spinner" />
          Calcul des indicateurs…
        </div>
      )}
    </>
  );
}
function Returns({
  revision,
  open,
  run,
}: {
  revision: number;
  open: (id: string) => void;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
}) {
  const [claims, setClaims] = useState<any[]>([]),
    [selected, setSelected] = useState<any>(null),
    [error, setError] = useState("");
  useEffect(() => {
    api("/admin/claims?pageSize=100")
      .then((d) => {
        setClaims(d.items);
        setSelected((old: any) =>
          old ? d.items.find((x: any) => x.id === old.id) : null,
        );
      })
      .catch((e) => setError(e.message));
  }, [revision]);
  const submit = (
    e: React.FormEvent<HTMLFormElement>,
    endpoint: string,
    body: (d: FormData) => any,
  ) => {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    void run(
      () => post("/admin/claims/" + selected.id + "/" + endpoint, body(data)),
      "Opération de retour enregistrée et tracée.",
    );
  };
  return (
    <>
      <section className="panel">
        <h2>Réclamations et contrôle des retours</h2>
        <p>
          Décision, réception physique, inspection, avoir et remboursement sont
          des actions distinctes.
        </p>
        {error && <div className="error-banner">{error}</div>}
        {claims.length ? (
          <div className="table-wrap module-section">
            <table>
              <thead>
                <tr>
                  <th>Commande / commerce</th>
                  <th>Produit</th>
                  <th>Motif</th>
                  <th>Quantité</th>
                  <th>Statut</th>
                  <th>Avoir émis</th>
                </tr>
              </thead>
              <tbody>
                {claims.map((c) => (
                  <tr
                    key={c.id}
                    tabIndex={0}
                    onClick={() => setSelected(c)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") setSelected(c);
                    }}
                  >
                    <td>
                      {c.orderNumber}
                      <small>{c.shopName}</small>
                    </td>
                    <td>{c.productName}</td>
                    <td>{c.reason}</td>
                    <td>
                      {c.quantity}
                      <small>
                        {c.receivedQuantity} reçues · {c.inspectedQuantity}{" "}
                        inspectées
                      </small>
                    </td>
                    <td>
                      <Badge value={c.status} />
                    </td>
                    <td>{money(c.creditedAmount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <Empty>
            Les réclamations envoyées par les commerçants apparaîtront ici.
          </Empty>
        )}
      </section>
      {selected && (
        <Drawer
          title={"Retour · " + selected.productName}
          close={() => setSelected(null)}
        >
          <div className="drawer-content">
            <p>{selected.reason}</p>
            <Button
              secondary
              onClick={() => {
                open(selected.orderId);
                setSelected(null);
              }}
            >
              Ouvrir la commande
            </Button>
            {selected.status === "OPEN" && (
              <form
                className="action-form"
                onSubmit={(e) =>
                  submit(e, "resolve", (d) => ({
                    status: d.get("status"),
                    note: d.get("note"),
                  }))
                }
              >
                <h3>Décider de la demande</h3>
                <label>
                  Décision
                  <select name="status">
                    <option value="ACCEPTED">Accepter le retour</option>
                    <option value="REJECTED">Refuser avec motif</option>
                  </select>
                </label>
                <label>
                  Motif de décision
                  <textarea name="note" required maxLength={1500} />
                </label>
                <Button type="submit">Enregistrer la décision</Button>
              </form>
            )}
            {selected.status === "ACCEPTED" && (
              <>
                {selected.inspectedQuantity < selected.receivedQuantity && (
                  <ReturnLotIdentification
                    sourceType="CLAIM"
                    sourceId={selected.id}
                    max={selected.receivedQuantity - selected.inspectedQuantity}
                    run={run}
                  />
                )}
                {selected.receivedQuantity < selected.quantity && (
                  <form
                    className="action-form"
                    onSubmit={(e) =>
                      submit(e, "receive", (d) => ({
                        quantity: Number(d.get("quantity")),
                        note: d.get("note"),
                      }))
                    }
                  >
                    <h3>Réception physique en quarantaine</h3>
                    <label>
                      Unités réellement reçues
                      <input
                        type="number"
                        name="quantity"
                        min="1"
                        max={selected.quantity - selected.receivedQuantity}
                        defaultValue={
                          selected.quantity - selected.receivedQuantity
                        }
                        required
                      />
                    </label>
                    <label>
                      Constat
                      <input name="note" required />
                    </label>
                    <Button type="submit">Enregistrer la réception</Button>
                  </form>
                )}
                {selected.inspectedQuantity < selected.receivedQuantity && (
                  <form
                    className="action-form"
                    onSubmit={(e) =>
                      submit(e, "inspect", (d) => ({
                        quantity: Number(d.get("quantity")),
                        disposition: d.get("disposition"),
                        reason: d.get("reason"),
                      }))
                    }
                  >
                    <h3>Contrôle qualité</h3>
                    <label>
                      Quantité
                      <input
                        name="quantity"
                        type="number"
                        min="1"
                        max={
                          selected.receivedQuantity - selected.inspectedQuantity
                        }
                        defaultValue={
                          selected.receivedQuantity - selected.inspectedQuantity
                        }
                        required
                      />
                    </label>
                    <label>
                      Décision
                      <select name="disposition">
                        <option value="RESTOCK">
                          Remise en stock après contrôle
                        </option>
                        <option value="WRITE_OFF">Déclasser / perte</option>
                      </select>
                    </label>
                    <label>
                      Motif
                      <input name="reason" required />
                    </label>
                    <Button type="submit">Valider le contrôle</Button>
                  </form>
                )}
                {selected.receivedQuantity > 0 && (
                  <form
                    className="action-form"
                    onSubmit={(e) =>
                      submit(e, "credit", (d) => ({
                        amount: Number(d.get("amount")),
                        reason: d.get("reason"),
                        idempotencyKey: key(),
                      }))
                    }
                  >
                    <h3>Avoir commercial explicite</h3>
                    <p>
                      Le serveur limite l’avoir à la valeur des unités
                      retournées. Un éventuel remboursement reste à enregistrer
                      séparément.
                    </p>
                    <label>
                      Montant · DA
                      <input
                        name="amount"
                        type="number"
                        min="0.01"
                        step="0.01"
                        required
                      />
                    </label>
                    <label>
                      Motif
                      <input name="reason" required />
                    </label>
                    <Button type="submit">Émettre l’avoir</Button>
                  </form>
                )}
              </>
            )}
          </div>
        </Drawer>
      )}
    </>
  );
}
function Settings({
  session,
  team,
  revision,
  run,
}: {
  session: Session;
  team: any[];
  revision: number;
  run: (f: () => Promise<unknown>, m?: string) => Promise<void>;
}) {
  const [health, setHealth] = useState<any>(null),
    [audit, setAudit] = useState<any[]>([]);
  useEffect(() => {
    Promise.all([api("/health"), api("/admin/audit?pageSize=25")]).then(
      ([h, a]) => {
        setHealth(h);
        setAudit(a.items);
      },
    );
  }, [revision]);
  return (
    <>
      <div className="split-panels">
        <section className="panel">
          <h2>Équipe & habilitations</h2>
          <p>
            Les autorisations sont contrôlées par le serveur sur chaque
            ressource.
          </p>
          <div className="simple-list">
            {team.map((t) => (
              <div key={t.id}>
                <span>
                  <strong>{t.name}</strong>
                  <small>{t.phone || "Compte interne"}</small>
                </span>
                <span className="badge neutral">
                  {(
                    {
                      ADMIN: "Administration",
                      WAREHOUSE: "Préparation",
                      DISPATCHER: "Dispatch",
                      FINANCE: "Finance",
                      SALES: "Conseil commercial",
                      DRIVER: "Livreur",
                    } as any
                  )[t.role] || t.role}
                </span>
              </div>
            ))}
          </div>
          <form
            className="action-form"
            onSubmit={(e) => {
              e.preventDefault();
              const d = new FormData(e.currentTarget);
              void run(
                () =>
                  post("/operations/team", {
                    name: d.get("name"),
                    email: d.get("email"),
                    phone: d.get("phone"),
                    password: d.get("password"),
                    role: d.get("role"),
                  }),
                "Compte d’équipe créé avec son rôle.",
              );
            }}
          >
            <h3>Ajouter un membre</h3>
            <div className="form-grid">
              <label>
                Nom
                <input name="name" required maxLength={100} />
              </label>
              <label>
                E-mail
                <input name="email" type="email" required />
              </label>
              <label>
                Téléphone
                <input name="phone" type="tel" />
              </label>
              <label>
                Rôle
                <select name="role">
                  <option value="DRIVER">Livreur</option>
                  <option value="WAREHOUSE">Préparation</option>
                  <option value="DISPATCHER">Dispatch</option>
                  <option value="FINANCE">Finance</option>
                  <option value="SALES">Conseil commercial</option>
                </select>
              </label>
            </div>
            <label>
              Mot de passe initial · 14 caractères minimum
              <input
                name="password"
                type="password"
                minLength={14}
                maxLength={128}
                required
                autoComplete="new-password"
              />
            </label>
            <Button type="submit">Créer le compte</Button>
          </form>
        </section>
        <section className="panel">
          <h2>Environnement</h2>
          <div className="health-chip">
            <span className="live-dot" />
            <strong>
              {health?.status === "ok" ? "Serveur connecté" : "Vérification…"}
            </strong>
          </div>
          <dl className="detail-list">
            <div>
              <dt>Framework</dt>
              <dd>{health?.framework || ".NET 10"}</dd>
            </div>
            <div>
              <dt>ABP</dt>
              <dd>{health?.abpVersion || "10.6.1"}</dd>
            </div>
            <div>
              <dt>Catalogue</dt>
              <dd>{number(health?.products || 0)} produits</dd>
            </div>
            <div>
              <dt>Espace</dt>
              <dd>
                {session.workspace.isDemo
                  ? "Démonstration isolée"
                  : "Professionnel"}
              </dd>
            </div>
          </dl>
          <h3 className="spaced">Les trois interfaces</h3>
          <div className="simple-list">
            <div>
              <span>
                <strong>Plateforme grossiste</strong>
                <small>Opérations, préparation, finance et audit</small>
              </span>
              <span className="badge green">Cette fenêtre</span>
            </div>
            <div>
              <span>
                <strong>Application commerçant</strong>
                <small>Catalogue, commandes et réception</small>
              </span>
              <a href="/mobile/merchant" target="_blank" rel="noreferrer">
                Ouvrir ↗
              </a>
            </div>
            <div>
              <span>
                <strong>Application livreur</strong>
                <small>Manifeste, position, remise et espèces</small>
              </span>
              <a href="/mobile/driver" target="_blank" rel="noreferrer">
                Ouvrir ↗
              </a>
            </div>
          </div>
          <div className="note">
            <strong>Usage de démonstration</strong>
            <p>
              Les opérations de cet espace sont fictives. Les accès invités ne
              donnent aucun droit d’administration. Les espaces de démonstration
              expirent après sept jours.
            </p>
          </div>
        </section>
      </div>
      <section className="panel module-section">
        <h2>Journal d’audit</h2>
        <p>
          Les opérations sont attribuées à leur auteur et conservent leur date.
        </p>
        <div className="table-wrap module-section">
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Auteur</th>
                <th>Action</th>
                <th>Référence</th>
                <th>Détail</th>
              </tr>
            </thead>
            <tbody>
              {audit.map((a) => (
                <tr key={a.id}>
                  <td>{date(a.at, true)}</td>
                  <td>{a.actor}</td>
                  <td>{a.action.replaceAll("_", " ")}</td>
                  <td>{a.entityId.slice(0, 12)}</td>
                  <td>{a.details}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}
createRoot(document.getElementById("root")!).render(<App />);
