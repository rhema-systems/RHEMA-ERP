using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class MarketAnalysisServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IMarketAnalysisRepository> _analyses = new();
    private readonly Mock<IPriceHistoryRepository> _prices = new();
    private readonly Mock<IBusinessPartnerRepository> _partners = new();
    private readonly Mock<ICurrencyService> _currencies = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task AddSurveyQuoteUsesApprovedCentralBusinessPartner()
    {
        var analysis = DraftAnalysis();
        var supplier = ApprovedSupplier("Both");
        _analyses.Setup(value => value.GetByIdAsync(analysis.Id)).ReturnsAsync(analysis);
        _partners.Setup(value => value.GetByIdAsync(supplier.Id)).ReturnsAsync(supplier);
        _prices.Setup(value => value.AddAsync(It.IsAny<PriceHistory>()))
            .ReturnsAsync((PriceHistory value) => value);

        var result = await CreateService().AddPriceHistoryAsync(analysis.Id, new CreatePriceHistoryDto
        {
            SupplierId = supplier.Id,
            SupplierName = "untrusted browser value",
            PriceDate = DateTime.UtcNow.Date,
            UnitPrice = 125m,
            Currency = analysis.Currency,
            UnitOfMeasure = "ea",
            PriceSource = "MarketSurvey"
        });

        result.SupplierId.Should().Be(supplier.Id);
        result.SupplierName.Should().Be(supplier.PartnerName);
        result.Currency.Should().Be(analysis.Currency);
        _prices.Verify(value => value.AddAsync(It.Is<PriceHistory>(quote =>
            quote.SupplierId == supplier.Id &&
            quote.SupplierName == supplier.PartnerName &&
            quote.TenantId == _tenantId &&
            quote.UnitOfMeasure == "EA")), Times.Once);
        _unitOfWork.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddSurveyQuoteRejectsSupplierFromAnotherTenantBeforeSaving()
    {
        var analysis = DraftAnalysis();
        var supplier = ApprovedSupplier("Supplier");
        supplier.TenantId = Guid.NewGuid();
        _analyses.Setup(value => value.GetByIdAsync(analysis.Id)).ReturnsAsync(analysis);
        _partners.Setup(value => value.GetByIdAsync(supplier.Id)).ReturnsAsync(supplier);

        var action = () => CreateService().AddPriceHistoryAsync(analysis.Id, new CreatePriceHistoryDto
        {
            SupplierId = supplier.Id,
            PriceDate = DateTime.UtcNow.Date,
            UnitPrice = 125m,
            Currency = analysis.Currency,
            UnitOfMeasure = "EA"
        });

        var error = await action.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Code.Should().Be("MARKET_SURVEY_SUPPLIER_NOT_FOUND");
        _prices.Verify(value => value.AddAsync(It.IsAny<PriceHistory>()), Times.Never);
        _unitOfWork.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishMakesReadyDraftAvailableWithoutApprovalWorkflow()
    {
        var analysis = DraftAnalysis();
        _analyses.Setup(value => value.GetByIdAsync(analysis.Id)).ReturnsAsync(analysis);
        _analyses.Setup(value => value.GetWithPriceHistoriesAsync(analysis.Id)).ReturnsAsync(analysis);
        _analyses.Setup(value => value.UpdateAsync(analysis)).Returns(Task.CompletedTask);
        _currencies.Setup(value => value.GetByCodeAsync(analysis.Currency, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveCurrency(analysis.Currency));

        var result = await CreateService().PublishAsync(analysis.Id);

        result.Status.Should().Be("Published");
        analysis.Status.Should().Be("Published");
        _analyses.Verify(value => value.UpdateAsync(analysis), Times.Once);
        _unitOfWork.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishIsIdempotentAfterFirstSuccess()
    {
        var analysis = DraftAnalysis();
        analysis.Status = "Published";
        _analyses.Setup(value => value.GetByIdAsync(analysis.Id)).ReturnsAsync(analysis);
        _analyses.Setup(value => value.GetWithPriceHistoriesAsync(analysis.Id)).ReturnsAsync(analysis);

        var result = await CreateService().PublishAsync(analysis.Id);

        result.Status.Should().Be("Published");
        _analyses.Verify(value => value.UpdateAsync(It.IsAny<MarketAnalysis>()), Times.Never);
        _unitOfWork.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private MarketAnalysisService CreateService()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        currentUser.SetupGet(value => value.UserId).Returns(_userId);
        currentUser.SetupGet(value => value.Username).Returns("procurement.officer");
        _unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return new MarketAnalysisService(
            _analyses.Object,
            _prices.Object,
            _partners.Object,
            _currencies.Object,
            _unitOfWork.Object,
            currentUser.Object,
            NullLogger<MarketAnalysisService>.Instance);
    }

    private MarketAnalysis DraftAnalysis() => new()
    {
        TenantId = _tenantId,
        AnalysisCode = "MA-2026-0001",
        Title = "Cement market analysis",
        ItemCategory = "Construction Materials",
        ItemDescription = "Cement",
        AnalysisPeriodStart = DateTime.UtcNow.Date,
        AnalysisPeriodEnd = DateTime.UtcNow.Date.AddMonths(1),
        CurrentMarketPrice = 120m,
        Currency = "GHS",
        Status = "Draft"
    };

    private BusinessPartner ApprovedSupplier(string partnerType) => new()
    {
        TenantId = _tenantId,
        PartnerCode = "SUP-001",
        PartnerName = "Approved Supplier Ltd",
        PartnerType = partnerType,
        RegistrationStatus = "Approved",
        ApprovalStatus = "Approved",
        IsActive = true,
        IsBlacklisted = false
    };

    private CurrencyDto ActiveCurrency(string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        CurrencyCode = code,
        CurrencyName = "Ghana Cedi",
        IsActive = true
    };
}
