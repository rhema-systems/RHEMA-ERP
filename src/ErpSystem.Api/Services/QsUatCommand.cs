using ErpSystem.Api.Extensions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public static class QsUatCommand
{
    public static async Task RunAsync(WebApplicationBuilder builder)
    {
        builder.Services.AddErpSystemLogging(builder.Configuration);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddErpSystemCliDatabase(builder.Configuration, 600);
        builder.Services.AddErpSystemIdentity();
        builder.Services.AddErpSystemRepositories();
        builder.Services.AddScoped<QsUatSeedContext>();
        builder.Services.AddScoped<ICurrentUserService>(p => p.GetRequiredService<QsUatSeedContext>());
        builder.Services.AddScoped<ICurrentUserProvider>(p => p.GetRequiredService<QsUatSeedContext>());
        builder.Services.AddScoped<ITenantContext>(p => p.GetRequiredService<QsUatSeedContext>());
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IBusinessPartnerUserService, BusinessPartnerUserService>();
        builder.Services.AddScoped<IEstateManagedAssetService, EstateManagedAssetService>();
        builder.Services.AddScoped<ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService,
            ErpSystem.Core.Services.Workflow.WorkflowDefinitionService>();
        builder.Services.AddScoped<ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService,
            Workflow.WorkflowDefinitionServiceAdapter>();
        builder.Services.AddScoped<IQuantitySurveyConfigurationService, QuantitySurveyConfigurationService>();
        builder.Services.AddScoped<IReportsService, DatabaseReportsService>();
        builder.Services.AddScoped<IReportTemplateLifecycleService, ReportTemplateLifecycleService>();
        builder.Services.AddScoped<QuantitySurveyAccessControlSeeder>();
        builder.Services.AddScoped<CivilEngineeringAccessControlSeeder>();
        builder.Services.AddScoped<QuantitySurveyConfigurationProfileSeeder>();
        builder.Services.AddScoped<QuantitySurveyStatutoryReportSeeder>();
        builder.Services.AddScoped<QsUatActorSeeder>();
        builder.Services.AddScoped<EstateUatLandSeeder>();
        builder.Services.AddScoped<QsUatPreparationSeeder>();
        builder.Services.AddScoped<ErpSystem.Core.Interfaces.Projects.IProjectSetupService,
            ErpSystem.Core.Services.Projects.ProjectSetupService>();
        builder.Services.AddScoped<QsUatCatalogueSeeder>();
        builder.Services.AddScoped<ErpSystem.Core.Interfaces.HR.ILocationStructureService,
            ErpSystem.Core.Services.HR.LocationStructureService>();
        builder.Services.AddScoped<ErpSystem.Core.Interfaces.HR.ILocationLevelService,
            ErpSystem.Core.Services.HR.LocationLevelService>();
        builder.Services.AddScoped<ErpSystem.Core.Interfaces.HR.ILocationService,
            ErpSystem.Core.Services.HR.LocationService>();
        builder.Services.AddScoped<ErpSystem.Core.Services.Reference.IGeographyService,
            ErpSystem.Core.Services.Reference.GeographyService>();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var expected = builder.Configuration["QsUat:ExpectedDatabase"];
        QsUatActorSeeder.ValidateTarget(db.Database.GetDbConnection().Database, expected,
            builder.Environment.EnvironmentName, builder.Configuration.GetValue<bool>("QsUat:Enabled"));
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("QS_UAT_MIGRATION_PARITY_REQUIRED: deploy all migrations before preparation.");
        var defaultTenantId = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(tenant => tenant.Code == "DEFAULT" && !tenant.IsDeleted)
            .Select(tenant => tenant.Id).SingleAsync();
        Console.WriteLine("QS_UAT_STAGE|ACCESS");
        await services.GetRequiredService<QuantitySurveyAccessControlSeeder>().SeedAsync(tenantId: defaultTenantId);
        await services.GetRequiredService<CivilEngineeringAccessControlSeeder>().SeedAsync(tenantId: defaultTenantId);
        Console.WriteLine("QS_UAT_STAGE|ACTORS");
        await services.GetRequiredService<QsUatActorSeeder>().PrepareAsync();
        db.ChangeTracker.Clear();
        Console.WriteLine("QS_UAT_STAGE|LAND");
        var land = await services.GetRequiredService<EstateUatLandSeeder>().SeedAsync();
        db.ChangeTracker.Clear();
        Console.WriteLine("QS_UAT_STAGE|CATALOGUES");
        var catalogues = await services.GetRequiredService<QsUatCatalogueSeeder>().PrepareAsync();
        Console.WriteLine("QS_UAT_STAGE|CONFIGURATION");
        var preparation = await services.GetRequiredService<QsUatPreparationSeeder>().PrepareAsync(
            expected!, true, new Dictionary<string, Guid> { ["LocationId"] = catalogues.LocationId });
        var report = new { Database = expected, CapturedUtc = DateTime.UtcNow, Land = land, Catalogues = catalogues,
            Preparation = preparation, QsEndToEndVerified = false };
        var output = Path.Combine(builder.Environment.ContentRootPath, "qs-uat-preparation.json");
        await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(report,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("QS_UAT_PREPARATION|COMPLETED|Review qs-uat-preparation.json; independent configuration approval remains required.");
    }
}
