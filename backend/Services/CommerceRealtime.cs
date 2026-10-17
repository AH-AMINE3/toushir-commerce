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
                await hub.Clients.Group(CommerceHub.Operations(item.Workspace)).SendAsync("operations.updated", notice);
                await hub.Clients.Group(CommerceHub.Account(item.Workspace, item.Merchant)).SendAsync("order.updated", notice);
                await hub.Clients.Group(CommerceHub.Order(item.Workspace, item.Order)).SendAsync("order.updated", notice);
                var driver = await db.DeliveryAssignments.AsNoTracking().Where(x => x.WorkspaceId == item.Workspace && x.OrderId == item.Order).Select(x => x.DriverId).FirstOrDefaultAsync();
                if (driver != null) await hub.Clients.Group(CommerceHub.Account(item.Workspace, driver)).SendAsync("order.updated", notice);
            }
            foreach (var item in drivers)
            {
                var assignments = await db.DeliveryAssignments.AsNoTracking().Where(x => x.WorkspaceId == item.Workspace && x.DriverId == item.Driver).ToListAsync();
                await hub.Clients.Group(CommerceHub.Operations(item.Workspace)).SendAsync("tracking.updated", new { driverId = item.Driver, at = DateTimeOffset.UtcNow });
                foreach (var assignment in assignments)
                {
                    var merchant = await db.Orders.AsNoTracking().Where(x => x.Id == assignment.OrderId && x.WorkspaceId == item.Workspace).Select(x => x.AccountId).FirstOrDefaultAsync();
                    if (merchant != null) await hub.Clients.Group(CommerceHub.Account(item.Workspace, merchant)).SendAsync("tracking.updated", new { orderId = assignment.OrderId, at = DateTimeOffset.UtcNow });
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
    internal static string Operations(string workspace) => $"workspace:{workspace}:operations";
    internal static string Account(string workspace, string account) => $"workspace:{workspace}:account:{account}";
    internal static string Order(string workspace, string order) => $"workspace:{workspace}:order:{order}";

    private async Task<Actor> ActiveActor()
    {
        var sessionId = Context.User?.FindFirstValue("sid");
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sessionId);
        var account = session == null ? null : await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == session.AccountId);
        if (session == null || session.ExpiresAt <= DateTimeOffset.UtcNow || account == null || account.ApprovalStatus == "SUSPENDED") throw new HubException("Session expirée.");
        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == account.WorkspaceId);
        if (workspace == null || workspace.IsDemo && workspace.CreatedAt.AddDays(7) <= DateTimeOffset.UtcNow) throw new HubException("Espace expiré.");
        var guest = await db.DemoGuests.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == account.Id);
        if (guest != null && !await db.DemoInvitations.AnyAsync(x => x.Id == guest.InvitationId && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow)) throw new HubException("Invitation expirée.");
        return new(account.Id, account.WorkspaceId, account.Name, account.Role, account.ApprovalStatus);
    }
    public override async Task OnConnectedAsync()
    {
        var actor = await ActiveActor();
        await Groups.AddToGroupAsync(Context.ConnectionId, Account(actor.WorkspaceId, actor.Id));
        if (actor.IsStaff) await Groups.AddToGroupAsync(Context.ConnectionId, Operations(actor.WorkspaceId));
        await base.OnConnectedAsync();
    }
    public async Task SubscribeOrder(string orderId)
    {
        var actor = await ActiveActor();
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId && x.WorkspaceId == actor.WorkspaceId);
        var allowed = order != null && (actor.IsStaff || actor.Role == "MERCHANT" && order.AccountId == actor.Id || actor.Role == "DRIVER" && await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId && x.DriverId == actor.Id));
        if (!allowed) throw new HubException("Commande introuvable.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Order(actor.WorkspaceId, orderId));
    }
    public async Task UnsubscribeOrder(string orderId)
    {
        var actor = await ActiveActor();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Order(actor.WorkspaceId, orderId));
    }
}
