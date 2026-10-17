using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public class CatalogService(WholesaleDb db, IConfiguration config, WarehouseService warehouse, PricingService pricing, PromotionsService promotions)
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
    private static int DisplayQuantity(Offer? offer)
    {
        var minimum = Math.Max(1, offer?.MinimumQuantity ?? 1);
        var pack = Math.Max(1, offer?.CaseSize ?? 1);
        return checked((minimum + pack - 1) / pack * pack);
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

    public static object ProductView(Product p, Offer? o, bool admin = false, int? eligibleStock = null, PricingDecision? pricing = null)
    {
        var price = pricing == null ? o?.OfferApproved == true && o.IsActive ? o.Price : null : pricing.UnitPrice;
        return new { p.Id, p.Barcode, p.Name, p.Brand, p.Category, p.ImageUrl, p.Unit,
            caseSize = o?.OfferApproved == true ? o.CaseSize : (int?)null, minimumQuantity = o?.MinimumQuantity ?? 1,
            price, stock = eligibleStock ?? (o?.OnHand is int h ? Math.Max(0, h - o.Reserved) : (int?)null),
            lotManaged = eligibleStock.HasValue, stockBasis = eligibleStock.HasValue ? "UNEXPIRED_LOTS" : "CONFIRMED_AGGREGATE",
            pricingMode = price != null ? "FIXED" : "ON_REQUEST", pricing,
            offerApproved = o?.OfferApproved ?? false, isActive = o?.IsActive ?? true,
            source = p.Source, sourceUrl = p.SourceUrl, sourceLicense = p.SourceLicense, imageLicense = p.ImageLicense,
            countryTags = CountryTags(p.CountryTags), physicalStock = admin ? o?.OnHand : null,
            savedPrice = admin ? o?.Price : null, savedCaseSize = admin ? o?.CaseSize : null,
            quarantined = admin ? o?.Quarantined ?? 0 : 0, reserved = admin ? o?.Reserved ?? 0 : 0 };
    }
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
        var book = await pricing.LoadBook(a.WorkspaceId, a.Role == "MERCHANT" ? a.Id : null);
        var decisions = filtered.ToDictionary(p => p.Id, p => book.Resolve(p.Id, DisplayQuantity(offers.GetValueOrDefault(p.Id)), offers.GetValueOrDefault(p.Id)));
        var ranks = sort == "featured" ? FeaturedRanks() : new Dictionary<string, int>();
        filtered = sort switch
        {
            "featured" => filtered.OrderBy(p => ranks.GetValueOrDefault(p.Barcode, int.MaxValue)).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            "price_asc" => filtered.OrderBy(p => decisions[p.Id].UnitPrice ?? decimal.MaxValue).ThenBy(p => p.Name),
            "price_desc" => filtered.OrderByDescending(p => decisions[p.Id].UnitPrice ?? -1).ThenBy(p => p.Name),
            "newest" => filtered.OrderByDescending(p => p.CreatedAt),
            _ => filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        };
        var array = filtered.ToList();
        return new
        {
            items = array.Skip((page - 1) * size).Take(size).Select(p => ProductView(p, offers.GetValueOrDefault(p.Id), admin, eligible.TryGetValue(p.Id, out var stock) ? stock : null, decisions[p.Id])),
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
        var book = await pricing.LoadBook(a.WorkspaceId, a.Role == "MERCHANT" ? a.Id : null);
        return ProductView(p!, o, a.IsAdmin, await warehouse.EligibleAvailable(a.WorkspaceId, id), book.Resolve(id, DisplayQuantity(o), o));
    }
    public async Task<object> Categories() => (await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).Select(p => p.Category).ToListAsync()).GroupBy(x => x).OrderBy(x => x.Key).Select(x => new { id = x.Key, name = x.Key, count = x.Count() }).ToArray();
    public async Task<object> Brands() => (await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).Select(p => p.Brand).ToListAsync()).Where(x => x.Length > 0).GroupBy(x => x).OrderBy(x => x.Key).Select(x => new { name = x.Key, count = x.Count() }).ToArray();
    public static int Page(string? raw, int fallback) => int.TryParse(raw, out var n) && n > 0 ? Math.Min(n, 100000) : fallback;
    public static List<LineInput> ValidateLines(List<LineInput>? input)
    {
        Check.That(input is { Count: > 0 and <= 200 }, "Sélectionnez de 1 à 200 produits.");
        Check.That(input!.All(x => x != null && !string.IsNullOrWhiteSpace(x.ProductId) && x.Quantity is > 0 and <= 100000), "Quantité invalide (1 à 100 000 unités).");
        Check.That(input!.Select(x => x.ProductId).Distinct().Count() == input!.Count, "Un produit ne peut apparaître qu'une seule fois.");
        return input!.OrderBy(x => x.ProductId).ToList();
    }
    public async Task<object> Quote(Actor actor, List<LineInput>? requested) => (await BuildQuote(actor, requested)).View;

    public async Task<object> SimulatePricing(Actor actor, SimulatePricingInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE", "MERCHANT");
        var merchantId = actor.Role == "MERCHANT" ? actor.Id : Check.Text(input.MerchantId, 100, "Magasin");
        if (actor.Role == "MERCHANT") Check.That(string.IsNullOrWhiteSpace(input.MerchantId) || input.MerchantId == actor.Id,
            "Magasin introuvable.", "NOT_FOUND", 404);
        var lines = ValidateLines(input.Items);
        var at = input.At?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        Check.That(at > DateTimeOffset.UtcNow.AddDays(-1) && at < DateTimeOffset.UtcNow.AddYears(5), "La simulation doit rester dans la période autorisée.");
        var plan = await BuildQuote(actor, lines, merchantId, at, issueConsent: false);
        return new { items = lines.Select((line, index) => new { line.ProductId, productName = plan.Products[line.ProductId].Name,
                line.Quantity, price = plan.Prices.Lines[line.ProductId].Decision,
                unitPrice = plan.View.Items[index].UnitPrice, lineTotal = plan.View.Items[index].LineTotal,
                promotion = plan.Promotions.GetValueOrDefault(line.ProductId) }),
            quote = plan.View, at, currency = "DZD", monetaryUnit = "CENTIME_FOR_PRICE_DECISION", quantityUnit = "UNIT",
            notice = "Simulation selon les règles actuellement publiées. Les stocks et les frais sont ceux connus aujourd’hui. Aucune réservation ni confirmation de commande." };
    }

    public async Task<CartPricingPlan> BuildQuote(Actor actor, List<LineInput>? requested,
        string? merchantId = null, DateTimeOffset? at = null, bool issueConsent = true)
    {
        if (actor.Role == "MERCHANT") Check.That(merchantId == null || merchantId == actor.Id, "Magasin introuvable.", "NOT_FOUND", 404);
        else if (merchantId != null) actor.RequireRoles("ADMIN", "FINANCE");
        var shopId = actor.Role == "MERCHANT" ? actor.Id : merchantId;
        var lines = requested ?? [];
        if (lines.Count == 0)
        {
            var emptyAt = DateTimeOffset.UtcNow;
            return new(new([], null, null, null, true, false, "DZD", null, emptyAt),
                new Dictionary<string, Product>(), new Dictionary<string, Offer>(),
                new([], new Dictionary<string, PricingLinePlan>(), new HashSet<string>(), emptyAt),
                new Dictionary<string, CampaignPricePreview>(), "");
        }
        ValidateLines(lines);
        var eligible = await EligibleStocks(actor);
        var ids = lines.Select(x => x.ProductId).ToList();
        var products = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        Check.That(products.Count == lines.Count, "Un produit du panier est introuvable.", "PRODUCT_NOT_FOUND", 404);
        var offers = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);
        var book = await pricing.LoadBook(actor.WorkspaceId, shopId, at);
        var plans = new Dictionary<string, PricingLinePlan>();
        var known = new List<QuoteLineInput>();
        var blocked = new HashSet<string>();
        foreach (var line in lines)
        {
            var offer = offers.GetValueOrDefault(line.ProductId);
            var decision = book.Resolve(line.ProductId, line.Quantity, offer);
            var campaignsAllowed = decision.UnitPrice != null && (decision.RuleId.Length == 0 || decision.AllowCampaigns);
            if (!campaignsAllowed) blocked.Add(line.ProductId);
            plans[line.ProductId] = new(line.ProductId, line.Quantity, decision, decision.UnitPrice ?? 0,
                decision.Source, campaignsAllowed, "", System.Text.Json.JsonSerializer.Serialize(new { decision, terms = book.Terms(decision.RuleId) }));
            if (decision.UnitPrice is decimal price) known.Add(new(line.ProductId, line.Quantity, price, offer?.CaseSize ?? 1, null));
        }
        var preview = (await promotions.Preview(actor.WorkspaceId, known, blocked, book.At)).ToDictionary(x => x.ProductId);
        var giftIds = preview.Values.Where(x => x.Kind == "FREE_GOODS" && x.GiftQuantity > 0).Select(x => x.GiftProductId).Distinct().ToList();
        var activeGifts = await db.Products.AsNoTracking().Where(x => giftIds.Contains(x.Id) && x.IsCatalogActive).ToDictionaryAsync(x => x.Id, x => x.Name);
        preview = preview.ToDictionary(x => x.Key, x => x.Value with { GiftProductName = activeGifts.GetValueOrDefault(x.Value.GiftProductId) ?? "" });
        var giftOffers = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && giftIds.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);
        var giftDemand = preview.Values.Where(x => x.Kind == "FREE_GOODS" && x.GiftQuantity > 0)
            .GroupBy(x => x.GiftProductId).ToDictionary(x => x.Key, x => x.Sum(g => (long)g.GiftQuantity)
                + lines.Where(l => l.ProductId == x.Key).Sum(l => (long)l.Quantity));
        var items = new List<CartPricingItem>();
        var commercialTerms = new List<CheckoutLineTerms>();
        decimal subtotal = 0;
        var complete = true;
        foreach (var line in lines)
        {
            var offer = offers.GetValueOrDefault(line.ProductId);
            var plan = plans[line.ProductId];
            var promotion = preview.GetValueOrDefault(line.ProductId);
            var available = eligible.TryGetValue(line.ProductId, out var lotStock) ? lotStock
                : offer?.OnHand is int physical ? Math.Max(0, physical - offer.Reserved) : (int?)null;
            var price = promotion?.AppliedUnitPrice ?? plan.Decision.UnitPrice;
            var issues = new List<string>();
            if (offer?.IsActive == false) issues.Add("INDISPONIBLE");
            if (price == null) issues.Add("PRIX_SUR_DEVIS");
            if (offer?.OnHand == null) issues.Add("STOCK_A_CONFIRMER");
            else if (line.Quantity > available) issues.Add("STOCK_INSUFFISANT");
            if (offer?.OfferApproved != true) issues.Add("CONDITIONNEMENT_A_CONFIRMER");
            else if (line.Quantity < offer.MinimumQuantity || offer.CaseSize <= 0 || line.Quantity % offer.CaseSize != 0) issues.Add("CONDITIONNEMENT");
            if (promotion?.Issue.Length > 0) { issues.Add(promotion.Issue); price = null; }
            if (promotion is { Kind: "FREE_GOODS", GiftQuantity: > 0 })
            {
                var gift = giftOffers.GetValueOrDefault(promotion.GiftProductId);
                var availableGift = eligible.TryGetValue(promotion.GiftProductId, out var giftLots) ? giftLots
                    : gift?.OnHand is int giftPhysical ? Math.Max(0, giftPhysical - gift.Reserved) : (int?)null;
                if (!activeGifts.ContainsKey(promotion.GiftProductId) || gift?.IsActive != true || availableGift == null || availableGift < giftDemand[promotion.GiftProductId])
                    issues.Add("CADEAU_A_CONFIRMER");
            }
            if (issues.Count > 0) complete = false;
            subtotal += price * line.Quantity ?? 0;
            items.Add(new(ProductView(products[line.ProductId], offer, false, eligible.TryGetValue(line.ProductId, out var stock) ? stock : null, plan.Decision),
                line.Quantity, price, price * line.Quantity, available, issues, plan.Decision,
                promotion?.CampaignId.Length > 0 ? promotion : null));
            if (price is decimal applied)
                commercialTerms.Add(new(line.ProductId, line.Quantity, offer?.CaseSize ?? 1, offer?.MinimumQuantity ?? 1,
                    plan.Decision.Source, plan.Decision.RuleId, plan.Decision.RuleVersion, PricingService.Minor(plan.BaseUnitPrice),
                    PricingService.Minor(applied), plan.CampaignsAllowed, promotion?.CampaignId ?? "", promotion?.CampaignVersion ?? 0,
                    promotion?.Kind ?? "", promotion?.GiftProductId ?? "", promotion?.GiftQuantity ?? 0));
        }
        var deliveryFee = config.GetValue<decimal?>("Wholesale:DeliveryFee");
        if (deliveryFee == null && config.GetValue<decimal?>("Demo:DeliveryFee") is decimal demoFee
            && await db.Workspaces.AsNoTracking().AnyAsync(x => x.Id == actor.WorkspaceId && x.IsDemo))
            deliveryFee = demoFee;
        if (deliveryFee != null) Check.Money(deliveryFee.Value, "Tarif de livraison configuré");
        complete = complete && deliveryFee != null;
        var canOrder = complete && actor.Role == "MERCHANT" && actor.ApprovalStatus == "APPROVED";
        var fingerprint = complete ? PricingService.Fingerprint(commercialTerms, PricingService.Minor(deliveryFee!.Value)) : "";
        var token = canOrder && issueConsent ? pricing.CreateConsent(actor, actor.Id, fingerprint) : null;
        var hasUnknownPrice = items.Any(x => x.UnitPrice == null);
        var view = new CartPricingView(items, hasUnknownPrice ? null : subtotal, complete ? deliveryFee : null,
            complete ? subtotal + deliveryFee : null, !complete, canOrder, "DZD", token, book.At);
        return new(view, products, offers, new(known, plans, blocked, book.At), preview, fingerprint);
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
        var book = await pricing.LoadBook(a.WorkspaceId, a.Role == "MERCHANT" ? a.Id : null);
        return ps.Select(p => ProductView(p, os.GetValueOrDefault(p.Id), false, eligible.TryGetValue(p.Id, out var stock) ? stock : null,
            book.Resolve(p.Id, DisplayQuantity(os.GetValueOrDefault(p.Id)), os.GetValueOrDefault(p.Id)))).ToArray();
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
