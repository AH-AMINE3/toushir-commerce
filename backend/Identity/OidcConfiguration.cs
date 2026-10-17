using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Toushir.Wholesale;

public static class OidcConfiguration
{
    public static IServiceCollection AddCommerceOidc(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, string dataPath)
    {
        services.AddAntiforgery(options => { options.Cookie.Name = "toushir_antiforgery"; options.Cookie.SameSite = SameSiteMode.Strict; options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; });
        var signing = Certificate(configuration, dataPath, "signing");
        var encryption = Certificate(configuration, dataPath, "encryption");
        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<WholesaleDb>())
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize").SetTokenEndpointUris("/connect/token").SetUserInfoEndpointUris("/connect/userinfo").SetEndSessionEndpointUris("/connect/logout").SetRevocationEndpointUris("/connect/revoke");
                options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().RequireProofKeyForCodeExchange();
                options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, "commerce_api");
                options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(2)).SetAccessTokenLifetime(TimeSpan.FromMinutes(15)).SetRefreshTokenLifetime(TimeSpan.FromHours(8)).SetRefreshTokenReuseLeeway(TimeSpan.Zero);
                options.AddSigningCertificate(signing).AddEncryptionCertificate(encryption).DisableAccessTokenEncryption();
                var issuer = configuration["Oidc:Issuer"] ?? configuration["RENDER_EXTERNAL_URL"];
                if (!string.IsNullOrWhiteSpace(issuer))
                {
                    Check.That(Uri.TryCreate(issuer, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || environment.IsDevelopment() && uri.IsLoopback), "Oidc:Issuer must be HTTPS outside local development.");
                    options.SetIssuer(uri!);
                }
                var aspnet = options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough().EnableUserInfoEndpointPassthrough().EnableEndSessionEndpointPassthrough();
                if (environment.IsDevelopment()) aspnet.DisableTransportSecurityRequirement();
            })
            .AddValidation(options => { options.AddAudiences("toushir-commerce"); options.UseLocalServer(); options.UseAspNetCore(); options.EnableTokenEntryValidation(); });
        services.Configure<OpenIddictServerOptions>(options => options.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
        return services;
    }

    private static X509Certificate2 Certificate(IConfiguration configuration, string dataPath, string kind)
    {
        var configured = configuration[$"Oidc:{kind}CertificateBase64"];
        if (!string.IsNullOrWhiteSpace(configured)) return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(configured), configuration[$"Oidc:{kind}CertificatePassword"], OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
        var directory = Path.Combine(dataPath, "keys"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "oidc-" + kind + ".pfx");
        if (!File.Exists(path))
        {
            using var rsa = RSA.Create(3072);
            var request = new CertificateRequest("CN=Toushir Commerce " + kind, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(kind == "signing" ? X509KeyUsageFlags.DigitalSignature : X509KeyUsageFlags.KeyEncipherment, critical: true));
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        return X509CertificateLoader.LoadPkcs12FromFile(path, password: null, OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
    }

    public static async Task SeedClients(IServiceProvider provider, IConfiguration configuration)
    {
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var web = new OpenIddictApplicationDescriptor { ClientId = "toushir-web", DisplayName = "Toushir Commerce Web", ClientType = ClientTypes.Public, ConsentType = ConsentTypes.Implicit };
        var defaults = "http://127.0.0.1:5860/auth/callback;http://localhost:5860/auth/callback;http://127.0.0.1:5882/auth/callback;http://127.0.0.1:5883/auth/callback;http://127.0.0.1:5173/auth/callback;http://localhost:5173/auth/callback;http://127.0.0.1:8086/auth/callback;http://localhost:8086/auth/callback;http://127.0.0.1:5860/mobile/auth/callback;http://localhost:5860/mobile/auth/callback;http://127.0.0.1:8086/mobile/auth/callback;http://localhost:8086/mobile/auth/callback";
        foreach (var value in (configuration["Oidc:WebRedirectUris"] ?? defaults).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) AddRedirect(web, value);
        var external = configuration["Oidc:Issuer"] ?? configuration["RENDER_EXTERNAL_URL"];
        if (Uri.TryCreate(external, UriKind.Absolute, out var origin) && origin.Scheme == "https") { AddRedirect(web, new Uri(origin, "/auth/callback").AbsoluteUri); AddRedirect(web, new Uri(origin, "/mobile/auth/callback").AbsoluteUri); }
        var mobile = new OpenIddictApplicationDescriptor { ClientId = "toushir-mobile", DisplayName = "Toushir Commerce Mobile", ClientType = ClientTypes.Public, ConsentType = ConsentTypes.Implicit };
        foreach (var value in (configuration["Oidc:MobileRedirectUris"] ?? "toushirgros://auth/callback;toushircommerce://auth/callback;toushirlivreur://auth/callback").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) AddRedirect(mobile, value);
        foreach(var value in (configuration["Oidc:MobileWebRedirectUris"]??"http://127.0.0.1:5860/mobile/auth/callback;http://localhost:5860/mobile/auth/callback;http://127.0.0.1:8086/mobile/auth/callback;http://localhost:8086/mobile/auth/callback").Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))AddRedirect(mobile,value);
        if(Uri.TryCreate(external,UriKind.Absolute,out var mobileOrigin)&&mobileOrigin.Scheme=="https")AddRedirect(mobile,new Uri(mobileOrigin,"/mobile/auth/callback").AbsoluteUri);
        foreach (var client in new[] { web, mobile })
        {
            client.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession, Permissions.Endpoints.Revocation, Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code, Permissions.Scopes.Profile, Permissions.Scopes.Email, Permissions.Prefixes.Scope + "commerce_api"]);
            client.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
            var existing = await manager.FindByClientIdAsync(client.ClientId!);
            if (existing == null) await manager.CreateAsync(client); else await manager.UpdateAsync(existing, client);
        }
    }
    private static void AddRedirect(OpenIddictApplicationDescriptor descriptor, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0 || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback) && !new[]{"toushirgros","toushircommerce","toushirlivreur"}.Contains(uri.Scheme))) throw new InvalidOperationException("OIDC redirect URIs must be exact HTTPS, local-loopback HTTP or the registered native scheme, without fragments/user info.");
        descriptor.RedirectUris.Add(uri); descriptor.PostLogoutRedirectUris.Add(new Uri(uri.GetLeftPart(UriPartial.Authority) + "/"));
    }
}
