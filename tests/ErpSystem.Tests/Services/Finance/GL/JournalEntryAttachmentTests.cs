using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Moq;
using MockQueryable.Moq;
using Xunit;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace ErpSystem.Tests.Services.Finance.GL
{
    public class JournalEntryAttachmentTests
    {
        private readonly Mock<IFinancialRepository> _mockRepo;
        private readonly Mock<ICurrentUserService> _mockUserService;
        private readonly JournalEntryService _service;
        private readonly Guid _tenantId = Guid.NewGuid();

        public JournalEntryAttachmentTests()
        {
            _mockRepo = new Mock<IFinancialRepository>();
            _mockUserService = new Mock<ICurrentUserService>();

            _mockUserService.Setup(u => u.TenantId).Returns(_tenantId);

            var mockGeneralLedgerService = new Mock<IGeneralLedgerService>();
            var mockMapper = new Mock<IMapper>();
            var mockSegmentSecurityService = new Mock<IGLSegmentSecurityService>();
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockDocumentSplittingService = new Mock<IDocumentSplittingService>();
            var mockBookValidationService = new Mock<IBookValidationService>();
            var mockNotificationService = new Mock<INotificationService>();

            _service = new JournalEntryService(
                _mockRepo.Object,
                _mockUserService.Object,
                mockGeneralLedgerService.Object,
                mockMapper.Object,
                mockSegmentSecurityService.Object,
                mockConfig.Object,
                mockDocumentSplittingService.Object,
                mockBookValidationService.Object,
                mockNotificationService.Object
            );
        }

        [Fact]
        public async Task JournalEntryAttachment_Should_LinkAttachment_And_IncrementCount()
        {
            // Arrange
            var entryId = Guid.NewGuid();
            var fileId = Guid.NewGuid();

            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                AttachmentCount = 0,
                HasAttachments = false
            };

            _mockRepo.Setup(r => r.GetByIdAsync<JournalEntry>(entryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            _mockRepo.Setup(r => r.Query<JournalEntryAttachment>())
                .Returns(new List<JournalEntryAttachment>().AsQueryable().BuildMock());

            // Act
            await _service.LinkAttachmentAsync(entryId, fileId);

            // Assert
            _mockRepo.Verify(r => r.Add(It.Is<JournalEntryAttachment>(a => a.JournalEntryId == entryId && a.FileUploadRecordId == fileId)), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(1, entry.AttachmentCount);
            Assert.True(entry.HasAttachments);
        }

        [Fact]
        public async Task JournalEntryAttachment_Should_UnlinkAttachment_And_DecrementCount()
        {
            // Arrange
            var entryId = Guid.NewGuid();
            var fileId = Guid.NewGuid();

            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                AttachmentCount = 1,
                HasAttachments = true
            };

            var attachment = new JournalEntryAttachment
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entryId,
                FileUploadRecordId = fileId,
                TenantId = _tenantId
            };

            _mockRepo.Setup(r => r.GetByIdAsync<JournalEntry>(entryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            _mockRepo.Setup(r => r.Query<JournalEntryAttachment>())
                .Returns(new List<JournalEntryAttachment> { attachment }.AsQueryable().BuildMock());

            // Act
            await _service.UnlinkAttachmentAsync(entryId, fileId);

            // Assert
            _mockRepo.Verify(r => r.Remove(attachment), Times.Once);
            _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(0, entry.AttachmentCount);
            Assert.False(entry.HasAttachments);
        }

        [Fact]
        public async Task JournalEntryAttachment_Should_EnforceTenantIsolation()
        {
            // Arrange
            var entryId = Guid.NewGuid();
            var fileId = Guid.NewGuid();

            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = Guid.NewGuid(), // Different tenant!
            };

            _mockRepo.Setup(r => r.GetByIdAsync<JournalEntry>(entryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.LinkAttachmentAsync(entryId, fileId));
        }
    }
}
