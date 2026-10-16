using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

/// <summary>Explicit, isolated demonstration fixtures built through the normal business services.</summary>
public class DemoScenarioService(WholesaleDb db,OrderService orders,AdminService admin,CatalogService catalog,IConfiguration config,PasswordHasher<Account> hasher) {
 const string Marker="DEMO_SCENARIO_CREATED";
 const string Example="EXEMPLE — scénario fictif : prix, stocks, règlements et boutiques de démonstration. Aucune opération commerciale réelle.";

 public async Task<object> Seed(Actor actor) {
  actor.Admin();
  Check.That(config.GetValue<bool>("Demo:Enabled"),"Le scénario est réservé à la démonstration explicitement activée.","DEMO_DISABLED",404);
  var space=await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==actor.WorkspaceId);
  Check.That(space?.IsDemo==true,"Aucune donnée fictive ne peut être ajoutée à un espace commercial réel.","DEMO_ONLY",403);
  await using var ownedTransaction=db.Database.CurrentTransaction==null?await db.Database.BeginTransactionAsync():null;
  if(await db.Audit.AnyAsync(x=>x.WorkspaceId==actor.WorkspaceId&&x.Action==Marker))return new{seeded=false,orders=0};

  var products=await FeaturedProducts(actor);
  var current=await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x=>x.WorkspaceId==actor.WorkspaceId&&x.Role=="MERCHANT"&&x.ApprovalStatus=="APPROVED");
  Check.That(current!=null,"Une persona commerçant approuvée est nécessaire pour explorer le scénario.","MERCHANT_REQUIRED",409);
  var merchants=new List<Account>{current!};
  var now=DateTimeOffset.UtcNow;
  var shops=new[]{("Supérette Es Sénia · EXEMPLE","Es Sénia"),("Épicerie Bir El Djir · EXEMPLE","Bir El Djir"),("Alimentation Akid Lotfi · EXEMPLE","Oran")};
  for(var i=0;i<shops.Length;i++) {
   var user=new Account{WorkspaceId=actor.WorkspaceId,Email=$"scenario-{i}-{actor.WorkspaceId}@demo.invalid",Name=$"Commerçant {i+2} · EXEMPLE",ShopName=shops[i].Item1,Phone="Exemple",ApprovalStatus="APPROVED",CreatedAt=now.AddDays(-60)};
   user.PasswordHash=hasher.HashPassword(user,Tokens.New());
   db.Accounts.Add(user);
   db.Addresses.Add(new(){WorkspaceId=actor.WorkspaceId,AccountId=user.Id,Label="Boutique · EXEMPLE",Wilaya="Oran",Commune=shops[i].Item2,Street="Adresse fictive de démonstration",IsDefault=true});
   merchants.Add(user);
  }
  await db.SaveChangesAsync();
  var addresses=await db.Addresses.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId).ToListAsync();
  Check.That(merchants.All(m=>addresses.Any(x=>x.AccountId==m.Id)),"Chaque boutique du scénario doit disposer d'une adresse.","ADDRESS_REQUIRED",409);
  // Creating an order normally consumes its cart. Preserve any visitor's existing basket.
  var merchantIds=merchants.Select(x=>x.Id).ToList();
  var originalCart=await db.CartItems.AsNoTracking().Where(x=>merchantIds.Contains(x.AccountId)).ToListAsync();
  foreach(var product in products) {
   await admin.Adjust(actor,new(product.Id,2000,Example),catalog);
   var initial=await db.Inventory.Where(x=>x.WorkspaceId==actor.WorkspaceId&&x.ProductId==product.Id&&x.OrderId==null&&x.Reason==Example).ToListAsync();
   foreach(var entry in initial)entry.At=now.AddDays(-56);
   await db.SaveChangesAsync();
   db.ChangeTracker.Clear();
  }

  var fixturePrices=new[]{145m,275m,95m}; // Deliberately fictional; never published as catalog offers.
  for(var i=0;i<40;i++) {
   var merchant=merchants[i%merchants.Count];
   var buyer=new Actor(merchant.Id,actor.WorkspaceId,merchant.Name,"MERCHANT","APPROVED");
   var key=$"demo-scenario-v1-{i:00}";
   var lines=Enumerable.Range(0,1+i%3).Select(j=>new LineInput(products[(i+j)%products.Count].Id,6+((i*3+j*5)%19))).ToList();
   var address=addresses.First(x=>x.AccountId==merchant.Id);
   await orders.Create(buyer,new(key,address.Id,lines,Example,"REQUEST_QUOTE"));
   var order=await db.Orders.Include(x=>x.Items).SingleAsync(x=>x.WorkspaceId==actor.WorkspaceId&&x.AccountId==merchant.Id&&x.IdempotencyKey==key);
   var status=i>=32?new[]{"REQUESTED","QUOTED","CONFIRMED","PREPARING","READY","OUT_FOR_DELIVERY","REQUESTED","QUOTED"}[i-32]:i%13==5?"CANCELLED":i%11==4?"PARTIALLY_DELIVERED":"DELIVERED";
   if(status!="REQUESTED") {
    var quoted=lines.Select(l=>new QuoteLineInput(l.ProductId,l.Quantity,fixturePrices[products.FindIndex(p=>p.Id==l.ProductId)],1,null)).ToList();
    await orders.Quote(actor,order.Id,new(quoted,150m,now.AddDays(14),Example));
    if(status!="QUOTED") {
     await orders.Accept(buyer,order.Id,new(1));
     if(status=="CANCELLED") {
      if(i%2==1)await orders.Collect(actor,order.Id,new(300m,"CASH","EXEMPLE · acompte fictif",$"{key}-receipt"));
      await orders.Cancel(buyer,order.Id,new("EXEMPLE · annulation fictive demandée par la boutique"));
     } else if(status!="CONFIRMED") {
      await orders.Transition(actor,order.Id,new("PREPARING",Example));
      if(status!="PREPARING") {
       await orders.Transition(actor,order.Id,new("READY",Example));
       if(status!="READY") {
        await orders.Transition(actor,order.Id,new("OUT_FOR_DELIVERY",Example));
        if(status!="OUT_FOR_DELIVERY") {
         var delivery=order.Items.Select((l,j)=>new DeliveryLineInput(l.ProductId,l.Quantity-(status=="PARTIALLY_DELIVERED"&&j==0?2:0),status=="PARTIALLY_DELIVERED"&&j==0?2:0)).ToList();
         await orders.Delivery(actor,order.Id,new(delivery,Example));
         if(status=="PARTIALLY_DELIVERED"&&i%2==0)await orders.Inspect(actor,order.Id,new(delivery[0].ProductId,2,"RESTOCK",Example));
         if(i%5!=0) {
          var amount=i%4==0?decimal.Round(order.Total!.Value/2,2):order.Total!.Value;
          await orders.Collect(actor,order.Id,new(amount,i%3==0?"TRANSFER":"CASH","EXEMPLE · règlement fictif",$"{key}-receipt"));
          if(i%3!=1) {
           var payment=await db.Payments.SingleAsync(x=>x.OrderId==order.Id&&x.WorkspaceId==actor.WorkspaceId&&!x.IsRefund);
           await orders.Reconcile(actor,payment.Id);
          }
         }
        }
       }
      }
     }
    }
   }
   await ShiftHistory(actor,order.Id,now.AddDays(-((39-i)*55/39)).AddHours(-2));
   db.ChangeTracker.Clear(); // Conditional stock SQL must be re-read before the next order.
  }
  db.CartItems.AddRange(originalCart);
  orders.Audit(actor,Marker,actor.WorkspaceId,$"40 commandes, 4 boutiques et 3 références. {Example}");
  await db.SaveChangesAsync();
  if(ownedTransaction!=null)await ownedTransaction.CommitAsync();
  return new{seeded=true,orders=40};
 }

 async Task<List<Product>> FeaturedProducts(Actor actor) {
  var directory=config["Catalog:Directory"]??Path.GetDirectoryName(config["Catalog:Path"]??"")??"../catalog";
  var file=Path.Combine(directory,"featured-barcodes.json");
  var barcodes=File.Exists(file)?JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(file)):new(){"6130093010069","6130093067261","6130760007422","6130760003349","6130695111348"};
  var eligible=await db.Products.AsNoTracking().Where(x=>x.IsCatalogActive&&barcodes!.Contains(x.Barcode)).ToListAsync();
  var inactive=(await db.Offers.AsNoTracking().Where(x=>x.WorkspaceId==actor.WorkspaceId&&!x.IsActive).Select(x=>x.ProductId).ToListAsync()).ToHashSet();
  var selected=eligible.Where(x=>!inactive.Contains(x.Id)).OrderBy(x=>barcodes!.IndexOf(x.Barcode)).Take(3).ToList();
  Check.That(selected.Count==3,"Trois références de découverte actives sont nécessaires pour ce scénario.","CATALOG_NOT_READY",409);
  return selected;
 }

 async Task ShiftHistory(Actor actor,string id,DateTimeOffset createdAt) {
  var order=await db.Orders.FirstAsync(x=>x.Id==id&&x.WorkspaceId==actor.WorkspaceId);
  var offset=createdAt-order.CreatedAt;
  order.Number=$"EXEMPLE-{createdAt:yyMMdd}-{order.Id[..6].ToUpperInvariant()}";
  order.CreatedAt=createdAt;order.UpdatedAt+=offset;
  if(order.DeliveredAt!=null)order.DeliveredAt+=offset;
  if(order.QuoteValidUntil!=null&&order.Status!="QUOTED")order.QuoteValidUntil+=offset;
  foreach(var entry in await db.OrderEvents.Where(x=>x.WorkspaceId==actor.WorkspaceId&&x.OrderId==id).ToListAsync())entry.At+=offset;
  foreach(var entry in await db.Payments.Where(x=>x.WorkspaceId==actor.WorkspaceId&&x.OrderId==id).ToListAsync())entry.At+=offset;
  foreach(var entry in await db.Inventory.Where(x=>x.WorkspaceId==actor.WorkspaceId&&x.OrderId==id).ToListAsync())entry.At+=offset;
  foreach(var entry in await db.Audit.Where(x=>x.WorkspaceId==actor.WorkspaceId&&x.EntityId==id).ToListAsync())entry.At+=offset;
  await db.SaveChangesAsync();
 }
}
