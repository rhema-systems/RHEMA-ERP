using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF DISCIPLINE ACTION STEP SERVICE
// ============================================================================

#region Staff Discipline Action Step Service

public class StaffDisciplineActionStepService : IStaffDisciplineActionStepService
{
    private readonly IStaffDisciplineActionStepRepository _actionStepRepository;
    private readonly IStaffDisciplinaryActionRepository _caseRepository;
    private readonly IStaffOffenseProcedureRepository _procedureRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineActionStepService> _logger;

    public StaffDisciplineActionStepService(
        IStaffDisciplineActionStepRepository actionStepRepository,
        IStaffDisciplinaryActionRepository caseRepository,
        IStaffOffenseProcedureRepository procedureRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineActionStepService> logger)
    {
        _actionStepRepository = actionStepRepository;
        _caseRepository = caseRepository;
        _procedureRepository = procedureRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplineActionStep> GetOwnedStepAsync(Guid id)
    {
        var entity = await _actionStepRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Action step with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffDisciplinaryAction> GetOwnedCaseAsync(Guid caseId)
    {
        var entity = await _caseRepository.GetByIdAsync(caseId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Disciplinary case with ID '{caseId}' not found.");
        return entity;
    }

    public async Task<StaffDisciplineActionStepDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _actionStepRepository.GetWithDocumentsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineActionStepDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _actionStepRepository.GetByCaseIdAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineActionStepDto>> GetPendingStepsAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _actionStepRepository.GetPendingStepsAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineActionStepDto>> GetOverdueStepsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _actionStepRepository.GetOverdueStepsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineActionStepDto>> GetByActionedByAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _actionStepRepository.GetByActionedByAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineActionStepDto>> InitialiseFromOffenseProceduresAsync(Guid caseId, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var disciplinaryCase = await GetOwnedCaseAsync(caseId);

        var existingSteps = await _actionStepRepository.GetByCaseIdAsync(caseId);
        if (existingSteps.Any(s => s.TenantId == tenantId))
            throw new InvalidOperationException("Action steps have already been initialised for this case.");

        var procedures = (await _procedureRepository.GetByOffenseIdAsync(disciplinaryCase.StaffOffenseId))
            .Where(p => p.TenantId == tenantId)
            .ToList();

        if (!procedures.Any())
            throw new InvalidOperationException("No procedure steps are defined for the offense associated with this case.");

        var today = DateTime.UtcNow.Date;
        var steps = procedures.Select(p => new StaffDisciplineActionStep
        {
            TenantId             = tenantId,
            DisciplinaryActionId = caseId,
            OffenseProcedureId   = p.Id,
            DueDate              = p.ExpectedCompletionDays.HasValue
                                     ? today.AddDays(p.ExpectedCompletionDays.Value)
                                     : (DateTime?)null,
            Status               = DisciplinaryActionStepStatus.Pending,
            CreatedBy            = userId.ToString(),
        }).ToList();

        foreach (var step in steps)
            await _actionStepRepository.AddAsync(step);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{Count} action steps initialised for case {CaseId}", steps.Count, caseId);

        return steps.Select(s => s.ToDto()).ToList();
    }

    public async Task<StaffDisciplineActionStepDto> UpdateAsync(UpdateActionStepDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedStepAsync(dto.StepId);

        entity.DueDate       = dto.DueDate       ?? entity.DueDate;
        entity.StartedDate   = dto.StartedDate   ?? entity.StartedDate;
        entity.CompletedDate = dto.CompletedDate  ?? entity.CompletedDate;
        entity.Status        = dto.Status;
        entity.ActionedById  = dto.ActionedById  ?? entity.ActionedById;
        entity.Notes         = dto.Notes         ?? entity.Notes;
        entity.UpdatedAt     = DateTime.UtcNow;
        entity.UpdatedBy     = userId.ToString();

        await _actionStepRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteStepAsync(Guid stepId, string notes, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedStepAsync(stepId);

        if (entity.Status == DisciplinaryActionStepStatus.Completed)
            throw new InvalidOperationException("The action step is already completed.");

        entity.Status        = DisciplinaryActionStepStatus.Completed;
        entity.CompletedDate = DateTime.UtcNow;
        entity.Notes         = notes;
        entity.UpdatedAt     = DateTime.UtcNow;
        entity.UpdatedBy     = userId.ToString();

        await _actionStepRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> SkipStepAsync(Guid stepId, string reason, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedStepAsync(stepId);

        if (entity.Status == DisciplinaryActionStepStatus.Completed)
            throw new InvalidOperationException("A completed action step cannot be skipped.");

        entity.Status    = DisciplinaryActionStepStatus.Skipped;
        entity.Notes     = reason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _actionStepRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WITNESS SERVICE
// ============================================================================

#region Staff Discipline Witness Service

public class StaffDisciplineWitnessService : IStaffDisciplineWitnessService
{
    private readonly IStaffDisciplineWitnessRepository _witnessRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineWitnessService> _logger;

    public StaffDisciplineWitnessService(
        IStaffDisciplineWitnessRepository witnessRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineWitnessService> logger)
    {
        _witnessRepository = witnessRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplineWitness> GetOwnedWitnessAsync(Guid id)
    {
        var entity = await _witnessRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Witness with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffDisciplineWitnessDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _witnessRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineWitnessSummaryDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _witnessRepository.GetByCaseIdAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineWitnessSummaryDto>> GetByEmployeeWitnessAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _witnessRepository.GetByEmployeeWitnessAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineWitnessSummaryDto>> GetWithoutStatementAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _witnessRepository.GetWithoutStatementAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<StaffDisciplineWitnessDto> AddAsync(CreateStaffDisciplineWitnessDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);

        await _witnessRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineWitnessDto> UpdateAsync(UpdateStaffDisciplineWitnessDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWitnessAsync(dto.Id);

        entity.UpdateEntity(dto, userId);

        await _witnessRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWitnessAsync(id);

        await _witnessRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE DOCUMENT SERVICE
// ============================================================================

#region Staff Discipline Document Service

public class StaffDisciplineDocumentService : IStaffDisciplineDocumentService
{
    private static readonly string[] AllowedExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".txt"];

    private readonly IStaffDisciplineDocumentRepository _documentRepository;
    private readonly IStaffDisciplineActionStepRepository _actionStepRepository;
    private readonly IStaffDisciplineAppealRepository _appealRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineDocumentService> _logger;

    public StaffDisciplineDocumentService(
        IStaffDisciplineDocumentRepository documentRepository,
        IStaffDisciplineActionStepRepository actionStepRepository,
        IStaffDisciplineAppealRepository appealRepository,
        IFileStorageService fileStorage,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineDocumentService> logger)
    {
        _documentRepository = documentRepository;
        _actionStepRepository = actionStepRepository;
        _appealRepository = appealRepository;
        _fileStorage = fileStorage;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplineDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Document with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffDisciplineDocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _documentRepository.GetByCaseIdAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByScopeAsync(Guid caseId, DisciplinaryDocumentScope scope, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _documentRepository.GetByScopeAsync(caseId, scope);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByActionStepIdAsync(Guid actionStepId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _documentRepository.GetByActionStepIdAsync(actionStepId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByAppealIdAsync(Guid appealId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _documentRepository.GetByAppealIdAsync(appealId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByCategoryAsync(Guid caseId, DisciplinaryDocumentCategory category, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _documentRepository.GetByCategoryAsync(caseId, category);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<StaffDisciplineDocumentDto> AddAsync(CreateStaffDisciplineDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        await ValidateScopeAsync(
            dto.DisciplinaryActionId,
            dto.Scope,
            dto.ActionStepId,
            dto.AppealId,
            cancellationToken);

        var entity = dto.ToEntity(tenantId, userId);

        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    /// <summary>
    /// Validates that the given scope/step/appeal combination is coherent for a case, so the
    /// controller can fail an upload before any bytes are stored.
    /// </summary>
    public Task ValidateDocumentScopeAsync(
        Guid caseId,
        DisciplinaryDocumentScope scope,
        Guid? actionStepId,
        Guid? appealId,
        CancellationToken cancellationToken = default)
        => ValidateScopeAsync(caseId, scope, actionStepId, appealId, cancellationToken);

    public async Task<(Stream Stream, string FileName, string ContentType)?> OpenFileAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity is null || entity.TenantId != tenantId)
            return null;

        if (!await _fileStorage.FileExistsAsync(entity.FilePath))
            throw new FileNotFoundException($"Stored file not found for document '{id}'.");

        var stream = await _fileStorage.DownloadFileAsync(entity.FilePath, entity.Id);
        return (stream, entity.FileName, GetContentType(entity.FileName));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(id);

        var filePath = entity.FilePath;

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(filePath))
                await _fileStorage.DeleteFileAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file for document {DocumentId}", id);
        }

        return true;
    }

    private async Task ValidateScopeAsync(
        Guid caseId,
        DisciplinaryDocumentScope scope,
        Guid? actionStepId,
        Guid? appealId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        switch (scope)
        {
            case DisciplinaryDocumentScope.Case:
                if (actionStepId.HasValue || appealId.HasValue)
                    throw new ArgumentException("Case-scoped documents cannot be linked to a step or appeal.");
                break;

            case DisciplinaryDocumentScope.ActionStep:
                if (!actionStepId.HasValue)
                    throw new ArgumentException("Action step is required for step-scoped documents.");
                if (appealId.HasValue)
                    throw new ArgumentException("Appeal cannot be set for step-scoped documents.");

                var step = await _actionStepRepository.GetByIdAsync(actionStepId.Value);
                if (step is null || step.TenantId != tenantId || step.DisciplinaryActionId != caseId)
                    throw new ArgumentException("The action step does not belong to this case.");
                break;

            case DisciplinaryDocumentScope.Appeal:
                if (!appealId.HasValue)
                    throw new ArgumentException("Appeal is required for appeal-scoped documents.");
                if (actionStepId.HasValue)
                    throw new ArgumentException("Action step cannot be set for appeal-scoped documents.");

                var appeal = await _appealRepository.GetByIdAsync(appealId.Value);
                if (appeal is null || appeal.TenantId != tenantId || appeal.DisciplinaryActionId != caseId)
                    throw new ArgumentException("The appeal does not belong to this case.");
                break;

            default:
                throw new ArgumentException($"Unsupported document scope '{scope}'.");
        }

        await Task.CompletedTask;
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTE SERVICE
// ============================================================================

#region Staff Discipline Note Service

public class StaffDisciplineNoteService : IStaffDisciplineNoteService
{
    private readonly IStaffDisciplineNoteRepository _noteRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineNoteService> _logger;

    public StaffDisciplineNoteService(
        IStaffDisciplineNoteRepository noteRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineNoteService> logger)
    {
        _noteRepository = noteRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplineNote> GetOwnedNoteAsync(Guid id)
    {
        var entity = await _noteRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Note with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffDisciplineNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _noteRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineNoteSummaryDto>> GetByCaseIdAsync(Guid caseId, bool includeConfidential = true, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _noteRepository.GetByCaseIdAsync(caseId, includeConfidential);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineNoteSummaryDto>> GetByAuthorAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _noteRepository.GetByAuthorAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<StaffDisciplineNoteDto> AddAsync(CreateStaffDisciplineNoteDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);

        await _noteRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineNoteDto> UpdateAsync(UpdateStaffDisciplineNoteDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNoteAsync(dto.Id);

        entity.UpdateEntity(dto, userId);

        await _noteRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNoteAsync(id);

        await _noteRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTIFICATION SERVICE
// ============================================================================

#region Staff Discipline Notification Service

public class StaffDisciplineNotificationService : IStaffDisciplineNotificationService
{
    private readonly IStaffDisciplineNotificationRepository _notificationRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineNotificationService> _logger;

    public StaffDisciplineNotificationService(
        IStaffDisciplineNotificationRepository notificationRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineNotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplineNotification> GetOwnedNotificationAsync(Guid id)
    {
        var entity = await _notificationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Notification with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffDisciplineNotificationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _notificationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineNotificationSummaryDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _notificationRepository.GetByCaseIdAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineNotificationSummaryDto>> GetUnacknowledgedAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _notificationRepository.GetUnacknowledgedAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffDisciplineNotificationSummaryDto>> GetPendingFollowupAsync(int daysOld = 3, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _notificationRepository.GetPendingFollowupAsync(daysOld);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<StaffDisciplineNotificationDto> SendAsync(CreateStaffDisciplineNotificationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);

        await _notificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Notification sent for case {CaseId}, Type: {Type}", dto.DisciplinaryActionId, dto.NotificationType);

        return entity.ToDto();
    }

    public async Task<bool> AcknowledgeAsync(AcknowledgeNotificationDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNotificationAsync(dto.NotificationId);

        if (entity.AcknowledgedDate.HasValue)
            throw new InvalidOperationException("This notification has already been acknowledged.");

        entity.AcknowledgedDate = dto.AcknowledgedDate;
        entity.UpdatedAt        = DateTime.UtcNow;

        await _notificationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> SendFollowupAsync(SendFollowupNotificationDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNotificationAsync(dto.NotificationId);

        if (entity.IsFollowupSent)
            throw new InvalidOperationException("A follow-up has already been sent for this notification.");

        entity.IsFollowupSent = true;
        entity.FollowupDate   = dto.FollowupDate;
        entity.UpdatedAt      = DateTime.UtcNow;

        await _notificationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Follow-up notification recorded for notification {NotificationId}", dto.NotificationId);

        return true;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE LEGAL REVIEW SERVICE
// ============================================================================

#region Staff Discipline Legal Review Service

public class StaffDisciplineLegalReviewService : IStaffDisciplineLegalReviewService
{
    private readonly IStaffDisciplineLegalReviewRepository _legalReviewRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffDisciplineLegalReviewService> _logger;

    public StaffDisciplineLegalReviewService(
        IStaffDisciplineLegalReviewRepository legalReviewRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffDisciplineLegalReviewService> logger)
    {
        _legalReviewRepository = legalReviewRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffDisciplineLegalReview> GetOwnedLegalReviewAsync(Guid id)
    {
        var entity = await _legalReviewRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Legal review with ID '{id}' not found.");
        return entity;
    }

    public async Task<StaffDisciplineLegalReviewDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _legalReviewRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _legalReviewRepository.GetByCaseIdAsync(caseId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetOpenReviewsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _legalReviewRepository.GetOpenReviewsAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetByRiskLevelAsync(DisciplineLegalRiskLevel minimumRisk, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _legalReviewRepository.GetByRiskLevelAsync(minimumRisk);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetRequiringExternalCounselAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _legalReviewRepository.GetRequiringExternalCounselAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<decimal> GetTotalLegalCostsForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var reviews = await _legalReviewRepository.GetByCaseIdAsync(caseId);
        return reviews.Where(e => e.TenantId == tenantId).Sum(e => e.LegalCostsIncurred ?? 0m);
    }

    public async Task<StaffDisciplineLegalReviewDto> ReferAsync(CreateStaffDisciplineLegalReviewDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);

        if (entity.ReferredToLegalDate == default)
            entity.ReferredToLegalDate = DateTime.UtcNow;

        await _legalReviewRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Case {CaseId} referred to legal review, Risk: {Risk}", dto.DisciplinaryActionId, dto.LegalRiskLevel);

        return entity.ToDto();
    }

    public async Task<StaffDisciplineLegalReviewDto> UpdateAsync(UpdateStaffDisciplineLegalReviewDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedLegalReviewAsync(dto.Id);

        entity.UpdateEntity(dto, userId);

        await _legalReviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> CompleteAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedLegalReviewAsync(reviewId);

        if (entity.LegalReviewCompleteDate.HasValue)
            throw new InvalidOperationException("This legal review has already been marked as complete.");

        entity.LegalReviewCompleteDate = DateTime.UtcNow;
        entity.UpdatedAt               = DateTime.UtcNow;
        entity.UpdatedBy               = userId.ToString();

        await _legalReviewRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Legal review {ReviewId} marked complete", reviewId);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedLegalReviewAsync(id);

        await _legalReviewRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

#endregion
