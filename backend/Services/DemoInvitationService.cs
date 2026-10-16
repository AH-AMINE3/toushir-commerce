using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

// A merchant invitation never contains the workspace owner capability.
// Joining creates a scoped merchant account; the owner sees its real test orders.
public class DemoInvitationService(WholesaleDb db,AuthService auth,PasswordHasher<Account> hasher,IConfiguration config,OrderService orders) {
 public static async Task EnsureSchema(WholesaleDb db){
  if(!db.Database.IsSqlite())return;
  await db.Database.ExecuteSqlRawAsync("""
   CREATE TABLE IF NOT EXISTS DemoInvitations (Id TEXT NOT NULL PRIMARY KEY, WorkspaceId TEXT NOT NULL, TokenHash TEXT NOT NULL, Label TEXT NOT NULL, MaxUses INTEGER NOT NULL, Uses INTEGER NOT NULL, ExpiresAt TEXT NOT NULL, RevokedAt TEXT NULL, CreatedAt TEXT NOT NULL, CreatedBy TEXT NOT NULL);
   CREATE UNIQUE INDEX IF NOT EXISTS IX_DemoInvitations_TokenHash ON DemoInvitations (TokenHash);
   CREATE INDEX IF NOT EXISTS IX_DemoInvitations_WorkspaceId ON DemoInvitations (WorkspaceId);
   CREATE TABLE IF NOT EXISTS DemoGuests (Id TEXT NOT NULL PRIMARY KEY, InvitationId TEXT NOT NULL, AccountId TEXT NOT NULL, JoinKeyHash TEXT NOT NULL);
   CREATE UNIQUE INDEX IF NOT EXISTS IX_DemoGuests_AccountId ON DemoGuests (AccountId);
   CREATE UNIQUE INDEX IF NOT EXISTS IX_DemoGuests_InvitationId_JoinKeyHash ON DemoGuests (InvitationId, JoinKeyHash);
   """);
 }
 async Task<Workspace> Owner(Actor a){a.Admin();Enabled();var w=await db.Workspaces.FindAsync(a.WorkspaceId);Check.That(w is {IsDemo:true}&&w.CreatedAt.AddDays(7)>DateTimeOffset.UtcNow,"L’invitation est disponible dans un espace de test actif.","DEMO_EXPIRED",403);return w;}
 void Enabled()=>Check.That(config.GetValue<bool>("Demo:Enabled"),"Les invitations de test ne sont pas disponibles.","DEMO_DISABLED",404);
 static object View(DemoInvitation i)=>new{i.Id,i.Label,i.MaxUses,i.Uses,i.ExpiresAt,i.RevokedAt,i.CreatedAt};
 public async Task<object> List(Actor a){await Owner(a);var list=await db.DemoInvitations.AsNoTracking().Where(x=>x.WorkspaceId==a.WorkspaceId).ToListAsync();return new{items=list.OrderByDescending(x=>x.CreatedAt).Select(View)};}
 public async Task<object> Create(Actor a,InvitationInput x){var w=await Owner(a);Check.That(x.Hours is >=1 and <=168&&x.MaxUses is >=1 and <=100,"Durée : 1 à 168 heures. Accès : 1 à 100 commerces.");var all=await db.DemoInvitations.Where(x=>x.WorkspaceId==a.WorkspaceId).ToListAsync();Check.That(all.Count(x=>x.RevokedAt==null&&x.ExpiresAt>DateTimeOffset.UtcNow)<20,"Maximum 20 invitations actives.","INVITE_LIMIT",409);var token=Tokens.New();var end=DateTimeOffset.UtcNow.AddHours(x.Hours);var limit=w.CreatedAt.AddDays(7);var invite=new DemoInvitation{WorkspaceId=a.WorkspaceId,TokenHash=Tokens.Hash(token),Label=Check.Text(x.Label,100,"Libellé"),MaxUses=x.MaxUses,ExpiresAt=end<limit?end:limit,CreatedBy=a.Id};db.DemoInvitations.Add(invite);orders.Audit(a,"MERCHANT_INVITATION_CREATED",invite.Id,$"{invite.Label}: {invite.MaxUses} accès de test");await db.SaveChangesAsync();return new{invite.Id,invite.Label,invite.ExpiresAt,invite.MaxUses,inviteToken=token,joinPath="/join/"+token};}
 public async Task<object> Revoke(Actor a,string id){await Owner(a);var invite=await db.DemoInvitations.FirstOrDefaultAsync(x=>x.Id==id&&x.WorkspaceId==a.WorkspaceId);Check.That(invite!=null,"Invitation introuvable.","NOT_FOUND",404);invite!.RevokedAt??=DateTimeOffset.UtcNow;orders.Audit(a,"MERCHANT_INVITATION_REVOKED",id);await db.SaveChangesAsync();return new{ok=true};}
 async Task<DemoInvitation> Find(string token){Enabled();Check.That(token is {Length:64}&&token.All(Uri.IsHexDigit),"Invitation indisponible ou expirée.","INVITE_UNAVAILABLE",404);var hash=Tokens.Hash(token);var i=await db.DemoInvitations.FirstOrDefaultAsync(x=>x.TokenHash==hash);Check.That(i!=null&&i.RevokedAt==null&&i.ExpiresAt>DateTimeOffset.UtcNow,"Invitation indisponible ou expirée. Demandez un nouveau lien au grossiste.","INVITE_UNAVAILABLE",404);var w=await db.Workspaces.FindAsync(i!.WorkspaceId);Check.That(w is {IsDemo:true}&&w.CreatedAt.AddDays(7)>DateTimeOffset.UtcNow,"Cet espace de test a expiré.","DEMO_EXPIRED",404);return i;}
 public async Task<object> Describe(string token){var i=await Find(token);return new{wholesaler="Toushir Distribution · Oran",i.ExpiresAt,available=i.Uses<i.MaxUses,isDemo=true,joinKey=Tokens.New()};}
 public async Task<object> Join(JoinDemoInput x,HttpContext c){var i=await Find(x.InviteToken);Check.That(x.JoinKey is {Length:>=32 and <=128},"Identifiant de connexion invalide.");var keyHash=Tokens.Hash(x.JoinKey);var previous=await db.DemoGuests.FirstOrDefaultAsync(g=>g.InvitationId==i.Id&&g.JoinKeyHash==keyHash);if(previous!=null){var existing=await db.Accounts.FindAsync(previous.AccountId);Check.That(existing is {ApprovalStatus:"APPROVED"},"Cet accès est suspendu.","FORBIDDEN",403);return await auth.Issue(existing!,c);}
  var claimed=await db.DemoInvitations.Where(v=>v.Id==i.Id&&v.Uses<v.MaxUses).ExecuteUpdateAsync(set=>set.SetProperty(v=>v.Uses,v=>v.Uses+1));Check.That(claimed==1,"Cette invitation a atteint son nombre de commerces. Demandez un nouveau lien au grossiste.","INVITE_FULL",409);
  var account=new Account{WorkspaceId=i.WorkspaceId,Name=Check.Text(x.Name,100,"Votre nom"),ShopName=Check.Text(x.ShopName,150,"Nom du commerce"),Phone=Check.Text(x.Phone,30,"Téléphone",false),Email=$"invite-{IdGen.New()}@demo.invalid",ApprovalStatus="APPROVED"};account.PasswordHash=hasher.HashPassword(account,Tokens.New());db.Accounts.Add(account);db.DemoGuests.Add(new(){InvitationId=i.Id,AccountId=account.Id,JoinKeyHash=keyHash});db.Addresses.Add(AuthService.MakeAddress(account,new("Mon commerce","Oran",string.IsNullOrWhiteSpace(x.Commune)?"Oran":x.Commune,string.IsNullOrWhiteSpace(x.Street)?"Adresse de test à confirmer avant livraison":x.Street,true)));orders.Audit(new(account.Id,account.WorkspaceId,account.Name,account.Role,account.ApprovalStatus),"MERCHANT_INVITATION_JOINED",i.Id,"Commerce de test créé via invitation");await db.SaveChangesAsync();return await auth.Issue(account,c);
 }
}
