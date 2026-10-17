import { api, post } from "./api";
import type { Session } from "./api";
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
