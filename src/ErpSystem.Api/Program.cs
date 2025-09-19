using Serilog;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ErpSystem.Api.Extensions;
using ErpSystem.Web.Middleware;
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
    await ErpSystem.Api.UserSeeder.SeedTestUsersAsync(tempApp.Services);
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Configure host shutdown timeout
builder.Host.ConfigureServices((context, services) =>
{
    services.Configure<HostOptions>(opts =>
    {
        opts.ShutdownTimeout = TimeSpan.FromSeconds(60);
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
builder.Services.AddErpSystemJwtAuthentication(builder.Configuration);
builder.Services.AddErpSystemAuthorization();
builder.Services.AddErpSystemApi();
builder.Services.AddErpSystemHealthChecks(builder.Configuration);
builder.Services.AddErpSystemCaching(builder.Configuration);
builder.Services.AddErpSystemWebFarm(builder.Configuration);
builder.Services.AddErpSystemSearch(builder.Configuration);
builder.Services.AddErpSystemLifecycle();
builder.Services.AddErpSystemCors(builder.Configuration);
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

// Enable Swagger in all environments for testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP System API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "ERP System API Documentation";
    c.DefaultModelsExpandDepth(-1); // Hide schemas section by default
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None); // Collapse operations by default
    c.EnableDeepLinking();
    c.EnableFilter();
    c.ShowExtensions();
    
    // Persist authorization
    c.EnablePersistAuthorization();
    
    // Add custom CSS for better UX
    c.InjectStylesheet("/swagger-ui/custom.css");
});

// Add application lifecycle management
app.UseApplicationLifecycleManagement();

// Add development middleware
app.UseSimpleDevelopmentMiddleware(app.Environment);

// Add Serilog request logging
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseResponseCaching();

app.UseRouting();

// CORS must be after UseRouting and before UseAuthentication
app.UseCors("ErpSystemCorsPolicy");

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

// API Controllers
app.MapControllers();

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
        // await app.Services.SeedDatabaseAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}