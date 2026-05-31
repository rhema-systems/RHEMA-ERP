using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    public class ExchangeRateServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ITenantSettingsService> _mockTenantSettingsService;
        private readonly Mock<ILogger<ExchangeRateService>> _mockLogger;
        private readonly Mock<IGenericRepository<ExchangeRate>> _mockExchangeRateRepository;
        private readonly Mock<IGenericRepository<AccountTransaction>> _mockAccountTransactionRepository;
        private readonly Mock<IUserService> _mockUserService;
        private readonly ExchangeRateService _service;
        
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _userName = "test-user";
        private readonly Guid _userId = Guid.NewGuid();

        public ExchangeRateServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockTenantSettingsService = new Mock<ITenantSettingsService>();
            _mockLogger = new Mock<ILogger<ExchangeRateService>>();
            _mockExchangeRateRepository = new Mock<IGenericRepository<ExchangeRate>>();
            _mockAccountTransactionRepository = new Mock<IGenericRepository<AccountTransaction>>();
            _mockUserService = new Mock<IUserService>();

            _mockCurrentUserService.Setup(s => s.TenantId).Returns(_tenantId);
            _mockCurrentUserService.Setup(s => s.UserName).Returns(_userName);
            _mockCurrentUserService.Setup(s => s.UserId).Returns(_userId.ToString());

            _mockUnitOfWork.Setup(u => u.Repository<ExchangeRate>()).Returns(_mockExchangeRateRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<AccountTransaction>()).Returns(_mockAccountTransactionRepository.Object);

            _service = new ExchangeRateService(
                _mockUnitOfWork.Object,
                _mockCurrentUserService.Object,
                _mockTenantSettingsService.Object,
                _mockLogger.Object,
                _mockUserService.Object);
                
            // Setup empty account transactions by default to avoid null ref in MapToDto usage count logic
            _mockAccountTransactionRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<AccountTransaction, bool>>>()))
                .Returns(new List<AccountTransaction>().AsQueryable().BuildMock());
        }

        private void SetupExchangeRateQueryable(List<ExchangeRate> rates)
        {
            _mockExchangeRateRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<ExchangeRate, bool>>>()))
                .Returns((Expression<Func<ExchangeRate, bool>> predicate) =>
                    rates.AsQueryable().Where(predicate).BuildMock());

            _mockExchangeRateRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<ExchangeRate, bool>>>()))
                .ReturnsAsync((Expression<Func<ExchangeRate, bool>> predicate) =>
                    rates.AsQueryable().FirstOrDefault(predicate));
        }

        [Fact]
        public async Task ExchangeRateService_ShouldMapCreatedByUserName_WhenUserExists()
        {
            var rateId = Guid.NewGuid();
            var creatorId = Guid.NewGuid();
            var rate = new ExchangeRate
            {
                Id = rateId,
                TenantId = _tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                CreatedByUserId = creatorId
            };

            SetupExchangeRateQueryable(new List<ExchangeRate> { rate });

            _mockUserService.Setup(s => s.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<ErpSystem.Core.Entities.ApplicationUser> 
                { 
                    new ErpSystem.Core.Entities.ApplicationUser { Id = creatorId, UserName = "fx-admin" } 
                });

            var result = await _service.GetExchangeRateByIdAsync(rateId);

            result.Should().NotBeNull();
            result.CreatedByUserName.Should().Be("fx-admin");
        }

        [Fact]
        public async Task ExchangeRateService_ShouldMapApprovedByUserName_WhenUserExists()
        {
            var rateId = Guid.NewGuid();
            var approverId = Guid.NewGuid();
            var rate = new ExchangeRate
            {
                Id = rateId,
                TenantId = _tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                CreatedByUserId = Guid.NewGuid(),
                ApprovedByUserId = approverId
            };

            SetupExchangeRateQueryable(new List<ExchangeRate> { rate });

            _mockUserService.Setup(s => s.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<ErpSystem.Core.Entities.ApplicationUser> 
                { 
                    new ErpSystem.Core.Entities.ApplicationUser { Id = approverId, UserName = "finance-manager" } 
                });

            var result = await _service.GetExchangeRateByIdAsync(rateId);

            result.Should().NotBeNull();
            result.ApprovedByUserName.Should().Be("finance-manager");
        }

        [Fact]
        public async Task ExchangeRateService_ShouldPreserveRawUserIds()
        {
            var rateId = Guid.NewGuid();
            var creatorId = Guid.NewGuid();
            var approverId = Guid.NewGuid();
            var rate = new ExchangeRate
            {
                Id = rateId,
                TenantId = _tenantId,
                BaseCurrencyCode = "GHS",
                TargetCurrencyCode = "USD",
                CreatedByUserId = creatorId,
                ApprovedByUserId = approverId
            };

            SetupExchangeRateQueryable(new List<ExchangeRate> { rate });

            _mockUserService.Setup(s => s.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<ErpSystem.Core.Entities.ApplicationUser>());

            var result = await _service.GetExchangeRateByIdAsync(rateId);

            result.Should().NotBeNull();
            result.CreatedBy.Should().Be(creatorId.ToString()); // The original mapped ID
            result.CreatedByUserName.Should().Be("Unknown user");
            result.ApprovedByUserName.Should().Be("Unknown user");
        }

        [Fact]
        public async Task ExchangeRateTrends_ShouldReturnRatesWithinDateRange()
        {
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 10);
            
            var rates = new List<ExchangeRate>
            {
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2023, 12, 31), Rate = 10m },
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 1), Rate = 11m },
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 5), Rate = 12m },
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 11), Rate = 13m }
            };

            SetupExchangeRateQueryable(rates);

            var result = await _service.GetTrendsAsync("GHS", "USD", startDate, endDate, "daily", 7);

            result.Should().NotBeNull();
            result.Should().HaveCount(2); // Only dates 1st and 5th
            result.First().Rate.Should().Be(11m);
            result.Last().Rate.Should().Be(12m);
        }

        [Fact]
        public async Task ExchangeRateTrends_ShouldCalculatePercentageChange()
        {
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 2);
            
            var rates = new List<ExchangeRate>
            {
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 1), Rate = 10m },
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 2), Rate = 12m }
            };

            SetupExchangeRateQueryable(rates);

            var result = await _service.GetTrendsAsync("GHS", "USD", startDate, endDate, "daily", 7);

            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.First().ChangePercentage.Should().BeNull(); // No previous rate
            result.Last().ChangePercentage.Should().Be(20m); // (12 - 10) / 10 = 20%
        }

        [Fact]
        public async Task ExchangeRateTrends_ShouldCalculateMovingAverage()
        {
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 3);
            
            var rates = new List<ExchangeRate>
            {
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 1), Rate = 10m },
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 2), Rate = 20m },
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 3), Rate = 30m }
            };

            SetupExchangeRateQueryable(rates);

            var result = await _service.GetTrendsAsync("GHS", "USD", startDate, endDate, "daily", 2);

            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.First().MovingAverage.Should().BeNull(); // Window is 2, index 0 is not enough
            result[1].MovingAverage.Should().Be(15m); // Average of 10 and 20
            result[2].MovingAverage.Should().Be(25m); // Average of 20 and 30
        }

        [Fact]
        public async Task ExchangeRateTrends_ShouldRespectCurrencyPair()
        {
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 5);
            
            var rates = new List<ExchangeRate>
            {
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "USD", EffectiveDate = new DateTime(2024, 1, 1), Rate = 10m },
                new ExchangeRate { TenantId = _tenantId, BaseCurrencyCode = "GHS", TargetCurrencyCode = "EUR", EffectiveDate = new DateTime(2024, 1, 2), Rate = 15m }
            };

            SetupExchangeRateQueryable(rates);

            var result = await _service.GetTrendsAsync("GHS", "USD", startDate, endDate, "daily", 7);

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().TargetCurrency.Should().Be("USD");
        }
    }
}
