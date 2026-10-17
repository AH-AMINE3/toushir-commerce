using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Only invalidation notices cross the live channel. Clients refetch authorized API projections.</summary>
public sealed class RealtimeChanges
{
    private readonly HashSet<(string Workspace, string Order, string Merchant)> orders = [];
    private readonly HashSet<(string Workspace, string Driver)> drivers = [];
    public void Order(string workspace, string order, string merchant) => orders.Add((workspace, order, merchant));
    public void Driver(string workspace, string driver) => drivers.Add((workspace, driver));
    public async Task Publish(IHubContext<CommerceHub> hub, WholesaleDb db, ILogger logger)
    {
        try
        {
            foreach (var item in orders)
            {
                var notice = new { orderId = item.Order, at = DateTimeOffset.UtcNow };
                await LiveAudience.Send(db, hub, item.Workspace, a => a.IsStaff, "operations.updated", notice);
                await LiveAudience.Send(db, hub, item.Workspace, a => a.Role == "MERCHANT" && a.ResourceOwnerId == item.Merchant, "order.updated", notice);
                var driver = await db.DeliveryAssignments.AsNoTracking().Where(x => x.WorkspaceId == item.Workspace && x.OrderId == item.Order).Select(x => x.DriverId).FirstOrDefaultAsync();
                if (driver != null) await LiveAudience.Send(db, hub, item.Workspace, a => a.Role == "DRIVER" && a.Id == driver, "order.updated", notice);
            }
            foreach (var item in drivers)
            {
                var assignments = await db.DeliveryAssignments.AsNoTracking().Where(x => x.WorkspaceId == item.Workspace && x.DriverId == item.Driver).ToListAsync();
                await LiveAudience.Send(db, hub, item.Workspace, a => a.IsStaff, "tracking.updated", new { driverId = item.Driver, at = DateTimeOffset.UtcNow });
                foreach (var assignment in assignments)
                {
                    var merchant = await db.Orders.AsNoTracking().Where(x => x.Id == assignment.OrderId && x.WorkspaceId == item.Workspace).Select(x => x.AccountId).FirstOrDefaultAsync();
                    if (merchant != null) await LiveAudience.Send(db, hub, item.Workspace, a => a.Role == "MERCHANT" && a.ResourceOwnerId == merchant, "tracking.updated", new { orderId = assignment.OrderId, at = DateTimeOffset.UtcNow });
                }
            }
        }
        catch (Exception error) { logger.LogWarning(error, "Live invalidation could not be delivered; committed state remains available through the API."); }
        finally { orders.Clear(); drivers.Clear(); }
    }
}

[Authorize]
public sealed class CommerceHub(WholesaleDb db) : Hub
{
    private async Task<Actor> ActiveActor()
        => await ShopAccess.SessionActor(db, Context.User?.FindFirstValue("sid"))
            ?? throw new HubException("Session ou accès au magasin expiré.");
    public override async Task OnConnectedAsync()
    {
        var actor = await ActiveActor();
        await Groups.AddToGroupAsync(Context.ConnectionId, LiveAudience.Group(Context.User!.FindFirstValue("sid")!, actor));
        await base.OnConnectedAsync();
    }
    public async Task SubscribeOrder(string orderId)
    {
        var actor = await ActiveActor();
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId && x.WorkspaceId == actor.WorkspaceId);
        var allowed = order != null && (actor.IsStaff || actor.Role == "MERCHANT" && order.AccountId == actor.ResourceOwnerId || actor.Role == "DRIVER" && await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId && x.DriverId == actor.Id));
        if (!allowed) throw new HubException("Commande introuvable.");
        // Account/session notices already cover authorized orders. No durable
        // order group may outlive revocation or reassignment.
    }
    public async Task UnsubscribeOrder(string orderId)
    {
        var actor = await ActiveActor();
        _ = orderId;
    }
}
