using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class OperationsService(WholesaleDb db, OrderService orders, RealtimeChanges changes, PasswordHasher<Account> hasher, WarehouseService warehouse)
{
    internal static Actor Delegate(Actor actor) => actor with { Role = "ADMIN" };
    private static void MatchVersion(PurchaseOrder order, int version) => Check.That(version == order.Version, "Cette commande a évolué. Rechargez-la avant de continuer.", "VERSION_CONFLICT", 409);
    private async Task<PurchaseOrder> Find(Actor actor, string id)
    {
        var order = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId);
        Check.That(order != null, "Commande introuvable.", "NOT_FOUND", 404);
        if (actor.Role == "MERCHANT")
            Check.That(order.AccountId == actor.ResourceOwnerId, "Commande introuvable.", "NOT_FOUND", 404);
        else if (actor.Role == "DRIVER")
            Check.That(await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == id && x.DriverId == actor.Id), "Commande introuvable.", "NOT_FOUND", 404);
        else
            actor.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "FINANCE");
        return order;
    }
    private Task<DeliveryAssignment?> Assignment(PurchaseOrder order) => db.DeliveryAssignments.FirstOrDefaultAsync(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id);
    private async Task<DeliveryAssignment> Assigned(Actor actor, PurchaseOrder order)
    {
        actor.RequireRoles("DRIVER");
        var assignment = await Assignment(order);
        Check.That(assignment?.DriverId == actor.Id, "Cette livraison ne vous est pas affectée.", "FORBIDDEN", 403);
        return assignment!;
    }
    private async Task<object?> AssignmentView(PurchaseOrder order)
    {
        var assignment = await Assignment(order);
        if (assignment == null)
            return null;
        var driver = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == assignment.DriverId);
        return new
        {
            assignment.Id,
            assignment.RouteId,
            assignment.StopRank,
            custodyMode = assignment.RouteId.Length == 0 ? "PER_ORDER_FALLBACK" : "ROUTE_MANIFEST",
            assignment.DriverId,
            driverName = driver.Name,
            driverPhone = driver.Phone,
            assignment.Status,
            assignment.AssignedAt,
            assignment.PickedUpAt,
            assignment.ArrivedAt,
            assignment.FailureReason
        };
    }
    internal async Task<object> OperationalOrder(Actor actor, PurchaseOrder order)
    {
        var merchant = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == order.AccountId);
        return new
        {
            order.Id,
            order.Number,
            order.Status,
            order.PaymentStatus,
            order.Total,
            order.Version,
            order.CreatedAt,
            order.UpdatedAt,
            merchant = new
            {
                merchant.Id,
                merchant.Name,
                merchant.ShopName,
                merchant.Phone
            },
            items = order.Items.Select(x => new { x.Id, x.ProductId, x.Name, x.Brand, x.ImageUrl, x.Quantity, x.PreparedQuantity, x.AcceptedQuantity, x.RefusedQuantity, x.UnitPrice, x.LineTotal }),
            address = OrderService.ReadAddress(order.AddressJson),
            assignment = await AssignmentView(order),
            balance = new
            {
                due = order.Total,
                collected = order.Collected,
                remaining = order.Total is decimal total ? Math.Max(0, total - order.Collected) : (decimal?)null,
                refundDue = order.Total is decimal due ? Math.Max(0, order.Collected - due) : 0
            },
            order.Note,
            order.AdminNote,
            promotions = new
            {
                items = await db.PromotionSnapshots.AsNoTracking().Where(x => x.WorkspaceId == order.WorkspaceId && x.OrderId == order.Id && x.QuoteVersion == order.QuoteVersion && x.Status != "SUPERSEDED").ToListAsync()
            },
            currency = "DZD"
        };
    }
    public async Task<object> List(Actor actor, bool courier = false)
    {
        if (courier)
            actor.RequireRoles("DRIVER");
        else
            actor.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "FINANCE");
        var query = db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == actor.WorkspaceId);
        if (courier)
            query = query.Where(x => db.DeliveryAssignments.Any(assignment => assignment.WorkspaceId == actor.WorkspaceId && assignment.OrderId == x.Id && assignment.DriverId == actor.Id));
        var list = (await query.ToListAsync()).OrderByDescending(x => x.CreatedAt).Take(250).ToList();
        var result = new List<object>();
        foreach (var order in list)
            result.Add(await OperationalOrder(actor, order));
        return new
        {
            items = result,
            total = result.Count
        };
    }
    public async Task<object> Team(Actor actor)
    {
        actor.RequireRoles("ADMIN", "DISPATCHER", "WAREHOUSE");
        var team = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.Role != "MERCHANT").ToListAsync();
        return new
        {
            items = team.Select(x => new { x.Id, x.Name, x.Role, x.Phone, x.ApprovalStatus })
        };
    }
    public async Task<object> CreateTeam(Actor actor, CreateTeamInput input)
    {
        actor.Admin();
        var role = input.Role.ToUpperInvariant();
        Check.That(role is "WAREHOUSE" or "DISPATCHER" or "FINANCE" or "DRIVER" or "SALES", "Rôle inconnu.");
        Check.That(input.Password is { Length: >= 14 and <= 128 }, "Un mot de passe de 14 à 128 caractères est requis.");
        var email = Check.Text(input.Email, 254, "E-mail").ToLowerInvariant();
        Check.That(System.Net.Mail.MailAddress.TryCreate(email, out _), "E-mail invalide.");
        Check.That(!await db.Accounts.AnyAsync(x => x.Email == email), "Cette adresse est déjà utilisée.", "EMAIL_EXISTS", 409);
        var account = new Account { WorkspaceId = actor.WorkspaceId, Name = Check.Text(input.Name, 100, "Nom"), Email = email, Role = role, Phone = Check.Text(input.Phone, 30, "Téléphone", false), ApprovalStatus = "APPROVED" };
        account.PasswordHash = hasher.HashPassword(account, input.Password);
        db.Accounts.Add(account);
        orders.Audit(actor, "TEAM_MEMBER_CREATED", account.Id, role);
        await db.SaveChangesAsync();
        return AuthService.User(account);
    }
    public async Task<object> StaffOrder(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "FINANCE");
        var order = await Find(actor, id);
        return await orders.View(Delegate(actor), order);
    }
    public async Task<object> Dashboard(Actor actor)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "FINANCE");
        var list = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync();
        var locations = await db.DriverLocations.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.Consent).ToListAsync();
        return new
        {
            totalOrders = list.Count,
            awaitingQuote = list.Count(x => x.Status == "REQUESTED"),
            preparing = list.Count(x => x.Status is "CONFIRMED" or "PREPARING"),
            ready = list.Count(x => x.Status == "READY"),
            enRoute = list.Count(x => x.Status == "OUT_FOR_DELIVERY"),
            delivered = list.Count(x => x.Status is "DELIVERED" or "PARTIALLY_DELIVERED"),
            preparedUnits = list.Sum(x => x.Items.Sum(l => l.PreparedQuantity)),
            due = list.Where(x => x.Total != null).Sum(x => Math.Max(0, x.Total!.Value - x.Collected)),
            freshDrivers = actor.Role == "FINANCE" ? 0 : locations.Count(x => x.CapturedAt > DateTimeOffset.UtcNow.AddMinutes(-2)),
            at = DateTimeOffset.UtcNow
        };
    }
    public async Task<object> Tracking(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "MERCHANT", "DRIVER");
        var order = await Find(actor, id);
        var assignment = await Assignment(order);
        DriverLocation? location = null;
        if (assignment != null && order.Status == "OUT_FOR_DELIVERY" && assignment.Status != "FAILED")
            location = await db.DriverLocations.AsNoTracking().FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.DriverId == assignment.DriverId && x.Consent);
        var age = location == null ? (double?)null : Math.Max(0, (DateTimeOffset.UtcNow - location.CapturedAt).TotalSeconds);
        return new
        {
            order = await OperationalOrder(actor, order),
            order.Version,
            preparation = new
            {
                prepared = order.Items.Sum(x => x.PreparedQuantity),
                total = order.Items.Sum(x => x.Quantity),
                percentage = order.Items.Sum(x => x.Quantity) == 0 ? 0 : (int)Math.Round(100d * order.Items.Sum(x => x.PreparedQuantity) / order.Items.Sum(x => x.Quantity)),
                items = order.Items.Select(x => new { x.ProductId, x.Name, x.Quantity, x.PreparedQuantity })
            },
            assignment = await AssignmentView(order),
            location = location == null ? null : new
            {
                location.Latitude,
                location.Longitude,
                location.Accuracy,
                location.CapturedAt,
                location.ReceivedAt,
                isFresh = age <= 120,
                ageSeconds = Math.Round(age!.Value),
                source = "DEVICE_GPS"
            },
            receipt = await db.OperationReceipts.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == id).Select(x => new { x.Id, x.MerchantId, x.DriverId, x.AcceptedVersion, x.At }).FirstOrDefaultAsync()
        };
    }
    public async Task<object> Prepare(Actor actor, string id, PrepareInput input)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE");
        var order = await Find(actor, id);
        MatchVersion(order, input.Version);
        Check.That(order.Status is "CONFIRMED" or "PREPARING", "Cette commande n'est pas en préparation.", "INVALID_TRANSITION", 409);
        Check.That(input.Items is { Count: > 0 } && input.Items.Select(x => x.ProductId).Distinct().Count() == input.Items.Count, "Renseignez chaque produit une seule fois.");
        await warehouse.ValidatePreparation(actor, order, input.Items);
        foreach (var item in input.Items)
        {
            var line = order.Items.FirstOrDefault(x => x.ProductId == item.ProductId);
            Check.That(line != null && item.PreparedQuantity >= 0 && item.PreparedQuantity <= line.Quantity, "Quantité préparée invalide.");
            line.PreparedQuantity = item.PreparedQuantity;
        }
        order.Status = order.Items.All(x => x.PreparedQuantity == x.Quantity) && await warehouse.ScansComplete(order) ? "READY" : "PREPARING";
        orders.Event(actor, order, "PREPARATION_UPDATED", $"{order.Items.Sum(x => x.PreparedQuantity)}/{order.Items.Sum(x => x.Quantity)} unités préparées.");
        await db.SaveChangesAsync();
        return await Tracking(actor, id);
    }
    public async Task<object> Assign(Actor actor, string id, AssignDriverInput input)
    {
        actor.RequireRoles("ADMIN", "DISPATCHER");
        var order = await Find(actor, id);
        MatchVersion(order, input.Version);
        Check.That(order.Status is "CONFIRMED" or "PREPARING" or "READY", "L'affectation doit précéder le départ.", "INVALID_TRANSITION", 409);
        var driver = await db.Accounts.FirstOrDefaultAsync(x => x.Id == input.DriverId && x.WorkspaceId == actor.WorkspaceId && x.Role == "DRIVER" && x.ApprovalStatus == "APPROVED");
        Check.That(driver != null, "Livreur actif introuvable.", "NOT_FOUND", 404);
        var assignment = await Assignment(order);
        if (assignment == null)
        {
            assignment = new()
            {
                WorkspaceId = actor.WorkspaceId,
                OrderId = id
            };
            db.DeliveryAssignments.Add(assignment);
        }
        Check.That(assignment.RouteId.Length == 0, "Cette commande appartient à un manifeste de tournée.", "ROUTE_MANIFEST_REQUIRED", 409);
        Check.That(assignment.PickedUpAt == null, "Le livreur a déjà pris en charge la commande.", "INVALID_TRANSITION", 409);
        assignment.DriverId = driver.Id;
        assignment.AssignedBy = actor.Id;
        assignment.AssignedAt = DateTimeOffset.UtcNow;
        assignment.Status = "ASSIGNED";
        orders.Event(actor, order, "DRIVER_ASSIGNED", driver.Name);
        changes.Driver(actor.WorkspaceId, driver.Id);
        await db.SaveChangesAsync();
        return await Tracking(actor, id);
    }
    public async Task<object> Pickup(Actor actor, string id, VersionInput input)
    {
        var order = await Find(actor, id);
        var assignment = await Assigned(actor, order);
        MatchVersion(order, input.Version);
        Check.That(assignment.RouteId.Length == 0, "Scannez le manifeste de tournée pour prendre en charge ces commandes.", "ROUTE_MANIFEST_REQUIRED", 409);
        Check.That(order.Status == "READY" && order.Items.All(x => x.PreparedQuantity == x.Quantity), "La préparation complète doit être vérifiée avant le chargement.", "PREPARATION_INCOMPLETE", 409);
        await orders.Transition(Delegate(actor), id, new("OUT_FOR_DELIVERY", "Prise en charge physique par le livreur affecté."));
        assignment.PickedUpAt = DateTimeOffset.UtcNow;
        assignment.Status = "PICKED_UP";
        await db.SaveChangesAsync();
        return await Tracking(actor, id);
    }
    public async Task<object> Status(Actor actor, string id, CourierStatusInput input)
    {
        var order = await Find(actor, id);
        var assignment = await Assigned(actor, order);
        Check.That(order.Status == "OUT_FOR_DELIVERY" && assignment.PickedUpAt != null, "Cette livraison n'est pas en cours.", "INVALID_TRANSITION", 409);
        var status = input.Status.ToUpperInvariant();
        Check.That(status is "EN_ROUTE" or "ARRIVED" or "FAILED", "État de livraison inconnu.");
        if (status == "ARRIVED")
            Check.That(assignment.Status is "PICKED_UP" or "EN_ROUTE", "Le livreur doit être en route.", "INVALID_TRANSITION", 409);
        if (status == "FAILED")
            assignment.FailureReason = Check.Text(input.Reason, 800, "Motif d'échec");
        assignment.Status = status;
        if (status == "ARRIVED")
            assignment.ArrivedAt = DateTimeOffset.UtcNow;
        orders.Event(actor, order, "COURIER_" + status, Check.Text(input.Reason, 800, "Note", false));
        await db.SaveChangesAsync();
        return await Tracking(actor, id);
    }
    public async Task<object> Location(Actor actor, DriverLocationInput input)
    {
        actor.RequireRoles("DRIVER");
        var current = await db.DriverLocations.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.DriverId == actor.Id);
        if (!input.Consent)
        {
            if (current != null)
            {
                current.Consent = false;
                await db.SaveChangesAsync();
            }
            changes.Driver(actor.WorkspaceId, actor.Id);
            return new
            {
                sharing = false
            };
        }
        Check.That(double.IsFinite(input.Latitude) && double.IsFinite(input.Longitude) && input.Latitude is >= -90 and <= 90 && input.Longitude is >= -180 and <= 180, "Coordonnées GPS invalides.");
        Check.That(double.IsFinite(input.Accuracy) && input.Accuracy is >= 0 and <= 10000, "Précision GPS invalide.");
        var now = DateTimeOffset.UtcNow;
        Check.That(input.CapturedAt >= now.AddMinutes(-5) && input.CapturedAt <= now.AddSeconds(30), "Cette position est trop ancienne ou datée dans le futur.", "LOCATION_STALE", 409);
        Check.That(current == null || input.CapturedAt > current.CapturedAt, "Une position plus récente a déjà été reçue.", "LOCATION_OUT_OF_ORDER", 409);
        Check.That(await db.DeliveryAssignments.AnyAsync(x => x.WorkspaceId == actor.WorkspaceId && x.DriverId == actor.Id && x.PickedUpAt != null && (x.Status == "PICKED_UP" || x.Status == "EN_ROUTE" || x.Status == "ARRIVED")), "Le partage est disponible pendant une tournée active.", "NO_ACTIVE_DELIVERY", 409);
        current ??= new()
        {
            WorkspaceId = actor.WorkspaceId,
            DriverId = actor.Id
        };
        if (db.Entry(current).State == EntityState.Detached)
            db.DriverLocations.Add(current);
        current.Latitude = input.Latitude;
        current.Longitude = input.Longitude;
        current.Accuracy = input.Accuracy;
        current.CapturedAt = input.CapturedAt;
        current.ReceivedAt = now;
        current.Consent = true;
        await db.SaveChangesAsync();
        changes.Driver(actor.WorkspaceId, actor.Id);
        return new
        {
            sharing = true,
            current.CapturedAt,
            current.ReceivedAt,
            isFresh = true
        };
    }
    private static string ReceiptHash(List<DeliveryLineInput> items) => Tokens.Hash(JsonSerializer.Serialize(items.OrderBy(x => x.ProductId).Select(x => new { x.ProductId, x.AcceptedQuantity, x.RefusedQuantity })));
    private static void ValidateReceipt(PurchaseOrder order, List<DeliveryLineInput> items)
    {
        Check.That(items != null && items.Count == order.Items.Count && items.Select(x => x.ProductId).Distinct().Count() == items.Count, "Confirmez chaque produit une seule fois.");
        foreach (var line in order.Items)
        {
            var item = items.FirstOrDefault(x => x.ProductId == line.ProductId);
            Check.That(item != null && item.AcceptedQuantity >= 0 && item.RefusedQuantity >= 0 && item.AcceptedQuantity + item.RefusedQuantity == line.Quantity, "Les unités acceptées et refusées doivent couvrir les unités expédiées.");
        }
    }
    public async Task<object> Challenge(Actor actor, string id, ReceiptProofInput input)
    {
        actor.RequireRoles("MERCHANT");
        actor.Approved();
        actor.ShopWrite();
        var order = await Find(actor, id);
        var assignment = await Assignment(order);
        MatchVersion(order, input.Version);
        ValidateReceipt(order, input.Items);
        await RecallGuard.AssertDistribution(db, order, input.Items.Where(x => x.AcceptedQuantity > 0).Select(x => x.ProductId).ToList());
        Check.That(order.Status == "OUT_FOR_DELIVERY" && assignment?.Status == "ARRIVED", "Le livreur doit avoir confirmé son arrivée.", "INVALID_TRANSITION", 409);
        var pending = await db.ReceiptChallenges.Where(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == id && x.ConsumedAt == null).ToListAsync();
        foreach (var old in pending)
            old.ExpiresAt = DateTimeOffset.UtcNow;
        var token = Tokens.New();
        var route = assignment.RouteId.Length == 0 ? null : await db.DeliveryRoutes.FirstAsync(x => x.Id == assignment.RouteId && x.WorkspaceId == actor.WorkspaceId);
        var challenge = new ReceiptChallenge { RouteId = assignment.RouteId, RouteVersion = route?.Version ?? 0, WorkspaceId = actor.WorkspaceId, OrderId = id, MerchantId = actor.ResourceOwnerId, IssuedBy = actor.Id, DriverId = assignment.DriverId, Version = order.Version, TokenHash = Tokens.Hash(token), ItemsHash = ReceiptHash(input.Items), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3) };
        db.ReceiptChallenges.Add(challenge);
        orders.Audit(actor, "RECEIPT_CHALLENGE_CREATED", id);
        await db.SaveChangesAsync();
        return new
        {
            challenge = token,
            challenge.ExpiresAt,
            orderId = id,
            version = order.Version,
            merchantId = actor.ResourceOwnerId,
            issuedBy = actor.Id,
            driverId = assignment.DriverId,
            routeId = challenge.RouteId,
            routeVersion = challenge.RouteVersion,
            qrPayload = $"toushir://receipt/{id}?code={token}"
        };
    }
    public async Task<object> Complete(Actor actor, string id, CompleteDeliveryInput input)
    {
        var order = await Find(actor, id);
        var assignment = await Assigned(actor, order);
        Check.That(input.Challenge is { Length: 64 }, "Code de réception invalide.", "RECEIPT_INVALID", 409);
        var hash = Tokens.Hash(input.Challenge);
        var proof = await db.ReceiptChallenges.FirstOrDefaultAsync(x => x.TokenHash == hash && x.WorkspaceId == actor.WorkspaceId && x.OrderId == id && x.DriverId == actor.Id && x.MerchantId == order.AccountId);
        Check.That(proof != null && proof.ConsumedAt == null && proof.ExpiresAt > DateTimeOffset.UtcNow && proof.Version == order.Version, "Code expiré, déjà utilisé ou commande modifiée. Le commerçant doit générer un nouveau code.", "RECEIPT_INVALID", 409);
        var issuerId = proof.IssuedBy.Length == 0 ? proof.MerchantId : proof.IssuedBy;
        var issuerAccount = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == issuerId && x.WorkspaceId == actor.WorkspaceId);
        var issuer = issuerAccount == null ? null : await ShopAccess.Resolve(db, issuerAccount);
        Check.That(issuer != null && issuer.CanShopWrite && issuer.ApprovalStatus == "APPROVED" && issuer.ResourceOwnerId == order.AccountId,
            "L'accès de la personne ayant confirmé la réception n'est plus actif. Demandez un nouveau code.", "RECEIPT_INVALID", 409);
        Check.That(proof.RouteId == assignment.RouteId, "Le manifeste de livraison a changé.", "RECEIPT_INVALID", 409);
        if (proof.RouteId.Length > 0)
        {
            var route = await db.DeliveryRoutes.FirstAsync(x => x.Id == proof.RouteId && x.WorkspaceId == actor.WorkspaceId);
            Check.That(route.Version == proof.RouteVersion && route.Status == "ACTIVE", "La tournée a changé. Générez une nouvelle preuve.", "RECEIPT_INVALID", 409);
        }
        ValidateReceipt(order, input.Items);
        await RecallGuard.AssertDistribution(db, order, input.Items.Where(x => x.AcceptedQuantity > 0).Select(x => x.ProductId).ToList());
        Check.That(proof.ItemsHash == ReceiptHash(input.Items), "Les quantités diffèrent de celles confirmées par le commerçant.", "RECEIPT_MISMATCH", 409);
        Check.That(assignment.Status == "ARRIVED", "Confirmez votre arrivée avant la réception.", "INVALID_TRANSITION", 409);
        await orders.Delivery(Delegate(actor), id, new(input.Items, input.Note));
        proof.ConsumedAt = DateTimeOffset.UtcNow;
        assignment.Status = "COMPLETED";
        db.OperationReceipts.Add(new()
        {
            WorkspaceId = actor.WorkspaceId,
            OrderId = id,
            MerchantId = order.AccountId,
            DriverId = actor.Id,
            ChallengeId = proof.Id,
            AcceptedVersion = proof.Version,
            ItemsJson = JsonSerializer.Serialize(input.Items)
        });
        await db.SaveChangesAsync();
        return await Tracking(actor, id);
    }
    public async Task<object> Collect(Actor actor, string id, CourierCollectionInput input)
    {
        var order = await Find(actor, id);
        var assignment = await Assigned(actor, order);
        if (assignment.RouteId.Length > 0)
        {
            var route = await db.DeliveryRoutes.FirstAsync(x => x.Id == assignment.RouteId && x.WorkspaceId == actor.WorkspaceId);
            Check.That(route.CashState == "OPEN", "La caisse de tournée est déjà déclarée.", "ROUTE_CASH_CLOSED", 409);
        }
        Check.That(assignment.Status == "COMPLETED", "L'encaissement suit la réception physique.", "INVALID_TRANSITION", 409);
        Check.That(input.Method == "CASH", "Le livreur ne peut déclarer que des espèces physiquement reçues.");
        Check.Money(input.Amount, "Montant");
        Check.Money(input.Tendered, "Espèces reçues");
        Check.Money(input.Change, "Monnaie rendue");
        Check.That(input.Tendered - input.Change == input.Amount && input.Amount > 0, "Le montant net doit correspondre aux espèces reçues moins la monnaie rendue.", "CASH_MISMATCH", 409);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d'idempotence");
        var requestHash = Tokens.Hash(JsonSerializer.Serialize(input));
        var previous = await db.CashReceipts.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == id && x.IdempotencyKey == key);
        if (previous != null)
        {
            Check.That(previous.DriverId == actor.Id && previous.RequestHash == requestHash, "Cette clé correspond à un autre encaissement.", "IDEMPOTENCY_CONFLICT", 409);
            return await Tracking(actor, id);
        }
        await orders.Collect(Delegate(actor), id, new(input.Amount, "CASH", input.Reference, "courier:" + key));
        db.CashReceipts.Add(new()
        {
            RouteId = assignment.RouteId,
            WorkspaceId = actor.WorkspaceId,
            OrderId = id,
            DriverId = actor.Id,
            IdempotencyKey = key,
            RequestHash = requestHash,
            Amount = input.Amount,
            Tendered = input.Tendered,
            Change = input.Change
        });
        await db.SaveChangesAsync();
        return await Tracking(actor, id);
    }
    public async Task<object> FinanceCollect(Actor actor, string id, PaymentInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        await Find(actor, id);
        return await orders.Collect(Delegate(actor), id, input);
    }
    public async Task<object> FinanceRefund(Actor actor, string id, RefundInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        await Find(actor, id);
        return await orders.Refund(Delegate(actor), id, input);
    }
    public async Task<object> Reconcile(Actor actor, string paymentId)
    {
        actor.RequireRoles("ADMIN", "FINANCE");
        return await orders.Reconcile(Delegate(actor), paymentId);
    }
    public async Task<object> Inspect(Actor actor, string id, InspectReturnInput input)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE");
        await Find(actor, id);
        return await orders.Inspect(Delegate(actor), id, input);
    }
}
