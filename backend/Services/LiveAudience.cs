using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Toushir.Wholesale;

/// <summary>Revalidate live recipients at publication; stale account/order groups carry no data.</summary>
public static class LiveAudience
{
    public static string Group(string sessionId, Actor actor) => "session:" + sessionId + ":" + Tokens.Hash(
        string.Join('\0', actor.Id, actor.WorkspaceId, actor.Role, actor.ApprovalStatus,
            actor.ResourceOwnerId, actor.ShopRole, actor.MembershipVersion.ToString()));

    public static async Task Send(WholesaleDb db, IHubContext<CommerceHub> hub, string workspaceId,
        Func<Actor, bool> permitted, string kind, object payload, CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? transaction = null;
        try
        {
            if (db.Database.IsNpgsql())
            {
                // Share the mutation lock until send has been authorized. A
                // revocation cannot commit between this check and publication.
                if (db.Database.CurrentTransaction == null) transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                foreach (var key in MutationLocks.Keys(db.Model.GetDefaultSchema()!, workspaceId, "/api/shop/team"))
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
            }
            var accounts = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == workspaceId).ToListAsync(cancellationToken);
            var sessions = (await db.Sessions.AsNoTracking().Where(x => db.Accounts.Any(a => a.Id == x.AccountId && a.WorkspaceId == workspaceId))
                .ToListAsync(cancellationToken)).Where(x => x.ExpiresAt > DateTimeOffset.UtcNow).ToList();
            var groups = new List<string>();
            foreach (var account in accounts.Where(a => sessions.Any(s => s.AccountId == a.Id)))
            {
                var actor = await ShopAccess.Resolve(db, account);
                if (actor == null || !permitted(actor)) continue;
                groups.AddRange(sessions.Where(s => s.AccountId == actor.Id).Select(s => Group(s.Id, actor)));
            }
            if (groups.Count > 0) await hub.Clients.Groups(groups).SendAsync(kind, payload, cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
        }
        finally { if (transaction != null) await transaction.DisposeAsync(); }
    }
}
