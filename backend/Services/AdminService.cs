using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public class AdminService(WholesaleDb db, OrderService orders, WarehouseService warehouse)
{
    public async Task<object> Dashboard(Actor a, int days)
    {
        a.Admin();
        days = Math.Clamp(days, 1, 366);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Algiers");
        DateOnly Day(DateTimeOffset x) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x, zone).DateTime);
        var end = Day(DateTimeOffset.UtcNow);
        var start = end.AddDays(1 - days);
        var list = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var payments = await db.Payments.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var users = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.Role == "MERCHANT" && !db.ShopMemberships.Any(m => m.AccountId == x.Id)).ToListAsync();
        var offers = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var period = list.Where(x => Day(x.CreatedAt) >= start).ToList();
        bool Delivered(PurchaseOrder o) => o.Status is "DELIVERED" or "PARTIALLY_DELIVERED";
        var delivered = list.Where(Delivered).ToList();
        var periodDelivered = delivered.Where(x => Day(x.DeliveredAt ?? x.UpdatedAt) >= start).ToList();
        var current = list.Where(x => x.Status is "CONFIRMED" or "PREPARING" or "READY" or "OUT_FOR_DELIVERY").ToList();
        var claims = await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var credits = await db.Credits.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var periodOrderIds = periodDelivered.Select(x => x.Id).ToHashSet();
        var currentPayments = payments.Where(x => Day(x.At) >= start).ToList();
        var recent = list.OrderByDescending(x => x.CreatedAt).Take(8).Select(o => new { o.Id, o.Number, o.Status, o.Total, o.CreatedAt, itemCount = o.Items.Count, shopName = users.FirstOrDefault(x => x.Id == o.AccountId)?.ShopName ?? "Boutique" });
        var daily = Enumerable.Range(0, days).Select(i => { var d = start.AddDays(i); return new { date = d.ToString("yyyy-MM-dd"), orders = period.Count(x => Day(x.CreatedAt) == d), sales = periodDelivered.Where(x => Day(x.DeliveredAt ?? x.UpdatedAt) == d).Sum(x => x.Total ?? 0), collected = payments.Where(x => Day(x.At) == d).Sum(x => x.IsRefund ? -x.Amount : x.Amount) }; }).ToArray();
        var top = periodDelivered.SelectMany(x => x.Items).GroupBy(x => new { x.ProductId, x.Name, x.ImageUrl }).Select(g => new { g.Key.ProductId, g.Key.Name, g.Key.ImageUrl, quantity = g.Sum(x => x.AcceptedQuantity), returnedQuantity = claims.Where(x => periodOrderIds.Contains(x.OrderId) && x.ProductId == g.Key.ProductId).Sum(x => x.ReceivedQuantity), revenue = g.Sum(x => (x.UnitPrice ?? 0) * x.AcceptedQuantity) - credits.Where(x => periodOrderIds.Contains(x.OrderId) && claims.Any(c => c.Id == x.ClaimId && c.ProductId == g.Key.ProductId)).Sum(x => x.Amount) }).OrderByDescending(x => x.revenue).Take(8);
        return new
        {
            summary = new
            {
                requests = list.Count(x => x.Status == "REQUESTED"),
                quotes = list.Count(x => x.Status == "QUOTED"),
                newOrders = period.Count,
                activeOrders = current.Count,
                deliveredOrders = periodDelivered.Count,
                sales = periodDelivered.Sum(x => x.Total ?? 0),
                collected = currentPayments.Sum(x => x.IsRefund ? -x.Amount : x.Amount),
                remaining = list.Where(x => x.Total != null && x.Status is not "CANCELLED" and not "REJECTED" and not "DECLINED").Sum(x => Math.Max(0, (x.Total ?? 0) - x.Collected)),
                merchants = users.Count,
                products = await db.Products.CountAsync(x => x.IsCatalogActive),
                publishedOffers = offers.Count(x => x.OfferApproved && x.IsActive),
                lowStock = offers.Count(x => x.OfferApproved && x.OnHand != null && x.OnHand - x.Reserved <= 10),
                pendingMerchants = users.Count(x => x.ApprovalStatus == "PENDING")
            },
            daily,
            statusCounts = list.GroupBy(x => x.Status).Select(g => new { status = g.Key, count = g.Count() }),
            topProducts = top,
            recentOrders = recent,
            period = new
            {
                from = start.ToString("yyyy-MM-dd"),
                to = end.ToString("yyyy-MM-dd"),
                days,
                timezone = "Africa/Algiers"
            },
            basis = new
            {
                sales = "Commandes livrées selon la date de livraison; montant des unités acceptées et frais contractuels, diminué des avoirs commerciaux explicites.",
                collected = "Encaissements réels moins remboursements selon leur date.",
                requests = "Demandes ouvertes sans réservation.",
                stock = "État actuel; seuls les stocks explicitement confirmés sont comptés."
            }
        };
    }
    public async Task<Offer> GetOffer(Actor a, string id)
    {
        Check.That(await db.Products.AnyAsync(x => x.Id == id && x.IsCatalogActive), "Produit introuvable.", "NOT_FOUND", 404);
        var o = await db.Offers.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == id);
        if (o == null)
        {
            o = new()
            {
                WorkspaceId = a.WorkspaceId,
                ProductId = id
            };
            db.Offers.Add(o);
        }
        return o;
    }
    public async Task ApplyOffer(Actor a, string id, OfferInput x)
    {
        a.Admin();
        var o = await GetOffer(a, id);
        await warehouse.AssertManualStockChange(a.WorkspaceId, id, x.Stock);
        if (x.Price != null)
            Check.Money(x.Price.Value, "Prix");
        Check.That(x.Stock == null || x.Stock >= o.Reserved, "Le stock ne peut pas être inférieur aux réservations existantes.");
        Check.That(x.CaseSize is > 0 and <= 10000 && x.MinimumQuantity > 0 && x.MinimumQuantity <= 100000 && x.MinimumQuantity % x.CaseSize == 0, "Conditionnement ou minimum invalide.");
        Check.That(!x.OfferApproved || (x.Price > 0 && x.Stock != null), "Une offre publiée doit avoir un prix positif et un stock physique confirmé.");
        Check.That(x.Stock != null || o.Reserved == 0, "Un stock réservé ne peut pas redevenir inconnu.");
        var delta = (x.Stock ?? 0) - (o.OnHand ?? 0);
        if (x.Stock != o.OnHand)
            orders.Ledger(a, id, "STOCK_SET", delta: delta, reason: "Stock physique saisi par le grossiste.");
        o.Price = x.Price;
        o.OnHand = x.Stock;
        o.CaseSize = x.CaseSize;
        o.MinimumQuantity = x.MinimumQuantity;
        o.OfferApproved = x.OfferApproved;
        o.IsActive = x.IsActive;
        o.UpdatedAt = DateTimeOffset.UtcNow;
        orders.Audit(a, "OFFER_UPDATED", id, $"Offre approuvée : {x.OfferApproved}; stock confirmé : {x.Stock?.ToString() ?? "inconnu"}.");
    }
    public async Task<object> SetOffer(Actor a, string id, OfferInput input, CatalogService catalog)
    {
        await ApplyOffer(a, id, input);
        await db.SaveChangesAsync();
        return await catalog.Product(a, id);
    }
    public async Task<object> Adjust(Actor a, InventoryInput input, CatalogService catalog)
    {
        a.Admin();
        Check.That(!await warehouse.IsManaged(a.WorkspaceId, input.ProductId), "Ce produit est géré par lots. Utilisez le comptage physique par lot.", "LOT_COUNT_REQUIRED", 409);
        Check.That(input.Delta is >= -1000000 and <= 1000000 && input.Delta != 0, "Variation de stock invalide.");
        var reason = Check.Text(input.Reason, 800, "Motif");
        var o = await GetOffer(a, input.ProductId);
        Check.That(o.OnHand != null || input.Delta > 0, "Confirmez d'abord le stock physique.");
        var next = (o.OnHand ?? 0) + input.Delta;
        Check.That(next >= o.Reserved, "Cet ajustement consommerait le stock réservé.", "STOCK_CONFLICT", 409);
        o.OnHand = next;
        o.UpdatedAt = DateTimeOffset.UtcNow;
        orders.Ledger(a, input.ProductId, "ADJUSTED", delta: input.Delta, reason: reason);
        orders.Audit(a, "INVENTORY_ADJUSTED", input.ProductId, $"{input.Delta}: {reason}");
        await db.SaveChangesAsync();
        return await catalog.Product(a, input.ProductId);
    }
    public async Task<object> Inventory(Actor a, IQueryCollection query)
    {
        a.Admin();
        var id = query["productId"].ToString();
        var list = await db.Inventory.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && (id == "" || x.ProductId == id)).ToListAsync();
        return Paged(list.OrderByDescending(x => x.At).Cast<object>().ToList(), query);
    }
    public static object Paged(List<object> rows, IQueryCollection q)
    {
        var page = CatalogService.Page(q["page"], 1);
        var size = Math.Clamp(CatalogService.Page(q["pageSize"], 25), 1, 100);
        return new
        {
            items = rows.Skip((page - 1) * size).Take(size),
            total = rows.Count,
            page,
            pageSize = size,
            totalPages = (int)Math.Ceiling(rows.Count / (double)size)
        };
    }
    public async Task<object> Merchants(Actor a, IQueryCollection query)
    {
        a.Admin();
        var q = query["q"].ToString().ToLowerInvariant();
        var status = query["status"].ToString();
        var region = query["region"].ToString();
        var users = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.Role == "MERCHANT" && !db.ShopMemberships.Any(m => m.AccountId == x.Id)).ToListAsync();
        var addresses = (await db.Addresses.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync())
            .GroupBy(x => x.AccountId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).First());
        var list = await db.Orders.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        var rows = users.Where(x => status.Length == 0 || x.ApprovalStatus == status)
            .Where(x => region.Length == 0 || string.Equals(addresses.GetValueOrDefault(x.Id)?.Wilaya, region, StringComparison.OrdinalIgnoreCase))
            .Where(x => q.Length == 0 || (x.Name + " " + x.ShopName + " " + x.Email + " " + x.Phone + " " + addresses.GetValueOrDefault(x.Id)?.Wilaya + " " + addresses.GetValueOrDefault(x.Id)?.Commune).ToLowerInvariant().Contains(q))
            .OrderByDescending(x => x.CreatedAt).Select(x => (object)new { x.Id, x.Name, x.ShopName, x.Email, x.Phone, x.ApprovalStatus, x.CreatedAt,
                region = addresses.GetValueOrDefault(x.Id)?.Wilaya ?? "", commune = addresses.GetValueOrDefault(x.Id)?.Commune ?? "",
                orderCount = list.Count(o => o.AccountId == x.Id), totalSpent = list.Where(o => o.AccountId == x.Id && o.Status is "DELIVERED" or "PARTIALLY_DELIVERED").Sum(o => o.Total ?? 0) }).ToList();
        return Paged(rows, query);
    }
    public async Task<object> Approve(Actor a, string id, MerchantInput input)
    {
        a.Admin();
        var status = input.ApprovalStatus.ToUpperInvariant();
        Check.That(status is "APPROVED" or "PENDING" or "SUSPENDED", "Statut de boutique invalide.");
        var user = await db.Accounts.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == a.WorkspaceId && x.Role == "MERCHANT" && !db.ShopMemberships.Any(m => m.AccountId == x.Id));
        Check.That(user != null, "Boutique introuvable.", "NOT_FOUND", 404);
        user!.ApprovalStatus = status;
        orders.Audit(a, "MERCHANT_" + status, id);
        await db.SaveChangesAsync();
        return AuthService.User(user);
    }
    public async Task<object> Audit(Actor a, IQueryCollection query)
    {
        a.Admin();
        var list = await db.Audit.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync();
        return Paged(list.OrderByDescending(x => x.At).Cast<object>().ToList(), query);
    }
    public async Task<object> Import(Actor a, CsvInput input)
    {
        a.Admin();
        Check.That(input.Csv is { Length: > 0 and < 2000000 }, "Le fichier CSV est vide ou dépasse 2 Mo.");
        var rows = ParseCsv(input.Csv);
        Check.That(rows.Count is > 1 and <= 10001, "Importez de 1 à 10 000 lignes.");
        var header = rows[0].Select(x => x.Trim().ToLowerInvariant()).ToArray();
        var required = new[] { "barcode", "price", "stock", "casesize", "minimumquantity", "offerapproved" };
        Check.That(required.All(header.Contains), "En-têtes requis : barcode,price,stock,caseSize,minimumQuantity,offerApproved.");
        string Cell(List<string> row, string name)
        {
            var at = Array.IndexOf(header, name);
            return at >= 0 && at < row.Count ? row[at].Trim() : "";
        }
        var all = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).ToDictionaryAsync(x => x.Barcode);
        var seen = new HashSet<string>();
        var changes = new List<(string, OfferInput)>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace))
                continue;
            var code = Cell(row, "barcode");
            Check.That(all.TryGetValue(code, out var p), $"Ligne {i + 1} : code-barres inconnu ({code}).");
            Check.That(seen.Add(code), $"Ligne {i + 1} : code-barres dupliqué.");
            decimal? price = null;
            int? stock = null;
            var priceText = Cell(row, "price");
            var stockText = Cell(row, "stock");
            if (priceText != "")
            {
                Check.That(decimal.TryParse(priceText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v), $"Ligne {i + 1} : prix invalide.");
                price = v;
            }
            if (stockText != "")
            {
                Check.That(int.TryParse(stockText, out var v), $"Ligne {i + 1} : stock invalide.");
                stock = v;
            }
            var packValid = int.TryParse(Cell(row, "casesize"), out var pack);
            var minValid = int.TryParse(Cell(row, "minimumquantity"), out var min);
            Check.That(packValid && minValid, $"Ligne {i + 1} : conditionnement invalide.");
            Check.That(bool.TryParse(Cell(row, "offerapproved"), out var approved), $"Ligne {i + 1} : offerApproved attend true ou false.");
            changes.Add((p!.Id, new(price, stock, pack, min, approved)));
        }
        foreach (var (id, x) in changes)
            await ApplyOffer(a, id, x);
        orders.Audit(a, "CATALOG_IMPORTED", a.WorkspaceId, $"{changes.Count} offres validées et importées.");
        await db.SaveChangesAsync();
        return new
        {
            imported = changes.Count
        };
    }
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                    quoted = !quoted;
            }
            else if (c == ',' && !quoted)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\n' && !quoted)
            {
                row.Add(cell.ToString().TrimEnd('\r'));
                rows.Add(row);
                row = [];
                cell.Clear();
            }
            else
                cell.Append(c);
        }
        Check.That(!quoted, "CSV invalide : guillemet non fermé.");
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString().TrimEnd('\r'));
            rows.Add(row);
        }
        return rows;
    }
    public async Task<string> ExportOrders(Actor a)
    {
        a.Admin();
        var list = (await db.Orders.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToListAsync()).OrderByDescending(x => x.CreatedAt);
        string C(string s)
        {
            if (s.Length > 0 && "=+-@\t\r".Contains(s[0]))
                s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        var lines = new List<string> { "number,status,createdAt,totalDZD,collectedDZD,paymentStatus" };
        lines.AddRange(list.Select(x => string.Join(",", C(x.Number), C(x.Status), C(x.CreatedAt.ToString("O")), x.Total?.ToString(CultureInfo.InvariantCulture) ?? "", x.Collected.ToString(CultureInfo.InvariantCulture), C(x.PaymentStatus))));
        return string.Join("\r\n", lines);
    }
}
