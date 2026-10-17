using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Live shop authority, shared by HTTP, OIDC, receipt proofs and live recipients.</summary>
public static class ShopAccess
{
    public static async Task<DateTimeOffset?> ValidUntil(WholesaleDb db, Account account)
    {
        if (account.ApprovalStatus == "SUSPENDED") return null;
        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == account.WorkspaceId);
        if (workspace == null) return null;
        var until = workspace.IsDemo ? workspace.CreatedAt.AddDays(7) : DateTimeOffset.MaxValue;
        var guest = await db.DemoGuests.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == account.Id);
        if (guest != null)
        {
            var invite = await db.DemoInvitations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == guest.InvitationId && x.WorkspaceId == account.WorkspaceId);
            if (invite == null || invite.RevokedAt != null) return null;
            if (invite.ExpiresAt < until) until = invite.ExpiresAt;
        }
        return until > DateTimeOffset.UtcNow ? until : null;
    }

    public static async Task<Actor?> Resolve(WholesaleDb db, Account account, bool ownerCapability = false)
    {
        if (await ValidUntil(db, account) == null) return null;
        var membership = await db.ShopMemberships.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == account.Id);
        if (membership == null)
            return new(account.Id, account.WorkspaceId, account.Name, account.Role, account.ApprovalStatus, ownerCapability);
        if (account.Role != "MERCHANT" || membership.WorkspaceId != account.WorkspaceId || membership.Status != "ACTIVE"
            || membership.Role is not ("MANAGER" or "BUYER" or "VIEWER") || membership.ShopId == account.Id) return null;
        var shop = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == membership.ShopId && x.WorkspaceId == account.WorkspaceId && x.Role == "MERCHANT");
        if (shop == null || await db.ShopMemberships.AnyAsync(x => x.AccountId == shop.Id) || await ValidUntil(db, shop) == null) return null;
        return new(account.Id, account.WorkspaceId, account.Name, "MERCHANT", shop.ApprovalStatus, false,
            shop.Id, membership.Role, membership.Version);
    }

    public static async Task<Actor?> SessionActor(WholesaleDb db, string? sessionId)
    {
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sessionId);
        if (session == null || session.ExpiresAt <= DateTimeOffset.UtcNow) return null;
        var account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == session.AccountId);
        return account == null ? null : await Resolve(db, account, session.OwnerCapability);
    }
}
