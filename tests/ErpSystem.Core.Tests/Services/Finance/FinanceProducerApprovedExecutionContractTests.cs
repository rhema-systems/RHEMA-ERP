using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Finance;

public sealed class FinanceProducerApprovedExecutionContractTests
{
    [Fact]
    public void Contract_IsPublicBookBlindDataOnlyAndCoreConsumable()
    {
        typeof(IFinanceProducerApprovedExecutionService).IsPublic.Should().BeTrue();

        var execute = typeof(IFinanceProducerApprovedExecutionService).GetMethods()
            .Single(method => method.Name == nameof(IFinanceProducerApprovedExecutionService.ExecuteInAmbientTransactionAsync)
                && method.GetParameters().Length == 4);
        execute.ReturnType.Should().Be(typeof(Task<FinanceProducerApprovedExecutionResultDto>));
        execute.GetParameters().Select(parameter => parameter.ParameterType).Should().ContainInOrder(
            typeof(Guid),
            typeof(ProducerAccountingIntentDto),
            typeof(ProducerOwnerEffectReceiptDto),
            typeof(CancellationToken));
        execute.GetParameters().Should().NotContain(parameter =>
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));

        var idOnly = typeof(IFinanceProducerApprovedExecutionService).GetMethods()
            .Single(method => method.Name == nameof(IFinanceProducerApprovedExecutionService.ExecuteInAmbientTransactionAsync)
                && method.GetParameters().Length == 3);
        idOnly.GetParameters().Select(parameter => parameter.ParameterType).Should().ContainInOrder(
            typeof(Guid), typeof(ProducerOwnerEffectReceiptDto), typeof(CancellationToken));
        idOnly.GetParameters().Select(parameter => parameter.ParameterType).Should().NotContain([
            typeof(ProducerAccountingIntentDto), typeof(ProducerFinancePostingRequestDto)
        ]);
        idOnly.GetParameters().Select(parameter => parameter.Name).Should().NotContain(name =>
            name!.Contains("Book", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Line", StringComparison.OrdinalIgnoreCase));

        var resultProperties = typeof(FinanceProducerApprovedExecutionResultDto)
            .GetProperties().Select(property => property.Name).ToArray();
        resultProperties.Should().Equal(
            nameof(FinanceProducerApprovedExecutionResultDto.AccountingEventId),
            nameof(FinanceProducerApprovedExecutionResultDto.AccountingEventRequestFingerprint),
            nameof(FinanceProducerApprovedExecutionResultDto.Status),
            nameof(FinanceProducerApprovedExecutionResultDto.FinancePostingEventId),
            nameof(FinanceProducerApprovedExecutionResultDto.JournalEntryId));
        resultProperties.Should().NotContain(name =>
            name.Contains("Book", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReversalPreparationContract_IsBookAndLineBlind()
    {
        typeof(IFinanceProducerReversalPreparationService).IsPublic.Should().BeTrue();
        var method = typeof(IFinanceProducerReversalPreparationService)
            .GetMethod(nameof(IFinanceProducerReversalPreparationService.PrepareReversalAsync))!;
        method.GetParameters().Select(parameter => parameter.ParameterType).Should().ContainInOrder(
            typeof(PrepareProducerAccountingReversalDto), typeof(CancellationToken));

        var requestProperties = typeof(PrepareProducerAccountingReversalDto).GetProperties()
            .Select(property => property.Name).ToArray();
        requestProperties.Should().NotContain(name =>
            name.Contains("Book", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Line", StringComparison.OrdinalIgnoreCase));
        typeof(ProducerAccountingReversalPreparationResultDto).GetProperties()
            .Select(property => property.Name).Should().NotContain(name =>
                name.Contains("Book", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Line", StringComparison.OrdinalIgnoreCase));
    }
}
