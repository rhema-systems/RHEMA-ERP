using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyContractCommercialTermsService
{
    Task<QuantitySurveyContractCommercialTermsWorkspaceDto> GetWorkspaceAsync(
        Guid contractId, CancellationToken cancellationToken = default);

    Task<QuantitySurveyContractCommercialTermsWorkspaceDto> ConfigureAsync(
        Guid contractId, ConfigureQuantitySurveyContractCommercialTermsRequest request,
        string correlationId, CancellationToken cancellationToken = default);
}

public sealed class QuantitySurveyContractCommercialTermsNotFoundException(string message) : Exception(message);
public sealed class QuantitySurveyContractCommercialTermsValidationException(string message) : Exception(message);
public sealed class QuantitySurveyContractCommercialTermsConflictException(string message) : Exception(message);
