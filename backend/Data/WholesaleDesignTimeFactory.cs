using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Toushir.Wholesale;

/// <summary>Builds the ABP data model without starting ASP.NET hosting, provisioning accounts or accessing a database.</summary>
public sealed class WholesaleDesignTimeFactory : IDesignTimeDbContextFactory<WholesaleDb>
{
    public WholesaleDb CreateDbContext(string[] args) => Task.Run(async () =>
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
        var application = await AbpApplicationFactory.CreateAsync<CommerceDesignTimeModule>(options =>
        {
            options.UseAutofac();
            options.Services.AddSingleton<IConfiguration>(configuration);
        });
        await application.InitializeAsync();
        return application.ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<WholesaleDb>();
    }).GetAwaiter().GetResult();
}

[DependsOn(typeof(AbpEntityFrameworkCoreModule), typeof(AbpAutofacModule))]
public sealed class CommerceDesignTimeModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddAbpDbContext<WholesaleDb>();
        Configure<AbpDbContextOptions>(options => options.Configure<WholesaleDb>(database =>
        {
            database.DbContextOptions.UseOpenIddict();
            DatabaseConfiguration.Configure(database.DbContextOptions, configuration["DATABASE_URL"], Path.Combine(Path.GetTempPath(), "toushir-design.db"), configuration["Database:Schema"]);
        }));
    }
}
