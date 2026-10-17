using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public static class MerchantAccountSummary
{
    public static async Task<object> Read(WholesaleDb db, Actor actor)
    {
        actor.RequireRoles("MERCHANT");
        var orders = db.Orders.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.AccountId == actor.Id);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Algiers");
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var monthStart = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, local.Offset);
        var nextMonth = monthStart.AddMonths(1);
        // Filter by recorded payment date, never by order date or delivery status.
        // Materialize the scoped month for identical decimal aggregation on SQLite and PostgreSQL.
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.WorkspaceId == actor.WorkspaceId && orders.Any(o => o.Id == x.OrderId))
            .ToListAsync();
        var month = payments.Where(x => x.At >= monthStart && x.At < nextMonth).ToList();
        var collected = month.Where(x => !x.IsRefund).Sum(x => x.Amount);
        var refunded = month.Where(x => x.IsRefund).Sum(x => x.Amount);
        return new
        {
            deliveredOrders = await orders.CountAsync(x => x.Status == "DELIVERED"),
            partiallyDeliveredOrders = await orders.CountAsync(x => x.Status == "PARTIALLY_DELIVERED"),
            orderCount = await orders.CountAsync(),
            collectedThisMonth = collected,
            refundedThisMonth = refunded,
            netPaidThisMonth = collected - refunded,
            month = monthStart.ToString("yyyy-MM"),
            timezone = "Africa/Algiers"
        };
    }
}
