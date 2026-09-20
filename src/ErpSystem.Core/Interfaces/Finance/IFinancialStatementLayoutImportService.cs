using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinancialStatementLayoutImportService
{
    Task<FinancialStatementLayoutFileDto> CreateWorkbookTemplateAsync(
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutImportDefinitionDto> ExportDefinitionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutFileDto> ExportWorkbookAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutImportPreviewDto> PreviewDefinitionAsync(
        FinancialStatementLayoutImportDefinitionDto definition,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutImportResultDto> CommitDefinitionAsync(
        FinancialStatementLayoutImportCommitDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutImportPreviewDto> PreviewWorkbookAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutImportResultDto> CommitWorkbookAsync(
        Stream stream,
        string fileName,
        string expectedDefinitionHash,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutImportPreviewDto> PreviewLegacyMigrationAsync(
        LegacyFinancialStatementLayoutMigrationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FinancialStatementLayoutImportResultDto> CommitLegacyMigrationAsync(
        LegacyFinancialStatementLayoutMigrationCommitDto request,
        CancellationToken cancellationToken = default);
}
