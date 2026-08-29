using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementContractBudgetLifecycleRegressionTests
{
    [Fact]
    public void Reservation_is_deferred_until_the_final_activation_transaction()
    {
        var source = ReadContractActivationService();
        var submitStart = source.IndexOf(
            "public async Task<ProcurementContractActivationDto> SubmitAsync(",
            StringComparison.Ordinal);
        var decideStart = source.IndexOf(
            "public async Task<ProcurementContractActivationDto> DecideAsync(",
            submitStart,
            StringComparison.Ordinal);
        var activateStart = source.IndexOf(
            "public async Task<ContractDto> ActivateAsync(",
            decideStart,
            StringComparison.Ordinal);
        var evaluationStart = source.IndexOf(
            "private async Task<Evaluation> EvaluateAsync(",
            activateStart,
            StringComparison.Ordinal);
        var availabilityStart = source.IndexOf(
            "private async Task<ProcurementContractActivationCheckDto> EvaluateBudgetAvailabilityAsync(",
            evaluationStart,
            StringComparison.Ordinal);
        var reservationHelperStart = source.IndexOf(
            "private async Task EnsureBudgetCommitmentForActivationAsync(",
            availabilityStart,
            StringComparison.Ordinal);

        submitStart.Should().BeGreaterThanOrEqualTo(0);
        decideStart.Should().BeGreaterThan(submitStart);
        activateStart.Should().BeGreaterThan(decideStart);
        evaluationStart.Should().BeGreaterThan(activateStart);
        availabilityStart.Should().BeGreaterThan(evaluationStart);
        reservationHelperStart.Should().BeGreaterThan(availabilityStart);

        var submitBody = source[submitStart..decideStart];
        submitBody.Should().Contain("await EvaluateAsync(")
            .And.NotContain("EnsureBudgetCommitmentForActivationAsync(",
                "submission validates availability but must not reserve funds");

        var availabilityBody = source[availabilityStart..reservationHelperStart];
        availabilityBody.Should().Contain("_budgetControl.GetDownstreamReadinessAsync(")
            .And.NotContain("ReserveForDownstreamAsync(",
                "pre-activation checks must remain read-only")
            .And.Contain("GetNetCommittedExposureAsync(",
                "advisory readiness must use the same net exposure calculation as activation")
            .And.Contain("committedExposure + contract.ContractValue",
                "submission availability must cover cumulative direct formal exposure")
            .And.Contain("requiredExposure,");

        var activateBody = source[activateStart..evaluationStart];
        var executeStart = activateBody.IndexOf(
            "await ExecuteAsync(async () =>", StringComparison.Ordinal);
        var reserveStart = activateBody.IndexOf(
            "await EnsureBudgetCommitmentForActivationAsync(",
            StringComparison.Ordinal);
        var formalCommitStart = activateBody.IndexOf(
            "await _budgetCommitments.CommitContractAsync(",
            StringComparison.Ordinal);
        var formalCommitSave = activateBody.IndexOf(
            "await _unitOfWork.SaveChangesAsync(",
            formalCommitStart,
            StringComparison.Ordinal);
        var activeStatusStart = activateBody.IndexOf(
            "activation.Contract.Status = \"Active\";",
            StringComparison.Ordinal);
        executeStart.Should().BeGreaterThanOrEqualTo(0);
        reserveStart.Should().BeGreaterThan(executeStart,
            "the reservation must be owned by the final activation transaction");
        formalCommitStart.Should().BeGreaterThan(reserveStart,
            "the reserved exposure must be converted to the contract commitment immediately afterwards");
        activateBody[reserveStart..formalCommitStart].Should().NotContain("SaveChangesAsync(",
            "no independent persistence boundary may split reservation from formal commitment");
        formalCommitSave.Should().BeGreaterThan(formalCommitStart);
        activeStatusStart.Should().BeGreaterThan(formalCommitSave,
            "the formal commitment must be visible to database guards before the contract becomes Active");

        var reservationBody = source[reservationHelperStart..];
        reservationBody.Should().Contain("_budgetControl.ReserveForDownstreamAsync(")
            .And.Contain("ApprovePermission",
                "the final approver must not also need the contract maker permission")
            .And.Contain("HasIdempotentContractExposureAsync(",
                "contract activation replay must not expand the requisition reservation")
            .And.Contain("_budgetReservationStore.GetBudgetForUpdateAsync(",
                "cumulative exposure must be calculated while holding the budget lock")
            .And.Contain("GetNetCommittedExposureAsync(",
                "activation must use the same net exposure calculation as advisory readiness")
            .And.Contain("committedExposure + contract.ContractValue")
            .And.Contain("requiredExposure,");

        var firstReplayCheck = reservationBody.IndexOf(
            "if (await HasIdempotentContractExposureAsync(",
            StringComparison.Ordinal);
        var budgetLock = reservationBody.IndexOf(
            "_budgetReservationStore.GetBudgetForUpdateAsync(",
            StringComparison.Ordinal);
        var secondReplayCheck = reservationBody.IndexOf(
            "if (await HasIdempotentContractExposureAsync(",
            firstReplayCheck + 1,
            StringComparison.Ordinal);
        var committedExposureQuery = reservationBody.IndexOf(
            "var committedExposure = await GetNetCommittedExposureAsync(",
            StringComparison.Ordinal);
        firstReplayCheck.Should().BeGreaterThanOrEqualTo(0);
        budgetLock.Should().BeGreaterThan(firstReplayCheck,
            "completed contract activation replay must exit before budget revalidation and locking");
        secondReplayCheck.Should().BeGreaterThan(budgetLock,
            "a concurrent replay must be detected again after acquiring the budget lock");
        committedExposureQuery.Should().BeGreaterThan(secondReplayCheck,
            "the cumulative exposure query must execute under the lock after replay detection");

        var netExposureHelperStart = source.IndexOf(
            "private async Task<decimal> GetNetCommittedExposureAsync(",
            StringComparison.Ordinal);
        netExposureHelperStart.Should().BeGreaterThan(reservationHelperStart);
        var netExposureBody = source[netExposureHelperStart..];
        netExposureBody.Should().Contain("ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment")
            .And.Contain("ProcurementBudgetCommitmentLedgerEntryType.Release")
            .And.Contain("item.FormalCommitmentEntryId != null")
            .And.Contain("GetDirectPurchaseOrderAdjustmentExposureAsync(")
            .And.Contain("formalCommitments - releasedFormalCommitments + directPurchaseOrderAdjustments");
    }

    [Fact]
    public void Governed_active_contract_cannot_exit_without_atomic_commitment_close()
    {
        var source = ReadContractService();
        var statusStart = source.IndexOf(
            "public async Task<ContractDto> UpdateStatusAsync(",
            StringComparison.Ordinal);
        var activateStart = source.IndexOf(
            "public async Task<ContractDto> ActivateContractAsync(",
            statusStart,
            StringComparison.Ordinal);
        var completeStart = source.IndexOf(
            "public async Task<ContractDto> CompleteContractAsync(",
            activateStart,
            StringComparison.Ordinal);
        var terminateStart = source.IndexOf(
            "public async Task<ContractDto> TerminateContractAsync(",
            completeStart,
            StringComparison.Ordinal);
        var statusBody = source[statusStart..activateStart];
        var completeBody = source[completeStart..terminateStart];
        var terminateBody = source[terminateStart..source.IndexOf("#endregion", terminateStart,
            StringComparison.Ordinal)];

        statusBody.Should().Contain("EnsureNoGovernedContractExitAsync(");
        completeBody.Should().Contain("EnsureNoGovernedContractExitAsync(");
        terminateBody.Should().Contain("EnsureNoGovernedContractExitAsync(");
        source.Should().Contain("CONTRACT_COMMITMENT_CLOSE_LIFECYCLE_REQUIRED")
            .And.Contain("ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment")
            .And.Contain("item.SourceType == \"Contract\"")
            .And.Contain("item.SourceId == contract.Id")
            .And.Contain("allocations, and utilization");
    }

    private static string ReadContractActivationService(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                continue;
            return File.ReadAllText(Path.Combine(
                directory.FullName,
                "src", "ErpSystem.Core", "Services", "Procurement",
                "ProcurementContractActivationService.cs"));
        }

        throw new DirectoryNotFoundException(
            "Repository root containing ErpSystem.sln was not found.");
    }

    private static string ReadContractService(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                continue;
            return File.ReadAllText(Path.Combine(
                directory.FullName,
                "src", "ErpSystem.Core", "Services", "Procurement",
                "ContractService.cs"));
        }

        throw new DirectoryNotFoundException(
            "Repository root containing ErpSystem.sln was not found.");
    }
}
