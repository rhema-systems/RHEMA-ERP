using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing safety protocols in maintenance operations
/// Handles protocol definitions, compliance tracking, and safety documentation
/// </summary>
public class SafetyProtocolService : ISafetyProtocolService
{
    private readonly ISafetyProtocolRepository _protocolRepository;
    private readonly ISafetyComplianceRepository _complianceRepository;
    private readonly IProtocolAdherenceRepository _protocolAdherenceRepository;
    private readonly IProtocolViolationRepository _protocolViolationRepository;
    private readonly IProtocolTrainingRepository _protocolTrainingRepository;
    private readonly ILogger<SafetyProtocolService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    public SafetyProtocolService(
        ISafetyProtocolRepository protocolRepository,
        ISafetyComplianceRepository complianceRepository,
        IProtocolAdherenceRepository protocolAdherenceRepository,
        IProtocolViolationRepository protocolViolationRepository,
        IProtocolTrainingRepository protocolTrainingRepository,
        ILogger<SafetyProtocolService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _protocolRepository = protocolRepository;
        _complianceRepository = complianceRepository;
        _protocolAdherenceRepository = protocolAdherenceRepository;
        _protocolViolationRepository = protocolViolationRepository;
        _protocolTrainingRepository = protocolTrainingRepository;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    #region CRUD Operations

    public async Task<SafetyProtocolDto> CreateProtocolAsync(CreateSafetyProtocolDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating safety protocol: {ProtocolTitle}", createDto.Title);

            if (!await _protocolRepository.IsCodeUniqueAsync(createDto.Code))
            {
                throw new ArgumentException($"Protocol code '{createDto.Code}' already exists");
            }

            var protocol = new SafetyProtocol
            {
                Id = Guid.NewGuid(),
                Title = createDto.Title,
                Code = createDto.Code,
                Description = createDto.Description,
                Category = createDto.Category,
                RiskLevel = createDto.RiskLevel,
                Procedures = JsonSerializer.Serialize(createDto.Procedures),
                RequiredPPE = JsonSerializer.Serialize(createDto.RequiredPPE),
                RequiredCertifications = JsonSerializer.Serialize(createDto.RequiredCertifications),
                EmergencyProcedures = JsonSerializer.Serialize(createDto.EmergencyProcedures),
                ComplianceCheckpoints = JsonSerializer.Serialize(createDto.ComplianceCheckpoints),
                RegulatorySources = JsonSerializer.Serialize(createDto.RegulatorySources),
                ApplicableEnvironments = JsonSerializer.Serialize(createDto.ApplicableEnvironments),
                EquipmentTypes = JsonSerializer.Serialize(createDto.EquipmentTypes),
                MaintenanceTypes = JsonSerializer.Serialize(createDto.MaintenanceTypes),
                MinimumTrainingLevel = createDto.MinimumTrainingLevel,
                ReviewFrequencyMonths = createDto.ReviewFrequencyMonths,
                LastReviewDate = createDto.LastReviewDate,
                NextReviewDate = createDto.NextReviewDate,
                ReviewedBy = createDto.ReviewedBy,
                ApprovedBy = createDto.ApprovedBy,
                ApprovalDate = createDto.ApprovalDate,
                EffectiveDate = createDto.EffectiveDate,
                ExpirationDate = createDto.ExpirationDate,
                DocumentVersion = createDto.DocumentVersion,
                IsActive = createDto.IsActive,
                IsRegulatory = createDto.IsRegulatory,
                CreatedById = _currentUserProvider.UserId,
                TenantId = _currentUserProvider.TenantId
            };

            await _protocolRepository.AddAsync(protocol);

            _logger.LogInformation("Created safety protocol {ProtocolId} successfully", protocol.Id);

            return await MapToDto(protocol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating safety protocol: {ProtocolTitle}", createDto.Title);
            throw;
        }
    }

    public async Task<SafetyProtocolDto> UpdateProtocolAsync(Guid id, UpdateSafetyProtocolDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating safety protocol: {ProtocolId}", id);

            var protocol = await _protocolRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Protocol with ID {id} not found");
            protocol.Title = updateDto.Title;
            protocol.Description = updateDto.Description;
            protocol.Category = updateDto.Category;
            protocol.RiskLevel = updateDto.RiskLevel;
            protocol.Procedures = JsonSerializer.Serialize(updateDto.Procedures);
            protocol.RequiredPPE = JsonSerializer.Serialize(updateDto.RequiredPPE);
            protocol.RequiredCertifications = JsonSerializer.Serialize(updateDto.RequiredCertifications);
            protocol.EmergencyProcedures = JsonSerializer.Serialize(updateDto.EmergencyProcedures);
            protocol.ComplianceCheckpoints = JsonSerializer.Serialize(updateDto.ComplianceCheckpoints);
            protocol.RegulatorySources = JsonSerializer.Serialize(updateDto.RegulatorySources);
            protocol.ApplicableEnvironments = JsonSerializer.Serialize(updateDto.ApplicableEnvironments);
            protocol.EquipmentTypes = JsonSerializer.Serialize(updateDto.EquipmentTypes);
            protocol.MaintenanceTypes = JsonSerializer.Serialize(updateDto.MaintenanceTypes);
            protocol.MinimumTrainingLevel = updateDto.MinimumTrainingLevel;
            protocol.ReviewFrequencyMonths = updateDto.ReviewFrequencyMonths;
            protocol.LastReviewDate = updateDto.LastReviewDate;
            protocol.NextReviewDate = updateDto.NextReviewDate;
            protocol.ReviewedBy = updateDto.ReviewedBy;
            protocol.ApprovedBy = updateDto.ApprovedBy;
            protocol.ApprovalDate = updateDto.ApprovalDate;
            protocol.EffectiveDate = updateDto.EffectiveDate;
            protocol.ExpirationDate = updateDto.ExpirationDate;
            protocol.DocumentVersion = updateDto.DocumentVersion;
            protocol.IsActive = updateDto.IsActive;
            protocol.IsRegulatory = updateDto.IsRegulatory;
            protocol.LastModifiedById = _currentUserProvider.UserId;

            await _protocolRepository.UpdateAsync(protocol);

            _logger.LogInformation("Updated safety protocol {ProtocolId} successfully", id);

            return await MapToDto(protocol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating safety protocol: {ProtocolId}", id);
            throw;
        }
    }

    public async Task DeleteProtocolAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting safety protocol: {ProtocolId}", id);

            var protocol = await _protocolRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Protocol with ID {id} not found");
            if (protocol.IsRegulatory)
            {
                throw new InvalidOperationException("Cannot delete regulatory protocols. Deactivate instead.");
            }

            await _protocolRepository.DeleteAsync(id);

            _logger.LogInformation("Deleted safety protocol {ProtocolId} successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting safety protocol: {ProtocolId}", id);
            throw;
        }
    }

    public async Task<SafetyProtocolDto?> GetProtocolByIdAsync(Guid id)
    {
        var protocol = await _protocolRepository.GetByIdAsync(id);
        return protocol != null ? await MapToDto(protocol) : null;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetAllProtocolsAsync()
    {
        var protocols = await _protocolRepository.GetAllAsync();
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<PagedResult<SafetyProtocolDto>> GetProtocolsPagedAsync(SafetyProtocolFilterDto filter)
    {
        var allProtocols = await _protocolRepository.GetAllAsync();
        var filtered = allProtocols.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            filtered = filtered.Where(p => p.Title.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          p.Code.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          p.Description.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(filter.Category))
        {
            filtered = filtered.Where(p => p.Category == filter.Category);
        }

        if (!string.IsNullOrEmpty(filter.RiskLevel))
        {
            filtered = filtered.Where(p => p.RiskLevel == filter.RiskLevel);
        }

        if (filter.IsActive.HasValue)
        {
            filtered = filtered.Where(p => p.IsActive == filter.IsActive.Value);
        }

        if (filter.IsRegulatory.HasValue)
        {
            filtered = filtered.Where(p => p.IsRegulatory == filter.IsRegulatory.Value);
        }

        if (filter.NeedsReview.HasValue && filter.NeedsReview.Value)
        {
            filtered = filtered.Where(p => p.NextReviewDate <= DateTime.UtcNow.AddDays(30));
        }

        if (filter.IsExpired.HasValue && filter.IsExpired.Value)
        {
            filtered = filtered.Where(p => p.ExpirationDate <= DateTime.UtcNow);
        }

        var totalCount = filtered.Count();
        var protocolsPage = filtered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var items = new List<SafetyProtocolDto>();
        foreach (var protocol in protocolsPage)
        {
            items.Add(await MapToDto(protocol));
        }

        return new PagedResult<SafetyProtocolDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    #endregion

    #region Business Logic

    public async Task<IEnumerable<SafetyProtocolDto>> GetActiveProtocolsAsync()
    {
        var protocols = await _protocolRepository.GetActiveAsync();
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetProtocolsByCategoryAsync(string category)
    {
        var protocols = await _protocolRepository.GetByCategoryAsync(category);
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetProtocolsByRiskLevelAsync(string riskLevel)
    {
        var protocols = await _protocolRepository.GetByRiskLevelAsync(riskLevel);
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetMandatoryProtocolsAsync()
    {
        var protocols = await _protocolRepository.GetMandatoryAsync();
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetProtocolsBySeverityAsync(string severity)
    {
        var protocols = await _protocolRepository.GetBySeverityAsync(severity);
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetProtocolsByRegulatoryStandardAsync(string standard)
    {
        var protocols = await _protocolRepository.GetByRegulatoryStandardAsync(standard);
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetOverdueProtocolsAsync()
    {
        var protocols = await _protocolRepository.GetOverdueAsync();
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetProtocolsDueForReviewAsync()
    {
        var protocols = await _protocolRepository.GetDueForReviewAsync();
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<IEnumerable<SafetyProtocolDto>> GetExpiredProtocolsAsync()
    {
        var protocols = await _protocolRepository.GetExpiredAsync();
        var result = new List<SafetyProtocolDto>();

        foreach (var protocol in protocols)
        {
            result.Add(await MapToDto(protocol));
        }

        return result;
    }

    public async Task<SafetyProtocolDto> ToggleProtocolStatusAsync(Guid id)
    {
        var protocol = await _protocolRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Protocol with ID {id} not found");
        protocol.IsActive = !protocol.IsActive;
        protocol.LastModifiedById = _currentUserProvider.UserId;

        await _protocolRepository.UpdateAsync(protocol);

        return await MapToDto(protocol);
    }

    public async Task<bool> IsProtocolCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        return await _protocolRepository.IsCodeUniqueAsync(code, excludeId);
    }

    // Approval workflow methods
    public async Task SubmitForApprovalAsync(Guid protocolId)
    {
        var protocol = await _protocolRepository.GetByIdAsync(protocolId) ?? throw new ArgumentException($"Protocol with ID {protocolId} not found");
        protocol.Status = "PendingApproval";
        protocol.LastModifiedById = _currentUserProvider.UserId;
        await _protocolRepository.UpdateAsync(protocol);

        _logger.LogInformation("Submitted protocol {ProtocolId} for approval", protocolId);
    }

    public async Task ApproveProtocolAsync(Guid protocolId, ApprovalRequestDto approvalRequest)
    {
        var protocol = await _protocolRepository.GetByIdAsync(protocolId) ?? throw new ArgumentException($"Protocol with ID {protocolId} not found");
        protocol.Status = "Approved";
        protocol.ApprovedBy = approvalRequest.ApproverName;
        protocol.ApprovalDate = DateTime.UtcNow;
        protocol.LastModifiedById = _currentUserProvider.UserId;
        await _protocolRepository.UpdateAsync(protocol);

        _logger.LogInformation("Approved protocol {ProtocolId}", protocolId);
    }

    public async Task RejectProtocolAsync(Guid protocolId, ApprovalRequestDto approvalRequest)
    {
        var protocol = await _protocolRepository.GetByIdAsync(protocolId) ?? throw new ArgumentException($"Protocol with ID {protocolId} not found");
        protocol.Status = "Rejected";
        protocol.LastModifiedById = _currentUserProvider.UserId;
        await _protocolRepository.UpdateAsync(protocol);

        _logger.LogInformation("Rejected protocol {ProtocolId}", protocolId);
    }

    public async Task RequestChangesAsync(Guid protocolId, ApprovalRequestDto approvalRequest)
    {
        var protocol = await _protocolRepository.GetByIdAsync(protocolId) ?? throw new ArgumentException($"Protocol with ID {protocolId} not found");
        protocol.Status = "ChangesRequested";
        protocol.LastModifiedById = _currentUserProvider.UserId;
        await _protocolRepository.UpdateAsync(protocol);

        _logger.LogInformation("Requested changes for protocol {ProtocolId}", protocolId);
    }

    // Compliance tracking methods
    public async Task RecordAdherenceAsync(CreateProtocolAdherenceDto createDto)
    {
        var adherence = new ProtocolAdherence
        {
            Id = Guid.NewGuid(),
            ProtocolId = createDto.ProtocolId,
            TechnicianId = createDto.TechnicianId,
            WorkOrderId = createDto.WorkOrderId ?? Guid.Empty,
            AdherenceDate = createDto.AdherenceDate,
            AdherenceLevel = createDto.AdherenceLevel,
            Notes = createDto.Notes,
            CreatedById = _currentUserProvider.UserId,
            TenantId = _currentUserProvider.TenantId
        };

        await _protocolAdherenceRepository.AddAsync(adherence);
        _logger.LogInformation("Recorded protocol adherence {AdherenceId}", adherence.Id);
    }

    public async Task RecordViolationAsync(CreateProtocolViolationDto createDto)
    {
        var violation = new ProtocolViolation
        {
            Id = Guid.NewGuid(),
            ProtocolId = createDto.ProtocolId,
            TechnicianId = createDto.TechnicianId,
            WorkOrderId = createDto.WorkOrderId,
            ViolationDate = createDto.ViolationDate,
            ViolationType = createDto.ViolationType,
            Severity = createDto.Severity,
            Description = createDto.Description,
            CorrectiveAction = createDto.CorrectiveAction,
            CreatedById = _currentUserProvider.UserId,
            TenantId = _currentUserProvider.TenantId
        };

        await _protocolViolationRepository.AddAsync(violation);
        _logger.LogInformation("Recorded protocol violation {ViolationId}", violation.Id);
    }

    public async Task<IEnumerable<ProtocolAdherenceDto>> GetAdherenceHistoryAsync(Guid protocolId)
    {
        var adherenceRecords = await _protocolAdherenceRepository.GetByProtocolIdAsync(protocolId);
        return adherenceRecords.Select(r => new ProtocolAdherenceDto
        {
            Id = r.Id,
            ProtocolId = r.ProtocolId,
            TechnicianId = r.TechnicianId,
            WorkOrderId = r.WorkOrderId,
            AdherenceDate = r.AdherenceDate,
            AdherenceLevel = r.AdherenceLevel,
            Notes = r.Notes
        });
    }

    public async Task<IEnumerable<ProtocolViolationDto>> GetViolationHistoryAsync(Guid protocolId)
    {
        var violations = await _protocolViolationRepository.GetByProtocolIdAsync(protocolId);
        return violations.Select(v => new ProtocolViolationDto
        {
            Id = v.Id,
            ProtocolId = v.ProtocolId,
            TechnicianId = v.TechnicianId,
            WorkOrderId = v.WorkOrderId,
            ViolationDate = v.ViolationDate,
            ViolationType = v.ViolationType,
            Severity = v.Severity,
            Description = v.Description,
            CorrectiveAction = v.CorrectiveAction
        });
    }

    // Training tracking methods
    public async Task RecordTrainingAsync(CreateProtocolTrainingDto createDto)
    {
        var training = new ProtocolTraining
        {
            Id = Guid.NewGuid(),
            ProtocolId = createDto.ProtocolId,
            TechnicianId = createDto.TechnicianId,
            TrainingDate = createDto.TrainingDate,
            TrainingType = createDto.TrainingType,
            TrainerName = createDto.TrainerName,
            CompletionStatus = createDto.CompletionStatus,
            Score = createDto.Score,
            CertificationIssued = createDto.CertificationIssued,
            Notes = createDto.Notes,
            CreatedById = _currentUserProvider.UserId,
            TenantId = _currentUserProvider.TenantId
        };

        await _protocolTrainingRepository.AddAsync(training);
        _logger.LogInformation("Recorded protocol training {TrainingId}", training.Id);
    }

    public async Task<IEnumerable<ProtocolTrainingDto>> GetProtocolTrainingHistoryAsync(Guid protocolId)
    {
        var trainings = await _protocolTrainingRepository.GetByProtocolIdAsync(protocolId);
        return trainings.Select(t => new ProtocolTrainingDto
        {
            Id = t.Id,
            ProtocolId = t.ProtocolId,
            TechnicianId = t.TechnicianId,
            TrainingDate = t.TrainingDate,
            TrainingType = t.TrainingType,
            TrainerName = t.TrainerName,
            CompletionStatus = t.CompletionStatus,
            Score = t.Score,
            CertificationIssued = t.CertificationIssued,
            Notes = t.Notes
        });
    }

    public async Task<IEnumerable<ProtocolTrainingDto>> GetTechnicianTrainingHistoryAsync(Guid technicianId)
    {
        var trainings = await _protocolTrainingRepository.GetByTechnicianIdAsync(technicianId);
        return trainings.Select(t => new ProtocolTrainingDto
        {
            Id = t.Id,
            ProtocolId = t.ProtocolId,
            TechnicianId = t.TechnicianId,
            TrainingDate = t.TrainingDate,
            TrainingType = t.TrainingType,
            TrainerName = t.TrainerName,
            CompletionStatus = t.CompletionStatus,
            Score = t.Score,
            CertificationIssued = t.CertificationIssued,
            Notes = t.Notes
        });
    }

    // Reporting methods
    public async Task<ComplianceReportDto> GetComplianceReportAsync(DateTime startDate, DateTime endDate)
    {
        return await GenerateComplianceReportAsync(startDate, endDate);
    }

    public async Task<IEnumerable<SafetyProtocolComplianceDto>> GetProtocolComplianceAsync(DateTime startDate, DateTime endDate)
    {
        var protocols = await _protocolRepository.GetAllAsync();
        var adherenceRecords = await _protocolAdherenceRepository.GetByDateRangeAsync(startDate, endDate);
        var violationRecords = await _protocolViolationRepository.GetByDateRangeAsync(startDate, endDate);

        return protocols.Select(p => new SafetyProtocolComplianceDto
        {
            ProtocolId = p.Id,
            ProtocolTitle = p.Title,
            ProtocolCode = p.Code,
            TotalAdherence = adherenceRecords.Count(a => a.ProtocolId == p.Id),
            TotalViolations = violationRecords.Count(v => v.ProtocolId == p.Id),
            ComplianceRate = adherenceRecords.Any(a => a.ProtocolId == p.Id)
                ? (decimal)adherenceRecords.Count(a => a.ProtocolId == p.Id && a.AdherenceLevel == "Full") / adherenceRecords.Count(a => a.ProtocolId == p.Id) * 100
                : 0
        });
    }

    public async Task<IEnumerable<CategoryComplianceDto>> GetCategoryComplianceAsync()
    {
        var protocols = await _protocolRepository.GetAllAsync();
        var adherenceRecords = await _protocolAdherenceRepository.GetAllAsync();
        var violationRecords = await _protocolViolationRepository.GetAllAsync();

        return protocols.GroupBy(p => p.Category).Select(g => new CategoryComplianceDto
        {
            Category = g.Key,
            TotalProtocols = g.Count(),
            TotalAdherence = adherenceRecords.Count(a => g.Any(p => p.Id == a.ProtocolId)),
            TotalViolations = violationRecords.Count(v => g.Any(p => p.Id == v.ProtocolId)),
            ComplianceRate = adherenceRecords.Any(a => g.Any(p => p.Id == a.ProtocolId))
                ? (decimal)adherenceRecords.Count(a => g.Any(p => p.Id == a.ProtocolId) && a.AdherenceLevel == "Full") / adherenceRecords.Count(a => g.Any(p => p.Id == a.ProtocolId)) * 100
                : 0
        });
    }

    public async Task<SafetyAnalyticsDto> GetSafetyAnalyticsAsync(DateTime startDate, DateTime endDate)
    {
        var protocols = await _protocolRepository.GetAllAsync();
        var complianceRecords = await _complianceRepository.GetByDateRangeAsync(startDate, endDate);

        return new SafetyAnalyticsDto
        {
            TotalProtocols = protocols.Count(),
            ActiveProtocols = protocols.Count(p => p.IsActive),
            RegulatoryProtocols = protocols.Count(p => p.IsRegulatory),
            ProtocolsDueForReview = protocols.Count(p => p.NextReviewDate <= DateTime.UtcNow.AddDays(30)),
            ExpiredProtocols = protocols.Count(p => p.ExpirationDate <= DateTime.UtcNow),
            TotalComplianceChecks = complianceRecords.Count(),
            OverallComplianceRate = complianceRecords.Any() ?
                (decimal)complianceRecords.Count(r => r.ComplianceStatus == "Compliant") / complianceRecords.Count() * 100 : 0,
            AnalysisPeriodStart = startDate,
            AnalysisPeriodEnd = endDate
        };
    }

    #endregion

    #region Compliance Management

    public async Task<SafetyComplianceRecordDto> RecordComplianceCheckAsync(CreateSafetyComplianceRecordDto createDto)
    {
        try
        {
            _logger.LogInformation("Recording compliance check for protocol {ProtocolId}", createDto.ProtocolId);

            var protocol = await _protocolRepository.GetByIdAsync(createDto.ProtocolId) ?? throw new ArgumentException($"Protocol with ID {createDto.ProtocolId} not found");
            var complianceRecord = new SafetyComplianceRecord
            {
                Id = Guid.NewGuid(),
                ProtocolId = createDto.ProtocolId,
                WorkOrderId = createDto.WorkOrderId,
                TechnicianId = createDto.TechnicianId,
                CheckDate = createDto.CheckDate,
                ComplianceStatus = createDto.ComplianceStatus,
                ChecklistItems = JsonSerializer.Serialize(createDto.ChecklistItems),
                Violations = JsonSerializer.Serialize(createDto.Violations),
                CorrectiveActions = JsonSerializer.Serialize(createDto.CorrectiveActions),
                Notes = createDto.Notes,
                InspectorId = createDto.InspectorId,
                InspectorNotes = createDto.InspectorNotes,
                CreatedById = _currentUserProvider.UserId,
                TenantId = _currentUserProvider.TenantId
            };

            await _complianceRepository.AddAsync(complianceRecord);

            // Update protocol compliance statistics
            await UpdateProtocolComplianceStats(createDto.ProtocolId);

            _logger.LogInformation("Recorded compliance check {ComplianceId} for protocol {ProtocolId}",
                complianceRecord.Id, createDto.ProtocolId);

            return MapComplianceToDto(complianceRecord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording compliance check for protocol {ProtocolId}", createDto.ProtocolId);
            throw;
        }
    }

    public async Task<SafetyComplianceRecordDto> UpdateComplianceRecordAsync(Guid id, UpdateSafetyComplianceRecordDto updateDto)
    {
        var record = await _complianceRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Compliance record with ID {id} not found");
        record.ComplianceStatus = updateDto.ComplianceStatus;
        record.ChecklistItems = JsonSerializer.Serialize(updateDto.ChecklistItems);
        record.Violations = JsonSerializer.Serialize(updateDto.Violations);
        record.CorrectiveActions = JsonSerializer.Serialize(updateDto.CorrectiveActions);
        record.Notes = updateDto.Notes;
        record.InspectorId = updateDto.InspectorId;
        record.InspectorNotes = updateDto.InspectorNotes;
        record.LastModifiedById = _currentUserProvider.UserId;

        await _complianceRepository.UpdateAsync(record);

        // Update protocol compliance statistics
        await UpdateProtocolComplianceStats(record.ProtocolId);

        return MapComplianceToDto(record);
    }

    public async Task<IEnumerable<SafetyComplianceRecordDto>> GetProtocolComplianceHistoryAsync(Guid protocolId)
    {
        var records = await _complianceRepository.GetByProtocolIdAsync(protocolId);
        return records.Select(MapComplianceToDto);
    }

    public async Task<IEnumerable<SafetyComplianceRecordDto>> GetTechnicianComplianceRecordsAsync(Guid technicianId)
    {
        var records = await _complianceRepository.GetByTechnicianIdAsync(technicianId);
        return records.Select(MapComplianceToDto);
    }

    public async Task<ComplianceReportDto> GenerateComplianceReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Generating compliance report from {StartDate} to {EndDate}", startDate, endDate);

        var records = await _complianceRepository.GetByDateRangeAsync(startDate, endDate);

        var totalChecks = records.Count();
        var compliantChecks = records.Count(r => r.ComplianceStatus == "Compliant");
        var nonCompliantChecks = records.Count(r => r.ComplianceStatus == "Non-Compliant");
        var partiallyCompliantChecks = records.Count(r => r.ComplianceStatus == "Partially Compliant");

        var violationsByProtocol = records
            .Where(r => !string.IsNullOrEmpty(r.Violations))
            .GroupBy(r => r.ProtocolId)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        return new ComplianceReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalChecks = totalChecks,
            CompliantChecks = compliantChecks,
            NonCompliantChecks = nonCompliantChecks,
            PartiallyCompliantChecks = partiallyCompliantChecks,
            ComplianceRate = totalChecks > 0 ? (decimal)compliantChecks / totalChecks * 100 : 0,
            ViolationsByProtocol = violationsByProtocol,
            GeneratedDate = DateTime.UtcNow
        };
    }

    #endregion

    #region Analytics

    public async Task<SafetyAnalyticsDto> GetSafetyAnalyticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddMonths(-12);
        var end = endDate ?? DateTime.UtcNow;

        var protocols = await _protocolRepository.GetAllAsync();
        var complianceRecords = await _complianceRepository.GetByDateRangeAsync(start, end);

        return new SafetyAnalyticsDto
        {
            TotalProtocols = protocols.Count(),
            ActiveProtocols = protocols.Count(p => p.IsActive),
            RegulatoryProtocols = protocols.Count(p => p.IsRegulatory),
            ProtocolsDueForReview = protocols.Count(p => p.NextReviewDate <= DateTime.UtcNow.AddDays(30)),
            ExpiredProtocols = protocols.Count(p => p.ExpirationDate <= DateTime.UtcNow),
            TotalComplianceChecks = complianceRecords.Count(),
            OverallComplianceRate = complianceRecords.Any() ?
                (decimal)complianceRecords.Count(r => r.ComplianceStatus == "Compliant") / complianceRecords.Count() * 100 : 0,
            AnalysisPeriodStart = start,
            AnalysisPeriodEnd = end
        };
    }

    #endregion

    #region Private Methods

    private static async Task<SafetyProtocolDto> MapToDto(SafetyProtocol protocol)
    {
        return new SafetyProtocolDto
        {
            Id = protocol.Id,
            Title = protocol.Title,
            Code = protocol.Code,
            Description = protocol.Description,
            Category = protocol.Category,
            RiskLevel = protocol.RiskLevel,
            Procedures = DeserializeStringList(protocol.Procedures),
            RequiredPPE = DeserializeStringList(protocol.RequiredPPE),
            RequiredCertifications = DeserializeStringList(protocol.RequiredCertifications),
            EmergencyProcedures = DeserializeStringList(protocol.EmergencyProcedures),
            ComplianceCheckpoints = DeserializeStringList(protocol.ComplianceCheckpoints),
            RegulatorySources = DeserializeStringList(protocol.RegulatorySources),
            ApplicableEnvironments = DeserializeStringList(protocol.ApplicableEnvironments),
            EquipmentTypes = DeserializeStringList(protocol.EquipmentTypes),
            MaintenanceTypes = DeserializeStringList(protocol.MaintenanceTypes),
            MinimumTrainingLevel = protocol.MinimumTrainingLevel,
            ReviewFrequencyMonths = protocol.ReviewFrequencyMonths,
            LastReviewDate = protocol.LastReviewDate,
            NextReviewDate = protocol.NextReviewDate,
            ReviewedBy = protocol.ReviewedBy,
            ApprovedBy = protocol.ApprovedBy,
            ApprovalDate = protocol.ApprovalDate,
            EffectiveDate = protocol.EffectiveDate,
            ExpirationDate = protocol.ExpirationDate,
            DocumentVersion = protocol.DocumentVersion,
            IsActive = protocol.IsActive,
            IsRegulatory = protocol.IsRegulatory,
            ComplianceScore = protocol.ComplianceScore,
            TotalViolations = protocol.TotalViolations,
            CreatedDate = protocol.CreatedDate,
            CreatedBy = "Created By Name", // Would be resolved
            LastModifiedDate = protocol.LastModifiedDate,
            LastModifiedBy = "Modified By Name" // Would be resolved
        };
    }

    private SafetyComplianceRecordDto MapComplianceToDto(SafetyComplianceRecord record)
    {
        return new SafetyComplianceRecordDto
        {
            Id = record.Id,
            SafetyProtocolId = record.ProtocolId,
            WorkOrderId = record.WorkOrderId,
            TechnicianId = record.TechnicianId,
            ComplianceDate = record.CheckDate,
            ComplianceStatus = record.ComplianceStatus,
            ChecklistItems = DeserializeDtoList<ComplianceChecklistItemDto>(record.ChecklistItems),
            Violations = DeserializeDtoList<ComplianceViolationDto>(record.Violations),
            CorrectiveActions = record.CorrectiveActions,
            Notes = record.Notes,
            InspectorId = record.InspectorId,
            InspectorNotes = record.InspectorNotes,
            CreatedAt = record.CheckDate // Use CheckDate as the creation timestamp
        };
    }

    private async Task UpdateProtocolComplianceStats(Guid protocolId)
    {
        try
        {
            var records = await _complianceRepository.GetByProtocolIdAsync(protocolId);
            var recentRecords = records.Where(r => r.CheckDate >= DateTime.UtcNow.AddMonths(-6)).ToList();

            if (recentRecords.Any())
            {
                var compliantCount = recentRecords.Count(r => r.ComplianceStatus == "Compliant");
                var complianceScore = (decimal)compliantCount / recentRecords.Count * 100;
                var totalViolations = recentRecords.Sum(r => DeserializeDtoList<ComplianceViolationDto>(r.Violations).Count);

                var protocol = await _protocolRepository.GetByIdAsync(protocolId);
                if (protocol != null)
                {
                    protocol.ComplianceScore = complianceScore;
                    protocol.TotalViolations = totalViolations;
                    await _protocolRepository.UpdateAsync(protocol);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating compliance stats for protocol {ProtocolId}", protocolId);
            // Don't throw here as this is a background operation
        }
    }

    private static List<string> DeserializeStringList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>();
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(trimmed);
            return values?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList() ?? new List<string>();
        }
        catch (JsonException)
        {
            return trimmed
                .Split(new[] { "\r\n", "\n", "\r", ";", "|", "," }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }
    }

    private static List<T> DeserializeDtoList<T>(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(value) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }

    #endregion
}
