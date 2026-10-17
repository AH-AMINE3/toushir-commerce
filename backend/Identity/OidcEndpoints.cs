using System.Net;
using Microsoft.AspNetCore;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Toushir.Wholesale;

public static class OidcEndpoints
{
    private const string Server = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
    public static void MapCommerceOidc(this WebApplication app)
    {
        app.MapGet("/connect/authorize", Authorize);
        app.MapPost("/connect/token", Exchange);
        app.MapGet("/connect/userinfo", UserInfo);
        app.MapMethods("/connect/logout", ["GET", "POST"], Logout);
        app.MapGet("/auth/login", LoginPage);
        app.MapPost("/auth/login", Login).AddEndpointFilter<MutationFilter>().RequireRateLimiting("auth").DisableAntiforgery();
    }

    private static IResult ProtocolError(string error, string description) => Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?> { [OpenIddictServerAspNetCoreConstants.Properties.Error] = error, [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description }), [Server]);
    private static async Task<IResult> Authorize(HttpContext context, WholesaleDb db)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("OIDC request missing.");
        var authentication = await context.AuthenticateAsync("session");
        if (!authentication.Succeeded)
        {
            if ((request.Prompt ?? "").Split(' ').Contains("none")) return ProtocolError(Errors.LoginRequired, "An interactive sign-in is required.");
            var returnUrl = context.Request.Path + context.Request.QueryString;
            return Results.Redirect("/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        }
        var accountId = authentication.Principal!.FindFirstValue(ClaimTypes.NameIdentifier);
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == accountId);
        if (account == null || account.ApprovalStatus == "SUSPENDED") return ProtocolError(Errors.AccessDenied, "The account is unavailable.");
        var identity = new ClaimsIdentity("oidc", Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, account.Id).SetClaim(Claims.Name, account.Name).SetClaim(Claims.Email, account.Email).SetClaim(Claims.Role, account.Role).SetClaim("workspace", account.WorkspaceId).SetClaim("approval", account.ApprovalStatus).SetClaim("sid", authentication.Principal.FindFirstValue("sid"));
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes()); principal.SetResources("toushir-commerce");
        DestinationsFor(principal);
        return Results.SignIn(principal, authenticationScheme: Server);
    }

    private static void DestinationsFor(ClaimsPrincipal principal)
    {
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name when principal.HasScope(Scopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email when principal.HasScope(Scopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Role or "workspace" or "approval" or "sid" => [Destinations.AccessToken],
            _ => []
        });
    }

    private static async Task<IResult> Exchange(HttpContext context, WholesaleDb db)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("OIDC request missing.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType()) return ProtocolError(Errors.UnsupportedGrantType, "Only authorization code with PKCE and refresh grants are supported.");
        var result = await context.AuthenticateAsync(Server);
        if (!result.Succeeded || result.Principal == null) return ProtocolError(Errors.InvalidGrant, "The grant is unavailable.");
        var principal = result.Principal;
        var subject = principal.GetClaim(Claims.Subject); var sid = principal.GetClaim("sid");
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == subject);
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sid && x.AccountId == subject);
        var actor = account == null ? null : await ShopAccess.Resolve(db, account);
        if (account == null || actor == null || session == null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return ProtocolError(Errors.InvalidGrant, "The account, shop access or sign-in session has expired.");
        principal.SetClaim(Claims.Name, account.Name).SetClaim(Claims.Email, account.Email).SetClaim(Claims.Role, account.Role).SetClaim("workspace", account.WorkspaceId).SetClaim("approval", account.ApprovalStatus);
        DestinationsFor(principal);
        return Results.SignIn(principal, authenticationScheme: Server);
    }

    private static async Task<IResult> UserInfo(HttpContext context, WholesaleDb db)
    {
        var result = await context.AuthenticateAsync("session");
        if (!result.Succeeded) return Results.Unauthorized();
        var id = result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier);
        var account = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == id);
        return Results.Json(new { sub = account.Id, name = account.Name, email = account.Email, role = account.Role, workspace = account.WorkspaceId });
    }

    private static async Task<IResult> Logout(HttpContext context, AuthService auth)
    {
        await auth.Logout(context);
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [Server]);
    }

    private static string ReturnPath(string? path) => path is { Length: <= 8192 } && path.StartsWith("/connect/authorize?", StringComparison.Ordinal) && !path.Contains('\r') && !path.Contains('\n') ? path : "/";
    private static IResult RenderLogin(HttpContext context, IAntiforgery antiforgery, string returnUrl, string? error = null)
    {
        var token = antiforgery.GetAndStoreTokens(context);
        string E(string? value) => HtmlEncoder.Default.Encode(value ?? "");
        var html = "<!doctype html><html lang='fr'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Connexion Toushir</title><style>body{font:16px system-ui;background:#f4f7f5;color:#163731;margin:0;display:grid;place-items:center;min-height:100vh}main{background:white;padding:36px;max-width:380px;width:calc(100% - 90px);border:1px solid #d9e5df;border-radius:16px}h1{font-size:28px;letter-spacing:-1px}label{display:block;margin:20px 0 6px}input{box-sizing:border-box;width:100%;padding:13px;border:1px solid #bfd1c7;border-radius:6px;font:inherit}button{background:#046642;color:white;border:0;padding:14px;width:100%;border-radius:6px;font:inherit;margin-top:24px}.error{color:#a62b22}p{line-height:1.5;color:#526960}</style><main><h1>toushir.</h1><p>Connectez-vous avec votre compte professionnel pour autoriser l’application Toushir.</p>" + (error == null ? "" : "<p class='error'>" + E(error) + "</p>") + "<form method='post' action='/auth/login'><input type='hidden' name='" + E(token.FormFieldName) + "' value='" + E(token.RequestToken) + "'><input type='hidden' name='returnUrl' value='" + E(returnUrl) + "'><label for='email'>E-mail professionnel</label><input id='email' name='email' type='email' autocomplete='username' required><label for='password'>Mot de passe</label><input id='password' name='password' type='password' autocomplete='current-password' required><button>Se connecter</button></form></main></html>";
        context.Response.Headers.CacheControl = "no-store"; context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
        return Results.Content(html, "text/html; charset=utf-8");
    }
    private static IResult LoginPage(HttpContext context, IAntiforgery antiforgery) => RenderLogin(context, antiforgery, ReturnPath(context.Request.Query["returnUrl"]));
    private static async Task<IResult> Login(HttpContext context, IAntiforgery antiforgery, AuthService auth)
    {
        try { await antiforgery.ValidateRequestAsync(context); } catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = new { code = "ANTIFORGERY", message = "Rechargez la page de connexion." } }); }
        var form = await context.Request.ReadFormAsync(); var returnUrl = ReturnPath(form["returnUrl"]);
        try
        {
            await auth.Login(new(form["email"].ToString(), form["password"].ToString()), context);
            // End the same-origin form submission before a fresh GET resumes the
            // authorization flow. Otherwise form-action 'self' also blocks the
            // protocol's registered cross-origin/native callback redirect.
            var destination = HtmlEncoder.Default.Encode(returnUrl);
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            return Results.Content("<!doctype html><html lang='fr'><meta charset='utf-8'><meta http-equiv='refresh' content='0;url=" + destination + "'><title>Connexion Toushir</title><p>Connexion réussie. <a href='" + destination + "'>Continuer vers Toushir</a></p></html>", "text/html; charset=utf-8");
        }
        catch (ApiFailure failure) when (failure.Status == 401) { return RenderLogin(context, antiforgery, returnUrl, "E-mail ou mot de passe incorrect."); }
    }
}
