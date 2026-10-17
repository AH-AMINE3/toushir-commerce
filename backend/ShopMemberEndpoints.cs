using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public static class ShopMemberEndpoints
{
    public static void MapShopMembers(this RouteGroupBuilder api, RouteGroupBuilder secure)
    {
        api.MapPost("/auth/shop-invitation/preview", async (MemberInvitationTokenInput input, ShopMemberService service) =>
            Results.Ok(await service.Preview(input))).RequireRateLimiting("auth");
        api.MapPost("/auth/shop-invitation/accept", async (AcceptShopMemberInput input, ShopMemberService service, HttpContext context) =>
            Results.Ok(await service.Accept(input, context))).RequireRateLimiting("auth");
        secure.MapGet("/shop/team", async (ShopMemberService service, HttpContext context) => Results.Ok(await service.Team(Actor.From(context))));
        secure.MapPost("/shop/team/invitations", async (InviteShopMemberInput input, ShopMemberService service, HttpContext context) => Results.Ok(await service.Invite(Actor.From(context), input)));
        secure.MapPost("/shop/team/invitations/{id}/revoke", async (string id, RevokeShopInvitationInput input, ShopMemberService service, HttpContext context) => Results.Ok(await service.RevokeInvitation(Actor.From(context), id, input)));
        secure.MapPost("/shop/team/members/{id}/role", async (string id, UpdateShopMemberInput input, ShopMemberService service, HttpContext context) => Results.Ok(await service.Update(Actor.From(context), id, input)));
        secure.MapPost("/shop/team/members/{id}/revoke", async (string id, RevokeShopMemberInput input, ShopMemberService service, HttpContext context) => Results.Ok(await service.Revoke(Actor.From(context), id, input)));
    }
}

public sealed class ShopProfileService(WholesaleDb db, AuthService auth, OrderService orders)
{
    public async Task<object> Read(Actor actor) => await auth.UserContext((await db.Accounts.FindAsync(actor.Id))!, actor);
    public async Task<object> Update(Actor actor, ProfileInput input)
    {
        var user = (await db.Accounts.FindAsync(actor.Id))!;
        var name = Check.Text(input.Name, 100, "Nom"); var phone = Check.Text(input.Phone, 30, "Téléphone");
        var shopName = Check.Text(input.ShopName, 150, "Boutique");
        if (actor.IsShopMember)
        {
            var shop = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == actor.ResourceOwnerId);
            Check.That(shopName == shop.ShopName, "Le nom du magasin doit être modifié par son propriétaire.", "OWNER_REQUIRED", 403);
        }
        else user.ShopName = shopName;
        user.Name = name; user.Phone = phone; orders.Audit(actor, "PROFILE_UPDATED", actor.Id);
        await db.SaveChangesAsync(); return await auth.UserContext(user, actor);
    }
    public async Task<object> Addresses(Actor actor)
    {
        actor.RequireRoles("MERCHANT");
        return await db.Addresses.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.AccountId == actor.ResourceOwnerId).ToListAsync();
    }
    public async Task<object> SaveAddress(Actor actor, string? id, AddressInput input)
    {
        actor.ManageShop();
        var valid = AuthService.MakeAddress(new() { Id = actor.ResourceOwnerId, WorkspaceId = actor.WorkspaceId }, input);
        var address = id == null ? null : await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId && x.AccountId == actor.ResourceOwnerId);
        Check.That(id == null || address != null, "Adresse introuvable.", "NOT_FOUND", 404);
        if (id == null) Check.That(await db.Addresses.CountAsync(x => x.AccountId == actor.ResourceOwnerId) < 20, "Maximum 20 adresses par boutique.");
        if (input.IsDefault) await db.Addresses.Where(x => x.AccountId == actor.ResourceOwnerId && (id == null || x.Id != id))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsDefault, false));
        if (address == null) { address = valid; db.Addresses.Add(address); }
        else { address.Label = valid.Label; address.Wilaya = valid.Wilaya; address.Commune = valid.Commune; address.Street = valid.Street; address.IsDefault = valid.IsDefault; }
        orders.Audit(actor, id == null ? "SHOP_ADDRESS_CREATED" : "SHOP_ADDRESS_UPDATED", address.Id);
        await db.SaveChangesAsync(); return address;
    }
    public async Task DeleteAddress(Actor actor, string id)
    {
        actor.ManageShop();
        var count = await db.Addresses.Where(x => x.Id == id && x.AccountId == actor.ResourceOwnerId && x.WorkspaceId == actor.WorkspaceId).ExecuteDeleteAsync();
        Check.That(count > 0, "Adresse introuvable.", "NOT_FOUND", 404); orders.Audit(actor, "SHOP_ADDRESS_DELETED", id); await db.SaveChangesAsync();
    }
}
