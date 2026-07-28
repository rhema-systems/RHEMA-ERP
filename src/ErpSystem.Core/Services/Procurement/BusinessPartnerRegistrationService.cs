using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Shared;
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

        return MapToDetailDto(registration, statusHistory);
    }

    public async Task<BusinessPartnerRegistrationDetailDto?> GetByApplicationNumberAsync(string applicationNumber)
    {
        var registration = await _registrationRepository.GetByApplicationNumberAsync(applicationNumber);
        if (registration == null)
        {
            return null;
        }

        var statusHistory = await _statusHistoryRepository.GetHistoryByRegistrationAsync(registration.Id);

        return MapToDetailDto(registration, statusHistory);
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
            if (!_currentUserProvider.Roles.Contains("Admin") &&
                !_currentUserProvider.Roles.Contains("BusinessPartnerAdmin"))
            {
                _logger.LogWarning("User {UserId} attempted to update registration {RegistrationId} owned by {OwnerId}",
                    userId, id, registration.CreatedById);
                throw new InvalidOperationException($"You do not have permission to update this registration");
            }
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
        registration.RegistrationDataJson = System.Text.Json.JsonSerializer.Serialize(dto);
        registration.UpdatedAt = DateTime.UtcNow;

        var updated = await _registrationRepository.UpdateAsync(registration);

        // Commit changes
        await _unitOfWork.SaveChangesAsync();

        var statusHistory = await _statusHistoryRepository.GetHistoryByRegistrationAsync(id);

        return MapToDetailDto(updated, statusHistory);
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
            _logger.LogWarning("No registrations found for user {UserId}. This might indicate CreatedById was not set correctly during creation.", userId);
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
    public async Task SubmitForReviewAsync(Guid id, Guid userId)
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
            userId,
            $"supplier-registration-submit-{id:N}",
            CancellationToken.None);

        await _registrationRepository.UpdateStatusAsync(id, "Submitted", userId, "Submitted for review");

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
                TriggeredByUserId = userId,
                Data = data
            });

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = registration.TenantId,
                EntityType = "BusinessPartner",
                Activity = "Submitted",
                Audience = "Internal",
                EntityId = registration.Id,
                TriggeredByUserId = userId,
                Data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish BusinessPartner.Submitted entity activity event for registration {RegistrationId}", id);
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
                    Title = "Registration Submitted Successfully",
                    Message = $"Your business partner registration ({registration.RegistrationNumber}) has been submitted for review. You will be notified once the review is complete.",
                    Priority = "Normal",
                    EntityType = "BusinessPartnerRegistration",
                    EntityId = id,
                    ActionUrl = $"/register/business-partner/status/{id}"
                };
                await _notificationService.CreateNotificationAsync(notificationDto, userId, registration.TenantId);
                _logger.LogInformation("Submission notification sent to user {UserId} for registration {RegistrationId}",
                    registration.CreatedById.Value, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send submission notification for registration {RegistrationId}", id);
                // Don't throw - notification failure shouldn't prevent submission
            }
        }

        // Create in-app notification and queue email (fire-and-forget)
        if (_notificationService != null && registration.CreatedById.HasValue)
        {
            try
            {
                // 1. Create in-app notification with friendly message
                var inAppNotificationDto = new CreateNotificationDto
                {
                    RecipientId = registration.CreatedById.Value,
                    Type = "InApp",
                    Title = "Registration Submitted Successfully",
                    Message = $"Your business partner registration (Application #{registration.RegistrationNumber}) has been submitted and is now under review.",
                    Priority = "Normal",
                    EntityType = "BusinessPartnerRegistration",
                    EntityId = id,
                    ActionUrl = $"/external-portal/business-partner"
                };
                await _notificationService.CreateNotificationAsync(inAppNotificationDto, userId, registration.TenantId);

                // 2. Send email notification
                if (!string.IsNullOrEmpty(registration.ApplicantEmail))
                {
                    var emailSubject = "Business Partner Registration Submitted";
                    var emailBody = GenerateRegistrationSubmittedEmailBody(registration.ApplicantName, registration.RegistrationNumber);

                    await _notificationService.SendEmailAsync(
                        registration.ApplicantEmail,
                        emailSubject,
                        emailBody,
                        isHtml: true
                    );

                    _logger.LogInformation("Registration submitted email sent to {Email} for application {ApplicationNumber}",
                        registration.ApplicantEmail, registration.RegistrationNumber);
                }

                _logger.LogInformation("Notifications created for registration submission {ApplicationNumber}", registration.RegistrationNumber);
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
        var registration = await _registrationRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Registration with ID {id} not found");
        await EnsureInternalCapabilityAsync(
            "procurement.supplier.approve", registration, approvedById,
            $"supplier-registration-approve-{id:N}");
        if (registration.Status != "Submitted" && registration.Status != "UnderReview")
        {
            throw new InvalidOperationException($"Cannot approve registration in {registration.Status} status");
        }

        // Check if all uploaded documents are verified
        var documents = await _documentRepository.GetByRegistrationIdAsync(id);
        var uploadedDocuments = documents.Where(d => !d.IsDeleted).ToList();

        if (uploadedDocuments.Any())
        {
            var unverifiedDocuments = uploadedDocuments.Where(d => !d.IsVerified && !d.IsRejected).ToList();
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
        }

        _logger.LogInformation("Starting approval process for registration {RegistrationId} by user {UserId}", id, approvedById);

        // Convert to business partner (this creates BP, contacts, financials, documents and saves them)
        var businessPartner = await ConvertToBusinessPartnerAsync(id, approvedById);

        _logger.LogInformation("Business partner {PartnerCode} created successfully from registration {RegistrationId}",
            businessPartner.PartnerCode, id);

        // Update registration status manually instead of using UpdateStatusAsync to maintain Unit of Work
        var oldStatus = registration.Status;
        registration.Status = "Approved";
        registration.ApprovedDate = DateTime.UtcNow;
        registration.ApprovedById = approvedById;
        registration.BusinessPartnerId = businessPartner.Id;
        registration.UpdatedAt = DateTime.UtcNow;

        await _registrationRepository.UpdateAsync(registration);

        // Create status history entry
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

        // Persist the terminal application state before the token hard-stop verifies
        // it across tables. The token transition itself remains separately atomic.
        await _unitOfWork.SaveChangesAsync();
        if (_onboardingTokenService != null)
        {
            await _onboardingTokenService.ExpireForTerminalRegistrationAsync(
                id,
                "Approved",
                approvedById,
                $"registration-approved-{id:N}");
        }

        // Save all changes using Unit of Work (business partner, contacts, documents, financials, registration update, status history)
        _logger.LogInformation("Saving all changes for registration {RegistrationId} approval...", id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Registration {RegistrationId} approved successfully. Status updated to Approved, BusinessPartnerId: {BusinessPartnerId}",
            id, businessPartner.Id);

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
            VirusScanStatus = d.FileUploadRecord?.VirusScanStatus,
            DocumentType = d.DocumentType,
            DocumentName = d.DocumentName,
            FilePath = d.DocumentPath,
            DocumentPath = d.DocumentPath,
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
        if (fileRecord.VirusScanStatus is Enums.FileVirusScanStatus.Pending or
            Enums.FileVirusScanStatus.Infected or
            Enums.FileVirusScanStatus.Error)
        {
            throw new InvalidOperationException(
                "Registration evidence does not have an acceptable virus-scan result.");
        }
        var requestedPath = (dto.DocumentPath ?? dto.FilePath ?? string.Empty)
            .Replace('\\', '/');
        if (!string.Equals(
                fileRecord.FilePath.Replace('\\', '/'),
                requestedPath,
                StringComparison.OrdinalIgnoreCase) ||
            fileRecord.FileSize != dto.FileSize)
        {
            throw new InvalidOperationException(
                "Registration evidence metadata does not match the controlled upload.");
        }
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
            DocumentType = dto.DocumentType,
            DocumentName = dto.DocumentName,
            DocumentPath = dto.DocumentPath ?? dto.FilePath ?? string.Empty,
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
            VirusScanStatus = fileRecord.VirusScanStatus,
            DocumentType = created.DocumentType,
            DocumentName = created.DocumentName,
            FilePath = created.DocumentPath,
            DocumentPath = created.DocumentPath,
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
            VirusScanStatus = document.FileUploadRecord?.VirusScanStatus,
            DocumentType = document.DocumentType,
            DocumentName = document.DocumentName,
            FilePath = document.DocumentPath,
            DocumentPath = document.DocumentPath,
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

        _logger.LogInformation("RegistrationDataJson for registration {RegistrationId}: {Json}",
            registrationId, registration.RegistrationDataJson);

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

        _logger.LogInformation("Creating business partner with data - CompanyName: {CompanyName}, Email: {Email}, Phone: {Phone}, " +
            "BankName: {BankName}, BankAccountNumber: {BankAccountNumber}, ContactPersonName: {ContactPersonName}",
            additionalData.CompanyName, additionalData.Email, additionalData.Phone,
            additionalData.BankName, additionalData.BankAccountNumber, additionalData.ContactPersonName);

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
            BankName = additionalData.BankName,
            BankAccountNumber = additionalData.BankAccountNumber,
            BankBranch = additionalData.BankBranchCode,
            // Primary Contact Information (from contact person)
            PrimaryContactName = additionalData.ContactPersonName,
            PrimaryContactTitle = additionalData.ContactPersonTitle,
            // Link to user account (for external portal access and notifications)
            UserId = registration.CreatedById, // Link to the user who created the registration
            // Operational status used across internal UIs and downstream docs.
            RegistrationStatus = "Active",
            ApprovalStatus = "Approved",
            IsPreferred = false,
            IsBlacklisted = false,
            IsActive = true,
            ApprovedById = approvedById,
            ApprovedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedById = approvedById
        };

        var createdPartner = await _partnerRepository.CreateAsync(partner);

        // Create primary contact if contact person data exists
        if (!string.IsNullOrEmpty(additionalData.ContactPersonName))
        {
            var contact = new Entities.Procurement.BusinessPartnerContact
            {
                Id = Guid.NewGuid(),
                TenantId = registration.TenantId,
                BusinessPartnerId = createdPartner.Id,
                ContactName = additionalData.ContactPersonName,
                ContactTitle = additionalData.ContactPersonTitle,
                Email = additionalData.ContactPersonEmail,
                Phone = additionalData.ContactPersonPhone,
                IsPrimary = true,
                CreatedAt = DateTime.UtcNow,
                CreatedById = approvedById
            };
            await _contactRepository.CreateAsync(contact);
            _logger.LogInformation("Created primary contact for business partner {PartnerCode}: {ContactName}",
                createdPartner.PartnerCode, additionalData.ContactPersonName);
        }
        else
        {
            _logger.LogWarning("No contact person data found for registration {RegistrationId}", registrationId);
        }

        // Create financial record if banking data exists
        if (!string.IsNullOrEmpty(additionalData.BankName) || !string.IsNullOrEmpty(additionalData.BankAccountNumber))
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
                // First parse the outer DTO structure
                var outerData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(registration.RegistrationDataJson);

                System.Text.Json.JsonElement registrationData;

                // Check if there's a nested "RegistrationData" property (from UpdateBusinessPartnerRegistrationDto)
                // Note: Property name is case-sensitive, check both camelCase and PascalCase
                if (outerData.TryGetProperty("RegistrationData", out var nestedData) && nestedData.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    // Parse the nested JSON string
                    var nestedJsonString = nestedData.GetString();

                    if (!string.IsNullOrEmpty(nestedJsonString))
                    {
                        registrationData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(nestedJsonString);
                    }
                    else
                    {
                        registrationData = outerData;
                    }
                }
                else if (outerData.TryGetProperty("registrationData", out nestedData) && nestedData.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    // Try camelCase version
                    var nestedJsonString = nestedData.GetString();

                    if (!string.IsNullOrEmpty(nestedJsonString))
                    {
                        registrationData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(nestedJsonString);
                    }
                    else
                    {
                        registrationData = outerData;
                    }
                }
                else
                {
                    // No nested structure, use the outer data directly
                    registrationData = outerData;
                }

                // Company Information
                if (registrationData.TryGetProperty("tradingName", out var tradingName))
                    dto.TradingName = tradingName.GetString();
                if (registrationData.TryGetProperty("registrationNumber", out var regNumber))
                    dto.RegistrationNumber = regNumber.GetString();
                if (registrationData.TryGetProperty("taxNumber", out var taxNumber))
                    dto.TaxNumber = taxNumber.GetString();
                if (registrationData.TryGetProperty("vatNumber", out var vatNumber))
                    dto.VatNumber = vatNumber.GetString();
                if (registrationData.TryGetProperty("website", out var website))
                    dto.Website = website.GetString();
                if (registrationData.TryGetProperty("industryType", out var industryType))
                    dto.IndustryType = industryType.GetString();
                if (registrationData.TryGetProperty("yearsInBusiness", out var yearsInBusiness) && yearsInBusiness.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dto.YearsInBusiness = yearsInBusiness.GetInt32();
                if (registrationData.TryGetProperty("numberOfEmployees", out var numberOfEmployees) && numberOfEmployees.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dto.NumberOfEmployees = numberOfEmployees.GetInt32();
                if (registrationData.TryGetProperty("annualRevenue", out var annualRevenue) && annualRevenue.ValueKind == System.Text.Json.JsonValueKind.Number)
                    dto.AnnualRevenue = annualRevenue.GetDecimal();

                // Contact Information
                if (registrationData.TryGetProperty("alternatePhone", out var alternatePhone))
                    dto.AlternatePhone = alternatePhone.GetString();
                if (registrationData.TryGetProperty("physicalAddress", out var physicalAddress))
                    dto.PhysicalAddress = physicalAddress.GetString();
                if (registrationData.TryGetProperty("city", out var city))
                    dto.City = city.GetString();
                if (registrationData.TryGetProperty("country", out var country))
                    dto.Country = country.GetString();
                if (registrationData.TryGetProperty("postalCode", out var postalCode))
                    dto.PostalCode = postalCode.GetString();

                // Primary Contact Person
                if (registrationData.TryGetProperty("contactPersonName", out var contactPersonName))
                    dto.ContactPersonName = contactPersonName.GetString();
                if (registrationData.TryGetProperty("contactPersonTitle", out var contactPersonTitle))
                    dto.ContactPersonTitle = contactPersonTitle.GetString();
                if (registrationData.TryGetProperty("contactPersonEmail", out var contactPersonEmail))
                    dto.ContactPersonEmail = contactPersonEmail.GetString();
                if (registrationData.TryGetProperty("contactPersonPhone", out var contactPersonPhone))
                    dto.ContactPersonPhone = contactPersonPhone.GetString();

                // Banking Information
                if (registrationData.TryGetProperty("bankName", out var bankName))
                    dto.BankName = bankName.GetString();
                if (registrationData.TryGetProperty("bankAccountNumber", out var bankAccountNumber))
                    dto.BankAccountNumber = bankAccountNumber.GetString();
                if (registrationData.TryGetProperty("bankBranchCode", out var bankBranchCode))
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
                VirusScanStatus = d.FileUploadRecord?.VirusScanStatus,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FilePath = d.DocumentPath,
                DocumentPath = d.DocumentPath,
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
            <p>You can now access all business partner features and services.</p>
            <p><strong>What's Next:</strong></p>
            <ul>
                <li>Log in to your account using your credentials</li>
                <li>Complete your profile information</li>
                <li>Start exploring available opportunities</li>
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

            // Banking Information
            result.BankName = TryGetStringProperty(dataElement, "BankName", "bankName");
            result.BankAccountNumber = TryGetStringProperty(dataElement, "BankAccountNumber", "bankAccountNumber");
            result.BankBranchCode = TryGetStringProperty(dataElement, "BankBranchCode", "bankBranchCode");

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
        if (_currentUserProvider.HasRole(Constants.Roles.SuperAdmin) ||
            _currentUserProvider.HasRole("TenantAdmin"))
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

    // Banking Information
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranchCode { get; set; }

    // Licenses
    public List<LicenseData>? Licenses { get; set; }
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
