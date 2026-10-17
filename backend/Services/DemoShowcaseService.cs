using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Explicitly fictional offers for an interactive owner-controlled demo, separate from catalog facts.</summary>
public sealed class DemoShowcaseService(WholesaleDb db, IConfiguration config, EngagementService engagement)
{
    private const string Marker = "DEMO_PRICED_CATALOG_V1";
    public async Task<object> Seed(Actor actor)
    {
        Check.That(config.GetValue<bool>("Demo:Enabled"), "La démonstration n’est pas activée.", "DEMO_DISABLED", 404);
        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actor.WorkspaceId);
        Check.That(workspace?.IsDemo == true && actor.CanSwitchRole,
            "Seul le propriétaire de cet espace de démonstration peut charger les offres fictives.", "DEMO_OWNER_REQUIRED", 403);
        if (await db.Audit.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.Action == Marker))
        {
            await EngagementExamples(actor);
            return new { seeded = false, fictional = true };
        }
        var products = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).ToListAsync();
        var offers = await db.Offers.Where(x => x.WorkspaceId == actor.WorkspaceId).ToDictionaryAsync(x => x.ProductId);
        var managed = await db.LotProductControls.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).Select(x => x.ProductId).ToHashSetAsync();
        var priced = 0;
        foreach (var product in products)
        {
            if (!offers.TryGetValue(product.Id, out var offer))
            {
                offer = new Offer { WorkspaceId = actor.WorkspaceId, ProductId = product.Id };
                db.Offers.Add(offer);
            }
            // Never overwrite an entered price, inactive product, reservation, counted lot or historical order.
            if (offer.Price != null || !offer.IsActive || managed.Contains(product.Id)) continue;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(product.Barcode));
            offer.Price = 50m + (hash[0] % 90) * 5m;
            offer.CaseSize = Math.Max(1, offer.CaseSize);
            offer.MinimumQuantity = Math.Max(offer.CaseSize, offer.MinimumQuantity);
            offer.OfferApproved = true;
            if (offer.OnHand == null)
            {
                offer.OnHand = 1000;
                db.Inventory.Add(new InventoryEntry { WorkspaceId = actor.WorkspaceId, ProductId = product.Id,
                    Kind = "DEMO_OPENING", Delta = 1000, Actor = actor.Name,
                    Reason = "EXEMPLE · stock fictif de test, sans inventaire physique réel." });
            }
            offer.UpdatedAt = DateTimeOffset.UtcNow;
            priced++;
        }
        db.Audit.Add(new AuditEntry { WorkspaceId = actor.WorkspaceId, ActorId = actor.Id, Actor = actor.Name,
            Action = Marker, EntityId = actor.WorkspaceId,
            Details = $"EXEMPLE · {priced} prix fictifs DZD par unité de vente. Aucun prix marché ni disponibilité commerciale réelle. Offres existantes préservées." });
        await db.SaveChangesAsync();
        await EngagementExamples(actor);
        return new { seeded = true, fictional = true, priced };
    }

    private async Task EngagementExamples(Actor owner)
    {
        const string marker = "DEMO_ENGAGEMENT_SHOWCASE_V1";
        if (await db.Audit.AnyAsync(x => x.WorkspaceId == owner.WorkspaceId && x.Action == marker)) return;
        var people = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == owner.WorkspaceId && (x.Role == "ADMIN" || x.Role == "FINANCE")).ToListAsync();
        var admin = people.First(x => x.Role == "ADMIN"); var finance = people.First(x => x.Role == "FINANCE");
        var creator = new Actor(admin.Id, owner.WorkspaceId, admin.Name, "ADMIN", "APPROVED");
        var reviewer = new Actor(finance.Id, owner.WorkspaceId, finance.Name, "FINANCE", "APPROVED");
        var specs = new[] {
            ("Écouteurs sans fil · démo", "/figma/reward-airpods.png", 900),
            ("Machine à café · démo", "/figma/reward-coffee.png", 2400),
            ("Un nouveau téléphone · démo", "/figma/reward-phone.png", 12000)
        };
        var rewards = new List<RewardItem>();
        foreach (var (name, image, cost) in specs) rewards.Add((RewardItem)await engagement.SaveReward(creator, null,
            new(name, "EXEMPLE · dotation et stock fictifs pour explorer le parcours. Aucune livraison de cadeau réel.", image, cost, 20)));
        var references = await db.Offers.AsNoTracking().Where(x => x.WorkspaceId == owner.WorkspaceId && x.OfferApproved && x.IsActive && x.Price > 0 && x.OnHand > x.Reserved)
            .OrderBy(x => x.ProductId).Take(3).Select(x => x.ProductId).ToListAsync();
        if (references.Count >= 3) {
            var now = DateTimeOffset.UtcNow;
            var definitions = new[] {
                new CreateCampaignInput("Votre réassort, 10 % de remise · démo", "EXEMPLE · dès 6 unités de la référence indiquée. Conditions fictives, une seule mécanique par produit.", "DISCOUNT", references[0], 6, 10m, null, 0, 0, 1, null, 20, now.AddMinutes(-1), now.AddDays(14), null),
                new CreateCampaignInput("Un produit en plus · démo", "EXEMPLE · dès 2 unités achetées, un produit offert distinct. Réservation et remise physique du cadeau suivies séparément.", "FREE_GOODS", references[1], 2, 0m, references[2], 1, 0, 1, null, 20, now.AddMinutes(-1), now.AddDays(14), null),
                new CreateCampaignInput("Objectif réassort · démo", "EXEMPLE · 12 unités livrées, acceptées et réglées après publication, avant la fin de la campagne. Attribution distincte après contrôle et fin de période.", "CHALLENGE", references[2], 1, 0m, null, 0, 12, 1, rewards[1].Id, 20, now.AddMinutes(-1), now.AddDays(7), now.AddDays(14))
            };
            foreach (var definition in definitions) {
                var campaign = (Campaign)await engagement.CreateCampaign(creator, definition);
                await engagement.PublishCampaign(reviewer, campaign.Id, new(campaign.Version));
            }
        }
        db.Audit.Add(new() { WorkspaceId = owner.WorkspaceId, ActorId = owner.Id, Actor = owner.Name, Action = marker,
            EntityId = owner.WorkspaceId, Details = "EXEMPLE · catalogue de cadeaux et campagnes fictives créées via les règles normales avec une persona de contrôle indépendante." });
        await db.SaveChangesAsync();
    }
}
