using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public class CatalogService(WholesaleDb db, IConfiguration config, WarehouseService warehouse)
{
    private async Task<Dictionary<string, int>> EligibleStocks(Actor a)
    {
        var ids = await db.LotProductControls.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).Select(x => x.ProductId).ToListAsync();
        if (ids.Count == 0)
            return new();
        var lots = await db.WarehouseLots.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.ProductId) && x.OnHand > x.Reserved).ToListAsync();
        var now = DateTimeOffset.UtcNow;
        return ids.ToDictionary(id => id, id => checked((int)lots.Where(x => x.ProductId == id && (x.ExpiresAt == null || x.ExpiresAt > now)).Sum(x => (long)x.OnHand - x.Reserved)));
    }
    private Dictionary<string, int> FeaturedRanks()
    {
        var directory = config["Catalog:Directory"] ?? Path.Combine(Directory.GetCurrentDirectory(), "../catalog");
        var file = Path.Combine(directory, "featured-barcodes.json");
        if (!File.Exists(file))
            return new();
        try
        {
            return (System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadAllText(file)) ?? []).Select((code, index) => (code, index)).DistinctBy(x => x.code).ToDictionary(x => x.code, x => x.index);
        }
        catch (System.Text.Json.JsonException) { return new(); }
    }

    public static object ProductView(Product p, Offer? o, bool admin = false, int? eligibleStock = null) => new { p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl, p.Unit, caseSize = o?.OfferApproved == true ? o.CaseSize : (int?)null, minimumQuantity = o?.MinimumQuantity ?? 1, price = o?.OfferApproved == true && o.IsActive ? o.Price : null, stock = eligibleStock ?? (o?.OnHand is int h ? Math.Max(0, h - o.Reserved) : (int?)null), lotManaged = eligibleStock.HasValue, stockBasis = eligibleStock.HasValue ? "UNEXPIRED_LOTS" : "CONFIRMED_AGGREGATE", pricingMode = o?.OfferApproved == true && o.IsActive && o.Price != null ? "FIXED" : "ON_REQUEST", offerApproved = o?.OfferApproved ?? false, isActive = o?.IsActive ?? true, source = p.Source, sourceUrl = p.SourceUrl, sourceLicense = p.SourceLicense, imageLicense = p.ImageLicense, countryTags = CountryTags(p.CountryTags), physicalStock = admin ? o?.OnHand : null, savedPrice = admin ? o?.Price : null, savedCaseSize = admin ? o?.CaseSize : null, quarantined = admin ? o?.Quarantined ?? 0 : 0, reserved = admin ? o?.Reserved ?? 0 : 0 };
    public static string[] CountryTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        try
        {
            var tags = System.Text.Json.JsonSerializer.Deserialize<string[]>(raw);
            if (tags != null)
                return tags.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToArray();
        }
        catch (System.Text.Json.JsonException) { }
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
    public async Task<object> Products(Actor a, IQueryCollection query, bool admin = false)
    {
        var eligible = await EligibleStocks(a);
        if (admin)
            a.Admin();
        var q = (query["q"].ToString()).Trim().ToLowerInvariant();
        var category = query["category"].ToString();
        var brand = query["brand"].ToString();
        var sort = query["sort"].ToString();
        var page = Page(query["page"], 1);
        var size = Math.Clamp(Page(query["pageSize"], 24), 1, 100);
        var products = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).ToListAsync();
        var offers = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToDictionaryAsync(x => x.ProductId);
        IEnumerable<Product> filtered = products;
        if (q.Length > 0)
            filtered = filtered.Where(p => (p.Name + " " + p.Brand + " " + p.Barcode).ToLowerInvariant().Contains(q));
        if (category.Length > 0)
            filtered = filtered.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (brand.Length > 0)
            filtered = filtered.Where(p => p.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase));
        if (!admin)
            filtered = filtered.Where(p => !offers.TryGetValue(p.Id, out var o) || o.IsActive);
        if (query["inStock"] == "true")
            filtered = filtered.Where(p => offers.TryGetValue(p.Id, out var o) && (eligible.TryGetValue(p.Id, out var available) ? available > 0 : o.OnHand - o.Reserved > 0));
        var ranks = sort == "featured" ? FeaturedRanks() : new Dictionary<string, int>();
        filtered = sort switch
        {
            "featured" => filtered.OrderBy(p => ranks.GetValueOrDefault(p.Barcode, int.MaxValue)).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            "price_asc" => filtered.OrderBy(p => offers.GetValueOrDefault(p.Id)?.OfferApproved == true ? offers[p.Id].Price ?? decimal.MaxValue : decimal.MaxValue).ThenBy(p => p.Name),
            "price_desc" => filtered.OrderByDescending(p => offers.GetValueOrDefault(p.Id)?.OfferApproved == true ? offers[p.Id].Price ?? -1 : -1).ThenBy(p => p.Name),
            "newest" => filtered.OrderByDescending(p => p.CreatedAt),
            _ => filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        };
        var array = filtered.ToList();
        return new
        {
            items = array.Skip((page - 1) * size).Take(size).Select(p => ProductView(p, offers.GetValueOrDefault(p.Id), admin, eligible.TryGetValue(p.Id, out var stock) ? stock : null)),
            total = array.Count,
            page,
            pageSize = size,
            totalPages = (int)Math.Ceiling(array.Count / (double)size)
        };
    }
    public async Task<object> Product(Actor a, string id)
    {
        var p = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).FirstOrDefaultAsync(x => x.Id == id);
        Check.That(p != null, "Produit introuvable.", "NOT_FOUND", 404);
        var o = await db.Offers.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == id);
        Check.That(a.IsAdmin || o?.IsActive != false, "Produit indisponible.", "NOT_FOUND", 404);
        return ProductView(p!, o, a.IsAdmin, await warehouse.EligibleAvailable(a.WorkspaceId, id));
    }
    public async Task<object> Categories() => (await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).Select(p => p.Category).ToListAsync()).GroupBy(x => x).OrderBy(x => x.Key).Select(x => new { id = x.Key, name = x.Key, count = x.Count() }).ToArray();
    public async Task<object> Brands() => (await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).Select(p => p.Brand).ToListAsync()).Where(x => x.Length > 0).GroupBy(x => x).OrderBy(x => x.Key).Select(x => new { name = x.Key, count = x.Count() }).ToArray();
    public static int Page(string? raw, int fallback) => int.TryParse(raw, out var n) && n > 0 ? Math.Min(n, 100000) : fallback;
    public static List<LineInput> ValidateLines(List<LineInput>? input)
    {
        Check.That(input is { Count: > 0 and <= 200 }, "Sélectionnez de 1 à 200 produits.");
        Check.That(input!.All(x => !string.IsNullOrWhiteSpace(x.ProductId) && x.Quantity is > 0 and <= 100000), "Quantité invalide (1 à 100 000 unités).");
        Check.That(input!.Select(x => x.ProductId).Distinct().Count() == input!.Count, "Un produit ne peut apparaître qu'une seule fois.");
        return input!.OrderBy(x => x.ProductId).ToList();
    }
    public async Task<object> Quote(Actor a, List<LineInput>? requested)
    {
        var eligible = await EligibleStocks(a);
        var lines = requested ?? [];
        if (lines.Count == 0)
            return new
            {
                items = Array.Empty<object>(),
                subtotal = (decimal?)null,
                deliveryFee = (decimal?)null,
                total = (decimal?)null,
                needsQuote = true,
                canOrder = false,
                currency = "DZD"
            };
        ValidateLines(lines);
        var ids = lines.Select(x => x.ProductId).ToList();
        var products = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var offers = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);
        decimal subtotal = 0;
        bool needs = false, can = true;
        var result = new List<object>();
        foreach (var l in lines)
        {
            Check.That(products.TryGetValue(l.ProductId, out var p), "Un produit du panier est introuvable.", "PRODUCT_NOT_FOUND", 404);
            var o = offers.GetValueOrDefault(l.ProductId);
            var available = eligible.TryGetValue(l.ProductId, out var lotStock) ? lotStock : o?.OnHand is int h ? Math.Max(0, h - o.Reserved) : (int?)null;
            var price = o?.OfferApproved == true && o.IsActive ? o.Price : null;
            var issues = new List<string>();
            if (o?.IsActive == false)
                issues.Add("INDISPONIBLE");
            if (price == null)
            {
                issues.Add("PRIX_SUR_DEVIS");
                needs = true;
            }
            if (o?.OnHand == null)
                issues.Add("STOCK_A_CONFIRMER");
            else if (l.Quantity > available)
                issues.Add("STOCK_INSUFFISANT");
            if (o?.OfferApproved == true && (l.Quantity < o.MinimumQuantity || l.Quantity % o.CaseSize != 0))
                issues.Add("CONDITIONNEMENT");
            if (issues.Count > 0)
                can = false;
            decimal? lineTotal = price * l.Quantity;
            subtotal += lineTotal ?? 0;
            result.Add(new
            {
                product = ProductView(p!, o, false, eligible.TryGetValue(l.ProductId, out var physical) ? physical : null),
                quantity = l.Quantity,
                unitPrice = price,
                lineTotal,
                available,
                issues
            });
        }
        var deliveryFee = config.GetValue<decimal?>("Wholesale:DeliveryFee");
        if (deliveryFee != null)
            Check.Money(deliveryFee.Value, "Tarif de livraison configuré");
        var complete = !needs && can && deliveryFee != null;
        return new
        {
            items = result,
            subtotal = needs ? (decimal?)null : subtotal,
            deliveryFee = complete ? deliveryFee : null,
            total = complete ? subtotal + deliveryFee : null,
            needsQuote = !complete,
            canOrder = complete && a.ApprovalStatus == "APPROVED",
            currency = "DZD"
        };
    }
    public async Task<object> Cart(Actor a) => await Quote(a, await db.CartItems.Where(x => x.AccountId == a.Id).Select(x => new LineInput(x.ProductId, x.Quantity)).ToListAsync());
    public async Task<object> SaveCart(Actor a, CartInput input)
    {
        Check.That(input.Items != null && input.Items.Count <= 200, "Panier invalide.");
        if (input.Items!.Count > 0)
        {
            ValidateLines(input.Items);
            await Quote(a, input.Items);
        }
        await db.CartItems.Where(x => x.AccountId == a.Id).ExecuteDeleteAsync();
        db.CartItems.AddRange(input.Items.Select(x => new CartItem { AccountId = a.Id, ProductId = x.ProductId, Quantity = x.Quantity }));
        await db.SaveChangesAsync();
        return await Cart(a);
    }
    public async Task<object> Favorites(Actor a)
    {
        var eligible = await EligibleStocks(a);
        var ids = await db.Favorites.Where(x => x.AccountId == a.Id).Select(x => x.ProductId).ToListAsync();
        var ps = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).Where(x => ids.Contains(x.Id)).ToListAsync();
        var os = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);
        return ps.Select(p => ProductView(p, os.GetValueOrDefault(p.Id), false, eligible.TryGetValue(p.Id, out var stock) ? stock : null)).ToArray();
    }
    public async Task Favorite(Actor a, string id, bool add)
    {
        Check.That(await db.Products.AnyAsync(x => x.Id == id && x.IsCatalogActive), "Produit introuvable.", "NOT_FOUND", 404);
        var f = await db.Favorites.FirstOrDefaultAsync(x => x.AccountId == a.Id && x.ProductId == id);
        if (add && f == null)
            db.Favorites.Add(new()
            {
                AccountId = a.Id,
                ProductId = id
            });
        else if (!add && f != null)
            db.Favorites.Remove(f);
        await db.SaveChangesAsync();
    }
}
