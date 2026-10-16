using System.Globalization;
using Microsoft.EntityFrameworkCore;
namespace Toushir.Wholesale;

/// <summary>Workspace-scoped operating analytics. Money, delivery and collection retain distinct bases.</summary>
public class AnalyticsService(WholesaleDb db) {
 public async Task<object> Report(Actor actor,IQueryCollection query) {
  actor.Admin();
  var zone=TimeZoneInfo.FindSystemTimeZoneById("Africa/Algiers");
  DateOnly Day(DateTimeOffset value)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value,zone).DateTime);
  var today=Day(DateTimeOffset.UtcNow);
  var days=Math.Clamp(CatalogService.Page(query["days"],30),1,366);
  var end=today;var start=end.AddDays(1-days);
  var from=query["from"].ToString();var to=query["to"].ToString();
  if(from.Length>0||to.Length>0){
   Check.That(DateOnly.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out start)&&DateOnly.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out end),"Utilisez deux dates valides au format AAAA-MM-JJ.");
   days=end.DayNumber-start.DayNumber+1;
   Check.That(days is >=1 and <=366&&end<=today,"Choisissez une période passée ou actuelle de 1 à 366 jours.");
  }
  Check.That(start.DayNumber>=days,"La période précédente doit être représentable. Choisissez une date plus récente.");
  var priorEnd=start.AddDays(-1);var priorStart=priorEnd.AddDays(1-days);
  bool In(DateTimeOffset date,DateOnly first,DateOnly last){var day=Day(date);return day>=first&&day<=last;}
  var orders=await db.Orders.AsNoTracking().Include(x=>x.Items).Where(x=>x.WorkspaceId==actor.WorkspaceId).ToListAsync();
  var payments=await db.Payments.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId).ToListAsync();
  var users=await db.Accounts.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId&&x.Role=="MERCHANT").ToListAsync();
  var offers=await db.Offers.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId).ToListAsync();
  var products=await db.Products.AsNoTracking().ToListAsync();
  var claims=await db.Claims.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId).ToListAsync();
  var credits=await db.Credits.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId).ToListAsync();
  var confirmed=(await db.OrderEvents.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId&&(x.Action=="CONFIRMED"||x.Action=="QUOTE_ACCEPTED")).Select(x=>x.OrderId).ToListAsync()).ToHashSet();
  var categoryByProduct=products.ToDictionary(x=>x.Id,x=>x.Category);
  bool Received(PurchaseOrder o)=>o.DeliveredAt!=null&&o.Status is "DELIVERED" or "PARTIALLY_DELIVERED" or "DELIVERY_REFUSED";
  bool Contract(PurchaseOrder o)=>o.Total!=null&&o.Status is "CONFIRMED" or "PREPARING" or "READY" or "OUT_FOR_DELIVERY" or "DELIVERED" or "PARTIALLY_DELIVERED" or "DELIVERY_REFUSED";
  var selected=orders.Where(o=>In(o.CreatedAt,start,end)).ToList();
  var received=orders.Where(o=>Received(o)&&In(o.DeliveredAt!.Value,start,end)).ToList();
  var current=orders.Where(Contract).ToList();
  decimal Round(decimal value)=>Math.Round(value,2,MidpointRounding.AwayFromZero);
  decimal Revenue(IEnumerable<PurchaseOrder> list)=>list.Sum(o=>o.Total??0);
  decimal Cash(DateOnly first,DateOnly last)=>payments.Where(p=>In(p.At,first,last)).Sum(p=>p.IsRefund?-p.Amount:p.Amount);
  object Metric(decimal value,decimal previous)=>new{value,previous,changePercent=previous==0?(decimal?)null:Round((value-previous)/Math.Abs(previous)*100)};
  var previousOrders=orders.Where(o=>In(o.CreatedAt,priorStart,priorEnd)).ToList();
  var previousReceived=orders.Where(o=>Received(o)&&In(o.DeliveredAt!.Value,priorStart,priorEnd)).ToList();
  object Point(DateOnly day){return new{date=day.ToString("yyyy-MM-dd"),orders=orders.Count(o=>Day(o.CreatedAt)==day),revenue=orders.Where(o=>Received(o)&&Day(o.DeliveredAt!.Value)==day).Sum(o=>o.Total??0),collected=payments.Where(p=>Day(p.At)==day).Sum(p=>p.IsRefund?-p.Amount:p.Amount)};}
  var daily=Enumerable.Range(0,days).Select(i=>Point(start.AddDays(i))).ToArray();
  var previousDaily=Enumerable.Range(0,days).Select(i=>Point(priorStart.AddDays(i))).ToArray();
  var productLines=received.SelectMany(o=>o.Items.Select(l=>new{Order=o,Line=l})).ToList();
  decimal ProductCredits(string productId,HashSet<string> orderIds)=>credits.Where(c=>orderIds.Contains(c.OrderId)&&claims.Any(cl=>cl.Id==c.ClaimId&&cl.ProductId==productId)).Sum(c=>c.Amount);
  var receivedIds=received.Select(o=>o.Id).ToHashSet();
  var topProducts=productLines.GroupBy(x=>x.Line.ProductId).Select(g=>new{productId=g.Key,name=g.First().Line.Name,imageUrl=g.First().Line.ImageUrl,category=categoryByProduct.GetValueOrDefault(g.Key,"Autres"),quantity=g.Sum(x=>x.Line.AcceptedQuantity),revenue=Round(g.Sum(x=>(x.Line.UnitPrice??0)*x.Line.AcceptedQuantity)-ProductCredits(g.Key,receivedIds))}).OrderByDescending(x=>x.revenue).ThenByDescending(x=>x.quantity).ToList();
  var categories=topProducts.GroupBy(x=>x.category).Select(g=>new{name=g.Key,revenue=Round(g.Sum(x=>x.revenue)),quantity=g.Sum(x=>x.quantity)}).OrderByDescending(x=>x.revenue).ToList();
  var merchants=users.Select(u=>new{u.Id,name=u.ShopName,contact=u.Name,orders=selected.Count(o=>o.AccountId==u.Id),revenue=Revenue(received.Where(o=>o.AccountId==u.Id)),remaining=current.Where(o=>o.AccountId==u.Id).Sum(o=>Math.Max(0,(o.Total??0)-o.Collected))}).Where(x=>x.orders>0||x.revenue!=0||x.remaining>0).OrderByDescending(x=>x.revenue).ThenByDescending(x=>x.orders).Take(12).ToList();
  var quoteCohort=selected.Where(o=>o.QuoteVersion>0).ToList();
  var receivedQuantity=received.Sum(o=>o.Items.Sum(l=>l.Quantity));var acceptedQuantity=received.Sum(o=>o.Items.Sum(l=>l.AcceptedQuantity));
  var outstanding=current.Where(o=>(o.Total??0)>o.Collected).OrderBy(o=>o.DeliveredAt??o.CreatedAt).ToList();
  object Aging(string label,int min,int? max)=>new{label,amount=outstanding.Where(o=>{var age=today.DayNumber-Day(o.DeliveredAt??o.CreatedAt).DayNumber;return age>=min&&(max==null||age<=max);}).Sum(o=>(o.Total??0)-o.Collected)};
  var tasks=orders.Where(o=>o.Status is "SUBMITTED" or "REQUESTED" or "QUOTED" or "CONFIRMED" or "PREPARING" or "READY" or "OUT_FOR_DELIVERY").OrderBy(o=>o.CreatedAt).Take(8).Select(o=>new{o.Id,o.Number,o.Status,o.Total,o.CreatedAt,shopName=users.FirstOrDefault(u=>u.Id==o.AccountId)?.ShopName??"Commerce",itemCount=o.Items.Count}).ToList();
  return new{
   generatedAt=DateTimeOffset.UtcNow,period=new{from=start.ToString("yyyy-MM-dd"),to=end.ToString("yyyy-MM-dd"),previousFrom=priorStart.ToString("yyyy-MM-dd"),previousTo=priorEnd.ToString("yyyy-MM-dd"),days,timezone="Africa/Algiers"},
   metrics=new{orders=Metric(selected.Count,previousOrders.Count),revenue=Metric(Revenue(received),Revenue(previousReceived)),collected=Metric(Cash(start,end),Cash(priorStart,priorEnd)),customers=Metric(selected.Select(o=>o.AccountId).Distinct().Count(),previousOrders.Select(o=>o.AccountId).Distinct().Count())},
   daily,previousDaily,statusCounts=selected.GroupBy(o=>o.Status).Select(g=>new{status=g.Key,count=g.Count()}).OrderByDescending(x=>x.count),categories,topProducts=topProducts.Take(8),merchants,
   operations=new{requests=orders.Count(o=>o.Status=="REQUESTED"),quotes=orders.Count(o=>o.Status=="QUOTED"),active=orders.Count(o=>o.Status is "CONFIRMED" or "PREPARING" or "READY" or "OUT_FOR_DELIVERY"),pendingMerchants=users.Count(u=>u.ApprovalStatus=="PENDING"),openClaims=claims.Count(c=>c.Status=="OPEN"),remaining=outstanding.Sum(o=>(o.Total??0)-o.Collected),refundDue=orders.Where(o=>o.Total!=null).Sum(o=>Math.Max(0,o.Collected-(o.Total??0))),unreconciled=payments.Where(p=>!p.Reconciled).Sum(p=>p.IsRefund?-p.Amount:p.Amount),unreconciledCount=payments.Count(p=>!p.Reconciled),unreconciledReceipts=payments.Where(p=>!p.Reconciled&&!p.IsRefund).Sum(p=>p.Amount),unreconciledRefunds=payments.Where(p=>!p.Reconciled&&p.IsRefund).Sum(p=>p.Amount),lowStock=offers.Count(o=>o.OfferApproved&&o.IsActive&&o.OnHand!=null&&o.OnHand-o.Reserved<=10)},
   performance=new{deliveries=received.Count,acceptedUnits=acceptedQuantity,shippedUnits=receivedQuantity,fillRate=receivedQuantity==0?(decimal?)null:Round(100m*acceptedQuantity/receivedQuantity),quoteConversion=quoteCohort.Count==0?(decimal?)null:Round(100m*quoteCohort.Count(o=>confirmed.Contains(o.Id))/quoteCohort.Count),quotedOrders=quoteCohort.Count,confirmedQuotes=quoteCohort.Count(o=>confirmed.Contains(o.Id)),averageDelivered=received.Count==0?(decimal?)null:Round(Revenue(received)/received.Count),returnedUnits=received.Sum(o=>o.Items.Sum(l=>l.RefusedQuantity)),credits=credits.Where(c=>In(c.At,start,end)).Sum(c=>c.Amount)},
   aging=new[]{Aging("0–7 jours",0,7),Aging("8–30 jours",8,30),Aging("31 jours et +",31,null)},tasks,
   basis=new{revenue="Montants nets actuels des commandes réceptionnées pendant la période, unités acceptées et livraison incluses, après avoirs explicites. Une modification d’avoir peut réviser une période passée. Aucune base HT/TTC n’est présumée.",collected="Encaissements moins remboursements réellement enregistrés à leur date, indépendamment de la date de commande.",orders="Demandes et commandes créées pendant la période; comparaison avec la période précédente de même durée.",categories="Valeur des unités acceptées, après avoirs, hors frais de livraison.",operations="Situation actuelle de l’ensemble des commandes du grossiste, indépendante du filtre de dates.",fillRate="Unités acceptées à réception / unités expédiées, refus de livraison inclus. Retours après livraison suivis séparément.",quoteConversion="Devis d’une cohorte créée dans la période ayant connu une confirmation, y compris une annulation ultérieure.",aging="Âge actuel des soldes positifs depuis la livraison ou, avant livraison, la création. Ce n’est pas une échéance contractuelle."}
  };
 }
}
