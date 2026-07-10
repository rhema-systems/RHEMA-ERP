using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/migration-signoff")]
public sealed class MigrationSignOffController : ControllerBase
{
    private readonly IMigrationSignOffService _migrationSignOffService;

    public MigrationSignOffController(IMigrationSignOffService migrationSignOffService)
    {
        _migrationSignOffService = migrationSignOffService;
    }

    [HttpPost("posting-back-references/diagnose")]
    public async Task<ActionResult<PostingBackReferenceRepairResultDto>> DiagnosePostingBackReferences(
        [FromBody] PostingBackReferenceRepairRequestDto? request,
        CancellationToken cancellationToken)
        => Ok(await _migrationSignOffService.DiagnosePostingBackReferencesAsync(request, cancellationToken));

    [HttpPost("posting-back-references/repair")]
    public async Task<ActionResult<PostingBackReferenceRepairResultDto>> RepairPostingBackReferences(
        [FromBody] PostingBackReferenceRepairRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _migrationSignOffService.RepairPostingBackReferencesAsync(request, cancellationToken));

    [HttpPost("bank-snapshots/diagnose")]
    public async Task<ActionResult<BankSnapshotRebuildResultDto>> DiagnoseBankSnapshots(
        [FromBody] BankSnapshotRebuildRequestDto? request,
        CancellationToken cancellationToken)
        => Ok(await _migrationSignOffService.DiagnoseBankSnapshotsAsync(request, cancellationToken));

    [HttpPost("bank-snapshots/rebuild")]
    public async Task<ActionResult<BankSnapshotRebuildResultDto>> RebuildBankSnapshots(
        [FromBody] BankSnapshotRebuildRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _migrationSignOffService.RebuildBankSnapshotsAsync(request, cancellationToken));

    [HttpGet("subledger-opening-scope")]
    public async Task<ActionResult<SubledgerOpeningMigrationDecisionDto>> GetSubledgerOpeningScope(
        CancellationToken cancellationToken)
        => Ok(await _migrationSignOffService.GetSubledgerOpeningMigrationDecisionAsync(cancellationToken));

    [HttpPost("final-signoff/run")]
    public async Task<ActionResult<FinalMigrationSignOffRunDto>> RunFinalSignOff(
        [FromBody] FinalMigrationSignOffRunRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _migrationSignOffService.RunFinalMigrationSignOffAsync(request, cancellationToken));

    [HttpPost("final-signoff/review")]
    public async Task<ActionResult<SignOffReviewResultDto>> ReviewFinalSignOff(
        [FromBody] SignOffReviewRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _migrationSignOffService.ReviewSignOffRunAsync(request, cancellationToken));
}
