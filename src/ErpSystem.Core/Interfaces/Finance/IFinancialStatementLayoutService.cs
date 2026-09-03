using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinancialStatementLayoutService
{
    Task<IReadOnlyList<FinancialStatementLayoutSummaryDto>> GetLayoutsAsync(
        FinancialStatementType? statementType = null,
        Guid? accountingBookId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutDto?> GetLayoutAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialStatementLayoutAuditEventDto>> GetAuditTrailAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutDto> CreateLayoutAsync(
        CreateFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutDto> UpdateLayoutAsync(
        Guid layoutId,
        UpdateFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutDto> CloneProtectedStandardAsync(
        Guid sourceLayoutId,
        CloneFinancialStatementLayoutDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutVersionDto> CreateDraftVersionAsync(
        Guid layoutId,
        CreateFinancialStatementLayoutVersionDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutVersionDto> ReplaceDraftRowsAsync(
        Guid versionId,
        ReplaceFinancialStatementRowsDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutValidationResultDto> ValidateVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutValidationResultDto> ValidateDefinitionAsync(
        FinancialStatementType statementType,
        Guid accountingBookId,
        IReadOnlyList<FinancialStatementRowInputDto> rows,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutVersionDto> PublishVersionAsync(
        Guid versionId,
        PublishFinancialStatementLayoutVersionDto request,
        CancellationToken cancellationToken = default);
}
