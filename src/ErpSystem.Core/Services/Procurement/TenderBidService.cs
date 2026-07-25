using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderBidService : ITenderBidService
{
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderBidItemRepository _bidItemRepository;
    private readonly ITenderBidDocumentRepository _bidDocumentRepository;
    private readonly ITenderPaymentRepository _paymentRepository;
    private readonly ITenderInterviewRepository _interviewRepository;
    private readonly ITenderAssignmentRepository _assignmentRepository;
    private readonly ITenderBidLotRepository _bidLotRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IBusinessPartnerUserRepository _businessPartnerUserRepository;
    private readonly ISupplierValidationService _supplierValidationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderBidService> _logger;
    private readonly IAppEventBus _appEventBus;
    private readonly IProcurementTenderControlService _tenderControlService;
    private readonly IProcurementExceptionalSourcingControlService _exceptionalSourcingControlService;

    public TenderBidService(
        ITenderBidRepository bidRepository,
        ITenderRepository tenderRepository,
        ITenderBidItemRepository bidItemRepository,
        ITenderBidDocumentRepository bidDocumentRepository,
        ITenderPaymentRepository paymentRepository,
        ITenderInterviewRepository interviewRepository,
        ITenderAssignmentRepository assignmentRepository,
        ITenderBidLotRepository bidLotRepository,
        ITenderNotificationService notificationService,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        ISupplierValidationService supplierValidationService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IAppEventBus appEventBus,
        IProcurementTenderControlService tenderControlService,
        IProcurementExceptionalSourcingControlService exceptionalSourcingControlService,
        ILogger<TenderBidService> logger)
    {
        _bidRepository = bidRepository;
        _tenderRepository = tenderRepository;
        _bidItemRepository = bidItemRepository;
        _bidDocumentRepository = bidDocumentRepository;
        _paymentRepository = paymentRepository;
        _interviewRepository = interviewRepository;
        _assignmentRepository = assignmentRepository;
        _bidLotRepository = bidLotRepository;
        _notificationService = notificationService;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _supplierValidationService = supplierValidationService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
        _tenderControlService = tenderControlService;
        _exceptionalSourcingControlService = exceptionalSourcingControlService;
        _logger = logger;
    }

    public async Task<TenderBidDetailDto?> GetBidByIdAsync(Guid id)
    {
        try
        {
            var bid = await _bidRepository.GetWithAllRelatedDataAsync(id);
            if (bid == null) return null;

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var items = await _bidItemRepository.GetByBidIdAsync(id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(id);

            return MapToDetailDto(bid, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bid {BidId}", id);
            throw;
        }
    }

    public async Task<TenderBidDetailDto?> GetBidByNumberAsync(string bidNumber)
    {
        try
        {
            var bid = await _bidRepository.GetByBidNumberAsync(bidNumber);
            if (bid == null) return null;

            // Fetch all related data including evaluations and interviews
            var bidWithAllData = await _bidRepository.GetWithAllRelatedDataAsync(bid.Id);
            if (bidWithAllData == null) return null;

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var items = await _bidItemRepository.GetByBidIdAsync(bid.Id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(bid.Id);

            return MapToDetailDto(bidWithAllData, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bid by number {BidNumber}", bidNumber);
            throw;
        }
    }

    public async Task<PagedResult<TenderBidSummaryDto>> GetBidsAsync(int page, int pageSize, string? search = null, string? status = null)
    {
        try
        {
            var pagedBids = await _bidRepository.GetBidsAsync(page, pageSize, search, status);
            var items = new List<TenderBidSummaryDto>();

            foreach (var bid in pagedBids.Items)
            {
                var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);

                // Get payment information
                var payments = await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId);
                var completedPayment = payments.FirstOrDefault(p => p.Status == "Completed");

                items.Add(MapToSummaryDto(bid, tender, completedPayment));
            }

            return new PagedResult<TenderBidSummaryDto>
            {
                Items = items,
                TotalCount = pagedBids.TotalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bids");
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidSummaryDto>> GetBidsByTenderIdAsync(Guid tenderId)
    {
        try
        {
            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var tender = await _tenderRepository.GetByIdAsync(tenderId);
            var summaries = new List<TenderBidSummaryDto>();

            foreach (var bid in bids)
            {
                // Get payment information
                var payments = await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId);
                var completedPayment = payments.FirstOrDefault(p => p.Status == "Completed");

                summaries.Add(MapToSummaryDto(bid, tender, completedPayment));
            }

            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bids for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidSummaryDto>> GetMyBidsAsync(Guid businessPartnerId)
    {
        try
        {
            // Get bids created by this business partner
            var bids = await _bidRepository.GetByBusinessPartnerIdAsync(businessPartnerId);
            var bidList = bids.ToList();

            // Also get bids for tenders where the current user is assigned via TenderAssignment
            var assignments = await _assignmentRepository.GetByBusinessPartnerIdAsync(businessPartnerId);

            // Filter assignments for the current user
            var userAssignments = assignments.Where(a =>
                a.AssignmentType == "AllUsers" ||
                a.AssignedToUserId == _currentUserProvider.UserId
            ).ToList();

            // Get tender IDs from assignments
            var assignedTenderIds = userAssignments.Select(a => a.TenderId).Distinct().ToList();

            // Get bids for assigned tenders (that aren't already in the list)
            foreach (var tenderId in assignedTenderIds)
            {
                var tenderBids = await _bidRepository.GetByTenderIdAsync(tenderId);
                foreach (var tenderBid in tenderBids)
                {
                    // Only add if not already in the list (avoid duplicates)
                    if (!bidList.Any(b => b.Id == tenderBid.Id))
                    {
                        bidList.Add(tenderBid);
                    }
                }
            }

            var summaries = new List<TenderBidSummaryDto>();

            foreach (var bid in bidList)
            {
                var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);

                // Get payment information
                var payments = await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId);
                var completedPayment = payments.FirstOrDefault(p => p.Status == "Completed");

                summaries.Add(MapToSummaryDto(bid, tender, completedPayment));
            }

            return summaries.OrderByDescending(s => s.SubmittedDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bids for business partner {BusinessPartnerId}", businessPartnerId);
            throw;
        }
    }

    public async Task<TenderBidDetailDto?> GetMyDraftBidByTenderIdAsync(Guid tenderId)
    {
        try
        {
            // Get the business partner for the current user
            // First, try to get as main account owner
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            // If not found, try to get as sub-user via BusinessPartnerUser table
            if (businessPartner == null)
            {
                var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                if (businessPartnerUser != null && businessPartnerUser.IsActive)
                {
                    businessPartner = businessPartnerUser.BusinessPartner;
                }
            }

            if (businessPartner == null)
            {
                return null;
            }

            // Get the draft bid for this tender and business partner
            var bid = await _bidRepository.GetByTenderAndPartnerAsync(tenderId, businessPartner.Id);
            if (bid == null || bid.Status != "Draft")
            {
                return null;
            }

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var items = await _bidItemRepository.GetByBidIdAsync(bid.Id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(bid.Id);

            return MapToDetailDto(bid, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving draft bid for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderBidDetailDto> CreateBidAsync(CreateTenderBidDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(dto.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {dto.TenderId} not found");

            if (tender.Status != "Published")
            {
                throw new InvalidOperationException("Bids can only be submitted for published tenders");
            }

            var isNctOrIct = await _tenderControlService.IsNctOrIctAsync(tender.Id);
            if (!isNctOrIct && DateTime.UtcNow > tender.SubmissionDeadline)
            {
                throw new InvalidOperationException("Tender submission deadline has passed");
            }

            // Get the business partner for the current user
            // First, try to get as main account owner
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            // If not found, try to get as sub-user via BusinessPartnerUser table
            if (businessPartner == null)
            {
                var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                if (businessPartnerUser != null && businessPartnerUser.IsActive)
                {
                    businessPartner = businessPartnerUser.BusinessPartner;
                }
            }

            if (businessPartner == null)
            {
                throw new InvalidOperationException("No business partner found for the current user. Please complete your business partner registration first.");
            }

            await _exceptionalSourcingControlService.EnsureBidSupplierAllowedAsync(tender.Id, businessPartner.Id);

            // Validate supplier eligibility for this tender
            var validationResult = await _supplierValidationService.ValidateForTenderAsync(
                businessPartner.Id,
                tender.RequiresPrequalification,
                tender.MinimumPerformanceRating
            );

            if (!validationResult.IsValid)
            {
                var errorMessage = string.Join("; ", validationResult.Errors);
                _logger.LogWarning("Supplier {PartnerId} failed validation for tender {TenderId}: {Errors}",
                    businessPartner.Id, dto.TenderId, errorMessage);
                throw new InvalidOperationException($"You are not eligible to bid on this tender. {errorMessage}");
            }

            // Generate bid number
            var bidNumber = await GenerateBidNumberAsync(tender.TenderNumber);

            var bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = dto.TenderId,
                BusinessPartnerId = businessPartner.Id,
                BidNumber = bidNumber,
                SubmittedDate = DateTime.UtcNow,
                Status = "Draft",
                DeliveryDays = dto.DeliveryDays,
                PaymentTerms = dto.PaymentTerms,
                WarrantyTerms = dto.WarrantyTerms,
                TechnicalProposal = dto.TechnicalProposal,
                CommercialProposal = dto.CommercialProposal,
                Currency = tender.Currency,
                CreatedAt = DateTime.UtcNow
            };

            await _bidRepository.CreateAsync(bid);

            // Save the bid first to satisfy foreign key constraint
            await _unitOfWork.SaveChangesAsync();

            // Add bid items
            var items = new List<TenderBidItem>();
            if (dto.Items != null && dto.Items.Any())
            {
                decimal totalBidAmount = 0;

                foreach (var itemDto in dto.Items)
                {
                    var item = new TenderBidItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserProvider.TenantId,
                        TenderBidId = bid.Id,
                        TenderItemId = itemDto.TenderItemId,
                        OfferedQuantity = itemDto.OfferedQuantity,
                        UnitPrice = itemDto.UnitPrice,
                        TotalPrice = itemDto.OfferedQuantity * itemDto.UnitPrice,
                        DeliveryDays = itemDto.DeliveryDays,
                        Specifications = itemDto.Specifications,
                        Brand = itemDto.Brand,
                        Model = itemDto.Model,
                        TechnicalDetails = itemDto.TechnicalDetails,
                        CreatedAt = DateTime.UtcNow
                    };

                    totalBidAmount += item.TotalPrice;
                    await _bidItemRepository.CreateAsync(item);
                    items.Add(item);
                }

                bid.TotalBidAmount = totalBidAmount;
                await _bidRepository.UpdateAsync(bid);

                // Save bid items and updated total
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Created bid {BidId} for tender {TenderId}", bid.Id, dto.TenderId);

            // Publish events for admin-configurable notification topics (best-effort).
            try
            {
                var baseData = new Dictionary<string, object>
                {
                    ["BidId"] = bid.Id,
                    ["BidNumber"] = bid.BidNumber ?? string.Empty,
                    ["TenderId"] = bid.TenderId,
                    ["TenderNumber"] = tender.TenderNumber ?? string.Empty,
                    ["BusinessPartnerId"] = bid.BusinessPartnerId,
                    ["Status"] = bid.Status ?? string.Empty,
                    ["TotalBidAmount"] = bid.TotalBidAmount,
                    ["Currency"] = bid.Currency ?? string.Empty
                };

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Created",
                    Audience = "Supplier",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Bid.Created entity activity event for bid {BidId}", bid.Id);
            }

            return MapToDetailDto(bid, tender, items, new List<TenderBidDocument>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bid for tender {TenderId}", dto.TenderId);
            throw;
        }
    }

    public async Task<TenderBidDetailDto> UpdateBidAsync(Guid id, UpdateTenderBidDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft bids can be updated");
            }

            bid.DeliveryDays = dto.DeliveryDays;
            bid.PaymentTerms = dto.PaymentTerms;
            bid.WarrantyTerms = dto.WarrantyTerms;
            bid.TechnicalProposal = dto.TechnicalProposal;
            bid.CommercialProposal = dto.CommercialProposal;
            bid.UpdatedAt = DateTime.UtcNow;

            await _bidRepository.UpdateAsync(bid);

            // Update bid items if provided
            if (dto.Items != null && dto.Items.Any())
            {
                // Get existing items
                var existingItems = await _bidItemRepository.GetByBidIdAsync(id);

                decimal totalBidAmount = 0;

                foreach (var itemDto in dto.Items)
                {
                    // Find existing item by TenderItemId
                    var existingItem = existingItems.FirstOrDefault(i => i.TenderItemId == itemDto.TenderItemId);

                    if (existingItem != null)
                    {
                        // Update existing item
                        existingItem.OfferedQuantity = itemDto.OfferedQuantity;
                        existingItem.UnitPrice = itemDto.UnitPrice;
                        existingItem.TotalPrice = itemDto.OfferedQuantity * itemDto.UnitPrice;
                        existingItem.DeliveryDays = itemDto.DeliveryDays;
                        existingItem.Specifications = itemDto.Specifications;
                        existingItem.Brand = itemDto.Brand;
                        existingItem.Model = itemDto.Model;
                        existingItem.TechnicalDetails = itemDto.TechnicalDetails;
                        existingItem.UpdatedAt = DateTime.UtcNow;

                        await _bidItemRepository.UpdateAsync(existingItem);
                    }
                    else
                    {
                        // Create new item
                        var newItem = new TenderBidItem
                        {
                            Id = Guid.NewGuid(),
                            TenantId = _currentUserProvider.TenantId,
                            TenderBidId = bid.Id,
                            TenderItemId = itemDto.TenderItemId,
                            OfferedQuantity = itemDto.OfferedQuantity,
                            UnitPrice = itemDto.UnitPrice,
                            TotalPrice = itemDto.OfferedQuantity * itemDto.UnitPrice,
                            DeliveryDays = itemDto.DeliveryDays,
                            Specifications = itemDto.Specifications,
                            Brand = itemDto.Brand,
                            Model = itemDto.Model,
                            TechnicalDetails = itemDto.TechnicalDetails,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _bidItemRepository.CreateAsync(newItem);
                    }

                    totalBidAmount += itemDto.OfferedQuantity * itemDto.UnitPrice;
                }

                // Update total bid amount
                bid.TotalBidAmount = totalBidAmount;
                await _bidRepository.UpdateAsync(bid);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated bid {BidId}", id);

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var items = await _bidItemRepository.GetByBidIdAsync(id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(id);

            return MapToDetailDto(bid, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid {BidId}", id);
            throw;
        }
    }

    public async Task<TenderBidDetailDto> SubmitBidAsync(Guid id, SubmitTenderBidDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft bids can be submitted");
            }

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {bid.TenderId} not found");

            var submittedAtUtc = DateTime.UtcNow;
            await _exceptionalSourcingControlService.EnsureBidSupplierAllowedAsync(tender.Id, bid.BusinessPartnerId);
            var isNctOrIct = await _tenderControlService.IsNctOrIctAsync(tender.Id);
            if (!isNctOrIct && submittedAtUtc > tender.SubmissionDeadline)
            {
                throw new InvalidOperationException("Tender submission deadline has passed");
            }

            // Validate bid items before submission
            var items = await _bidItemRepository.GetByBidIdAsync(id);
            if (items == null || !items.Any())
            {
                throw new InvalidOperationException("Cannot submit bid without any items");
            }

            // Validate that all items have valid pricing
            var invalidItems = items.Where(item => item.UnitPrice <= 0 || item.OfferedQuantity <= 0).ToList();
            if (invalidItems.Any())
            {
                throw new InvalidOperationException("All bid items must have valid unit price (greater than 0) and offered quantity (greater than 0) before submission");
            }

            bid.Status = "Submitted";
            bid.SubmittedDate = submittedAtUtc;
            bid.UpdatedAt = submittedAtUtc;

            await _bidRepository.UpdateAsync(bid);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(id);
            var disposition = await _tenderControlService.RecordSubmissionAsync(
                bid, bid.SubmittedDate, Guid.NewGuid().ToString("N"));
            if (disposition == ProcurementTenderSubmissionDisposition.LateRejected)
            {
                bid.Status = "Rejected";
                bid.RejectionReason = "Late submission rejected by the statutory NCT/ICT deadline control.";
                await _bidRepository.UpdateAsync(bid);
            }
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Submitted bid {BidId}", id);

            // Send notification only for accepted submissions.
            if (disposition != ProcurementTenderSubmissionDisposition.LateRejected)
                await _notificationService.SendBidSubmittedNotificationAsync(id);

            // Publish events for admin-configurable notification topics (best-effort).
            try
            {
                var baseData = new Dictionary<string, object>
                {
                    ["BidId"] = bid.Id,
                    ["BidNumber"] = bid.BidNumber ?? string.Empty,
                    ["TenderId"] = bid.TenderId,
                    ["TenderNumber"] = tender.TenderNumber ?? string.Empty,
                    ["BusinessPartnerId"] = bid.BusinessPartnerId,
                    ["Status"] = bid.Status ?? string.Empty,
                    ["SubmittedDate"] = bid.SubmittedDate.ToString("o"),
                    ["TotalBidAmount"] = bid.TotalBidAmount,
                    ["Currency"] = bid.Currency ?? string.Empty
                };

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Submitted",
                    Audience = "Supplier",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Submitted",
                    Audience = "Internal",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Bid.Submitted entity activity event for bid {BidId}", bid.Id);
            }

            return MapToDetailDto(bid, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting bid {BidId}", id);
            throw;
        }
    }

    public async Task WithdrawBidAsync(Guid id, WithdrawTenderBidDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status == "Awarded" || bid.Status == "Withdrawn")
            {
                throw new InvalidOperationException($"Cannot withdraw bid with status {bid.Status}");
            }

            bid.Status = "Withdrawn";
            bid.RejectionReason = dto.Reason;
            bid.UpdatedAt = DateTime.UtcNow;

            await _bidRepository.UpdateAsync(bid);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Withdrew bid {BidId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing bid {BidId}", id);
            throw;
        }
    }

    public async Task<TenderBidDetailDto> OpenBidAsync(Guid id)
    {
        try
        {
            var bid = await _bidRepository.GetWithAllRelatedDataAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");
            if (await _tenderControlService.IsNctOrIctAsync(bid.TenderId))
                throw new ProcurementTenderControlConflictException(
                    "TENDER_STATUTORY_OPENING_REQUIRED",
                    "NCT/ICT bids can be opened only through the signed public-opening control.");

            if (bid.Status != "Submitted")
            {
                throw new InvalidOperationException($"Only submitted bids can be opened. Current status: {bid.Status}");
            }

            bid.Status = "Opened";
            bid.OpenedDate = DateTime.UtcNow;
            bid.OpenedById = _currentUserProvider.UserId;
            bid.UpdatedAt = DateTime.UtcNow;

            await _bidRepository.UpdateAsync(bid);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Opened bid {BidId} by user {UserId}", id, _currentUserProvider.UserId);

            // Reload to get the updated data with navigation properties
            bid = await _bidRepository.GetWithAllRelatedDataAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found after update");

            return MapToDetailDto(bid, bid.Tender, bid.Items.ToList(), bid.Documents.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening bid {BidId}", id);
            throw;
        }
    }

    public async Task<int> OpenAllBidsByTenderAsync(Guid tenderId)
    {
        try
        {
            if (await _tenderControlService.IsNctOrIctAsync(tenderId))
                throw new ProcurementTenderControlConflictException(
                    "TENDER_STATUTORY_OPENING_REQUIRED",
                    "NCT/ICT bids can be opened only through the signed public-opening control.");
            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var submittedBids = bids.Where(b => b.Status == "Submitted").ToList();

            if (!submittedBids.Any())
            {
                return 0;
            }

            var openedDate = DateTime.UtcNow;
            var openedById = _currentUserProvider.UserId;

            foreach (var bid in submittedBids)
            {
                bid.Status = "Opened";
                bid.OpenedDate = openedDate;
                bid.OpenedById = openedById;
                bid.UpdatedAt = openedDate;
                await _bidRepository.UpdateAsync(bid);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Opened {Count} bids for tender {TenderId} by user {UserId}",
                submittedBids.Count, tenderId, _currentUserProvider.UserId);

            return submittedBids.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening bids for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<List<SupplierBidListItemDto>> GetSupplierBidListByTenderAsync(Guid tenderId)
    {
        try
        {
            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);

            return bids
                .OrderBy(b => b.SubmittedDate)
                .Select(b => new SupplierBidListItemDto
                {
                    BidNumber = b.BidNumber,
                    BusinessPartnerName = b.BusinessPartner?.PartnerName ?? "Unknown",
                    BusinessPartnerCode = b.BusinessPartner?.PartnerCode ?? "N/A",
                    SubmittedDate = b.SubmittedDate,
                    Status = b.Status,
                    OpenedDate = b.OpenedDate,
                    TotalBidAmount = b.TotalBidAmount,
                    Currency = b.Currency
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier bid list for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task DeleteBidAsync(Guid id)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft bids can be deleted");
            }

            await _bidRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted bid {BidId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid {BidId}", id);
            throw;
        }
    }

    // Bid Items
    public async Task<TenderBidItemDto> AddBidItemAsync(Guid bidId, CreateTenderBidItemDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId)
                ?? throw new InvalidOperationException($"Bid with ID {bidId} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Can only add items to draft bids");
            }

            var item = new TenderBidItem
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderBidId = bidId,
                TenderItemId = dto.TenderItemId,
                OfferedQuantity = dto.OfferedQuantity,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.OfferedQuantity * dto.UnitPrice,
                DeliveryDays = dto.DeliveryDays,
                Specifications = dto.Specifications,
                Brand = dto.Brand,
                Model = dto.Model,
                TechnicalDetails = dto.TechnicalDetails,
                CreatedAt = DateTime.UtcNow
            };

            await _bidItemRepository.CreateAsync(item);

            // Update total bid amount
            var allItems = await _bidItemRepository.GetByBidIdAsync(bidId);
            bid.TotalBidAmount = allItems.Sum(i => i.TotalPrice) + item.TotalPrice;
            await _bidRepository.UpdateAsync(bid);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added item {ItemId} to bid {BidId}", item.Id, bidId);

            return MapBidItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<TenderBidItemDto> UpdateBidItemAsync(Guid itemId, CreateTenderBidItemDto dto)
    {
        try
        {
            var item = await _bidItemRepository.GetByIdAsync(itemId)
                ?? throw new InvalidOperationException($"Item with ID {itemId} not found");

            var bid = await _bidRepository.GetByIdAsync(item.TenderBidId);
            if (bid != null && bid.Status != "Draft")
            {
                throw new InvalidOperationException("Can only update items in draft bids");
            }

            item.TenderItemId = dto.TenderItemId;
            item.OfferedQuantity = dto.OfferedQuantity;
            item.UnitPrice = dto.UnitPrice;
            item.TotalPrice = dto.OfferedQuantity * dto.UnitPrice;
            item.DeliveryDays = dto.DeliveryDays;
            item.Specifications = dto.Specifications;
            item.Brand = dto.Brand;
            item.Model = dto.Model;
            item.TechnicalDetails = dto.TechnicalDetails;
            item.UpdatedAt = DateTime.UtcNow;

            await _bidItemRepository.UpdateAsync(item);

            // Update total bid amount
            if (bid != null)
            {
                var allItems = await _bidItemRepository.GetByBidIdAsync(bid.Id);
                bid.TotalBidAmount = allItems.Sum(i => i.TotalPrice);
                await _bidRepository.UpdateAsync(bid);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated bid item {ItemId}", itemId);

            return MapBidItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid item {ItemId}", itemId);
            throw;
        }
    }

    public async Task DeleteBidItemAsync(Guid itemId)
    {
        try
        {
            var item = await _bidItemRepository.GetByIdAsync(itemId);
            if (item != null)
            {
                var bid = await _bidRepository.GetByIdAsync(item.TenderBidId);
                if (bid != null && bid.Status != "Draft")
                {
                    throw new InvalidOperationException("Can only delete items from draft bids");
                }

                await _bidItemRepository.DeleteAsync(itemId);

                // Update total bid amount
                if (bid != null)
                {
                    var allItems = await _bidItemRepository.GetByBidIdAsync(bid.Id);
                    bid.TotalBidAmount = allItems.Sum(i => i.TotalPrice);
                    await _bidRepository.UpdateAsync(bid);
                }

                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Deleted bid item {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid item {ItemId}", itemId);
            throw;
        }
    }

    // Bid Documents
    public async Task<TenderBidDocumentDto> UploadBidDocumentAsync(Guid bidId, UploadBidDocumentDto dto, string filePath, string? fileType, long? fileSize)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId)
                ?? throw new InvalidOperationException($"Bid with ID {bidId} not found");

            var document = new TenderBidDocument
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderBidId = bidId,
                DocumentName = dto.DocumentName,
                DocumentType = dto.DocumentType,
                FilePath = filePath,
                FileType = fileType,
                FileSize = fileSize,
                UploadedDate = DateTime.UtcNow,
                UploadedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _bidDocumentRepository.CreateAsync(document);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Uploaded document {DocumentId} to bid {BidId}", document.Id, bidId);

            return MapBidDocumentToDto(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to bid {BidId}", bidId);
            throw;
        }
    }

    public async Task DeleteBidDocumentAsync(Guid documentId)
    {
        try
        {
            await _bidDocumentRepository.DeleteAsync(documentId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted bid document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidDocumentDto>> GetBidDocumentsAsync(Guid bidId)
    {
        try
        {
            var documents = await _bidDocumentRepository.GetByBidIdAsync(bidId);
            return documents.Select(MapBidDocumentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for bid {BidId}", bidId);
            throw;
        }
    }

    // Bid Payments
    public async Task<TenderPaymentDto> RecordPaymentAsync(CreateTenderPaymentDto dto)
    {
        try
        {
            // Get the business partner for the current user
            // First, try to get as main account owner
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            // If not found, try to get as sub-user via BusinessPartnerUser table
            if (businessPartner == null)
            {
                var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                if (businessPartnerUser != null && businessPartnerUser.IsActive)
                {
                    businessPartner = businessPartnerUser.BusinessPartner;
                }
            }

            if (businessPartner == null)
            {
                throw new InvalidOperationException("No business partner found for the current user. Please complete your business partner registration first.");
            }

            var payment = new TenderPayment
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderFeeId = dto.TenderFeeId,
                BusinessPartnerId = businessPartner.Id,
                PaymentReference = await _paymentRepository.GeneratePaymentReferenceAsync(),
                Amount = dto.Amount,
                Currency = dto.Currency,
                PaymentMethod = dto.PaymentMethod,
                Status = "Pending",
                PaymentDate = DateTime.UtcNow,
                TransactionId = dto.TransactionId,
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepository.CreateAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Recorded payment {PaymentId}", payment.Id);

            return MapPaymentToDto(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording payment");
            throw;
        }
    }

    public async Task<TenderPaymentDto> VerifyPaymentAsync(Guid paymentId, VerifyPaymentDto dto)
    {
        try
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId)
                ?? throw new InvalidOperationException($"Payment with ID {paymentId} not found");

            payment.Status = dto.IsApproved ? "Verified" : "Rejected";
            payment.VerifiedDate = DateTime.UtcNow;
            payment.VerifiedById = _currentUserProvider.UserId;
            payment.Notes = $"{payment.Notes}\n{dto.Notes}";
            payment.UpdatedAt = DateTime.UtcNow;

            await _paymentRepository.UpdateAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Verified payment {PaymentId}", paymentId);

            return MapPaymentToDto(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment {PaymentId}", paymentId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderPaymentDto>> GetBidPaymentsAsync(Guid bidId)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId);
            if (bid == null) return Enumerable.Empty<TenderPaymentDto>();

            var payments = await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId);
            return payments.Select(MapPaymentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments for bid {BidId}", bidId);
            throw;
        }
    }

    // Bid Interviews
    public async Task<TenderInterviewDto> ScheduleInterviewAsync(ScheduleInterviewDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {dto.TenderBidId} not found");

            var interview = new TenderInterview
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = bid.TenderId,
                TenderBidId = dto.TenderBidId,
                Title = dto.Title,
                ScheduledDate = dto.ScheduledDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Location = dto.Location,
                InterviewType = dto.InterviewType,
                PanelMembers = dto.PanelMembers,
                Agenda = dto.Agenda,
                Status = "Scheduled",
                ConductedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _interviewRepository.CreateAsync(interview);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Scheduled interview {InterviewId} for bid {BidId}", interview.Id, dto.TenderBidId);

            // Send notification
            await _notificationService.SendInterviewScheduledNotificationAsync(interview.Id);

            return MapInterviewToDto(interview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling interview for bid {BidId}", dto.TenderBidId);
            throw;
        }
    }

    public async Task<TenderInterviewDto> UpdateInterviewAsync(Guid interviewId, ScheduleInterviewDto dto)
    {
        try
        {
            var interview = await _interviewRepository.GetByIdAsync(interviewId)
                ?? throw new InvalidOperationException($"Interview with ID {interviewId} not found");

            interview.Title = dto.Title;
            interview.ScheduledDate = dto.ScheduledDate;
            interview.StartTime = dto.StartTime;
            interview.EndTime = dto.EndTime;
            interview.Location = dto.Location;
            interview.InterviewType = dto.InterviewType;
            interview.PanelMembers = dto.PanelMembers;
            interview.Agenda = dto.Agenda;
            interview.UpdatedAt = DateTime.UtcNow;

            await _interviewRepository.UpdateAsync(interview);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated interview {InterviewId}", interviewId);

            return MapInterviewToDto(interview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating interview {InterviewId}", interviewId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderInterviewDto>> GetBidInterviewsAsync(Guid bidId)
    {
        try
        {
            var interviews = await _interviewRepository.GetByBidIdAsync(bidId);
            return interviews.Select(MapInterviewToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving interviews for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderInterviewDto>> GetUpcomingInterviewsAsync()
    {
        try
        {
            var interviews = await _interviewRepository.GetUpcomingInterviewsAsync();
            return interviews.Select(MapInterviewToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming interviews");
            throw;
        }
    }

    // Helper methods
    private async Task<string> GenerateBidNumberAsync(string tenderNumber)
    {
        return await _bidRepository.GenerateBidNumberAsync();
    }

    // Mapping methods
    private static TenderBidSummaryDto MapToSummaryDto(TenderBid bid, Tender? tender, TenderPayment? payment)
    {
        return new TenderBidSummaryDto
        {
            Id = bid.Id,
            BidNumber = bid.BidNumber,
            TenderId = bid.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            BusinessPartnerId = bid.BusinessPartnerId,
            BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
            SubmittedDate = bid.SubmittedDate,
            TotalBidAmount = bid.TotalBidAmount,
            Currency = bid.Currency,
            Status = bid.Status,
            TotalScore = bid.TotalScore,
            Rank = bid.Rank,
            HasPaidFees = payment != null && payment.Status == "Completed",
            PaymentStatus = payment?.Status,
            // QCBS Scores
            TechnicalScore = bid.TechnicalScore,
            FinancialScore = bid.FinancialScore,
            CombinedScore = bid.CombinedScore,
            IsQualifiedTechnically = bid.IsQualifiedTechnically,
            DisqualificationReason = bid.DisqualificationReason
        };
    }

    private static TenderBidDetailDto MapToDetailDto(TenderBid bid, Tender? tender, IEnumerable<TenderBidItem> items, IEnumerable<TenderBidDocument> documents)
    {
        return new TenderBidDetailDto
        {
            Id = bid.Id,
            BidNumber = bid.BidNumber,
            TenderId = bid.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            BusinessPartnerId = bid.BusinessPartnerId,
            BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
            SubmittedDate = bid.SubmittedDate,
            Status = bid.Status,
            TotalBidAmount = bid.TotalBidAmount,
            Currency = bid.Currency,
            DeliveryDays = bid.DeliveryDays,
            PaymentTerms = bid.PaymentTerms,
            WarrantyTerms = bid.WarrantyTerms,
            TechnicalProposal = bid.TechnicalProposal,
            CommercialProposal = bid.CommercialProposal,
            IsCompliant = bid.IsCompliant,
            NonComplianceReasons = bid.NonComplianceReasons,
            EvaluationTemplateId = tender?.EvaluationTemplateId,
            EvaluationTemplateName = tender?.EvaluationTemplate?.TemplateName,
            PriceScore = bid.PriceScore,
            QualityScore = bid.QualityScore,
            DeliveryScore = bid.DeliveryScore,
            ExperienceScore = bid.ExperienceScore,
            TotalScore = bid.TotalScore,
            Rank = bid.Rank,
            // QCBS Scores
            TechnicalScore = bid.TechnicalScore,
            FinancialScore = bid.FinancialScore,
            CombinedScore = bid.CombinedScore,
            IsQualifiedTechnically = bid.IsQualifiedTechnically,
            DisqualificationReason = bid.DisqualificationReason,
            OpenedDate = bid.OpenedDate,
            OpenedByName = bid.OpenedBy != null ? $"{bid.OpenedBy.FirstName} {bid.OpenedBy.LastName}" : null,
            EvaluationNotes = bid.EvaluationNotes,
            RejectionReason = bid.RejectionReason,
            CreatedAt = bid.CreatedAt,
            UpdatedAt = bid.UpdatedAt ?? bid.CreatedAt,
            Items = items.Select(MapBidItemToDto).ToList(),
            Documents = documents.Select(MapBidDocumentToDto).ToList(),
            Evaluations = bid.Evaluations?.Select(MapEvaluationToDto).ToList() ?? new(),
            Interviews = bid.Interviews?.Select(MapInterviewToDto).ToList() ?? new()
        };
    }

    private static TenderBidItemDto MapBidItemToDto(TenderBidItem item)
    {
        return new TenderBidItemDto
        {
            Id = item.Id,
            TenderBidId = item.TenderBidId,
            BidLotId = item.BidLotId,
            LotCode = item.TenderItem?.Lot?.LotCode,
            TenderItemId = item.TenderItemId,
            TenderItemDescription = item.TenderItem?.Description ?? string.Empty,
            RequestedQuantity = item.TenderItem?.Quantity ?? 0,
            OfferedQuantity = item.OfferedQuantity,
            UnitOfMeasure = item.TenderItem?.UnitOfMeasure,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            DeliveryDays = item.DeliveryDays,
            Specifications = item.Specifications,
            Brand = item.Brand,
            Model = item.Model,
            TechnicalDetails = item.TechnicalDetails
        };
    }

    private static TenderBidDocumentDto MapBidDocumentToDto(TenderBidDocument document)
    {
        return new TenderBidDocumentDto
        {
            Id = document.Id,
            TenderBidId = document.TenderBidId,
            DocumentName = document.DocumentName,
            DocumentType = document.DocumentType,
            FilePath = document.FilePath,
            FileType = document.FileType,
            FileSize = document.FileSize,
            UploadedDate = document.UploadedDate,
            UploadedByName = string.Empty // Would need to fetch from User entity
        };
    }

    private static TenderPaymentDto MapPaymentToDto(TenderPayment payment)
    {
        return new TenderPaymentDto
        {
            Id = payment.Id,
            TenderFeeId = payment.TenderFeeId,
            BusinessPartnerId = payment.BusinessPartnerId,
            BusinessPartnerName = string.Empty, // Would need to fetch from BusinessPartner entity
            PaymentReference = payment.PaymentReference,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            PaymentDate = payment.PaymentDate,
            VerifiedDate = payment.VerifiedDate,
            VerifiedByName = string.Empty, // Would need to fetch from User entity
            TransactionId = payment.TransactionId,
            PaymentProof = payment.PaymentProof
        };
    }

    private static TenderEvaluationDto MapEvaluationToDto(TenderEvaluation evaluation)
    {
        return new TenderEvaluationDto
        {
            Id = evaluation.Id,
            TenderBidId = evaluation.TenderBidId,
            TenderEvaluatorId = evaluation.TenderEvaluatorId,
            EvaluatorName = evaluation.TenderEvaluator != null ? $"{evaluation.TenderEvaluator.User?.FirstName} {evaluation.TenderEvaluator.User?.LastName}".Trim() : string.Empty,
            EvaluatorRole = string.Empty,
            EvaluationDate = evaluation.CreatedAt,
            Status = evaluation.Status,
            PriceScore = evaluation.PriceScore,
            QualityScore = evaluation.QualityScore,
            DeliveryScore = evaluation.DeliveryScore,
            ExperienceScore = evaluation.ExperienceScore,
            TechnicalScore = evaluation.TechnicalScore,
            ComplianceScore = evaluation.ComplianceScore,
            TotalScore = evaluation.TotalScore,
            EvaluationCriteriaJson = evaluation.EvaluationCriteriaJson,
            TechnicalComments = evaluation.TechnicalComments,
            CommercialComments = evaluation.CommercialComments,
            OverallComments = evaluation.OverallComments,
            IsRecommended = evaluation.IsRecommended,
            Recommendation = evaluation.Recommendation
        };
    }

    private static TenderInterviewDto MapInterviewToDto(TenderInterview interview)
    {
        return new TenderInterviewDto
        {
            Id = interview.Id,
            TenderId = interview.TenderId,
            TenderBidId = interview.TenderBidId,
            BusinessPartnerName = string.Empty, // Would need to fetch from BusinessPartner entity
            Title = interview.Title,
            ScheduledDate = interview.ScheduledDate,
            StartTime = interview.StartTime,
            EndTime = interview.EndTime,
            Location = interview.Location,
            InterviewType = interview.InterviewType,
            Agenda = interview.Agenda,
            Status = interview.Status,
            InterviewNotes = interview.InterviewNotes,
            InterviewScore = interview.InterviewScore,
            CompletedDate = interview.CompletedDate,
            ConductedByName = string.Empty // Would need to fetch from User entity
        };
    }

    #region Bid LOT Methods

    public async Task<TenderBidLotDto> AddBidLotAsync(Guid bidId, CreateTenderBidLotDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId);
            if (bid == null)
                throw new InvalidOperationException($"Bid with ID {bidId} not found");

            var bidLot = new TenderBidLot
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderBidId = bidId,
                LotId = dto.LotId,
                TotalLotAmount = 0, // Will be calculated from items
                Currency = bid.Currency,
                DeliveryDays = dto.DeliveryDays,
                PaymentTerms = dto.PaymentTerms,
                WarrantyTerms = dto.WarrantyTerms,
                TechnicalProposal = dto.TechnicalProposal,
                CommercialProposal = dto.CommercialProposal,
                Notes = dto.Notes,
                Status = "Draft"
            };

            await _bidLotRepository.CreateAsync(bidLot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created bid lot for bid {BidId}, lot {LotId}", bidId, dto.LotId);

            return MapToBidLotDto(bidLot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bid lot for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<TenderBidLotDto> UpdateBidLotAsync(Guid bidLotId, UpdateTenderBidLotDto dto)
    {
        try
        {
            var bidLot = await _bidLotRepository.GetByIdAsync(bidLotId);
            if (bidLot == null)
                throw new InvalidOperationException($"Bid lot with ID {bidLotId} not found");

            bidLot.DeliveryDays = dto.DeliveryDays ?? bidLot.DeliveryDays;
            bidLot.PaymentTerms = dto.PaymentTerms ?? bidLot.PaymentTerms;
            bidLot.WarrantyTerms = dto.WarrantyTerms ?? bidLot.WarrantyTerms;
            bidLot.TechnicalProposal = dto.TechnicalProposal ?? bidLot.TechnicalProposal;
            bidLot.CommercialProposal = dto.CommercialProposal ?? bidLot.CommercialProposal;
            bidLot.Notes = dto.Notes ?? bidLot.Notes;

            await _bidLotRepository.UpdateAsync(bidLot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated bid lot {BidLotId}", bidLotId);

            return MapToBidLotDto(bidLot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid lot {BidLotId}", bidLotId);
            throw;
        }
    }

    public async Task DeleteBidLotAsync(Guid bidLotId)
    {
        try
        {
            var bidLot = await _bidLotRepository.GetByIdWithItemsAsync(bidLotId);
            if (bidLot == null)
                throw new InvalidOperationException($"Bid lot with ID {bidLotId} not found");

            // Remove lot assignment from bid items
            if (bidLot.Items != null)
            {
                foreach (var item in bidLot.Items)
                {
                    item.BidLotId = null;
                    await _bidItemRepository.UpdateAsync(item);
                }
            }

            await _bidLotRepository.DeleteAsync(bidLotId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted bid lot {BidLotId}", bidLotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid lot {BidLotId}", bidLotId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidLotDto>> GetBidLotsAsync(Guid bidId)
    {
        try
        {
            var bidLots = await _bidLotRepository.GetByBidIdAsync(bidId);
            return bidLots.Select(MapToBidLotDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid lots for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<TenderBidLotDto?> GetBidLotByIdAsync(Guid bidLotId)
    {
        try
        {
            var bidLot = await _bidLotRepository.GetByIdWithItemsAsync(bidLotId);
            return bidLot != null ? MapToBidLotDto(bidLot) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid lot {BidLotId}", bidLotId);
            throw;
        }
    }

    public async Task AssignBidItemToLotAsync(Guid bidItemId, Guid bidLotId)
    {
        try
        {
            var bidItem = await _bidItemRepository.GetByIdAsync(bidItemId);
            if (bidItem == null)
                throw new InvalidOperationException($"Bid item with ID {bidItemId} not found");

            var bidLot = await _bidLotRepository.GetByIdAsync(bidLotId);
            if (bidLot == null)
                throw new InvalidOperationException($"Bid lot with ID {bidLotId} not found");

            if (bidItem.TenderBidId != bidLot.TenderBidId)
                throw new InvalidOperationException("Bid item and bid lot must belong to the same bid");

            bidItem.BidLotId = bidLotId;
            await _bidItemRepository.UpdateAsync(bidItem);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Assigned bid item {BidItemId} to bid lot {BidLotId}", bidItemId, bidLotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning bid item {BidItemId} to bid lot {BidLotId}", bidItemId, bidLotId);
            throw;
        }
    }

    public async Task RemoveBidItemFromLotAsync(Guid bidItemId)
    {
        try
        {
            var bidItem = await _bidItemRepository.GetByIdAsync(bidItemId);
            if (bidItem == null)
                throw new InvalidOperationException($"Bid item with ID {bidItemId} not found");

            bidItem.BidLotId = null;
            await _bidItemRepository.UpdateAsync(bidItem);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed bid item {BidItemId} from lot", bidItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing bid item {BidItemId} from lot", bidItemId);
            throw;
        }
    }

    private static TenderBidLotDto MapToBidLotDto(TenderBidLot bidLot)
    {
        return new TenderBidLotDto
        {
            Id = bidLot.Id,
            TenderBidId = bidLot.TenderBidId,
            LotId = bidLot.LotId,
            LotCode = bidLot.Lot?.LotCode ?? string.Empty,
            LotTitle = bidLot.Lot?.Title ?? string.Empty,
            TotalLotAmount = bidLot.TotalLotAmount,
            Currency = bidLot.Currency,
            DeliveryDays = bidLot.DeliveryDays,
            PaymentTerms = bidLot.PaymentTerms,
            WarrantyTerms = bidLot.WarrantyTerms,
            TechnicalProposal = bidLot.TechnicalProposal,
            CommercialProposal = bidLot.CommercialProposal,
            Status = bidLot.Status,
            PriceScore = bidLot.PriceScore,
            QualityScore = bidLot.QualityScore,
            DeliveryScore = bidLot.DeliveryScore,
            TotalScore = bidLot.TotalScore,
            Rank = bidLot.Rank,
            EvaluationNotes = bidLot.EvaluationNotes,
            Notes = bidLot.Notes,
            ItemCount = bidLot.Items?.Count ?? 0,
            Items = bidLot.Items?.Select(i => new TenderBidItemDto
            {
                Id = i.Id,
                TenderBidId = i.TenderBidId,
                BidLotId = i.BidLotId,
                TenderItemId = i.TenderItemId,
                TenderItemDescription = i.TenderItem?.Description ?? string.Empty,
                RequestedQuantity = i.TenderItem?.Quantity ?? 0,
                OfferedQuantity = i.OfferedQuantity,
                UnitOfMeasure = i.TenderItem?.UnitOfMeasure,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                DeliveryDays = i.DeliveryDays,
                Specifications = i.Specifications,
                Brand = i.Brand,
                Model = i.Model,
                TechnicalDetails = i.TechnicalDetails
            }).ToList() ?? new List<TenderBidItemDto>()
        };
    }

    #endregion
}
