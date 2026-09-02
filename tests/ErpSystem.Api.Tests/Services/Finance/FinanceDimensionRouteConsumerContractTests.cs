using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceDimensionRouteConsumerContractTests
{
    [Fact]
    public void FinanceOwnedRoutesRemainCaptureOptionalAndUseSourceLineGrain()
    {
        var routes = new[]
        {
            FinanceDimensionRouteId.FinanceApVendorInvoice,
            FinanceDimensionRouteId.FinanceApSupplierDebitNote,
            FinanceDimensionRouteId.FinanceArCustomerInvoice
        }.Select(FinanceDimensionRouteCatalog.GetRequired).ToArray();

        routes.Should().OnlyContain(route =>
            route.ProducerModule == "Finance"
            && route.Grain == FinanceDimensionGrain.SourceDocumentLine
            && route.DefaultState == FinanceDimensionCertificationState.CaptureOptional
            && route.SupportsDocumentDefaults
            && route.RequiresReadinessProvider);
        routes.Select(route => route.DocumentType).Should().BeEquivalentTo(
            "VendorInvoice", "SupplierDebitNote", "CustomerInvoice");
    }

    [Fact]
    public void LegacyProducerOverloadsRemainAlongsideTrustedManualFinanceOverloads()
    {
        typeof(IVendorInvoiceService).GetMethods().Count(method => method.Name == "CreateAsync")
            .Should().BeGreaterThanOrEqualTo(2);
        typeof(IInvoiceService).GetMethods().Count(method => method.Name == "CreateAsync")
            .Should().BeGreaterThanOrEqualTo(2);
        typeof(ISupplierDebitNoteService).GetMethods().Count(method => method.Name == "CreateAsync")
            .Should().BeGreaterThanOrEqualTo(2);

        typeof(IInvoiceService).GetMethods().Should().Contain(method =>
            method.Name == "SendInvoiceAsync"
            && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(FinancePostingProducerContext)));
        typeof(IVendorInvoiceService).GetMethods().Should().Contain(method =>
            method.Name == "PostAsync"
            && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(FinancePostingProducerContext)));
    }

    [Fact]
    public void ThreeFinanceDtosShareTheSameAdditiveDimensionContract()
    {
        typeof(VendorInvoiceCreateDto).GetProperty("FinanceDimensions")!.PropertyType
            .Should().Be(typeof(FinanceSourceDocumentDimensionInputDto));
        typeof(CreateSupplierDebitNoteDto).GetProperty("FinanceDimensions")!.PropertyType
            .Should().Be(typeof(FinanceSourceDocumentDimensionInputDto));
        typeof(InvoiceCreateDto).GetProperty("FinanceDimensions")!.PropertyType
            .Should().Be(typeof(FinanceSourceDocumentDimensionInputDto));
    }

    [Fact]
    public void SalesCreditNotesCanAdoptTheGenericAdapterWithoutFinanceOwningTheirLifecycle()
    {
        var route = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.SalesCreditNote);

        route.ProducerModule.Should().Be("Sales");
        route.DocumentType.Should().Be("SalesCreditNote");
        route.Grain.Should().Be(FinanceDimensionGrain.SourceDocumentLine);
        route.DefaultState.Should().Be(FinanceDimensionCertificationState.CaptureOptional);
        route.SupportsDocumentDefaults.Should().BeFalse();
        route.Notes.Should().Contain("contract only");

        typeof(IFinanceSourceDimensionService).GetMethods().Should().Contain(method =>
            method.Name == nameof(IFinanceSourceDimensionService.SynchronizeDraftAsync)
            && method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(FinancePostingProducerContext))
            && method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(FinanceSourceDocumentDimensionInputDto)));
    }

    [Fact]
    public void FinanceBankingSettlementRoutesUseTheGenericAllocationContract()
    {
        var routes = new[]
        {
            FinanceDimensionRouteId.FinanceBankDeposit,
            FinanceDimensionRouteId.FinanceReturnedCheque
        }.Select(FinanceDimensionRouteCatalog.GetRequired).ToArray();

        routes.Should().OnlyContain(route =>
            route.ProducerModule == "Finance"
            && route.PostingSourceModule == "CASHBANK"
            && route.Grain == FinanceDimensionGrain.SettlementAllocationLine
            && route.DefaultState == FinanceDimensionCertificationState.CaptureOptional
            && route.SupportsDocumentDefaults
            && route.RequiresReadinessProvider);
        typeof(CreateBankDepositDto).GetProperty(nameof(CreateBankDepositDto.FinanceDimensions))!
            .PropertyType.Should().Be(typeof(FinanceSourceDocumentDimensionInputDto));
        typeof(CreateReturnedChequeCaseDto).GetProperty(nameof(CreateReturnedChequeCaseDto.FinanceDimensions))!
            .PropertyType.Should().Be(typeof(FinanceSourceDocumentDimensionInputDto));

        var returnedChequeId = Guid.NewGuid();
        FinanceBankingDimensionIdentity.ReturnedChequeBankLine(returnedChequeId)
            .Should().Be(FinanceBankingDimensionIdentity.ReturnedChequeBankLine(returnedChequeId));
        FinanceBankingDimensionIdentity.ReturnedChequeBankLine(returnedChequeId)
            .Should().NotBe(FinanceBankingDimensionIdentity.ReturnedChequeCustomerLine(returnedChequeId));
        FinanceBankingDimensionIdentity.ReturnedChequeExpenseLine(returnedChequeId)
            .Should().NotBe(FinanceBankingDimensionIdentity.ReturnedChequeCustomerLine(returnedChequeId));
    }

    [Fact]
    public void ReconciliationAdjustmentsUseACompiledFinanceOnlyRouteAndTrustedCashAdapter()
    {
        var route = FinanceDimensionRouteCatalog.GetRequired(
            FinanceDimensionRouteId.FinanceBankReconciliationAdjustment);

        route.ProducerModule.Should().Be("Finance");
        route.PostingSourceModule.Should().Be("CASHBANK");
        route.SourceRoute.Should().Be("finance.cash.bank-reconciliation-adjustments");
        route.DocumentType.Should().Be("BankReconciliationAdjustment");
        route.Grain.Should().Be(FinanceDimensionGrain.SourceDocumentLine);
        route.DefaultState.Should().Be(FinanceDimensionCertificationState.CaptureOptional);
        route.RequiresReadinessProvider.Should().BeTrue();

        typeof(ICashTransactionService).GetMethods().Should().Contain(method =>
            method.Name == nameof(ICashTransactionService.CreatePaymentForProducerAsync)
            && method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(FinancePostingProducerContext)));
        typeof(CreateReconciliationAdjustmentDto).GetProperty("FinanceDimensions")!.PropertyType
            .Should().Be(typeof(FinanceSourceDocumentDimensionInputDto));

        var transactionId = Guid.NewGuid();
        FinanceReconciliationDimensionIdentity.BankLine(transactionId)
            .Should().Be(FinanceReconciliationDimensionIdentity.BankLine(transactionId));
        FinanceReconciliationDimensionIdentity.OffsetLine(transactionId)
            .Should().NotBe(FinanceReconciliationDimensionIdentity.BankLine(transactionId));
    }

    [Fact]
    public void FixedAssetRoutesAreDistinctStableFinanceContracts()
    {
        var routeIds = Enumerable.Range(46, 15)
            .Select(value => (FinanceDimensionRouteId)value)
            .ToArray();
        var routes = routeIds.Select(FinanceDimensionRouteCatalog.GetRequired).ToArray();

        routes.Select(item => (int)item.Id).Should().Equal(Enumerable.Range(46, 15));
        routes.Should().OnlyContain(item =>
            item.ProducerModule == "Finance"
            && item.Owner == "Finance / Fixed Assets"
            && item.Grain == FinanceDimensionGrain.SourceDocumentLine
            && item.DefaultState == FinanceDimensionCertificationState.CaptureOptional
            && item.RequiresReadinessProvider);
        routes.Select(item => item.SourceRoute).Should().OnlyHaveUniqueItems();
        routes.Select(item => item.DocumentType).Should().OnlyContain(item =>
            !string.IsNullOrWhiteSpace(item));

        var documentId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        FinanceSourceLineIdentity.Create(documentId, "asset-cost", lineageId)
            .Should().Be(FinanceSourceLineIdentity.Create(documentId, "ASSET-COST", lineageId));
        FinanceSourceLineIdentity.Create(documentId, "asset-cost", lineageId)
            .Should().NotBe(FinanceSourceLineIdentity.Create(documentId, "clearing", lineageId));

        typeof(IPaymentService).GetMethods().Should().Contain(method =>
            method.Name == nameof(IPaymentService.CreateAsync)
            && method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(FinancePostingProducerContext)));
        typeof(IInvoiceService).GetMethods().Should().Contain(method =>
            method.Name == nameof(IInvoiceService.SendInvoiceAsync)
            && method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(FinancePostingProducerContext)));
        typeof(IFxAccountingService).GetMethods().Should().Contain(method =>
            method.Name == nameof(IFxAccountingService.PostRealizedFxForArReceiptAsync)
            && method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(FinancePostingProducerContext)));
    }
}
