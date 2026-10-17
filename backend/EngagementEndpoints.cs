using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Toushir.Wholesale;

public static class EngagementEndpoints
{
    public static IServiceCollection AddEngagementModule(this IServiceCollection services)
    {
        services.AddScoped<EngagementService>(); services.AddScoped<PromotionsService>(); services.AddScoped<SupplyDocumentService>(); services.AddHostedService<EngagementOutboxPublisher>(); return services;
    }
    public static WebApplication MapEngagementEndpoints(this WebApplication app, RouteGroupBuilder secure)
    {
        // secure inherits /api, authentication and MutationFilter, including transaction/rollback and live notices after commit.
        secure.MapSupplierDocumentEndpoints();
        secure.MapGet("/conversations", async (EngagementService s, HttpContext c) => Results.Ok(await s.Conversations(Actor.From(c))));
        secure.MapPost("/conversations", async (CreateConversationInput x, EngagementService s, HttpContext c) => Results.Ok(await s.CreateConversation(Actor.From(c), x)));
        secure.MapPatch("/conversations/{id}", async (string id, UpdateConversationInput x, EngagementService s, HttpContext c) => Results.Ok(await s.UpdateThread(Actor.From(c), id, x)));
        secure.MapGet("/conversations/{id}/messages", async (string id, EngagementService s, HttpContext c) => Results.Ok(await s.Messages(Actor.From(c), id)));
        secure.MapPost("/conversations/{id}/messages", async (string id, SendMessageInput x, EngagementService s, HttpContext c) => Results.Ok(await s.Send(Actor.From(c), id, x)));
        secure.MapGet("/preferences/marketing", async (EngagementService s, HttpContext c) => Results.Ok(await s.Preference(Actor.From(c))));
        secure.MapPut("/preferences/marketing", async (MarketingPreferenceInput x, EngagementService s, HttpContext c) => Results.Ok(await s.SetPreference(Actor.From(c), x)));
        secure.MapGet("/admin/broadcasts", async (EngagementService s, HttpContext c) => Results.Ok(await s.Broadcasts(Actor.From(c))));
        secure.MapPost("/admin/broadcasts", async (CreateBroadcastInput x, EngagementService s, HttpContext c) => Results.Ok(await s.CreateBroadcast(Actor.From(c), x)));
        secure.MapPost("/admin/broadcasts/{id}/dispatch", async (string id, EngagementService s, HttpContext c) => Results.Ok(await s.DispatchBroadcast(Actor.From(c), id)));
        secure.MapGet("/campaigns", async (EngagementService s, HttpContext c) => Results.Ok(await s.Campaigns(Actor.From(c))));
        secure.MapGet("/admin/campaigns", async (EngagementService s, HttpContext c) => Results.Ok(await s.Campaigns(Actor.From(c), true)));
        secure.MapPost("/admin/campaigns", async (CreateCampaignInput x, EngagementService s, HttpContext c) => Results.Ok(await s.CreateCampaign(Actor.From(c), x)));
        secure.MapPost("/admin/campaigns/{id}/publish", async (string id, VersionInput x, EngagementService s, HttpContext c) => Results.Ok(await s.PublishCampaign(Actor.From(c), id, x)));
        secure.MapPost("/campaigns/{id}/preview", async (string id, CampaignPreviewInput x, EngagementService s, HttpContext c) => Results.Ok(await s.PreviewCampaign(Actor.From(c), id, x)));
        secure.MapGet("/campaigns/{id}/progress", async (string id, string? merchantId, EngagementService s, HttpContext c) => Results.Ok(await s.Progress(Actor.From(c), id, merchantId)));
        secure.MapPost("/admin/campaigns/{id}/award", async (string id, AwardCampaignInput x, EngagementService s, HttpContext c) => Results.Ok(await s.Award(Actor.From(c), id, x)));
        secure.MapGet("/loyalty", async (string? merchantId, EngagementService s, HttpContext c) => Results.Ok(await s.Loyalty(Actor.From(c), merchantId)));
        secure.MapPost("/loyalty/sync", async (SyncLoyaltyInput x, EngagementService s, HttpContext c) => Results.Ok(await s.SyncLoyalty(Actor.From(c), x)));
        secure.MapGet("/rewards", async (EngagementService s, HttpContext c) => Results.Ok(await s.Rewards(Actor.From(c))));
        secure.MapPost("/admin/rewards", async (RewardItemInput x, EngagementService s, HttpContext c) => Results.Ok(await s.SaveReward(Actor.From(c), null, x)));
        secure.MapPatch("/admin/rewards/{id}", async (string id, RewardItemInput x, EngagementService s, HttpContext c) => Results.Ok(await s.SaveReward(Actor.From(c), id, x)));
        secure.MapPost("/rewards/{id}/reserve", async (string id, ReserveGiftInput x, EngagementService s, HttpContext c) => Results.Ok(await s.ReserveGift(Actor.From(c), id, x)));
        secure.MapGet("/gift-requests", async (string? merchantId, EngagementService s, HttpContext c) => Results.Ok(await s.Gifts(Actor.From(c), merchantId)));
        secure.MapGet("/orders/{orderId}/promotions", async (string orderId, PromotionsService s, HttpContext c) => Results.Ok(await s.View(Actor.From(c), orderId)));
        secure.MapPost("/orders/{orderId}/promotions/{id}/gift-receipt", async (string orderId, string id, PromotionGiftReceiptInput x, PromotionsService s, HttpContext c) => Results.Ok(await s.GiftReceipt(Actor.From(c), orderId, id, x)));
        secure.MapPost("/orders/{orderId}/promotions/{id}/gift-return", async (string orderId, string id, PromotionGiftDispositionInput x, PromotionsService s, HttpContext c) => Results.Ok(await s.GiftReturn(Actor.From(c), orderId, id, x, false)));
        secure.MapPost("/orders/{orderId}/promotions/{id}/gift-inspect", async (string orderId, string id, PromotionGiftDispositionInput x, PromotionsService s, HttpContext c) => Results.Ok(await s.GiftReturn(Actor.From(c), orderId, id, x, true)));
        secure.MapPost("/admin/gift-requests/{id}/transition", async (string id, GiftTransitionInput x, EngagementService s, HttpContext c) => Results.Ok(await s.TransitionGift(Actor.From(c), id, x)));
        secure.MapGet("/admin/suppliers", async (EngagementService s, HttpContext c) => Results.Ok(await s.Suppliers(Actor.From(c))));
        secure.MapPost("/admin/suppliers", async (SupplierInput x, EngagementService s, HttpContext c) => Results.Ok(await s.CreateSupplier(Actor.From(c), x)));
        secure.MapGet("/admin/purchases", async (EngagementService s, HttpContext c) => Results.Ok(await s.Purchases(Actor.From(c))));
        secure.MapGet("/admin/purchases/{id}", async (string id, EngagementService s, HttpContext c) => Results.Ok(await s.Purchase(Actor.From(c), id)));
        secure.MapPost("/admin/purchases", async (CreateSupplyInput x, EngagementService s, HttpContext c) => Results.Ok(await s.CreatePurchase(Actor.From(c), x)));
        secure.MapPost("/admin/purchases/{id}/approve", async (string id, VersionInput x, EngagementService s, HttpContext c) => Results.Ok(await s.ApprovePurchase(Actor.From(c), id, x)));
        secure.MapPost("/admin/purchases/{id}/receive", async (string id, ReceiveSupplyInput x, EngagementService s, HttpContext c) => Results.Ok(await s.ReceivePurchase(Actor.From(c), id, x)));
        secure.MapPost("/admin/purchases/{id}/inspect", async (string id, InspectSupplyInput x, EngagementService s, HttpContext c) => Results.Ok(await s.InspectSupply(Actor.From(c), id, x)));
        secure.MapPost("/admin/purchases/{id}/invoice-review", async (string id, InvoiceReviewInput x, EngagementService s, HttpContext c) => Results.Ok(await s.ReviewInvoice(Actor.From(c), id, x)));
        secure.MapGet("/admin/engagement/outbox", async (WholesaleDb db, HttpContext c) =>
        {
            var actor = Actor.From(c); actor.Admin(); var items = await db.EngagementNotices.AsNoTracking().Where(x => x.WorkspaceId == actor.WorkspaceId).ToListAsync();
            return Results.Ok(new { pending = items.Count(x => x.PublishedAt == null), items = items.OrderByDescending(x => x.CreatedAt).Take(100) });
        });
        return app;
    }
}

