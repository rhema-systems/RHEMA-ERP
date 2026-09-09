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

        var execute = typeof(IFinanceProducerApprovedExecutionService)
            .GetMethod(nameof(IFinanceProducerApprovedExecutionService.ExecuteInAmbientTransactionAsync))!;
        execute.ReturnType.Should().Be(typeof(Task<FinanceProducerApprovedExecutionResultDto>));
        execute.GetParameters().Select(parameter => parameter.ParameterType).Should().ContainInOrder(
            typeof(Guid),
            typeof(ProducerAccountingIntentDto),
            typeof(ProducerOwnerEffectReceiptDto),
            typeof(CancellationToken));
        execute.GetParameters().Should().NotContain(parameter =>
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType));

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
}
