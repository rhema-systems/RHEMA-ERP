using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectDrawingDto>> GetProjectDrawingsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        return (await GetProjectDrawingEntitiesAsync(projectId))
            .OrderBy(x => x.DrawingNumber)
            .ThenBy(x => x.Title)
            .Select(x => MapProjectDrawingDto(x, phases))
            .ToList();
    }

    public async Task<ProjectDrawingDto> AddProjectDrawingAsync(Guid projectId, CreateProjectDrawingDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var superseded = await ValidateSupersededDrawingAsync(projectId, dto.SupersedesDrawingId,
            dto.DrawingNumber, dto.Revision, null);

        var entity = new ProjectDrawing
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            SupersedesDrawingId = superseded?.Id,
            DrawingNumber = dto.DrawingNumber.Trim(),
            Title = dto.Title.Trim(),
            Discipline = NormalizeProjectDrawingDiscipline(dto.Discipline),
            Revision = TrimOrNull(dto.Revision),
            Status = NormalizeProjectDrawingStatus(dto.Status),
            IssuedDate = dto.IssuedDate,
            ReviewDueDate = dto.ReviewDueDate,
            ApprovedDate = dto.ApprovedDate,
            IsAsBuilt = dto.IsAsBuilt,
            ResponsibleParty = TrimOrNull(dto.ResponsibleParty),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectDrawing>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectDrawingDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectDrawingDto> UpdateProjectDrawingAsync(Guid drawingId, UpdateProjectDrawingDto dto)
    {
        var entity = await GetProjectDrawingEntityAsync(drawingId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var superseded = await ValidateSupersededDrawingAsync(entity.ProjectId, dto.SupersedesDrawingId,
            dto.DrawingNumber, dto.Revision, entity.Id);

        entity.ProjectPhaseId = phase?.Id;
        entity.SupersedesDrawingId = superseded?.Id;
        entity.DrawingNumber = dto.DrawingNumber.Trim();
        entity.Title = dto.Title.Trim();
        entity.Discipline = NormalizeProjectDrawingDiscipline(dto.Discipline);
        entity.Revision = TrimOrNull(dto.Revision);
        entity.Status = NormalizeProjectDrawingStatus(dto.Status);
        entity.IssuedDate = dto.IssuedDate;
        entity.ReviewDueDate = dto.ReviewDueDate;
        entity.ApprovedDate = dto.ApprovedDate;
        entity.IsAsBuilt = dto.IsAsBuilt;
        entity.ResponsibleParty = TrimOrNull(dto.ResponsibleParty);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectDrawing>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectDrawingDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectDrawingAsync(Guid drawingId)
    {
        var entity = await GetProjectDrawingEntityAsync(drawingId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        await _unitOfWork.Repository<ProjectDrawing>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectSubmittalDto>> GetProjectSubmittalsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectSubmittalsAsync(
            (await GetProjectSubmittalEntitiesAsync(projectId)).ToList(),
            (await GetProjectPhaseEntitiesAsync(projectId)).ToList(),
            (await GetProjectPackageEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectSubmittalDto> AddProjectSubmittalAsync(Guid projectId, CreateProjectSubmittalDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(projectId, dto.ProjectPackageId);

        var entity = new ProjectSubmittal
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            ProjectPackageId = package?.Id,
            SubmittalType = NormalizeProjectSubmittalType(dto.SubmittalType),
            ReferenceNumber = TrimOrNull(dto.ReferenceNumber),
            Title = dto.Title.Trim(),
            Status = NormalizeProjectSubmittalStatus(dto.Status),
            SubmittedDate = dto.SubmittedDate,
            ResponseDueDate = dto.ResponseDueDate,
            RespondedDate = dto.RespondedDate,
            SubmittedByName = TrimOrNull(dto.SubmittedByName),
            ReviewedByName = TrimOrNull(dto.ReviewedByName),
            ResponsibleParty = TrimOrNull(dto.ResponsibleParty),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectSubmittal>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectSubmittalDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectSubmittalDto> UpdateProjectSubmittalAsync(Guid submittalId, UpdateProjectSubmittalDto dto)
    {
        var entity = await GetProjectSubmittalEntityAsync(submittalId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(entity.ProjectId, dto.ProjectPackageId);

        entity.ProjectPhaseId = phase?.Id;
        entity.ProjectPackageId = package?.Id;
        entity.SubmittalType = NormalizeProjectSubmittalType(dto.SubmittalType);
        entity.ReferenceNumber = TrimOrNull(dto.ReferenceNumber);
        entity.Title = dto.Title.Trim();
        entity.Status = NormalizeProjectSubmittalStatus(dto.Status);
        entity.SubmittedDate = dto.SubmittedDate;
        entity.ResponseDueDate = dto.ResponseDueDate;
        entity.RespondedDate = dto.RespondedDate;
        entity.SubmittedByName = TrimOrNull(dto.SubmittedByName);
        entity.ReviewedByName = TrimOrNull(dto.ReviewedByName);
        entity.ResponsibleParty = TrimOrNull(dto.ResponsibleParty);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectSubmittal>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectSubmittalDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectSubmittalAsync(Guid submittalId)
    {
        var entity = await GetProjectSubmittalEntityAsync(submittalId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        await _unitOfWork.Repository<ProjectSubmittal>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectRfiDto>> GetProjectRfisAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectRfisAsync(
            (await GetProjectRfiEntitiesAsync(projectId)).ToList(),
            (await GetProjectPhaseEntitiesAsync(projectId)).ToList(),
            (await GetProjectPackageEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectRfiDto> AddProjectRfiAsync(Guid projectId, CreateProjectRfiDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(projectId, dto.ProjectPackageId);

        var entity = new ProjectRfi
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            ProjectPackageId = package?.Id,
            ReferenceNumber = TrimOrNull(dto.ReferenceNumber),
            Subject = dto.Subject.Trim(),
            Question = dto.Question.Trim(),
            Priority = NormalizeProjectRfiPriority(dto.Priority),
            Status = NormalizeProjectRfiStatus(dto.Status),
            RaisedDate = dto.RaisedDate ?? DateTime.UtcNow,
            ResponseDueDate = dto.ResponseDueDate,
            RespondedDate = dto.RespondedDate,
            RaisedByName = TrimOrNull(dto.RaisedByName),
            RespondedByName = TrimOrNull(dto.RespondedByName),
            ImpactSummary = TrimOrNull(dto.ImpactSummary),
            Response = TrimOrNull(dto.Response),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectRfi>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectRfiDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectRfiDto> UpdateProjectRfiAsync(Guid rfiId, UpdateProjectRfiDto dto)
    {
        var entity = await GetProjectRfiEntityAsync(rfiId);
        if (entity.CivilDesignCaseId.HasValue)
            throw new InvalidOperationException("Civil design-input requests must use the governed cross-section response and review endpoints.");
        if (await _unitOfWork.Repository<ProjectCivilRfiRouting>().ExistsAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectRfiId == rfiId && !value.IsDeleted))
            throw new InvalidOperationException("A governed Civil supervision RFI must use the Project Engineer and Project Manager routing endpoints.");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(entity.ProjectId, dto.ProjectPackageId);

        entity.ProjectPhaseId = phase?.Id;
        entity.ProjectPackageId = package?.Id;
        entity.ReferenceNumber = TrimOrNull(dto.ReferenceNumber);
        entity.Subject = dto.Subject.Trim();
        entity.Question = dto.Question.Trim();
        entity.Priority = NormalizeProjectRfiPriority(dto.Priority);
        entity.Status = NormalizeProjectRfiStatus(dto.Status);
        entity.RaisedDate = dto.RaisedDate ?? entity.RaisedDate;
        entity.ResponseDueDate = dto.ResponseDueDate;
        entity.RespondedDate = dto.RespondedDate;
        entity.RaisedByName = TrimOrNull(dto.RaisedByName);
        entity.RespondedByName = TrimOrNull(dto.RespondedByName);
        entity.ImpactSummary = TrimOrNull(dto.ImpactSummary);
        entity.Response = TrimOrNull(dto.Response);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectRfi>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectRfiDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectRfiAsync(Guid rfiId)
    {
        var entity = await GetProjectRfiEntityAsync(rfiId);
        if (entity.CivilDesignCaseId.HasValue)
            throw new InvalidOperationException("A governed Civil design-input request cannot be deleted through the generic RFI endpoint.");
        if (await _unitOfWork.Repository<ProjectCivilRfiRouting>().ExistsAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectRfiId == rfiId && !value.IsDeleted))
            throw new InvalidOperationException("A governed Civil supervision RFI cannot be deleted through the generic RFI endpoint.");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        await _unitOfWork.Repository<ProjectRfi>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectSiteInstructionDto>> GetProjectSiteInstructionsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectSiteInstructionsAsync(
            (await GetProjectSiteInstructionEntitiesAsync(projectId)).ToList(),
            (await GetProjectPhaseEntitiesAsync(projectId)).ToList(),
            (await GetProjectPackageEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectSiteInstructionDto> AddProjectSiteInstructionAsync(Guid projectId, CreateProjectSiteInstructionDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        if (string.Equals(NormalizeProjectSiteInstructionType(dto.InstructionType), ProjectSiteInstructionTypes.EngineerInstruction, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the governed Civil Engineering site-instruction route for Engineer Instructions.");
        var phase = await ValidateProjectDesignPhaseAsync(projectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(projectId, dto.ProjectPackageId);

        var entity = new ProjectSiteInstruction
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = phase?.Id,
            ProjectPackageId = package?.Id,
            InstructionType = NormalizeProjectSiteInstructionType(dto.InstructionType),
            ReferenceNumber = TrimOrNull(dto.ReferenceNumber),
            Title = dto.Title.Trim(),
            Description = TrimOrNull(dto.Description),
            Status = NormalizeProjectSiteInstructionStatus(dto.Status),
            IssuedDate = dto.IssuedDate ?? DateTime.UtcNow,
            EffectiveDate = dto.EffectiveDate,
            ClosedDate = dto.ClosedDate,
            IssuedByName = TrimOrNull(dto.IssuedByName),
            ResponsibleParty = TrimOrNull(dto.ResponsibleParty),
            EstimatedCostImpact = dto.EstimatedCostImpact,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency),
            ScheduleImpactDays = dto.ScheduleImpactDays,
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectSiteInstruction>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectSiteInstructionDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectSiteInstructionDto> UpdateProjectSiteInstructionAsync(Guid siteInstructionId, UpdateProjectSiteInstructionDto dto)
    {
        var entity = await GetProjectSiteInstructionEntityAsync(siteInstructionId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        if (string.Equals(entity.InstructionType, ProjectSiteInstructionTypes.EngineerInstruction, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeProjectSiteInstructionType(dto.InstructionType), ProjectSiteInstructionTypes.EngineerInstruction, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Engineer Instructions are immutable through the generic editor; use governed Civil Engineering routing actions.");
        var phase = await ValidateProjectDesignPhaseAsync(entity.ProjectId, dto.ProjectPhaseId);
        var package = await ValidateProjectDesignPackageAsync(entity.ProjectId, dto.ProjectPackageId);

        entity.ProjectPhaseId = phase?.Id;
        entity.ProjectPackageId = package?.Id;
        entity.InstructionType = NormalizeProjectSiteInstructionType(dto.InstructionType);
        entity.ReferenceNumber = TrimOrNull(dto.ReferenceNumber);
        entity.Title = dto.Title.Trim();
        entity.Description = TrimOrNull(dto.Description);
        entity.Status = NormalizeProjectSiteInstructionStatus(dto.Status);
        entity.IssuedDate = dto.IssuedDate ?? entity.IssuedDate;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ClosedDate = dto.ClosedDate;
        entity.IssuedByName = TrimOrNull(dto.IssuedByName);
        entity.ResponsibleParty = TrimOrNull(dto.ResponsibleParty);
        entity.EstimatedCostImpact = dto.EstimatedCostImpact;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? entity.Currency);
        entity.ScheduleImpactDays = dto.ScheduleImpactDays;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectSiteInstruction>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectSiteInstructionDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectSiteInstructionAsync(Guid siteInstructionId)
    {
        var entity = await GetProjectSiteInstructionEntityAsync(siteInstructionId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        if (string.Equals(entity.InstructionType, ProjectSiteInstructionTypes.EngineerInstruction, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Engineer Instructions cannot be deleted through the generic editor; use the governed Civil Engineering lifecycle.");
        await _unitOfWork.Repository<ProjectSiteInstruction>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ProjectDrawingDto> GetProjectDrawingDtoAsync(Guid projectId, Guid drawingId)
    {
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var entity = await GetProjectDrawingEntityAsync(drawingId);
        return MapProjectDrawingDto(entity, phases);
    }

    private async Task<ProjectSubmittalDto> GetProjectSubmittalDtoAsync(Guid projectId, Guid submittalId)
    {
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToList();
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToList();
        var entity = await GetProjectSubmittalEntityAsync(submittalId);
        return (await MapProjectSubmittalsAsync([entity], phases, packages)).Single();
    }

    private async Task<ProjectRfiDto> GetProjectRfiDtoAsync(Guid projectId, Guid rfiId)
    {
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToList();
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToList();
        var entity = await GetProjectRfiEntityAsync(rfiId);
        return (await MapProjectRfisAsync([entity], phases, packages)).Single();
    }

    private async Task<ProjectSiteInstructionDto> GetProjectSiteInstructionDtoAsync(Guid projectId, Guid siteInstructionId)
    {
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToList();
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToList();
        var entity = await GetProjectSiteInstructionEntityAsync(siteInstructionId);
        return (await MapProjectSiteInstructionsAsync([entity], phases, packages)).Single();
    }

    private async Task<List<ProjectDrawing>> GetProjectDrawingEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectDrawing>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<List<ProjectSubmittal>> GetProjectSubmittalEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectSubmittal>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<List<ProjectRfi>> GetProjectRfiEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectRfi>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<List<ProjectSiteInstruction>> GetProjectSiteInstructionEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectSiteInstruction>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
    }

    private async Task<ProjectDrawing> GetProjectDrawingEntityAsync(Guid drawingId)
        => await _unitOfWork.Repository<ProjectDrawing>().GetByIdAsync(drawingId)
            ?? throw new InvalidOperationException("Project drawing was not found.");

    private async Task<ProjectSubmittal> GetProjectSubmittalEntityAsync(Guid submittalId)
        => await _unitOfWork.Repository<ProjectSubmittal>().GetByIdAsync(submittalId)
            ?? throw new InvalidOperationException("Project submittal was not found.");

    private async Task<ProjectRfi> GetProjectRfiEntityAsync(Guid rfiId)
        => await _unitOfWork.Repository<ProjectRfi>().GetByIdAsync(rfiId)
            ?? throw new InvalidOperationException("Project RFI was not found.");

    private async Task<ProjectSiteInstruction> GetProjectSiteInstructionEntityAsync(Guid siteInstructionId)
        => await _unitOfWork.Repository<ProjectSiteInstruction>().GetByIdAsync(siteInstructionId)
            ?? throw new InvalidOperationException("Project site instruction was not found.");

    private async Task<ProjectPhase?> ValidateProjectDesignPhaseAsync(Guid projectId, Guid? projectPhaseId)
    {
        if (!projectPhaseId.HasValue)
        {
            return null;
        }

        var phase = await GetProjectPhaseEntityAsync(projectPhaseId.Value);
        if (phase.ProjectId != projectId)
        {
            throw new InvalidOperationException("Selected phase must belong to the current project.");
        }

        return phase;
    }

    private async Task<ProjectPackage?> ValidateProjectDesignPackageAsync(Guid projectId, Guid? projectPackageId)
    {
        if (!projectPackageId.HasValue)
        {
            return null;
        }

        var package = await GetProjectPackageEntityAsync(projectPackageId.Value);
        if (package.ProjectId != projectId)
        {
            throw new InvalidOperationException("Selected package must belong to the current project.");
        }

        return package;
    }

    private static ProjectDrawingDto MapProjectDrawingDto(ProjectDrawing entity, IReadOnlyDictionary<Guid, ProjectPhase> phases)
        => new()
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            ProjectPhaseId = entity.ProjectPhaseId,
            ProjectPhaseName = entity.ProjectPhaseId.HasValue && phases.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
            SupersedesDrawingId = entity.SupersedesDrawingId,
            DrawingNumber = entity.DrawingNumber,
            Title = entity.Title,
            Discipline = entity.Discipline,
            Revision = entity.Revision,
            Status = entity.Status,
            IssuedDate = entity.IssuedDate,
            ReviewDueDate = entity.ReviewDueDate,
            ApprovedDate = entity.ApprovedDate,
            IsAsBuilt = entity.IsAsBuilt,
            ResponsibleParty = entity.ResponsibleParty,
            Notes = entity.Notes
        };

    private async Task<ProjectDrawing?> ValidateSupersededDrawingAsync(
        Guid projectId, Guid? supersedesDrawingId, string? drawingNumber, string? revision, Guid? currentDrawingId)
    {
        if (!supersedesDrawingId.HasValue) return null;
        if (currentDrawingId == supersedesDrawingId)
            throw new InvalidOperationException("A drawing cannot supersede itself.");
        var prior = await _unitOfWork.Repository<ProjectDrawing>().GetByIdAsync(supersedesDrawingId.Value)
            ?? throw new InvalidOperationException("The selected prior drawing revision was not found.");
        if (prior.TenantId != _currentUserProvider.TenantId || prior.ProjectId != projectId || prior.IsDeleted)
            throw new InvalidOperationException("The selected prior drawing revision must belong to this tenant and project.");
        if (!string.Equals(prior.DrawingNumber.Trim(), drawingNumber?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A revised drawing must retain the prior drawing number.");
        if (string.Equals(prior.Revision?.Trim(), revision?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A revised drawing must use a different revision code.");
        return prior;
    }

    private static Task<List<ProjectSubmittalDto>> MapProjectSubmittalsAsync(
        IReadOnlyCollection<ProjectSubmittal> entities,
        IReadOnlyCollection<ProjectPhase> phases,
        IReadOnlyCollection<ProjectPackage> packages)
    {
        var phaseLookup = phases.ToDictionary(x => x.Id);
        var packageLookup = packages.ToDictionary(x => x.Id);
        return Task.FromResult(entities
            .OrderByDescending(x => x.SubmittedDate ?? x.CreatedAt)
            .ThenBy(x => x.Title)
            .Select(entity => new ProjectSubmittalDto
            {
                Id = entity.Id,
                ProjectId = entity.ProjectId,
                ProjectPhaseId = entity.ProjectPhaseId,
                ProjectPhaseName = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
                ProjectPackageId = entity.ProjectPackageId,
                ProjectPackageName = entity.ProjectPackageId.HasValue && packageLookup.TryGetValue(entity.ProjectPackageId.Value, out var package) ? package.Name : null,
                SubmittalType = entity.SubmittalType,
                ReferenceNumber = entity.ReferenceNumber,
                Title = entity.Title,
                Status = entity.Status,
                SubmittedDate = entity.SubmittedDate,
                ResponseDueDate = entity.ResponseDueDate,
                RespondedDate = entity.RespondedDate,
                SubmittedByName = entity.SubmittedByName,
                ReviewedByName = entity.ReviewedByName,
                ResponsibleParty = entity.ResponsibleParty,
                Notes = entity.Notes
            })
            .ToList());
    }

    private static Task<List<ProjectRfiDto>> MapProjectRfisAsync(
        IReadOnlyCollection<ProjectRfi> entities,
        IReadOnlyCollection<ProjectPhase> phases,
        IReadOnlyCollection<ProjectPackage> packages)
    {
        var phaseLookup = phases.ToDictionary(x => x.Id);
        var packageLookup = packages.ToDictionary(x => x.Id);
        return Task.FromResult(entities
            .OrderByDescending(x => x.RaisedDate)
            .ThenBy(x => x.Subject)
            .Select(entity => new ProjectRfiDto
            {
                Id = entity.Id,
                ProjectId = entity.ProjectId,
                ProjectPhaseId = entity.ProjectPhaseId,
                ProjectPhaseName = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
                ProjectPackageId = entity.ProjectPackageId,
                ProjectPackageName = entity.ProjectPackageId.HasValue && packageLookup.TryGetValue(entity.ProjectPackageId.Value, out var package) ? package.Name : null,
                ReferenceNumber = entity.ReferenceNumber,
                Subject = entity.Subject,
                Question = entity.Question,
                Priority = entity.Priority,
                Status = entity.Status,
                RaisedDate = entity.RaisedDate,
                ResponseDueDate = entity.ResponseDueDate,
                RespondedDate = entity.RespondedDate,
                RaisedByName = entity.RaisedByName,
                RespondedByName = entity.RespondedByName,
                ImpactSummary = entity.ImpactSummary,
                Response = entity.Response,
                Notes = entity.Notes
            })
            .ToList());
    }

    private static Task<List<ProjectSiteInstructionDto>> MapProjectSiteInstructionsAsync(
        IReadOnlyCollection<ProjectSiteInstruction> entities,
        IReadOnlyCollection<ProjectPhase> phases,
        IReadOnlyCollection<ProjectPackage> packages)
    {
        var phaseLookup = phases.ToDictionary(x => x.Id);
        var packageLookup = packages.ToDictionary(x => x.Id);
        return Task.FromResult(entities
            .OrderByDescending(x => x.IssuedDate)
            .ThenBy(x => x.Title)
            .Select(entity => new ProjectSiteInstructionDto
            {
                Id = entity.Id,
                ProjectId = entity.ProjectId,
                ProjectPhaseId = entity.ProjectPhaseId,
                ProjectPhaseName = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var phase) ? phase.Name : null,
                ProjectPackageId = entity.ProjectPackageId,
                ProjectPackageName = entity.ProjectPackageId.HasValue && packageLookup.TryGetValue(entity.ProjectPackageId.Value, out var package) ? package.Name : null,
                InstructionType = entity.InstructionType,
                ReferenceNumber = entity.ReferenceNumber,
                Title = entity.Title,
                Description = entity.Description,
                Status = entity.Status,
                IssuedDate = entity.IssuedDate,
                EffectiveDate = entity.EffectiveDate,
                ClosedDate = entity.ClosedDate,
                IssuedByName = entity.IssuedByName,
                ResponsibleParty = entity.ResponsibleParty,
                EstimatedCostImpact = entity.EstimatedCostImpact,
                Currency = entity.Currency,
                ScheduleImpactDays = entity.ScheduleImpactDays,
                Notes = entity.Notes
            })
            .ToList());
    }

    private static string NormalizeProjectDrawingStatus(string? value)
        => value?.Trim() switch
        {
            ProjectDrawingStatuses.ForReview => ProjectDrawingStatuses.ForReview,
            ProjectDrawingStatuses.ApprovedForConstruction => ProjectDrawingStatuses.ApprovedForConstruction,
            ProjectDrawingStatuses.ApprovedAsBuilt => ProjectDrawingStatuses.ApprovedAsBuilt,
            ProjectDrawingStatuses.Superseded => ProjectDrawingStatuses.Superseded,
            ProjectDrawingStatuses.Archived => ProjectDrawingStatuses.Archived,
            _ => ProjectDrawingStatuses.Draft
        };

    private static string NormalizeProjectDrawingDiscipline(string? value)
        => value?.Trim() switch
        {
            ProjectDrawingDisciplines.Architectural => ProjectDrawingDisciplines.Architectural,
            ProjectDrawingDisciplines.Structural => ProjectDrawingDisciplines.Structural,
            ProjectDrawingDisciplines.Mechanical => ProjectDrawingDisciplines.Mechanical,
            ProjectDrawingDisciplines.Electrical => ProjectDrawingDisciplines.Electrical,
            ProjectDrawingDisciplines.Plumbing => ProjectDrawingDisciplines.Plumbing,
            ProjectDrawingDisciplines.Civil => ProjectDrawingDisciplines.Civil,
            ProjectDrawingDisciplines.FireProtection => ProjectDrawingDisciplines.FireProtection,
            ProjectDrawingDisciplines.Interior => ProjectDrawingDisciplines.Interior,
            _ => ProjectDrawingDisciplines.Other
        };

    private static string NormalizeProjectSubmittalStatus(string? value)
        => value?.Trim() switch
        {
            ProjectSubmittalStatuses.Submitted => ProjectSubmittalStatuses.Submitted,
            ProjectSubmittalStatuses.UnderReview => ProjectSubmittalStatuses.UnderReview,
            ProjectSubmittalStatuses.Approved => ProjectSubmittalStatuses.Approved,
            ProjectSubmittalStatuses.ApprovedWithComments => ProjectSubmittalStatuses.ApprovedWithComments,
            ProjectSubmittalStatuses.Rejected => ProjectSubmittalStatuses.Rejected,
            ProjectSubmittalStatuses.ResubmissionRequired => ProjectSubmittalStatuses.ResubmissionRequired,
            ProjectSubmittalStatuses.Closed => ProjectSubmittalStatuses.Closed,
            _ => ProjectSubmittalStatuses.Draft
        };

    private static string NormalizeProjectSubmittalType(string? value)
        => value?.Trim() switch
        {
            ProjectSubmittalTypes.Material => ProjectSubmittalTypes.Material,
            ProjectSubmittalTypes.ShopDrawing => ProjectSubmittalTypes.ShopDrawing,
            ProjectSubmittalTypes.MethodStatement => ProjectSubmittalTypes.MethodStatement,
            ProjectSubmittalTypes.Sample => ProjectSubmittalTypes.Sample,
            ProjectSubmittalTypes.TechnicalData => ProjectSubmittalTypes.TechnicalData,
            ProjectSubmittalTypes.Mockup => ProjectSubmittalTypes.Mockup,
            _ => ProjectSubmittalTypes.Other
        };

    private static string NormalizeProjectRfiStatus(string? value)
        => value?.Trim() switch
        {
            ProjectRfiStatuses.Submitted => ProjectRfiStatuses.Submitted,
            ProjectRfiStatuses.Answered => ProjectRfiStatuses.Answered,
            ProjectRfiStatuses.Closed => ProjectRfiStatuses.Closed,
            ProjectRfiStatuses.Void => ProjectRfiStatuses.Void,
            _ => ProjectRfiStatuses.Draft
        };

    private static string NormalizeProjectRfiPriority(string? value)
        => value?.Trim() switch
        {
            ProjectRfiPriorities.Low => ProjectRfiPriorities.Low,
            ProjectRfiPriorities.High => ProjectRfiPriorities.High,
            ProjectRfiPriorities.Critical => ProjectRfiPriorities.Critical,
            _ => ProjectRfiPriorities.Medium
        };

    private static string NormalizeProjectSiteInstructionStatus(string? value)
        => value?.Trim() switch
        {
            ProjectSiteInstructionStatuses.Issued => ProjectSiteInstructionStatuses.Issued,
            ProjectSiteInstructionStatuses.Acknowledged => ProjectSiteInstructionStatuses.Acknowledged,
            ProjectSiteInstructionStatuses.InProgress => ProjectSiteInstructionStatuses.InProgress,
            ProjectSiteInstructionStatuses.Completed => ProjectSiteInstructionStatuses.Completed,
            ProjectSiteInstructionStatuses.Closed => ProjectSiteInstructionStatuses.Closed,
            ProjectSiteInstructionStatuses.Cancelled => ProjectSiteInstructionStatuses.Cancelled,
            _ => ProjectSiteInstructionStatuses.Draft
        };

    private static string NormalizeProjectSiteInstructionType(string? value)
        => value?.Trim() switch
        {
            ProjectSiteInstructionTypes.ArchitectInstruction => ProjectSiteInstructionTypes.ArchitectInstruction,
            ProjectSiteInstructionTypes.EngineerInstruction => ProjectSiteInstructionTypes.EngineerInstruction,
            ProjectSiteInstructionTypes.VariationInstruction => ProjectSiteInstructionTypes.VariationInstruction,
            ProjectSiteInstructionTypes.SafetyInstruction => ProjectSiteInstructionTypes.SafetyInstruction,
            ProjectSiteInstructionTypes.QualityInstruction => ProjectSiteInstructionTypes.QualityInstruction,
            ProjectSiteInstructionTypes.Other => ProjectSiteInstructionTypes.Other,
            _ => ProjectSiteInstructionTypes.SiteInstruction
        };
}
