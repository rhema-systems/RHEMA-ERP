using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IMigrationSignOffService
{
    Task<PostingBackReferenceRepairResultDto> DiagnosePostingBackReferencesAsync(
        PostingBackReferenceRepairRequestDto? request = null,
        CancellationToken cancellationToken = default);

    Task<PostingBackReferenceRepairResultDto> RepairPostingBackReferencesAsync(
        PostingBackReferenceRepairRequestDto request,
        CancellationToken cancellationToken = default);

    Task<BankSnapshotRebuildResultDto> DiagnoseBankSnapshotsAsync(
        BankSnapshotRebuildRequestDto? request = null,
        CancellationToken cancellationToken = default);

    Task<BankSnapshotRebuildResultDto> RebuildBankSnapshotsAsync(
        BankSnapshotRebuildRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SubledgerOpeningMigrationDecisionDto> GetSubledgerOpeningMigrationDecisionAsync(
        CancellationToken cancellationToken = default);

    Task<FinalMigrationSignOffRunDto> RunFinalMigrationSignOffAsync(
        FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken = default);

    Task<SignOffReviewResultDto> ReviewSignOffRunAsync(
        SignOffReviewRequestDto request,
        CancellationToken cancellationToken = default);
}
