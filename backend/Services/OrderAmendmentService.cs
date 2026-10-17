using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class OrderAmendmentService(WholesaleDb db, OrderService orders, CatalogService catalog)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static JsonElement Read(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private async Task<PurchaseOrder> Order(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "MERCHANT");
        return await orders.Find(actor, id);
    }
    private async Task<string> Eligibility(PurchaseOrder order)
    {
        if (order.Status is not "SUBMITTED" and not "CONFIRMED") return "La commande doit être reçue ou confirmée, avant préparation.";
        if (!order.ReservationHeld || order.Total == null || order.DeliveryFee == null) return "Le contrat et sa réservation doivent être complets.";
        if (order.Items.Any(x => x.PreparedQuantity != 0 || x.AcceptedQuantity != 0 || x.RefusedQuantity != 0)) return "La préparation ou la réception a déjà commencé.";
        if (order.Collected != 0 || order.PaymentStatus != "UNPAID" || await db.Payments.AnyAsync(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id)) return "Un règlement existe déjà sur cette commande.";
        if (await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id)) return "La commande a déjà été affectée à la livraison.";
        if (await db.LotAllocations.AnyAsync(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id && (x.WaveId != "" || x.PickedQuantity != 0))) return "Une vague ou un contrôle physique a déjà commencé.";
        return "";
    }
    private static string Applicability(OrderAmendment proposal, PurchaseOrder order, string eligibility)
        => proposal.Status != "PENDING" ? "CLOSED" : proposal.ExpiresAt <= DateTimeOffset.UtcNow ? "EXPIRED"
            : proposal.BaseOrderVersion != order.Version || eligibility.Length > 0 ? "STALE" : "PENDING";

    private static object View(Actor actor, OrderAmendment proposal, PurchaseOrder order, string eligibility)
    {
        var applicability = Applicability(proposal, order, eligibility);
        var merchant = actor.Role == "MERCHANT" && actor.CanShopWrite && actor.ApprovalStatus == "APPROVED";
        return new { proposal.Id, proposal.Version, proposal.Status, applicability,
            canAccept = merchant && applicability == "PENDING", canReject = merchant && applicability == "PENDING",
            canWithdraw = actor.IsAdmin && proposal.Status == "PENDING", proposal.BaseOrderVersion,
            proposal.Reason, proposal.ExpiresAt, proposal.ProposedBy, proposal.ProposerName, proposal.ProposedAt,
            before = Read(proposal.BeforeJson), after = PublicAfter(actor, proposal), proposal.TermsHash,
            proposal.DecidedBy, proposal.DecisionName, proposal.DecidedAt, proposal.DecisionReason, proposal.AcceptedOrderVersion };
    }
    private static object PublicAfter(Actor actor, OrderAmendment proposal)
    {
        if (actor.IsAdmin) return Read(proposal.AfterJson);
        var terms = JsonSerializer.Deserialize<AmendmentTerms>(proposal.AfterJson, Json)!;
        return new { items = terms.Items.Select(x => new { x.ProductId, x.Name, x.Brand, x.ImageUrl, x.Quantity,
            x.CaseSize, x.MinimumQuantity, x.BaseUnitPrice, x.UnitPrice, x.LineTotal, x.PricingSource,
            x.CampaignsAllowed, x.CampaignId, x.CampaignVersion, x.CampaignTitle, x.CampaignKind,
            x.GiftProductId, x.GiftProductName, x.GiftQuantity }), terms.Subtotal, terms.DeliveryFee, terms.Total,
            terms.Currency, terms.DeliveryFeeBasis };
    }

    public async Task<object> List(Actor actor, string orderId)
    {
        var order = await Order(actor, orderId);
        var eligibility = await Eligibility(order);
        var list = await db.OrderAmendments.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId).ToListAsync();
        return new { items = list.OrderByDescending(x => x.ProposedAt).Select(x => View(actor, x, order, eligibility)), orderVersion = order.Version,
            canPropose = actor.IsAdmin && eligibility.Length == 0 && !list.Any(x => x.Status == "PENDING" && x.ExpiresAt > DateTimeOffset.UtcNow),
            eligibilityReason = eligibility,
            notice = "La proposition ne modifie ni les produits commandés, ni les réservations. Seul l’accord explicite d’une personne habilitée du magasin remplace le contrat. Les prix et les avantages doivent encore être identiques au moment de l’accord." };
    }

    public async Task<object> Propose(Actor actor, string orderId, ProposeOrderAmendmentInput input)
    {
        actor.Admin();
        var order = await Order(actor, orderId);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d’idempotence");
        var lines = CatalogService.ValidateLines(input.Items);
        var reason = Check.Text(input.Reason, 1000, "Motif de la modification");
        Check.That(reason.Length >= 8, "Expliquez la modification en au moins 8 caractères.");
        var expiry = input.ExpiresAt.ToUniversalTime();
        var hash = Tokens.Hash(Serialize(new { actor.Id, input.Version, lines, reason, expiresAt = expiry }));
        var replay = await db.OrderAmendments.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId && x.IdempotencyKey == key);
        if (replay != null)
        {
            Check.That(replay.RequestHash == hash, "Cette clé correspond à une autre proposition.", "IDEMPOTENCY_CONFLICT", 409);
            return new { proposal = View(actor, replay, order, await Eligibility(order)), order = await orders.View(actor, order) };
        }
        Check.That(order.Version == input.Version, "La commande a changé. Rechargez-la.", "VERSION_CONFLICT", 409);
        var eligibility = await Eligibility(order);
        Check.That(eligibility.Length == 0, eligibility, "AMENDMENT_NOT_ALLOWED", 409);
        Check.That(expiry >= DateTimeOffset.UtcNow.AddMinutes(5) && expiry <= DateTimeOffset.UtcNow.AddHours(24), "La proposition doit expirer entre 5 minutes et 24 heures.");
        Check.That(await db.OrderAmendments.CountAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId) < 50,
            "La limite de propositions conservées pour cette commande est atteinte.", "AMENDMENT_QUOTA", 409);
        var pending = await db.OrderAmendments.Where(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == orderId && x.Status == "PENDING").ToListAsync();
        Check.That(pending.All(x => x.ExpiresAt <= DateTimeOffset.UtcNow), "Une proposition attend déjà la décision du magasin. Retirez-la avant d’en créer une autre.", "AMENDMENT_PENDING", 409);
        foreach (var old in pending) { old.Status = "EXPIRED"; old.Version++; old.DecidedAt = DateTimeOffset.UtcNow; old.DecisionReason = "Délai de réponse expiré."; }
        // Flush expired pending rows before inserting the replacement's filtered unique key.
        if (pending.Count > 0) await db.SaveChangesAsync();
        var plan = await catalog.BuildQuote(actor, lines, merchantId: order.AccountId, issueConsent: false);
        var after = OrderService.AmendmentTermsFor(plan, lines, order.DeliveryFee!.Value);
        var afterJson = Serialize(after);
        var proposal = new OrderAmendment { WorkspaceId = actor.WorkspaceId, OrderId = order.Id, MerchantId = order.AccountId,
            ProposedBy = actor.Id, ProposerName = actor.Name, Reason = reason, ExpiresAt = expiry,
            IdempotencyKey = key, RequestHash = hash, LinesJson = Serialize(lines),
            BeforeJson = Serialize(await orders.AmendmentBeforeTerms(actor, order)), AfterJson = afterJson, TermsHash = Tokens.Hash(afterJson) };
        db.OrderAmendments.Add(proposal);
        orders.Event(actor, order, "AMENDMENT_PROPOSED", $"Proposition {proposal.Id}. {reason} Le contrat initial et ses réservations restent en vigueur.");
        proposal.BaseOrderVersion = order.Version;
        await db.SaveChangesAsync();
        return new { proposal = View(actor, proposal, order, ""), order = await orders.View(actor, order) };
    }

    public async Task<object> Decide(Actor actor, string orderId, string proposalId, DecideOrderAmendmentInput input, string decision)
    {
        if (decision == "WITHDRAWN") actor.Admin();
        else { actor.RequireRoles("MERCHANT"); actor.ShopWrite(); actor.Approved(); }
        var order = await Order(actor, orderId);
        var proposal = await db.OrderAmendments.FirstOrDefaultAsync(x => x.Id == proposalId && x.OrderId == orderId && x.WorkspaceId == actor.WorkspaceId);
        Check.That(proposal != null, "Proposition introuvable.", "NOT_FOUND", 404);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d’idempotence");
        var reason = Check.Text(input.Reason, 1000, "Motif de la décision", decision != "ACCEPTED");
        var hash = Tokens.Hash(Serialize(new { actor.Id, decision, input.Version, input.TermsHash, reason }));
        if (proposal!.Status != "PENDING")
        {
            Check.That(proposal.Status == decision && proposal.DecisionKey == key && proposal.DecisionRequestHash == hash,
                "Cette proposition a déjà reçu une autre décision.", "AMENDMENT_ALREADY_DECIDED", 409);
            return new { proposal = View(actor, proposal, order, await Eligibility(order)), order = await orders.View(actor, order) };
        }
        Check.That(proposal.Version == input.Version, "La proposition a changé. Rechargez-la.", "VERSION_CONFLICT", 409);
        var eligibility = await Eligibility(order);
        if (decision != "WITHDRAWN")
            Check.That(Applicability(proposal, order, eligibility) == "PENDING",
                "La proposition est expirée ou la commande a évolué. Le grossiste doit la retirer et actualiser les conditions.", "AMENDMENT_STALE", 409);
        if (decision == "ACCEPTED")
        {
            Check.That(input.TermsHash == proposal.TermsHash, "Relisez les produits, prix et avantages proposés avant de confirmer.", "AMENDMENT_CONSENT_REQUIRED", 409);
            var lines = JsonSerializer.Deserialize<List<LineInput>>(proposal.LinesJson, Json)!;
            await orders.ApplyAcceptedAmendment(actor, order, lines, proposal.TermsHash);
        }
        proposal.Status = decision; proposal.Version++;
        proposal.DecidedBy = actor.Id; proposal.DecisionName = actor.Name; proposal.DecidedAt = DateTimeOffset.UtcNow;
        proposal.DecisionReason = reason; proposal.DecisionKey = key; proposal.DecisionRequestHash = hash;
        orders.Event(actor, order, "AMENDMENT_" + decision, $"Proposition {proposal.Id}. " + (decision == "ACCEPTED" ? "Nouvelles conditions relues et acceptées par le magasin; ancien contrat conservé dans le dossier." : reason));
        if (decision == "ACCEPTED") proposal.AcceptedOrderVersion = order.Version;
        await db.SaveChangesAsync();
        return new { proposal = View(actor, proposal, order, await Eligibility(order)), order = await orders.View(actor, order) };
    }
}
