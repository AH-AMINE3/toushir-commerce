import { UserManager, WebStorageStateStore } from "oidc-client-ts";
let manager: UserManager | undefined;
export function identity() {
  if (!manager)
    manager = new UserManager({
      authority: location.origin,
      client_id: "toushir-web",
      redirect_uri: location.origin + "/auth/callback",
      post_logout_redirect_uri: location.origin + "/",
      response_type: "code",
      scope: "openid profile email commerce_api offline_access",
      userStore: new WebStorageStateStore({ store: sessionStorage }),
      stateStore: new WebStorageStateStore({ store: sessionStorage }),
      automaticSilentRenew: true,
      monitorSession: false,
      revokeTokensOnSignout: true,
      loadUserInfo: true,
    });
  return manager;
}
let renewing: Promise<string | null> | null = null;
export async function restoreIdentityToken(): Promise<string | null> {
  const user = await identity().getUser();
  if (!user) return null;
  if (!user.expired) return user.access_token;
  return renewIdentityToken();
}
export async function renewIdentityToken(): Promise<string | null> {
  if (!renewing)
    renewing = identity()
      .signinSilent()
      .then((user) => user?.access_token || null)
      .catch(async () => {
        await identity().removeUser();
        return null;
      })
      .finally(() => {
        renewing = null;
      });
  return renewing;
}
export const startIdentityLogin = () => identity().signinRedirect();
export async function finishIdentityLogin() {
  const user = await identity().signinRedirectCallback();
  if (!user?.access_token)
    throw new Error(
      "La connexion n’a pas abouti. Rouvrez la page de connexion.",
    );
  history.replaceState(null, "", "/");
  return user.access_token;
}
export async function clearIdentity() {
  const u = await identity().getUser();
  if (u)
    try {
      await identity().revokeTokens(["access_token", "refresh_token"]);
    } catch {}
  await identity().removeUser();
}
