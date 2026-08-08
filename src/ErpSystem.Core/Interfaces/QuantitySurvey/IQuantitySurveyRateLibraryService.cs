using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyRateLibraryService
{
    Task<QuantitySurveyLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateLibraryPageDto> GetItemsAsync(QuantitySurveyRateLibraryListRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyMarketSurveySourceDto>> GetMarketSurveySourcesAsync(CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateLibraryItemDto> GetItemAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateLibraryItemDto> CreateItemAsync(CreateQuantitySurveyRateLibraryItemRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateLibraryItemDto> UpdateItemAsync(Guid id, UpdateQuantitySurveyRateLibraryItemRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateDto> CreateRateAsync(Guid itemId, SaveQuantitySurveyRateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateDto> PrepareMarketSurveyUpdateAsync(Guid itemId, PrepareQuantitySurveyMarketSurveyUpdateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateDto> UpdateRateAsync(Guid itemId, Guid rateId, UpdateQuantitySurveyRateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateDto> PublishRateAsync(Guid itemId, Guid rateId, QuantitySurveyRateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyRateDto> RetireRateAsync(Guid itemId, Guid rateId, QuantitySurveyRateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyRateLibraryRevisionDto>> GetHistoryAsync(Guid itemId, CancellationToken cancellationToken = default);
}

public class QuantitySurveyRateLibraryException(string message) : Exception(message);
public sealed class QuantitySurveyRateLibraryNotFoundException(string message) : QuantitySurveyRateLibraryException(message);
public sealed class QuantitySurveyRateLibraryConflictException(string message) : QuantitySurveyRateLibraryException(message);
public sealed class QuantitySurveyRateLibraryValidationException(string message) : QuantitySurveyRateLibraryException(message);