/// <summary>Durable at-least-once invalidation notices. Notification failure never rolls back a committed business action.</summary>
public sealed class EngagementOutboxPublisher(IServiceScopeFactory scopes, IHubContext<CommerceHub> hub, ILogger<EngagementOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<WholesaleDb>();
                    var notices = await db.EngagementNotices.Where(x => x.PublishedAt == null && x.Attempts < 20).Take(100).ToListAsync(stoppingToken);
                    foreach (var notice in notices)
                    {
                        notice.Attempts++;
                        try
                        {
                            var payload = new { eventId = notice.Id, kind = notice.Kind, aggregateId = notice.AggregateId, at = notice.CreatedAt };
                            await hub.Clients.Group(CommerceHub.Operations(notice.WorkspaceId)).SendAsync("engagement.updated", payload, stoppingToken);
                            if (notice.MerchantId.Length > 0) await hub.Clients.Group(CommerceHub.Account(notice.WorkspaceId, notice.MerchantId)).SendAsync("engagement.updated", payload, stoppingToken);
                            notice.PublishedAt = DateTimeOffset.UtcNow;
                        }
                        catch (Exception error) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(error, "Engagement notice {EventId} will retry", notice.Id); }
                    }
                    if (notices.Count > 0) await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(error, "Engagement outbox poll failed; committed operations remain available."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
