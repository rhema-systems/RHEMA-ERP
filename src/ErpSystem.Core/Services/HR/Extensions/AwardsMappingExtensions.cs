using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class AwardsMappingExtensions
{
    #region AwardType Mappings

    public static AwardTypeDto ToDto(this AwardType entity)
    {
        return new AwardTypeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            IsTeamAward = entity.IsTeamAward,
            Frequency = entity.Frequency,
            MinServiceYears = entity.MinServiceYears,
            MaxServiceYears = entity.MaxServiceYears,
            MinAge = entity.MinAge,
            MaxAge = entity.MaxAge,
            MaxAwardsPerPeriod = entity.MaxAwardsPerPeriod,
            MaxAwardsPerEmployee = entity.MaxAwardsPerEmployee,
            HasMonetaryReward = entity.HasMonetaryReward,
            MinMonetaryAmount = entity.MinMonetaryAmount,
            MaxMonetaryAmount = entity.MaxMonetaryAmount,
            HasCertificate = entity.HasCertificate,
            HasTrophy = entity.HasTrophy,
            LeaveDaysBonus = entity.LeaveDaysBonus,
            HasLevels = entity.HasLevels,
            RequiresFormalReview = entity.RequiresFormalReview,
            MinRequiredReviewers = entity.MinRequiredReviewers,
            NominationSource = entity.NominationSource,
            WinnerDecision = entity.WinnerDecision,
            AllowSelfNomination = entity.AllowSelfNomination,
            DisqualifyOnDisciplinaryRecord = entity.DisqualifyOnDisciplinaryRecord,
            DisqualifyingDisciplineMonths = entity.DisqualifyingDisciplineMonths,
            MinPerformanceScore = entity.MinPerformanceScore,
            MinGoalsAchieved = entity.MinGoalsAchieved,
            Notes = entity.Notes,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardTypeSummaryDto ToSummaryDto(this AwardType entity, int awardCount = 0)
    {
        return new AwardTypeSummaryDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Category = entity.Category,
            IsTeamAward = entity.IsTeamAward,
            Frequency = entity.Frequency,
            HasMonetaryReward = entity.HasMonetaryReward,
            MinMonetaryAmount = entity.MinMonetaryAmount,
            MaxMonetaryAmount = entity.MaxMonetaryAmount,
            HasLevels = entity.HasLevels,
            RequiresFormalReview = entity.RequiresFormalReview,
            NominationSource = entity.NominationSource,
            WinnerDecision = entity.WinnerDecision,
            IsActive = entity.IsActive,
            AwardCount = awardCount
        };
    }

    public static AwardType ToEntity(this CreateAwardTypeDto dto, Guid tenantId, Guid userId)
    {
        return new AwardType
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            IsTeamAward = dto.IsTeamAward,
            Frequency = dto.Frequency,
            MinServiceYears = dto.MinServiceYears,
            MaxServiceYears = dto.MaxServiceYears,
            MinAge = dto.MinAge,
            MaxAge = dto.MaxAge,
            MaxAwardsPerPeriod = dto.MaxAwardsPerPeriod,
            MaxAwardsPerEmployee = dto.MaxAwardsPerEmployee,
            HasMonetaryReward = dto.HasMonetaryReward,
            MinMonetaryAmount = dto.MinMonetaryAmount,
            MaxMonetaryAmount = dto.MaxMonetaryAmount,
            HasCertificate = dto.HasCertificate,
            HasTrophy = dto.HasTrophy,
            LeaveDaysBonus = dto.LeaveDaysBonus,
            HasLevels = dto.HasLevels,
            RequiresFormalReview = dto.RequiresFormalReview,
            MinRequiredReviewers = dto.MinRequiredReviewers,
            NominationSource = dto.NominationSource,
            WinnerDecision = dto.WinnerDecision,
            AllowSelfNomination = dto.AllowSelfNomination,
            DisqualifyOnDisciplinaryRecord = dto.DisqualifyOnDisciplinaryRecord,
            DisqualifyingDisciplineMonths = dto.DisqualifyingDisciplineMonths,
            MinPerformanceScore = dto.MinPerformanceScore,
            MinGoalsAchieved = dto.MinGoalsAchieved,
            Notes = dto.Notes,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardType entity, UpdateAwardTypeDto dto, Guid userId)
    {
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.IsTeamAward = dto.IsTeamAward;
        entity.Frequency = dto.Frequency;
        entity.MinServiceYears = dto.MinServiceYears;
        entity.MaxServiceYears = dto.MaxServiceYears;
        entity.MinAge = dto.MinAge;
        entity.MaxAge = dto.MaxAge;
        entity.MaxAwardsPerPeriod = dto.MaxAwardsPerPeriod;
        entity.MaxAwardsPerEmployee = dto.MaxAwardsPerEmployee;
        entity.HasMonetaryReward = dto.HasMonetaryReward;
        entity.MinMonetaryAmount = dto.MinMonetaryAmount;
        entity.MaxMonetaryAmount = dto.MaxMonetaryAmount;
        entity.HasCertificate = dto.HasCertificate;
        entity.HasTrophy = dto.HasTrophy;
        entity.LeaveDaysBonus = dto.LeaveDaysBonus;
        entity.HasLevels = dto.HasLevels;
        entity.RequiresFormalReview = dto.RequiresFormalReview;
        entity.MinRequiredReviewers = dto.MinRequiredReviewers;
        entity.NominationSource = dto.NominationSource;
        entity.WinnerDecision = dto.WinnerDecision;
        entity.AllowSelfNomination = dto.AllowSelfNomination;
        entity.DisqualifyOnDisciplinaryRecord = dto.DisqualifyOnDisciplinaryRecord;
        entity.DisqualifyingDisciplineMonths = dto.DisqualifyingDisciplineMonths;
        entity.MinPerformanceScore = dto.MinPerformanceScore;
        entity.MinGoalsAchieved = dto.MinGoalsAchieved;
        entity.Notes = dto.Notes;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardTypeSummaryDto> ToSummaryDtoList(this IEnumerable<AwardType> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region EmployeeAward Mappings

    public static EmployeeAwardDto ToDto(this EmployeeAward entity)
    {
        return new EmployeeAwardDto
        {
            AwardLevelId = entity.AwardLevelId,
            AwardLevelName = entity.AwardLevel?.Name,
            AwardNominationId = entity.AwardNominationId,
            NominationNumber = entity.AwardNomination?.NominationNumber,
            AwardCycleId = entity.AwardCycleId,
            AwardCycleName = entity.AwardCycle?.Name,
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardNumber = entity.AwardNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            DepartmentName = entity.Employee?.Department?.Name,
            AwardTypeId = entity.AwardTypeId,
            AwardTypeName = entity.AwardType?.Name ?? string.Empty,
            AwardDate = entity.AwardDate,
            Reason = entity.AwardNomination?.Justification ?? string.Empty,
            Citation = entity.Citation,
            PresentationDate = entity.PresentationDate,
            PresentationVenue = entity.PresentationVenue,
            PresentedById = entity.PresentedById,
            PresentedByName = entity.PresentedBy != null 
                ? $"{entity.PresentedBy.FirstName} {entity.PresentedBy.LastName}" 
                : null,
            MonetaryAmount = entity.MonetaryAmount,
            CertificateNumber = entity.CertificateNumber,
            CertificateIssued = entity.CertificateIssued,
            TrophyIssued = entity.TrophyIssued,
            PaymentProcessed = entity.PaymentProcessed,
            PaymentDate = entity.PaymentDate,
            PaymentReference = entity.PaymentReference,
            PublishToIntranet = false, // Not in entity, default to false
            PublishToWebsite = entity.PublishToWebsite,
            PublicationNotes = entity.PublicationNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EmployeeAwardSummaryDto ToSummaryDto(this EmployeeAward entity)
    {
        return new EmployeeAwardSummaryDto
        {
            AwardLevelId = entity.AwardLevelId,
            AwardLevelName = entity.AwardLevel?.Name,
            Id = entity.Id,
            AwardNumber = entity.AwardNumber,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            AwardTypeName = entity.AwardType?.Name ?? string.Empty,
            AwardDate = entity.AwardDate,
            MonetaryAmount = entity.MonetaryAmount,
            PresentationDate = entity.PresentationDate
        };
    }

    public static EmployeeAwardDetailDto ToDetailDto(this EmployeeAward entity)
    {
        return new EmployeeAwardDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardNumber = entity.AwardNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            DepartmentName = entity.Employee?.Department?.Name,
            AwardTypeId = entity.AwardTypeId,
            AwardTypeName = entity.AwardType?.Name ?? string.Empty,
            AwardDate = entity.AwardDate,
            Reason = entity.AwardNomination?.Justification ?? string.Empty,
            Citation = entity.Citation,
            PresentationDate = entity.PresentationDate,
            PresentationVenue = entity.PresentationVenue,
            PresentedById = entity.PresentedById,
            PresentedByName = entity.PresentedBy != null 
                ? $"{entity.PresentedBy.FirstName} {entity.PresentedBy.LastName}" 
                : null,
            MonetaryAmount = entity.MonetaryAmount,
            CertificateNumber = entity.CertificateNumber,
            CertificateIssued = entity.CertificateIssued,
            TrophyIssued = entity.TrophyIssued,
            PaymentProcessed = entity.PaymentProcessed,
            PaymentDate = entity.PaymentDate,
            PaymentReference = entity.PaymentReference,
            PublishToIntranet = false,
            PublishToWebsite = entity.PublishToWebsite,
            PublicationNotes = entity.PublicationNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Attachments = entity.Attachments?.Select(a => a.ToDto()).ToList() ?? new List<AwardAttachmentDto>()
        };
    }

    public static EmployeeAward ToEntity(this CreateEmployeeAwardDto dto, Guid tenantId, Guid userId, string awardNumber)
    {
        return new EmployeeAward
        {
            TenantId = tenantId,
            AwardNumber = awardNumber,
            EmployeeId = dto.EmployeeId,
            AwardTypeId = dto.AwardTypeId,
            AwardLevelId = dto.AwardLevelId,
            AwardDate = dto.AwardDate,
            Citation = dto.Citation,
            MonetaryAmount = dto.MonetaryAmount,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this EmployeeAward entity, UpdateEmployeeAwardDto dto, Guid userId)
    {
        entity.EmployeeId = dto.EmployeeId;
        entity.AwardTypeId = dto.AwardTypeId;
        entity.AwardDate = dto.AwardDate;
        entity.Citation = dto.Citation;
        entity.MonetaryAmount = dto.MonetaryAmount;
        entity.CertificateNumber = dto.CertificateNumber;
        entity.CertificateIssued = dto.CertificateIssued;
        entity.TrophyIssued = dto.TrophyIssued;
        entity.PublishToWebsite = dto.PublishToWebsite;
        entity.PublicationNotes = dto.PublicationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<EmployeeAwardSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeAward> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region AwardAttachment Mappings

    public static AwardAttachmentDto ToDto(this AwardAttachment entity)
    {
        return new AwardAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardId = entity.AwardId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            FileSizeBytes = entity.FileSizeBytes,
            AttachmentType = entity.AttachmentType,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy == null
                ? null
                : $"{entity.UploadedBy.FirstName} {entity.UploadedBy.LastName}".Trim(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>
    /// Builds the row from the metadata the caller sent AND the file the gate stored.
    /// </summary>
    /// <remarks>
    /// ⚠ The file's own facts — name, path, size and the three gate ids — come from
    /// <c>HrControlledDocument</c>, never from the DTO. That separation is the fix for D-39: a
    /// caller can describe what a file IS, and cannot say where it lives.
    /// </remarks>
    public static AwardAttachment ToEntity(
        this CreateAwardAttachmentDto dto,
        Guid tenantId,
        Guid awardId,
        Guid uploadedById,
        Guid userId,
        string fileName,
        string filePath,
        long? fileSizeBytes,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId)
    {
        return new AwardAttachment
        {
            TenantId = tenantId,
            AwardId = awardId,
            FileName = fileName,
            FilePath = filePath,
            FileSizeBytes = fileSizeBytes,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            AttachmentType = dto.AttachmentType,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById,
            CreatedBy = userId.ToString()
        };
    }

    public static List<AwardAttachmentDto> ToDtoList(this IEnumerable<AwardAttachment> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardNomination Mappings

    public static AwardNominationDto ToDto(this AwardNomination entity)
    {
        return new AwardNominationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            NominationNumber = entity.NominationNumber,
            AwardTypeId = entity.AwardTypeId,
            AwardTypeName = entity.AwardType?.Name ?? string.Empty,
            AwardCycleId = entity.AwardCycleId,
            AwardCycleName = entity.AwardCycle?.Name,
            CommitteeId = entity.CommitteeId,
            CommitteeName = entity.Committee?.Name,
            AwardLevelId = entity.AwardLevelId,
            AwardLevelName = entity.AwardLevel?.Name,
            NomineeId = entity.NomineeId,
            NomineeName = entity.Nominee != null 
                ? $"{entity.Nominee.FirstName} {entity.Nominee.LastName}" 
                : string.Empty,
            NomineeEmployeeNumber = entity.Nominee?.EmployeeNumber,
            NomineeDepartment = entity.Nominee?.Department?.Name,
            NominatedById = entity.NominatedById,
            NominatedByName = entity.NominatedBy != null 
                ? $"{entity.NominatedBy.FirstName} {entity.NominatedBy.LastName}" 
                : string.Empty,
            NominationDate = entity.NominationDate,
            Year = entity.Year,
            TeamName = entity.TeamName,
            Quarter = entity.Quarter,
            Month = entity.Month,
            Justification = entity.Justification,
            ProposedMonetaryAmount = entity.ProposedMonetaryAmount,
            ProposedLeaveDays = entity.ProposedLeaveDays,
            Status = entity.Status,
            OutcomeDate = entity.OutcomeDate,
            OutcomeReason = entity.OutcomeReason,
            AwardId = entity.EmployeeAwardId,
            AwardNumber = entity.Award?.AwardNumber,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardNominationSummaryDto ToSummaryDto(this AwardNomination entity)
    {
        return new AwardNominationSummaryDto
        {
            Id = entity.Id,
            NominationNumber = entity.NominationNumber,
            AwardTypeId = entity.AwardTypeId,
            AwardTypeName = entity.AwardType?.Name ?? string.Empty,
            NomineeName = entity.Nominee != null 
                ? $"{entity.Nominee.FirstName} {entity.Nominee.LastName}" 
                : string.Empty,
            NominatedByName = entity.NominatedBy != null 
                ? $"{entity.NominatedBy.FirstName} {entity.NominatedBy.LastName}" 
                : string.Empty,
            NominationDate = entity.NominationDate,
            Year = entity.Year,
            TeamName = entity.TeamName,
            Quarter = entity.Quarter,
            Month = entity.Month,
            Status = entity.Status
        };
    }

    public static AwardNomination ToEntity(this CreateAwardNominationDto dto, Guid tenantId, Guid nominatedById, Guid userId, string nominationNumber)
    {
        return new AwardNomination
        {
            TenantId = tenantId,
            NominationNumber = nominationNumber,
            AwardTypeId = dto.AwardTypeId,
            AwardCycleId = dto.AwardCycleId,
            AwardLevelId = dto.AwardLevelId,
            NomineeId = dto.NomineeId,
            NominatedById = nominatedById,
            TeamName = dto.TeamName,
            NominationDate = DateTime.UtcNow,
            Year = dto.Year,
            Quarter = dto.Quarter,
            Month = dto.Month,
            Justification = dto.Justification,
            ProposedMonetaryAmount = dto.ProposedMonetaryAmount,
            ProposedLeaveDays = dto.ProposedLeaveDays,
            Status = AwardNominationStatus.Draft,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardNomination entity, UpdateAwardNominationDto dto, Guid userId)
    {
        entity.AwardTypeId = dto.AwardTypeId;
        entity.AwardLevelId = dto.AwardLevelId;
        entity.NomineeId = dto.NomineeId;
        entity.TeamName = dto.TeamName;
        entity.Year = dto.Year;
        entity.Quarter = dto.Quarter;
        entity.Month = dto.Month;
        entity.Justification = dto.Justification;
        entity.ProposedMonetaryAmount = dto.ProposedMonetaryAmount;
        entity.ProposedLeaveDays = dto.ProposedLeaveDays;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
        entity.AwardTypeId = dto.AwardTypeId;
        entity.AwardLevelId = dto.AwardLevelId;
        entity.NomineeId = dto.NomineeId;
        entity.TeamName = string.IsNullOrWhiteSpace(dto.TeamName) ? null : dto.TeamName.Trim();
        entity.Year = dto.Year;
        entity.Quarter = dto.Quarter;
        entity.Month = dto.Month;
    }

    public static List<AwardNominationSummaryDto> ToSummaryDtoList(this IEnumerable<AwardNomination> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region Award Nomination Contribution Mappings

    public static AwardNomineeContributionDto ToDto(this AwardNomineeContribution entity)
    {
        return new AwardNomineeContributionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardNominationId = entity.AwardNominationId,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardNomineeContribution ToEntity(this CreateAwardNomineeContributionDto dto, Guid tenantId, Guid nominationId, Guid userId)
    {
        return new AwardNomineeContribution
        {
            TenantId = tenantId,
            AwardNominationId = nominationId,
            Description = dto.Description,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardNomineeContribution entity, UpdateAwardNomineeContributionDto dto, Guid userId)
    {
        entity.Description = dto.Description;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardNomineeContributionDto> ToDtoList(this IEnumerable<AwardNomineeContribution> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region Award Nomination Attachment Mappings

    public static AwardNominationAttachmentDto ToDto(this AwardNominationAttachment entity)
    {
        return new AwardNominationAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardNominationId = entity.AwardNominationId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            FileSizeBytes = entity.FileSizeBytes,
            AttachmentType = entity.AttachmentType,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy == null
                ? null
                : $"{entity.UploadedBy.FirstName} {entity.UploadedBy.LastName}".Trim(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>The caller describes the file; the GATE says where it is. See D-39.</summary>
    public static AwardNominationAttachment ToEntity(
        this CreateAwardNominationAttachmentDto dto,
        Guid tenantId,
        Guid nominationId,
        Guid uploadedById,
        Guid userId,
        string fileName,
        string filePath,
        long? fileSizeBytes,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId)
    {
        return new AwardNominationAttachment
        {
            TenantId = tenantId,
            AwardNominationId = nominationId,
            FileName = fileName,
            FilePath = filePath,
            FileSizeBytes = fileSizeBytes,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
            AttachmentType = dto.AttachmentType,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardNominationAttachment entity, UpdateAwardNominationAttachmentDto dto, Guid userId)
    {
        entity.AttachmentType = dto.AttachmentType;
        entity.Description = dto.Description;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardNominationAttachmentDto> ToDtoList(this IEnumerable<AwardNominationAttachment> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardLevel Mappings

    public static AwardLevelDto ToDto(this AwardLevel entity)
    {
        return new AwardLevelDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardTypeId = entity.AwardTypeId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            Rank = entity.Rank,
            MonetaryAmount = entity.MonetaryAmount,
            LeaveDaysBonus = entity.LeaveDaysBonus,
            Benefits = entity.Benefits,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardLevel ToEntity(this CreateAwardLevelDto dto, Guid tenantId, Guid userId)
    {
        return new AwardLevel
        {
            TenantId = tenantId,
            AwardTypeId = dto.AwardTypeId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Rank = dto.Rank,
            MonetaryAmount = dto.MonetaryAmount,
            LeaveDaysBonus = dto.LeaveDaysBonus,
            Benefits = dto.Benefits,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardLevel entity, UpdateAwardLevelDto dto, Guid userId)
    {
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Rank = dto.Rank;
        entity.MonetaryAmount = dto.MonetaryAmount;
        entity.LeaveDaysBonus = dto.LeaveDaysBonus;
        entity.Benefits = dto.Benefits;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardLevelDto> ToDtoList(this IEnumerable<AwardLevel> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardTypeTarget Mappings

    public static AwardTypeTargetDto ToDto(this AwardTypeTarget entity)
    {
        return new AwardTypeTargetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardTypeId = entity.AwardTypeId,
            Purpose = entity.Purpose,
            TargetType = entity.TargetType,
            TargetId = entity.TargetId,
            MinAge = entity.MinAge,
            MaxAge = entity.MaxAge,
            IsExclusion = entity.IsExclusion,
            Reason = entity.Reason,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static AwardTypeTarget ToEntity(this CreateAwardTypeTargetDto dto, Guid tenantId, Guid userId)
    {
        return new AwardTypeTarget
        {
            TenantId = tenantId,
            AwardTypeId = dto.AwardTypeId,
            Purpose = dto.Purpose,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            MinAge = dto.MinAge,
            MaxAge = dto.MaxAge,
            IsExclusion = dto.IsExclusion,
            Reason = dto.Reason,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardTypeTarget entity, UpdateAwardTypeTargetDto dto, Guid userId)
    {
        entity.Purpose = dto.Purpose;
        entity.TargetType = dto.TargetType;
        entity.TargetId = dto.TargetId;
        entity.MinAge = dto.MinAge;
        entity.MaxAge = dto.MaxAge;
        entity.IsExclusion = dto.IsExclusion;
        entity.Reason = dto.Reason;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardTypeTargetDto> ToDtoList(this IEnumerable<AwardTypeTarget> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardBudget Mappings

    public static AwardBudgetDto ToDto(this AwardBudget entity)
    {
        return new AwardBudgetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardTypeId = entity.AwardTypeId,
            AwardTypeName = entity.AwardType?.Name ?? string.Empty,
            Year = entity.Year,
            BudgetCode = entity.BudgetCode,
            BudgetAmount = entity.BudgetAmount,
            SpentAmount = entity.SpentAmount,
            ReservedAmount = entity.ReservedAmount,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardBudget ToEntity(this CreateAwardBudgetDto dto, Guid tenantId, Guid userId)
    {
        return new AwardBudget
        {
            TenantId = tenantId,
            AwardTypeId = dto.AwardTypeId,
            Year = dto.Year,
            BudgetCode = dto.BudgetCode,
            BudgetAmount = dto.BudgetAmount,
            SpentAmount = 0,
            ReservedAmount = 0,
            Notes = dto.Notes,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardBudget entity, UpdateAwardBudgetDto dto, Guid userId)
    {
        entity.BudgetAmount = dto.BudgetAmount;
        entity.SpentAmount = dto.SpentAmount;
        entity.ReservedAmount = dto.ReservedAmount;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardBudgetDto> ToDtoList(this IEnumerable<AwardBudget> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region TeamAwardNominee Mappings

    public static TeamAwardNomineeDto ToDto(this TeamAwardNominee entity)
    {
        return new TeamAwardNomineeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            NominationId = entity.NominationId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            Role = entity.Role,
            ContributionSummary = entity.ContributionSummary,
            RewardPercentage = entity.RewardPercentage,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static TeamAwardNominee ToEntity(this CreateTeamAwardNomineeDto dto, Guid nominationId, Guid employeeId, Guid userId)
    {
        return new TeamAwardNominee
        {
            NominationId = nominationId,
            EmployeeId = employeeId,
            Role = dto.Role,
            ContributionSummary = dto.ContributionSummary,
            RewardPercentage = dto.RewardPercentage,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this TeamAwardNominee entity, UpdateTeamAwardNomineeDto dto, Guid userId)
    {
        entity.Role = dto.Role;
        entity.ContributionSummary = dto.ContributionSummary;
        entity.RewardPercentage = dto.RewardPercentage;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<TeamAwardNomineeDto> ToDtoList(this IEnumerable<TeamAwardNominee> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardCommittee Mappings

    public static AwardCommitteeDto ToDto(this AwardCommittee entity)
    {
        return new AwardCommitteeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Description = entity.Description,
            QuorumRequired = entity.QuorumRequired,
            ReviewDeadlineDays = entity.ReviewDeadlineDays,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            IsActive = entity.IsActive,
            MemberCount = entity.Members?.Count ?? 0,
            Members = entity.Members?.Select(m => m.ToDto()).ToList()!,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardCommittee ToEntity(this CreateAwardCommitteeDto dto, Guid tenantId, Guid userId)
    {
        return new AwardCommittee
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            QuorumRequired = dto.QuorumRequired,
            ReviewDeadlineDays = dto.ReviewDeadlineDays,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardCommittee entity, UpdateAwardCommitteeDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.QuorumRequired = dto.QuorumRequired;
        entity.ReviewDeadlineDays = dto.ReviewDeadlineDays;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardCommitteeDto> ToDtoList(this IEnumerable<AwardCommittee> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardCommitteeMember Mappings

    public static AwardCommitteeMemberDto ToDto(this AwardCommitteeMember entity)
    {
        return new AwardCommitteeMemberDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CommitteeId = entity.CommitteeId,
            CommitteeName = entity.Committee?.Name!,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee != null
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}"
                : string.Empty,
            // ⚠ The DTO has carried this field since the area was ported and the mapper never set
            // it, so every committee membership list showed names beside a blank number column. The
            // navigation is loaded already — the name next to it proves that — which is why nothing
            // ever failed. Found by the slice-11 payload probe, not by a symptom.
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            Role = entity.Role,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardCommitteeMember ToEntity(this CreateAwardCommitteeMemberDto dto, Guid committeeId, Guid userId)
    {
        return new AwardCommitteeMember
        {
            CommitteeId = committeeId,
            EmployeeId = dto.EmployeeId,
            Role = dto.Role!,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardCommitteeMember entity, UpdateAwardCommitteeMemberDto dto, Guid userId)
    {
        entity.Role = dto.Role!;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardCommitteeMemberDto> ToDtoList(this IEnumerable<AwardCommitteeMember> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardNominationReview Mappings

    public static AwardCommitteeReviewDto ToDto(this AwardNominationReview entity)
    {
        return new AwardCommitteeReviewDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardNominationId = entity.AwardNominationId,
            NominationNumber = entity.AwardNomination?.NominationNumber ?? string.Empty,
            ReviewerId = entity.ReviewerId,
            ReviewerName = entity.Reviewer != null 
                ? $"{entity.Reviewer.FirstName} {entity.Reviewer.LastName}" 
                : string.Empty,
            ReviewDate = entity.ReviewDate,
            Score = entity.Score,
            Comments = entity.Comments,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AwardNominationReview ToEntity(this SubmitCommitteeReviewDto dto, Guid nominationId, Guid reviewerId, Guid tenantId, Guid userId)
    {
        return new AwardNominationReview
        {
            TenantId = tenantId,
            AwardNominationId = nominationId,
            ReviewerId = reviewerId,
            ReviewDate = DateTime.UtcNow,
            Score = dto.Score,
            Comments = dto.Comments,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AwardNominationReview entity, UpdateCommitteeReviewDto dto, Guid userId)
    {
        entity.Score = dto.Score;
        entity.Comments = dto.Comments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AwardCommitteeReviewDto> ToDtoList(this IEnumerable<AwardNominationReview> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AwardNominationDetailDto Mapping

    public static AwardNominationDetailDto ToDetailDto(this AwardNomination entity)
    {
        var baseDto = entity.ToDto();
        return new AwardNominationDetailDto
        {
            Id = baseDto.Id,
            TenantId = baseDto.TenantId,
            NominationNumber = baseDto.NominationNumber,
            AwardTypeId = baseDto.AwardTypeId,
            AwardTypeName = baseDto.AwardTypeName,
            NomineeId = baseDto.NomineeId,
            NomineeName = baseDto.NomineeName,
            NomineeEmployeeNumber = baseDto.NomineeEmployeeNumber,
            NomineeDepartment = baseDto.NomineeDepartment,
            NominatedById = baseDto.NominatedById,
            NominatedByName = baseDto.NominatedByName,
            NominationDate = baseDto.NominationDate,
            Year = baseDto.Year,
            Justification = baseDto.Justification,
            Status = baseDto.Status,
            OutcomeDate = baseDto.OutcomeDate,
            OutcomeReason = baseDto.OutcomeReason,
            AwardId = baseDto.AwardId,
            AwardNumber = baseDto.AwardNumber,
            CreatedAt = baseDto.CreatedAt,
            CreatedBy = baseDto.CreatedBy,
            UpdatedAt = baseDto.UpdatedAt,
            UpdatedBy = baseDto.UpdatedBy,
            CommitteeId = entity.CommitteeId,
            CommitteeName = entity.Committee?.Name,
            AwardLevelId = entity.AwardLevelId,
            AwardLevelName = entity.AwardLevel?.Name,
            TeamNominees = entity.TeamNominees?.Select(n => n.ToDto()).ToList() ?? new List<TeamAwardNomineeDto>(),
            CommitteeReviews = entity.Reviews?.Select(r => r.ToDto()).ToList() ?? new List<AwardCommitteeReviewDto>()
        };
    }

    #endregion

    #region CreateEmployeeAwardFromNominationDto Mapping

    public static EmployeeAward ToEntity(this CreateEmployeeAwardFromNominationDto dto, AwardNomination nomination, Guid tenantId, Guid userId, string awardNumber)
    {
        return new EmployeeAward
        {
            TenantId = tenantId,
            AwardNumber = awardNumber,
            EmployeeId = nomination.NomineeId ?? Guid.Empty,
            AwardTypeId = nomination.AwardTypeId,
            AwardLevelId = nomination.AwardLevelId,
            AwardNominationId = nomination.Id,
            AwardDate = dto.AwardDate,
            Citation = !string.IsNullOrWhiteSpace(dto.AdditionalCitation) 
                ? $"{nomination.Justification}\n\n{dto.AdditionalCitation}" 
                : nomination.Justification,
            MonetaryAmount = dto.MonetaryAmount,
            CreatedBy = userId.ToString()
        };
    }

    #endregion

    #region LongServiceAward Mappings

    public static LongServiceAwardDto ToDto(this LongServiceAward entity)
    {
        return new LongServiceAwardDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            DepartmentName = entity.Employee?.Department?.Name,
            AwardTypeId = entity.AwardTypeId,
            AwardTypeName = entity.AwardType?.Name,
            EmployeeAwardId = entity.EmployeeAwardId,
            YearsOfService = entity.YearsOfService,
            ServiceStartDate = entity.ServiceStartDate,
            MilestoneDate = entity.MilestoneDate,
            AwardDescription = entity.AwardDescription,
            MonetaryAmount = entity.MonetaryAmount,
            LeaveDaysBonus = entity.LeaveDaysBonus,
            OtherBenefits = entity.OtherBenefits,
            IsProcessed = entity.IsProcessed,
            ProcessedDate = entity.ProcessedDate,
            PresentationDate = entity.PresentationDate,
            PresentationNotes = entity.PresentationNotes,
            PaymentProcessed = entity.PaymentProcessed,
            PaymentDate = entity.PaymentDate,
            PaymentReference = entity.PaymentReference,
            LeaveProcessed = entity.LeaveProcessed,
            LeaveProcessedDate = entity.LeaveProcessedDate,
            LeaveId = entity.LeaveId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static LongServiceAwardSummaryDto ToSummaryDto(this LongServiceAward entity)
    {
        return new LongServiceAwardSummaryDto
        {
            Id = entity.Id,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            YearsOfService = entity.YearsOfService,
            MilestoneDate = entity.MilestoneDate,
            MonetaryAmount = entity.MonetaryAmount,
            IsProcessed = entity.IsProcessed,
            PresentationDate = entity.PresentationDate
        };
    }

    public static LongServiceAward ToEntity(this CreateLongServiceAwardDto dto, Guid tenantId, Guid userId)
    {
        return new LongServiceAward
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            AwardTypeId = dto.AwardTypeId,
            EmployeeAwardId = dto.EmployeeAwardId,
            YearsOfService = dto.YearsOfService,
            ServiceStartDate = dto.ServiceStartDate,
            MilestoneDate = dto.MilestoneDate,
            AwardDescription = dto.AwardDescription,
            MonetaryAmount = dto.MonetaryAmount,
            LeaveDaysBonus = dto.LeaveDaysBonus,
            OtherBenefits = dto.OtherBenefits,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this LongServiceAward entity, UpdateLongServiceAwardDto dto, Guid userId)
    {
        entity.EmployeeAwardId = dto.EmployeeAwardId;
        entity.AwardDescription = dto.AwardDescription;
        entity.MonetaryAmount = dto.MonetaryAmount;
        entity.LeaveDaysBonus = dto.LeaveDaysBonus;
        entity.OtherBenefits = dto.OtherBenefits;
        entity.IsProcessed = dto.IsProcessed;
        entity.PresentationDate = dto.PresentationDate;
        entity.PresentationNotes = dto.PresentationNotes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<LongServiceAwardSummaryDto> ToSummaryDtoList(this IEnumerable<LongServiceAward> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #region LongServiceMilestone Mappings

    public static LongServiceMilestoneDto ToDto(this LongServiceMilestone entity)
    {
        return new LongServiceMilestoneDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AwardTypeId = entity.AwardTypeId,
            AwardTypeName = entity.AwardType?.Name,
            Years = entity.Years,
            Name = entity.Name,
            MonetaryAmount = entity.MonetaryAmount,
            LeaveDaysBonus = entity.LeaveDaysBonus,
            Benefits = entity.Benefits,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    #endregion

    #endregion
}



