import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { renewIdentityToken } from "./oidc";
export type Session = {
  token: string;
  workspaceToken?: string;
  user: { id: string; name: string; role: string; shopName: string };
  workspace: {
    id: string;
    name: string;
    isDemo: boolean;
    canSwitchRole?: boolean;
  };
};
let token = sessionStorage.getItem("toushir-web-token") || "";
export const setToken = (value: string) => {
  token = value;
  sessionStorage.setItem("toushir-web-token", value);
};
export async function api<T = any>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const request = () =>
    fetch("/api" + path, {
      ...options,
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: "Bearer " + token } : {}),
        ...options.headers,
      },
      signal: options.signal || AbortSignal.timeout(path === "/admin/demo/scenario" ? 120000 : 30000),
    });
  let response = await request();
  if (response.status === 401 && token.includes(".") && !options.headers) {
    const next = await renewIdentityToken();
    if (next) {
      setToken(next);
      response = await request();
    }
  }
  const body = response.headers.get("content-type")?.includes("json")
    ? await response.json()
    : null;
  if (!response.ok)
    throw new Error(
      body?.error?.message ||
        "Cette opération n’a pas abouti. Vérifiez votre connexion ou reconnectez-vous.",
    );
  return body;
}
export const post = <T = any>(path: string, body: unknown = {}) =>
  api<T>(path, { method: "POST", body: JSON.stringify(body) });
export const patch = <T = any>(path: string, body: unknown) =>
  api<T>(path, { method: "PATCH", body: JSON.stringify(body) });
export const put = <T = any>(path: string, body: unknown) =>
  api<T>(path, { method: "PUT", body: JSON.stringify(body) });
export const key = () => crypto.randomUUID();
export const money = (v: number | null | undefined) =>
  v == null
    ? "Sur devis"
    : new Intl.NumberFormat("fr-DZ", { maximumFractionDigits: 2 }).format(v) +
      " DA";
export const number = (v: number) =>
  new Intl.NumberFormat("fr-DZ").format(v || 0);
export const date = (v: string, withTime = false) =>
  v
    ? new Intl.DateTimeFormat("fr-DZ", {
        timeZone: "Africa/Algiers",
        day: "numeric",
        month: "short",
        ...(withTime ? { hour: "2-digit", minute: "2-digit" } : {}),
      }).format(new Date(v))
    : "—";
export const labels: Record<string, string> = {
  REQUESTED: "Demande reçue",
  QUOTED: "Accord attendu",
  SUBMITTED: "À confirmer",
  CONFIRMED: "Confirmée",
  PREPARING: "En préparation",
  READY: "Prête à charger",
  OUT_FOR_DELIVERY: "En livraison",
  DELIVERED: "Livrée",
  PARTIALLY_DELIVERED: "Réception partielle",
  DELIVERY_REFUSED: "Livraison refusée",
  CANCELLED: "Annulée",
  REJECTED: "Refusée",
  DECLINED: "Offre refusée",
  UNPAID: "À encaisser",
  PARTIAL: "Paiement partiel",
  COLLECTED: "Encaissé",
  RECONCILED: "Rapproché",
  REFUND_DUE: "Remboursement dû",
  APPROVED: "Approuvé",
  PENDING: "À vérifier",
  OPEN: "Ouvert",
  ACCEPTED: "Accepté",
  EN_ROUTE: "En route",
  ARRIVED: "Sur place",
  ASSIGNED: "Affecté",
  PICKED_UP: "Sous garde",
};
export function connect(
  onUpdate: () => void,
  onState: (state: string) => void,
) {
  const connection = new HubConnectionBuilder()
    .withUrl("/hubs/commerce", { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Error)
    .build();
  for (const event of [
    "order.updated",
    "tracking.updated",
    "operations.updated",
    "engagement.updated",
  ])
    connection.on(event, onUpdate);
  connection.onreconnecting(() => onState("Reconnexion"));
  connection.onreconnected(() => {
    onState("En direct");
    onUpdate();
  });
  connection.onclose(() => onState("Actualisation 15 s"));
  connection
    .start()
    .then(() => onState("En direct"))
    .catch(() => onState("Actualisation 15 s"));
  return () => {
    void connection.stop();
  };
}

export async function openDocument(path: string) {
  const target = window.open("about:blank", "_blank");
  try {
    const response = await fetch("/api" + path, {
      credentials: "include",
      headers: token ? { Authorization: "Bearer " + token } : {},
      signal: AbortSignal.timeout(30000),
    });
    if (!response.ok) throw new Error("Document indisponible pour cet accès.");
    const blob = new Blob([await response.text()], { type: "text/html" });
    const url = URL.createObjectURL(blob);
    if (target) {
      target.opener = null;
      target.location.href = url;
    } else {
      const a = document.createElement("a");
      a.href = url;
      a.download = "commande-toushir.html";
      a.click();
    }
    setTimeout(() => URL.revokeObjectURL(url), 300000);
  } catch (error) {
    target?.close();
    throw error;
  }
}
