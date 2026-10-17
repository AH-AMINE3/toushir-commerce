using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class ShopMemberService(WholesaleDb db, AuthService auth, PasswordHasher<Account> passwords)
{
    private static void Version(int actual, int expected) => Check.That(actual == expected,
        "Cet accès a changé. Rechargez l'équipe.", "VERSION_CONFLICT", 409);
    private static string MemberRole(string? value)
    {
        var role = Check.Text(value, 20, "Rôle").ToUpperInvariant();
        Check.That(role is "MANAGER" or "BUYER" or "VIEWER", "Rôle de magasin inconnu.");
        return role;
    }
    private static void MayManage(Actor actor, string role)
    {
        actor.ManageShop();
        Check.That(actor.ShopRole == "OWNER" || role is "BUYER" or "VIEWER",
            "Seul le propriétaire peut gérer un responsable.", "OWNER_REQUIRED", 403);
    }
    private void Event(Actor actor, string kind, string subject, object details)
    {
        var at = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(details);
        db.ShopAccessEvents.Add(new() { WorkspaceId = actor.WorkspaceId, ShopId = actor.ResourceOwnerId,
            ActorId = actor.Id, Kind = kind, SubjectId = subject, Details = json, At = at, UtcTicks = at.UtcTicks });
        db.Audit.Add(new() { WorkspaceId = actor.WorkspaceId, ActorId = actor.Id, Actor = actor.Name,
            Action = kind, EntityId = subject, Details = json });
        db.EngagementNotices.Add(new() { WorkspaceId = actor.WorkspaceId, MerchantId = actor.ResourceOwnerId,
            Kind = kind, AggregateId = subject });
    }
    private async Task LockShop(string workspace)
    {
        // Anonymous acceptance has the identity lock; it must also serialize
        // with shop revocation and role changes before validating the token.
        if (db.Database.IsNpgsql())
            foreach (var key in MutationLocks.Keys(db.Model.GetDefaultSchema()!, workspace, "/api/shop/team"))
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
    }
    public async Task<object> Team(Actor actor)
    {
        actor.RequireRoles("MERCHANT");
        var shop = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == actor.ResourceOwnerId && x.WorkspaceId == actor.WorkspaceId);
        var members = await db.ShopMemberships.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shop.Id).ToListAsync();
        var ids = members.Select(x => x.AccountId).ToList();
        var people = await db.Accounts.AsNoTracking().Where(x => ids.Contains(x.Id) && x.WorkspaceId == actor.WorkspaceId).ToDictionaryAsync(x => x.Id);
        var invitations = actor.CanManageShop ? await db.ShopMemberInvitations.AsNoTracking()
            .Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shop.Id).ToListAsync() : [];
        var events = actor.CanManageShop ? await db.ShopAccessEvents.AsNoTracking()
            .Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shop.Id).OrderByDescending(x => x.UtcTicks).Take(60).ToListAsync() : [];
        var validUntil = await ShopAccess.ValidUntil(db, shop);
        return new { shop = new { shop.Id, name = shop.ShopName, validUntil = validUntil == DateTimeOffset.MaxValue ? null : validUntil }, actor = new { actor.Id, role = actor.ShopRole,
            canManage = actor.CanManageShop, canInviteManager = actor.ShopRole == "OWNER" },
            owner = new { shop.Id, shop.Name, role = "OWNER" },
            members = members.OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.AccountId,
                name = people.GetValueOrDefault(x.AccountId)?.Name ?? "Compte indisponible",
                email = actor.CanManageShop ? people.GetValueOrDefault(x.AccountId)?.Email : null,
                x.Role, x.Status, x.Version, x.CreatedAt, x.UpdatedAt }),
            invitations = invitations.OrderByDescending(x => x.CreatedAt).Select(InvitationView), events,
            notice = "Chaque personne utilise son propre compte. Les commandes sont partagées, les paniers restent personnels." };
    }
    private static object InvitationView(ShopMemberInvitation x) => new { x.Id, x.Email, x.Role, x.Version, x.CreatedAt,
        x.ExpiresAt, x.AcceptedAt, x.RevokedAt, status = x.RevokedAt != null ? "REVOKED" : x.AcceptedAt != null ? "ACCEPTED" : x.ExpiresAt <= DateTimeOffset.UtcNow ? "EXPIRED" : "PENDING" };
    public async Task<object> Invite(Actor actor, InviteShopMemberInput input)
    {
        var role = MemberRole(input.Role); MayManage(actor, role); actor.Approved();
        var email = Check.Text(input.Email, 254, "E-mail").ToLowerInvariant();
        Check.That(System.Net.Mail.MailAddress.TryCreate(email, out _), "E-mail invalide.");
        Check.That(!await db.Accounts.AnyAsync(x => x.Email == email), "Cette adresse possède déjà un compte. Utilisez une adresse personnelle distincte.", "EMAIL_EXISTS", 409);
        var now = DateTimeOffset.UtcNow;
        Check.That(input.ExpiresAt.Offset == TimeSpan.Zero && input.ExpiresAt > now.AddMinutes(5) && input.ExpiresAt <= now.AddDays(7), "L'invitation doit expirer entre 5 minutes et 7 jours, en UTC.");
        var shop = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == actor.ResourceOwnerId);
        var until = await ShopAccess.ValidUntil(db, shop);
        Check.That(until != null && input.ExpiresAt <= until, "L'invitation ne peut pas dépasser la validité du magasin.", "INVITATION_EXPIRY", 409);
        var pending = await db.ShopMemberInvitations.Where(x => x.ShopId == shop.Id && x.WorkspaceId == actor.WorkspaceId && x.Email == email && x.AcceptedAt == null && x.RevokedAt == null).ToListAsync();
        foreach (var old in pending) { MayManage(actor, old.Role); old.RevokedAt = now; old.RevokedBy = actor.Id; old.Version++; }
        Check.That(await db.ShopMemberships.CountAsync(x => x.ShopId == shop.Id && x.Status == "ACTIVE") < 30,
            "Maximum 30 membres actifs par magasin.");
        Check.That((await db.ShopMemberInvitations.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shop.Id && x.AcceptedAt == null && x.RevokedAt == null).ToListAsync()).Count(x => x.ExpiresAt > now) < 100,
            "Maximum 100 invitations en attente par magasin.");
        var token = Tokens.New();
        var invite = new ShopMemberInvitation { WorkspaceId = actor.WorkspaceId, ShopId = shop.Id, Email = email,
            Role = role, TokenHash = Tokens.Hash(token), CreatedBy = actor.Id, ExpiresAt = input.ExpiresAt };
        db.ShopMemberInvitations.Add(invite); Event(actor, "SHOP_MEMBER_INVITED", invite.Id, new { email, role, input.ExpiresAt });
        await db.SaveChangesAsync();
        return new { invitation = InvitationView(invite), token, joinPath = "/mobile/merchant#team-invite=" + token,
            notice = "Copiez ce lien privé et transmettez-le vous-même. Aucun message externe n'a été envoyé." };
    }
    private async Task<ShopMemberInvitation> Pending(string? token, bool acquireLock)
    {
        Check.That(token is { Length: 64 } && token.All(Uri.IsHexDigit), "Invitation indisponible.", "INVITATION_INVALID", 404);
        var hash = Tokens.Hash(token);
        var invite = await db.ShopMemberInvitations.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash);
        Check.That(invite != null, "Invitation indisponible.", "INVITATION_INVALID", 404);
        if (acquireLock) { await LockShop(invite.WorkspaceId); invite = await db.ShopMemberInvitations.AsNoTracking().FirstAsync(x => x.Id == invite.Id); }
        Check.That(invite.AcceptedAt == null && invite.RevokedAt == null && invite.ExpiresAt > DateTimeOffset.UtcNow,
            "Cette invitation est expirée, utilisée ou révoquée.", "INVITATION_UNAVAILABLE", 409);
        var issuer = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == invite.CreatedBy && x.WorkspaceId == invite.WorkspaceId);
        var actor = issuer == null ? null : await ShopAccess.Resolve(db, issuer);
        Check.That(actor != null && actor.ResourceOwnerId == invite.ShopId && actor.CanManageShop
            && (invite.Role != "MANAGER" || actor.ShopRole == "OWNER") && actor.ApprovalStatus == "APPROVED",
            "L'autorisation à l'origine de cette invitation n'est plus active.", "INVITATION_UNAVAILABLE", 409);
        return invite;
    }
    public async Task<object> Preview(MemberInvitationTokenInput input)
    {
        var invite = await Pending(input.Token, false);
        var shop = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == invite.ShopId);
        return new { shop = shop.ShopName, invite.Email, invite.Role, invite.ExpiresAt,
            notice = "Créez votre compte personnel pour rejoindre ce magasin. Le propriétaire peut révoquer cet accès." };
    }
    public async Task<object> Accept(AcceptShopMemberInput input, HttpContext context)
    {
        var invite = await Pending(input.Token, true);
        Check.That(input.Password is { Length: >= 12 and <= 128 }, "Utilisez un mot de passe de 12 à 128 caractères.");
        Check.That(!await db.Accounts.AnyAsync(x => x.Email == invite.Email), "Cette adresse possède déjà un compte.", "EMAIL_EXISTS", 409);
        Check.That(await db.ShopMemberships.CountAsync(x => x.ShopId == invite.ShopId && x.Status == "ACTIVE") < 30,
            "Le magasin a atteint sa limite de membres.", "MEMBER_LIMIT", 409);
        var account = new Account { WorkspaceId = invite.WorkspaceId, Email = invite.Email,
            Name = Check.Text(input.Name, 100, "Nom"), Phone = Check.Text(input.Phone, 30, "Téléphone"),
            ShopName = "", Role = "MERCHANT", ApprovalStatus = "APPROVED" };
        account.PasswordHash = passwords.HashPassword(account, input.Password);
        var membership = new ShopMembership { WorkspaceId = invite.WorkspaceId, ShopId = invite.ShopId,
            AccountId = account.Id, Role = invite.Role, InvitationId = invite.Id, UpdatedBy = invite.CreatedBy };
        db.Accounts.Add(account); db.ShopMemberships.Add(membership);
        var tracked = await db.ShopMemberInvitations.FirstAsync(x => x.Id == invite.Id);
        Version(tracked.Version, invite.Version); tracked.AcceptedAt = DateTimeOffset.UtcNow; tracked.AcceptedBy = account.Id; tracked.Version++;
        var actor = new Actor(account.Id, account.WorkspaceId, account.Name, "MERCHANT", "APPROVED", false, invite.ShopId, invite.Role, 1);
        Event(actor, "SHOP_MEMBER_JOINED", membership.Id, new { invitationId = invite.Id, accountId = account.Id, invite.Role });
        await db.SaveChangesAsync();
        return await auth.Issue(account, context);
    }
    private async Task<ShopMembership> Member(Actor actor, string id)
    {
        actor.ManageShop();
        var member = await db.ShopMemberships.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId && x.ShopId == actor.ResourceOwnerId);
        Check.That(member != null, "Membre introuvable.", "NOT_FOUND", 404); MayManage(actor, member.Role);
        return member;
    }
    public async Task<object> Update(Actor actor, string id, UpdateShopMemberInput input)
    {
        var member = await Member(actor, id); var role = MemberRole(input.Role); MayManage(actor, role);
        Version(member.Version, input.Version);
        Check.That(member.Status == "ACTIVE", "Cet accès est révoqué.", "MEMBERSHIP_REVOKED", 409);
        var reason = Check.Text(input.Reason, 800, "Motif"); Check.That(reason.Length >= 8, "Précisez le motif du changement.");
        var previous = member.Role; member.Role = role; member.Version++; member.UpdatedBy = actor.Id; member.UpdatedAt = DateTimeOffset.UtcNow;
        await Invalidate(member); Event(actor, "SHOP_MEMBER_ROLE_CHANGED", id, new { previous, role, reason, member.AccountId, member.Version });
        await db.SaveChangesAsync(); return await Team(actor);
    }
    private async Task Invalidate(ShopMembership member)
    {
        await db.Sessions.Where(x => x.AccountId == member.AccountId).ExecuteDeleteAsync();
        await db.ReceiptChallenges.Where(x => x.WorkspaceId == member.WorkspaceId && x.IssuedBy == member.AccountId && x.ConsumedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow));
    }
    public async Task<object> Revoke(Actor actor, string id, RevokeShopMemberInput input)
    {
        var member = await Member(actor, id); Version(member.Version, input.Version);
        Check.That(member.Status == "ACTIVE", "Cet accès est déjà révoqué.", "MEMBERSHIP_REVOKED", 409);
        var reason = Check.Text(input.Reason, 800, "Motif"); Check.That(reason.Length >= 8, "Précisez le motif de la révocation.");
        member.Status = "REVOKED"; member.Version++; member.UpdatedBy = actor.Id; member.UpdatedAt = DateTimeOffset.UtcNow;
        await Invalidate(member); Event(actor, "SHOP_MEMBER_REVOKED", id, new { reason, member.AccountId, member.Version });
        await db.SaveChangesAsync(); return await Team(actor);
    }
    public async Task<object> RevokeInvitation(Actor actor, string id, RevokeShopInvitationInput input)
    {
        actor.ManageShop();
        var invite = await db.ShopMemberInvitations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId && x.ShopId == actor.ResourceOwnerId);
        Check.That(invite != null, "Invitation introuvable.", "NOT_FOUND", 404); MayManage(actor, invite.Role); Version(invite.Version, input.Version);
        Check.That(invite.AcceptedAt == null && invite.RevokedAt == null, "Cette invitation est déjà terminée.", "INVITATION_UNAVAILABLE", 409);
        var reason = Check.Text(input.Reason, 800, "Motif"); Check.That(reason.Length >= 8, "Précisez le motif de la révocation.");
        invite.RevokedAt = DateTimeOffset.UtcNow; invite.RevokedBy = actor.Id; invite.Version++;
        Event(actor, "SHOP_INVITATION_REVOKED", id, new { reason, invite.Role }); await db.SaveChangesAsync(); return await Team(actor);
    }
}
