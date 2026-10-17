using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public class OrderService(WholesaleDb db, CatalogService catalog, IConfiguration config, RealtimeChanges changes, PromotionsService promotions, WarehouseService warehouse)
{
    public void Audit(Actor a, string action, string id, string details = "")
    {
        db.Audit.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            ActorId = a.Id,
            Actor = a.Name,
            Action = action,
            EntityId = id,
            Details = details
        });
    }
    public void Event(Actor a, PurchaseOrder o, string action, string details = "")
    {
        changes.Order(o.WorkspaceId, o.Id, o.AccountId);
        o.Version++;
        o.UpdatedAt = DateTimeOffset.UtcNow;
        db.OrderEvents.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            OrderId = o.Id,
            Action = action,
            Actor = a.Name,
            Details = details
        });
        Audit(a, action, o.Id, details);
    }
    public void Ledger(Actor a, string productId, string kind, int delta = 0, int reserved = 0, int quarantine = 0, string? orderId = null, string reason = "")
    {
        db.Inventory.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            ProductId = productId,
            Kind = kind,
            Delta = delta,
            ReservedDelta = reserved,
            QuarantineDelta = quarantine,
            OrderId = orderId,
            Reason = reason,
            Actor = a.Name
        });
    }
    public async Task<PurchaseOrder> Find(Actor a, string id)
    {
        var o = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == a.WorkspaceId && (a.IsAdmin || x.AccountId == a.Id));
        Check.That(o != null, "Commande introuvable.", "NOT_FOUND", 404);
        return o!;
    }
    public async Task<object> View(Actor a, PurchaseOrder o)
    {
        var merchant = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == o.AccountId);
        var timeline = (await db.OrderEvents.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == o.Id).ToListAsync()).OrderBy(x => x.At).Select(x => new { x.Id, x.Action, x.At, x.Actor, x.Details });
        var payments = (await db.Payments.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == o.Id).ToListAsync()).OrderBy(x => x.At).Select(x => new { x.Id, x.Amount, x.Method, x.Reference, x.IsRefund, x.Reconciled, x.At });
        var claims = (await db.Claims.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == o.Id).ToListAsync()).OrderByDescending(x => x.CreatedAt).Select(ClaimsService.View);
        var credits = (await db.Credits.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == o.Id).ToListAsync()).OrderBy(x => x.At).ToList();
        var w = await db.Workspaces.FindAsync(a.WorkspaceId);
        return new
        {
            o.Id,
            o.Number,
            o.Status,
            o.PaymentStatus,
            o.Version,
            merchant = new
            {
                merchant.Id,
                merchant.Name,
                merchant.ShopName,
                merchant.Phone
            },
            items = o.Items.Select(x => new { x.Id, x.ProductId, x.Name, x.Brand, x.ImageUrl, x.RequestedQuantity, x.Quantity, x.UnitPrice, x.LineTotal, x.CaseSize, x.PreparedQuantity, x.AcceptedQuantity, x.RefusedQuantity, x.InspectedQuantity }),
            o.Subtotal,
            o.DeliveryFee,
            o.Total,
            currency = "DZD",
            o.QuoteVersion,
            o.QuoteValidUntil,
            o.CreatedAt,
            o.UpdatedAt,
            o.DeliveredAt,
            address = ReadAddress(o.AddressJson),
            o.Note,
            o.AdminNote,
            balance = new
            {
                due = o.Total,
                collected = o.Collected,
                remaining = o.Total is decimal t ? Math.Max(0, t - o.Collected) : (decimal?)null,
                refundDue = o.Total is decimal d ? Math.Max(0, o.Collected - d) : 0
            },
            timeline,
            payments,
            claims,
            credits = credits.Select(x => new { x.Id, x.ClaimId, x.Amount, x.Reason, x.At, x.Actor }),
            creditTotal = credits.Sum(x => x.Amount),
            promotions = await promotions.View(a, o.Id),
            o.ReservationHeld,
            isDemo = w!.IsDemo
        };
    }
    public static Dictionary<string, string> ReadAddress(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, string>();
        foreach (var key in new[] { "id", "label", "wilaya", "commune", "street" })
        {
            var property = document.RootElement.EnumerateObject().FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            result[key] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? "" : "";
        }
        return result;
    }
    public async Task<object> Get(Actor a, string id) => await View(a, await Find(a, id));
    public async Task<object> List(Actor a, IQueryCollection query)
    {
        var status = query["status"].ToString();
        var q = query["q"].ToString().Trim().ToLowerInvariant();
        var page = CatalogService.Page(query["page"], 1);
        var size = Math.Clamp(CatalogService.Page(query["pageSize"], 20), 1, 100);
        var orders = await db.Orders.AsNoTracking().Include(x => x.Items).Where(x => x.WorkspaceId == a.WorkspaceId && (a.IsAdmin || x.AccountId == a.Id)).ToListAsync();
        var users = await db.Accounts.AsNoTracking().Where(x => x.WorkspaceId == a.WorkspaceId).ToDictionaryAsync(x => x.Id);
        var filtered = orders.Where(x => status.Length == 0 || x.Status == status).Where(x => q.Length == 0 || (x.Number + " " + users[x.AccountId].ShopName + " " + users[x.AccountId].Name).ToLowerInvariant().Contains(q)).OrderByDescending(x => x.CreatedAt).ToList();
        return new
        {
            items = filtered.Skip((page - 1) * size).Take(size).Select(o => new { o.Id, o.Number, o.Status, o.PaymentStatus, o.Total, currency = "DZD", o.CreatedAt, o.UpdatedAt, o.QuoteVersion, o.QuoteValidUntil, itemCount = o.Items.Count, quantity = o.Items.Sum(x => x.Quantity), merchant = new { id = o.AccountId, name = users[o.AccountId].Name, shopName = users[o.AccountId].ShopName }, imageUrl = o.Items.FirstOrDefault()?.ImageUrl, balance = new { due = o.Total, collected = o.Collected, remaining = o.Total is decimal t ? Math.Max(0, t - o.Collected) : (decimal?)null } }),
            total = filtered.Count,
            page,
            pageSize = size,
            totalPages = (int)Math.Ceiling(filtered.Count / (double)size)
        };
    }
    public async Task<object> Create(Actor a, CreateOrderInput input)
    {
        a.Approved();
        Check.That(a.Role == "MERCHANT", "Utilisez la persona commerçant pour passer une commande.", "MERCHANT_REQUIRED", 403);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d'idempotence");
        var lines = CatalogService.ValidateLines(input.Items);
        var mode = (input.Mode ?? "REQUEST_QUOTE").ToUpperInvariant();
        Check.That(mode is "REQUEST_QUOTE" or "PLACE_ORDER", "Mode de commande inconnu.");
        var hash = Tokens.Hash(JsonSerializer.Serialize(new
        {
            addressId = input.AddressId,
            items = lines,
            note = (input.Note ?? "").Trim(),
            mode
        }));
        var previous = await db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.AccountId == a.Id && x.IdempotencyKey == key);
        if (previous != null)
        {
            Check.That(previous.RequestHash == hash, "Cette clé a déjà servi pour une demande différente.", "IDEMPOTENCY_CONFLICT", 409);
            return await View(a, previous);
        }
        var address = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.AddressId && x.WorkspaceId == a.WorkspaceId && x.AccountId == a.Id);
        Check.That(address != null, "Sélectionnez une adresse de votre boutique.", "ADDRESS_REQUIRED");
        var ids = lines.Select(x => x.ProductId).ToList();
        var ps = await db.Products.AsNoTracking().Where(x => x.IsCatalogActive).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        Check.That(ps.Count == lines.Count, "Un produit de cette demande est introuvable.", "PRODUCT_NOT_FOUND", 404);
        var os = await db.Offers.Where(x => x.WorkspaceId == a.WorkspaceId && ids.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId);
        Check.That(os.Values.All(x => x.IsActive), "Un produit est désactivé.");
        var order = new PurchaseOrder { WorkspaceId = a.WorkspaceId, AccountId = a.Id, Number = $"TS-{DateTimeOffset.UtcNow:yyMMdd}-{Tokens.New()[..8].ToUpperInvariant()}", IdempotencyKey = key, RequestHash = hash, AddressJson = JsonSerializer.Serialize(new { id = address!.Id, label = address.Label, wilaya = address.Wilaya, commune = address.Commune, street = address.Street }), Note = Check.Text(input.Note, 1500, "Note", false) };
        foreach (var l in lines)
        {
            var p = ps[l.ProductId];
            var o = os.GetValueOrDefault(l.ProductId);
            if (mode == "PLACE_ORDER")
            {
                Check.That(o?.OfferApproved == true && o.IsActive && o.Price != null && o.OnHand != null, "Le prix et le stock doivent être confirmés. Demandez un devis.", "QUOTE_REQUIRED", 409);
                Check.That(l.Quantity >= o!.MinimumQuantity && l.Quantity % o.CaseSize == 0, "La quantité ne respecte pas le conditionnement.", "PACK_SIZE");
            }
            order.Items.Add(new()
            {
                ProductId = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                ImageUrl = p.ImageUrl,
                RequestedQuantity = l.Quantity,
                Quantity = l.Quantity,
                UnitPrice = mode == "PLACE_ORDER" ? o!.Price : null,
                LineTotal = mode == "PLACE_ORDER" ? o!.Price * l.Quantity : null,
                CaseSize = o?.CaseSize ?? 1
            });
        }
        if (mode == "PLACE_ORDER")
        {
            var fee = config.GetValue<decimal?>("Wholesale:DeliveryFee");
            Check.That(fee != null, "Le tarif de livraison doit être confirmé. Demandez un devis.", "QUOTE_REQUIRED", 409);
            Check.Money(fee!.Value, "Frais de livraison");
            await promotions.ApplyDirectOrder(a, order);
            order.Status = "SUBMITTED";
            order.Subtotal = order.Items.Sum(x => x.LineTotal ?? 0);
            order.DeliveryFee = fee;
            order.Total = order.Subtotal + fee;
            await Reserve(a, order);
        }
        db.Orders.Add(order);
        Event(a, order, mode == "REQUEST_QUOTE" ? "REQUESTED" : "SUBMITTED", "Demande envoyée par le commerçant.");
        await db.CartItems.Where(x => x.AccountId == a.Id).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
        return await View(a, order);
    }
    public async Task Reserve(Actor a, PurchaseOrder order)
    {
        foreach (var line in order.Items.Where(x => x.Quantity > 0))
        {
            var qty = line.Quantity;
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Offers" SET "Reserved" = "Reserved" + {qty} WHERE "WorkspaceId" = {a.WorkspaceId} AND "ProductId" = {line.ProductId} AND "IsActive" = {true} AND "OnHand" IS NOT NULL AND "OnHand" - "Reserved" >= {qty}""");
            Check.That(changed == 1, $"Stock insuffisant pour {line.Name}. Le grossiste doit actualiser le devis.", "STOCK_CONFLICT", 409);
            Ledger(a, line.ProductId, "RESERVED", reserved: qty, orderId: order.Id);
        }
        order.ReservationHeld = true;
        await warehouse.ReserveOrder(a, order);
        await promotions.Reserve(a, order);
    }
    public async Task Release(Actor a, PurchaseOrder order)
    {
        if (!order.ReservationHeld)
        {
            await warehouse.ReleaseOrder(a, order);
            await promotions.Release(a, order);
            return;
        }
        foreach (var l in order.Items.Where(x => x.Quantity > 0))
        {
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Offers" SET "Reserved" = "Reserved" - {l.Quantity} WHERE "WorkspaceId" = {a.WorkspaceId} AND "ProductId" = {l.ProductId} AND "Reserved" >= {l.Quantity}""");
            Check.That(changed == 1, "Réservation de stock incohérente.", "INVENTORY_CONFLICT", 409);
            Ledger(a, l.ProductId, "RELEASED", reserved: -l.Quantity, orderId: order.Id);
        }
        await warehouse.ReleaseOrder(a, order);
        await promotions.Release(a, order);
        order.ReservationHeld = false;
    }
    public async Task<object> Quote(Actor a, string id, QuoteInput input)
    {
        a.Admin();
        var o = await Find(a, id);
        Check.That(input.Version == null || input.Version == o.Version, "La commande a été modifiée. Rechargez-la avant de réviser le devis.", "VERSION_CONFLICT", 409);
        Check.That(o.Status is "REQUESTED" or "QUOTED", "Cette demande ne peut plus être chiffrée.", "INVALID_TRANSITION", 409);
        Check.That(input.ValidUntil > DateTimeOffset.UtcNow.AddMinutes(1) && input.ValidUntil <= DateTimeOffset.UtcNow.AddDays(30), "La validité du devis doit être comprise entre une minute et 30 jours.");
        Check.Money(input.DeliveryFee, "Frais de livraison");
        Check.That(input.Items != null && input.Items.Count == o.Items.Count && input.Items.Select(x => x.ProductId).Distinct().Count() == o.Items.Count, "Le devis doit préciser chaque ligne, sans doublon.");
        input = input with
        {
            Items = await promotions.ApplyQuote(a, o, input.Items, campaignIds: input.CampaignIds)
        };
        foreach (var l in o.Items)
        {
            var q = input.Items!.FirstOrDefault(x => x.ProductId == l.ProductId);
            Check.That(q != null, "Une ligne du devis est manquante.");
            Check.That(q!.Quantity >= 0 && q.Quantity <= l.RequestedQuantity, "La quantité confirmée doit être comprise entre 0 et la quantité demandée.");
            Check.Money(q.UnitPrice, "Prix unitaire");
            Check.That(q.Quantity == 0 || q.UnitPrice > 0, "Un prix positif est requis pour les unités confirmées.");
            var pack = q.CaseSize ?? 1;
            Check.That(pack > 0 && pack <= 10000 && (q.Quantity == 0 || q.Quantity % pack == 0), "Conditionnement invalide.");
            var offer = await db.Offers.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.ProductId == l.ProductId);
            if (offer == null)
            {
                offer = new()
                {
                    WorkspaceId = a.WorkspaceId,
                    ProductId = l.ProductId
                };
                db.Offers.Add(offer);
            }
            if (q.AvailableStock != null)
            {
                await warehouse.AssertManualStockChange(a.WorkspaceId, l.ProductId, q.AvailableStock);
                Check.That(q.AvailableStock >= offer.Reserved && q.AvailableStock >= 0, "Le stock confirmé ne peut pas être inférieur aux réservations existantes.");
                var delta = q.AvailableStock.Value - (offer.OnHand ?? 0);
                offer.OnHand = q.AvailableStock;
                Ledger(a, l.ProductId, "STOCK_CONFIRMED", delta: delta, orderId: o.Id, reason: "Stock physique confirmé lors du devis.");
            }
            Check.That(q.Quantity == 0 || (offer.OnHand != null && offer.OnHand - offer.Reserved >= q.Quantity), "Confirmez le stock physique disponible pour chaque produit.", "STOCK_REQUIRED", 409);
            var eligible = await warehouse.EligibleAvailable(a.WorkspaceId, l.ProductId);
            Check.That(q.Quantity == 0 || eligible == null || eligible >= q.Quantity, "Les lots non expirés disponibles ne couvrent pas la quantité proposée.", "LOT_STOCK_INSUFFICIENT", 409);
            Check.That(offer.IsActive, "Un produit du devis est désactivé.");
            l.Quantity = q.Quantity;
            l.UnitPrice = q.Quantity > 0 ? q.UnitPrice : 0;
            l.LineTotal = q.Quantity * q.UnitPrice;
            l.CaseSize = pack;
        }
        Check.That(o.Items.Any(x => x.Quantity > 0), "Au moins un produit doit être disponible. Sinon rejetez la demande.");
        o.Subtotal = o.Items.Sum(x => x.LineTotal ?? 0);
        o.DeliveryFee = input.DeliveryFee;
        o.Total = o.Subtotal + o.DeliveryFee;
        o.QuoteVersion++;
        o.QuoteValidUntil = input.ValidUntil.ToUniversalTime();
        o.AdminNote = Check.Text(input.Note, 1500, "Note grossiste", false);
        o.Status = "QUOTED";
        Event(a, o, "QUOTED", $"Devis version {o.QuoteVersion}, valable jusqu'au {o.QuoteValidUntil:O}. Aucun stock réservé avant acceptation.");
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public async Task<object> Accept(Actor a, string id, AcceptQuoteInput input)
    {
        Check.That(a.Role == "MERCHANT", "L'acceptation appartient au commerçant.", "MERCHANT_REQUIRED", 403);
        a.Approved();
        var o = await Find(a, id);
        Check.That(input.Version == o.QuoteVersion, "Le devis a été révisé. Consultez la nouvelle version.", "QUOTE_VERSION_CONFLICT", 409);
        if (o.Status is "CONFIRMED" or "PREPARING" or "READY" or "OUT_FOR_DELIVERY" or "DELIVERED" or "PARTIALLY_DELIVERED" or "DELIVERY_REFUSED")
            return await View(a, o);
        Check.That(o.Status == "QUOTED", "Aucun devis n'est disponible à accepter.", "INVALID_TRANSITION", 409);
        Check.That(o.QuoteValidUntil > DateTimeOffset.UtcNow, "Ce devis a expiré. Demandez son actualisation.", "QUOTE_EXPIRED", 409);
        await Reserve(a, o);
        o.Status = "CONFIRMED";
        Event(a, o, "QUOTE_ACCEPTED", $"Version {o.QuoteVersion} acceptée, prix figés et stock réservé.");
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public async Task<object> Decline(Actor a, string id, ReasonInput input)
    {
        Check.That(a.Role == "MERCHANT", "Action réservée au commerçant.", "MERCHANT_REQUIRED", 403);
        var o = await Find(a, id);
        Check.That(o.Status == "QUOTED", "Seul un devis en attente peut être refusé.", "INVALID_TRANSITION", 409);
        await promotions.Release(a, o);
        o.Status = "DECLINED";
        Event(a, o, "DECLINED", Check.Text(input.Reason, 800, "Motif", false));
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public void VoidCancelledContract(Actor a, PurchaseOrder order)
    {
        var previousTotal = order.Total;
        var previousFee = order.DeliveryFee;
        order.Subtotal = 0;
        order.DeliveryFee = 0;
        order.Total = 0;
        UpdatePayment(order);
        Event(a, order, "CONTRACT_VOIDED", $"Contrat annulé : ancien montant {previousTotal?.ToString() ?? "sur devis"} DZD, anciens frais {previousFee?.ToString() ?? "à confirmer"} DZD. Montant dû ramené à zéro, lignes initiales conservées. Tout encaissement reste enregistré et doit être remboursé explicitement.");
    }
    public async Task<object> Cancel(Actor a, string id, ReasonInput input)
    {
        var o = await Find(a, id);
        Check.That(o.Status is "REQUESTED" or "QUOTED" or "SUBMITTED" or "CONFIRMED", "La préparation a commencé. Contactez le grossiste.", "INVALID_TRANSITION", 409);
        var reason = Check.Text(input.Reason, 800, "Motif d'annulation");
        await Release(a, o);
        o.Status = "CANCELLED";
        VoidCancelledContract(a, o);
        Event(a, o, "CANCELLED", reason);
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public async Task<object> Transition(Actor a, string id, TransitionInput input)
    {
        a.Admin();
        var o = await Find(a, id);
        var target = input.Status.ToUpperInvariant();
        var valid = (o.Status, target) switch
        {
            ("SUBMITTED", "CONFIRMED") => true,
            ("CONFIRMED", "PREPARING") => true,
            ("PREPARING", "READY") => true,
            ("READY", "OUT_FOR_DELIVERY") => true,
            ("REQUESTED" or "QUOTED" or "SUBMITTED", "REJECTED") => true,
            ("SUBMITTED" or "CONFIRMED", "CANCELLED") => true,
            _ => false
        };
        if (target == "READY")
            Check.That(await warehouse.ScansComplete(o), "Validez les scans de préparation avant de terminer.", "SCAN_REQUIRED", 409);
        Check.That(valid, "Cette transition n'est pas autorisée.", "INVALID_TRANSITION", 409);
        if (target is "REJECTED" or "CANCELLED")
        {
            Check.Text(input.Reason, 800, "Motif");
            await Release(a, o);
        }
        if (target == "OUT_FOR_DELIVERY")
        {
            await warehouse.DispatchOrder(a, o);
            Check.That(o.ReservationHeld, "Aucune réservation n'est disponible.", "INVENTORY_CONFLICT", 409);
            foreach (var l in o.Items.Where(x => x.Quantity > 0))
            {
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Offers" SET "OnHand" = "OnHand" - {l.Quantity}, "Reserved" = "Reserved" - {l.Quantity} WHERE "WorkspaceId" = {a.WorkspaceId} AND "ProductId" = {l.ProductId} AND "Reserved" >= {l.Quantity} AND "OnHand" >= {l.Quantity}""");
                Check.That(changed == 1, "Le stock réservé ne peut pas être expédié.", "INVENTORY_CONFLICT", 409);
                Ledger(a, l.ProductId, "DISPATCHED", delta: -l.Quantity, reserved: -l.Quantity, orderId: o.Id);
            }
            await promotions.Dispatch(a, o);
            o.ReservationHeld = false;
        }
        o.Status = target;
        if (target == "CANCELLED")
            VoidCancelledContract(a, o);
        Event(a, o, target, Check.Text(input.Reason, 800, "Note", false));
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public async Task<object> Delivery(Actor a, string id, DeliveryInput input)
    {
        a.Admin();
        var o = await Find(a, id);
        Check.That(o.Status == "OUT_FOR_DELIVERY", "La commande doit être en livraison.", "INVALID_TRANSITION", 409);
        Check.That(input.Items != null && input.Items.Count == o.Items.Count && input.Items.Select(x => x.ProductId).Distinct().Count() == o.Items.Count, "Renseignez toutes les lignes une seule fois.");
        foreach (var l in o.Items)
        {
            var d = input.Items!.FirstOrDefault(x => x.ProductId == l.ProductId);
            Check.That(d != null && d.AcceptedQuantity >= 0 && d.RefusedQuantity >= 0 && d.AcceptedQuantity + d.RefusedQuantity == l.Quantity, "Les unités acceptées et refusées doivent correspondre aux unités expédiées.");
            l.AcceptedQuantity = d!.AcceptedQuantity;
            l.RefusedQuantity = d.RefusedQuantity;
            if (d.RefusedQuantity > 0)
            {
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Offers" SET "Quarantined" = "Quarantined" + {d.RefusedQuantity} WHERE "WorkspaceId" = {a.WorkspaceId} AND "ProductId" = {l.ProductId}""");
                Check.That(changed == 1, "Produit de retour introuvable.", "INVENTORY_CONFLICT", 409);
                Ledger(a, l.ProductId, "RETURN_QUARANTINE", quarantine: d.RefusedQuantity, orderId: o.Id, reason: "Refus à la livraison, contrôle physique requis.");
            }
        }
        await promotions.RecordDelivery(a, o);
        var accepted = o.Items.Sum(x => x.AcceptedQuantity);
        var refused = o.Items.Sum(x => x.RefusedQuantity);
        o.Status = accepted == 0 ? "DELIVERY_REFUSED" : refused > 0 ? "PARTIALLY_DELIVERED" : "DELIVERED";
        o.Subtotal = o.Items.Sum(x => x.UnitPrice * x.AcceptedQuantity ?? 0);
        o.Total = o.Subtotal + o.DeliveryFee;
        o.DeliveredAt = DateTimeOffset.UtcNow;
        UpdatePayment(o);
        Event(a, o, o.Status, Check.Text(input.Note, 1000, "Note de livraison", false));
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public async Task<object> Inspect(Actor a, string id, InspectReturnInput input)
    {
        a.Admin();
        var o = await Find(a, id);
        var l = o.Items.FirstOrDefault(x => x.ProductId == input.ProductId);
        Check.That(l != null && input.Quantity > 0 && input.Quantity <= l.RefusedQuantity - l.InspectedQuantity, "Quantité à inspecter invalide.");
        var disposition = input.Disposition.ToUpperInvariant();
        Check.That(disposition is "RESTOCK" or "WRITE_OFF", "Choisissez une remise en stock ou une perte.");
        var reason = Check.Text(input.Reason, 800, "Motif du contrôle");
        await warehouse.InspectReturn(a, o.Id, input.ProductId, "REFUSAL", l!.Id, input.Quantity, disposition);
        var delta = disposition == "RESTOCK" ? input.Quantity : 0;
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Offers" SET "Quarantined" = "Quarantined" - {input.Quantity}, "OnHand" = COALESCE("OnHand",0) + {delta} WHERE "WorkspaceId" = {a.WorkspaceId} AND "ProductId" = {input.ProductId} AND "Quarantined" >= {input.Quantity}""");
        Check.That(changed == 1, "Stock en quarantaine insuffisant.", "INVENTORY_CONFLICT", 409);
        l!.InspectedQuantity += input.Quantity;
        Ledger(a, l.ProductId, disposition, delta: delta, quarantine: -input.Quantity, orderId: o.Id, reason: reason);
        Event(a, o, "RETURN_INSPECTED", $"{input.Quantity} × {l.Name} : {disposition}. {reason}");
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public static void UpdatePayment(PurchaseOrder o)
    {
        o.PaymentStatus = o.Collected <= 0 ? "UNPAID" : o.Total == null || o.Collected < o.Total ? "PARTIAL" : o.Collected > o.Total ? "REFUND_DUE" : "COLLECTED";
    }
    public async Task<object> Collect(Actor a, string id, PaymentInput input)
    {
        a.Admin();
        var o = await Find(a, id);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d'idempotence");
        Check.Money(input.Amount, "Montant");
        Check.That(input.Amount > 0, "Le montant doit être positif.");
        var method = input.Method.ToUpperInvariant();
        Check.That(method is "CASH" or "TRANSFER", "Moyen de paiement non pris en charge.");
        var reference = Check.Text(input.Reference, 150, "Référence", false);
        var prev = await db.Payments.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == id && x.IdempotencyKey == key);
        if (prev != null)
        {
            Check.That(!prev.IsRefund && prev.Amount == input.Amount && prev.Method == method && prev.Reference == reference, "Cette clé correspond à un encaissement différent.", "IDEMPOTENCY_CONFLICT", 409);
            return await View(a, o);
        }
        Check.That(o.Status is "CONFIRMED" or "PREPARING" or "READY" or "OUT_FOR_DELIVERY" or "DELIVERED" or "PARTIALLY_DELIVERED" or "DELIVERY_REFUSED", "La commande ne peut pas être encaissée.", "INVALID_TRANSITION", 409);
        Check.That(o.Total != null && input.Amount <= o.Total - o.Collected, "Le montant dépasse le solde à encaisser.", "PAYMENT_EXCEEDS_DUE", 409);
        db.Payments.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            OrderId = id,
            IdempotencyKey = key,
            Amount = input.Amount,
            Method = method,
            Reference = reference
        });
        o.Collected += input.Amount;
        UpdatePayment(o);
        Event(a, o, "PAYMENT_COLLECTED", $"{input.Amount:0.00} DZD · {method} · {reference}");
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public async Task<object> Refund(Actor a, string id, RefundInput input)
    {
        a.Admin();
        var o = await Find(a, id);
        var key = Check.Text(input.IdempotencyKey, 100, "Clé d'idempotence");
        Check.Money(input.Amount, "Remboursement");
        var reason = Check.Text(input.Reason, 800, "Motif");
        var reference = Check.Text(input.Reference, 150, "Référence");
        var prev = await db.Payments.FirstOrDefaultAsync(x => x.WorkspaceId == a.WorkspaceId && x.OrderId == id && x.IdempotencyKey == key);
        if (prev != null)
        {
            Check.That(prev.IsRefund && prev.Amount == input.Amount && prev.Reference == reference, "Cette clé correspond à un remboursement différent.", "IDEMPOTENCY_CONFLICT", 409);
            return await View(a, o);
        }
        Check.That(input.Amount > 0 && o.Total != null && input.Amount <= o.Collected - o.Total, "Le remboursement dépasse le trop-perçu.", "REFUND_EXCEEDS_DUE", 409);
        db.Payments.Add(new()
        {
            WorkspaceId = a.WorkspaceId,
            OrderId = id,
            IdempotencyKey = key,
            Amount = input.Amount,
            Method = "REFUND",
            Reference = reference,
            IsRefund = true
        });
        o.Collected -= input.Amount;
        UpdatePayment(o);
        Event(a, o, "PAYMENT_REFUNDED", $"{input.Amount:0.00} DZD · {reason}");
        await db.SaveChangesAsync();
        return await View(a, o);
    }
    public async Task<object> Reconcile(Actor a, string paymentId)
    {
        a.Admin();
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId && x.WorkspaceId == a.WorkspaceId);
        Check.That(p != null, "Encaissement introuvable.", "NOT_FOUND", 404);
        var o = await Find(a, p!.OrderId);
        if (!p.Reconciled)
        {
            p.Reconciled = true;
            if (o.Total != null && o.Collected == o.Total && (await db.Payments.Where(x => x.OrderId == o.Id && x.Id != p.Id).ToListAsync()).All(x => x.Reconciled))
                o.PaymentStatus = "RECONCILED";
            Event(a, o, "PAYMENT_RECONCILED", $"Encaissement {p.Id} rapproché.");
            await db.SaveChangesAsync();
        }
        return await View(a, o);
    }
    public async Task<object> Reorder(Actor a, string id)
    {
        var o = await Find(a, id);
        return await catalog.SaveCart(a, new(o.Items.Select(x => new LineInput(x.ProductId, x.RequestedQuantity)).ToList()));
    }
}
