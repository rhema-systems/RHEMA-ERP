using ErpSystem.Shared;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class BusinessPartnerRegistrationService : IBusinessPartnerRegistrationService
{
    private readonly IBusinessPartnerRegistrationRepository _registrationRepository;
    private readonly IBusinessPartnerRegistrationDocumentRepository _documentRepository;
    private readonly IBusinessPartnerRegistrationStatusHistoryRepository _statusHistoryRepository;
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IBusinessPartnerContactRepository _contactRepository;
    private readonly IBusinessPartnerFinancialRepository _financialRepository;
    private readonly IBusinessPartnerDocumentRepository _partnerDocumentRepository;
    private readonly IBusinessPartnerLicenseRepository _licenseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<BusinessPartnerRegistrationService> _logger;
    private readonly IEmailService? _emailService;
    private readonly INotificationService? _notificationService;
    private readonly IAppEventBus _appEventBus;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSupplierEvidencePackService? _evidencePackService;
    private readonly IProcurementSupplierOnboardingTokenService? _onboardingTokenService;

    public BusinessPartnerRegistrationService(
        IBusinessPartnerRegistrationRepository registrationRepository,
        IBusinessPartnerRegistrationDocumentRepository documentRepository,
        IBusinessPartnerRegistrationStatusHistoryRepository statusHistoryRepository,
        IBusinessPartnerRepository partnerRepository,
        IBusinessPartnerContactRepository contactRepository,
        IBusinessPartnerFinancialRepository financialRepository,
        IBusinessPartnerDocumentRepository partnerDocumentRepository,
        IBusinessPartnerLicenseRepository licenseRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IAppEventBus appEventBus,
        IProcurementAccessControlService accessControl,
        ILogger<BusinessPartnerRegistrationService> logger,
        IEmailService? emailService = null,
        INotificationService? notificationService = null,
        IProcurementSupplierEvidencePackService? evidencePackService = null,
        IProcurementSupplierOnboardingTokenService? onboardingTokenService = null)
    {
        _registrationRepository = registrationRepository;
        _documentRepository = documentRepository;
        _statusHistoryRepository = statusHistoryRepository;
        _partnerRepository = partnerRepository;
        _contactRepository = contactRepository;
        _financialRepository = financialRepository;
        _partnerDocumentRepository = partnerDocumentRepository;
        _licenseRepository = licenseRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
        _accessControl = accessControl;
        _logger = logger;
        _emailService = emailService;
        _notificationService = notificationService;
        _evidencePackService = evidencePackService;
        _onboardingTokenService = onboardingTokenService;
    }

    public async Task<BusinessPartnerRegistrationDetailDto?> GetByIdAsync(Guid id)
    {
        var registration = await _registrationRepository.GetWithDocumentsAsync(id);
        if (registration == null)
        {
            return null;
        }

        var statusHistory = await _statusHistoryRepository.GetHistoryByRegistrationAsync(id);
        var result = MapToDetailDto(registration, statusHistory);
        await PopulateEvidenceReadinessAsync(result, registration);
        return result;
    }

    public async Task<BusinessPartnerRegistrationDetailDto?> GetByApplicationNumberAsync(string applicationNumber)
    {
        var registration = await _registrationRepository.GetByApplicationNumberAsync(applicationNumber);
        if (registration == null)
        {
            return null;
        }

        var statusHistory = await _statusHistoryRepository.GetHistoryByRegistrationAsync(registration.Id);
        var result = MapToDetailDto(registration, statusHistory);
        await PopulateEvidenceReadinessAsync(result, registration);
        return result;
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetAllAsync()
    {
        var registrations = await _registrationRepository.GetAllAsync();
        return registrations.Select(MapToDto);
    }

    public async Task<BusinessPartnerRegistrationDetailDto> CreateAsync(CreateBusinessPartnerRegistrationDto dto, Guid userId)
    {
        _logger.LogInformation("Creating business partner registration for user {UserId}, Company: {CompanyName}, PartnerType: {PartnerType}",
            userId, dto.CompanyName, dto.PartnerType);

        var applicationNumber = await _registrationRepository.GenerateApplicationNumberAsync();

        var registration = new Entities.Procurement.BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUserProvider.TenantId, // Use current user's tenant
            RegistrationNumber = applicationNumber,
            ApplicantName = dto.CompanyName,
            ApplicantEmail = dto.Email,
            ApplicantPhone = dto.Phone,
            PartnerType = dto.PartnerType,
            RegistrationCategory = dto.RegistrationCategory,
            Status = "Draft",
            RegistrationDataJson = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId,  // Explicitly set CreatedById
            CreatedBy = _currentUserProvider.Username ?? "External User"
        };

        _logger.LogInformation("Registration entity created with Id: {RegistrationId}, CreatedById: {CreatedById}, TenantId: {TenantId}",
            registration.Id, registration.CreatedById, registration.TenantId);

        var created = await _registrationRepository.CreateAsync(registration);

        // Save the registration first to ensure it exists in the database
        await _unitOfWork.SaveChangesAsync();

        // Create initial status history
        await _statusHistoryRepository.CreateAsync(new Entities.Procurement.BusinessPartnerRegistrationStatusHistory
        {
            Id = Guid.NewGuid(),
            RegistrationId = created.Id,
            FromStatus = null,
            ToStatus = "Draft",
            ChangedById = userId,
            ChangedAt = DateTime.UtcNow,
            Notes = "Registration created",
            CreatedAt = DateTime.UtcNow
        });

        // Commit status history
        await _unitOfWork.SaveChangesAsync();

        // Publish event for admin-configurable notification topics (best-effort).
        try
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = created.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Created",
                Audience = "Internal",
                EntityId = created.Id,
                TriggeredByUserId = userId,
                Data = new Dictionary<string, object>
                {
                    ["RegistrationId"] = created.Id,
                    ["RegistrationNumber"] = created.RegistrationNumber ?? string.Empty,
                    ["ApplicantName"] = created.ApplicantName ?? string.Empty,
                    ["ApplicantEmail"] = created.ApplicantEmail ?? string.Empty,
                    ["PartnerType"] = created.PartnerType ?? string.Empty,
                    ["Status"] = created.Status ?? string.Empty,
                    ["CreatedByUserId"] = userId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish BusinessPartner.Created entity activity event for registration {RegistrationId}", created.Id);
        }

        return MapToDetailDto(created, new List<Entities.Procurement.BusinessPartnerRegistrationStatusHistory>());
    }

    public async Task<BusinessPartnerRegistrationDetailDto> UpdateAsync(Guid id, UpdateBusinessPartnerRegistrationDto dto, Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
        {
            _logger.LogWarning("Attempted to update non-existent registration {RegistrationId} by user {UserId}", id, userId);
            throw new InvalidOperationException($"Registration with ID {id} not found");
        }

        // External users can update only an owned legacy draft or the exact
        // application bound to their restricted applicant-token claim.
        if (_currentUserProvider.IsExternalUser)
        {
            var ownsRegistration =
                registration.CreatedById.HasValue &&
                registration.CreatedById.Value == userId;
            if (!ownsRegistration &&
                !await HasRestrictedApplicantAccessAsync(registration, userId))
            {
                _logger.LogWarning(
                    "External user {UserId} attempted to update registration {RegistrationId} owned by {OwnerId}",
                    userId, id, registration.CreatedById);
                throw new InvalidOperationException(
                    "You do not have permission to update this registration");
            }
        }
        else if (registration.CreatedById.HasValue &&
                 registration.CreatedById.Value != userId)
        {
            await EnsureInternalCapabilityAsync(
                "procurement.supplier.manage",
                registration,
                userId,
                $"supplier-registration-update-{registration.Id:N}");
        }

        if (registration.Status != "Draft" && registration.Status != "MoreInfoRequired")
        {
            throw new InvalidOperationException($"Cannot update registration in {registration.Status} status");
        }

        _logger.LogInformation("Updating registration {RegistrationId} for user {UserId}", id, userId);

        if (registration.RegistrationCategory != dto.RegistrationCategory)
        {
            var isBound = await _unitOfWork
                .Repository<Entities.Procurement.ProcurementSupplierRegistrationEvidencePackBinding>()
                .GetQueryable(item => item.TenantId == registration.TenantId &&
                    item.RegistrationId == registration.Id && !item.IsDeleted)
                .AnyAsync();
            if (isBound)
            {
                throw new InvalidOperationException(
                    "The registration category cannot change after the evidence pack is bound.");
            }
        }

        registration.ApplicantName = dto.CompanyName;
        registration.ApplicantEmail = dto.Email;
        registration.ApplicantPhone = dto.Phone;
        registration.RegistrationCategory = dto.RegistrationCategory;
        registration.RegistrationDataJson = NormalizeRegistrationDataJson(dto.RegistrationData);
        registration.UpdatedAt = DateTime.UtcNow;

        var updated = await _registrationRepository.UpdateAsync(registration);

        // Commit changes
        await _unitOfWork.SaveChangesAsync();

        var statusHistory = await _statusHistoryRepository.GetHistoryByRegistrationAsync(id);
        var result = MapToDetailDto(updated, statusHistory);
        await PopulateEvidenceReadinessAsync(result, updated);
        return result;
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Registration with ID {id} not found");
        if (registration.Status != "Draft")
        {
            throw new InvalidOperationException($"Cannot delete registration in {registration.Status} status");
        }

        await _registrationRepository.DeleteAsync(id);
    }
    public async Task<DTOs.Common.PagedResult<BusinessPartnerRegistrationDto>> GetRegistrationsAsync(int page, int pageSize, string? search = null, string? status = null, string? partnerType = null, DateTime? submittedFrom = null, DateTime? submittedTo = null)
    {
        var result = await _registrationRepository.GetRegistrationsAsync(page, pageSize, search, status, partnerType, submittedFrom, submittedTo);

        return new DTOs.Common.PagedResult<BusinessPartnerRegistrationDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetMyRegistrationsAsync(Guid userId)
    {
        _logger.LogInformation("Fetching registrations for user {UserId}", userId);

        var registrations = await _registrationRepository.GetRegistrationsByUserAsync(userId);
        var dtos = registrations.Select(MapToDto).ToList();

        _logger.LogInformation("User {UserId} has {Count} registrations: {RegistrationIds}",
            userId, dtos.Count, string.Join(", ", dtos.Select(r => $"{r.Id}({r.Status})")));

        if (dtos.Count == 0)
        {
            _logger.LogWarning(
                "No owned or active business-partner-linked registrations were found for user {UserId}.",
                userId);
        }

        return dtos;
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetAllRegistrationsForDebugAsync()
    {
        _logger.LogInformation("DEBUG: Fetching ALL registrations (ignoring filters)");
        var allRegistrations = await _registrationRepository.GetAllAsync();
        var dtos = allRegistrations.Select(MapToDto).ToList();
        _logger.LogInformation("DEBUG: Found {Count} total registrations", dtos.Count);
        return dtos;
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetPendingRegistrationsAsync()
    {
        var registrations = await _registrationRepository.GetRegistrationsByStatusAsync("Submitted");
        return registrations.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetPendingReviewRegistrationsAsync()
    {
        var registrations = await _registrationRepository.GetPendingReviewRegistrationsAsync();
        return registrations.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetUnderReviewRegistrationsAsync()
    {
        var registrations = await _registrationRepository.GetRegistrationsByStatusAsync("UnderReview");
        return registrations.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetApprovedRegistrationsAsync()
    {
        var registrations = await _registrationRepository.GetApprovedRegistrationsAsync();
        return registrations.Select(MapToDto);
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetRejectedRegistrationsAsync()
    {
        var registrations = await _registrationRepository.GetRejectedRegistrationsAsync();
        return registrations.Select(MapToDto);
    }
    public Task SubmitForReviewAsync(Guid id, Guid userId) =>
        SubmitForReviewCoreAsync(id, userId, userId);

    public Task SubmitExternalApplicantForReviewAsync(Guid id, Guid applicantActorId) =>
        SubmitForReviewCoreAsync(id, applicantActorId, changedByUserId: null);

    private async Task SubmitForReviewCoreAsync(
        Guid id,
        Guid actionActorId,
        Guid? changedByUserId)
    {
        var registration = await _registrationRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Registration with ID {id} not found");
        if (registration.Status != "Draft" && registration.Status != "MoreInfoRequired")
        {
            throw new InvalidOperationException($"Cannot submit registration in {registration.Status} status");
        }

        // Validate required fields before submission
        var validationErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(registration.ApplicantEmail) &&
            string.IsNullOrWhiteSpace(registration.ApplicantPhone))
        {
            validationErrors.Add("A verified email address or phone number is required for submission");
        }
        else if (!string.IsNullOrWhiteSpace(registration.ApplicantEmail) &&
                 !IsValidEmail(registration.ApplicantEmail))
        {
            validationErrors.Add("Please provide a valid email address");
        }

        // Parse registration data to check for additional required fields
        if (!string.IsNullOrEmpty(registration.RegistrationDataJson))
        {
            try
            {
                var registrationData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(registration.RegistrationDataJson);

                // Add more validation based on partner type if needed
                // Example: Check for required documents, licenses, etc.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse registration data for validation");
            }
        }

        if (validationErrors.Any())
        {
            throw new InvalidOperationException($"Cannot submit incomplete registration: {string.Join(", ", validationErrors)}");
        }

        if (_evidencePackService is null)
        {
            throw new InvalidOperationException(
                "Supplier evidence-pack validation is unavailable. The registration cannot be submitted.");
        }

        await _evidencePackService.BindAndValidateRegistrationAsync(
            id,
            actionActorId,
            $"supplier-registration-submit-{id:N}",
            CancellationToken.None);

        await _registrationRepository.UpdateStatusAsync(
            id, "Submitted", changedByUserId, "Submitted by verified supplier applicant");

        // Publish events for admin-configurable notification topics (best-effort).
        try
        {
            var data = new Dictionary<string, object>
            {
                ["RegistrationId"] = registration.Id,
                ["RegistrationNumber"] = registration.RegistrationNumber ?? string.Empty,
                ["ApplicantName"] = registration.ApplicantName ?? string.Empty,
                ["ApplicantEmail"] = registration.ApplicantEmail ?? string.Empty,
                ["PartnerType"] = registration.PartnerType ?? string.Empty,
                ["Status"] = "Submitted",
                ["CreatedByUserId"] = registration.CreatedById ?? Guid.Empty
            };

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = registration.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Submitted",
                Audience = "Supplier",
                EntityId = registration.Id,
                TriggeredByUserId = changedByUserId,
                Data = data
            });

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = registration.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Submitted",
                Audience = "Internal",
                EntityId = registration.Id,
                TriggeredByUserId = changedByUserId,
                Data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish BusinessPartner.Submitted entity activity event for registration {RegistrationId}", id);
        }

        // Send in-app notification to the applicant
        if (_notificationService != null && changedByUserId.HasValue &&
            registration.CreatedById.HasValue)
        {
            try
            {
                var notificationDto = new CreateNotificationDto
                {
                    RecipientId = registration.CreatedById.Value,
                    Type = "InApp",
                    Title = "Registration Submitted Successfully",
                    Message = $"Your business partner registration ({registration.RegistrationNumber}) has been submitted for review. You will be notified once the review is complete.",
                    Priority = "Normal",
                    EntityType = "BusinessPartnerRegistration",
                    EntityId = id,
                    ActionUrl = $"/register/business-partner/status/{id}"
                };
                await _notificationService.CreateNotificationAsync(
                    notificationDto, changedByUserId.Value, registration.TenantId);
                _logger.LogInformation("Submission notification sent to user {UserId} for registration {RegistrationId}",
                    registration.CreatedById.Value, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send submission notification for registration {RegistrationId}", id);
                // Don't throw - notification failure shouldn't prevent submission
            }
        }

        // External applicant identities are not ERP Users, so do not create an
        // in-app notification for them. Email delivery remains independent.
        if (_notificationService != null &&
            !string.IsNullOrEmpty(registration.ApplicantEmail))
        {
            try
            {
                var emailSubject = "Business Partner Registration Submitted";
                var emailBody = GenerateRegistrationSubmittedEmailBody(
                    registration.ApplicantName, registration.RegistrationNumber);
                await _notificationService.SendEmailAsync(
                    registration.ApplicantEmail,
                    emailSubject,
                    emailBody,
                    isHtml: true);

                _logger.LogInformation(
                    "Registration submitted email sent for application {ApplicationNumber}",
                    registration.RegistrationNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create notifications for registration {ApplicationNumber}", registration.RegistrationNumber);
                // Don't throw - notification failure shouldn't prevent registration submission
            }
        }
    }

    public async Task ReviewRegistrationAsync(ReviewBusinessPartnerRegistrationDto dto, Guid reviewedById)
    {
        var registration = await _registrationRepository.GetByIdAsync(dto.RegistrationId) ?? throw new InvalidOperationException($"Registration with ID {dto.RegistrationId} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.review", registration, reviewedById,
            $"supplier-registration-review-{dto.RegistrationId:N}");
        if (registration.Status != "Submitted")
        {
            throw new InvalidOperationException($"Cannot review registration in {registration.Status} status");
        }

        await _registrationRepository.UpdateStatusAsync(dto.RegistrationId, "UnderReview", reviewedById, dto.ReviewNotes ?? dto.Notes);
    }

    public async Task ApproveRegistrationAsync(Guid id, Guid approvedById, string? notes = null)
    {
        BusinessPartnerDetailDto? businessPartner = null;
        Entities.Procurement.BusinessPartnerRegistration? registration = null;

        // Supplier creation, copied evidence, category assignment and the terminal
        // application state are one unit. Some legacy repositories flush while
        // creating children, so the caller-owned transaction is essential: a
        // later failure must not leave an orphan supplier behind.
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    $"supplier-registration-approval:{id:N}");

                registration = await _registrationRepository.GetByIdAsync(id)
                    ?? throw new InvalidOperationException($"Registration with ID {id} not found");
                await EnsureInternalCapabilityAsync(
                    "procurement.supplier.approve", registration, approvedById,
                    $"supplier-registration-approve-{id:N}");

                if (registration.Status == "Approved" && registration.BusinessPartnerId.HasValue)
                {
                    var existingRegistrationData = ParseRegistrationDataJson(
                        registration.RegistrationDataJson);
                    var existingRegistrationBankAccounts = BuildRegistrationBankAccounts(
                        existingRegistrationData);
                    await EnsureRegistrationContactsAsync(
                        registration,
                        registration.BusinessPartnerId.Value,
                        approvedById,
                        BuildRegistrationContacts(registration, existingRegistrationData));
                    await EnsureRegistrationBankAccountsAsync(
                        registration,
                        registration.BusinessPartnerId.Value,
                        approvedById,
                        existingRegistrationBankAccounts);
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation(
                        "Registration {RegistrationId} is already approved as business partner {BusinessPartnerId}; contact and bank-account reconciliation completed and the repeated approval is otherwise complete",
                        id,
                        registration.BusinessPartnerId);
                    await _unitOfWork.CommitAsync();
                    return;
                }

                if (registration.Status != "Submitted" && registration.Status != "UnderReview")
                {
                    throw new InvalidOperationException($"Cannot approve registration in {registration.Status} status");
                }

                var documents = await _documentRepository.GetByRegistrationIdAsync(id);
                var uploadedDocuments = documents.Where(d => !d.IsDeleted).ToList();
                var unverifiedDocuments = uploadedDocuments
                    .Where(d => !d.IsVerified && !d.IsRejected).ToList();
                if (unverifiedDocuments.Any())
                {
                    var unverifiedDocNames = string.Join(", ", unverifiedDocuments.Select(d => d.DocumentName));
                    throw new InvalidOperationException($"Cannot approve registration. The following documents must be verified or rejected first: {unverifiedDocNames}");
                }

                var rejectedDocuments = uploadedDocuments.Where(d => d.IsRejected).ToList();
                if (rejectedDocuments.Any())
                {
                    var rejectedDocNames = string.Join(", ", rejectedDocuments.Select(d => d.DocumentName));
                    throw new InvalidOperationException($"Cannot approve registration. The following documents have been rejected: {rejectedDocNames}. Please request the applicant to re-upload these documents.");
                }

                _logger.LogInformation(
                    "Starting approval process for registration {RegistrationId} by user {UserId}",
                    id,
                    approvedById);

                businessPartner = await ConvertToBusinessPartnerAsync(id, approvedById);
                var oldStatus = registration.Status;
                registration.Status = "Approved";
                registration.ApprovedDate = DateTime.UtcNow;
                registration.ApprovedById = approvedById;
                registration.BusinessPartnerId = businessPartner.Id;
                registration.UpdatedAt = DateTime.UtcNow;

                // The registration was loaded tracked. Calling Update on the root
                // recursively marked newly added category/document children as
                // existing rows and caused the zero-row concurrency exception.
                var history = new Entities.Procurement.BusinessPartnerRegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = id,
                    FromStatus = oldStatus,
                    ToStatus = "Approved",
                    ChangedById = approvedById,
                    ChangedAt = DateTime.UtcNow,
                    Notes = notes ?? "Registration approved and converted to business partner",
                    CreatedAt = DateTime.UtcNow
                };
                await _statusHistoryRepository.CreateAsync(history);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                try
                {
                    await _unitOfWork.RollbackAsync();
                }
                catch (InvalidOperationException)
                {
                    // Commit/Save may already have released the transaction.
                }
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        });

        // A repeated request that observed an already-completed approval has no
        // further work and must not publish duplicate events or notifications.
        if (businessPartner is null)
            return;

        _logger.LogInformation(
            "Business partner {PartnerCode} and registration {RegistrationId} approved atomically",
            businessPartner.PartnerCode,
            id);

        // Token expiry remains independently retry-safe after the approval commit.
        if (_onboardingTokenService != null)
        {
            await _onboardingTokenService.ExpireForTerminalRegistrationAsync(
                id,
                "Approved",
                approvedById,
                $"registration-approved-{id:N}");
        }

        // Publish events for admin-configurable notification topics (best-effort).
        try
        {
            var data = new Dictionary<string, object>
            {
                ["RegistrationId"] = registration.Id,
                ["RegistrationNumber"] = registration.RegistrationNumber ?? string.Empty,
                ["ApplicantName"] = registration.ApplicantName ?? string.Empty,
                ["ApplicantEmail"] = registration.ApplicantEmail ?? string.Empty,
                ["PartnerType"] = registration.PartnerType ?? string.Empty,
                ["Status"] = registration.Status ?? string.Empty,
                ["BusinessPartnerId"] = businessPartner.Id,
                ["PartnerCode"] = businessPartner.PartnerCode ?? string.Empty,
                ["ApprovedById"] = approvedById
            };

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = registration.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Approved",
                Audience = "Supplier",
                EntityId = businessPartner.Id,
                TriggeredByUserId = approvedById,
                Data = data
            });

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = registration.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Approved",
                Audience = "Internal",
                EntityId = businessPartner.Id,
                TriggeredByUserId = approvedById,
                Data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish BusinessPartner.Approved entity activity event for registration {RegistrationId}", id);
        }

        // Send in-app notification to the applicant
        if (_notificationService != null && registration.CreatedById.HasValue)
        {
            try
            {
                var notificationDto = new CreateNotificationDto
                {
                    RecipientId = registration.CreatedById.Value,
                    Type = "InApp",
                    Title = "Registration Approved",
                    Message = $"Your business partner registration ({registration.RegistrationNumber}) has been approved and converted to partner code: {businessPartner.PartnerCode}",
                    Priority = "High",
                    EntityType = "BusinessPartnerRegistration",
                    EntityId = id,
                    ActionUrl = $"/register/business-partner/status/{id}"
                };
                await _notificationService.CreateNotificationAsync(notificationDto, approvedById, registration.TenantId);
                _logger.LogInformation("Approval notification sent to user {UserId} for registration {RegistrationId}",
                    registration.CreatedById.Value, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval notification for registration {RegistrationId}", id);
                // Don't throw - notification failure shouldn't prevent approval
            }
        }

        // Send approval email
        if (_notificationService != null && !string.IsNullOrEmpty(registration.ApplicantEmail) && registration.CreatedById.HasValue)
        {
            try
            {
                var emailSubject = "Business Partner Registration Approved";
                var emailBody = GenerateRegistrationApprovedEmailBody(registration.ApplicantName, businessPartner.PartnerCode);

                await _notificationService.SendEmailAsync(
                    registration.ApplicantEmail,
                    emailSubject,
                    emailBody,
                    isHtml: true
                );

                _logger.LogInformation("Registration approved email sent to {Email} for partner {PartnerCode}",
                    registration.ApplicantEmail, businessPartner.PartnerCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send registration approved email to {Email}", registration.ApplicantEmail);
                // Don't throw - email failure shouldn't prevent approval
            }
        }
    }

    public async Task RejectRegistrationAsync(Guid id, Guid rejectedById, string reason)
    {
        var registration = await _registrationRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Registration with ID {id} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.approve", registration, rejectedById,
            $"supplier-registration-reject-{id:N}");
        if (string.Equals(registration.Status, "Rejected",
                StringComparison.OrdinalIgnoreCase))
        {
            // A prior attempt may have committed the registration status and then
            // failed while closing the token/session boundary. Replaying the same
            // rejection repairs that terminal closure instead of leaving a usable
            // applicant session with no recovery route.
            if (_onboardingTokenService != null)
            {
                await _onboardingTokenService.ExpireForTerminalRegistrationAsync(
                    id,
                    "Rejected",
                    rejectedById,
                    $"registration-rejected-{id:N}");
            }
            return;
        }
        if (registration.Status != "Submitted" && registration.Status != "UnderReview")
        {
            throw new InvalidOperationException($"Cannot reject registration in {registration.Status} status");
        }

        await _registrationRepository.UpdateStatusAsync(id, "Rejected", rejectedById, reason);
        await _unitOfWork.SaveChangesAsync();
        if (_onboardingTokenService != null)
        {
            await _onboardingTokenService.ExpireForTerminalRegistrationAsync(
                id,
                "Rejected",
                rejectedById,
                $"registration-rejected-{id:N}");
        }

        // Publish events for admin-configurable notification topics (best-effort).
        try
        {
            var data = new Dictionary<string, object>
            {
                ["RegistrationId"] = registration.Id,
                ["RegistrationNumber"] = registration.RegistrationNumber ?? string.Empty,
                ["ApplicantName"] = registration.ApplicantName ?? string.Empty,
                ["ApplicantEmail"] = registration.ApplicantEmail ?? string.Empty,
                ["PartnerType"] = registration.PartnerType ?? string.Empty,
                ["Status"] = "Rejected",
                ["RejectedById"] = rejectedById,
                ["Reason"] = reason
            };

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = registration.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Rejected",
                Audience = "Supplier",
                EntityId = registration.Id,
                TriggeredByUserId = rejectedById,
                Data = data
            });

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = registration.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Rejected",
                Audience = "Internal",
                EntityId = registration.Id,
                TriggeredByUserId = rejectedById,
                Data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish BusinessPartner.Rejected entity activity event for registration {RegistrationId}", id);
        }

        // Send in-app notification to the applicant
        if (_notificationService != null && registration.CreatedById.HasValue)
        {
            try
            {
                var notificationDto = new CreateNotificationDto
                {
                    RecipientId = registration.CreatedById.Value,
                    Type = "InApp",
                    Title = "Registration Rejected",
                    Message = $"Your business partner registration ({registration.RegistrationNumber}) has been rejected. Reason: {reason}",
                    Priority = "High",
                    EntityType = "BusinessPartnerRegistration",
                    EntityId = id,
                    ActionUrl = $"/register/business-partner/status/{id}"
                };
                await _notificationService.CreateNotificationAsync(notificationDto, rejectedById, registration.TenantId);
                _logger.LogInformation("Rejection notification sent to user {UserId} for registration {RegistrationId}",
                    registration.CreatedById.Value, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rejection notification for registration {RegistrationId}", id);
                // Don't throw - notification failure shouldn't prevent rejection
            }
        }

        // Queue rejection email via notification system (fire-and-forget)
        if (_notificationService != null && !string.IsNullOrEmpty(registration.ApplicantEmail) && registration.CreatedById.HasValue)
        {
            try
            {
                var emailSubject = "Business Partner Registration Rejected";
                var emailBody = GenerateRegistrationRejectedEmailBody(registration.ApplicantName, reason);

                await _notificationService.SendEmailAsync(
                    registration.ApplicantEmail,
                    emailSubject,
                    emailBody,
                    isHtml: true
                );

                _logger.LogInformation("Registration rejected email sent to {Email} for application {ApplicationNumber}",
                    registration.ApplicantEmail, registration.RegistrationNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send registration rejected email to {Email}", registration.ApplicantEmail);
                // Don't throw - email failure shouldn't prevent rejection
            }
        }
    }

    public async Task RequestMoreInfoAsync(Guid id, Guid requestedById, string notes)
    {
        var registration = await _registrationRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Registration with ID {id} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.review", registration, requestedById,
            $"supplier-registration-more-info-{id:N}");
        if (registration.Status != "Submitted" && registration.Status != "UnderReview")
        {
            throw new InvalidOperationException($"Cannot request more info for registration in {registration.Status} status");
        }

        // Get rejected documents to include in notification
        var documents = await _documentRepository.GetDocumentsByRegistrationAsync(id);
        var rejectedDocuments = documents.Where(d => d.IsRejected).ToList();

        await _registrationRepository.UpdateStatusAsync(id, "MoreInfoRequired", requestedById, notes);

        // Build message with rejected documents
        var message = $"Additional information is required for your business partner registration ({registration.RegistrationNumber}). Please review and update your application.";
        if (rejectedDocuments.Any())
        {
            message += $"\n\nRejected Documents ({rejectedDocuments.Count}):";
            foreach (var doc in rejectedDocuments)
            {
                message += $"\n- {doc.DocumentType}: {doc.DocumentName} - Reason: {doc.RejectionReason}";
            }
        }
        message += $"\n\nReviewer Notes: {notes}";

        // Send in-app notification to the applicant
        if (_notificationService != null && registration.CreatedById.HasValue)
        {
            try
            {
                var notificationDto = new CreateNotificationDto
                {
                    RecipientId = registration.CreatedById.Value,
                    Type = "InApp",
                    Title = "More Information Required",
                    Message = message,
                    Priority = "High",
                    EntityType = "BusinessPartnerRegistration",
                    EntityId = id,
                    ActionUrl = $"/register/business-partner"
                };
                await _notificationService.CreateNotificationAsync(notificationDto, requestedById, registration.TenantId);
                _logger.LogInformation("More info required notification sent to user {UserId} for registration {RegistrationId}",
                    registration.CreatedById.Value, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send more info required notification for registration {RegistrationId}", id);
                // Don't throw - notification failure shouldn't prevent status update
            }
        }

        // Queue email notification via notification system (fire-and-forget)
        if (_notificationService != null && !string.IsNullOrEmpty(registration.ApplicantEmail) && registration.CreatedById.HasValue)
        {
            try
            {
                var emailSubject = "Business Partner Registration - More Information Required";
                var emailBody = GenerateMoreInfoRequiredEmailBody(registration.ApplicantName, registration.RegistrationNumber, notes, rejectedDocuments);

                await _notificationService.SendEmailAsync(
                    registration.ApplicantEmail,
                    emailSubject,
                    emailBody,
                    isHtml: true
                );

                _logger.LogInformation("More info required email sent to {Email} for application {ApplicationNumber}",
                    registration.ApplicantEmail, registration.RegistrationNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send more info required email to {Email}", registration.ApplicantEmail);
                // Don't throw - email failure shouldn't prevent status update
            }
        }
    }
    public async Task<IEnumerable<BusinessPartnerRegistrationDocumentDto>> GetDocumentsAsync(Guid registrationId)
    {
        var documents = await _documentRepository.GetDocumentsByRegistrationAsync(registrationId);
        return documents.Select(d => new BusinessPartnerRegistrationDocumentDto
        {
            Id = d.Id,
            RegistrationId = d.RegistrationId,
            FileUploadRecordId = d.FileUploadRecordId,
            CentralDocumentRecordId = d.CentralDocumentRecordId,
            CentralDocumentVersionId = d.CentralDocumentVersionId,
            VirusScanStatus = d.FileUploadRecord?.VirusScanStatus,
            DocumentType = d.DocumentType,
            DocumentName = d.DocumentName,
            FilePath = string.Empty,
            DocumentPath = null,
            InternalStoragePath = d.FileUploadRecord?.FilePath ?? d.DocumentPath,
            FileSize = d.FileSize ?? 0,
            MimeType = d.MimeType,
            EvidenceRequirementCode = d.EvidenceRequirementCode,
            ClassificationCode = d.ClassificationCode,
            IssuedAtUtc = d.IssuedAtUtc,
            ExpiresAtUtc = d.ExpiresAtUtc,
            ChecksumSha256 = d.ChecksumSha256,
            IsVerified = d.IsVerified,
            IsRejected = d.IsRejected,
            RejectionReason = d.RejectionReason,
            RejectedDate = d.RejectedDate,
            UploadedAt = d.CreatedAt
        });
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDocumentDto>> GetDocumentsForInternalReviewAsync(
        Guid registrationId,
        Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId)
            ?? throw new InvalidOperationException(
                $"Registration with ID {registrationId} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.review",
            registration,
            userId,
            $"supplier-registration-document-list-{registrationId:N}");
        return await GetDocumentsAsync(registrationId);
    }

    public async Task<BusinessPartnerRegistrationDocumentDto> UploadDocumentAsync(Guid registrationId, CreateBusinessPartnerDocumentDto dto, Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId) ?? throw new InvalidOperationException($"Registration with ID {registrationId} not found");
        if (_currentUserProvider.IsExternalUser)
        {
            var ownsRegistration =
                registration.CreatedById.HasValue &&
                registration.CreatedById.Value == userId;
            if (!ownsRegistration &&
                !await HasRestrictedApplicantAccessAsync(registration, userId))
            {
                throw new UnauthorizedAccessException(
                    "You do not have permission to upload evidence for this registration.");
            }
        }
        else
        {
            await EnsureInternalCapabilityAsync(
                "procurement.supplier.review", registration, userId,
                $"supplier-registration-document-upload-{registrationId:N}");
        }
        if (registration.Status is not ("Draft" or "MoreInfoRequired"))
        {
            throw new InvalidOperationException(
                $"Cannot upload registration evidence in {registration.Status} status");
        }
        if (!dto.FileUploadRecordId.HasValue || dto.FileUploadRecordId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Registration evidence must reference a controlled file-upload record.");
        }
        if (!dto.CentralDocumentRecordId.HasValue ||
            dto.CentralDocumentRecordId == Guid.Empty ||
            !dto.CentralDocumentVersionId.HasValue ||
            dto.CentralDocumentVersionId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Registration evidence must reference a central DMS record and version.");
        }
        var fileRecord = await _unitOfWork.Repository<Entities.FileUploadRecord>()
            .GetQueryable(item =>
                item.Id == dto.FileUploadRecordId.Value &&
                item.TenantId == registration.TenantId &&
                !item.IsDeleted)
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "The controlled file-upload record was not found for this tenant.");
        if (fileRecord.UploadedByUserId != userId)
        {
            throw new UnauthorizedAccessException(
                "The controlled file-upload record belongs to a different actor.");
        }
        if (fileRecord.VirusScanStatus != Enums.FileVirusScanStatus.Clean)
        {
            throw new InvalidOperationException(
                "Registration evidence must have a clean virus-scan result.");
        }
        if (fileRecord.FileSize != dto.FileSize)
        {
            throw new InvalidOperationException(
                "Registration evidence metadata does not match the controlled upload.");
        }
        var centralRecord = await _unitOfWork
            .Repository<Entities.DocumentManagement.CentralDocumentRecord>()
            .GetQueryable(item =>
                item.Id == dto.CentralDocumentRecordId.Value &&
                item.TenantId == registration.TenantId &&
                item.SourceRecordId == registration.Id &&
                !item.IsDeleted)
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "The central DMS record was not found for this registration.");
        var centralVersion = await _unitOfWork
            .Repository<Entities.DocumentManagement.CentralDocumentVersion>()
            .GetQueryable(item =>
                item.Id == dto.CentralDocumentVersionId.Value &&
                item.DocumentRecordId == centralRecord.Id &&
                item.TenantId == registration.TenantId &&
                item.FileUploadRecordId == fileRecord.Id &&
                !item.IsDeleted)
            .SingleOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "The central DMS version does not match the controlled upload.");
        var alreadyBound = await _unitOfWork
            .Repository<Entities.Procurement.BusinessPartnerRegistrationDocument>()
            .GetQueryable(item =>
                item.TenantId == registration.TenantId &&
                item.FileUploadRecordId == fileRecord.Id &&
                !item.IsDeleted)
            .AnyAsync();
        if (alreadyBound)
        {
            throw new InvalidOperationException(
                "The controlled upload is already bound to registration evidence.");
        }
        var document = new Entities.Procurement.BusinessPartnerRegistrationDocument
        {
            Id = Guid.NewGuid(),
            TenantId = registration.TenantId, // Set TenantId from registration
            RegistrationId = registrationId,
            FileUploadRecordId = fileRecord.Id,
            CentralDocumentRecordId = centralRecord.Id,
            CentralDocumentVersionId = centralVersion.Id,
            DocumentType = dto.DocumentType,
            DocumentName = dto.DocumentName,
            DocumentPath = $"dms://{centralRecord.Id:N}/{centralVersion.Id:N}",
            FileSize = dto.FileSize,
            MimeType = dto.MimeType,
            EvidenceRequirementCode = dto.EvidenceRequirementCode,
            ClassificationCode = dto.ClassificationCode,
            IssuedAtUtc = dto.IssueDate,
            ExpiresAtUtc = dto.ExpiryDate,
            ChecksumSha256 = dto.ChecksumSha256,
            IsVerified = false,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _documentRepository.CreateAsync(document);
        await _unitOfWork.SaveChangesAsync(); // Ensure document is saved

        return new BusinessPartnerRegistrationDocumentDto
        {
            Id = created.Id,
            RegistrationId = created.RegistrationId,
            FileUploadRecordId = created.FileUploadRecordId,
            CentralDocumentRecordId = created.CentralDocumentRecordId,
            CentralDocumentVersionId = created.CentralDocumentVersionId,
            VirusScanStatus = fileRecord.VirusScanStatus,
            DocumentType = created.DocumentType,
            DocumentName = created.DocumentName,
            FilePath = string.Empty,
            DocumentPath = null,
            InternalStoragePath = fileRecord.FilePath,
            FileSize = created.FileSize ?? 0,
            MimeType = created.MimeType,
            EvidenceRequirementCode = created.EvidenceRequirementCode,
            ClassificationCode = created.ClassificationCode,
            IssuedAtUtc = created.IssuedAtUtc,
            ExpiresAtUtc = created.ExpiresAtUtc,
            ChecksumSha256 = created.ChecksumSha256,
            IsVerified = created.IsVerified,
            IsRejected = created.IsRejected,
            RejectionReason = created.RejectionReason,
            RejectedDate = created.RejectedDate,
            UploadedAt = created.CreatedAt
        };
    }

    public async Task DeleteDocumentAsync(Guid registrationId, Guid documentId, Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId) ??
            throw new InvalidOperationException($"Registration with ID {registrationId} not found");
        if (_currentUserProvider.IsExternalUser)
        {
            var ownsRegistration =
                registration.CreatedById.HasValue &&
                registration.CreatedById.Value == userId;
            if (!ownsRegistration &&
                !await HasRestrictedApplicantAccessAsync(registration, userId))
            {
                throw new UnauthorizedAccessException(
                    "You do not have permission to delete evidence from this registration.");
            }
        }
        else
        {
            await EnsureInternalCapabilityAsync(
                "procurement.supplier.review", registration, userId,
                $"supplier-registration-document-delete-{documentId:N}");
        }
        if (registration.Status is not ("Draft" or "MoreInfoRequired"))
        {
            throw new InvalidOperationException(
                $"Cannot delete registration evidence in {registration.Status} status");
        }
        if (await _unitOfWork
                .Repository<Entities.Procurement.ProcurementSupplierRegistrationEvidencePackBinding>()
                .GetQueryable(item => item.TenantId == registration.TenantId &&
                    item.RegistrationId == registration.Id && !item.IsDeleted)
                .AnyAsync())
        {
            throw new InvalidOperationException(
                "Evidence cannot be deleted after the registration pack is bound.");
        }
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found for registration {registrationId}");
        }

        await _documentRepository.DeleteAsync(documentId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task VerifyDocumentAsync(Guid registrationId, Guid documentId, Guid verifiedById)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found for registration {registrationId}");
        }
        var registration = await _registrationRepository.GetByIdAsync(registrationId) ??
            throw new InvalidOperationException($"Registration with ID {registrationId} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.review", registration, verifiedById,
            $"supplier-registration-document-verify-{documentId:N}");

        await _documentRepository.VerifyDocumentAsync(documentId, verifiedById);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Document {DocumentId} verified by user {UserId} for registration {RegistrationId}",
            documentId, verifiedById, registrationId);
    }

    public async Task RejectDocumentAsync(Guid registrationId, Guid documentId, Guid rejectedById, string reason)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found for registration {registrationId}");
        }
        var registration = await _registrationRepository.GetByIdAsync(registrationId) ??
            throw new InvalidOperationException($"Registration with ID {registrationId} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.review", registration, rejectedById,
            $"supplier-registration-document-reject-{documentId:N}");

        // Mark document as rejected
        document.IsRejected = true;
        document.RejectedById = rejectedById;
        document.RejectedDate = DateTime.UtcNow;
        document.RejectionReason = reason;
        document.IsVerified = false; // Can't be both verified and rejected
        document.UpdatedAt = DateTime.UtcNow;

        // Log the rejection
        _logger.LogInformation("Document {DocumentId} rejected by user {UserId} for registration {RegistrationId}. Reason: {Reason}",
            documentId, rejectedById, registrationId, reason);

        await _documentRepository.UpdateAsync(document);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RevertDocumentRejectionAsync(Guid registrationId, Guid documentId, Guid userId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found for registration {registrationId}");
        }
        var registration = await _registrationRepository.GetByIdAsync(registrationId) ??
            throw new InvalidOperationException($"Registration with ID {registrationId} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.review", registration, userId,
            $"supplier-registration-document-revert-{documentId:N}");

        // Revert rejection
        document.IsRejected = false;
        document.RejectedById = null;
        document.RejectedDate = null;
        document.RejectionReason = null;
        document.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(document);
        await _unitOfWork.SaveChangesAsync();

        // Log the reversion
        _logger.LogInformation("Document {DocumentId} rejection reverted by user {UserId} for registration {RegistrationId}",
            documentId, userId, registrationId);

        await _documentRepository.UpdateAsync(document);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<BusinessPartnerRegistrationDocumentDto?> GetDocumentByIdAsync(Guid registrationId, Guid documentId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
        {
            return null;
        }

        return new BusinessPartnerRegistrationDocumentDto
        {
            Id = document.Id,
            RegistrationId = document.RegistrationId,
            FileUploadRecordId = document.FileUploadRecordId,
            CentralDocumentRecordId = document.CentralDocumentRecordId,
            CentralDocumentVersionId = document.CentralDocumentVersionId,
            VirusScanStatus = document.FileUploadRecord?.VirusScanStatus,
            DocumentType = document.DocumentType,
            DocumentName = document.DocumentName,
            FilePath = string.Empty,
            DocumentPath = null,
            InternalStoragePath = document.FileUploadRecord?.FilePath ?? document.DocumentPath,
            FileSize = document.FileSize ?? 0,
            MimeType = document.MimeType,
            EvidenceRequirementCode = document.EvidenceRequirementCode,
            ClassificationCode = document.ClassificationCode,
            IssuedAtUtc = document.IssuedAtUtc,
            ExpiresAtUtc = document.ExpiresAtUtc,
            ChecksumSha256 = document.ChecksumSha256,
            IsVerified = document.IsVerified,
            IsRejected = document.IsRejected,
            RejectionReason = document.RejectionReason,
            RejectedDate = document.RejectedDate,
            UploadedAt = document.CreatedAt
        };
    }

    public async Task<BusinessPartnerRegistrationDocumentDto?> GetDocumentForInternalDownloadAsync(
        Guid registrationId,
        Guid documentId,
        Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId)
            ?? throw new InvalidOperationException(
                $"Registration with ID {registrationId} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.review",
            registration,
            userId,
            $"supplier-registration-document-download-{documentId:N}");
        return await GetDocumentByIdAsync(registrationId, documentId);
    }

    public async Task TrackDocumentDownloadAsync(Guid registrationId, Guid documentId, Guid userId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
        {
            _logger.LogWarning("Document {DocumentId} not found for registration {RegistrationId}", documentId, registrationId);
            return;
        }

        // Log download event with detailed information
        _logger.LogInformation(
            "Document downloaded - DocumentId: {DocumentId}, DocumentName: {DocumentName}, DocumentType: {DocumentType}, " +
            "RegistrationId: {RegistrationId}, UserId: {UserId}, Timestamp: {Timestamp}",
            documentId,
            document.DocumentName,
            document.DocumentType,
            registrationId,
            userId,
            DateTime.UtcNow
        );

        // Note: If you need to store download counts in the database, you can add a DownloadCount field
        // to the BusinessPartnerRegistrationDocument entity and increment it here:
        // document.DownloadCount = (document.DownloadCount ?? 0) + 1;
        // await _documentRepository.UpdateAsync(document);
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationStatusHistoryDto>> GetStatusHistoryAsync(Guid registrationId)
    {
        var history = await _statusHistoryRepository.GetHistoryByRegistrationAsync(registrationId);
        return history.Select(h => new BusinessPartnerRegistrationStatusHistoryDto
        {
            Id = h.Id,
            RegistrationId = h.RegistrationId,
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            ChangedBy = h.ChangedBy?.UserName,
            ChangedAt = h.ChangedAt,
            Notes = h.Notes
        });
    }

    public async Task<BusinessPartnerDetailDto> ConvertToBusinessPartnerAsync(Guid registrationId, Guid approvedById)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId) ?? throw new InvalidOperationException($"Registration with ID {registrationId} not found");
        if (string.IsNullOrEmpty(registration.RegistrationDataJson))
        {
            throw new InvalidOperationException("Registration data is missing");
        }

        // Deserialize registration data - handle nested structure
        CreateBusinessPartnerRegistrationDto? registrationData = null;

        try
        {
            // First try to parse as the outer DTO structure
            var outerData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(registration.RegistrationDataJson);

            // Check if there's a nested "RegistrationData" property
            if (outerData.TryGetProperty("RegistrationData", out var nestedData) && nestedData.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                // Parse the nested JSON string
                var nestedJsonString = nestedData.GetString();
                if (!string.IsNullOrEmpty(nestedJsonString))
                {
                    registrationData = System.Text.Json.JsonSerializer.Deserialize<CreateBusinessPartnerRegistrationDto>(nestedJsonString);
                }
            }
            else if (outerData.TryGetProperty("registrationData", out nestedData) && nestedData.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                // Try camelCase version
                var nestedJsonString = nestedData.GetString();
                if (!string.IsNullOrEmpty(nestedJsonString))
                {
                    registrationData = System.Text.Json.JsonSerializer.Deserialize<CreateBusinessPartnerRegistrationDto>(nestedJsonString);
                }
            }
            else
            {
                // No nested structure, try to deserialize directly
                registrationData = System.Text.Json.JsonSerializer.Deserialize<CreateBusinessPartnerRegistrationDto>(registration.RegistrationDataJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize registration data for registration {RegistrationId}", registrationId);
            throw new InvalidOperationException("Failed to deserialize registration data", ex);
        }

        if (registrationData == null)
        {
            throw new InvalidOperationException("Failed to deserialize registration data");
        }

        // Generate partner code
        var partnerCode = await _partnerRepository.GeneratePartnerCodeAsync(registration.PartnerType);

        // Parse the nested RegistrationData JSON to get additional fields
        var additionalData = ParseRegistrationDataJson(registration.RegistrationDataJson);
        var registrationContacts = BuildRegistrationContacts(registration, additionalData);
        var primaryRegistrationContact = registrationContacts.FirstOrDefault(contact => contact.IsPrimary);
        var registrationBankAccounts = BuildRegistrationBankAccounts(additionalData);
        var primaryRegistrationBankAccount = registrationBankAccounts
            .FirstOrDefault(account => account.IsPrimary);

        _logger.LogInformation(
            "Creating business partner for registration {RegistrationId} with {ContactCount} contacts and {BankAccountCount} bank accounts",
            registrationId,
            registrationContacts.Count,
            registrationBankAccounts.Count);

        // Create business partner - use additionalData which has all fields properly parsed
        var partner = new Entities.Procurement.BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = registration.TenantId, // Set tenant from registration
            PartnerCode = partnerCode,
            PartnerName = additionalData.CompanyName ?? string.Empty,
            PartnerType = registration.PartnerType,
            LegalName = additionalData.CompanyName,
            BusinessRegistrationNumber = additionalData.RegistrationNumber,
            TaxIdentificationNumber = additionalData.TaxNumber,
            VATNumber = additionalData.VatNumber,
            PrimaryEmail = additionalData.Email,
            PrimaryPhone = additionalData.Phone,
            SecondaryPhone = additionalData.AlternatePhone,
            Website = additionalData.Website,
            PhysicalAddress = additionalData.PhysicalAddress,
            PhysicalCity = additionalData.City,
            PhysicalCountry = additionalData.Country,
            PhysicalPostalCode = additionalData.PostalCode,
            IndustryClassification = additionalData.IndustryType,
            AnnualTurnover = additionalData.AnnualRevenue,
            // Banking Information
            BankName = primaryRegistrationBankAccount?.BankName,
            BankAccountName = primaryRegistrationBankAccount?.AccountName,
            BankAccountNumber = primaryRegistrationBankAccount?.AccountNumber,
            BankBranch = primaryRegistrationBankAccount?.BranchName,
            BankSwiftCode = primaryRegistrationBankAccount?.SwiftCode,
            BankIBAN = primaryRegistrationBankAccount?.Iban,
            Currency = primaryRegistrationBankAccount?.Currency,
            // Primary Contact Information (from contact person)
            PrimaryContactName = primaryRegistrationContact?.ContactName,
            PrimaryContactTitle = primaryRegistrationContact?.ContactTitle,
            // The registration creator is audit provenance, not necessarily an
            // ApplicationUser. Token-gated applications are created by an
            // applicant-session subject, so copying CreatedById into this foreign
            // key can reference a non-existent Users row. Credential provisioning
            // links the approved supplier account after that account is created.
            UserId = null,
            // Operational status used across internal UIs and downstream docs.
            RegistrationStatus = BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus,
            ApprovalStatus = BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus,
            IsPreferred = false,
            IsBlacklisted = false,
            IsActive = true,
            ApprovedById = approvedById,
            ApprovedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedById = approvedById
        };

        foreach (var registrationBankAccount in registrationBankAccounts)
        {
            partner.BankAccounts.Add(new Entities.Procurement.BusinessPartnerBankAccount
            {
                Id = Guid.NewGuid(),
                TenantId = registration.TenantId,
                BusinessPartnerId = partner.Id,
                BusinessPartner = partner,
                BankName = registrationBankAccount.BankName,
                BranchName = registrationBankAccount.BranchName,
                AccountName = registrationBankAccount.AccountName,
                AccountNumber = registrationBankAccount.AccountNumber,
                SwiftCode = registrationBankAccount.SwiftCode,
                Iban = registrationBankAccount.Iban,
                Currency = registrationBankAccount.Currency,
                IsPrimary = registrationBankAccount.IsPrimary,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedById = approvedById
            });
        }

        // The public registration category is the supplier's initial controlled
        // procurement classification. Keep this in the same approval unit of
        // work so an approved supplier can never be created without the category
        // later required by PR/RFQ/PO eligibility checks.
        Entities.Procurement.BusinessPartnerCategory? categoryAssignment = null;
        if (registration.RegistrationCategory.HasValue)
        {
            var categoryCode = ProcurementSupplierCategoryRegistry.CodeFor(
                registration.RegistrationCategory.Value);
            var category = await _unitOfWork.Repository<Entities.Procurement.PartnerCategory>()
                .FirstOrDefaultAsync(item =>
                    item.TenantId == registration.TenantId &&
                    item.CategoryCode == categoryCode &&
                    item.IsActive &&
                    !item.IsDeleted)
                ?? throw new InvalidOperationException(
                    $"The supplier registration category {categoryCode} is not configured for this tenant. Run the procurement baseline setup before approving the application.");

            categoryAssignment = new Entities.Procurement.BusinessPartnerCategory
            {
                Id = Guid.NewGuid(),
                BusinessPartnerId = partner.Id,
                CategoryId = category.Id,
                Category = category,
                BusinessPartner = partner,
                IsPrimary = true
            };
            // Add the relationship to the new aggregate before the first flush.
            // EF now sees both rows as Added and cannot misclassify the client-keyed
            // join as an existing row that requires an UPDATE.
            partner.Categories.Add(categoryAssignment);
        }
        else
        {
            _logger.LogWarning(
                "Registration {RegistrationId} has no registration category; no initial partner category was assigned",
                registrationId);
        }

        var createdPartner = await _partnerRepository.CreateAsync(partner);
        if (categoryAssignment is not null)
        {
            _logger.LogInformation(
                "Assigned onboarding category {CategoryCode} to approved business partner {PartnerCode}",
                categoryAssignment.Category.CategoryCode,
                createdPartner.PartnerCode);
        }

        await EnsureRegistrationContactsAsync(
            registration,
            createdPartner.Id,
            approvedById,
            registrationContacts);

        // Create financial record if banking data exists
        if (registrationBankAccounts.Count > 0)
        {
            var currentYear = DateTime.UtcNow.Year;
            var financial = new Entities.Procurement.BusinessPartnerFinancial
            {
                Id = Guid.NewGuid(),
                TenantId = registration.TenantId,
                BusinessPartnerId = createdPartner.Id,
                FiscalYear = currentYear,
                AnnualRevenue = additionalData.AnnualRevenue,
                CreatedAt = DateTime.UtcNow,
                CreatedById = approvedById
            };
            await _financialRepository.CreateAsync(financial);
        }

        // Copy documents from registration to business partner
        var registrationDocuments = await _documentRepository.GetDocumentsByRegistrationAsync(registration.Id);
        var verifiedDocs = registrationDocuments.Where(d => d.IsVerified && !d.IsRejected).ToList();

        _logger.LogInformation("Found {TotalDocs} total documents, {VerifiedDocs} verified documents for registration {RegistrationId}",
            registrationDocuments.Count(), verifiedDocs.Count, registrationId);

        foreach (var regDoc in verifiedDocs)
        {
            var document = new Entities.Procurement.BusinessPartnerDocument
            {
                Id = Guid.NewGuid(),
                TenantId = registration.TenantId,
                BusinessPartnerId = createdPartner.Id,
                DocumentType = regDoc.DocumentType,
                DocumentName = regDoc.DocumentName,
                DocumentPath = regDoc.DocumentPath,
                FileSize = regDoc.FileSize,
                MimeType = regDoc.MimeType,
                IsVerified = true,
                VerifiedById = approvedById,
                VerifiedDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedById = approvedById
            };
            await _partnerDocumentRepository.CreateAsync(document);
            _logger.LogInformation("Copied document {DocumentName} to business partner {PartnerCode}",
                regDoc.DocumentName, createdPartner.PartnerCode);
        }

        // Copy licenses from registration data to business partner
        _logger.LogInformation("DEBUG: Checking licenses - additionalData.Licenses is null: {IsNull}, Count: {Count}",
            additionalData.Licenses == null, additionalData.Licenses?.Count ?? 0);

        if (additionalData.Licenses != null && additionalData.Licenses.Any())
        {
            _logger.LogInformation("Found {LicenseCount} licenses in registration data for registration {RegistrationId}",
                additionalData.Licenses.Count(), registrationId);

            foreach (var regLicense in additionalData.Licenses)
            {
                _logger.LogInformation("DEBUG: Processing license - TypeId: {TypeId}, Number: {Number}, IssueDate: {IssueDate}",
                    regLicense.LicenseTypeId, regLicense.LicenseNumber, regLicense.IssueDate);

                var license = new Entities.Procurement.BusinessPartnerLicense
                {
                    Id = Guid.NewGuid(),
                    TenantId = registration.TenantId,
                    BusinessPartnerId = createdPartner.Id,
                    LicenseTypeId = Guid.Parse(regLicense.LicenseTypeId),
                    LicenseNumber = regLicense.LicenseNumber,
                    IssuingAuthority = regLicense.IssuingAuthority,
                    IssueDate = DateTime.Parse(regLicense.IssueDate),
                    ExpiryDate = !string.IsNullOrEmpty(regLicense.ExpiryDate) ? DateTime.Parse(regLicense.ExpiryDate) : null,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = approvedById
                };
                await _licenseRepository.CreateAsync(license);
                _logger.LogInformation("Copied license {LicenseNumber} to business partner {PartnerCode}",
                    regLicense.LicenseNumber, createdPartner.PartnerCode);
            }
        }
        else
        {
            _logger.LogInformation("No licenses found in registration data for registration {RegistrationId}. additionalData.Licenses is null: {IsNull}",
                registrationId, additionalData.Licenses == null);
        }

        // NOTE: Do NOT save here - let the caller (ApproveRegistrationAsync) save everything in one transaction
        // This ensures the business partner creation and registration status update happen atomically

        _logger.LogInformation("Business partner {PartnerCode} prepared from registration {RegistrationId}",
            createdPartner.PartnerCode, registrationId);

        // Map to DTO
        return new BusinessPartnerDetailDto
        {
            Id = createdPartner.Id,
            PartnerCode = createdPartner.PartnerCode,
            PartnerName = createdPartner.PartnerName,
            PartnerType = createdPartner.PartnerType,
            RegistrationNumber = createdPartner.BusinessRegistrationNumber,
            TaxNumber = createdPartner.TaxIdentificationNumber,
            VatNumber = createdPartner.VATNumber,
            Email = createdPartner.PrimaryEmail,
            Phone = createdPartner.PrimaryPhone,
            Website = createdPartner.Website,
            PhysicalAddress = createdPartner.PhysicalAddress,
            City = createdPartner.PhysicalCity,
            Country = createdPartner.PhysicalCountry,
            PhysicalPostalCode = createdPartner.PhysicalPostalCode,
            Status = createdPartner.RegistrationStatus,
            ApprovalStatus = createdPartner.ApprovalStatus,
            IsPreferred = createdPartner.IsPreferred,
            IsBlacklisted = createdPartner.IsBlacklisted,
            CreatedAt = createdPartner.CreatedAt
        };
    }

    // Helper mapping methods
    private BusinessPartnerRegistrationDto MapToDto(Entities.Procurement.BusinessPartnerRegistration registration)
    {
        return new BusinessPartnerRegistrationDto
        {
            Id = registration.Id,
            BusinessPartnerId = registration.BusinessPartnerId,
            ApplicationNumber = registration.RegistrationNumber,
            CompanyName = registration.ApplicantName,
            Email = registration.ApplicantEmail,
            Phone = registration.ApplicantPhone,
            PartnerType = registration.PartnerType,
            RegistrationCategory = registration.RegistrationCategory,
            Status = registration.Status,
            SubmittedDate = registration.SubmittedDate,
            ReviewedDate = registration.ReviewedDate,
            ReviewedBy = registration.ReviewedById?.ToString(),
            CompletionPercentage = CalculateCompletionPercentage(registration),
            CreatedAt = registration.CreatedAt
        };
    }

    private BusinessPartnerRegistrationDetailDto MapToDetailDto(
        Entities.Procurement.BusinessPartnerRegistration registration,
        IEnumerable<Entities.Procurement.BusinessPartnerRegistrationStatusHistory> statusHistory)
    {
        var dto = new BusinessPartnerRegistrationDetailDto
        {
            Id = registration.Id,
            BusinessPartnerId = registration.BusinessPartnerId,
            ApplicationNumber = registration.RegistrationNumber,
            CompanyName = registration.ApplicantName,
            Email = registration.ApplicantEmail,
            Phone = registration.ApplicantPhone,
            PartnerType = registration.PartnerType,
            RegistrationCategory = registration.RegistrationCategory,
            Status = registration.Status,
            SubmittedDate = registration.SubmittedDate,
            ReviewedDate = registration.ReviewedDate,
            ReviewedBy = registration.ReviewedById?.ToString(),
            CompletionPercentage = CalculateCompletionPercentage(registration),
            RegistrationData = registration.RegistrationDataJson,
            ReviewNotes = registration.InternalNotes,
            RejectionReason = registration.RejectionReason,
            CreatedAt = registration.CreatedAt
        };

        // Parse and map registration data JSON to individual fields
        if (!string.IsNullOrEmpty(registration.RegistrationDataJson))
        {
            try
            {
                var registrationData = ParseRegistrationData(registration.RegistrationDataJson);
                dto.RegistrationData = registrationData.GetRawText();

                // Company Information
                if (TryGetProperty(registrationData, "tradingName", out var tradingName))
                    dto.TradingName = tradingName.GetString();
                if (TryGetProperty(registrationData, "registrationNumber", out var regNumber))
                    dto.RegistrationNumber = regNumber.GetString();
                if (TryGetProperty(registrationData, "taxNumber", out var taxNumber))
                    dto.TaxNumber = taxNumber.GetString();
                if (TryGetProperty(registrationData, "vatNumber", out var vatNumber))
                    dto.VatNumber = vatNumber.GetString();
                if (TryGetProperty(registrationData, "website", out var website))
                    dto.Website = website.GetString();
                if (TryGetProperty(registrationData, "industryType", out var industryType))
                    dto.IndustryType = industryType.GetString();
                if (TryGetProperty(registrationData, "yearsInBusiness", out var yearsInBusiness) && yearsInBusiness.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dto.YearsInBusiness = yearsInBusiness.GetInt32();
                if (TryGetProperty(registrationData, "numberOfEmployees", out var numberOfEmployees) && numberOfEmployees.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dto.NumberOfEmployees = numberOfEmployees.GetInt32();
                if (TryGetProperty(registrationData, "annualRevenue", out var annualRevenue) && annualRevenue.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dto.AnnualRevenue = annualRevenue.GetDecimal();

                // Contact Information
                if (TryGetProperty(registrationData, "alternatePhone", out var alternatePhone))
                    dto.AlternatePhone = alternatePhone.GetString();
                if (TryGetProperty(registrationData, "physicalAddress", out var physicalAddress))
                    dto.PhysicalAddress = physicalAddress.GetString();
                if (TryGetProperty(registrationData, "city", out var city))
                    dto.City = city.GetString();
                if (TryGetProperty(registrationData, "country", out var country))
                    dto.Country = country.GetString();
                if (TryGetProperty(registrationData, "postalCode", out var postalCode))
                    dto.PostalCode = postalCode.GetString();

                // Primary Contact Person
                if (TryGetProperty(registrationData, "contactPersonName", out var contactPersonName))
                    dto.ContactPersonName = contactPersonName.GetString();
                if (TryGetProperty(registrationData, "contactPersonTitle", out var contactPersonTitle))
                    dto.ContactPersonTitle = contactPersonTitle.GetString();
                if (TryGetProperty(registrationData, "contactPersonEmail", out var contactPersonEmail))
                    dto.ContactPersonEmail = contactPersonEmail.GetString();
                if (TryGetProperty(registrationData, "contactPersonPhone", out var contactPersonPhone))
                    dto.ContactPersonPhone = contactPersonPhone.GetString();

                // Banking Information
                if (TryGetProperty(registrationData, "bankName", out var bankName))
                    dto.BankName = bankName.GetString();
                if (TryGetProperty(registrationData, "bankAccountNumber", out var bankAccountNumber))
                    dto.BankAccountNumber = bankAccountNumber.GetString();
                if (TryGetProperty(registrationData, "bankBranchCode", out var bankBranchCode))
                    dto.BankBranchCode = bankBranchCode.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse registration data JSON for registration {RegistrationId}", registration.Id);
            }
        }

        // Map documents if available
        if (registration.Documents != null && registration.Documents.Any())
        {
            dto.Documents = registration.Documents.Select(d => new BusinessPartnerRegistrationDocumentDto
            {
                Id = d.Id,
                RegistrationId = d.RegistrationId,
                FileUploadRecordId = d.FileUploadRecordId,
                CentralDocumentRecordId = d.CentralDocumentRecordId,
                CentralDocumentVersionId = d.CentralDocumentVersionId,
                VirusScanStatus = d.FileUploadRecord?.VirusScanStatus,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FilePath = string.Empty,
                DocumentPath = null,
                InternalStoragePath = d.FileUploadRecord?.FilePath ?? d.DocumentPath,
                FileSize = d.FileSize ?? 0,
                MimeType = d.MimeType,
                EvidenceRequirementCode = d.EvidenceRequirementCode,
                ClassificationCode = d.ClassificationCode,
                IssuedAtUtc = d.IssuedAtUtc,
                ExpiresAtUtc = d.ExpiresAtUtc,
                ChecksumSha256 = d.ChecksumSha256,
                IsVerified = d.IsVerified,
                IsRejected = d.IsRejected,
                RejectionReason = d.RejectionReason,
                RejectedDate = d.RejectedDate,
                UploadedAt = d.CreatedAt
            }).ToList();
        }

        // Map status history
        if (statusHistory != null && statusHistory.Any())
        {
            dto.StatusHistory = statusHistory.Select(h => new BusinessPartnerRegistrationStatusHistoryDto
            {
                Id = h.Id,
                RegistrationId = h.RegistrationId,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                ChangedBy = h.ChangedBy?.UserName,
                ChangedAt = h.ChangedAt,
                Notes = h.Notes
            }).ToList();
        }

        return dto;
    }

    private async Task PopulateEvidenceReadinessAsync(
        BusinessPartnerRegistrationDetailDto result,
        Entities.Procurement.BusinessPartnerRegistration registration)
    {
        if (_evidencePackService is null ||
            !string.Equals(registration.PartnerType, "Supplier", StringComparison.OrdinalIgnoreCase))
            return;

        result.EvidenceReadiness = await _evidencePackService
            .GetRegistrationReadinessAsync(registration.Id, CancellationToken.None);
    }

    private static string NormalizeRegistrationDataJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "{}";
        try
        {
            var parsed = ParseRegistrationData(value);
            if (parsed.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new InvalidOperationException(
                    "Registration data must be a JSON object.");
            return parsed.GetRawText();
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new InvalidOperationException(
                "Registration data must contain valid JSON.", exception);
        }
    }

    private static System.Text.Json.JsonElement ParseRegistrationData(string value)
    {
        using var document = System.Text.Json.JsonDocument.Parse(value);
        var current = document.RootElement.Clone();

        // Older saves serialized UpdateBusinessPartnerRegistrationDto itself,
        // producing one or more RegistrationData string wrappers. Unwrap those
        // shapes centrally so every client receives the same canonical object.
        for (var depth = 0; depth < 3 &&
             current.ValueKind == System.Text.Json.JsonValueKind.Object &&
             TryGetProperty(current, "registrationData", out var nested); depth++)
        {
            if (nested.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                current = nested.Clone();
                continue;
            }

            if (nested.ValueKind != System.Text.Json.JsonValueKind.String ||
                string.IsNullOrWhiteSpace(nested.GetString()))
                break;

            using var nestedDocument = System.Text.Json.JsonDocument.Parse(nested.GetString()!);
            current = nestedDocument.RootElement.Clone();
        }

        return current;
    }

    private static bool TryGetProperty(
        System.Text.Json.JsonElement source,
        string propertyName,
        out System.Text.Json.JsonElement value)
    {
        if (source.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static int CalculateCompletionPercentage(Entities.Procurement.BusinessPartnerRegistration registration)
    {
        // If registration is approved, it's 100% complete
        if (registration.Status == "Approved")
        {
            return 100;
        }

        if (string.IsNullOrEmpty(registration.RegistrationDataJson))
        {
            return 0;
        }

        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<CreateBusinessPartnerRegistrationDto>(registration.RegistrationDataJson);
            if (data == null)
            {
                return 0;
            }

            int totalFields = 10;
            int completedFields = 0;

            if (!string.IsNullOrEmpty(data.CompanyName))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.Email))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.Phone))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.PartnerType))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.PhysicalAddress))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.City))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.Country))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.RegistrationNumber))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.TaxNumber))
            {
                completedFields++;
            }

            if (!string.IsNullOrEmpty(data.ContactPersonName))
            {
                completedFields++;
            }

            return (int)((completedFields / (double)totalFields) * 100);
        }
        catch
        {
            return 0;
        }
    }

    #region Email Template Helpers

    private static string GenerateRegistrationSubmittedEmailBody(string companyName, string applicationNumber)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #3b82f6; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 5px 5px; }}
        .info-box {{ background-color: white; padding: 15px; margin: 20px 0; border-left: 4px solid #3b82f6; }}
        .footer {{ text-align: center; margin-top: 20px; color: #6b7280; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Registration Submitted Successfully</h1>
        </div>
        <div class='content'>
            <p>Dear {companyName},</p>
            <p>Thank you for submitting your business partner registration application.</p>
            <div class='info-box'>
                <strong>Application Number:</strong> {applicationNumber}
            </div>
            <p>Your application is now under review. We will notify you once the review is complete.</p>
            <p><strong>Next Steps:</strong></p>
            <ul>
                <li>Our team will review your application within 3-5 business days</li>
                <li>You may be contacted if additional information is required</li>
                <li>You will receive an email notification once a decision is made</li>
            </ul>
            <p>If you have any questions, please don't hesitate to contact us.</p>
            <p>Best regards,<br>Business Partner Registration Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateRegistrationApprovedEmailBody(string companyName, string partnerNumber)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #10b981; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 5px 5px; }}
        .info-box {{ background-color: white; padding: 15px; margin: 20px 0; border-left: 4px solid #10b981; }}
        .footer {{ text-align: center; margin-top: 20px; color: #6b7280; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Registration Approved!</h1>
        </div>
        <div class='content'>
            <p>Dear {companyName},</p>
            <p>Congratulations! Your business partner registration has been approved.</p>
            <div class='info-box'>
                <strong>Your Partner Number:</strong> {partnerNumber}
            </div>
            <p>Your supplier approval is complete. Portal account access is activated through a separate credential-delivery step.</p>
            <p><strong>What's Next:</strong></p>
            <ul>
                <li>If this application uses the supplier portal, your login identifier and one-time temporary password will be sent separately to the verified application contact after account provisioning</li>
                <li>Do not use the application token as your portal password</li>
                <li>The temporary password must be changed at first login and expires after the configured validity period</li>
                <li>If the separate credential message does not arrive, contact procurement support</li>
            </ul>
            <p>Welcome to our business partner network!</p>
            <p>Best regards,<br>Business Partner Registration Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateRegistrationRejectedEmailBody(string companyName, string reason)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f59e0b; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 5px 5px; }}
        .info-box {{ background-color: white; padding: 15px; margin: 20px 0; border-left: 4px solid #f59e0b; }}
        .footer {{ text-align: center; margin-top: 20px; color: #6b7280; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Registration Status Update</h1>
        </div>
        <div class='content'>
            <p>Dear {companyName},</p>
            <p>Thank you for your interest in becoming a business partner. After careful review, we regret to inform you that your application has not been approved at this time.</p>
            <div class='info-box'>
                <strong>Reason:</strong><br>{reason}
            </div>
            <p>You may reapply after addressing the concerns mentioned above.</p>
            <p>If you have any questions or need clarification, please contact our team.</p>
            <p>Best regards,<br>Business Partner Registration Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string GenerateMoreInfoRequiredEmailBody(string companyName, string applicationNumber, string notes, List<Entities.Procurement.BusinessPartnerRegistrationDocument> rejectedDocuments)
    {
        var rejectedDocsHtml = "";
        if (rejectedDocuments.Any())
        {
            rejectedDocsHtml = @"
            <div class='rejected-docs-box'>
                <p><strong>❌ Rejected Documents:</strong></p>
                <ul>";
            foreach (var doc in rejectedDocuments)
            {
                rejectedDocsHtml += $@"
                    <li>
                        <strong>{doc.DocumentType}:</strong> {doc.DocumentName}<br>
                        <span style='color: #dc2626;'>Reason: {doc.RejectionReason}</span>
                    </li>";
            }
            rejectedDocsHtml += @"
                </ul>
                <p style='color: #dc2626; font-weight: bold;'>⚠️ Please re-upload these documents with the necessary corrections.</p>
            </div>";
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #f59e0b; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9fafb; padding: 30px; border-radius: 0 0 5px 5px; }}
        .info-box {{ background-color: white; padding: 15px; margin: 20px 0; border-left: 4px solid #f59e0b; }}
        .notes-box {{ background-color: #fef3c7; padding: 15px; margin: 20px 0; border-left: 4px solid #f59e0b; }}
        .rejected-docs-box {{ background-color: #fee2e2; padding: 15px; margin: 20px 0; border-left: 4px solid #dc2626; }}
        .footer {{ text-align: center; margin-top: 20px; color: #6b7280; font-size: 12px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #f59e0b; color: white; text-decoration: none; border-radius: 5px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>⚠️ More Information Required</h1>
        </div>
        <div class='content'>
            <p>Dear {companyName},</p>

            <p>Thank you for submitting your business partner registration application.</p>

            <div class='info-box'>
                <p><strong>Application Number:</strong> {applicationNumber}</p>
                <p><strong>Status:</strong> More Information Required</p>
            </div>

            <p>Our review team has identified that additional information is needed to process your application.</p>

            {rejectedDocsHtml}

            <div class='notes-box'>
                <p><strong>Reviewer Notes:</strong></p>
                <p>{notes}</p>
            </div>

            <p><strong>Next Steps:</strong></p>
            <ul>
                <li>Log in to your account</li>
                <li>Navigate to your business partner registration</li>
                <li>Update the required information based on the notes above</li>
                {(rejectedDocuments.Any() ? "<li>Re-upload the rejected documents with necessary corrections</li>" : "")}
                <li>Resubmit your application for review</li>
            </ul>

            <p>If you have any questions or need assistance, please don't hesitate to contact our support team.</p>

            <p>Best regards,<br>
            Procurement Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Parse the RegistrationDataJson to extract additional fields
    /// </summary>
    private RegistrationAdditionalData ParseRegistrationDataJson(string? registrationDataJson)
    {
        var result = new RegistrationAdditionalData();

        if (string.IsNullOrEmpty(registrationDataJson))
        {
            return result;
        }

        try
        {
            var jsonDoc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(registrationDataJson);

            // Check for nested RegistrationData property
            System.Text.Json.JsonElement dataElement = jsonDoc;
            if (jsonDoc.TryGetProperty("RegistrationData", out var nestedData) && nestedData.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var nestedJsonString = nestedData.GetString();
                if (!string.IsNullOrEmpty(nestedJsonString))
                {
                    dataElement = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(nestedJsonString);
                }
            }
            else if (jsonDoc.TryGetProperty("registrationData", out nestedData) && nestedData.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var nestedJsonString = nestedData.GetString();
                if (!string.IsNullOrEmpty(nestedJsonString))
                {
                    dataElement = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(nestedJsonString);
                }
            }

            // Extract fields - check both PascalCase and camelCase
            // Basic Company Information
            result.CompanyName = TryGetStringProperty(dataElement, "CompanyName", "companyName");
            result.TradingName = TryGetStringProperty(dataElement, "TradingName", "tradingName");
            result.RegistrationNumber = TryGetStringProperty(dataElement, "RegistrationNumber", "registrationNumber");
            result.TaxNumber = TryGetStringProperty(dataElement, "TaxNumber", "taxNumber");
            result.VatNumber = TryGetStringProperty(dataElement, "VatNumber", "vatNumber");

            // Contact Information
            result.Email = TryGetStringProperty(dataElement, "Email", "email");
            result.Phone = TryGetStringProperty(dataElement, "Phone", "phone");
            result.AlternatePhone = TryGetStringProperty(dataElement, "AlternatePhone", "alternatePhone");
            result.Website = TryGetStringProperty(dataElement, "Website", "website");

            // Address Information
            result.PhysicalAddress = TryGetStringProperty(dataElement, "PhysicalAddress", "physicalAddress");
            result.City = TryGetStringProperty(dataElement, "City", "city");
            result.Country = TryGetStringProperty(dataElement, "Country", "country");
            result.PostalCode = TryGetStringProperty(dataElement, "PostalCode", "postalCode");

            // Business Details
            result.IndustryType = TryGetStringProperty(dataElement, "IndustryType", "industryType");
            result.YearsInBusiness = TryGetIntProperty(dataElement, "YearsInBusiness", "yearsInBusiness");
            result.NumberOfEmployees = TryGetIntProperty(dataElement, "NumberOfEmployees", "numberOfEmployees");
            result.AnnualRevenue = TryGetDecimalProperty(dataElement, "AnnualRevenue", "annualRevenue");

            // Contact Person
            result.ContactPersonName = TryGetStringProperty(dataElement, "ContactPersonName", "contactPersonName");
            result.ContactPersonTitle = TryGetStringProperty(dataElement, "ContactPersonTitle", "contactPersonTitle");
            result.ContactPersonEmail = TryGetStringProperty(dataElement, "ContactPersonEmail", "contactPersonEmail");
            result.ContactPersonPhone = TryGetStringProperty(dataElement, "ContactPersonPhone", "contactPersonPhone");

            // New registrations can submit multiple contacts. The singular
            // contact-person fields above remain supported for registrations
            // created before the collection was introduced.
            if ((dataElement.TryGetProperty("contacts", out var contactsElement) ||
                 dataElement.TryGetProperty("Contacts", out contactsElement)) &&
                contactsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var contactElement in contactsElement.EnumerateArray())
                {
                    result.Contacts.Add(new RegistrationContactData
                    {
                        ContactName = TryGetStringProperty(contactElement, "ContactName", "contactName"),
                        ContactTitle = TryGetStringProperty(contactElement, "ContactTitle", "contactTitle", "Title", "title"),
                        Department = TryGetStringProperty(contactElement, "Department", "department"),
                        Email = TryGetStringProperty(contactElement, "Email", "email"),
                        Phone = TryGetStringProperty(contactElement, "Phone", "phone"),
                        Mobile = TryGetStringProperty(contactElement, "Mobile", "mobile"),
                        IsPrimary = TryGetBooleanProperty(contactElement, "IsPrimary", "isPrimary")
                    });
                }
            }

            // Banking Information
            result.BankName = TryGetStringProperty(dataElement, "BankName", "bankName");
            result.BankAccountNumber = TryGetStringProperty(dataElement, "BankAccountNumber", "bankAccountNumber");
            result.BankBranchCode = TryGetStringProperty(
                dataElement,
                "BankBranchCode",
                "bankBranchCode",
                "BankBranch",
                "bankBranch",
                "BranchName",
                "branchName");
            result.BankAccountName = TryGetStringProperty(dataElement, "BankAccountName", "bankAccountName", "AccountName", "accountName");
            result.BankSwiftCode = TryGetStringProperty(dataElement, "BankSwiftCode", "bankSwiftCode", "SwiftCode", "swiftCode");
            result.BankIban = TryGetStringProperty(dataElement, "BankIBAN", "bankIBAN", "Iban", "iban");
            result.BankCurrency = TryGetStringProperty(dataElement, "Currency", "currency", "CurrencyCode", "currencyCode");

            if ((dataElement.TryGetProperty("bankAccounts", out var bankAccountsElement) ||
                 dataElement.TryGetProperty("BankAccounts", out bankAccountsElement)) &&
                bankAccountsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var bankAccountElement in bankAccountsElement.EnumerateArray())
                {
                    result.BankAccounts.Add(new RegistrationBankAccountData
                    {
                        BankName = TryGetStringProperty(bankAccountElement, "BankName", "bankName"),
                        BranchName = TryGetStringProperty(
                            bankAccountElement,
                            "BranchName",
                            "branchName",
                            "BankBranch",
                            "bankBranch",
                            "BankBranchCode",
                            "bankBranchCode"),
                        AccountName = TryGetStringProperty(bankAccountElement, "AccountName", "accountName"),
                        AccountNumber = TryGetStringProperty(bankAccountElement, "AccountNumber", "accountNumber", "BankAccountNumber", "bankAccountNumber"),
                        SwiftCode = TryGetStringProperty(bankAccountElement, "SwiftCode", "swiftCode"),
                        Iban = TryGetStringProperty(bankAccountElement, "Iban", "iban", "IBAN"),
                        Currency = TryGetStringProperty(bankAccountElement, "Currency", "currency", "CurrencyCode", "currencyCode"),
                        IsPrimary = TryGetBooleanProperty(bankAccountElement, "IsPrimary", "isPrimary")
                    });
                }
            }

            // Licenses
            _logger.LogInformation("DEBUG: Checking for licenses property in dataElement");
            var hasLicensesLower = dataElement.TryGetProperty("licenses", out var licensesElement);
            var hasLicensesUpper = dataElement.TryGetProperty("Licenses", out var licensesElementUpper);
            _logger.LogInformation("DEBUG: Has 'licenses' (lowercase): {HasLower}, Has 'Licenses' (uppercase): {HasUpper}",
                hasLicensesLower, hasLicensesUpper);

            if (hasLicensesLower && licensesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                _logger.LogInformation("DEBUG: Found 'licenses' array with {Count} items", licensesElement.GetArrayLength());
                result.Licenses = new List<LicenseData>();
                foreach (var licenseElement in licensesElement.EnumerateArray())
                {
                    var license = new LicenseData
                    {
                        LicenseTypeId = TryGetStringProperty(licenseElement, "LicenseTypeId", "licenseTypeId") ?? string.Empty,
                        LicenseNumber = TryGetStringProperty(licenseElement, "LicenseNumber", "licenseNumber") ?? string.Empty,
                        IssueDate = TryGetStringProperty(licenseElement, "IssueDate", "issueDate") ?? string.Empty,
                        ExpiryDate = TryGetStringProperty(licenseElement, "ExpiryDate", "expiryDate"),
                        IssuingAuthority = TryGetStringProperty(licenseElement, "IssuingAuthority", "issuingAuthority") ?? string.Empty
                    };
                    result.Licenses.Add(license);
                    _logger.LogInformation("DEBUG: Added license - TypeId: {TypeId}, Number: {Number}",
                        license.LicenseTypeId, license.LicenseNumber);
                }
            }
            else if (hasLicensesUpper && licensesElementUpper.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                _logger.LogInformation("DEBUG: Found 'Licenses' array with {Count} items", licensesElementUpper.GetArrayLength());
                result.Licenses = new List<LicenseData>();
                foreach (var licenseElement in licensesElementUpper.EnumerateArray())
                {
                    var license = new LicenseData
                    {
                        LicenseTypeId = TryGetStringProperty(licenseElement, "LicenseTypeId", "licenseTypeId") ?? string.Empty,
                        LicenseNumber = TryGetStringProperty(licenseElement, "LicenseNumber", "licenseNumber") ?? string.Empty,
                        IssueDate = TryGetStringProperty(licenseElement, "IssueDate", "issueDate") ?? string.Empty,
                        ExpiryDate = TryGetStringProperty(licenseElement, "ExpiryDate", "expiryDate"),
                        IssuingAuthority = TryGetStringProperty(licenseElement, "IssuingAuthority", "issuingAuthority") ?? string.Empty
                    };
                    result.Licenses.Add(license);
                    _logger.LogInformation("DEBUG: Added license - TypeId: {TypeId}, Number: {Number}",
                        license.LicenseTypeId, license.LicenseNumber);
                }
            }
            else
            {
                _logger.LogInformation("DEBUG: No licenses array found in dataElement");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse registration data JSON");
        }

        return result;
    }

    private string? TryGetStringProperty(System.Text.Json.JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private int? TryGetIntProperty(System.Text.Json.JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return prop.GetInt32();
                }
                else if (prop.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var strValue = prop.GetString();
                    if (int.TryParse(strValue, out var intValue))
                    {
                        return intValue;
                    }
                }
            }
        }
        return null;
    }

    private decimal? TryGetDecimalProperty(System.Text.Json.JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return prop.GetDecimal();
                }
                else if (prop.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var strValue = prop.GetString();
                    if (decimal.TryParse(strValue, out var decValue))
                    {
                        return decValue;
                    }
                }
            }
        }
        return null;
    }

    private bool TryGetBooleanProperty(System.Text.Json.JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (!element.TryGetProperty(propName, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == System.Text.Json.JsonValueKind.False)
            {
                return false;
            }

            if (prop.ValueKind == System.Text.Json.JsonValueKind.String &&
                bool.TryParse(prop.GetString(), out var value))
            {
                return value;
            }
        }

        return false;
    }

    private static List<RegistrationContactData> BuildRegistrationContacts(
        Entities.Procurement.BusinessPartnerRegistration registration,
        RegistrationAdditionalData additionalData)
    {
        var submittedContacts = additionalData.Contacts
            .Where(contact => !string.IsNullOrWhiteSpace(contact.ContactName))
            .Select(NormalizeRegistrationContact)
            .ToList();

        if (submittedContacts.Count == 0 &&
            !string.IsNullOrWhiteSpace(additionalData.ContactPersonName))
        {
            submittedContacts.Add(NormalizeRegistrationContact(new RegistrationContactData
            {
                ContactName = additionalData.ContactPersonName,
                ContactTitle = additionalData.ContactPersonTitle,
                Email = additionalData.ContactPersonEmail,
                Phone = additionalData.ContactPersonPhone,
                IsPrimary = true
            }));
        }

        // Some legacy registrations predate both contact representations. The
        // verified applicant identity is the safest available primary contact
        // for those completed applications.
        if (submittedContacts.Count == 0 &&
            !string.IsNullOrWhiteSpace(registration.ApplicantName))
        {
            submittedContacts.Add(NormalizeRegistrationContact(new RegistrationContactData
            {
                ContactName = registration.ApplicantName,
                Email = registration.ApplicantEmail ?? additionalData.Email,
                Phone = registration.ApplicantPhone ?? additionalData.Phone,
                IsPrimary = true
            }));
        }

        var uniqueContacts = new List<RegistrationContactData>();
        foreach (var contact in submittedContacts)
        {
            if (!uniqueContacts.Any(existing => AreEquivalentContacts(existing, contact)))
            {
                uniqueContacts.Add(contact);
            }
        }

        var primaryIndex = uniqueContacts.FindIndex(contact => contact.IsPrimary);
        if (primaryIndex < 0 && uniqueContacts.Count > 0)
        {
            primaryIndex = 0;
        }

        for (var index = 0; index < uniqueContacts.Count; index++)
        {
            uniqueContacts[index].IsPrimary = index == primaryIndex;
        }

        return uniqueContacts;
    }

    private async Task EnsureRegistrationContactsAsync(
        Entities.Procurement.BusinessPartnerRegistration registration,
        Guid businessPartnerId,
        Guid actorUserId,
        IReadOnlyCollection<RegistrationContactData> registrationContacts)
    {
        var existingContacts = ((await _contactRepository
                .GetContactsByPartnerAsync(businessPartnerId)) ??
            Array.Empty<Entities.Procurement.BusinessPartnerContact>())
            .Where(contact =>
                contact.TenantId == registration.TenantId &&
                !contact.IsDeleted)
            .ToList();
        var resolvedContacts = new List<(
            RegistrationContactData Registration,
            Entities.Procurement.BusinessPartnerContact Contact,
            bool IsNew)>();

        foreach (var registrationContact in registrationContacts)
        {
            var equivalent = existingContacts.FirstOrDefault(existing =>
                AreEquivalentContacts(existing, registrationContact));
            if (equivalent is not null)
            {
                resolvedContacts.Add((registrationContact, equivalent, false));
                continue;
            }

            var contact = new Entities.Procurement.BusinessPartnerContact
            {
                Id = Guid.NewGuid(),
                TenantId = registration.TenantId,
                BusinessPartnerId = businessPartnerId,
                ContactName = registrationContact.ContactName!,
                ContactTitle = registrationContact.ContactTitle,
                Department = registrationContact.Department,
                Email = registrationContact.Email,
                Phone = registrationContact.Phone,
                Mobile = registrationContact.Mobile,
                IsPrimary = false,
                CreatedAt = DateTime.UtcNow,
                CreatedById = actorUserId
            };
            await _contactRepository.CreateAsync(contact);
            existingContacts.Add(contact);
            resolvedContacts.Add((registrationContact, contact, true));
        }

        var currentPrimaries = existingContacts.Where(contact => contact.IsPrimary).ToList();
        var selectedPrimary = currentPrimaries.FirstOrDefault() ??
            resolvedContacts.FirstOrDefault(item => item.Registration.IsPrimary).Contact ??
            resolvedContacts.FirstOrDefault().Contact;

        if (selectedPrimary is not null)
        {
            if (selectedPrimary.Id != Guid.Empty &&
                resolvedContacts.Any(item => item.Contact.Id == selectedPrimary.Id && item.IsNew))
            {
                foreach (var contact in existingContacts)
                {
                    contact.IsPrimary = contact.Id == selectedPrimary.Id;
                }
            }
            else if (currentPrimaries.Count != 1 || !selectedPrimary.IsPrimary)
            {
                await _contactRepository.SetPrimaryContactAsync(
                    businessPartnerId,
                    selectedPrimary.Id);
            }
        }

        _logger.LogInformation(
            "Reconciled {SubmittedContactCount} submitted contacts to {ResolvedContactCount} supplier contacts for registration {RegistrationId}",
            registrationContacts.Count,
            resolvedContacts.Count,
            registration.Id);
    }

    private static RegistrationContactData NormalizeRegistrationContact(
        RegistrationContactData contact)
    {
        return new RegistrationContactData
        {
            ContactName = CleanContactValue(contact.ContactName),
            ContactTitle = CleanContactValue(contact.ContactTitle),
            Department = CleanContactValue(contact.Department),
            Email = CleanContactValue(contact.Email),
            Phone = CleanContactValue(contact.Phone),
            Mobile = CleanContactValue(contact.Mobile),
            IsPrimary = contact.IsPrimary
        };
    }

    private static bool AreEquivalentContacts(
        Entities.Procurement.BusinessPartnerContact existing,
        RegistrationContactData candidate)
    {
        return AreEquivalentContacts(
            new RegistrationContactData
            {
                ContactName = existing.ContactName,
                Email = existing.Email,
                Phone = existing.Phone,
                Mobile = existing.Mobile
            },
            candidate);
    }

    private static bool AreEquivalentContacts(
        RegistrationContactData first,
        RegistrationContactData second)
    {
        if (!string.IsNullOrWhiteSpace(first.Email) &&
            !string.IsNullOrWhiteSpace(second.Email) &&
            string.Equals(first.Email.Trim(), second.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var firstPhones = new[] { first.Phone, first.Mobile }
            .Select(NormalizeContactPhone)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var secondPhones = new[] { second.Phone, second.Mobile }
            .Select(NormalizeContactPhone)
            .Where(value => value.Length > 0)
            .ToList();
        if (secondPhones.Any(firstPhones.Contains))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(first.Email) ||
            !string.IsNullOrWhiteSpace(second.Email) ||
            firstPhones.Count > 0 ||
            secondPhones.Count > 0)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(first.ContactName) &&
            !string.IsNullOrWhiteSpace(second.ContactName) &&
            string.Equals(
                first.ContactName.Trim(),
                second.ContactName.Trim(),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeContactPhone(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private static string? CleanContactValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<RegistrationBankAccountData> BuildRegistrationBankAccounts(
        RegistrationAdditionalData additionalData)
    {
        if (additionalData.BankAccounts.Count > 10)
        {
            throw new InvalidOperationException(
                "A supplier application can contain at most 10 bank accounts.");
        }

        var submittedAccounts = new List<RegistrationBankAccountData>();
        foreach (var account in additionalData.BankAccounts)
        {
            var normalized = NormalizeRegistrationBankAccount(account);
            if (string.IsNullOrWhiteSpace(normalized.BankName) ||
                string.IsNullOrWhiteSpace(normalized.AccountNumber))
            {
                throw new InvalidOperationException(
                    "Every supplier bank account requires a bank name and account number.");
            }

            submittedAccounts.Add(normalized);
        }

        if (submittedAccounts.Count == 0 &&
            (!string.IsNullOrWhiteSpace(additionalData.BankName) ||
             !string.IsNullOrWhiteSpace(additionalData.BankAccountNumber)))
        {
            submittedAccounts.Add(NormalizeRegistrationBankAccount(
                new RegistrationBankAccountData
                {
                    BankName = additionalData.BankName,
                    BranchName = additionalData.BankBranchCode,
                    AccountName = additionalData.BankAccountName,
                    AccountNumber = additionalData.BankAccountNumber,
                    SwiftCode = additionalData.BankSwiftCode,
                    Iban = additionalData.BankIban,
                    Currency = additionalData.BankCurrency,
                    IsPrimary = true
                }));
        }

        var uniqueAccounts = new List<RegistrationBankAccountData>();
        foreach (var account in submittedAccounts)
        {
            if (!uniqueAccounts.Any(existing =>
                    AreEquivalentBankAccounts(existing, account)))
            {
                uniqueAccounts.Add(account);
            }
        }

        var primaryIndex = uniqueAccounts.FindIndex(account => account.IsPrimary);
        if (primaryIndex < 0 && uniqueAccounts.Count > 0)
        {
            primaryIndex = 0;
        }

        for (var index = 0; index < uniqueAccounts.Count; index++)
        {
            uniqueAccounts[index].IsPrimary = index == primaryIndex;
        }

        return uniqueAccounts;
    }

    private async Task EnsureRegistrationBankAccountsAsync(
        Entities.Procurement.BusinessPartnerRegistration registration,
        Guid businessPartnerId,
        Guid actorUserId,
        IReadOnlyCollection<RegistrationBankAccountData> registrationBankAccounts)
    {
        if (registrationBankAccounts.Count == 0)
        {
            return;
        }

        var partner = await _partnerRepository.GetWithBankAccountsAsync(businessPartnerId)
            ?? throw new InvalidOperationException(
                $"Approved business partner {businessPartnerId} was not found for bank-account reconciliation.");
        var existingAccounts = partner.BankAccounts
            .Where(account =>
                account.TenantId == registration.TenantId &&
                !account.IsDeleted)
            .ToList();
        var resolvedAccounts = new List<(
            RegistrationBankAccountData Registration,
            Entities.Procurement.BusinessPartnerBankAccount Account)>();

        foreach (var registrationBankAccount in registrationBankAccounts)
        {
            var equivalent = existingAccounts.FirstOrDefault(existing =>
                AreEquivalentBankAccounts(existing, registrationBankAccount));
            if (equivalent is null)
            {
                equivalent = new Entities.Procurement.BusinessPartnerBankAccount
                {
                    Id = Guid.NewGuid(),
                    TenantId = registration.TenantId,
                    BusinessPartnerId = businessPartnerId,
                    BusinessPartner = partner,
                    BankName = registrationBankAccount.BankName,
                    BranchName = registrationBankAccount.BranchName,
                    AccountName = registrationBankAccount.AccountName,
                    AccountNumber = registrationBankAccount.AccountNumber,
                    SwiftCode = registrationBankAccount.SwiftCode,
                    Iban = registrationBankAccount.Iban,
                    Currency = registrationBankAccount.Currency,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = actorUserId
                };
                partner.BankAccounts.Add(equivalent);
                existingAccounts.Add(equivalent);
            }

            resolvedAccounts.Add((registrationBankAccount, equivalent));
        }

        var currentPrimary = existingAccounts.FirstOrDefault(account => account.IsPrimary);
        var selectedPrimary = currentPrimary ??
            resolvedAccounts.FirstOrDefault(item => item.Registration.IsPrimary).Account ??
            resolvedAccounts.First().Account;
        foreach (var account in existingAccounts)
        {
            account.IsPrimary = account.Id == selectedPrimary.Id;
        }

        partner.BankName = selectedPrimary.BankName;
        partner.BankAccountName = selectedPrimary.AccountName;
        partner.BankAccountNumber = selectedPrimary.AccountNumber;
        partner.BankBranch = selectedPrimary.BranchName;
        partner.BankSwiftCode = selectedPrimary.SwiftCode;
        partner.BankIBAN = selectedPrimary.Iban;
        partner.Currency = selectedPrimary.Currency;
        partner.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Reconciled {SubmittedBankAccountCount} submitted bank accounts to {ResolvedBankAccountCount} supplier bank accounts for registration {RegistrationId}",
            registrationBankAccounts.Count,
            resolvedAccounts.Count,
            registration.Id);
    }

    private static RegistrationBankAccountData NormalizeRegistrationBankAccount(
        RegistrationBankAccountData account)
    {
        return new RegistrationBankAccountData
        {
            BankName = CleanContactValue(account.BankName),
            BranchName = CleanContactValue(account.BranchName),
            AccountName = CleanContactValue(account.AccountName),
            AccountNumber = CleanContactValue(account.AccountNumber),
            SwiftCode = CleanContactValue(account.SwiftCode),
            Iban = CleanContactValue(account.Iban),
            Currency = CleanContactValue(account.Currency),
            IsPrimary = account.IsPrimary
        };
    }

    private static bool AreEquivalentBankAccounts(
        Entities.Procurement.BusinessPartnerBankAccount existing,
        RegistrationBankAccountData candidate)
    {
        return AreEquivalentBankAccounts(
            new RegistrationBankAccountData
            {
                BankName = existing.BankName,
                AccountNumber = existing.AccountNumber,
                Iban = existing.Iban
            },
            candidate);
    }

    private static bool AreEquivalentBankAccounts(
        RegistrationBankAccountData first,
        RegistrationBankAccountData second)
    {
        if (!string.IsNullOrWhiteSpace(first.Iban) &&
            !string.IsNullOrWhiteSpace(second.Iban) &&
            string.Equals(
                NormalizeBankAccountIdentity(first.Iban),
                NormalizeBankAccountIdentity(second.Iban),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(first.BankName) &&
            !string.IsNullOrWhiteSpace(second.BankName) &&
            !string.IsNullOrWhiteSpace(first.AccountNumber) &&
            !string.IsNullOrWhiteSpace(second.AccountNumber) &&
            string.Equals(first.BankName.Trim(), second.BankName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                NormalizeBankAccountIdentity(first.AccountNumber),
                NormalizeBankAccountIdentity(second.AccountNumber),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBankAccountIdentity(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private async Task EnsureInternalCapabilityAsync(
        string permissionCode,
        Entities.Procurement.BusinessPartnerRegistration registration,
        Guid actorUserId,
        string correlationId)
    {
        if (!_currentUserProvider.IsAuthenticated ||
            _currentUserProvider.UserId == Guid.Empty ||
            _currentUserProvider.TenantId == Guid.Empty)
        {
            throw new ProcurementAccessAuthorizationException(
                "An authenticated tenant user is required for supplier review actions.");
        }
        if (_currentUserProvider.IsExternalUser)
        {
            throw new ProcurementAccessAuthorizationException(
                "Supplier portal users cannot perform internal supplier review actions.");
        }
        if (actorUserId != _currentUserProvider.UserId)
        {
            throw new ProcurementAccessAuthorizationException(
                "The supplier review actor must be derived from the authenticated user.");
        }
        if (registration.TenantId != _currentUserProvider.TenantId)
        {
            throw new ProcurementAccessAuthorizationException(
                "The supplier registration belongs to a different tenant.");
        }
        if (_currentUserProvider.HasRole(Constants.Roles.SuperAdmin))
        {
            return;
        }

        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permissionCode,
                SourceType = "SupplierRegistration",
                SourceReference = registration.RegistrationNumber ?? registration.Id.ToString()
            },
            correlationId);
        if (!decision.Allowed)
        {
            throw new ProcurementAccessAuthorizationException(decision.Message);
        }
    }

    private async Task<bool> HasRestrictedApplicantAccessAsync(
        Entities.Procurement.BusinessPartnerRegistration registration,
        Guid actorUserId)
    {
        if (!_currentUserProvider.IsAuthenticated ||
            !_currentUserProvider.IsExternalUser ||
            actorUserId == Guid.Empty ||
            actorUserId != _currentUserProvider.UserId ||
            registration.TenantId != _currentUserProvider.TenantId ||
            !string.Equals(
                _currentUserProvider.AuthenticationProvider,
                "ApplicantToken",
                StringComparison.OrdinalIgnoreCase) ||
            !_currentUserProvider.Claims.TryGetValue(
                "supplier_applicant_registration",
                out var registrationClaim) ||
            !Guid.TryParse(registrationClaim, out var claimedRegistrationId) ||
            claimedRegistrationId != registration.Id)
        {
            return false;
        }

        return await _unitOfWork
            .Repository<Entities.Procurement.ProcurementSupplierApplicantAccess>()
            .GetQueryable(item =>
                item.TenantId == registration.TenantId &&
                item.RegistrationId == registration.Id &&
                item.CreatedById == actorUserId &&
                item.Status ==
                    ProcurementSupplierApplicantAccessStatus.ApplicationInProgress &&
                !item.TerminalAtUtc.HasValue &&
                !item.IsDeleted)
            .AnyAsync();
    }

    #endregion
}

/// <summary>
/// Helper class to hold parsed registration data
/// </summary>
internal class RegistrationAdditionalData
{
    // Basic Company Information
    public string? CompanyName { get; set; }
    public string? TradingName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? VatNumber { get; set; }

    // Contact Information
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AlternatePhone { get; set; }
    public string? Website { get; set; }

    // Address Information
    public string? PhysicalAddress { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }

    // Business Details
    public string? IndustryType { get; set; }
    public int? YearsInBusiness { get; set; }
    public int? NumberOfEmployees { get; set; }
    public decimal? AnnualRevenue { get; set; }

    // Contact Person
    public string? ContactPersonName { get; set; }
    public string? ContactPersonTitle { get; set; }
    public string? ContactPersonEmail { get; set; }
    public string? ContactPersonPhone { get; set; }
    public List<RegistrationContactData> Contacts { get; set; } = new();

    // Banking Information
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranchCode { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankSwiftCode { get; set; }
    public string? BankIban { get; set; }
    public string? BankCurrency { get; set; }
    public List<RegistrationBankAccountData> BankAccounts { get; set; } = new();

    // Licenses
    public List<LicenseData>? Licenses { get; set; }
}

internal class RegistrationContactData
{
    public string? ContactName { get; set; }
    public string? ContactTitle { get; set; }
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public bool IsPrimary { get; set; }
}

internal class RegistrationBankAccountData
{
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? SwiftCode { get; set; }
    public string? Iban { get; set; }
    public string? Currency { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Helper class to hold license data from registration
/// </summary>
internal class LicenseData
{
    public string LicenseTypeId { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string? ExpiryDate { get; set; }
    public string IssuingAuthority { get; set; } = string.Empty;
}
