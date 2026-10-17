using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Contextual communication, campaigns and immutable reward ledger. Mutations run in the parent's database transaction.</summary>
public sealed partial class EngagementService(WholesaleDb db, OrderService orders, WarehouseService warehouse, PricingService pricing)
{
    private static string Hash(object value) => Tokens.Hash(JsonSerializer.Serialize(value));
    private static string Key(string value) => Check.Text(value, 100, "Clé d'idempotence");
    private static void Version(int current, int expected) => Check.That(current == expected, "La version a changé. Actualisez ce dossier.", "VERSION_CONFLICT", 409);
    private void Notice(Actor actor, string merchantId, string kind, string aggregateId) => db.EngagementNotices.Add(new() { WorkspaceId = actor.WorkspaceId, MerchantId = merchantId, Kind = kind, AggregateId = aggregateId });
    private async Task<string> Merchant(Actor actor, string? requested = null)
    {
        var id = actor.Role == "MERCHANT" ? actor.ResourceOwnerId : Check.Text(requested, 100, "Magasin");
        if (actor.Role == "MERCHANT") Check.That(string.IsNullOrWhiteSpace(requested) || requested == id, "Magasin introuvable.", "NOT_FOUND", 404);
        if (actor.Role != "MERCHANT")
            actor.RequireRoles("ADMIN", "FINANCE", "DISPATCHER", "WAREHOUSE");
        Check.That(await db.Accounts.AnyAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId && x.Role == "MERCHANT" && !db.ShopMemberships.Any(m => m.AccountId == x.Id)), "Magasin introuvable.", "NOT_FOUND", 404);
        return id;
    }
    private async Task<Conversation> Thread(Actor actor, string id)
    {
        var thread = await db.Conversations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(thread != null, "Conversation introuvable.", "NOT_FOUND", 404);
        if (actor.Role == "MERCHANT")
            Check.That(thread.MerchantId == actor.ResourceOwnerId, "Conversation introuvable.", "NOT_FOUND", 404);
        else if (actor.Role == "DRIVER")
            Check.That(thread.ContextType == "DELIVERY" && await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == thread.ContextId && x.DriverId == actor.Id), "Conversation introuvable.", "NOT_FOUND", 404);
        else
            actor.RequireRoles("ADMIN", "FINANCE", "DISPATCHER", "WAREHOUSE");
        return thread;
    }
    public async Task<object> Conversations(Actor actor)
    {
        var q = db.Conversations.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId);
        if (actor.Role == "MERCHANT")
            q = q.Where(x => x.MerchantId == actor.ResourceOwnerId);
        else if (actor.Role == "DRIVER")
            q = q.Where(x => x.ContextType == "DELIVERY" && db.DeliveryAssignments.Any(a => a.WorkspaceId == actor.WorkspaceId && a.OrderId == x.ContextId && a.DriverId == actor.Id));
        else
            actor.RequireRoles("ADMIN", "FINANCE", "DISPATCHER", "WAREHOUSE");
        var items = (await q.ToListAsync()).OrderByDescending(x => x.UpdatedAt).Take(250).ToList();
        var merchantIds = items.Select(x => x.MerchantId).Distinct().ToList();
        var shops = await db.Accounts.AsNoTracking().Where(x => merchantIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ShopName);
        return new
        {
            items = items.Select(x => new { x.Id, x.MerchantId, merchantName = shops.GetValueOrDefault(x.MerchantId), x.ContextType, x.ContextId, x.Subject, x.AssignedTo, x.Status, x.Version, x.CreatedAt, x.UpdatedAt }),
            total = items.Count
        };
    }
    private async Task<Conversation> EnsureThread(Actor actor, string merchantId, string type, string contextId, string subject)
    {
        var thread = await db.Conversations.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == merchantId && x.ContextType == type && x.ContextId == contextId);
        if (thread != null)
            return thread;
        thread = new()
        {
            WorkspaceId = actor.WorkspaceId,
            MerchantId = merchantId,
            ContextType = type,
            ContextId = contextId,
            Subject = subject
        };
        db.Conversations.Add(thread);
        return thread;
    }
    public async Task<object> CreateConversation(Actor actor, CreateConversationInput input)
    {
        if (actor.Role == "MERCHANT") actor.ShopWrite();
        var type = Check.Text(input.ContextType, 20, "Contexte").ToUpperInvariant();
        Check.That(type is "GENERAL" or "ORDER" or "DELIVERY" or "GIFT", "Contexte inconnu.");
        string merchant;
        var context = type == "GENERAL" ? "GENERAL" : Check.Text(input.ContextId, 100, "Dossier");
        if (type is "ORDER" or "DELIVERY")
        {
            var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == context && x.WorkspaceId == actor.WorkspaceId);
            Check.That(order != null, "Dossier introuvable.", "NOT_FOUND", 404);
            merchant = order.AccountId;
            if (actor.Role == "MERCHANT")
                Check.That(merchant == actor.ResourceOwnerId, "Dossier introuvable.", "NOT_FOUND", 404);
            else if (actor.Role == "DRIVER")
                Check.That(type == "DELIVERY" && await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == context && x.DriverId == actor.Id), "Dossier introuvable.", "NOT_FOUND", 404);
            else
                actor.RequireRoles("ADMIN", "FINANCE", "DISPATCHER", "WAREHOUSE");
        }
        else if (type == "GIFT")
        {
            var gift = await db.GiftRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == context && x.WorkspaceId == actor.WorkspaceId);
            Check.That(gift != null, "Cadeau introuvable.", "NOT_FOUND", 404);
            merchant = await Merchant(actor, gift.MerchantId);
            Check.That(merchant == gift.MerchantId, "Cadeau introuvable.", "NOT_FOUND", 404);
        }
        else
            merchant = await Merchant(actor, input.MerchantId);
        var thread = await EnsureThread(actor, merchant, type, context, Check.Text(input.Subject, 180, "Sujet"));
        await db.SaveChangesAsync();
        return thread;
    }
    public async Task<object> Messages(Actor actor, string id)
    {
        var thread = await Thread(actor, id);
        var q = db.ConversationMessages.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.ConversationId == id);
        if (!actor.IsStaff)
            q = q.Where(x => !x.Internal);
        var messages = (await q.ToListAsync()).OrderBy(x => x.At).TakeLast(500).ToList();
        return new
        {
            conversation = thread,
            items = messages.Select(x => new { x.Id, x.SenderId, x.SenderName, x.Text, x.Internal, x.At, status = "SENT" }),
            deliveryChannel = "IN_APP"
        };
    }
    public async Task<object> Send(Actor actor, string id, SendMessageInput input)
    {
        if (actor.Role == "MERCHANT") actor.ShopWrite();
        var thread = await Thread(actor, id);
        var key = Key(input.IdempotencyKey);
        var text = Check.Text(input.Text, 5000, "Message");
        Check.That(!input.Internal || actor.IsStaff, "Les notes internes sont réservées à l'équipe.", "FORBIDDEN", 403);
        var hash = Hash(new
        {
            text,
            input.Internal
        });
        var old = await db.ConversationMessages.AsNoTracking().FirstOrDefaultAsync(x => x.ConversationId == id && x.SenderId == actor.Id && x.IdempotencyKey == key);
        if (old != null)
        {
            Check.That(old.RequestHash == hash, "Clé déjà utilisée pour un autre message.", "IDEMPOTENCY_CONFLICT", 409);
            return old;
        }
        Check.That(thread.Status == "OPEN", "Cette conversation est clôturée.", "THREAD_CLOSED", 409);
        var message = new ConversationMessage { WorkspaceId = actor.WorkspaceId, ConversationId = id, SenderId = actor.Id, SenderName = actor.Name, Text = text, Internal = input.Internal, IdempotencyKey = key, RequestHash = hash };
        db.ConversationMessages.Add(message);
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        thread.Version++;
        orders.Audit(actor, input.Internal ? "INTERNAL_NOTE_CREATED" : "MESSAGE_SENT", id);
        Notice(actor, input.Internal ? "" : thread.MerchantId, "MESSAGE_SENT", id);
        await db.SaveChangesAsync();
        return message;
    }
    public async Task<object> UpdateThread(Actor actor, string id, UpdateConversationInput input)
    {
        actor.RequireRoles("ADMIN", "DISPATCHER", "FINANCE", "WAREHOUSE");
        var thread = await Thread(actor, id);
        Version(thread.Version, input.Version);
        var status = input.Status.ToUpperInvariant();
        Check.That(status is "OPEN" or "CLOSED", "État inconnu.");
        var assigned = Check.Text(input.AssignedTo, 100, "Responsable", false);
        if (assigned.Length > 0)
            Check.That(await db.Accounts.AnyAsync(x => x.Id == assigned && x.WorkspaceId == actor.WorkspaceId && (x.Role == "ADMIN" || x.Role == "WAREHOUSE" || x.Role == "DISPATCHER" || x.Role == "FINANCE") && x.ApprovalStatus == "APPROVED"), "Choisissez une personne habilitée à ouvrir ce dossier.");
        thread.Status = status;
        thread.AssignedTo = assigned;
        thread.Version++;
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        orders.Audit(actor, "CONVERSATION_UPDATED", id, status);
        Notice(actor, thread.MerchantId, "CONVERSATION_UPDATED", id);
        await db.SaveChangesAsync();
        return thread;
    }
    public async Task<object> Preference(Actor actor)
    {
        actor.RequireRoles("MERCHANT");
        var p = await db.CommunicationPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == actor.ResourceOwnerId);
        return new
        {
            optIn = p?.MarketingOptIn ?? false,
            p?.UpdatedAt,
            serviceMessages = true
        };
    }
    public async Task<object> SetPreference(Actor actor, MarketingPreferenceInput input)
    {
        actor.ManageShop();
        actor.RequireRoles("MERCHANT");
        var p = await db.CommunicationPreferences.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == actor.ResourceOwnerId);
        if (p == null)
        {
            p = new()
            {
                WorkspaceId = actor.WorkspaceId,
                MerchantId = actor.ResourceOwnerId
            };
            db.CommunicationPreferences.Add(p);
        }
        p.MarketingOptIn = input.OptIn;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        orders.Audit(actor, "MARKETING_PREFERENCE_UPDATED", actor.Id, input.OptIn.ToString());
        await db.SaveChangesAsync();
        return await Preference(actor);
    }
    public async Task<object> Broadcasts(Actor actor)
    {
        actor.Admin();
        return new
        {
            items = (await db.Broadcasts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync()).OrderByDescending(x => x.CreatedAt)
        };
    }
    public async Task<object> CreateBroadcast(Actor actor, CreateBroadcastInput input)
    {
        actor.Admin();
        var key = Key(input.IdempotencyKey);
        var purpose = input.Purpose.ToUpperInvariant();
        Check.That(purpose is "PROMOTIONAL" or "SERVICE", "Finalité inconnue.");
        Check.That(input.MerchantIds is { Count: > 0 and <= 1000 }, "Choisissez entre 1 et 1000 magasins.");
        var targets = input.MerchantIds.Distinct().Order().ToList();
        var title = Check.Text(input.Title, 180, "Titre");
        var body = Check.Text(input.Body, 5000, "Message");
        var hash = Hash(new
        {
            purpose,
            title,
            body,
            targets
        });
        var old = await db.Broadcasts.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.IdempotencyKey == key);
        if (old != null)
        {
            Check.That(old.RequestHash == hash, "Clé déjà utilisée.", "IDEMPOTENCY_CONFLICT", 409);
            return old;
        }
        Check.That(await db.Accounts.CountAsync(x => x.WorkspaceId == actor.WorkspaceId && x.Role == "MERCHANT" && !db.ShopMemberships.Any(m => m.AccountId == x.Id) && targets.Contains(x.Id)) == targets.Count, "Magasin introuvable.");
        var item = new Broadcast { WorkspaceId = actor.WorkspaceId, Purpose = purpose, Title = title, Body = body, MerchantIdsJson = JsonSerializer.Serialize(targets), Targeted = targets.Count, CreatedBy = actor.Id, IdempotencyKey = key, RequestHash = hash };
        db.Broadcasts.Add(item);
        orders.Audit(actor, "BROADCAST_DRAFT_CREATED", item.Id, purpose);
        await db.SaveChangesAsync();
        return item;
    }
    public async Task<object> DispatchBroadcast(Actor actor, string id)
    {
        actor.Admin();
        var b = await db.Broadcasts.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(b != null, "Diffusion introuvable.", "NOT_FOUND", 404);
        if (b.Status == "SENT")
            return new
            {
                broadcast = b,
                recipients = await db.BroadcastRecipients.AsNoTracking().Where(x => x.BroadcastId == id).ToListAsync()
            };
        var targets = JsonSerializer.Deserialize<List<string>>(b.MerchantIdsJson)!;
        var accounts = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && targets.Contains(x.Id) && x.Role == "MERCHANT" && !db.ShopMemberships.Any(m => m.AccountId == x.Id)).ToDictionaryAsync(x => x.Id);
        var opted = await db.CommunicationPreferences.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.MarketingOptIn).Select(x => x.MerchantId).ToListAsync();
        foreach (var merchantId in targets)
        {
            var allowed = accounts.TryGetValue(merchantId, out var shop) && shop.ApprovalStatus == "APPROVED" && (b.Purpose == "SERVICE" || opted.Contains(merchantId));
            var recipient = new BroadcastRecipient { WorkspaceId = actor.WorkspaceId, BroadcastId = id, MerchantId = merchantId, Status = allowed ? "SENT_IN_APP" : "EXCLUDED" };
            if (allowed)
            {
                var thread = await EnsureThread(actor, merchantId, "GENERAL", "GENERAL", "Votre relation Toushir");
                var message = new ConversationMessage { WorkspaceId = actor.WorkspaceId, ConversationId = thread.Id, SenderId = actor.Id, SenderName = actor.Name, Text = b.Title + "\n\n" + b.Body, IdempotencyKey = "broadcast:" + id, RequestHash = b.RequestHash };
                db.ConversationMessages.Add(message);
                recipient.MessageId = message.Id;
                thread.UpdatedAt = DateTimeOffset.UtcNow;
                thread.Version++;
                b.Sent++;
                Notice(actor, merchantId, "MESSAGE_SENT", thread.Id);
            }
            else
                b.Excluded++;
            db.BroadcastRecipients.Add(recipient);
        }
        b.Status = "SENT";
        b.DispatchedAt = DateTimeOffset.UtcNow;
        orders.Audit(actor, "BROADCAST_DISPATCHED", id, $"{b.Sent} envoyés / {b.Excluded} exclus; IN_APP");
        await db.SaveChangesAsync();
        return new
        {
            broadcast = b,
            recipients = await db.BroadcastRecipients.AsNoTracking().Where(x => x.BroadcastId == id).ToListAsync(),
            deliveryChannel = "IN_APP"
        };
    }

    public async Task<object> Campaigns(Actor actor, bool admin = false)
    {
        if (admin)
            actor.RequireRoles("ADMIN", "FINANCE");
        var q = db.Campaigns.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId);
        if (!admin)
        {
            actor.RequireRoles("MERCHANT", "ADMIN", "FINANCE");
            q = q.Where(x => x.Status == "PUBLISHED");
        }
        if (actor.Role == "MERCHANT") q = CampaignAudienceScope.ForShop(db, q, actor.ResourceOwnerId);
        var campaigns = await q.ToListAsync();
        if (actor.Role is "ADMIN" or "FINANCE") {
            var audiences = await db.CampaignAudiences.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync();
            foreach (var campaign in campaigns) {
                campaign.TargetMerchantIds = audiences.Where(x => x.CampaignId == campaign.Id).Select(x => x.MerchantId).ToList();
                campaign.TargetCount = campaign.TargetMerchantIds.Count;
            }
        }
        return new
        {
            items = campaigns.OrderByDescending(x => x.CreatedAt),
            pricingIntegration = "Campaign previews do not modify accepted quotes. Apply an approved campaign explicitly when quoting."
        };
    }
    public async Task<object> CreateCampaign(Actor actor, CreateCampaignInput x)
    {
        actor.Admin();
        var targets = x.TargetMerchantIds ?? [];
        Check.That(targets.Count <= 500 && targets.Distinct().Count() == targets.Count, "Choisissez au maximum 500 magasins distincts.");
        if (targets.Count > 0) {
            var eligible = await db.Accounts.AsNoTracking().Where(a => a.WorkspaceId == actor.WorkspaceId && a.Role == "MERCHANT" && a.ApprovalStatus == "APPROVED"
                && targets.Contains(a.Id) && !db.ShopMemberships.Any(m => m.AccountId == a.Id)).CountAsync();
            Check.That(eligible == targets.Count, "Un magasin ciblé n’est pas approuvé ou n’appartient pas à cet espace.", "CAMPAIGN_AUDIENCE_INVALID", 400);
        }
        var kind = x.Kind.ToUpperInvariant();
        Check.That(kind is "DISCOUNT" or "FREE_GOODS" or "CHALLENGE", "Mécanique inconnue.");
        Check.That(await db.Products.AnyAsync(p => p.Id == x.ProductId && p.IsCatalogActive), "Produit introuvable.");
        Check.That(x.StartsAt < x.EndsAt && x.EndsAt - x.StartsAt <= TimeSpan.FromDays(366), "Période invalide.");
        Check.That(x.MinimumQuantity is >= 1 and <= 1000000 && x.UnitFactor is >= 1 and <= 1000, "Seuil ou facteur invalide.");
        Check.That(x.DiscountPercent is >= 0 and <= 100 && decimal.Round(x.DiscountPercent, 2) == x.DiscountPercent, "Pourcentage invalide.");
        if (kind == "DISCOUNT")
            Check.That(x.DiscountPercent > 0, "Une remise positive est requise.");
        if (kind == "FREE_GOODS")
            Check.That(x.GiftQuantity is >= 1 and <= 1000 && await db.Products.AnyAsync(p => p.Id == x.GiftProductId && p.IsCatalogActive), "Produit offert invalide.");
        if (kind == "CHALLENGE")
            Check.That(x.GoalQuantity is >= 1 and <= 100000000 && x.AwardLimit is >= 1 and <= 100000 && x.ClaimUntil > x.EndsAt && await db.RewardItems.AnyAsync(r => r.Id == x.RewardId && r.WorkspaceId == actor.WorkspaceId && r.IsActive), "Objectif, dotation ou période de réclamation invalide.");
        var c = new Campaign { WorkspaceId = actor.WorkspaceId, Title = Check.Text(x.Title, 180, "Titre"), Description = Check.Text(x.Description, 3000, "Conditions"), Kind = kind, ProductId = x.ProductId, MinimumQuantity = x.MinimumQuantity, DiscountPercent = x.DiscountPercent, GiftProductId = x.GiftProductId ?? "", GiftQuantity = x.GiftQuantity, GoalQuantity = x.GoalQuantity, UnitFactor = x.UnitFactor, RewardId = x.RewardId ?? "", AwardLimit = x.AwardLimit, StartsAt = x.StartsAt, EndsAt = x.EndsAt, ClaimUntil = x.ClaimUntil, CreatedBy = actor.Id };
        db.Campaigns.Add(c);
        foreach (var target in targets) db.CampaignAudiences.Add(new() { WorkspaceId = actor.WorkspaceId, CampaignId = c.Id, MerchantId = target });
        c.TargetMerchantIds = targets.ToList(); c.TargetCount = targets.Count;
        orders.Audit(actor, "CAMPAIGN_DRAFT_CREATED", c.Id);
        await db.SaveChangesAsync();
        return c;
    }
    public async Task<object> PublishCampaign(Actor actor, string id, VersionInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        var c = await db.Campaigns.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(c != null, "Campagne introuvable.", "NOT_FOUND", 404);
        Version(c.Version, input.Version);
        Check.That(c.Status == "DRAFT", "Cette version est déjà publiée.", "INVALID_TRANSITION", 409);
        Check.That(c.EndsAt > DateTimeOffset.UtcNow, "Une campagne terminée ne peut pas être publiée rétroactivement.", "CAMPAIGN_EXPIRED", 409);
        Check.That(c.CreatedBy != actor.Id, "Une autre personne habilitée doit vérifier la campagne.", "INDEPENDENT_APPROVAL_REQUIRED", 409);
        c.Status = "PUBLISHED";
        c.ApprovedBy = actor.Id;
        c.Version++;
        orders.Audit(actor, "CAMPAIGN_PUBLISHED", id);
        var targets = await db.CampaignAudiences.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.CampaignId == id).Select(x => x.MerchantId).ToListAsync();
        c.TargetMerchantIds = targets; c.TargetCount = targets.Count;
        if (targets.Count == 0) Notice(actor, "", "CAMPAIGN_PUBLISHED", id);
        else foreach (var target in targets) Notice(actor, target, "CAMPAIGN_PUBLISHED", id);
        await db.SaveChangesAsync();
        return c;
    }
    private async Task<Campaign> VisibleCampaign(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "FINANCE", "MERCHANT");
        var c = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId && (actor.Role != "MERCHANT" || x.Status == "PUBLISHED"));
        Check.That(c != null, "Campagne introuvable.", "NOT_FOUND", 404);
        if (actor.Role == "MERCHANT") Check.That(await CampaignAudienceScope.Allows(db, c, actor.ResourceOwnerId), "Campagne introuvable.", "NOT_FOUND", 404);
        return c;
    }
    public async Task<object> PreviewCampaign(Actor actor, string id, CampaignPreviewInput x)
    {
        var c = await VisibleCampaign(actor, id);
        Check.That(x.Quantity is >= 0 and <= 1000000, "Quantité invalide.");
        var offer = await db.Offers.AsNoTracking().FirstOrDefaultAsync(o => o.WorkspaceId == actor.WorkspaceId && o.ProductId == c.ProductId);
        var book = await pricing.LoadBook(actor.WorkspaceId, actor.Role == "MERCHANT" ? actor.ResourceOwnerId : null);
        var decision = book.Resolve(c.ProductId, Math.Max(1, x.Quantity), offer);
        var active = c.Status == "PUBLISHED" && c.StartsAt <= book.At && book.At <= c.EndsAt;
        var tariffCompatible = c.Kind == "CHALLENGE" || decision.RuleId.Length == 0 || decision.AllowCampaigns;
        var qualifies = active && x.Quantity >= c.MinimumQuantity && tariffCompatible;
        var price = decision.UnitPrice;
        var applied = price;
        var pricingIssue = "";
        if (qualifies && price.HasValue && c.Kind == "DISCOUNT")
            try { applied = CampaignPricing.Applied(c, price.Value, x.Quantity); }
            catch (ApiFailure error) when (error.Code == "PROMOTION_PRICE_INVALID") { pricingIssue = error.Code; qualifies = false; }
        var goods = price * x.Quantity;
        var discount = qualifies && price.HasValue && applied.HasValue ? (price.Value - applied.Value) * x.Quantity : 0m;
        return new { campaign = c, active, qualifies, tariffCompatible, pricingIssue,
            quantity = x.Quantity, goodsBefore = goods, discount, goodsAfter = goods - discount,
            baseUnitPrice = price, appliedUnitPrice = applied, pricingSource = decision.Source,
            freeQuantity = qualifies && c.Kind == "FREE_GOODS" ? c.GiftQuantity : 0,
            maxGiftsPerOrder = 1, currency = "DZD", quoteRequired = price == null || pricingIssue.Length > 0, pointsDebited = 0,
            notice = !tariffCompatible ? "Votre tarif ne permet pas de cumuler cette campagne. Le prix de votre panier reste celui de votre contrat."
                : pricingIssue.Length > 0 ? "Cette remise doit être revue pour ce prix unitaire. Demandez un devis."
                : "Simulation de devis; aucune commande existante modifiée. Une seule campagne est retenue par produit." };
    }
    private async Task<int> EligibleCampaignUnits(Campaign c, string merchant)
    {
        if (!await CampaignAudienceScope.Allows(db, c, merchant)) return 0;
        var list = await db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.WorkspaceId == c.WorkspaceId && o.AccountId == merchant).ToListAsync();
        var published = await db.Audit.AsNoTracking().Where(x => x.WorkspaceId == c.WorkspaceId && x.EntityId == c.Id && x.Action == "CAMPAIGN_PUBLISHED").Select(x => x.At).ToListAsync();
        if (published.Count == 0)
            return 0;
        var effectiveStart = published.Min() > c.StartsAt ? published.Min() : c.StartsAt;
        var claims = await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == c.WorkspaceId && x.AccountId == merchant && x.ProductId == c.ProductId).ToListAsync();
        long units = 0;
        foreach (var o in list.Where(o => o.CreatedAt >= effectiveStart && o.CreatedAt <= c.EndsAt && o.Status is "DELIVERED" or "PARTIALLY_DELIVERED" && o.Total != null && o.Collected >= o.Total))
            foreach (var line in o.Items.Where(l => l.ProductId == c.ProductId))
                units += (long)Math.Max(0, line.AcceptedQuantity - claims.Where(x => x.OrderId == o.Id).Sum(x => x.ReceivedQuantity)) * c.UnitFactor;
        return checked((int)Math.Min(int.MaxValue, units));
    }
    public async Task<object> Progress(Actor actor, string id, string? merchantId)
    {
        var c = await VisibleCampaign(actor, id);
        var merchant = await Merchant(actor, merchantId);
        Check.That(await CampaignAudienceScope.Allows(db, c, merchant), "Ce magasin ne fait pas partie du périmètre de la campagne.", "CAMPAIGN_NOT_APPLICABLE", 409);
        var units = await EligibleCampaignUnits(c, merchant);
        var grant = await db.CampaignGrants.AsNoTracking().FirstOrDefaultAsync(x => x.CampaignId == id && x.MerchantId == merchant);
        return new
        {
            campaign = c,
            eligibleQuantity = units,
            goal = c.GoalQuantity,
            achieved = c.GoalQuantity > 0 && units >= c.GoalQuantity,
            grant,
            pointsCost = 0
        };
    }
    public async Task<object> Award(Actor actor, string id, AwardCampaignInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        var c = await VisibleCampaign(actor, id);
        var merchant = await Merchant(actor, input.MerchantId);
        Check.That(await CampaignAudienceScope.Allows(db, c, merchant), "Ce magasin ne fait pas partie du périmètre de la campagne.", "CAMPAIGN_NOT_APPLICABLE", 409);
        var old = await db.CampaignGrants.FirstOrDefaultAsync(x => x.CampaignId == id && x.MerchantId == merchant);
        if (old != null)
            return old;
        Check.That(c.Kind == "CHALLENGE" && c.Status == "PUBLISHED" && DateTimeOffset.UtcNow >= c.EndsAt && c.ClaimUntil > DateTimeOffset.UtcNow, "Cette attribution n'est pas ouverte.", "INVALID_TRANSITION", 409);
        var units = await EligibleCampaignUnits(c, merchant);
        Check.That(units >= c.GoalQuantity, "L'objectif validé n'est pas atteint.", "GOAL_NOT_REACHED", 409);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Campaigns\" SET \"AwardsGranted\"=\"AwardsGranted\"+1, \"Version\"=\"Version\"+1 WHERE \"Id\"={id} AND \"WorkspaceId\"={actor.WorkspaceId} AND \"AwardsGranted\"<\"AwardLimit\"");
        Check.That(changed == 1, "Le budget de dotations est épuisé.", "BUDGET_EXHAUSTED", 409);
        var grant = new CampaignGrant { WorkspaceId = actor.WorkspaceId, CampaignId = id, MerchantId = merchant, RewardId = c.RewardId, EligibleQuantity = units, ApprovedBy = actor.Id };
        db.CampaignGrants.Add(grant);
        orders.Audit(actor, "CAMPAIGN_AWARD_GRANTED", grant.Id);
        Notice(actor, merchant, "CAMPAIGN_AWARD_GRANTED", grant.Id);
        await db.SaveChangesAsync();
        return grant;
    }

    private async Task<LoyaltyWallet> Wallet(Actor actor, string merchant)
    {
        var wallet = await db.LoyaltyWallets.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == merchant);
        if (wallet == null)
        {
            wallet = new()
            {
                WorkspaceId = actor.WorkspaceId,
                MerchantId = merchant
            };
            db.LoyaltyWallets.Add(wallet);
            await db.SaveChangesAsync();
        }
        return wallet;
    }
    private void PointEntry(Actor a, string merchant, string kind, string source, int available = 0, int reserved = 0, int spent = 0, int due = 0, int orderPoints = 0) => db.LoyaltyEntries.Add(new() { WorkspaceId = a.WorkspaceId, MerchantId = merchant, Kind = kind, SourceRef = source, AvailableDelta = available, ReservedDelta = reserved, SpentDelta = spent, CorrectionDueDelta = due, OrderPointDelta = orderPoints, ActorId = a.Id });
    public async Task<object> Loyalty(Actor actor, string? merchantId = null)
    {
        var merchant = await Merchant(actor, merchantId);
        var wallet = await db.LoyaltyWallets.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == merchant);
        var entries = await db.LoyaltyEntries.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == merchant).ToListAsync();
        return new
        {
            merchantId = merchant,
            available = wallet?.Available ?? 0,
            reserved = wallet?.Reserved ?? 0,
            spent = wallet?.Spent ?? 0,
            correctionDue = wallet?.CorrectionDue ?? 0,
            version = wallet?.Version ?? 0,
            entries = entries.OrderByDescending(x => x.At).Take(200),
            rule = "1 point par tranche complète de 100 DA de marchandises acceptées et réglées; hors livraison."
        };
    }
    public async Task<object> SyncLoyalty(Actor actor, SyncLoyaltyInput input)
    {
        var merchant = await Merchant(actor, input.MerchantId);
        if (actor.Role == "MERCHANT")
        { actor.ShopWrite(); actor.Approved(); }
        else
            actor.RequireRoles("ADMIN", "FINANCE");
        return await ReconcileLoyalty(actor, merchant);
    }
    private async Task<object> ReconcileLoyalty(Actor actor, string merchant)
    {
        var wallet = await Wallet(actor, merchant);
        var list = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == actor.WorkspaceId && x.AccountId == merchant).ToListAsync();
        var returns = await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.AccountId == merchant && x.Status == "ACCEPTED" && x.ReceivedQuantity > 0).ToListAsync();
        var ledger = await db.LoyaltyEntries.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == merchant && x.Kind.StartsWith("ORDER_")).ToListAsync();
        foreach (var o in list)
        {
            // A physical return stops earning immediately. Taking the minimum with the net commercial subtotal
            // also handles explicit credits without subtracting the same returned unit twice.
            var retainedValue = o.Items.Sum(line => Math.Max(0, line.AcceptedQuantity - returns.Where(x => x.OrderId == o.Id && x.ProductId == line.ProductId).Sum(x => x.ReceivedQuantity)) * (line.UnitPrice ?? 0m));
            var eligible = o.Status is "DELIVERED" or "PARTIALLY_DELIVERED" && o.Total != null && o.Collected >= o.Total ? Math.Max(0m, Math.Min(Math.Min(o.Subtotal ?? 0m, retainedValue), o.Collected - (o.DeliveryFee ?? 0m))) : 0m;
            Check.That(decimal.Floor(eligible / 100m) <= 100000000m, "Cette attribution dépasse le plafond technique et exige un examen.", "POINTS_LIMIT", 409);
            var desired = checked((int)decimal.Floor(eligible / 100m));
            var previous = ledger.Where(e => e.SourceRef == o.Id).Sum(e => e.OrderPointDelta);
            var delta = desired - previous;
            if (delta == 0)
                continue;
            int av, due;
            if (delta > 0)
            {
                var settle = Math.Min(delta, wallet.CorrectionDue);
                Check.That((long)wallet.Available + delta - settle <= int.MaxValue, "Le solde exige un examen.", "POINTS_LIMIT", 409);
                wallet.CorrectionDue -= settle;
                wallet.Available += delta - settle;
                av = delta - settle;
                due = -settle;
            }
            else
            {
                var remove = Math.Min(-delta, wallet.Available);
                Check.That((long)wallet.CorrectionDue - delta - remove <= int.MaxValue, "La régularisation exige un examen.", "POINTS_LIMIT", 409);
                wallet.Available -= remove;
                wallet.CorrectionDue += -delta - remove;
                av = -remove;
                due = -delta - remove;
            }
            wallet.Version++;
            PointEntry(actor, merchant, delta > 0 ? "ORDER_EARNED" : "ORDER_CORRECTED", o.Id, available: av, due: due, orderPoints: delta);
        }
        Notice(actor, merchant, "LOYALTY_UPDATED", merchant);
        await db.SaveChangesAsync();
        return await Loyalty(actor, merchant);
    }
    public async Task<object> Rewards(Actor actor)
    {
        actor.RequireRoles("ADMIN", "FINANCE", "MERCHANT");
        var q = db.RewardItems.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId);
        if (actor.Role == "MERCHANT")
            q = q.Where(x => x.IsActive);
        return new
        {
            items = await q.OrderBy(x => x.PointsCost).ToListAsync()
        };
    }
    private static void ValidateReward(RewardItemInput x)
    {
        Check.That(x.PointsCost is >= 1 and <= 100000000 && x.AvailableStock is >= 0 and <= 1000000, "Coût ou stock invalide.");
        Check.That(string.IsNullOrWhiteSpace(x.ImageUrl) || x.ImageUrl.StartsWith("/") && !x.ImageUrl.StartsWith("//") || Uri.TryCreate(x.ImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https", "Image invalide.");
    }
    public async Task<object> SaveReward(Actor actor, string? id, RewardItemInput x)
    {
        actor.Admin();
        ValidateReward(x);
        RewardItem reward;
        if (id == null)
        {
            reward = new()
            {
                WorkspaceId = actor.WorkspaceId
            };
            db.RewardItems.Add(reward);
        }
        else
        {
            reward = (await db.RewardItems.FirstOrDefaultAsync(r => r.Id == id && r.WorkspaceId == actor.WorkspaceId))!;
            Check.That(reward != null, "Récompense introuvable.", "NOT_FOUND", 404);
            Version(reward.Version, x.Version ?? -1);
            reward.Version++;
        }
        reward.Name = Check.Text(x.Name, 180, "Nom");
        reward.Description = Check.Text(x.Description, 2000, "Conditions");
        reward.ImageUrl = Check.Text(x.ImageUrl, 2000, "Image", false);
        reward.PointsCost = x.PointsCost;
        reward.AvailableStock = x.AvailableStock;
        reward.IsActive = x.IsActive;
        orders.Audit(actor, "REWARD_CATALOG_SAVED", reward.Id);
        await db.SaveChangesAsync();
        return reward;
    }
    public async Task<object> Gifts(Actor actor, string? merchantId = null)
    {
        var q = db.GiftRequests.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId);
        if (actor.Role == "MERCHANT")
            q = q.Where(x => x.MerchantId == actor.ResourceOwnerId);
        else
        {
            actor.RequireRoles("ADMIN", "FINANCE", "WAREHOUSE");
            if (!string.IsNullOrEmpty(merchantId))
                q = q.Where(x => x.MerchantId == merchantId);
        }
        return new
        {
            items = (await q.ToListAsync()).OrderByDescending(x => x.CreatedAt)
        };
    }
    public async Task<object> ReserveGift(Actor actor, string rewardId, ReserveGiftInput input)
    {
        actor.ManageShop();
        actor.RequireRoles("MERCHANT");
        actor.Approved();
        var key = Key(input.IdempotencyKey);
        var grantId = Check.Text(input.GrantId, 100, "Attribution", false);
        var hash = Hash(new
        {
            rewardId,
            grantId
        });
        var old = await db.GiftRequests.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.MerchantId == actor.ResourceOwnerId && x.IdempotencyKey == key);
        if (old != null)
        {
            Check.That(old.RequestHash == hash, "Clé déjà utilisée pour un autre cadeau.", "IDEMPOTENCY_CONFLICT", 409);
            return old;
        }
        // Reconcile immediately before spending, so a recent refund/credit cannot leave spendable stale points.
        await ReconcileLoyalty(actor, actor.ResourceOwnerId);
        var reward = await db.RewardItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rewardId && x.WorkspaceId == actor.WorkspaceId && x.IsActive);
        Check.That(reward != null, "Récompense introuvable.", "NOT_FOUND", 404);
        var cost = reward.PointsCost;
        CampaignGrant? grant = null;
        if (grantId.Length > 0)
        {
            grant = await db.CampaignGrants.FirstOrDefaultAsync(x => x.Id == grantId && x.WorkspaceId == actor.WorkspaceId && x.MerchantId == actor.ResourceOwnerId && x.RewardId == rewardId);
            Check.That(grant?.Status == "GRANTED", "Attribution indisponible.", "INVALID_GRANT", 409);
            var c = await db.Campaigns.AsNoTracking().FirstAsync(x => x.Id == grant.CampaignId);
            Check.That(c.ClaimUntil >= DateTimeOffset.UtcNow && await EligibleCampaignUnits(c, actor.ResourceOwnerId) >= c.GoalQuantity, "L'attribution doit être réexaminée ou a expiré.", "GRANT_REVIEW_REQUIRED", 409);
            cost = 0;
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CampaignGrants\" SET \"Status\"={"CLAIMED"} WHERE \"Id\"={grant.Id} AND \"WorkspaceId\"={actor.WorkspaceId} AND \"MerchantId\"={actor.ResourceOwnerId} AND \"Status\"={"GRANTED"}");
            Check.That(claimed == 1, "Cette attribution a déjà été réclamée.", "GRANT_ALREADY_CLAIMED", 409);
            db.Entry(grant).State = EntityState.Detached;
        }
        var wallet = await Wallet(actor, actor.ResourceOwnerId);
        db.Entry(wallet).State = EntityState.Detached;
        if (cost > 0)
        {
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LoyaltyWallets\" SET \"Available\"=\"Available\"-{cost}, \"Reserved\"=\"Reserved\"+{cost}, \"Version\"=\"Version\"+1 WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"MerchantId\"={actor.ResourceOwnerId} AND \"Available\">={cost} AND \"Reserved\"<={int.MaxValue - cost} AND \"CorrectionDue\"=0");
            Check.That(changed == 1, "Points insuffisants ou régularisation en attente.", "INSUFFICIENT_POINTS", 409);
        }
        var stock = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"RewardItems\" SET \"AvailableStock\"=\"AvailableStock\"-1, \"Version\"=\"Version\"+1 WHERE \"Id\"={rewardId} AND \"WorkspaceId\"={actor.WorkspaceId} AND \"IsActive\"={true} AND \"AvailableStock\">0");
        Check.That(stock == 1, "Ce cadeau n'est plus disponible.", "REWARD_OUT_OF_STOCK", 409);
        var gift = new GiftRequest { WorkspaceId = actor.WorkspaceId, MerchantId = actor.ResourceOwnerId, RewardId = rewardId, RewardName = reward.Name, GrantId = grantId, PointsCost = cost, IdempotencyKey = key, RequestHash = hash };
        db.GiftRequests.Add(gift);
        if (cost > 0)
            PointEntry(actor, actor.ResourceOwnerId, "GIFT_RESERVED", gift.Id, available: -cost, reserved: cost);
        orders.Audit(actor, "GIFT_RESERVED", gift.Id);
        Notice(actor, actor.ResourceOwnerId, "GIFT_RESERVED", gift.Id);
        await db.SaveChangesAsync();
        return gift;
    }
    public async Task<object> TransitionGift(Actor actor, string id, GiftTransitionInput input)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE");
        var gift = await db.GiftRequests.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(gift != null, "Demande introuvable.", "NOT_FOUND", 404);
        var status = input.Status.ToUpperInvariant();
        Check.That(status is "READY" or "FULFILLED" or "CANCELLED", "État inconnu.");
        if (gift.Status == status)
        {
            if (status == "FULFILLED")
                Check.That(gift.ProofReference == input.ProofReference?.Trim(), "La remise existante a une autre preuve.", "IDEMPOTENCY_CONFLICT", 409);
            return gift;
        }
        Version(gift.Version, input.Version);
        Check.That(gift.Status is "RESERVED" or "READY", "Cette demande est déjà terminée.", "INVALID_TRANSITION", 409);
        if (status is "FULFILLED" or "CANCELLED")
            await ReconcileLoyalty(actor, gift.MerchantId);
        if (status == "READY")
            Check.That(gift.Status == "RESERVED", "La demande doit être réservée.", "INVALID_TRANSITION", 409);
        if (status == "FULFILLED")
        {
            Check.That(gift.Status == "READY", "Préparez le cadeau avant sa remise.", "INVALID_TRANSITION", 409);
            gift.ProofReference = Check.Text(input.ProofReference, 300, "Preuve de remise");
            if (gift.GrantId.Length > 0)
            {
                var grant = await db.CampaignGrants.AsNoTracking().FirstAsync(x => x.Id == gift.GrantId);
                var c = await db.Campaigns.AsNoTracking().FirstAsync(x => x.Id == grant.CampaignId);
                Check.That(await EligibleCampaignUnits(c, gift.MerchantId) >= c.GoalQuantity, "Un retour impose un réexamen de l'attribution.", "GRANT_REVIEW_REQUIRED", 409);
            }
            if (gift.PointsCost > 0)
            {
                var cost = gift.PointsCost;
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LoyaltyWallets\" SET \"Reserved\"=\"Reserved\"-{cost}, \"Spent\"=\"Spent\"+{cost}, \"Version\"=\"Version\"+1 WHERE \"WorkspaceId\"={actor.WorkspaceId} AND \"MerchantId\"={gift.MerchantId} AND \"Reserved\">={cost} AND \"Spent\"<={int.MaxValue - cost} AND \"CorrectionDue\"=0");
                Check.That(changed == 1, "Réserve de points incohérente ou correction à examiner.", "POINTS_REVIEW_REQUIRED", 409);
                PointEntry(actor, gift.MerchantId, "GIFT_FULFILLED", id, reserved: -cost, spent: cost);
            }
            gift.FulfilledAt = DateTimeOffset.UtcNow;
        }
        if (status == "CANCELLED")
        {
            gift.Reason = Check.Text(input.Reason, 1000, "Motif");
            var wallet = await Wallet(actor, gift.MerchantId);
            var cost = gift.PointsCost;
            Check.That(wallet.Reserved >= cost, "Réserve de points incohérente.", "POINTS_CONFLICT", 409);
            var settle = Math.Min(cost, wallet.CorrectionDue);
            wallet.Reserved -= cost;
            wallet.Available += cost - settle;
            wallet.CorrectionDue -= settle;
            wallet.Version++;
            if (cost > 0)
                PointEntry(actor, gift.MerchantId, "GIFT_CANCELLED", id, available: cost - settle, reserved: -cost, due: -settle);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"RewardItems\" SET \"AvailableStock\"=\"AvailableStock\"+1, \"Version\"=\"Version\"+1 WHERE \"Id\"={gift.RewardId} AND \"WorkspaceId\"={actor.WorkspaceId}");
            if (gift.GrantId.Length > 0)
            {
                var grant = await db.CampaignGrants.FirstAsync(x => x.Id == gift.GrantId);
                grant.Status = "GRANTED";
            }
        }
        gift.Status = status;
        gift.Version++;
        orders.Audit(actor, "GIFT_" + status, id, gift.Reason);
        Notice(actor, gift.MerchantId, "GIFT_UPDATED", id);
        await db.SaveChangesAsync();
        return gift;
    }
}
