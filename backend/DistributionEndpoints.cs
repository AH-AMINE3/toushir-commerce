using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toushir.Wholesale;

public sealed record AppDistribution(
    bool Available,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ApiOrigin = null);

public sealed record CommerceDistribution(AppDistribution Merchant, AppDistribution Driver);

public static class DistributionEndpoints
{
    public static void MapCommerceDistribution(this WebApplication app, RouteGroupBuilder api)
    {
        api.MapGet("/distribution", (HttpContext context, IConfiguration configuration) =>
        {
            var configured = configuration["Oidc:Issuer"] ?? configuration["RENDER_EXTERNAL_URL"];
            var expected = string.IsNullOrWhiteSpace(configured)
                ? context.Request.IsHttps ? $"https://{context.Request.Host}" : null
                : configured;
            return Results.Ok(Snapshot(app.Environment.WebRootPath, expected));
        });
        // Existing APKs are served by static files. Missing downloads never reach the SPA fallback.
        app.MapMethods("/downloads/{**path}", ["GET", "HEAD"], (string? path) =>
        {
            if (path is not ("toushir-merchant.apk" or "toushir-driver.apk" or "manifest.json"))
                return Results.NotFound();
            var file = Path.Combine(app.Environment.WebRootPath, "downloads", path);
            if (!File.Exists(file)) return Results.NotFound();
            return path == "manifest.json"
                ? Results.File(file, "application/json")
                : Results.File(file, "application/vnd.android.package-archive", fileDownloadName: path, enableRangeProcessing: true);
        });
    }

    public static CommerceDistribution Snapshot(string webRoot, string? expectedOrigin)
    {
        var unavailable = new AppDistribution(false);
        var origin = HttpsOrigin(expectedOrigin);
        if (origin == null) return new(unavailable, unavailable);
        var directory = Path.Combine(webRoot, "downloads");
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            AppDistribution Application(string name)
            {
                if (manifest.RootElement.ValueKind != JsonValueKind.Object ||
                    !manifest.RootElement.TryGetProperty(name, out var entry) ||
                    entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("apiOrigin", out var api) ||
                    api.ValueKind != JsonValueKind.String ||
                    HttpsOrigin(api.GetString()) != origin ||
                    !File.Exists(Path.Combine(directory, $"toushir-{name}.apk"))) return unavailable;
                return new(true, $"{origin}/downloads/toushir-{name}.apk", origin);
            }
            return new(Application("merchant"), Application("driver"));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(unavailable, unavailable);
        }
    }

    public static string? HttpsOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return null;
        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/').ToLowerInvariant();
    }
}
