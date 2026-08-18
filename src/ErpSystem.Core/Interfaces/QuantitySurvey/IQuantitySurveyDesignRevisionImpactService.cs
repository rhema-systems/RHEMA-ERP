using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyDesignRevisionImpactService
{
    Task<QuantitySurveyDesignImpactLookupsDto> GetLookupsAsync(Guid projectId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyDesignImpactDto>> ListAsync(Guid projectId, CancellationToken token = default);
    Task<QuantitySurveyDesignImpactDto> GetAsync(Guid id, CancellationToken token = default);
    Task<QuantitySurveyDesignImpactDto> CreateAsync(CreateQuantitySurveyDesignImpactRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyDesignImpactDto> SubmitAsync(Guid id, QuantitySurveyDesignImpactLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyDesignImpactDto> ApproveAsync(Guid id, QuantitySurveyDesignImpactLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyDesignImpactDto> RejectAsync(Guid id, QuantitySurveyDesignImpactLifecycleRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyDesignImpactRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public sealed class QuantitySurveyDesignImpactNotFoundException(string message) : KeyNotFoundException(message);
public sealed class QuantitySurveyDesignImpactValidationException(string message) : InvalidOperationException(message);
public sealed class QuantitySurveyDesignImpactConflictException(string message) : InvalidOperationException(message);
