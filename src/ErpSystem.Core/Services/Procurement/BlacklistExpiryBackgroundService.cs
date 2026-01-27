using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Background service that automatically removes expired blacklists
/// Runs daily to check for expired blacklists and remove them
/// </summary>
public class BlacklistExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BlacklistExpiryBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Run once per day

    public BlacklistExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BlacklistExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Blacklist Expiry Background Service is starting");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredBlacklistsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing expired blacklists");
                }

                // Wait for the next check interval
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            // This is expected when the application is shutting down
            _logger.LogInformation("Blacklist Expiry Background Service is being cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Blacklist Expiry Background Service");
            throw;
        }

        _logger.LogInformation("Blacklist Expiry Background Service is stopping");
    }

    private async Task ProcessExpiredBlacklistsAsync()
    {
        _logger.LogInformation("Starting expired blacklist check at {Time}", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var businessPartnerRepository = scope.ServiceProvider.GetRequiredService<IBusinessPartnerRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        try
        {
            // Get all blacklisted partners
            var blacklistedPartners = await businessPartnerRepository.GetBlacklistedPartnersAsync();
            var now = DateTime.UtcNow;
            var expiredCount = 0;

            foreach (var partner in blacklistedPartners)
            {
                // Check if blacklist has expired
                if (partner.BlacklistExpiryDate.HasValue && partner.BlacklistExpiryDate.Value <= now)
                {
                    _logger.LogInformation(
                        "Removing expired blacklist for partner {PartnerCode} - {PartnerName}. Expired on {ExpiryDate}",
                        partner.PartnerCode,
                        partner.PartnerName,
                        partner.BlacklistExpiryDate.Value);

                    // Remove blacklist
                    await businessPartnerRepository.RemoveFromBlacklistAsync(partner.Id);
                    expiredCount++;
                }
            }

            if (expiredCount > 0)
            {
                await unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} expired blacklists", expiredCount);
            }
            else
            {
                _logger.LogInformation("No expired blacklists found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired blacklists");
            throw;
        }
    }
}

