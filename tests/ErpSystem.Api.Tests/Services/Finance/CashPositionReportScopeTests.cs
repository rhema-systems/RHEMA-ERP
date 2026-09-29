using ErpSystem.Api.Services.Finance.Cash;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CashPositionReportScopeTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-CashScope")]
    [Trait("Category", "CashBank")]
    public async Task GetCurrentPositionAsync_ShouldRequestLedgerForVisibleAccountsOnly()
    {
        var bankId = Guid.NewGuid();
        var bankService = new Mock<IBankAccountService>();
        bankService.Setup(service => service.GetActiveAccountsAsync())
            .ReturnsAsync(new[]
            {
                new BankAccountDto
                {
                    Id = bankId,
                    AccountName = "Scoped Bank",
                    AccountType = BankAccountType.Checking,
                    Currency = "GHS"
                }
            });

        var ledgerService = new Mock<IGeneralLedgerService>();
        ledgerService.Setup(service => service.GenerateCashBankLedgerAsync(
                It.Is<CashBankLedgerRequestDto>(request =>
                    request.BankAccountIds != null &&
                    request.BankAccountIds.Count == 1 &&
                    request.BankAccountIds.Contains(bankId))))
            .ReturnsAsync(new CashBankLedgerReportDto
            {
                TotalClosingBalance = 125m,
                Accounts = new List<CashBankLedgerAccountDto>
                {
                    new() { BankAccountId = bankId, ClosingBalance = 125m }
                }
            });

        var settings = new Mock<ITenantSettingsService>();
        settings.Setup(service => service.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var service = new CashPositionReportService(bankService.Object, ledgerService.Object, settings.Object);

        var result = await service.GetCurrentPositionAsync();

        result.TotalBalance.Should().Be(125m);
        result.AccountCount.Should().Be(1);
        ledgerService.VerifyAll();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CashScope")]
    [Trait("Category", "CashBank")]
    public async Task GetCurrentPositionAsync_ShouldReturnZeroWithoutCallingLedger_WhenNoAccountsAreVisible()
    {
        var bankService = new Mock<IBankAccountService>();
        bankService.Setup(service => service.GetActiveAccountsAsync())
            .ReturnsAsync(Array.Empty<BankAccountDto>());
        var ledgerService = new Mock<IGeneralLedgerService>();
        var settings = new Mock<ITenantSettingsService>();
        settings.Setup(service => service.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var service = new CashPositionReportService(bankService.Object, ledgerService.Object, settings.Object);

        var result = await service.GetCurrentPositionAsync();

        result.TotalBalance.Should().Be(0m);
        result.AccountCount.Should().Be(0);
        ledgerService.Verify(
            service => service.GenerateCashBankLedgerAsync(It.IsAny<CashBankLedgerRequestDto>()),
            Times.Never);
    }
}
