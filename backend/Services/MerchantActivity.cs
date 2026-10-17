using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public static class MerchantActivity
{
    public static async Task<object> Read(WholesaleDb db, Actor actor, int days)
    {
        actor.RequireRoles("MERCHANT");
        Check.That(days is 7 or 30 or 90, "Choisissez une période de 7, 30 ou 90 jours.");
        var now = DateTimeOffset.UtcNow;
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Algiers");
        DateOnly Day(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
        var to = Day(now); var from = to.AddDays(1 - days);
        var all = await db.Orders.AsNoTracking().Include(x => x.Items)
            .Where(x => x.WorkspaceId == actor.WorkspaceId && x.AccountId == actor.ResourceOwnerId).ToListAsync();
        var ids = all.Select(x => x.Id).ToList();
        var payments = await db.Payments.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && ids.Contains(x.OrderId)).ToListAsync();
        var orders = all.Where(x => Day(x.CreatedAt) >= from && Day(x.CreatedAt) <= to).ToList();
        bool Active(PurchaseOrder o) => o.Status is "REQUESTED" or "QUOTED" or "SUBMITTED" or "CONFIRMED" or "PREPARING" or "READY" or "OUT_FOR_DELIVERY";
        bool Contract(PurchaseOrder o) => o.Total.HasValue && o.Status is not "REQUESTED" and not "QUOTED" and not "CANCELLED" and not "REJECTED" and not "DECLINED";
        var periodPayments = payments.Where(p => Day(p.At) >= from && Day(p.At) <= to).ToList();
        var paid = periodPayments.Where(x => !x.IsRefund).Sum(x => x.Amount);
        var refunded = periodPayments.Where(x => x.IsRefund).Sum(x => x.Amount);
        var productIds = orders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return new {
            period = new { days, from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"), timezone = "Africa/Algiers" },
            orders = new { total = orders.Count, active = all.Count(Active), delivered = orders.Count(x => x.Status is "DELIVERED" or "PARTIALLY_DELIVERED"), cancelled = orders.Count(x => x.Status is "CANCELLED" or "DECLINED" or "REJECTED") },
            financial = new { orderedTotal = orders.Where(Contract).Sum(x => x.Total ?? 0), paid, refunded, netPaid = paid - refunded,
                outstanding = all.Where(Contract).Sum(x => Math.Max(0, (x.Total ?? 0) - x.Collected)) },
            daily = Enumerable.Range(0, days).Select(i => { var d = from.AddDays(i); return new { date = d.ToString("yyyy-MM-dd"),
                orderCount = orders.Count(o => Day(o.CreatedAt) == d), orderedTotal = orders.Where(o => Contract(o) && Day(o.CreatedAt) == d).Sum(o => o.Total ?? 0),
                paid = periodPayments.Where(p => Day(p.At) == d).Sum(p => p.IsRefund ? -p.Amount : p.Amount) }; }),
            topCategories = orders.Where(Contract).SelectMany(o => o.Items).GroupBy(i => products.GetValueOrDefault(i.ProductId)?.Category ?? "Autres")
                .Select(g => new { name = g.Key, quantity = g.Sum(x => x.Quantity), total = g.Sum(x => x.LineTotal ?? 0) }).OrderByDescending(x => x.total).Take(8),
            recentOrders = all.OrderByDescending(x => x.CreatedAt).Take(12).Select(x => new { x.Id, x.Number, x.Status, x.Total, paidAmount = x.Collected, x.CreatedAt }),
            refreshedAt = now,
            basis = new { orderedTotal = "Montants contractuels actuels des commandes créées pendant la période, hors demandes et annulations.",
                paid = "Encaissements moins remboursements selon la date réelle du règlement, indépendamment de la date de commande.",
                outstanding = "Solde actuel des commandes contractuelles non annulées; le graphique historique ne le fige pas.",
                categories = "Valeur contractuelle actuelle des lignes commandées sur la période; ce graphique n’est pas une mesure de ventes nettes après avoirs." }
        };
    }
}
