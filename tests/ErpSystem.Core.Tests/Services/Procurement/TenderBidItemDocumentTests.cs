using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderBidItemDocumentTests
{
    [Fact]
    public void ItemDocumentRelationshipIsNullableAndBoundToTheSameTenantAndBid()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=NeverConnected;Integrated Security=True").Options);
        var document = context.Model.FindEntityType(typeof(TenderBidDocument))!;
        document.FindProperty(nameof(TenderBidDocument.TenderBidItemId))!.IsNullable.Should().BeTrue();
        var relationship = document.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(TenderBidItem));
        relationship.Properties.Select(property => property.Name).Should().Equal("TenantId", "TenderBidId", "TenderBidItemId");
        relationship.PrincipalKey.Properties.Select(property => property.Name).Should().Equal("TenantId", "TenderBidId", "Id");
        relationship.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var initialized = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            initialized.GetRelationalModel(), context.GetService<IDesignTimeModel>().Model.GetRelationalModel());
        var details = string.Join("; ", differences.Select(operation => $"{operation.GetType().Name}: " +
            $"{operation.GetType().GetProperty("Table")?.GetValue(operation)} / {operation.GetType().GetProperty("Name")?.GetValue(operation)}"));
        differences.Should().BeEmpty("the additive migrations and model snapshot must agree: {0}", details);
    }

    [Fact]
    public async Task ItemUploadPreservesDmsAndLotLineageAndCannotMasqueradeAsRequiredProposal()
    {
        using var f = new Fixture();
        var result = await f.Upload(f.Item.TenderItemId);
        result.TenderBidItemId.Should().Be(f.Item.Id);
        result.TenderItemId.Should().Be(f.Item.TenderItemId);
        result.LotId.Should().Be(f.Item.TenderItem.LotId);
        result.ItemDescription.Should().Be("Laptop");
        result.DocumentType.Should().Be("TechnicalItemSupportingDocument");
        var saved = f.Context.Set<TenderBidDocument>().Single();
        saved.CentralDocumentRecordId.Should().Be(f.DocumentId);
        saved.CentralDocumentVersionId.Should().Be(f.VersionId);
        saved.FileUploadRecordId.Should().Be(f.UploadId);
    }

    [Fact]
    public async Task MultipleFilesPerItemAndExistingBidLevelDocumentsRemainSupported()
    {
        using var f = new Fixture();
        await f.Upload(f.Item.TenderItemId);
        await f.Upload(f.Item.TenderItemId);
        var general = await f.Upload(null);
        general.TenderBidItemId.Should().BeNull();
        general.DocumentType.Should().Be("TechnicalProposal");
        f.Context.Set<TenderBidDocument>().Count().Should().Be(3);
    }

    [Theory]
    [InlineData("Submitted", "Published", false, "BID_DOCUMENT_LOCKED")]
    [InlineData("Draft", "Closed", false, "BID_DOCUMENT_CUTOFF")]
    [InlineData("Draft", "Published", true, "BID_DOCUMENT_CUTOFF")]
    public async Task LockedBidsAndCutoffPreventBothUploadAndDelete(string bidStatus, string tenderStatus, bool expired, string code)
    {
        using var f = new Fixture();
        var document = await f.Upload(f.Item.TenderItemId);
        f.Bid.Status = bidStatus;
        f.Tender.Status = tenderStatus;
        if (expired) f.Tender.SubmissionDeadline = DateTime.UtcNow.AddMinutes(-1);
        await f.Context.SaveChangesAsync();
        Func<Task> upload = () => f.Upload(f.Item.TenderItemId);
        Func<Task> delete = () => f.Service.DeleteBidDocumentAsync(document.Id, f.Bid.Id);
        (await upload.Should().ThrowAsync<TenderBidInitiationValidationException>()).Which.Code.Should().Be(code);
        (await delete.Should().ThrowAsync<TenderBidInitiationValidationException>()).Which.Code.Should().Be(code);
        f.Documents.Verify(repository => repository.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        f.Context.Set<TenderBidDocument>().Count().Should().Be(1);
    }

    [Fact]
    public async Task AnotherSupplierCannotMutateTheBidDocuments()
    {
        using var f = new Fixture();
        var document = await f.Upload(f.Item.TenderItemId);
        f.Partners.Setup(repository => repository.GetByUserIdAsync(f.UserId)).ReturnsAsync(new BusinessPartner
            { Id = Guid.NewGuid(), TenantId = f.TenantId });
        Func<Task> upload = () => f.Upload(f.Item.TenderItemId);
        Func<Task> delete = () => f.Service.DeleteBidDocumentAsync(document.Id, f.Bid.Id);
        await upload.Should().ThrowAsync<UnauthorizedAccessException>();
        await delete.Should().ThrowAsync<UnauthorizedAccessException>();
        f.Documents.Verify(repository => repository.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData("other-bid")]
    [InlineData("other-tenant")]
    [InlineData("deleted")]
    [InlineData("cancelled-lot")]
    public async Task InvalidItemLineageCannotReceiveDocuments(string invalidity)
    {
        using var f = new Fixture(invalidity);
        Func<Task> action = () => f.Upload(f.Item.TenderItemId);
        (await action.Should().ThrowAsync<TenderBidInitiationValidationException>()).Which.Code.Should().Be("BID_DOCUMENT_ITEM_INVALID");
        f.Context.Set<TenderBidDocument>().Should().BeEmpty();
    }

    [Fact]
    public async Task DeletionBindsDocumentToRouteBidAndPermitsOwnedDraftDeletion()
    {
        using var f = new Fixture();
        var document = await f.Upload(f.Item.TenderItemId);
        Func<Task> wrongRoute = () => f.Service.DeleteBidDocumentAsync(document.Id, Guid.NewGuid());
        (await wrongRoute.Should().ThrowAsync<TenderBidInitiationValidationException>()).Which.Code.Should().Be("BID_DOCUMENT_NOT_FOUND");
        f.Documents.Verify(repository => repository.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        await f.Service.DeleteBidDocumentAsync(document.Id, f.Bid.Id);
        f.Documents.Verify(repository => repository.DeleteAsync(document.Id), Times.Once);
    }

    [Fact]
    public async Task UnauthenticatedAndUnprivilegedInternalUsersCannotUpload()
    {
        using var f = new Fixture();
        f.User.SetupGet(user => user.IsAuthenticated).Returns(false);
        Func<Task> action = () => f.Upload(null);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        f.User.SetupGet(user => user.IsAuthenticated).Returns(true);
        f.User.SetupGet(user => user.IsExternalUser).Returns(false);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Guid TenantId = Guid.NewGuid(), UserId = Guid.NewGuid();
        public readonly Guid DocumentId = Guid.NewGuid(), VersionId = Guid.NewGuid(), UploadId = Guid.NewGuid();
        public readonly ApplicationDbContext Context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Mock<ICurrentUserProvider> User = new();
        public readonly Mock<IUnitOfWork> Unit = new();
        public readonly Mock<IBusinessPartnerRepository> Partners = new();
        public readonly Mock<ITenderBidDocumentRepository> Documents = new();
        public readonly Tender Tender;
        public readonly TenderBid Bid;
        public readonly TenderBidItem Item;
        public readonly TenderBidService Service;

        public Fixture(string? invalidity = null)
        {
            User.SetupGet(user => user.TenantId).Returns(TenantId);
            User.SetupGet(user => user.UserId).Returns(UserId);
            User.SetupGet(user => user.IsAuthenticated).Returns(true);
            User.SetupGet(user => user.IsExternalUser).Returns(true);
            var partner = new BusinessPartner { Id = Guid.NewGuid(), TenantId = TenantId, PartnerName = "Supplier", PartnerCode = "SUP-1", PartnerType = "Supplier" };
            Partners.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync(partner);
            Tender = new Tender { Id = Guid.NewGuid(), TenantId = TenantId, TenderNumber = "TND-1", Title = "Supply", Status = "Published", SubmissionDeadline = DateTime.UtcNow.AddDays(1) };
            var lot = new TenderLot { Id = Guid.NewGuid(), TenantId = TenantId, TenderId = Tender.Id, LotCode = "LOT-1", LotNumber = 1, Title = "Equipment", Status = "Active" };
            var tenderItem = new TenderItem { Id = Guid.NewGuid(), TenantId = TenantId, TenderId = Tender.Id, LotId = lot.Id, Lot = lot, Description = "Laptop", UnitOfMeasure = "EA" };
            Bid = new TenderBid { Id = Guid.NewGuid(), TenantId = TenantId, TenderId = Tender.Id, BusinessPartnerId = partner.Id, BidNumber = "BID-1", Status = "Draft" };
            var bidLot = new TenderBidLot { Id = Guid.NewGuid(), TenantId = TenantId, TenderBidId = Bid.Id, LotId = lot.Id, Status = "Draft" };
            Item = new TenderBidItem { Id = Guid.NewGuid(), TenantId = TenantId, TenderBidId = Bid.Id, TenderItemId = tenderItem.Id, TenderItem = tenderItem, BidLotId = bidLot.Id, BidLot = bidLot };
            if (invalidity == "other-bid") Item.TenderBidId = Guid.NewGuid();
            if (invalidity == "other-tenant") Item.TenantId = Guid.NewGuid();
            if (invalidity == "deleted") Item.IsDeleted = true;
            if (invalidity == "cancelled-lot") lot.Status = "Cancelled";
            Context.AddRange(Tender, Bid, Item);
            Context.SaveChanges();
            Bind<Tender>(); Bind<TenderBid>(); Bind<TenderBidItem>(); Bind<TenderBidDocument>();
            Unit.SetupGet(unit => unit.HasActiveTransaction).Returns(true);
            Unit.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) => Context.SaveChangesAsync(token));
            Documents.Setup(repository => repository.CreateAsync(It.IsAny<TenderBidDocument>())).ReturnsAsync((TenderBidDocument document) => { Context.Add(document); return document; });
            Documents.Setup(repository => repository.DeleteAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
            Service = new TenderBidService(Mock.Of<ITenderBidRepository>(), Mock.Of<ITenderRepository>(),
                Mock.Of<ITenderBidItemRepository>(), Documents.Object, Mock.Of<ITenderPaymentRepository>(),
                Mock.Of<ITenderFeeRepository>(), Mock.Of<ITenderInterviewRepository>(), Mock.Of<ITenderAssignmentRepository>(),
                Mock.Of<ITenderBidLotRepository>(), Mock.Of<ITenderNotificationService>(), Partners.Object,
                Mock.Of<IBusinessPartnerUserRepository>(), Mock.Of<ISupplierValidationService>(), Unit.Object, User.Object,
                Mock.Of<IAppEventBus>(), Mock.Of<IProcurementTenderControlService>(), Mock.Of<IProcurementTenderDocumentControlService>(),
                Mock.Of<IProcurementExceptionalSourcingControlService>(), Mock.Of<IQuantitySurveyTenderBoqSubmissionService>(),
                NullLogger<TenderBidService>.Instance);
        }

        private void Bind<T>() where T : BaseEntity
        {
            var repository = new Mock<IGenericRepository<T>>();
            repository.Setup(value => value.GetQueryable(It.IsAny<Expression<Func<T, bool>>>()))
                .Returns((Expression<Func<T, bool>> predicate) => Context.Set<T>().Where(predicate));
            Unit.Setup(unit => unit.Repository<T>()).Returns(repository.Object);
        }

        public Task<TenderBidDocumentDto> Upload(Guid? itemId) => Service.UploadBidDocumentAsync(Bid.Id,
            new UploadBidDocumentDto { TenderItemId = itemId, DocumentName = "specification.pdf", DocumentType = "TechnicalProposal" },
            "dms:specification", "application/pdf", 100, UploadId, DocumentId, VersionId);
        public void Dispose() => Context.Dispose();
    }
}
