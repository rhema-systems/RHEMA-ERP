using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ErpSystem.Web.Extensions;
using ErpSystem.Web.Middleware;
using ErpSystem.Web.Services;
using ErpSystem.Data;

// Check for seed command
if (args.Length > 0 && args[0] == "seed")
{
    var tempBuilder = WebApplication.CreateBuilder(args);
    
    // Configure services for seeding
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();
    
    var tempApp = tempBuilder.Build();
    
    // Run user seeding
    await ErpSystem.Web.UserSeeder.SeedTestUsersAsync(tempApp.Services);
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Configure host shutdown timeout
builder.Host.ConfigureServices((context, services) =>
{
    services.Configure<HostOptions>(opts =>
    {
        opts.ShutdownTimeout = TimeSpan.FromSeconds(60); // Allow up to 60 seconds for graceful shutdown
    });
});

// Configure Serilog early
builder.Host.UseSerilog();

// Add all ERP System services using extension methods
builder.Services.AddErpSystemLogging(builder.Configuration);
builder.Services.AddErpSystemDatabase(builder.Configuration);
builder.Services.AddErpSystemIdentity();
builder.Services.AddErpSystemRepositories();
builder.Services.AddErpSystemServices();
builder.Services.AddErpSystemAuthorization();
builder.Services.AddErpSystemBlazor();
builder.Services.AddErpSystemSession();
builder.Services.AddErpSystemHealthChecks(builder.Configuration);
builder.Services.AddErpSystemCaching(builder.Configuration);
builder.Services.AddErpSystemWebFarm(builder.Configuration);
builder.Services.AddErpSystemSearch(builder.Configuration);
builder.Services.AddErpSystemLifecycle();
builder.Services.AddDevelopmentServices(builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Add application lifecycle management
app.UseApplicationLifecycleManagement();

// Add development middleware
app.UseSimpleDevelopmentMiddleware(app.Environment);

// Add Serilog request logging
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseResponseCaching();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/shutdown", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("shutdown")
});

app.MapRazorPages();
app.MapBlazorHub();
app.MapControllers(); // Add API controller support
app.MapFallbackToPage("/_Host");

// Initialize database and seed data
await InitializeDatabaseAsync(app);
await SeedDatabaseAsync(app);

app.Run();

async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    try
    {
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database");
    }
}

async Task SeedDatabaseAsync(WebApplication app)
{
    try
    {
        await app.Services.SeedDatabaseAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}
