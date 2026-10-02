using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Sms;

public sealed class CompositeSmsSender : ISmsSender
{
    private readonly SmsOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompositeSmsSender> _logger;

    public CompositeSmsSender(IOptions<SmsOptions> options, IServiceProvider serviceProvider, ILogger<CompositeSmsSender> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task SendAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var providers = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.DefaultProvider))
        {
            providers.Add(_options.DefaultProvider);
        }

        if (_options.FallbackProviders?.Count > 0)
        {
            providers.AddRange(_options.FallbackProviders.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        if (providers.Count == 0)
        {
            throw new InvalidOperationException("No SMS providers are configured.");
        }

        Exception? last = null;
        foreach (var provider in providers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var sender = ResolveProvider(provider);
                await sender.SendAsync(toPhoneNumber, message, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.LogWarning(ex, "SMS provider {Provider} failed; trying next if available", provider);
            }
        }

        throw new InvalidOperationException("All SMS providers failed.", last);
    }

    private ISmsSender ResolveProvider(string provider)
    {
        var p = provider.Trim().ToLowerInvariant();
        return p switch
        {
            "twilio" => _serviceProvider.GetRequiredService<TwilioSmsSender>(),
            "mnotify" or "ghanagateway" or "ghana" => _serviceProvider.GetRequiredService<GhanaGatewaySmsSender>(),
            _ => throw new InvalidOperationException($"Unknown SMS provider '{provider}'.")
        };
    }
}

