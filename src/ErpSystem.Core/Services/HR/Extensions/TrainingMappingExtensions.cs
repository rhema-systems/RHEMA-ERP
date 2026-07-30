using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class TrainingMappingExtensions
{
    // ========================================================================
    // TRAINING VENDOR
    // ========================================================================

    #region TrainingVendor

    public static TrainingVendorDto ToDto(this TrainingVendor entity)
    {
        return new TrainingVendorDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            VendorCode = entity.VendorCode,
            Name = entity.Name,
            VendorType = entity.VendorType,
            IsActive = entity.IsActive,
            IsPreferred = entity.IsPreferred,
            PreferredSince = entity.PreferredSince,
            IsBlacklisted = entity.IsBlacklisted,
            BlacklistedDate = entity.BlacklistedDate,
            BlacklistReason = entity.BlacklistReason,
            AccreditationBody = entity.AccreditationBody,
            AccreditationNumber = entity.AccreditationNumber,
            AccreditationStatus = entity.AccreditationStatus,
            AccreditationExpiryDate = entity.AccreditationExpiryDate,
            PrimaryContactName = entity.PrimaryContactName,
            PrimaryContactEmail = entity.PrimaryContactEmail,
            PrimaryContactPhone = entity.PrimaryContactPhone,
            Website = entity.Website,
            Address = entity.Address,
            Currency = entity.Currency,
            DefaultDailyRate = entity.DefaultDailyRate,
            ContractReference = entity.ContractReference,
            ContractDocumentPath = entity.ContractDocumentPath,
            ContractStartDate = entity.ContractStartDate,
            ContractEndDate = entity.ContractEndDate,
            Notes = entity.Notes,
            TotalTrainersCount = entity.Trainers.Count,
        };
    }

    public static TrainingVendorSummaryDto ToSummaryDto(this TrainingVendor entity)
    {
        return new TrainingVendorSummaryDto
        {
            Id = entity.Id,
            VendorCode = entity.VendorCode,
            Name = entity.Name,
            VendorType = entity.VendorType,
            IsActive = entity.IsActive,
            IsPreferred = entity.IsPreferred,
            IsBlacklisted = entity.IsBlacklisted,
            AccreditationStatus = entity.AccreditationStatus,
            PrimaryContactName = entity.PrimaryContactName,
            PrimaryContactEmail = entity.PrimaryContactEmail,
            TotalTrainersCount = entity.Trainers.Count,
        };
    }

    public static TrainingVendor ToEntity(this CreateTrainingVendorDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingVendor
        {
            TenantId = tenantId,
            VendorCode = dto.VendorCode,
            Name = dto.Name,
            VendorType = dto.VendorType,
            IsActive = dto.IsActive,
            IsPreferred = dto.IsPreferred,
            PreferredSince = dto.PreferredSince,
            AccreditationBody = dto.AccreditationBody,
            AccreditationNumber = dto.AccreditationNumber,
            AccreditationStatus = dto.AccreditationStatus,
            AccreditationExpiryDate = dto.AccreditationExpiryDate,
            PrimaryContactName = dto.PrimaryContactName,
            PrimaryContactEmail = dto.PrimaryContactEmail,
            PrimaryContactPhone = dto.PrimaryContactPhone,
            Website = dto.Website,
            Address = dto.Address,
            Currency = dto.Currency,
            DefaultDailyRate = dto.DefaultDailyRate,
            ContractReference = dto.ContractReference,
            ContractDocumentPath = dto.ContractDocumentPath,
            ContractStartDate = dto.ContractStartDate,
            ContractEndDate = dto.ContractEndDate,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingVendor entity, UpdateTrainingVendorDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.VendorType = dto.VendorType;
        entity.IsActive = dto.IsActive;
        entity.IsPreferred = dto.IsPreferred;
        entity.PreferredSince = dto.PreferredSince;
        entity.AccreditationBody = dto.AccreditationBody;
        entity.AccreditationNumber = dto.AccreditationNumber;
        entity.AccreditationStatus = dto.AccreditationStatus;
        entity.AccreditationExpiryDate = dto.AccreditationExpiryDate;
        entity.PrimaryContactName = dto.PrimaryContactName;
        entity.PrimaryContactEmail = dto.PrimaryContactEmail;
        entity.PrimaryContactPhone = dto.PrimaryContactPhone;
        entity.Website = dto.Website;
        entity.Address = dto.Address;
        entity.Currency = dto.Currency;
        entity.DefaultDailyRate = dto.DefaultDailyRate;
        entity.ContractReference = dto.ContractReference;
        entity.ContractDocumentPath = dto.ContractDocumentPath;
        entity.ContractStartDate = dto.ContractStartDate;
        entity.ContractEndDate = dto.ContractEndDate;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingVendorSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingVendor> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINER PROFILE
    // ========================================================================

    #region TrainerProfile

    public static TrainerProfileDto ToDto(this TrainerProfile entity)
    {
        return new TrainerProfileDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,
            Bio = entity.Bio,
            Contact = entity.Contact,
            ExpertiseAreas = entity.ExpertiseAreas,
            TotalTrainingHoursDelivered = entity.TotalTrainingHoursDelivered,
            TotalSessionsDelivered = entity.TotalSessionsDelivered,
            AverageRating = entity.AverageRating,
            TotalRatingsCount = entity.TotalRatingsCount,
            IsActive = entity.IsActive,
            Skills = entity.TrainerSkills.Select(s => s.ToDto()).ToList(),
            Availability = entity.Availability.Select(a => a.ToDto()).ToList(),
        };
    }

    public static TrainerProfileSummaryDto ToSummaryDto(this TrainerProfile entity)
    {
        return new TrainerProfileSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,
            ExpertiseAreas = entity.ExpertiseAreas,
            AverageRating = entity.AverageRating,
            TotalSessionsDelivered = entity.TotalSessionsDelivered,
            IsActive = entity.IsActive,
        };
    }

    public static TrainerProfile ToEntity(this CreateTrainerProfileDto dto, Guid tenantId, Guid userId)
    {
        return new TrainerProfile
        {
            TenantId = tenantId,
            Name = dto.Name,
            EmployeeId = dto.EmployeeId,
            VendorId = dto.VendorId,
            Bio = dto.Bio,
            Contact = dto.Contact,
            ExpertiseAreas = dto.ExpertiseAreas,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainerProfile entity, UpdateTrainerProfileDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.EmployeeId = dto.EmployeeId;
        entity.VendorId = dto.VendorId;
        entity.Bio = dto.Bio;
        entity.Contact = dto.Contact;
        entity.ExpertiseAreas = dto.ExpertiseAreas;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainerProfileSummaryDto> ToSummaryDtoList(this IEnumerable<TrainerProfile> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINER SKILL
    // ========================================================================

    #region TrainerSkill

    public static TrainerSkillDto ToDto(this TrainerSkill entity)
    {
        return new TrainerSkillDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TrainerProfileId = entity.TrainerProfileId,
            TrainerName = entity.TrainerProfile?.Name ?? string.Empty,
            SkillId = entity.SkillId,
            SkillName = entity.Skill?.Name ?? string.Empty,
            SkillCategory = entity.Skill?.Category.ToString(),
            TrainerProficiency = entity.TrainerProficiency,
            IsCertifiedToTrain = entity.IsCertifiedToTrain,
            CertificateNumber = entity.CertificateNumber,
            CertificateName = entity.CertificateName,
            CertificateFilePath = entity.CertificateFilePath,
        };
    }

    public static TrainerSkill ToEntity(this CreateTrainerSkillDto dto, Guid tenantId, Guid userId)
    {
        return new TrainerSkill
        {
            TenantId = tenantId,
            TrainerProfileId = dto.TrainerProfileId,
            SkillId = dto.SkillId,
            TrainerProficiency = dto.TrainerProficiency,
            IsCertifiedToTrain = dto.IsCertifiedToTrain,
            CertificateNumber = dto.CertificateNumber,
            CertificateName = dto.CertificateName,
            CertificateFilePath = dto.CertificateFilePath,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainerSkill entity, UpdateTrainerSkillDto dto, Guid userId)
    {
        entity.TrainerProficiency = dto.TrainerProficiency;
        entity.IsCertifiedToTrain = dto.IsCertifiedToTrain;
        entity.CertificateNumber = dto.CertificateNumber;
        entity.CertificateName = dto.CertificateName;
        entity.CertificateFilePath = dto.CertificateFilePath;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TRAINER AVAILABILITY
    // ========================================================================

    #region TrainerAvailability

    public static TrainerAvailabilityDto ToDto(this TrainerAvailability entity)
    {
        return new TrainerAvailabilityDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TrainerProfileId = entity.TrainerProfileId,
            TrainerName = entity.TrainerProfile?.Name ?? string.Empty,
            FromDate = entity.FromDate,
            ToDate = entity.ToDate,
            IsAvailable = entity.IsAvailable,
            EngagementType = entity.EngagementType,
            Notes = entity.Notes,
        };
    }

    public static TrainerAvailability ToEntity(this CreateTrainerAvailabilityDto dto, Guid tenantId, Guid userId)
    {
        return new TrainerAvailability
        {
            TenantId = tenantId,
            TrainerProfileId = dto.TrainerProfileId,
            FromDate = dto.FromDate,
            ToDate = dto.ToDate,
            IsAvailable = dto.IsAvailable,
            EngagementType = dto.EngagementType,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainerAvailability entity, UpdateTrainerAvailabilityDto dto, Guid userId)
    {
        entity.FromDate = dto.FromDate;
        entity.ToDate = dto.ToDate;
        entity.IsAvailable = dto.IsAvailable;
        entity.EngagementType = dto.EngagementType;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TRAINING PROGRAM
    // ========================================================================

    #region TrainingProgram

    public static TrainingProgramDto ToDto(this TrainingProgram entity)
    {
        return new TrainingProgramDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramCode = entity.ProgramCode,
            ProgramName = entity.ProgramName,
            Description = entity.Description,
            CategoryOptionId = entity.CategoryOptionId,
            CategoryName = entity.CategoryOption?.Name,
            CategoryColor = entity.CategoryOption?.ColorHex,
            ProgramGroupId = entity.ProgramGroupId,
            GroupName = entity.ProgramGroup?.Name,
            GroupColor = entity.ProgramGroup?.ColorHex,
            Type = entity.Type,
            Source = entity.Source,
            Level = entity.Level,
            DurationDays = entity.DurationDays,
            DurationHours = entity.DurationHours,
            Prerequisites = entity.Prerequisites,
            LearningObjectives = entity.LearningObjectives,
            CostPerParticipant = entity.CostPerParticipant,
            Currency = entity.Currency,
            IncludesAccommodation = entity.IncludesAccommodation,
            IncludesMeals = entity.IncludesMeals,
            IncludesTransport = entity.IncludesTransport,
            ProvidesCertificate = entity.ProvidesCertificate,
            CertificateName = entity.CertificateName,
            CertificateValidityMonths = entity.CertificateValidityMonths,
            MinParticipants = entity.MinParticipants,
            MaxParticipants = entity.MaxParticipants,
            IsActive = entity.IsActive,
            RequiresApproval = entity.RequiresApproval,
            RequiresServiceBond = entity.RequiresServiceBond,
            ServiceBondMonths = entity.ServiceBondMonths,
            ServiceBondTerms = entity.ServiceBondTerms,
            Competencies = entity.Competencies.Select(c => c.ToDto()).ToList(),
            Skills = entity.Skills.Select(s => s.ToDto()).ToList(),
            Materials = entity.Materials.Select(m => m.ToDto()).ToList(),
        };
    }

    public static TrainingProgramSummaryDto ToSummaryDto(this TrainingProgram entity)
    {
        return new TrainingProgramSummaryDto
        {
            Id = entity.Id,
            ProgramCode = entity.ProgramCode,
            ProgramName = entity.ProgramName,
            CategoryOptionId = entity.CategoryOptionId,
            CategoryName = entity.CategoryOption?.Name,
            CategoryColor = entity.CategoryOption?.ColorHex,
            ProgramGroupId = entity.ProgramGroupId,
            GroupName = entity.ProgramGroup?.Name,
            GroupColor = entity.ProgramGroup?.ColorHex,
            Type = entity.Type,
            Source = entity.Source,
            Level = entity.Level,
            DurationDays = entity.DurationDays,
            DurationHours = entity.DurationHours,
            CostPerParticipant = entity.CostPerParticipant,
            Currency = entity.Currency,
            ProvidesCertificate = entity.ProvidesCertificate,
            IsActive = entity.IsActive,
            RequiresApproval = entity.RequiresApproval,
        };
    }

    public static TrainingProgram ToEntity(this CreateTrainingProgramDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingProgram
        {
            TenantId = tenantId,
            ProgramCode = dto.ProgramCode,
            ProgramName = dto.ProgramName,
            Description = dto.Description,
            CategoryOptionId = dto.CategoryOptionId,
            ProgramGroupId = dto.ProgramGroupId,
            Type = dto.Type,
            Source = dto.Source,
            Level = dto.Level,
            DurationDays = dto.DurationDays,
            DurationHours = dto.DurationHours,
            Prerequisites = dto.Prerequisites,
            LearningObjectives = dto.LearningObjectives,
            CostPerParticipant = dto.CostPerParticipant,
            Currency = dto.Currency,
            IncludesAccommodation = dto.IncludesAccommodation,
            IncludesMeals = dto.IncludesMeals,
            IncludesTransport = dto.IncludesTransport,
            ProvidesCertificate = dto.ProvidesCertificate,
            CertificateName = dto.CertificateName,
            CertificateValidityMonths = dto.CertificateValidityMonths,
            MinParticipants = dto.MinParticipants,
            MaxParticipants = dto.MaxParticipants,
            IsActive = dto.IsActive,
            RequiresApproval = dto.RequiresApproval,
            RequiresServiceBond = dto.RequiresServiceBond,
            ServiceBondMonths = dto.ServiceBondMonths,
            ServiceBondTerms = dto.ServiceBondTerms,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingProgram entity, UpdateTrainingProgramDto dto, Guid userId)
    {
        entity.ProgramName = dto.ProgramName;
        entity.Description = dto.Description;
        entity.CategoryOptionId = dto.CategoryOptionId;
        entity.ProgramGroupId = dto.ProgramGroupId;
        entity.Type = dto.Type;
        entity.Source = dto.Source;
        entity.Level = dto.Level;
        entity.DurationDays = dto.DurationDays;
        entity.DurationHours = dto.DurationHours;
        entity.Prerequisites = dto.Prerequisites;
        entity.LearningObjectives = dto.LearningObjectives;
        entity.CostPerParticipant = dto.CostPerParticipant;
        entity.Currency = dto.Currency;
        entity.IncludesAccommodation = dto.IncludesAccommodation;
        entity.IncludesMeals = dto.IncludesMeals;
        entity.IncludesTransport = dto.IncludesTransport;
        entity.ProvidesCertificate = dto.ProvidesCertificate;
        entity.CertificateName = dto.CertificateName;
        entity.CertificateValidityMonths = dto.CertificateValidityMonths;
        entity.MinParticipants = dto.MinParticipants;
        entity.MaxParticipants = dto.MaxParticipants;
        entity.IsActive = dto.IsActive;
        entity.RequiresApproval = dto.RequiresApproval;
        entity.RequiresServiceBond = dto.RequiresServiceBond;
        entity.ServiceBondMonths = dto.ServiceBondMonths;
        entity.ServiceBondTerms = dto.ServiceBondTerms;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingProgramSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingProgram> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINING CATEGORY OPTION
    // ========================================================================

    #region TrainingCategoryOption

    public static TrainingCategoryOptionDto ToDto(this TrainingCategoryOption entity)
    {
        return new TrainingCategoryOptionDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Code = entity.Code,
            Name = entity.Name,
            ColorHex = entity.ColorHex,
            Description = entity.Description,
            IsActive = entity.IsActive,
            SortOrder = entity.SortOrder,
            ProgramsCount = entity.Programs?.Count ?? 0,
        };
    }

    public static TrainingCategoryOption ToEntity(this CreateTrainingCategoryOptionDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingCategoryOption
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            ColorHex = dto.ColorHex,
            Description = dto.Description,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingCategoryOption entity, UpdateTrainingCategoryOptionDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.ColorHex = dto.ColorHex;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingCategoryOptionDto> ToDtoList(this IEnumerable<TrainingCategoryOption> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // TRAINING PROGRAM GROUP
    // ========================================================================

    #region TrainingProgramGroup

    public static TrainingProgramGroupDto ToDto(this TrainingProgramGroup entity)
    {
        return new TrainingProgramGroupDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Code = entity.Code,
            Name = entity.Name,
            ColorHex = entity.ColorHex,
            Description = entity.Description,
            IsActive = entity.IsActive,
            SortOrder = entity.SortOrder,
            ProgramsCount = entity.Programs?.Count ?? 0,
        };
    }

    public static TrainingProgramGroup ToEntity(this CreateTrainingProgramGroupDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingProgramGroup
        {
            TenantId = tenantId,
            Code = dto.Code,
            Name = dto.Name,
            ColorHex = dto.ColorHex,
            Description = dto.Description,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingProgramGroup entity, UpdateTrainingProgramGroupDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.ColorHex = dto.ColorHex;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingProgramGroupDto> ToDtoList(this IEnumerable<TrainingProgramGroup> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // TRAINING SERVICE BOND
    // ========================================================================

    #region TrainingServiceBond

    public static TrainingServiceBondDto ToDto(this TrainingServiceBond entity)
    {
        return new TrainingServiceBondDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            NominationId = entity.NominationId,
            NominationNumber = entity.Nomination?.NominationNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            ProgramId = entity.ProgramId,
            ProgramName = entity.Program?.ProgramName,
            BondDurationMonths = entity.BondDurationMonths,
            BondAmount = entity.BondAmount,
            Currency = entity.Currency,
            TermsText = entity.TermsText,
            Status = entity.Status,
            AcceptedByEmployee = entity.AcceptedByEmployee,
            AcceptedDate = entity.AcceptedDate,
            AcceptanceRecordedById = entity.AcceptanceRecordedById,
            AcceptanceRecordedByName = entity.AcceptanceRecordedBy?.FullName,
            AcceptanceNotes = entity.AcceptanceNotes,
            BondStartDate = entity.BondStartDate,
            BondEndDate = entity.BondEndDate,
            ExitDate = entity.ExitDate,
            RepaymentAmount = entity.RepaymentAmount,
            SettledDate = entity.SettledDate,
            WaivedDate = entity.WaivedDate,
            WaivedById = entity.WaivedById,
            WaiverReason = entity.WaiverReason,
            Notes = entity.Notes,
            MonthsRemaining = ComputeMonthsRemaining(entity),
        };
    }

    private static int ComputeMonthsRemaining(TrainingServiceBond entity)
    {
        // Only meaningful for an active, running obligation.
        if (entity.Status != TrainingBondStatus.Active || entity.BondEndDate is null)
            return 0;

        var months = (entity.BondEndDate.Value.Year - DateTime.UtcNow.Year) * 12
                     + (entity.BondEndDate.Value.Month - DateTime.UtcNow.Month);
        return months > 0 ? months : 0;
    }

    public static IEnumerable<TrainingServiceBondDto> ToDtoList(this IEnumerable<TrainingServiceBond> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // TRAINING STATUS HISTORY
    // ========================================================================

    #region TrainingStatusHistory

    public static TrainingStatusHistoryDto ToDto(this TrainingStatusHistory entity)
    {
        return new TrainingStatusHistoryDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            EntityType = entity.EntityType,
            EntityId = entity.EntityId,
            EntityReference = entity.EntityReference,
            FromStatus = entity.FromStatus,
            FromStatusName = entity.FromStatusName,
            ToStatus = entity.ToStatus,
            ToStatusName = entity.ToStatusName,
            ChangedByEmployeeId = entity.ChangedByEmployeeId,
            ChangedByName = entity.ChangedByName,
            ChangedAt = entity.ChangedAt,
            Reason = entity.Reason,
        };
    }

    public static IEnumerable<TrainingStatusHistoryDto> ToDtoList(this IEnumerable<TrainingStatusHistory> entities)
        => entities.Select(e => e.ToDto());

    #endregion

    // ========================================================================
    // TRAINING MATERIAL
    // ========================================================================

    #region TrainingMaterial

    public static TrainingMaterialDto ToDto(this TrainingMaterial entity)
    {
        return new TrainingMaterialDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            MaterialName = entity.MaterialName,
            Type = entity.Type,
            FilePath = entity.FilePath,
            ExternalUrl = entity.ExternalUrl,
            IsPublic = entity.IsPublic,
            IsActive = entity.IsActive,
            UploadDate = entity.UploadDate,
        };
    }

    public static TrainingMaterial ToEntity(this CreateTrainingMaterialDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingMaterial
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            MaterialName = dto.MaterialName,
            Type = dto.Type,
            FilePath = dto.FilePath,
            ExternalUrl = dto.ExternalUrl,
            IsPublic = dto.IsPublic,
            IsActive = dto.IsActive,
            UploadDate = dto.UploadDate,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingMaterial entity, UpdateTrainingMaterialDto dto, Guid userId)
    {
        entity.MaterialName = dto.MaterialName;
        entity.Type = dto.Type;
        entity.FilePath = dto.FilePath;
        entity.ExternalUrl = dto.ExternalUrl;
        entity.IsPublic = dto.IsPublic;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TRAINING PROGRAM COMPETENCY
    // ========================================================================

    #region TrainingProgramCompetency

    public static TrainingProgramCompetencyDto ToDto(this TrainingProgramCompetency entity)
    {
        return new TrainingProgramCompetencyDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            CompetencyId = entity.CompetencyId,
            CompetencyCode = entity.Competency?.Code ?? string.Empty,
            CompetencyName = entity.Competency?.Name ?? string.Empty,
            TargetLevel = entity.TargetLevel,
        };
    }

    public static TrainingProgramCompetency ToEntity(this CreateTrainingProgramCompetencyDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingProgramCompetency
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            CompetencyId = dto.CompetencyId,
            TargetLevel = dto.TargetLevel,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING PROGRAM SKILL
    // ========================================================================

    #region TrainingProgramSkill

    public static TrainingProgramSkillDto ToDto(this TrainingProgramSkill entity)
    {
        return new TrainingProgramSkillDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            SkillId = entity.SkillId,
            SkillName = entity.Skill?.Name ?? string.Empty,
            TargetProficiency = entity.TargetProficiency,
        };
    }

    public static TrainingProgramSkill ToEntity(this CreateTrainingProgramSkillDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingProgramSkill
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            SkillId = dto.SkillId,
            TargetProficiency = dto.TargetProficiency,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING SCHEDULE
    // ========================================================================

    #region TrainingSchedule

    public static TrainingScheduleDto ToDto(this TrainingSchedule entity)
    {
        return new TrainingScheduleDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScheduleNumber = entity.ScheduleNumber,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode ?? string.Empty,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Venue = entity.Venue,
            VenueAddress = entity.VenueAddress,
            OnlineLink = entity.OnlineLink,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            TrainerProfileId = entity.TrainerProfileId,
            TrainerName = entity.TrainerProfile?.Name,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,
            MaxParticipants = entity.MaxParticipants,
            Priority = entity.Priority,
            ConfirmedParticipantsCount = entity.Nominations.Count(n => n.Status == NominationStatus.Confirmed),
            RegistrationOpenDate = entity.RegistrationOpenDate,
            RegistrationCloseDate = entity.RegistrationCloseDate,
            Status = entity.Status,
            ActualCost = entity.ActualCost,
            BudgetNotes = entity.BudgetNotes,
            TrainingBudgetId = entity.TrainingBudgetId,
            BudgetCode = entity.TrainingBudget?.BudgetCode,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            CompletionDate = entity.CompletionDate,
            CompletionNotes = entity.CompletionNotes,
            CancellationReason = entity.CancellationReason,
            CancelledDate = entity.CancelledDate,
            Sessions = entity.Sessions.Select(s => s.ToDto()).ToList(),
        };
    }

    public static TrainingScheduleSummaryDto ToSummaryDto(this TrainingSchedule entity)
    {
        return new TrainingScheduleSummaryDto
        {
            Id = entity.Id,
            ScheduleNumber = entity.ScheduleNumber,
            ProgramCode = entity.Program?.ProgramCode ?? string.Empty,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Venue = entity.Venue,
            TrainerName = entity.TrainerProfile?.Name,
            VendorName = entity.Vendor?.Name,
            MaxParticipants = entity.MaxParticipants,
            Priority = entity.Priority,
            ConfirmedParticipantsCount = entity.Nominations.Count(n => n.Status == NominationStatus.Confirmed),
            Status = entity.Status,
            RegistrationCloseDate = entity.RegistrationCloseDate,
        };
    }

    public static TrainingSchedule ToEntity(this CreateTrainingScheduleDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingSchedule
        {
            TenantId = tenantId,
            ScheduleNumber = string.Empty,
            ProgramId = dto.ProgramId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Venue = dto.Venue,
            VenueAddress = dto.VenueAddress,
            OnlineLink = dto.OnlineLink,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            TrainerProfileId = dto.TrainerProfileId,
            VendorId = dto.VendorId,
            MaxParticipants = dto.MaxParticipants,
            Priority = dto.Priority,
            RegistrationOpenDate = dto.RegistrationOpenDate,
            RegistrationCloseDate = dto.RegistrationCloseDate,
            ActualCost = dto.ActualCost,
            BudgetNotes = dto.BudgetNotes,
            TrainingBudgetId = dto.TrainingBudgetId,
            Status = ScheduleStatus.Planned,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingSchedule entity, UpdateTrainingScheduleDto dto, Guid userId)
    {
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.Venue = dto.Venue;
        entity.VenueAddress = dto.VenueAddress;
        entity.OnlineLink = dto.OnlineLink;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.TrainerProfileId = dto.TrainerProfileId;
        entity.VendorId = dto.VendorId;
        entity.MaxParticipants = dto.MaxParticipants;
        entity.Priority = dto.Priority;
        entity.RegistrationOpenDate = dto.RegistrationOpenDate;
        entity.RegistrationCloseDate = dto.RegistrationCloseDate;
        entity.ActualCost = dto.ActualCost;
        entity.BudgetNotes = dto.BudgetNotes;
        entity.TrainingBudgetId = dto.TrainingBudgetId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingScheduleSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingSchedule> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINING SESSION
    // ========================================================================

    #region TrainingSession

    public static TrainingSessionDto ToDto(this TrainingSession entity)
    {
        return new TrainingSessionDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScheduleId = entity.ScheduleId,
            ScheduleNumber = entity.Schedule?.ScheduleNumber ?? string.Empty,
            Topic = entity.Topic,
            Date = entity.Date,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Description = entity.Description,
        };
    }

    public static TrainingSession ToEntity(this CreateTrainingSessionDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingSession
        {
            TenantId = tenantId,
            ScheduleId = dto.ScheduleId,
            Topic = dto.Topic,
            Date = dto.Date,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Description = dto.Description,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingSession entity, UpdateTrainingSessionDto dto, Guid userId)
    {
        entity.Topic = dto.Topic;
        entity.Date = dto.Date;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.Description = dto.Description;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TRAINING NOMINATION
    // ========================================================================

    #region TrainingNomination

    public static TrainingNominationDto ToDto(this TrainingNomination entity)
    {
        return new TrainingNominationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            NominationNumber = entity.NominationNumber,
            ScheduleId = entity.ScheduleId,
            ScheduleNumber = entity.Schedule?.ScheduleNumber ?? string.Empty,
            ProgramName = entity.Schedule?.Program?.ProgramName ?? string.Empty,
            TrainingStartDate = entity.Schedule?.StartDate ?? default,
            TrainingEndDate = entity.Schedule?.EndDate ?? default,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            EmployeeDepartment = entity.Employee?.Department?.Name,
            EmployeePosition = entity.Employee?.Position?.Title,
            Type = entity.Type,
            NominatedById = entity.NominatedById,
            NominatedByName = entity.NominatedBy?.FullName,
            NominationDate = entity.NominationDate,
            Justification = entity.Justification,
            TrainingNeedsAssessmentId = entity.TrainingNeedsAssessmentId,
            Status = entity.Status,
            SupervisorApprovedById = entity.SupervisorApprovedById,
            SupervisorApprovedByName = entity.SupervisorApprovedBy?.FullName,
            SupervisorApprovalDate = entity.SupervisorApprovalDate,
            SupervisorComments = entity.SupervisorComments,
            HrApprovedById = entity.HrApprovedById,
            HrApprovedByName = entity.HrApprovedBy?.FullName,
            HrApprovalDate = entity.HrApprovalDate,
            HrComments = entity.HrComments,
            RejectedDate = entity.RejectedDate,
            RejectionReason = entity.RejectionReason,
            ActualCost = entity.ActualCost,
            EmployeeContributed = entity.EmployeeContributed,
            EmployeeContribution = entity.EmployeeContribution,
            HasCompletionRecord = entity.CompletionRecord != null,
            CompletionRecord = entity.CompletionRecord?.ToSummaryDto(),
        };
    }

    public static TrainingNominationSummaryDto ToSummaryDto(this TrainingNomination entity)
    {
        return new TrainingNominationSummaryDto
        {
            Id = entity.Id,
            NominationNumber = entity.NominationNumber,
            ProgramName = entity.Schedule?.Program?.ProgramName ?? string.Empty,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Type = entity.Type,
            Status = entity.Status,
            NominationDate = entity.NominationDate,
            TrainingStartDate = entity.Schedule?.StartDate ?? default,
        };
    }

    public static TrainingNomination ToEntity(this CreateTrainingNominationDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingNomination
        {
            TenantId = tenantId,
            NominationNumber = string.Empty,
            ScheduleId = dto.ScheduleId,
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            // Controllers pass the authenticated employee id as userId for training create flows.
            NominatedById = userId,
            NominationDate = dto.NominationDate,
            Justification = dto.Justification,
            TrainingNeedsAssessmentId = dto.TrainingNeedsAssessmentId,
            Status = NominationStatus.Submitted,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<TrainingNominationSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingNomination> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINING COMPLETION
    // ========================================================================

    #region TrainingCompletion

    public static TrainingCompletionDto ToDto(this TrainingCompletion entity)
    {
        return new TrainingCompletionDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            NominationId = entity.NominationId,
            NominationNumber = entity.Nomination?.NominationNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            ProgramName = entity.Nomination?.Schedule?.Program?.ProgramName ?? string.Empty,
            CompletionDate = entity.CompletionDate,
            Status = entity.Status,
            FinalScore = entity.FinalScore,
            PreAssessmentScore = entity.PreAssessmentScore,
            PostAssessmentScore = entity.PostAssessmentScore,
            IsPassed = entity.IsPassed,
            IsVerifiedByManager = entity.IsVerifiedByManager,
            VerifiedById = entity.VerifiedById,
            VerificationDate = entity.VerificationDate,
            VerificationNotes = entity.VerificationNotes,
        };
    }

    public static TrainingCompletionSummaryDto ToSummaryDto(this TrainingCompletion entity)
    {
        return new TrainingCompletionSummaryDto
        {
            Id = entity.Id,
            CompletionDate = entity.CompletionDate,
            Status = entity.Status,
            FinalScore = entity.FinalScore,
            IsPassed = entity.IsPassed,
            IsVerifiedByManager = entity.IsVerifiedByManager,
        };
    }

    public static TrainingCompletion ToEntity(this RecordTrainingCompletionDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingCompletion
        {
            TenantId = tenantId,
            NominationId = dto.NominationId,
            EmployeeId = dto.EmployeeId,
            CompletionDate = dto.CompletionDate,
            Status = dto.Status,
            FinalScore = dto.FinalScore,
            PreAssessmentScore = dto.PreAssessmentScore,
            PostAssessmentScore = dto.PostAssessmentScore,
            IsPassed = dto.IsPassed,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<TrainingCompletionSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingCompletion> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static void UpdateEntity(this TrainingCompletion entity, UpdateTrainingCompletionDto dto, Guid userId)
    {
        entity.CompletionDate = dto.CompletionDate;
        entity.Status = dto.Status;
        entity.FinalScore = dto.FinalScore;
        entity.PreAssessmentScore = dto.PreAssessmentScore;
        entity.PostAssessmentScore = dto.PostAssessmentScore;
        entity.IsPassed = dto.IsPassed;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TRAINING ATTENDANCE
    // ========================================================================

    #region TrainingAttendance

    public static TrainingAttendanceDto ToDto(this TrainingAttendance entity)
    {
        return new TrainingAttendanceDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScheduleId = entity.ScheduleId,
            ScheduleNumber = entity.Schedule?.ScheduleNumber ?? string.Empty,
            ProgramName = entity.Schedule?.Program?.ProgramName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            NominationId = entity.NominationId,
            NominationNumber = entity.Nomination?.NominationNumber,
            AttendanceDate = entity.AttendanceDate,
            IsPresent = entity.IsPresent,
            CheckInTime = entity.CheckInTime,
            CheckOutTime = entity.CheckOutTime,
            AbsenceReason = entity.AbsenceReason,
            Notes = entity.Notes,
            MarkedById = entity.MarkedById,
            MarkedByName = entity.MarkedBy?.FullName,
            MarkedAt = entity.MarkedAt,
        };
    }

    public static TrainingAttendance ToEntity(this MarkAttendanceDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingAttendance
        {
            TenantId = tenantId,
            ScheduleId = dto.ScheduleId,
            EmployeeId = dto.EmployeeId,
            NominationId = dto.NominationId,
            AttendanceDate = dto.AttendanceDate,
            IsPresent = dto.IsPresent,
            CheckInTime = dto.CheckInTime,
            CheckOutTime = dto.CheckOutTime,
            AbsenceReason = dto.AbsenceReason,
            Notes = dto.Notes,
            // Controllers pass the authenticated employee id as userId for training create flows.
            MarkedById = userId,
            MarkedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING FEEDBACK
    // ========================================================================

    #region TrainingFeedback

    public static TrainingFeedbackDto ToDto(this TrainingFeedback entity)
    {
        return new TrainingFeedbackDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScheduleId = entity.ScheduleId,
            ScheduleNumber = entity.Schedule?.ScheduleNumber ?? string.Empty,
            ProgramName = entity.Schedule?.Program?.ProgramName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            NominationId = entity.NominationId,
            ContentRelevanceRating = entity.ContentRelevanceRating,
            TrainerKnowledgeRating = entity.TrainerKnowledgeRating,
            DeliveryMethodRating = entity.DeliveryMethodRating,
            MaterialQualityRating = entity.MaterialQualityRating,
            VenueFacilitiesRating = entity.VenueFacilitiesRating,
            OverallSatisfactionRating = entity.OverallSatisfactionRating,
            StrengthsOfTraining = entity.StrengthsOfTraining,
            AreasForImprovement = entity.AreasForImprovement,
            SuggestionsForFuture = entity.SuggestionsForFuture,
            AdditionalComments = entity.AdditionalComments,
            WouldRecommend = entity.WouldRecommend,
            LikelihoodToApply = entity.LikelihoodToApply,
            ExpectedApplicationOnJob = entity.ExpectedApplicationOnJob,
            BarriersToApplication = entity.BarriersToApplication,
            FeedbackDate = entity.FeedbackDate,
        };
    }

    public static TrainingFeedback ToEntity(this SubmitTrainingFeedbackDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingFeedback
        {
            TenantId = tenantId,
            ScheduleId = dto.ScheduleId,
            EmployeeId = dto.EmployeeId,
            NominationId = dto.NominationId,
            ContentRelevanceRating = dto.ContentRelevanceRating,
            TrainerKnowledgeRating = dto.TrainerKnowledgeRating,
            DeliveryMethodRating = dto.DeliveryMethodRating,
            MaterialQualityRating = dto.MaterialQualityRating,
            VenueFacilitiesRating = dto.VenueFacilitiesRating,
            OverallSatisfactionRating = dto.OverallSatisfactionRating,
            StrengthsOfTraining = dto.StrengthsOfTraining,
            AreasForImprovement = dto.AreasForImprovement,
            SuggestionsForFuture = dto.SuggestionsForFuture,
            AdditionalComments = dto.AdditionalComments,
            WouldRecommend = dto.WouldRecommend,
            LikelihoodToApply = dto.LikelihoodToApply,
            ExpectedApplicationOnJob = dto.ExpectedApplicationOnJob,
            BarriersToApplication = dto.BarriersToApplication,
            FeedbackDate = dto.FeedbackDate,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING FOLLOW-UP ASSESSMENT
    // ========================================================================

    #region TrainingFollowUpAssessment

    public static TrainingFollowUpAssessmentDto ToDto(this TrainingFollowUpAssessment entity)
    {
        return new TrainingFollowUpAssessmentDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScheduleId = entity.ScheduleId,
            ScheduleNumber = entity.Schedule?.ScheduleNumber ?? string.Empty,
            ProgramName = entity.Schedule?.Program?.ProgramName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            NominationId = entity.NominationId,
            AssessmentType = entity.AssessmentType,
            AssessmentDate = entity.AssessmentDate,
            KeyLearningsTaken = entity.KeyLearningsTaken,
            ConceptsStillUnclear = entity.ConceptsStillUnclear,
            FrequencyOfUse = entity.FrequencyOfUse,
            HowSkillsApplied = entity.HowSkillsApplied,
            BarriersToApplication = entity.BarriersToApplication,
            SupportNeeded = entity.SupportNeeded,
            ManagerId = entity.ManagerId,
            ManagerName = entity.Manager?.FullName,
            ManagerObservationNotes = entity.ManagerObservationNotes,
            ManagerSubmittedDate = entity.ManagerSubmittedDate,
            RecommendFurtherTraining = entity.RecommendFurtherTraining,
            RecommendedFollowUp = entity.RecommendedFollowUp,
        };
    }

    public static TrainingFollowUpAssessment ToEntity(this SubmitFollowUpAssessmentDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingFollowUpAssessment
        {
            TenantId = tenantId,
            ScheduleId = dto.ScheduleId,
            EmployeeId = dto.EmployeeId,
            NominationId = dto.NominationId,
            AssessmentType = dto.AssessmentType,
            AssessmentDate = dto.AssessmentDate,
            KeyLearningsTaken = dto.KeyLearningsTaken,
            ConceptsStillUnclear = dto.ConceptsStillUnclear,
            FrequencyOfUse = dto.FrequencyOfUse,
            HowSkillsApplied = dto.HowSkillsApplied,
            BarriersToApplication = dto.BarriersToApplication,
            SupportNeeded = dto.SupportNeeded,
            RecommendFurtherTraining = dto.RecommendFurtherTraining,
            RecommendedFollowUp = dto.RecommendedFollowUp,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING CERTIFICATE
    // ========================================================================

    #region TrainingCertificate

    public static TrainingCertificateDto ToDto(this TrainingCertificate entity)
    {
        return new TrainingCertificateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CertificateNumber = entity.CertificateNumber,
            VerificationCode = entity.VerificationCode,
            NominationId = entity.NominationId,
            NominationNumber = entity.Nomination?.NominationNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            ProgramId = entity.ProgramId,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            CertificateName = entity.CertificateName,
            IssuedDate = entity.IssuedDate,
            ExpiryDate = entity.ExpiryDate,
            ValidityMonths = entity.ValidityMonths,
            Status = entity.Status,
            RevokedDate = entity.RevokedDate,
            RevokedReason = entity.RevokedReason,
            FilePath = entity.FilePath,
            ExternalUrl = entity.ExternalUrl,
            IsRenewal = entity.IsRenewal,
            PreviousCertificateId = entity.PreviousCertificateId,
            PreviousCertificateNumber = entity.PreviousCertificate?.CertificateNumber,
            IssuedById = entity.IssuedById,
            IssuedByName = entity.IssuedBy?.FullName,
        };
    }

    public static TrainingCertificateSummaryDto ToSummaryDto(this TrainingCertificate entity)
    {
        return new TrainingCertificateSummaryDto
        {
            Id = entity.Id,
            CertificateNumber = entity.CertificateNumber,
            VerificationCode = entity.VerificationCode,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            CertificateName = entity.CertificateName,
            IssuedDate = entity.IssuedDate,
            ExpiryDate = entity.ExpiryDate,
            Status = entity.Status,
        };
    }

    public static TrainingCertificate ToEntity(this IssueCertificateDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingCertificate
        {
            TenantId = tenantId,
            CertificateNumber = string.Empty,
            NominationId = dto.NominationId,
            EmployeeId = dto.EmployeeId,
            ProgramId = dto.ProgramId,
            CertificateName = dto.CertificateName,
            IssuedDate = dto.IssuedDate,
            ExpiryDate = dto.ExpiryDate,
            ValidityMonths = dto.ValidityMonths,
            Status = CertificateStatus.Active,
            FilePath = dto.FilePath,
            ExternalUrl = dto.ExternalUrl,
            IsRenewal = dto.IsRenewal,
            PreviousCertificateId = dto.PreviousCertificateId,
            // Controllers pass the authenticated employee id as userId for training create flows.
            IssuedById = userId,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<TrainingCertificateSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingCertificate> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // EMPLOYEE CERTIFICATE
    // ========================================================================

    #region EmployeeCertificate

    public static EmployeeCertificateDto ToDto(this EmployeeCertificate entity)
    {
        return new EmployeeCertificateDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            CertificateName = entity.CertificateName,
            IssuingBody = entity.IssuingBody,
            CertificateNumber = entity.CertificateNumber,
            IssuedDate = entity.IssuedDate,
            ExpiryDate = entity.ExpiryDate,
            Status = entity.Status,
            Category = entity.Category,
            Description = entity.Description,
            FilePath = entity.FilePath,
            IsVerified = entity.IsVerified,
            VerifiedById = entity.VerifiedById,
            VerifiedByName = entity.VerifiedBy?.FullName,
            VerifiedDate = entity.VerifiedDate,
        };
    }

    public static EmployeeCertificateSummaryDto ToSummaryDto(this EmployeeCertificate entity)
    {
        return new EmployeeCertificateSummaryDto
        {
            Id = entity.Id,
            CertificateName = entity.CertificateName,
            IssuingBody = entity.IssuingBody,
            IssuedDate = entity.IssuedDate,
            ExpiryDate = entity.ExpiryDate,
            Status = entity.Status,
            IsVerified = entity.IsVerified,
        };
    }

    public static EmployeeCertificate ToEntity(this CreateEmployeeCertificateDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeCertificate
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            CertificateName = dto.CertificateName,
            IssuingBody = dto.IssuingBody,
            CertificateNumber = dto.CertificateNumber,
            IssuedDate = dto.IssuedDate,
            ExpiryDate = dto.ExpiryDate,
            Status = CertificateStatus.Active,
            Category = dto.Category,
            Description = dto.Description,
            FilePath = dto.FilePath,
            IsVerified = false,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<EmployeeCertificateSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeCertificate> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static void UpdateEntity(this EmployeeCertificate entity, UpdateEmployeeCertificateDto dto, Guid userId)
    {
        entity.CertificateName = dto.CertificateName;
        entity.IssuingBody = dto.IssuingBody;
        entity.CertificateNumber = dto.CertificateNumber;
        entity.IssuedDate = dto.IssuedDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Category = dto.Category;
        entity.Description = dto.Description;
        entity.FilePath = dto.FilePath;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region ComplianceTrainingRequirement

    public static ComplianceTrainingRequirementDto ToDto(this ComplianceTrainingRequirement entity)
    {
        var totalAssigned = entity.EmployeeRecords.Count;
        var compliantCount = entity.EmployeeRecords.Count(r => r.Status == ComplianceStatus.Compliant);

        return new ComplianceTrainingRequirementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RequirementCode = entity.RequirementCode,
            RequirementName = entity.RequirementName,
            Description = entity.Description,
            RegulatoryReference = entity.RegulatoryReference,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode ?? string.Empty,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            Frequency = entity.Frequency,
            CustomFrequencyDays = entity.CustomFrequencyDays,
            GracePeriodDays = entity.GracePeriodDays,
            IsActive = entity.IsActive,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
            NonComplianceConsequences = entity.NonComplianceConsequences,
            TotalAssignedEmployees = totalAssigned,
            CompliantEmployeesCount = compliantCount,
        };
    }

    public static ComplianceTrainingRequirementSummaryDto ToSummaryDto(this ComplianceTrainingRequirement entity)
    {
        var totalAssigned = entity.EmployeeRecords.Count;
        var compliantCount = entity.EmployeeRecords.Count(r => r.Status == ComplianceStatus.Compliant);

        return new ComplianceTrainingRequirementSummaryDto
        {
            Id = entity.Id,
            RequirementCode = entity.RequirementCode,
            RequirementName = entity.RequirementName,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            Frequency = entity.Frequency,
            IsActive = entity.IsActive,
            EffectiveDate = entity.EffectiveDate,
            TotalAssignedEmployees = totalAssigned,
            ComplianceRate = totalAssigned > 0
                ? Math.Round((decimal)compliantCount / totalAssigned * 100, 1)
                : 0,
        };
    }

    public static ComplianceTrainingRequirement ToEntity(this CreateComplianceTrainingRequirementDto dto, Guid tenantId, Guid userId)
    {
        return new ComplianceTrainingRequirement
        {
            TenantId = tenantId,
            RequirementCode = dto.RequirementCode,
            RequirementName = dto.RequirementName,
            Description = dto.Description,
            RegulatoryReference = dto.RegulatoryReference,
            ProgramId = dto.ProgramId,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            Frequency = dto.Frequency,
            CustomFrequencyDays = dto.CustomFrequencyDays,
            GracePeriodDays = dto.GracePeriodDays,
            IsActive = dto.IsActive,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            NonComplianceConsequences = dto.NonComplianceConsequences,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this ComplianceTrainingRequirement entity, UpdateComplianceTrainingRequirementDto dto, Guid userId)
    {
        entity.RequirementName = dto.RequirementName;
        entity.Description = dto.Description;
        entity.RegulatoryReference = dto.RegulatoryReference;
        entity.ProgramId = dto.ProgramId;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.Frequency = dto.Frequency;
        entity.CustomFrequencyDays = dto.CustomFrequencyDays;
        entity.GracePeriodDays = dto.GracePeriodDays;
        entity.IsActive = dto.IsActive;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.NonComplianceConsequences = dto.NonComplianceConsequences;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ComplianceTrainingRequirementSummaryDto> ToSummaryDtoList(this IEnumerable<ComplianceTrainingRequirement> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // EMPLOYEE COMPLIANCE RECORD
    // ========================================================================

    #region EmployeeComplianceRecord

    public static EmployeeComplianceRecordDto ToDto(this EmployeeComplianceRecord entity)
    {
        return new EmployeeComplianceRecordDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            RequirementId = entity.RequirementId,
            RequirementCode = entity.Requirement?.RequirementCode ?? string.Empty,
            RequirementName = entity.Requirement?.RequirementName ?? string.Empty,
            ProgramName = entity.Requirement?.Program?.ProgramName ?? string.Empty,
            Status = entity.Status,
            AssignedDate = entity.AssignedDate,
            LastCompletedDate = entity.LastCompletedDate,
            NextDueDate = entity.NextDueDate,
            GracePeriodExpiry = entity.GracePeriodExpiry,
            FulfillingNominationId = entity.FulfillingNominationId,
            FulfillingNominationNumber = entity.FulfillingNomination?.NominationNumber,
            IsExempt = entity.IsExempt,
            ExemptionReason = entity.ExemptionReason,
            ExemptedById = entity.ExemptedById,
            ExemptedByName = entity.ExemptedBy?.FullName,
            ExemptionDate = entity.ExemptionDate,
            ExemptionExpiryDate = entity.ExemptionExpiryDate,
            ReminderSentDate = entity.ReminderSentDate,
            Notes = entity.Notes,
        };
    }

    public static EmployeeComplianceRecordSummaryDto ToSummaryDto(this EmployeeComplianceRecord entity)
    {
        return new EmployeeComplianceRecordSummaryDto
        {
            Id = entity.Id,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            RequirementName = entity.Requirement?.RequirementName ?? string.Empty,
            Status = entity.Status,
            NextDueDate = entity.NextDueDate,
            IsExempt = entity.IsExempt,
        };
    }

    public static IEnumerable<EmployeeComplianceRecordSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeComplianceRecord> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINING BUDGET
    // ========================================================================

    #region TrainingBudget

    public static TrainingBudgetDto ToDto(this TrainingBudget entity)
    {
        return new TrainingBudgetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            BudgetCode = entity.BudgetCode,
            Year = entity.Year,
            Quarter = entity.Quarter,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Currency = entity.Currency,
            AllocatedAmount = entity.AllocatedAmount,
            CommittedAmount = entity.CommittedAmount,
            SpentAmount = entity.SpentAmount,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            GLAccountCode = entity.GLAccountCode,
            CostCenterCode = entity.CostCenterCode,
            Notes = entity.Notes,
        };
    }

    public static TrainingBudgetSummaryDto ToSummaryDto(this TrainingBudget entity)
    {
        return new TrainingBudgetSummaryDto
        {
            Id = entity.Id,
            BudgetCode = entity.BudgetCode,
            Year = entity.Year,
            Quarter = entity.Quarter,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Currency = entity.Currency,
            AllocatedAmount = entity.AllocatedAmount,
            SpentAmount = entity.SpentAmount,
            RemainingAmount = entity.RemainingAmount,
            Status = entity.Status,
        };
    }

    public static TrainingBudget ToEntity(this CreateTrainingBudgetDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingBudget
        {
            TenantId = tenantId,
            BudgetCode = dto.BudgetCode,
            Year = dto.Year,
            Quarter = dto.Quarter,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            Currency = dto.Currency,
            AllocatedAmount = dto.AllocatedAmount,
            GLAccountCode = dto.GLAccountCode,
            CostCenterCode = dto.CostCenterCode,
            Status = TrainingBudgetStatus.Draft,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingBudget entity, UpdateTrainingBudgetDto dto, Guid userId)
    {
        entity.AllocatedAmount = dto.AllocatedAmount;
        entity.GLAccountCode = dto.GLAccountCode;
        entity.CostCenterCode = dto.CostCenterCode;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingBudgetSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingBudget> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINING BUDGET TRANSACTION
    // ========================================================================

    #region TrainingBudgetTransaction

    public static TrainingBudgetTransactionDto ToDto(this TrainingBudgetTransaction entity)
    {
        return new TrainingBudgetTransactionDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            BudgetId = entity.BudgetId,
            BudgetCode = entity.Budget?.BudgetCode ?? string.Empty,
            ScheduleId = entity.ScheduleId,
            ScheduleNumber = entity.Schedule?.ScheduleNumber,
            Description = entity.Description,
            Amount = entity.Amount,
            TransactionDate = entity.TransactionDate,
            RecordedById = entity.RecordedById,
            RecordedByName = entity.RecordedBy?.FullName,
            Reference = entity.Reference,
            GLAccountCode = entity.GLAccountCode,
            VoucherNumber = entity.VoucherNumber,
            Notes = entity.Notes,
        };
    }

    public static TrainingBudgetTransaction ToEntity(this CreateTrainingBudgetTransactionDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingBudgetTransaction
        {
            TenantId = tenantId,
            BudgetId = dto.BudgetId,
            ScheduleId = dto.ScheduleId,
            Description = dto.Description,
            Amount = dto.Amount,
            TransactionDate = dto.TransactionDate,
            RecordedById = dto.RecordedById,
            Reference = dto.Reference,
            GLAccountCode = dto.GLAccountCode,
            VoucherNumber = dto.VoucherNumber,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING PLAN
    // ========================================================================

    #region TrainingPlan

    public static TrainingPlanDto ToDto(this TrainingPlan entity)
    {
        return new TrainingPlanDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PlanNumber = entity.PlanNumber,
            Year = entity.Year,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            Notes = entity.Notes,
            TotalItemsCount = entity.Items.Count,
            CompletedItemsCount = entity.Items.Count(i => i.IsCompleted),
            Items = entity.Items.Select(i => i.ToDto()).ToList(),
            BudgetLines = entity.BudgetLines.Select(b => b.ToDto()).ToList(),
        };
    }

    public static TrainingPlanSummaryDto ToSummaryDto(this TrainingPlan entity)
    {
        return new TrainingPlanSummaryDto
        {
            Id = entity.Id,
            PlanNumber = entity.PlanNumber,
            Year = entity.Year,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Status = entity.Status,
            TotalItemsCount = entity.Items.Count,
            CompletedItemsCount = entity.Items.Count(i => i.IsCompleted),
            ApprovalDate = entity.ApprovalDate,
        };
    }

    public static TrainingPlan ToEntity(this CreateTrainingPlanDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingPlan
        {
            TenantId = tenantId,
            PlanNumber = string.Empty,
            Year = dto.Year,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            Status = TrainingPlanStatus.Draft,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingPlan entity, UpdateTrainingPlanDto dto, Guid userId)
    {
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingPlanSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingPlan> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINING PLAN ITEM
    // ========================================================================

    #region TrainingPlanItem

    public static TrainingPlanItemDto ToDto(this TrainingPlanItem entity)
    {
        return new TrainingPlanItemDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PlanId = entity.PlanId,
            PlanNumber = entity.Plan?.PlanNumber ?? string.Empty,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode,
            TrainingTitle = entity.TrainingTitle,
            Description = entity.Description,
            Quarter = entity.Quarter,
            PlannedStartDate = entity.PlannedStartDate,
            PlannedEndDate = entity.PlannedEndDate,
            EstimatedParticipants = entity.EstimatedParticipants,
            EstimatedCost = entity.EstimatedCost,
            IsCompleted = entity.IsCompleted,
            CompletionDate = entity.CompletionDate,
            ActualParticipants = entity.ActualParticipants,
            ActualCost = entity.ActualCost,
            FulfilledByScheduleId = entity.FulfilledByScheduleId,
            FulfilledByScheduleNumber = entity.FulfilledBySchedule?.ScheduleNumber,
        };
    }

    public static TrainingPlanItem ToEntity(this CreateTrainingPlanItemDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingPlanItem
        {
            TenantId = tenantId,
            PlanId = dto.PlanId,
            ProgramId = dto.ProgramId,
            TrainingTitle = dto.TrainingTitle,
            Description = dto.Description,
            Quarter = dto.Quarter,
            PlannedStartDate = dto.PlannedStartDate,
            PlannedEndDate = dto.PlannedEndDate,
            EstimatedParticipants = dto.EstimatedParticipants,
            EstimatedCost = dto.EstimatedCost,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingPlanItem entity, UpdateTrainingPlanItemDto dto, Guid userId)
    {
        entity.ProgramId = dto.ProgramId;
        entity.TrainingTitle = dto.TrainingTitle;
        entity.Description = dto.Description;
        entity.Quarter = dto.Quarter;
        entity.PlannedStartDate = dto.PlannedStartDate;
        entity.PlannedEndDate = dto.PlannedEndDate;
        entity.EstimatedParticipants = dto.EstimatedParticipants;
        entity.EstimatedCost = dto.EstimatedCost;
        entity.IsCompleted = dto.IsCompleted;
        entity.CompletionDate = dto.CompletionDate;
        entity.ActualParticipants = dto.ActualParticipants;
        entity.ActualCost = dto.ActualCost;
        entity.FulfilledByScheduleId = dto.FulfilledByScheduleId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TRAINING PLAN BUDGET LINE
    // ========================================================================

    #region TrainingPlanBudgetLine

    public static TrainingPlanBudgetLineDto ToDto(this TrainingPlanBudgetLine entity)
    {
        return new TrainingPlanBudgetLineDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PlanId = entity.PlanId,
            PlanNumber = entity.Plan?.PlanNumber ?? string.Empty,
            Category = entity.Category,
            BudgetedAmount = entity.BudgetedAmount,
            ActualAmount = entity.ActualAmount,
            CommittedAmount = entity.CommittedAmount,
            Notes = entity.Notes,
        };
    }

    public static TrainingPlanBudgetLine ToEntity(this CreateTrainingPlanBudgetLineDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingPlanBudgetLine
        {
            TenantId = tenantId,
            PlanId = dto.PlanId,
            Category = dto.Category,
            BudgetedAmount = dto.BudgetedAmount,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingPlanBudgetLine entity, UpdateTrainingPlanBudgetLineDto dto, Guid userId)
    {
        entity.Category = dto.Category;
        entity.BudgetedAmount = dto.BudgetedAmount;
        entity.ActualAmount = dto.ActualAmount;
        entity.CommittedAmount = dto.CommittedAmount;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // TRAINING NEEDS ASSESSMENT
    // ========================================================================

    #region TrainingNeedsAssessment

    public static TrainingNeedsAssessmentDto ToDto(this TrainingNeedsAssessment entity)
    {
        return new TrainingNeedsAssessmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            EmployeeDepartment = entity.Employee?.Department?.Name,
            EmployeePosition = entity.Employee?.Position?.Title,
            Year = entity.Year,
            Source = entity.Source,
            IdentifiedGaps = entity.IdentifiedGaps,
            Priority = entity.Priority,
            IdentifiedById = entity.IdentifiedById,
            IdentifiedByName = entity.IdentifiedBy?.FullName,
            IdentifiedDate = entity.IdentifiedDate,
            AdditionalNotes = entity.AdditionalNotes,
            TrainingProvided = entity.TrainingProvided,
            TrainingProvidedDate = entity.TrainingProvidedDate,
            RecommendedPrograms = entity.RecommendedPrograms.Select(p => p.ToDto()).ToList(),
            SkillGaps = entity.SkillGaps.Select(s => s.ToDto()).ToList(),
        };
    }

    public static TrainingNeedsAssessmentSummaryDto ToSummaryDto(this TrainingNeedsAssessment entity)
    {
        return new TrainingNeedsAssessmentSummaryDto
        {
            Id = entity.Id,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Year = entity.Year,
            Source = entity.Source,
            Priority = entity.Priority,
            TrainingProvided = entity.TrainingProvided,
            RecommendedProgramsCount = entity.RecommendedPrograms.Count,
            SkillGapsCount = entity.SkillGaps.Count,
        };
    }

    public static TrainingNeedsAssessment ToEntity(this CreateTrainingNeedsAssessmentDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingNeedsAssessment
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            Year = dto.Year,
            Source = dto.Source,
            IdentifiedGaps = dto.IdentifiedGaps,
            Priority = dto.Priority,
            IdentifiedById = dto.IdentifiedById,
            IdentifiedDate = dto.IdentifiedDate,
            AdditionalNotes = dto.AdditionalNotes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this TrainingNeedsAssessment entity, UpdateTrainingNeedsAssessmentDto dto, Guid userId)
    {
        entity.Source = dto.Source;
        entity.IdentifiedGaps = dto.IdentifiedGaps;
        entity.Priority = dto.Priority;
        entity.AdditionalNotes = dto.AdditionalNotes;
        entity.TrainingProvided = dto.TrainingProvided;
        entity.TrainingProvidedDate = dto.TrainingProvidedDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<TrainingNeedsAssessmentSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingNeedsAssessment> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // TRAINING NEEDS ASSESSMENT PROGRAM
    // ========================================================================

    #region TrainingNeedsAssessmentProgram

    public static TrainingNeedsAssessmentProgramDto ToDto(this TrainingNeedsAssessmentProgram entity)
    {
        return new TrainingNeedsAssessmentProgramDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AssessmentId = entity.AssessmentId,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode ?? string.Empty,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            Priority = entity.Priority,
            Rationale = entity.Rationale,
        };
    }

    public static TrainingNeedsAssessmentProgram ToEntity(this CreateTrainingNeedsAssessmentProgramDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingNeedsAssessmentProgram
        {
            TenantId = tenantId,
            AssessmentId = dto.AssessmentId,
            ProgramId = dto.ProgramId,
            Priority = dto.Priority,
            Rationale = dto.Rationale,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING NEEDS ASSESSMENT SKILL
    // ========================================================================

    #region TrainingNeedsAssessmentSkill

    public static TrainingNeedsAssessmentSkillDto ToDto(this TrainingNeedsAssessmentSkill entity)
    {
        return new TrainingNeedsAssessmentSkillDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AssessmentId = entity.AssessmentId,
            SkillId = entity.SkillId,
            SkillName = entity.Skill?.Name ?? string.Empty,
            CurrentProficiency = entity.CurrentProficiency,
            RequiredProficiency = entity.RequiredProficiency,
            GapPriority = entity.GapPriority,
        };
    }

    public static TrainingNeedsAssessmentSkill ToEntity(this CreateTrainingNeedsAssessmentSkillDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingNeedsAssessmentSkill
        {
            TenantId = tenantId,
            AssessmentId = dto.AssessmentId,
            SkillId = dto.SkillId,
            CurrentProficiency = dto.CurrentProficiency,
            RequiredProficiency = dto.RequiredProficiency,
            GapPriority = dto.GapPriority,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING WAITLIST
    // ========================================================================

    #region TrainingWaitlist

    public static TrainingWaitlistDto ToDto(this TrainingWaitlist entity)
    {
        return new TrainingWaitlistDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ScheduleId = entity.ScheduleId,
            ScheduleNumber = entity.Schedule?.ScheduleNumber ?? string.Empty,
            ProgramName = entity.Schedule?.Program?.ProgramName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            Position = entity.Position,
            AddedDate = entity.AddedDate,
            Status = entity.Status,
            OfferDate = entity.OfferDate,
            OfferExpiryDate = entity.OfferExpiryDate,
            ResponseDate = entity.ResponseDate,
            OfferAccepted = entity.OfferAccepted,
            Notes = entity.Notes,
            CreatedNominationId = entity.CreatedNominationId,
            CreatedNominationNumber = entity.Nomination?.NominationNumber,
        };
    }

    public static TrainingWaitlist ToEntity(this CreateTrainingWaitlistDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingWaitlist
        {
            TenantId = tenantId,
            ScheduleId = dto.ScheduleId,
            EmployeeId = dto.EmployeeId,
            Position = 0,
            AddedDate = DateTime.UtcNow,
            Status = TrainingWaitlistStatus.Active,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // TRAINING REQUEST
    // ========================================================================

    #region TrainingRequest

    public static TrainingRequestDto ToDto(this TrainingRequest entity)
    {
        return new TrainingRequestDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            RequestedTrainingTitle = entity.RequestedTrainingTitle,
            Description = entity.Description,
            Justification = entity.Justification,
            RequestDate = entity.RequestDate,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = entity.ApprovalDate,
            RejectionReason = entity.RejectionReason,
            LinkedProgramId = entity.LinkedProgramId,
            LinkedProgramName = entity.LinkedProgram?.ProgramName,
            LinkedProgramCode = entity.LinkedProgram?.ProgramCode,
        };
    }

    public static TrainingRequestSummaryDto ToSummaryDto(this TrainingRequest entity)
    {
        return new TrainingRequestSummaryDto
        {
            Id = entity.Id,
            RequestNumber = entity.RequestNumber,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            RequestedTrainingTitle = entity.RequestedTrainingTitle,
            RequestDate = entity.RequestDate,
            Status = entity.Status,
            LinkedProgramName = entity.LinkedProgram?.ProgramName,
        };
    }

    public static TrainingRequest ToEntity(this CreateTrainingRequestDto dto, Guid tenantId, Guid userId)
    {
        return new TrainingRequest
        {
            TenantId = tenantId,
            RequestNumber = string.Empty,
            EmployeeId = dto.EmployeeId,
            RequestedTrainingTitle = dto.RequestedTrainingTitle,
            Description = dto.Description,
            Justification = dto.Justification,
            RequestDate = dto.RequestDate,
            LinkedProgramId = dto.LinkedProgramId,
            Status = TrainingRequestStatus.Draft,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<TrainingRequestSummaryDto> ToSummaryDtoList(this IEnumerable<TrainingRequest> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static void UpdateEntity(this TrainingRequest entity, UpdateTrainingRequestDto dto, Guid userId)
    {
        entity.RequestedTrainingTitle = dto.RequestedTrainingTitle;
        entity.Description = dto.Description;
        entity.Justification = dto.Justification;
        entity.LinkedProgramId = dto.LinkedProgramId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region LearningPath

    public static LearningPathDto ToDto(this LearningPath entity)
    {
        return new LearningPathDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            Status = entity.Status,
            EstimatedDurationDays = entity.EstimatedDurationDays,
            EstimatedDurationHours = entity.EstimatedDurationHours,
            ProvidesCertificate = entity.ProvidesCertificate,
            CompletionCertificateName = entity.CompletionCertificateName,
            TotalProgramsCount = entity.Programs.Count,
            EnrollmentsCount = entity.Enrollments.Count,
            Programs = entity.Programs.Select(p => p.ToDto()).ToList(),
            TargetSkills = entity.TargetSkills.Select(s => s.ToDto()).ToList(),
        };
    }

    public static LearningPathSummaryDto ToSummaryDto(this LearningPath entity)
    {
        return new LearningPathSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionTitle = entity.Position?.Title,
            Status = entity.Status,
            EstimatedDurationDays = entity.EstimatedDurationDays,
            TotalProgramsCount = entity.Programs.Count,
            ProvidesCertificate = entity.ProvidesCertificate,
        };
    }

    public static LearningPath ToEntity(this CreateLearningPathDto dto, Guid tenantId, Guid userId)
    {
        return new LearningPath
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            Status = dto.Status,
            EstimatedDurationDays = dto.EstimatedDurationDays,
            EstimatedDurationHours = dto.EstimatedDurationHours,
            ProvidesCertificate = dto.ProvidesCertificate,
            CompletionCertificateName = dto.CompletionCertificateName,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this LearningPath entity, UpdateLearningPathDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.Status = dto.Status;
        entity.EstimatedDurationDays = dto.EstimatedDurationDays;
        entity.EstimatedDurationHours = dto.EstimatedDurationHours;
        entity.ProvidesCertificate = dto.ProvidesCertificate;
        entity.CompletionCertificateName = dto.CompletionCertificateName;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<LearningPathSummaryDto> ToSummaryDtoList(this IEnumerable<LearningPath> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // LEARNING PATH PROGRAM
    // ========================================================================

    #region LearningPathProgram

    public static LearningPathProgramDto ToDto(this LearningPathProgram entity)
    {
        return new LearningPathProgramDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            LearningPathId = entity.LearningPathId,
            LearningPathName = entity.LearningPath?.Name ?? string.Empty,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode ?? string.Empty,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            SequenceOrder = entity.SequenceOrder,
            IsMandatory = entity.IsMandatory,
            Notes = entity.Notes,
            PrerequisitePathProgramId = entity.PrerequisitePathProgramId,
            PrerequisiteProgramName = entity.PrerequisitePathProgram?.Program?.ProgramName,
        };
    }

    public static LearningPathProgram ToEntity(this CreateLearningPathProgramDto dto, Guid tenantId, Guid userId)
    {
        return new LearningPathProgram
        {
            TenantId = tenantId,
            LearningPathId = dto.LearningPathId,
            ProgramId = dto.ProgramId,
            SequenceOrder = dto.SequenceOrder,
            IsMandatory = dto.IsMandatory,
            Notes = dto.Notes,
            PrerequisitePathProgramId = dto.PrerequisitePathProgramId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this LearningPathProgram entity, UpdateLearningPathProgramDto dto, Guid userId)
    {
        entity.SequenceOrder = dto.SequenceOrder;
        entity.IsMandatory = dto.IsMandatory;
        entity.Notes = dto.Notes;
        entity.PrerequisitePathProgramId = dto.PrerequisitePathProgramId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // LEARNING PATH SKILL
    // ========================================================================

    #region LearningPathSkill

    public static LearningPathSkillDto ToDto(this LearningPathSkill entity)
    {
        return new LearningPathSkillDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            LearningPathId = entity.LearningPathId,
            SkillId = entity.SkillId,
            SkillName = entity.Skill?.Name ?? string.Empty,
            TargetProficiency = entity.TargetProficiency,
        };
    }

    public static LearningPathSkill ToEntity(this CreateLearningPathSkillDto dto, Guid tenantId, Guid userId)
    {
        return new LearningPathSkill
        {
            TenantId = tenantId,
            LearningPathId = dto.LearningPathId,
            SkillId = dto.SkillId,
            TargetProficiency = dto.TargetProficiency,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // EMPLOYEE LEARNING PATH
    // ========================================================================

    #region EmployeeLearningPath

    public static EmployeeLearningPathDto ToDto(this EmployeeLearningPath entity)
    {
        return new EmployeeLearningPathDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            LearningPathId = entity.LearningPathId,
            LearningPathName = entity.LearningPath?.Name ?? string.Empty,
            EnrolledDate = entity.EnrolledDate,
            TargetCompletionDate = entity.TargetCompletionDate,
            ActualCompletionDate = entity.ActualCompletionDate,
            ProgressPercentage = entity.ProgressPercentage,
            IsCompleted = entity.IsCompleted,
            AssignedById = entity.AssignedById,
            AssignedByName = entity.AssignedBy?.FullName,
            Notes = entity.Notes,
            Steps = entity.Steps.Select(s => s.ToDto()).ToList(),
        };
    }

    public static EmployeeLearningPathSummaryDto ToSummaryDto(this EmployeeLearningPath entity)
    {
        return new EmployeeLearningPathSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            LearningPathName = entity.LearningPath?.Name ?? string.Empty,
            EnrolledDate = entity.EnrolledDate,
            TargetCompletionDate = entity.TargetCompletionDate,
            ProgressPercentage = entity.ProgressPercentage,
            IsCompleted = entity.IsCompleted,
        };
    }

    public static EmployeeLearningPath ToEntity(this EnrollEmployeeInLearningPathDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeLearningPath
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            LearningPathId = dto.LearningPathId,
            EnrolledDate = dto.EnrolledDate,
            TargetCompletionDate = dto.TargetCompletionDate,
            ProgressPercentage = 0,
            IsCompleted = false,
            AssignedById = dto.AssignedById,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<EmployeeLearningPathSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeLearningPath> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static EnrollmentListItemDto ToEnrollmentListItemDto(this EmployeeLearningPath entity)
    {
        return new EnrollmentListItemDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            OrganizationUnitName = entity.Employee?.OrganizationUnit?.Name,
            PositionTitle = entity.Employee?.Position?.Title,
            LearningPathId = entity.LearningPathId,
            LearningPathName = entity.LearningPath?.Name ?? string.Empty,
            EnrolledDate = entity.EnrolledDate,
            TargetCompletionDate = entity.TargetCompletionDate,
            ActualCompletionDate = entity.ActualCompletionDate,
            ProgressPercentage = entity.ProgressPercentage,
            IsCompleted = entity.IsCompleted,
            AssignedByName = entity.AssignedBy?.FullName,
            Notes = entity.Notes,
        };
    }

    #endregion

    // ========================================================================
    // EMPLOYEE LEARNING PATH STEP
    // ========================================================================

    #region EmployeeLearningPathStep

    public static EmployeeLearningPathStepDto ToDto(this EmployeeLearningPathStep entity)
    {
        return new EmployeeLearningPathStepDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeLearningPathId = entity.EmployeeLearningPathId,
            LearningPathProgramId = entity.LearningPathProgramId,
            ProgramName = entity.LearningPathProgram?.Program?.ProgramName ?? string.Empty,
            SequenceOrder = entity.LearningPathProgram?.SequenceOrder ?? 0,
            IsMandatory = entity.LearningPathProgram?.IsMandatory ?? false,
            IsCompleted = entity.IsCompleted,
            CompletedDate = entity.CompletedDate,
            NominationId = entity.NominationId,
            NominationNumber = entity.Nomination?.NominationNumber,
            PrerequisitePathProgramId = entity.LearningPathProgram?.PrerequisitePathProgramId,
            PrerequisiteProgramName = entity.LearningPathProgram?.PrerequisitePathProgram?.Program?.ProgramName,
        };
    }

    public static void UpdateEntity(this EmployeeLearningPathStep entity, UpdateLearningPathStepDto dto, Guid userId)
    {
        entity.IsCompleted = dto.IsCompleted;
        entity.CompletedDate = dto.CompletedDate;
        entity.NominationId = dto.NominationId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // MENTORING PROGRAM
    // ========================================================================

    #region MentoringProgram

    public static MentoringProgramDto ToDto(this MentoringProgram entity)
    {
        return new MentoringProgramDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramName = entity.ProgramName,
            Description = entity.Description,
            Objectives = entity.Objectives,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            SessionsPerMonth = entity.SessionsPerMonth,
            MinutesPerSession = entity.MinutesPerSession,
            IsActive = entity.IsActive,
            CoordinatedById = entity.CoordinatedById,
            CoordinatedByName = entity.CoordinatedBy?.FullName,
            TotalPairsCount = entity.Pairs.Count,
            ActivePairsCount = entity.Pairs.Count(p => p.Status == MentoringStatus.Active),
        };
    }

    public static MentoringProgramSummaryDto ToSummaryDto(this MentoringProgram entity)
    {
        return new MentoringProgramSummaryDto
        {
            Id = entity.Id,
            ProgramName = entity.ProgramName,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsActive = entity.IsActive,
            CoordinatedByName = entity.CoordinatedBy?.FullName,
            TotalPairsCount = entity.Pairs.Count,
            ActivePairsCount = entity.Pairs.Count(p => p.Status == MentoringStatus.Active),
        };
    }

    public static MentoringProgram ToEntity(this CreateMentoringProgramDto dto, Guid tenantId, Guid userId)
    {
        return new MentoringProgram
        {
            TenantId = tenantId,
            ProgramName = dto.ProgramName,
            Description = dto.Description,
            Objectives = dto.Objectives,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            SessionsPerMonth = dto.SessionsPerMonth,
            MinutesPerSession = dto.MinutesPerSession,
            IsActive = dto.IsActive,
            CoordinatedById = dto.CoordinatedById,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MentoringProgram entity, UpdateMentoringProgramDto dto, Guid userId)
    {
        entity.ProgramName = dto.ProgramName;
        entity.Description = dto.Description;
        entity.Objectives = dto.Objectives;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.SessionsPerMonth = dto.SessionsPerMonth;
        entity.MinutesPerSession = dto.MinutesPerSession;
        entity.IsActive = dto.IsActive;
        entity.CoordinatedById = dto.CoordinatedById;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MentoringProgramSummaryDto> ToSummaryDtoList(this IEnumerable<MentoringProgram> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // MENTORING PAIR
    // ========================================================================

    #region MentoringPair

    public static MentoringPairDto ToDto(this MentoringPair entity)
    {
        return new MentoringPairDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            MentorId = entity.MentorId,
            MentorName = entity.Mentor?.FullName ?? string.Empty,
            MentorNumber = entity.Mentor?.EmployeeNumber ?? string.Empty,
            MentorPosition = entity.Mentor?.Position?.Title,
            MenteeId = entity.MenteeId,
            MenteeName = entity.Mentee?.FullName ?? string.Empty,
            MenteeNumber = entity.Mentee?.EmployeeNumber ?? string.Empty,
            MenteePosition = entity.Mentee?.Position?.Title,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            Goals = entity.Goals,
            FocusAreas = entity.FocusAreas,
            ClosureNotes = entity.ClosureNotes,
            MentorRating = entity.MentorRating,
            MenteeRating = entity.MenteeRating,
            TotalSessionsCount = entity.Sessions.Count,
            TotalMinutes = entity.Sessions.Sum(s => s.DurationMinutes),
        };
    }

    public static MentoringPairSummaryDto ToSummaryDto(this MentoringPair entity)
    {
        return new MentoringPairSummaryDto
        {
            Id = entity.Id,
            MentorName = entity.Mentor?.FullName ?? string.Empty,
            MenteeName = entity.Mentee?.FullName ?? string.Empty,
            ProgramName = entity.Program?.ProgramName ?? string.Empty,
            StartDate = entity.StartDate,
            Status = entity.Status,
            TotalSessionsCount = entity.Sessions.Count,
        };
    }

    public static MentoringPair ToEntity(this CreateMentoringPairDto dto, Guid tenantId, Guid userId)
    {
        return new MentoringPair
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            MentorId = dto.MentorId,
            MenteeId = dto.MenteeId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Goals = dto.Goals,
            FocusAreas = dto.FocusAreas,
            Status = MentoringStatus.Active,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MentoringPair entity, UpdateMentoringPairDto dto, Guid userId)
    {
        entity.EndDate = dto.EndDate;
        entity.Status = dto.Status;
        entity.Goals = dto.Goals;
        entity.FocusAreas = dto.FocusAreas;
        entity.ClosureNotes = dto.ClosureNotes;
        entity.MentorRating = dto.MentorRating;
        entity.MenteeRating = dto.MenteeRating;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<MentoringPairSummaryDto> ToSummaryDtoList(this IEnumerable<MentoringPair> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // MENTORING SESSION
    // ========================================================================

    #region MentoringSession

    public static MentoringSessionDto ToDto(this MentoringSession entity)
    {
        return new MentoringSessionDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PairId = entity.PairId,
            MentorName = entity.Pair?.Mentor?.FullName ?? string.Empty,
            MenteeName = entity.Pair?.Mentee?.FullName ?? string.Empty,
            ProgramName = entity.Pair?.Program?.ProgramName ?? string.Empty,
            SessionDate = entity.SessionDate,
            DurationMinutes = entity.DurationMinutes,
            Format = entity.Format,
            TopicsDiscussed = entity.TopicsDiscussed,
            ActionItems = entity.ActionItems,
            MentorNotes = entity.MentorNotes,
            MenteeNotes = entity.MenteeNotes,
            AttendedByMentor = entity.AttendedByMentor,
            AttendedByMentee = entity.AttendedByMentee,
        };
    }

    public static MentoringSession ToEntity(this CreateMentoringSessionDto dto, Guid tenantId, Guid userId)
    {
        return new MentoringSession
        {
            TenantId = tenantId,
            PairId = dto.PairId,
            SessionDate = dto.SessionDate,
            DurationMinutes = dto.DurationMinutes,
            Format = dto.Format,
            TopicsDiscussed = dto.TopicsDiscussed,
            ActionItems = dto.ActionItems,
            MentorNotes = dto.MentorNotes,
            MenteeNotes = dto.MenteeNotes,
            AttendedByMentor = dto.AttendedByMentor,
            AttendedByMentee = dto.AttendedByMentee,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this MentoringSession entity, UpdateMentoringSessionDto dto, Guid userId)
    {
        entity.SessionDate = dto.SessionDate;
        entity.DurationMinutes = dto.DurationMinutes;
        entity.Format = dto.Format;
        entity.TopicsDiscussed = dto.TopicsDiscussed;
        entity.ActionItems = dto.ActionItems;
        entity.MentorNotes = dto.MentorNotes;
        entity.MenteeNotes = dto.MenteeNotes;
        entity.AttendedByMentor = dto.AttendedByMentor;
        entity.AttendedByMentee = dto.AttendedByMentee;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion
}
