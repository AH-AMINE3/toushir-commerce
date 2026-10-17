using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public sealed class RouteService(WholesaleDb db, OperationsService operations, OrderService orders, RealtimeChanges changes)
{
    private static void Version(DeliveryRoute route, int version) => RouteRules.RequireVersion(route.Version,version);
    private async Task<DeliveryRoute> Find(Actor actor, string id)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "FINANCE", "DRIVER");
        var route = await db.DeliveryRoutes.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == actor.WorkspaceId && (actor.Role != "DRIVER" || x.DriverId == actor.Id));
        Check.That(route != null, "Tournée introuvable.", "NOT_FOUND", 404); return route;
    }
    private async Task<List<PurchaseOrder>> Stops(DeliveryRoute route)
    {
        var assignments=await db.DeliveryAssignments.Where(x=>x.WorkspaceId==route.WorkspaceId&&x.RouteId==route.Id).ToListAsync();var ids=assignments.Select(x=>x.OrderId).ToList();
        return (await db.Orders.Include(x => x.Items).Where(x => x.WorkspaceId == route.WorkspaceId && ids.Contains(x.Id)).ToListAsync()).OrderBy(x=>assignments.First(a=>a.OrderId==x.Id).StopRank).ThenBy(x => x.CreatedAt).ToList();
    }
    private async Task<List<PromotionSnapshot>> Gifts(DeliveryRoute route,List<PurchaseOrder> stops){var ids=stops.Select(x=>x.Id).ToList();var list=await db.PromotionSnapshots.AsNoTracking().Where(x=>x.WorkspaceId==route.WorkspaceId&&ids.Contains(x.OrderId)&&x.GiftQuantity>0&&x.Status!="SUPERSEDED").ToListAsync();return list.Where(x=>stops.Any(o=>o.Id==x.OrderId&&o.QuoteVersion==x.QuoteVersion)).ToList();}
    private static object Manifest(DeliveryRoute route, List<PurchaseOrder> stops,List<PromotionSnapshot> gifts) => new { route.Id, route.Version, route.DriverId, route.VehicleReference, route.OpeningFloat,route.CapacityUnits,load=RouteRules.Load(stops.SelectMany(x=>x.Items).Select(x=>(long)x.Quantity),gifts.Select(x=>(long)x.GiftQuantity),route.CapacityUnits),gifts=gifts.OrderBy(x=>x.Id).Select(x=>new{x.Id,x.OrderId,x.GiftProductId,x.GiftProductName,x.GiftQuantity,x.GiftStockState,x.Version}), stops = stops.Select((x,rank) => new { stopRank=rank+1,x.Id, x.AccountId, x.Version, x.Number, items = x.Items.OrderBy(l => l.ProductId).Select(l => new { l.ProductId, l.Quantity, l.PreparedQuantity }) }) };
    private static string ManifestHash(DeliveryRoute route, List<PurchaseOrder> stops,List<PromotionSnapshot> gifts) => Tokens.Hash(JsonSerializer.Serialize(Manifest(route, stops,gifts)));
    private static void Ready(List<PurchaseOrder> stops) => Check.That(stops.Count > 0 && stops.All(x => x.Status == "READY" && x.Items.All(l => l.PreparedQuantity == l.Quantity)), "Chaque commande du manifeste doit être intégralement préparée.", "PREPARATION_INCOMPLETE", 409);
    private async Task<List<CashReceipt>> Cash(DeliveryRoute route) => await db.CashReceipts.AsNoTracking().Where(x => x.WorkspaceId == route.WorkspaceId && x.RouteId == route.Id).ToListAsync();
    public async Task<object> Get(Actor actor, string id)
    {
        var route = await Find(actor, id); var driver = await db.Accounts.AsNoTracking().FirstAsync(x => x.Id == route.DriverId);
        var stops = new List<object>(); foreach (var order in await Stops(route)) stops.Add(await operations.OperationalOrder(actor, order));
        var stopRecords=await Stops(route);var assignmentRows=await db.DeliveryAssignments.AsNoTracking().Where(x=>x.WorkspaceId==route.WorkspaceId&&x.RouteId==route.Id).ToListAsync();var receivedOrders=await db.OrderEvents.AsNoTracking().Where(x=>x.WorkspaceId==route.WorkspaceId&&x.Action=="ROUTE_GOODS_RETURNED").Select(x=>x.OrderId).ToListAsync();
        var returns=stopRecords.Select(order=>{var failed=assignmentRows.First(x=>x.OrderId==order.Id).Status=="FAILED";var received=receivedOrders.Contains(order.Id);var items=order.Items.Where(x=>(failed?x.Quantity:x.RefusedQuantity)>0).Select(x=>new{x.ProductId,x.Name,quantity=failed?x.Quantity:x.RefusedQuantity,remainingQuantity=received?0:failed?x.Quantity:Math.Max(0,x.RefusedQuantity-x.InspectedQuantity)}).ToList();return new{orderId=order.Id,version=order.Version,order.Number,state=received?"DEPOT_RECEIVED":items.Any(x=>x.remainingQuantity>0)?"PENDING_RETURN":"INSPECTED",items,requiresReceipt=items.Any(x=>x.remainingQuantity>0)};}).Where(x=>x.items.Count>0).ToList();
        var receipts = await Cash(route); var net = receipts.Sum(x => x.Amount); var expected = RouteRules.ExpectedCash(route.OpeningFloat,receipts.Select(x=>(x.Tendered,x.Change)));
        var custody = await db.RouteCustodyReceipts.AsNoTracking().Where(x => x.WorkspaceId == route.WorkspaceId && x.RouteId == id).Select(x => new { x.Id, x.DriverId, x.WarehouseActorId, x.AcceptedVersion, x.At }).FirstOrDefaultAsync();
        return new
        {
            route.Id, route.Name, route.Version, route.Status, route.VehicleReference,route.CapacityUnits,load=RouteRules.Load((await Stops(route)).SelectMany(x=>x.Items).Select(x=>(long)x.Quantity),(await Gifts(route,await Stops(route))).Select(x=>(long)x.GiftQuantity),route.CapacityUnits), route.CreatedAt, route.DepartedAt, route.DeclaredAt, route.CountedAt, route.ReconciledAt,
            driver = new { driver.Id, driver.Name, driver.Phone }, stops, custody,returns,
            cash = new { route.OpeningFloat, tendered = receipts.Sum(x => x.Tendered), change = receipts.Sum(x => x.Change), netReceipts = net, expected, declared = route.DeclaredAmount, counted = route.CountedAmount, shortage = route.Shortage, surplus = route.Surplus, state = route.CashState, declarationVariance = route.DeclaredAmount - expected, countVariance = route.CountedAmount - route.DeclaredAmount, route.ExpectedAtDeclaration, route.DeclarationNote, route.FinanceNote, merchantDebtUnaffected = true, unit = "DZD", receipts = receipts.Select(x => new { x.Id, x.OrderId, x.Amount, x.Tendered, x.Change, x.At }) }
        };
    }
    public async Task<object> List(Actor actor)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE", "DISPATCHER", "FINANCE", "DRIVER");
        var routes = (await db.DeliveryRoutes.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId && (actor.Role != "DRIVER" || x.DriverId == actor.Id)).ToListAsync()).OrderByDescending(x => x.CreatedAt).Take(100);
        var items = new List<object>(); foreach (var route in routes) items.Add(await Get(actor, route.Id));
        return new { items, total = items.Count };
    }
    public async Task<object> Create(Actor actor, CreateRouteInput input)
    {
        actor.RequireRoles("ADMIN", "DISPATCHER");
        Check.That(input.OrderIds is { Count: > 0 and <= 100 } && input.OrderIds.Distinct().Count() == input.OrderIds.Count, "Sélectionnez 1 à 100 commandes distinctes.");
        Check.Money(input.OpeningFloat, "Fond de caisse initial");
        var driver = await db.Accounts.FirstOrDefaultAsync(x => x.Id == input.DriverId && x.WorkspaceId == actor.WorkspaceId && x.Role == "DRIVER" && x.ApprovalStatus == "APPROVED");
        Check.That(driver != null, "Livreur actif introuvable.", "NOT_FOUND", 404);
        var stops = await db.Orders.Include(x => x.Items).Where(x => x.WorkspaceId == actor.WorkspaceId && input.OrderIds.Contains(x.Id)).ToListAsync();
        Check.That(stops.Count == input.OrderIds.Count, "Une commande est introuvable.", "NOT_FOUND", 404);
        Check.That(stops.All(x => x.Status is "CONFIRMED" or "PREPARING" or "READY"), "Seules les commandes confirmées avant départ peuvent rejoindre une tournée.", "INVALID_TRANSITION", 409);
        var route = new DeliveryRoute { WorkspaceId = actor.WorkspaceId, Name = Check.Text(input.Name, 120, "Nom de tournée"), DriverId = driver.Id, VehicleReference = Check.Text(input.VehicleReference, 100, "Véhicule", false), OpeningFloat = input.OpeningFloat,CapacityUnits=input.CapacityUnits, CreatedBy = actor.Id };
        RouteRules.RequireCapacity(stops.SelectMany(x=>x.Items).Select(x=>(long)x.Quantity),(await Gifts(route,stops)).Select(x=>(long)x.GiftQuantity),input.CapacityUnits);
        stops=input.OrderIds.Select(id=>stops.First(x=>x.Id==id)).ToList();
        db.DeliveryRoutes.Add(route);
        var rank=0;foreach (var order in stops)
        {
            var assignment = await db.DeliveryAssignments.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == order.Id);
            Check.That(assignment == null || assignment.RouteId.Length == 0 && assignment.PickedUpAt == null, "Une commande appartient déjà à une tournée ou a été chargée.", "ASSIGNMENT_CONFLICT", 409);
            if (assignment == null) { assignment = new() { WorkspaceId = actor.WorkspaceId, OrderId = order.Id }; db.DeliveryAssignments.Add(assignment); }
            else if (assignment.DriverId.Length > 0) changes.Driver(actor.WorkspaceId, assignment.DriverId);
            assignment.StopRank=++rank;assignment.RouteId = route.Id; assignment.DriverId = driver.Id; assignment.AssignedBy = actor.Id; assignment.AssignedAt = DateTimeOffset.UtcNow; assignment.Status = "ASSIGNED";
            orders.Event(actor, order, "ROUTE_ASSIGNED", $"{route.Name} · {driver.Name}");
        }
        orders.Audit(actor, "ROUTE_CREATED", route.Id, $"{stops.Count} commandes · fond initial {route.OpeningFloat:0.00} DZD");
        changes.Driver(actor.WorkspaceId, driver.Id); await db.SaveChangesAsync(); return await Get(actor, route.Id);
    }
    public async Task<object> Reorder(Actor actor,string id,ReorderRouteInput input){
        actor.RequireRoles("ADMIN","DISPATCHER");var route=await Find(actor,id);Version(route,input.Version);Check.That(route.Status=="PLANNED","Le manifeste est déjà sous garde.","INVALID_TRANSITION",409);
        var stops=await Stops(route);Check.That(input.OrderIds!=null&&input.OrderIds.Count==stops.Count&&input.OrderIds.Distinct().Count()==stops.Count&&input.OrderIds.All(orderId=>stops.Any(o=>o.Id==orderId)),"Le réordonnancement doit conserver chaque arrêt une seule fois.");
        RouteRules.RequireCapacity(stops.SelectMany(x=>x.Items).Select(x=>(long)x.Quantity),(await Gifts(route,stops)).Select(x=>(long)x.GiftQuantity),input.CapacityUnits);
        for(var rank=0;rank<input.OrderIds.Count;rank++){var assignment=await db.DeliveryAssignments.FirstAsync(x=>x.WorkspaceId==actor.WorkspaceId&&x.RouteId==id&&x.OrderId==input.OrderIds[rank]);assignment.StopRank=rank+1;}
        route.CapacityUnits=input.CapacityUnits;route.Version++;orders.Audit(actor,"ROUTE_REORDERED",id,"Ordre des arrêts et capacité explicitement confirmés.");changes.Driver(actor.WorkspaceId,route.DriverId);await db.SaveChangesAsync();return await Get(actor,id);
    }
    public async Task<object> ManifestChallenge(Actor actor, string id, VersionInput input)
    {
        actor.RequireRoles("ADMIN", "WAREHOUSE");
        var route = await Find(actor, id); Version(route, input.Version);
        Check.That(route.Status == "PLANNED", "Cette tournée a déjà été prise en charge.", "INVALID_TRANSITION", 409);
        var stops = await Stops(route); Ready(stops);var gifts=await Gifts(route,stops);RouteRules.RequireCapacity(stops.SelectMany(x=>x.Items).Select(x=>(long)x.Quantity),gifts.Select(x=>(long)x.GiftQuantity),route.CapacityUnits);
        var old = await db.RouteCustodyChallenges.Where(x => x.WorkspaceId == actor.WorkspaceId && x.RouteId == id && x.ConsumedAt == null).ToListAsync(); foreach (var item in old) item.ExpiresAt = DateTimeOffset.UtcNow;
        var token = Tokens.New(); var challenge = new RouteCustodyChallenge { WorkspaceId = actor.WorkspaceId, RouteId = id, DriverId = route.DriverId, WarehouseActorId = actor.Id, Version = route.Version, ManifestHash = ManifestHash(route, stops,gifts), TokenHash = Tokens.Hash(token), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3) };
        db.RouteCustodyChallenges.Add(challenge); orders.Audit(actor, "ROUTE_MANIFEST_CHALLENGE", id); await db.SaveChangesAsync();
        return new { challenge = token, challenge.ExpiresAt, routeId = id, version = route.Version, route.DriverId, manifest = Manifest(route, stops,gifts), qrPayload = $"toushir://custody/{id}?code={token}&version={route.Version}" };
    }
    public async Task<object> AcceptCustody(Actor actor, string id, CustodyInput input)
    {
        actor.RequireRoles("DRIVER"); var route = await Find(actor, id); Version(route, input.Version);
        Check.That(route.Status == "PLANNED" && input.Challenge is { Length: 64 }, "Le manifeste n'est plus disponible.", "CUSTODY_INVALID", 409);
        var hash = Tokens.Hash(input.Challenge); var proof = await db.RouteCustodyChallenges.FirstOrDefaultAsync(x => x.WorkspaceId == actor.WorkspaceId && x.RouteId == id && x.DriverId == actor.Id && x.TokenHash == hash);
        Check.That(proof != null && proof.ConsumedAt == null && proof.ExpiresAt > DateTimeOffset.UtcNow && proof.Version == route.Version, "Code de chargement expiré ou déjà utilisé.", "CUSTODY_INVALID", 409);
        var stops = await Stops(route); Ready(stops);var gifts=await Gifts(route,stops);RouteRules.RequireCapacity(stops.SelectMany(x=>x.Items).Select(x=>(long)x.Quantity),gifts.Select(x=>(long)x.GiftQuantity),route.CapacityUnits);
        Check.That(proof.ManifestHash == ManifestHash(route, stops,gifts), "Une commande ou une quantité du manifeste a changé. Demandez un nouveau QR.", "MANIFEST_CHANGED", 409);
        var snapshot = JsonSerializer.Serialize(Manifest(route, stops,gifts));
        foreach (var order in stops)
        {
            var assignment = await db.DeliveryAssignments.FirstAsync(x => x.WorkspaceId == actor.WorkspaceId && x.OrderId == order.Id && x.RouteId == id);
            Check.That(assignment.DriverId == actor.Id && assignment.PickedUpAt == null, "Affectation de tournée modifiée.", "ASSIGNMENT_CONFLICT", 409);
            await orders.Transition(OperationsService.Delegate(actor), order.Id, new("OUT_FOR_DELIVERY", $"Chargement du manifeste {route.Name} confirmé avec le préparateur."));
            assignment.Status = "PICKED_UP"; assignment.PickedUpAt = DateTimeOffset.UtcNow;
        }
        proof.ConsumedAt = DateTimeOffset.UtcNow;
        db.RouteCustodyReceipts.Add(new() { WorkspaceId = actor.WorkspaceId, RouteId = id, DriverId = actor.Id, WarehouseActorId = proof.WarehouseActorId, AcceptedVersion = route.Version, ManifestJson = snapshot });
        route.Status = "ACTIVE"; route.DepartedAt = DateTimeOffset.UtcNow; route.Version++;
        orders.Audit(actor, "ROUTE_CUSTODY_ACCEPTED", id, $"{stops.Count} commandes · manifeste et fond de caisse acceptés"); changes.Driver(actor.WorkspaceId, actor.Id);
        await db.SaveChangesAsync(); return await Get(actor, id);
    }
    public async Task<object> ReceiveReturns(Actor actor,string id,DepotReturnInput input){
        actor.RequireRoles("ADMIN","WAREHOUSE");var route=await Find(actor,id);Check.That(route.Status=="ACTIVE","La tournée doit être active.","INVALID_TRANSITION",409);
        var assignment=await db.DeliveryAssignments.FirstOrDefaultAsync(x=>x.WorkspaceId==actor.WorkspaceId&&x.RouteId==id&&x.OrderId==input.OrderId);Check.That(assignment!=null,"Arrêt introuvable.","NOT_FOUND",404);
        var order=await db.Orders.Include(x=>x.Items).FirstAsync(x=>x.Id==input.OrderId&&x.WorkspaceId==actor.WorkspaceId);Check.That(order.Version==input.Version,"La commande a évolué.","VERSION_CONFLICT",409);
        var failed=assignment.Status=="FAILED";Check.That(failed||assignment.Status=="COMPLETED","Le retour physique suit un arrêt terminé ou en échec.","INVALID_TRANSITION",409);
        var expected=order.Items.Where(x=>(failed?x.Quantity:x.RefusedQuantity)>0).Select(x=>new DepotReturnLine(x.ProductId,failed?x.Quantity:x.RefusedQuantity)).OrderBy(x=>x.ProductId).ToList();
        Check.That(expected.Count>0&&input.Items!=null&&input.Items.Count==expected.Count&&input.Items.Select(x=>x.ProductId).Distinct().Count()==expected.Count&&expected.All(x=>input.Items.Any(item=>item.ProductId==x.ProductId&&item.Quantity==x.Quantity)),"La réception doit compter exactement les unités sous garde revenues au dépôt.","RETURN_QUANTITY",409);
        Check.That(!await db.OrderEvents.AnyAsync(x=>x.WorkspaceId==actor.WorkspaceId&&x.OrderId==order.Id&&x.Action=="ROUTE_GOODS_RETURNED"),"Le retour de cet arrêt est déjà enregistré.","RETURN_ALREADY_RECEIVED",409);
        var note=Check.Text(input.Note,800,"Constat de réception dépôt");
        if(failed)await orders.Delivery(OperationsService.Delegate(actor),order.Id,new(order.Items.Select(x=>new DeliveryLineInput(x.ProductId,0,x.Quantity)).ToList(),"Retour physique après échec: "+note));
        orders.Event(actor,order,"ROUTE_GOODS_RETURNED",JsonSerializer.Serialize(new{routeId=id,items=expected,note,receivedBy=actor.Id}));
        await db.SaveChangesAsync();changes.Driver(actor.WorkspaceId,route.DriverId);return await Get(actor,id);
    }
    public async Task<object> DeclareCash(Actor actor, string id, DeclareCashInput input)
    {
        actor.RequireRoles("DRIVER"); var route = await Find(actor, id); Version(route, input.Version);
        Check.That(route.Status == "ACTIVE" && route.CashState == "OPEN", "Cette caisse a déjà été déclarée ou n'est pas ouverte.", "INVALID_TRANSITION", 409);
        var assignments = await db.DeliveryAssignments.Where(x => x.WorkspaceId == actor.WorkspaceId && x.RouteId == id).ToListAsync();
        Check.That(assignments.Count > 0 && assignments.All(x => x.Status is "COMPLETED" or "FAILED"), "Terminez chaque arrêt ou enregistrez explicitement son échec.", "ROUTE_INCOMPLETE", 409);
        var stopOrders=await Stops(route);foreach(var stop in stopOrders){var assignment=assignments.First(x=>x.OrderId==stop.Id);if(assignment.Status=="FAILED"||stop.Items.Any(x=>x.RefusedQuantity>x.InspectedQuantity))Check.That(await db.OrderEvents.AnyAsync(x=>x.WorkspaceId==actor.WorkspaceId&&x.OrderId==stop.Id&&x.Action=="ROUTE_GOODS_RETURNED"),"Des marchandises de retour restent sous garde. Le dépôt doit confirmer leur réception.","RETURN_CUSTODY_OPEN",409);}
        var gifts=await Gifts(route,stopOrders);Check.That(!gifts.Any(x=>x.GiftStockState is "IN_TRANSIT" or "RETURN_REQUIRED"),"Confirmez la remise ou le retour physique des gratuités du manifeste avant de clôturer la garde.","GIFT_CUSTODY_OPEN",409);
        Check.Money(input.DeclaredAmount, "Caisse déclarée");
        route.DeclaredAmount = input.DeclaredAmount; route.DeclarationNote = Check.Text(input.Note, 800, "Note", false); route.ExpectedAtDeclaration = route.OpeningFloat + (await Cash(route)).Sum(x => x.Amount); route.DeclaredAt = DateTimeOffset.UtcNow; route.CashState = "DECLARED"; route.Status = "RETURNED"; route.Version++;
        orders.Audit(actor, "ROUTE_CASH_DECLARED", id, $"Déclaré {input.DeclaredAmount:0.00} DZD · attendu {route.ExpectedAtDeclaration:0.00} DZD"); changes.Driver(actor.WorkspaceId, actor.Id);
        await db.SaveChangesAsync(); return await Get(actor, id);
    }
    public async Task<object> CountCash(Actor actor, string id, CountCashInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE"); var route = await Find(actor, id); Version(route, input.Version);
        Check.That(route.CashState == "DECLARED", "Le livreur doit déclarer sa caisse avant le comptage.", "INVALID_TRANSITION", 409);
        Check.Money(input.CountedAmount, "Espèces physiquement comptées");
        var expected = RouteRules.ExpectedCash(route.OpeningFloat,(await Cash(route)).Select(x=>(x.Tendered,x.Change)));
        route.CountedAmount = input.CountedAmount; route.CountedBy = actor.Id; route.CountedAt = DateTimeOffset.UtcNow; var discrepancy=RouteRules.Discrepancy(expected,input.CountedAmount);route.Shortage=discrepancy.Shortage;route.Surplus=discrepancy.Surplus; route.FinanceNote = Check.Text(input.Note, 800, "Note", route.Shortage > 0 || route.Surplus > 0); route.CashState = "COUNTED"; route.Version++;
        orders.Audit(actor, "ROUTE_CASH_COUNTED", id, $"Compté {input.CountedAmount:0.00} DZD · manque {route.Shortage:0.00} · surplus {route.Surplus:0.00}. Les paiements commerçants restent inchangés."); changes.Driver(actor.WorkspaceId, route.DriverId);
        await db.SaveChangesAsync(); return await Get(actor, id);
    }
    public async Task<object> Reconcile(Actor actor, string id, ReconcileRouteInput input)
    {
        actor.RequireRoles("ADMIN", "FINANCE"); var route = await Find(actor, id); Version(route, input.Version);
        Check.That(route.CashState == "COUNTED", "Un comptage physique doit précéder le rapprochement.", "INVALID_TRANSITION", 409);
        var note = Check.Text(input.Note, 800, "Décision de rapprochement", route.Shortage > 0 || route.Surplus > 0);
        route.ReconciledBy = actor.Id; route.ReconciledAt = DateTimeOffset.UtcNow; route.CashState = "RECONCILED"; route.Status = "SETTLED"; route.Version++;
        if (note.Length > 0) route.FinanceNote = note;
        orders.Audit(actor, "ROUTE_CASH_RECONCILED", id, note + ". Écart de caisse conservé au niveau de la tournée, sans créer de dette commerçant."); changes.Driver(actor.WorkspaceId, route.DriverId);
        await db.SaveChangesAsync(); return await Get(actor, id);
    }
}
