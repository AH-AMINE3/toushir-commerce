using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class CrmService(WholesaleDb db, OrderService orders)
{
    private static void Version(int actual, int expected) => Check.That(actual == expected,
        "Ce dossier a changé. Actualisez-le avant de continuer.", "VERSION_CONFLICT", 409);
    private static string Reason(string value)
    {
        var reason = Check.Text(value, 800, "Motif");
        Check.That(reason.Length >= 8, "Précisez le motif (au moins 8 caractères)."); return reason;
    }
    private void Event(Actor actor, string shopId, string kind, string subject, object detail)
    {
        var at = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(detail);
        db.CrmEvents.Add(new() { WorkspaceId = actor.WorkspaceId, ShopId = shopId, ActorId = actor.Id,
            Kind = kind, SubjectId = subject, Details = json, At = at, UtcTicks = at.UtcTicks });
        orders.Audit(actor, kind, subject, json);
    }
    private async Task<Account> Shop(string workspaceId, string id)
    {
        var shop = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == workspaceId
            && x.Role == "MERCHANT" && !db.ShopMemberships.Any(m => m.AccountId == x.Id));
        Check.That(shop != null, "Magasin introuvable.", "NOT_FOUND", 404); return shop!;
    }
    private async Task<CrmPortfolio?> StaffShop(Actor actor, string shopId)
    {
        actor.RequireRoles("ADMIN", "SALES");
        var portfolio = await db.CrmPortfolios.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shopId);
        Check.That(actor.IsAdmin || portfolio?.AdvisorId == actor.Id, "Magasin introuvable.", "NOT_FOUND", 404);
        await Shop(actor.WorkspaceId, shopId); return portfolio;
    }
    private async Task<Account> Advisor(string workspaceId, string id)
    {
        var person = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id
            && (x.Role == "ADMIN" || x.Role == "SALES") && x.ApprovalStatus == "APPROVED");
        Check.That(person != null && await ShopAccess.Resolve(db, person) != null, "Ce conseiller n'est pas disponible.", "ADVISOR_UNAVAILABLE", 409);
        return person!;
    }
    public async Task<object> Overview(Actor actor)
    {
        actor.RequireRoles("ADMIN", "SALES");
        var portfolios = await db.CrmPortfolios.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync();
        var shops = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.Role == "MERCHANT"
            && !db.ShopMemberships.Any(m => m.AccountId == x.Id)
            && (actor.Role == "ADMIN" || db.CrmPortfolios.Any(p => p.WorkspaceId == actor.WorkspaceId && p.ShopId == x.Id && p.AdvisorId == actor.Id)))
            .Take(500).ToListAsync();
        var shopIds = shops.Select(x => x.Id).ToList();
        var locations = (await db.Addresses.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && shopIds.Contains(x.AccountId)).ToListAsync())
            .GroupBy(x => x.AccountId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).First());
        var people = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId
            && (x.Role == "ADMIN" || x.Role == "SALES") && x.ApprovalStatus == "APPROVED"
            && (actor.Role == "ADMIN" || x.Id == actor.Id)).ToListAsync();
        var tasks = await db.CrmTasks.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && shopIds.Contains(x.ShopId) && x.Status == "OPEN").ToListAsync();
        return new { canAssign = actor.IsAdmin, shops = shops.OrderBy(x => x.ShopName).Select(x => new {
            x.Id, name = x.ShopName, contact = x.Name, x.Phone, x.ApprovalStatus,
            region = locations.GetValueOrDefault(x.Id)?.Wilaya ?? "", commune = locations.GetValueOrDefault(x.Id)?.Commune ?? "",
            portfolio = portfolios.Where(p => p.ShopId == x.Id).Select(p => new { p.AdvisorId, p.Version }).SingleOrDefault() }),
            advisors = people.Select(x => new { x.Id, x.Name, x.Role }),
            tasks = tasks.OrderBy(x => x.DueAt).Take(100), totalOpenTasks = tasks.Count,
            notice = "Les contacts et visites sont des comptes rendus déclarés. Une note ne vaut ni commande ni preuve de paiement." };
    }
    public async Task<object> Dossier(Actor actor, string shopId)
    {
        var portfolio = await StaffShop(actor, shopId);
        var shop = await Shop(actor.WorkspaceId, shopId);
        var location = (await db.Addresses.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.AccountId == shopId).ToListAsync()).OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).FirstOrDefault();
        var advisor = portfolio == null ? null : await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == portfolio.AdvisorId && x.WorkspaceId == actor.WorkspaceId);
        var tasks = await db.CrmTasks.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shopId).ToListAsync();
        var contacts = await db.CrmContacts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shopId).ToListAsync();
        var authorIds = contacts.Select(x => x.ActorId).Distinct().ToList();
        var authors = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && authorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var events = await db.CrmEvents.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shopId)
            .OrderByDescending(x => x.UtcTicks).Take(80).ToListAsync();
        var requests = await db.Orders.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.AccountId == shopId).ToListAsync();
        return new { shop = new { shop.Id, name = shop.ShopName, contact = shop.Name, shop.Phone, shop.ApprovalStatus, region = location?.Wilaya ?? "", commune = location?.Commune ?? "" },
            portfolio = portfolio == null ? null : new { portfolio.AdvisorId, advisorName = advisor?.Name, portfolio.Version },
            tasks = tasks.OrderBy(x => x.Status != "OPEN").ThenBy(x => x.DueAt).Take(100),
            contacts = contacts.OrderByDescending(x => x.RecordedAt).Take(50).Select(x => new { x.Id, x.WorkspaceId, x.ShopId, x.TaskId, x.Kind, x.Outcome, x.Note, x.NextAction, x.ObservedAt, x.ActorId, actorName = authors.GetValueOrDefault(x.ActorId), x.RecordedAt }), events,
            assistance = await AssistanceList(actor.WorkspaceId, shopId),
            orders = requests.OrderByDescending(x => x.CreatedAt).Take(20).Select(x => new { x.Id, x.Number, x.Status, x.Total, x.QuoteVersion, x.CreatedBy, x.CreatedAt }),
            notice = "Le portefeuille donne accès à ce dossier commercial, sans encaissement, GPS, administration ni acceptation à la place du magasin." };
    }
    public async Task<object> Assign(Actor actor, string shopId, AssignPortfolioInput input)
    {
        actor.Admin(); await Shop(actor.WorkspaceId, shopId); await Advisor(actor.WorkspaceId, input.AdvisorId);
        var reason = Reason(input.Reason);
        var portfolio = await db.CrmPortfolios.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shopId);
        var previous = portfolio?.AdvisorId;
        Version(portfolio?.Version ?? 0, input.Version);
        if (portfolio == null) { portfolio = new() { WorkspaceId = actor.WorkspaceId, ShopId = shopId, AdvisorId = input.AdvisorId }; db.CrmPortfolios.Add(portfolio); }
        else { portfolio.AdvisorId = input.AdvisorId; portfolio.Version++; }
        portfolio.UpdatedBy = actor.Id; portfolio.UpdatedAt = DateTimeOffset.UtcNow;
        var grants = await db.CrmAssistances.Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shopId && x.UsedAt == null && x.RevokedAt == null).ToListAsync();
        foreach (var grant in grants) { grant.RevokedAt = portfolio.UpdatedAt; grant.RevokedBy = actor.Id; grant.Version++; }
        Event(actor, shopId, "CRM_PORTFOLIO_ASSIGNED", portfolio.Id, new { previous, input.AdvisorId, portfolio.Version, reason });
        await db.SaveChangesAsync(); return await Dossier(actor, shopId);
    }
    private static string Priority(string value)
    {
        var priority = Check.Text(value, 20, "Priorité").ToUpperInvariant();
        Check.That(priority is "NORMAL" or "HIGH", "Priorité inconnue."); return priority;
    }
    private static void Schedule(string kind, DateTimeOffset dueAt, DateTimeOffset? visitEnd)
    {
        Check.That(dueAt.Offset == TimeSpan.Zero && dueAt >= DateTimeOffset.UtcNow.AddMinutes(-5) && dueAt <= DateTimeOffset.UtcNow.AddDays(365),
            "L'échéance UTC doit se situer entre maintenant et un an.");
        Check.That(kind == "VISIT" ? visitEnd.HasValue && visitEnd.Value.Offset == TimeSpan.Zero
            && visitEnd.Value >= dueAt.AddMinutes(15) && visitEnd.Value <= dueAt.AddHours(8) : visitEnd == null,
            "Une visite demande un créneau de 15 minutes à 8 heures ; un contact n'a pas de fin de visite.");
    }
    public async Task<object> CreateTask(Actor actor, string shopId, CreateCrmTaskInput input)
    {
        var portfolio = await StaffShop(actor, shopId);
        Check.That(portfolio != null, "Affectez un conseiller au magasin avant de planifier une tâche.", "PORTFOLIO_REQUIRED", 409);
        await Advisor(actor.WorkspaceId, portfolio!.AdvisorId);
        var kind = Check.Text(input.Kind, 20, "Type").ToUpperInvariant(); Check.That(kind is "CONTACT" or "VISIT", "Type de tâche inconnu.");
        Schedule(kind, input.DueAt, input.VisitEnd);
        Check.That(await db.CrmTasks.CountAsync(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shopId && x.Status == "OPEN") < 100, "Maximum 100 tâches ouvertes pour ce magasin.");
        var task = new CrmTask { WorkspaceId = actor.WorkspaceId, ShopId = shopId, AssignedTo = portfolio.AdvisorId,
            Kind = kind, Title = Check.Text(input.Title, 160, "Objet"), Priority = Priority(input.Priority), DueAt = input.DueAt, VisitEnd = input.VisitEnd, CreatedBy = actor.Id };
        db.CrmTasks.Add(task); Event(actor, shopId, "CRM_TASK_CREATED", task.Id, new { task.Kind, task.AssignedTo, task.DueAt, task.VisitEnd });
        await db.SaveChangesAsync(); return task;
    }
    private async Task<CrmTask> Task(Actor actor, string id, int version)
    {
        actor.RequireRoles("ADMIN", "SALES");
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(task != null, "Tâche introuvable.", "NOT_FOUND", 404);
        await StaffShop(actor, task!.ShopId); Version(task.Version, version);
        Check.That(task.Status == "OPEN", "Cette tâche est déjà terminée.", "TASK_CLOSED", 409); return task;
    }
    public async Task<object> Reschedule(Actor actor, string id, RescheduleCrmTaskInput input)
    {
        var task = await Task(actor, id, input.Version); var reason = Reason(input.Reason);
        Schedule(task.Kind, input.DueAt, input.VisitEnd);
        var portfolio = await StaffShop(actor, task.ShopId);
        Check.That(portfolio != null, "Affectez un conseiller avant de replanifier.", "PORTFOLIO_REQUIRED", 409);
        task.AssignedTo = portfolio!.AdvisorId; task.Title = Check.Text(input.Title, 160, "Objet");
        task.Priority = Priority(input.Priority); task.DueAt = input.DueAt; task.VisitEnd = input.VisitEnd;
        task.Version++; task.UpdatedAt = DateTimeOffset.UtcNow;
        Event(actor, task.ShopId, "CRM_TASK_RESCHEDULED", id, new { reason, task.DueAt, task.VisitEnd, task.AssignedTo });
        await db.SaveChangesAsync(); return task;
    }
    private static string Outcome(string kind, string value)
    {
        var outcome = Check.Text(value, 30, "Issue").ToUpperInvariant();
        Check.That(kind == "VISIT" ? outcome is "VISIT_COMPLETED" or "VISIT_MISSED" : outcome is "RESPONDED" or "CALL_BACK" or "UNAVAILABLE",
            "Choisissez une issue adaptée au contact ou à la visite."); return outcome;
    }
    private async Task<CrmContact> Contact(Actor actor, string shopId, string kind, string outcome, string note,
        string nextAction, DateTimeOffset observedAt, DateTimeOffset? followUpAt, string? taskId = null)
    {
        var portfolio = await StaffShop(actor, shopId);
        Check.That(portfolio != null, "Affectez un conseiller avant de consigner un contact.", "PORTFOLIO_REQUIRED", 409);
        Check.That(observedAt.Offset == TimeSpan.Zero && observedAt <= DateTimeOffset.UtcNow.AddMinutes(1) && observedAt >= DateTimeOffset.UtcNow.AddDays(-1),
            "Indiquez une observation UTC des dernières 24 heures.");
        var contact = new CrmContact { WorkspaceId = actor.WorkspaceId, ShopId = shopId, TaskId = taskId,
            Kind = kind, Outcome = Outcome(kind, outcome), Note = Check.Text(note, 1500, "Compte rendu"),
            NextAction = Check.Text(nextAction, 300, "Prochaine action"), ObservedAt = observedAt, ActorId = actor.Id };
        if (followUpAt != null) await CreateTask(actor, shopId, new("CONTACT", contact.NextAction, "NORMAL", followUpAt.Value, null));
        db.CrmContacts.Add(contact); Event(actor, shopId, "CRM_CONTACT_RECORDED", contact.Id,
            new { taskId, kind, contact.Outcome, observedAt, followUpAt, evidence = "SELF_REPORTED" });
        return contact;
    }
    public async Task<object> LogContact(Actor actor, string shopId, LogCrmContactInput input)
    {
        var kind = Check.Text(input.Kind, 20, "Type").ToUpperInvariant(); Check.That(kind is "CONTACT" or "VISIT", "Type de contact inconnu.");
        var contact = await Contact(actor, shopId, kind, input.Outcome, input.Note, input.NextAction, input.ObservedAt, input.FollowUpAt);
        await db.SaveChangesAsync(); return contact;
    }
    public async Task<object> Finish(Actor actor, string id, FinishCrmTaskInput input)
    {
        var task = await Task(actor, id, input.Version);
        if (task.Kind == "VISIT") Check.That(input.ObservedAt >= task.DueAt.AddMinutes(-15), "La visite n'a pas encore commencé.", "VISIT_NOT_STARTED", 409);
        var contact = await Contact(actor, task.ShopId, task.Kind, input.Outcome, input.Note, input.NextAction, input.ObservedAt, input.FollowUpAt, task.Id);
        task.Status = "DONE"; task.CompletedBy = actor.Id; task.CompletedAt = DateTimeOffset.UtcNow; task.UpdatedAt = task.CompletedAt.Value; task.Version++;
        Event(actor, task.ShopId, "CRM_TASK_COMPLETED", id, new { contactId = contact.Id, contact.Outcome });
        await db.SaveChangesAsync(); return new { task, contact };
    }
    public async Task<object> Cancel(Actor actor, string id, CancelCrmTaskInput input)
    {
        var task = await Task(actor, id, input.Version); var reason = Reason(input.Reason);
        task.Status = "CANCELLED"; task.CompletedBy = actor.Id; task.CompletedAt = DateTimeOffset.UtcNow; task.UpdatedAt = task.CompletedAt.Value; task.Version++;
        Event(actor, task.ShopId, "CRM_TASK_CANCELLED", id, new { reason }); await db.SaveChangesAsync(); return task;
    }
    private static string AddressSnapshot(Address address) => JsonSerializer.Serialize(new {
        id = address.Id, label = address.Label, wilaya = address.Wilaya, commune = address.Commune, street = address.Street });
    private async Task<string> AssistanceStatus(CrmAssistance grant)
    {
        if (grant.UsedAt != null) return "USED";
        if (grant.RevokedAt != null) return "REVOKED";
        if (grant.ExpiresAt <= DateTimeOffset.UtcNow) return "EXPIRED";
        var portfolio = await db.CrmPortfolios.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == grant.WorkspaceId && x.ShopId == grant.ShopId);
        if (portfolio?.AdvisorId != grant.AdvisorId || portfolio.Version != grant.PortfolioVersion) return "AUTHORITY_ENDED";
        var issuer = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == grant.IssuedBy && x.WorkspaceId == grant.WorkspaceId);
        var actor = issuer == null ? null : await ShopAccess.Resolve(db, issuer);
        if (actor == null || !actor.CanManageShop || actor.ResourceOwnerId != grant.ShopId || actor.ShopRole != grant.IssuerRole
            || actor.MembershipVersion != grant.IssuerMembershipVersion || actor.ApprovalStatus != "APPROVED") return "AUTHORITY_ENDED";
        var advisor = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == grant.AdvisorId && x.WorkspaceId == grant.WorkspaceId);
        if (advisor == null || advisor.ApprovalStatus != "APPROVED" || advisor.Role is not ("ADMIN" or "SALES") || await ShopAccess.Resolve(db, advisor) == null) return "AUTHORITY_ENDED";
        var address = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == grant.AddressId && x.WorkspaceId == grant.WorkspaceId && x.AccountId == grant.ShopId);
        return address == null || AddressSnapshot(address) != grant.AddressJson ? "ADDRESS_CHANGED" : "AVAILABLE";
    }
    private async Task<List<object>> AssistanceList(string workspaceId, string shopId)
    {
        var grants = (await db.CrmAssistances.AsNoTracking().Where(x => x.WorkspaceId == workspaceId && x.ShopId == shopId).ToListAsync())
            .OrderByDescending(x => x.CreatedAt).Take(30).ToList();
        var personIds = grants.SelectMany(x => new[] { x.IssuedBy, x.AdvisorId }).Distinct().ToList();
        var names = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == workspaceId && personIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
        var result = new List<object>();
        foreach (var grant in grants) result.Add(new { grant.Id, grant.AdvisorId, grant.IssuedBy, advisorName = names.GetValueOrDefault(grant.AdvisorId), issuerName = names.GetValueOrDefault(grant.IssuedBy), grant.CreatedAt, grant.ExpiresAt, grant.Version,
            grant.UsedAt, grant.UsedBy, grant.RevokedAt, grant.OrderId, status = await AssistanceStatus(grant),
            scope = "REQUEST_QUOTE_ONCE", address = JsonSerializer.Deserialize<object>(grant.AddressJson),
            items = JsonSerializer.Deserialize<object>(grant.ProductSnapshotsJson), grant.Note });
        return result;
    }
    public async Task<object> MerchantAssistance(Actor actor)
    {
        actor.RequireRoles("MERCHANT");
        var portfolio = await db.CrmPortfolios.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == actor.ResourceOwnerId);
        var advisor = portfolio == null ? null : await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == portfolio.AdvisorId && x.WorkspaceId == actor.WorkspaceId);
        var available = advisor is { ApprovalStatus: "APPROVED", Role: "ADMIN" or "SALES" } && await ShopAccess.Resolve(db, advisor) != null;
        var shop = await Shop(actor.WorkspaceId, actor.ResourceOwnerId);
        var validUntil = await ShopAccess.ValidUntil(db, shop);
        return new { canAuthorize = actor.CanManageShop,
            validUntil = validUntil == DateTimeOffset.MaxValue ? null : validUntil,
            advisor = available ? new { advisor!.Id, advisor.Name, portfolio!.Version } : null,
            items = await AssistanceList(actor.WorkspaceId, actor.ResourceOwnerId),
            notice = "Autorisez une seule demande de devis pour les produits et l'adresse affichés. Le conseiller ne peut ni accepter le devis, ni payer ou réceptionner à votre place." };
    }
    public async Task<object> Grant(Actor actor, GrantCrmAssistanceInput input)
    {
        actor.ManageShop(); actor.Approved();
        var shop = await Shop(actor.WorkspaceId, actor.ResourceOwnerId);
        var portfolio = await db.CrmPortfolios.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shop.Id);
        Check.That(portfolio != null && portfolio.AdvisorId == input.AdvisorId, "Votre conseiller a changé. Actualisez le dossier.", "ADVISOR_CHANGED", 409);
        Version(portfolio!.Version, input.PortfolioVersion); await Advisor(actor.WorkspaceId, input.AdvisorId);
        var until = await ShopAccess.ValidUntil(db, shop); var now = DateTimeOffset.UtcNow;
        Check.That(input.ExpiresAt.Offset == TimeSpan.Zero && input.ExpiresAt > now.AddMinutes(5) && input.ExpiresAt <= now.AddDays(7)
            && until != null && input.ExpiresAt <= until, "L'autorisation doit durer de 5 minutes à 7 jours sans dépasser l'accès du magasin.");
        var lines = CatalogService.ValidateLines(input.Items); Check.That(lines.Count <= 50 && lines.Sum(x => (long)x.Quantity) <= 100000,
            "Une demande assistée est limitée à 50 produits et 100 000 unités.");
        var ids = lines.Select(x => x.ProductId).ToList();
        var products = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        Check.That(products.Count == lines.Count, "Un produit n'est plus disponible.", "PRODUCT_NOT_FOUND", 404);
        var address = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.AddressId && x.WorkspaceId == actor.WorkspaceId && x.AccountId == shop.Id);
        Check.That(address != null, "Choisissez une adresse du magasin.", "ADDRESS_REQUIRED");
        Check.That((await db.CrmAssistances.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.ShopId == shop.Id && x.UsedAt == null && x.RevokedAt == null).ToListAsync())
            .Count(x => x.ExpiresAt > now) < 30, "Maximum 30 autorisations en attente.");
        var grant = new CrmAssistance { WorkspaceId = actor.WorkspaceId, ShopId = shop.Id, AdvisorId = portfolio.AdvisorId,
            PortfolioVersion = portfolio.Version, IssuedBy = actor.Id, IssuerRole = actor.ShopRole, IssuerMembershipVersion = actor.MembershipVersion,
            AddressId = address!.Id, AddressJson = AddressSnapshot(address), LinesJson = JsonSerializer.Serialize(lines),
            ProductSnapshotsJson = JsonSerializer.Serialize(lines.Select(x => new { productId = x.ProductId, name = products[x.ProductId].Name,
                imageUrl = products[x.ProductId].ImageUrl, quantity = x.Quantity })),
            Note = Check.Text(input.Note, 1500, "Note du magasin", false), ExpiresAt = input.ExpiresAt };
        db.CrmAssistances.Add(grant); Event(actor, shop.Id, "CRM_ASSISTANCE_AUTHORIZED", grant.Id,
            new { grant.AdvisorId, grant.PortfolioVersion, grant.IssuedBy, grant.ExpiresAt, scope = "REQUEST_QUOTE_ONCE", lines });
        await db.SaveChangesAsync(); return await MerchantAssistance(actor);
    }
    public async Task<object> RevokeAssistance(Actor actor, string id, RevokeCrmAssistanceInput input)
    {
        actor.ManageShop();
        var grant = await db.CrmAssistances.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId && x.ShopId == actor.ResourceOwnerId);
        Check.That(grant != null, "Autorisation introuvable.", "NOT_FOUND", 404); Version(grant!.Version, input.Version);
        Check.That(grant.UsedAt == null && grant.RevokedAt == null, "Cette autorisation est déjà terminée.", "ASSISTANCE_UNAVAILABLE", 409);
        var reason = Reason(input.Reason); grant.RevokedAt = DateTimeOffset.UtcNow; grant.RevokedBy = actor.Id; grant.Version++;
        Event(actor, grant.ShopId, "CRM_ASSISTANCE_REVOKED", id, new { reason }); await db.SaveChangesAsync(); return await MerchantAssistance(actor);
    }
    public async Task<object> UseAssistance(Actor actor, string id, UseCrmAssistanceInput input)
    {
        actor.RequireRoles("ADMIN", "SALES");
        var grant = await db.CrmAssistances.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(grant != null, "Autorisation introuvable.", "NOT_FOUND", 404);
        await StaffShop(actor, grant!.ShopId);
        Check.That(grant.AdvisorId == actor.Id, "Cette autorisation désigne un autre conseiller.", "ASSISTANCE_ADVISOR", 403);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d'idempotence"); var hash = Tokens.Hash(key);
        if (grant.UsedAt != null)
        {
            Check.That(grant.UseKeyHash == hash && grant.UsedBy == actor.Id, "Cette autorisation a déjà été utilisée.", "ASSISTANCE_USED", 409);
            return await AssistedOrder(actor, grant);
        }
        Version(grant.Version, input.Version);
        Check.That(await AssistanceStatus(grant) == "AVAILABLE", "Cette autorisation n'est plus utilisable. Demandez au magasin d'en créer une nouvelle.", "ASSISTANCE_UNAVAILABLE", 409);
        var shop = await Shop(actor.WorkspaceId, grant.ShopId);
        // The common RFQ core receives only the exact merchant-authorized terms.
        // The assistant cannot supply items, prices, mode or an alternate address here.
        var order = await orders.CreateAuthorizedShopRfq(actor, shop, grant);
        grant.OrderId = order.Id; grant.UsedAt = DateTimeOffset.UtcNow; grant.UsedBy = actor.Id; grant.UseKeyHash = hash; grant.Version++;
        Event(actor, grant.ShopId, "CRM_ASSISTANCE_USED", id, new { orderId = order.Id, grant.IssuedBy, grant.AdvisorId });
        await db.SaveChangesAsync(); return await AssistedOrder(actor, grant);
    }
    private async Task<object> AssistedOrder(Actor actor, CrmAssistance grant)
    {
        var order = await db.Orders.AsNoTracking().FirstAsync(x => x.Id == grant.OrderId && x.WorkspaceId == actor.WorkspaceId && x.AccountId == grant.ShopId);
        return new { order.Id, order.Number, order.Status, order.Total, order.CreatedBy, assistanceId = grant.Id,
            authorizedBy = grant.IssuedBy, notice = "Demande de devis transmise. Le magasin devra accepter le devis chiffré avant toute préparation." };
    }
}
