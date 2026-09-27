using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSettingsControlTests
{
    [Fact]
    public async Task OlderClientOmittingFlagsPreservesConfiguredControls()
    {
        var fixture = new Fixture();
        fixture.Settings.EnforceSegregationOfDuties = false;
        fixture.Settings.AutoCloseTenders = true;
        var result = await fixture.Service.UpdateSettingsAsync(new UpdateProcurementSettingsDto { AllowNonInventoryItems = true });
        result.EnforceSegregationOfDuties.Should().BeFalse();
        result.AutoCloseTenders.Should().BeTrue();
        fixture.Audits.Should().BeEmpty();
    }

    [Fact]
    public async Task ControlChangeCapturesOldAndNewValuesWithCurrentActorInSameSave()
    {
        var fixture = new Fixture();
        var result = await fixture.Service.UpdateSettingsAsync(new UpdateProcurementSettingsDto { AllowNonInventoryItems = true, EnforceSegregationOfDuties = false, AutoCloseTenders = true });
        result.EnforceSegregationOfDuties.Should().BeFalse();
        result.AutoCloseTenders.Should().BeTrue();
        var audit = fixture.Audits.Should().ContainSingle().Subject;
        audit.TenantId.Should().Be(fixture.Settings.TenantId);
        audit.UserId.Should().Be(fixture.Actor);
        audit.Action.Should().Be("PROCUREMENT_CONTROLS_CHANGED");
        JsonDocument.Parse(audit.OldValues!).RootElement.GetProperty("EnforceSegregationOfDuties").GetBoolean().Should().BeTrue();
        JsonDocument.Parse(audit.NewValues!).RootElement.GetProperty("EnforceSegregationOfDuties").GetBoolean().Should().BeFalse();
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture
    {
        public Guid Actor { get; } = Guid.NewGuid();
        public ProcurementSettings Settings { get; } = new() { Id = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        public List<AuditLog> Audits { get; } = [];
        public Mock<IUnitOfWork> Unit { get; } = new();
        public ProcurementSettingsService Service { get; }
        public Fixture()
        {
            var repository = new Mock<IProcurementSettingsRepository>();
            repository.Setup(value => value.GetOrCreateDefaultAsync(Settings.TenantId, Actor)).ReturnsAsync(Settings);
            var user = new Mock<ICurrentUserProvider>();
            user.SetupGet(value => value.TenantId).Returns(Settings.TenantId);
            user.SetupGet(value => value.UserId).Returns(Actor);
            user.SetupGet(value => value.Username).Returns("controls-admin");
            user.SetupGet(value => value.FullName).Returns("Controls Administrator");
            var audits = new Mock<IGenericRepository<AuditLog>>();
            audits.Setup(value => value.AddAsync(It.IsAny<AuditLog>())).Callback<AuditLog>(Audits.Add).ReturnsAsync((AuditLog value) => value);
            Unit.Setup(value => value.Repository<AuditLog>()).Returns(audits.Object);
            Service = new ProcurementSettingsService(repository.Object, user.Object, Unit.Object, NullLogger<ProcurementSettingsService>.Instance);
        }
    }
}
