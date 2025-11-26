using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class BusinessPartnerRegistrationService : IBusinessPartnerRegistrationService
{
    private readonly IBusinessPartnerRegistrationRepository _registrationRepository;
    private readonly IBusinessPartnerRegistrationDocumentRepository _documentRepository;
    private readonly IBusinessPartnerRegistrationStatusHistoryRepository _statusHistoryRepository;
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<BusinessPartnerRegistrationService> _logger;

    public BusinessPartnerRegistrationService(
        IBusinessPartnerRegistrationRepository registrationRepository,
        IBusinessPartnerRegistrationDocumentRepository documentRepository,
        IBusinessPartnerRegistrationStatusHistoryRepository statusHistoryRepository,
        IBusinessPartnerRepository partnerRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<BusinessPartnerRegistrationService> logger)
    {
        _registrationRepository = registrationRepository;
        _documentRepository = documentRepository;
        _statusHistoryRepository = statusHistoryRepository;
        _partnerRepository = partnerRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<BusinessPartnerRegistrationDetailDto?> GetByIdAsync(Guid id)
    {
        var registration = await _registrationRepository.GetWithDocumentsAsync(id);
        if (registration == null) return null;

        var statusHistory = await _statusHistoryRepository.GetHistoryByRegistrationAsync(id);

        return MapToDetailDto(registration, statusHistory);
    }

    public async Task<BusinessPartnerRegistrationDetailDto?> GetByApplicationNumberAsync(string applicationNumber)
    {
        var registration = await _registrationRepository.GetByApplicationNumberAsync(applicationNumber);
        if (registration == null) return null;

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
        var applicationNumber = await _registrationRepository.GenerateApplicationNumberAsync();

        // Use default tenant for external registrations
        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var registration = new Entities.Procurement.BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = defaultTenantId, // Set default tenant for external registrations
            RegistrationNumber = applicationNumber,
            ApplicantName = dto.CompanyName,
            ApplicantEmail = dto.Email,
            ApplicantPhone = dto.Phone,
            PartnerType = dto.PartnerType,
            Status = "Draft",
            RegistrationDataJson = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _registrationRepository.CreateAsync(registration);

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

        return MapToDetailDto(created, new List<Entities.Procurement.BusinessPartnerRegistrationStatusHistory>());
    }

    public async Task<BusinessPartnerRegistrationDetailDto> UpdateAsync(Guid id, UpdateBusinessPartnerRegistrationDto dto, Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {id} not found");

        if (registration.Status != "Draft" && registration.Status != "MoreInfoRequired")
            throw new InvalidOperationException($"Cannot update registration in {registration.Status} status");

        registration.ApplicantName = dto.CompanyName;
        registration.ApplicantEmail = dto.Email;
        registration.ApplicantPhone = dto.Phone;
        registration.RegistrationDataJson = System.Text.Json.JsonSerializer.Serialize(dto);
        registration.UpdatedAt = DateTime.UtcNow;

        var updated = await _registrationRepository.UpdateAsync(registration);
        var statusHistory = await _statusHistoryRepository.GetHistoryByRegistrationAsync(id);

        return MapToDetailDto(updated, statusHistory);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {id} not found");

        if (registration.Status != "Draft")
            throw new InvalidOperationException($"Cannot delete registration in {registration.Status} status");

        await _registrationRepository.DeleteAsync(id);
    }
    public async Task<PagedResult<BusinessPartnerRegistrationDto>> GetRegistrationsAsync(int page, int pageSize, string? search = null, string? status = null, string? partnerType = null, DateTime? submittedFrom = null, DateTime? submittedTo = null)
    {
        var result = await _registrationRepository.GetRegistrationsAsync(page, pageSize, search, status, partnerType, submittedFrom, submittedTo);

        return new PagedResult<BusinessPartnerRegistrationDto>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IEnumerable<BusinessPartnerRegistrationDto>> GetMyRegistrationsAsync(Guid userId)
    {
        var registrations = await _registrationRepository.GetRegistrationsByUserAsync(userId);
        return registrations.Select(MapToDto);
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
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {id} not found");

        if (registration.Status != "Draft" && registration.Status != "MoreInfoRequired")
            throw new InvalidOperationException($"Cannot submit registration in {registration.Status} status");

        await _registrationRepository.UpdateStatusAsync(id, "Submitted", userId, "Submitted for review");
    }

    public async Task ReviewRegistrationAsync(ReviewBusinessPartnerRegistrationDto dto, Guid reviewedById)
    {
        var registration = await _registrationRepository.GetByIdAsync(dto.RegistrationId);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {dto.RegistrationId} not found");

        if (registration.Status != "Submitted")
            throw new InvalidOperationException($"Cannot review registration in {registration.Status} status");

        await _registrationRepository.UpdateStatusAsync(dto.RegistrationId, "UnderReview", reviewedById, dto.ReviewNotes ?? dto.Notes);
    }

    public async Task ApproveRegistrationAsync(Guid id, Guid approvedById, string? notes = null)
    {
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {id} not found");

        if (registration.Status != "Submitted" && registration.Status != "UnderReview")
            throw new InvalidOperationException($"Cannot approve registration in {registration.Status} status");

        // Convert to business partner
        await ConvertToBusinessPartnerAsync(id, approvedById);

        // Update registration status
        await _registrationRepository.UpdateStatusAsync(id, "Approved", approvedById, notes ?? "Registration approved and converted to business partner");
    }

    public async Task RejectRegistrationAsync(Guid id, Guid rejectedById, string reason)
    {
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {id} not found");

        if (registration.Status != "Submitted" && registration.Status != "UnderReview")
            throw new InvalidOperationException($"Cannot reject registration in {registration.Status} status");

        await _registrationRepository.UpdateStatusAsync(id, "Rejected", rejectedById, reason);
    }

    public async Task RequestMoreInfoAsync(Guid id, Guid requestedById, string notes)
    {
        var registration = await _registrationRepository.GetByIdAsync(id);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {id} not found");

        if (registration.Status != "Submitted" && registration.Status != "UnderReview")
            throw new InvalidOperationException($"Cannot request more info for registration in {registration.Status} status");

        await _registrationRepository.UpdateStatusAsync(id, "MoreInfoRequired", requestedById, notes);
    }
    public async Task<IEnumerable<BusinessPartnerRegistrationDocumentDto>> GetDocumentsAsync(Guid registrationId)
    {
        var documents = await _documentRepository.GetDocumentsByRegistrationAsync(registrationId);
        return documents.Select(d => new BusinessPartnerRegistrationDocumentDto
        {
            Id = d.Id,
            RegistrationId = d.RegistrationId,
            DocumentType = d.DocumentType,
            DocumentName = d.DocumentName,
            FilePath = d.DocumentPath,
            DocumentPath = d.DocumentPath,
            FileSize = d.FileSize ?? 0,
            IsVerified = d.IsVerified,
            UploadedAt = d.CreatedAt
        });
    }

    public async Task<BusinessPartnerRegistrationDocumentDto> UploadDocumentAsync(Guid registrationId, CreateBusinessPartnerDocumentDto dto, Guid userId)
    {
        var registration = await _registrationRepository.GetByIdAsync(registrationId);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {registrationId} not found");

        var document = new Entities.Procurement.BusinessPartnerRegistrationDocument
        {
            Id = Guid.NewGuid(),
            RegistrationId = registrationId,
            DocumentType = dto.DocumentType,
            DocumentName = dto.DocumentName,
            DocumentPath = dto.DocumentPath ?? dto.FilePath ?? string.Empty,
            FileSize = dto.FileSize,
            MimeType = dto.MimeType,
            IsVerified = false,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _documentRepository.CreateAsync(document);

        return new BusinessPartnerRegistrationDocumentDto
        {
            Id = created.Id,
            RegistrationId = created.RegistrationId,
            DocumentType = created.DocumentType,
            DocumentName = created.DocumentName,
            FilePath = created.DocumentPath,
            DocumentPath = created.DocumentPath,
            FileSize = created.FileSize ?? 0,
            IsVerified = created.IsVerified,
            UploadedAt = created.CreatedAt
        };
    }

    public async Task DeleteDocumentAsync(Guid registrationId, Guid documentId, Guid userId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
            throw new InvalidOperationException($"Document with ID {documentId} not found for registration {registrationId}");

        await _documentRepository.DeleteAsync(documentId);
    }

    public async Task VerifyDocumentAsync(Guid registrationId, Guid documentId, Guid verifiedById)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null || document.RegistrationId != registrationId)
            throw new InvalidOperationException($"Document with ID {documentId} not found for registration {registrationId}");

        await _documentRepository.VerifyDocumentAsync(documentId, verifiedById);
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
        var registration = await _registrationRepository.GetByIdAsync(registrationId);
        if (registration == null)
            throw new InvalidOperationException($"Registration with ID {registrationId} not found");

        if (string.IsNullOrEmpty(registration.RegistrationDataJson))
            throw new InvalidOperationException("Registration data is missing");

        // Deserialize registration data
        var registrationData = System.Text.Json.JsonSerializer.Deserialize<CreateBusinessPartnerRegistrationDto>(registration.RegistrationDataJson);
        if (registrationData == null)
            throw new InvalidOperationException("Failed to deserialize registration data");

        // Generate partner code
        var partnerCode = await _partnerRepository.GeneratePartnerCodeAsync(registration.PartnerType);

        // Create business partner
        var partner = new Entities.Procurement.BusinessPartner
        {
            Id = Guid.NewGuid(),
            PartnerCode = partnerCode,
            PartnerName = registrationData.CompanyName,
            PartnerType = registration.PartnerType,
            LegalName = registrationData.CompanyName,
            BusinessRegistrationNumber = registrationData.RegistrationNumber,
            TaxIdentificationNumber = registrationData.TaxNumber,
            VATNumber = registrationData.VatNumber,
            PrimaryEmail = registrationData.Email,
            PrimaryPhone = registrationData.Phone,
            Website = registrationData.Website,
            PhysicalAddress = registrationData.PhysicalAddress,
            PhysicalCity = registrationData.City,
            PhysicalCountry = registrationData.Country,
            PhysicalPostalCode = registrationData.PostalCode,
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsPreferred = false,
            IsBlacklisted = false,
            ApprovedById = approvedById,
            ApprovedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var createdPartner = await _partnerRepository.CreateAsync(partner);

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
            ApplicationNumber = registration.RegistrationNumber,
            CompanyName = registration.ApplicantName,
            Email = registration.ApplicantEmail,
            Phone = registration.ApplicantPhone,
            PartnerType = registration.PartnerType,
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
            ApplicationNumber = registration.RegistrationNumber,
            CompanyName = registration.ApplicantName,
            Email = registration.ApplicantEmail,
            Phone = registration.ApplicantPhone,
            PartnerType = registration.PartnerType,
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

        // Map documents if available
        if (registration.Documents != null && registration.Documents.Any())
        {
            dto.Documents = registration.Documents.Select(d => new BusinessPartnerRegistrationDocumentDto
            {
                Id = d.Id,
                RegistrationId = d.RegistrationId,
                DocumentType = d.DocumentType,
                DocumentName = d.DocumentName,
                FilePath = d.DocumentPath,
                DocumentPath = d.DocumentPath,
                FileSize = d.FileSize ?? 0,
                IsVerified = d.IsVerified,
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

    private int CalculateCompletionPercentage(Entities.Procurement.BusinessPartnerRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.RegistrationDataJson))
            return 0;

        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<CreateBusinessPartnerRegistrationDto>(registration.RegistrationDataJson);
            if (data == null) return 0;

            int totalFields = 10;
            int completedFields = 0;

            if (!string.IsNullOrEmpty(data.CompanyName)) completedFields++;
            if (!string.IsNullOrEmpty(data.Email)) completedFields++;
            if (!string.IsNullOrEmpty(data.Phone)) completedFields++;
            if (!string.IsNullOrEmpty(data.PartnerType)) completedFields++;
            if (!string.IsNullOrEmpty(data.PhysicalAddress)) completedFields++;
            if (!string.IsNullOrEmpty(data.City)) completedFields++;
            if (!string.IsNullOrEmpty(data.Country)) completedFields++;
            if (!string.IsNullOrEmpty(data.RegistrationNumber)) completedFields++;
            if (!string.IsNullOrEmpty(data.TaxNumber)) completedFields++;
            if (!string.IsNullOrEmpty(data.ContactPersonName)) completedFields++;

            return (int)((completedFields / (double)totalFields) * 100);
        }
        catch
        {
            return 0;
        }
    }
}
