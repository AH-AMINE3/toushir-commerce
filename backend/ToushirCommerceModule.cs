using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Toushir.Wholesale;

[DependsOn(typeof(AbpAspNetCoreMvcModule), typeof(AbpAutofacModule), typeof(AbpEntityFrameworkCoreModule), typeof(AbpDddApplicationModule))]
public sealed class ToushirCommerceModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<WholesaleDb>();
        // Minimal API transactions and all domain services must share one context per request.
        // ABP's default context lifetime is transient; preserve its factory while making this context scoped.
        var registrations = context.Services.Where(x => x.ServiceType == typeof(WholesaleDb)).ToList();
        var registration = registrations.Last();
        foreach (var item in registrations) context.Services.Remove(item);
        context.Services.Add(registration.ImplementationFactory != null
            ? ServiceDescriptor.Scoped(typeof(WholesaleDb), registration.ImplementationFactory)
            : ServiceDescriptor.Scoped(typeof(WholesaleDb), registration.ImplementationType ?? typeof(WholesaleDb)));
        Configure<AbpDbContextOptions>(options => options.Configure<WholesaleDb>(db =>
        {
            var config = context.Services.GetConfiguration();
            var environment = context.Services.GetHostingEnvironment();
            var path = config["DataPath"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
            db.DbContextOptions.UseOpenIddict();
            DatabaseConfiguration.Configure(db.DbContextOptions, config["DATABASE_URL"], Path.Combine(path, "toushir-wholesale.db"), config["Database:Schema"]);
        }));
        Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
    }
}
