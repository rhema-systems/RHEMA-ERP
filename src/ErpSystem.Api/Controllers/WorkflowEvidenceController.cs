using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/workflow/evidence")]
[Authorize]
public sealed class WorkflowEvidenceController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;

    public WorkflowEvidenceController(ApplicationDbContext db, ICurrentUserService currentUser, IFileStorageService storage)
    { _db = db; _currentUser = currentUser; _storage = storage; }

    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy(CancellationToken cancellationToken)
    {
        var policy = await _db.WorkflowEvidencePolicies.AsNoTracking().Where(item =>
            item.TenantId == TenantId && !item.IsDeleted && item.IsActive).OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return Ok(new { success = true, data = policy });
    }

    [HttpPut("policy")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> SavePolicy([FromBody] SaveWorkflowEvidencePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MaximumFileSizeBytes <= 0 || request.RetentionDays < 2555 || request.AllowedExtensions.Count == 0)
            return BadRequest("File size, allowed extensions, and a retention of at least 2,555 days are required.");
        var current = await _db.WorkflowEvidencePolicies.Where(item =>
            item.TenantId == TenantId && !item.IsDeleted && item.IsActive).ToListAsync(cancellationToken);
        foreach (var item in current) item.IsActive = false;
        var policy = new WorkflowEvidencePolicy
        {
            TenantId = TenantId,
            AllowedExtensionsJson = JsonSerializer.Serialize(request.AllowedExtensions
                .Select(value => value.Trim().ToLowerInvariant()).Distinct().ToList()),
            MaximumFileSizeBytes = request.MaximumFileSizeBytes,
            RetentionDays = request.RetentionDays,
            RequireMalwareScan = request.RequireMalwareScan,
            IsActive = true,
            CreatedById = UserId,
            CreatedBy = _currentUser.UserName
        };
        _db.WorkflowEvidencePolicies.Add(policy);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = policy });
    }

    [HttpGet("step/{stepInstanceId:guid}")]
    public async Task<IActionResult> GetStepEvidence(Guid stepInstanceId, CancellationToken cancellationToken)
    {
        var rows = await _db.WorkflowEvidenceDocuments.AsNoTracking().Where(item =>
            item.TenantId == TenantId && item.StepInstanceId == stepInstanceId && !item.IsDeleted)
            .OrderBy(item => item.DocumentName).ThenByDescending(item => item.Version)
            .Select(item => new { item.Id, item.AttachmentId, item.DocumentName, item.DocumentType, item.FileName,
                item.Sha256, item.DocumentOwnerId, item.IssueDate, item.ExpiryDate, IsExpired = item.ExpiryDate < DateTime.UtcNow,
                item.Version, item.ReplacesEvidenceId, item.IsCurrent, item.VerificationStatus, item.VerifiedById,
                item.VerifiedAt, item.VerificationNotes, item.MalwareScanStatus, item.RetainUntil, item.IsLegalHold })
            .ToListAsync(cancellationToken);
        return Ok(new { success = true, data = rows });
    }

    [HttpGet("review/instances")]
    public async Task<IActionResult> ReviewInstances([FromQuery] string? search,
        [FromQuery] string? entityType, [FromQuery] Guid? workflowDefinitionId,
        [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 10, 100);
        var query = _db.WorkflowInstances.AsNoTracking()
            .Include(item => item.WorkflowDefinition)
            .Include(item => item.EntityType)
            .Include(item => item.StepInstances).ThenInclude(step => step.WorkflowStep)
            .Where(item => item.TenantId == TenantId && !item.IsDeleted);

        if (workflowDefinitionId.HasValue)
            query = query.Where(item => item.WorkflowDefinitionId == workflowDefinitionId.Value);

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim();
            query = query.Where(item => item.EntityType.Name == normalizedEntityType ||
                item.EntityType.Code == normalizedEntityType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var guidSearch = Guid.TryParse(term, out var entityId);
            query = query.Where(item => item.WorkflowDefinition.Name.Contains(term) ||
                item.EntityType.Name.Contains(term) ||
                (guidSearch && item.EntityId == entityId));
        }

        var instances = await query
            .OrderByDescending(item => item.UpdatedAt ?? item.StartedDate ?? item.CreatedDate)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var stepInstanceIds = instances.SelectMany(item => item.StepInstances)
            .Select(item => item.Id)
            .ToList();
        var evidenceCounts = await _db.WorkflowEvidenceDocuments.AsNoTracking()
            .Where(item => item.TenantId == TenantId && !item.IsDeleted && stepInstanceIds.Contains(item.StepInstanceId))
            .GroupBy(item => item.StepInstanceId)
            .Select(group => new
            {
                stepInstanceId = group.Key,
                total = group.Count(),
                pending = group.Count(item => item.VerificationStatus == WorkflowEvidenceVerificationStatus.Pending),
                verified = group.Count(item => item.VerificationStatus == WorkflowEvidenceVerificationStatus.Verified),
                rejected = group.Count(item => item.VerificationStatus == WorkflowEvidenceVerificationStatus.Rejected),
                legalHold = group.Count(item => item.IsLegalHold)
            })
            .ToDictionaryAsync(item => item.stepInstanceId, cancellationToken);

        var rows = instances.Select(item =>
        {
            var orderedSteps = item.StepInstances
                .OrderBy(step => step.WorkflowStep.Order)
                .ThenBy(step => step.WorkflowStep.Name)
                .ToList();
            var currentStepInstanceId = item.CurrentStepId.HasValue
                ? orderedSteps.FirstOrDefault(step => step.WorkflowStepId == item.CurrentStepId.Value)?.Id
                : null;

            return new
            {
                id = item.Id,
                workflowDefinitionId = item.WorkflowDefinitionId,
                workflowName = item.WorkflowDefinition.Name,
                entityType = item.EntityType.Name,
                item.EntityId,
                item.Status,
                item.StartedDate,
                item.CompletedDate,
                item.CreatedDate,
                currentStepInstanceId,
                steps = orderedSteps.Select(step =>
                {
                    evidenceCounts.TryGetValue(step.Id, out var counts);
                    return new
                    {
                        stepInstanceId = step.Id,
                        workflowStepId = step.WorkflowStepId,
                        stepName = step.WorkflowStep.Name,
                        status = step.Status,
                        step.StartedDate,
                        step.CompletedDate,
                        step.DueDate,
                        evidence = counts ?? new { stepInstanceId = step.Id, total = 0, pending = 0, verified = 0, rejected = 0, legalHold = 0 }
                    };
                }).ToList()
            };
        }).ToList();

        return Ok(new { success = true, data = rows });
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager,InternalAudit")]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyWorkflowEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = await Find(id, cancellationToken);
        if (evidence == null) return NotFound();
        evidence.VerificationStatus = request.Accepted
            ? WorkflowEvidenceVerificationStatus.Verified : WorkflowEvidenceVerificationStatus.Rejected;
        evidence.VerifiedById = UserId;
        evidence.VerifiedAt = DateTime.UtcNow;
        evidence.VerificationNotes = request.Notes?.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = evidence });
    }

    [HttpPost("{id:guid}/legal-hold")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,InternalAudit")]
    public async Task<IActionResult> LegalHold(Guid id, [FromBody] WorkflowLegalHoldRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = await Find(id, cancellationToken);
        if (evidence == null) return NotFound();
        if (request.Enabled && string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("A legal hold reason is required.");
        evidence.IsLegalHold = request.Enabled;
        evidence.LegalHoldReason = request.Enabled ? request.Reason!.Trim() : null;
        evidence.LegalHoldById = request.Enabled ? UserId : null;
        evidence.LegalHoldAt = request.Enabled ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = evidence });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var evidence = await Find(id, cancellationToken);
        if (evidence == null) return NotFound();
        if (evidence.IsLegalHold) return Conflict("Evidence under legal hold cannot be deleted.");
        if (evidence.RetainUntil > DateTime.UtcNow) return Conflict($"Evidence is retained until {evidence.RetainUntil:yyyy-MM-dd}.");
        await _storage.DeleteFileAsync(evidence.FilePath);
        evidence.IsDeleted = true;
        evidence.DeletedAt = DateTime.UtcNow;
        evidence.DeletedBy = _currentUser.UserName;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpGet("signatures/{approvalId:guid}")]
    public async Task<IActionResult> Signature(Guid approvalId, CancellationToken cancellationToken)
    {
        var signature = await _db.WorkflowSignatureEvidence.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && item.ApprovalId == approvalId && !item.IsDeleted, cancellationToken);
        return signature == null ? NotFound() : Ok(new { success = true, data = signature });
    }

    [HttpPost("signatures/{approvalId:guid}/stage")]
    public async Task<IActionResult> StageSignature(Guid approvalId, [FromBody] WorkflowSignatureSubmissionDto submission,
        CancellationToken cancellationToken)
    {
        var approval = await _db.WorkflowApprovals.Include(item => item.StepInstance).ThenInclude(step => step.WorkflowStep)
            .FirstOrDefaultAsync(item => item.Id == approvalId && item.TenantId == TenantId && !item.IsDeleted,
                cancellationToken);
        if (approval == null) return NotFound();
        var roles = new HashSet<string>(_currentUser.Roles ?? [], StringComparer.OrdinalIgnoreCase);
        if (approval.Status != WorkflowApprovalStatus.Pending ||
            !(approval.ApproverId == UserId || approval.ApproverRole != null && roles.Contains(approval.ApproverRole)))
            return Forbid();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        WorkflowSignaturePolicyDto? policy = null;
        try
        {
            policy = JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(
                approval.StepInstance.WorkflowStep.Configuration ?? "{}", options)?.ApprovalConfig?.SignaturePolicy;
        }
        catch (JsonException) { }
        var errors = WorkflowSignatureValidator.Validate(policy, _currentUser.Roles ?? [],
            new { signature = submission }, DateTime.UtcNow);
        if (errors.Count > 0) return BadRequest(new { success = false, errors });

        var certificate = WorkflowSignatureValidator.InspectCertificate(submission.CertificateBase64, DateTime.UtcNow);
        var signedPayload = $"{approval.Id:N}|{UserId:N}|{submission.SignedAt:O}|{submission.Attestation}";
        var signature = await _db.WorkflowSignatureEvidence.FirstOrDefaultAsync(item =>
            item.ApprovalId == approval.Id && !item.IsDeleted, cancellationToken);
        signature ??= new WorkflowSignatureEvidence { TenantId = TenantId, ApprovalId = approval.Id };
        signature.SignerUserId = UserId;
        signature.Method = submission.Method;
        signature.Attestation = submission.Attestation.Trim();
        signature.CertificateThumbprint = certificate?.Thumbprint;
        signature.CertificateSubject = certificate?.Subject;
        signature.CertificateNotBefore = certificate?.NotBefore;
        signature.CertificateNotAfter = certificate?.NotAfter;
        signature.CertificateChainValid = certificate?.ChainValid ?? false;
        signature.SubmissionJson = JsonSerializer.Serialize(submission);
        signature.SignedPayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signedPayload)));
        signature.SignedAt = submission.SignedAt;
        signature.IsCommitted = false;
        signature.CommittedAt = null;
        signature.IpAddress = _currentUser.IpAddress;
        signature.UserAgent = _currentUser.UserAgent;
        signature.CreatedById = UserId;
        signature.CreatedBy = _currentUser.UserName;
        if (_db.Entry(signature).State == EntityState.Detached) _db.WorkflowSignatureEvidence.Add(signature);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = new { signature.Id, signature.SignedPayloadHash } });
    }

    private Task<WorkflowEvidenceDocument?> Find(Guid id, CancellationToken cancellationToken) =>
        _db.WorkflowEvidenceDocuments.FirstOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted,
            cancellationToken);
    private Guid TenantId => _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : throw new UnauthorizedAccessException("User context is required.");
}

public sealed class SaveWorkflowEvidencePolicyRequest
{
    public List<string> AllowedExtensions { get; set; } = [];
    public long MaximumFileSizeBytes { get; set; }
    public int RetentionDays { get; set; } = 2555;
    public bool RequireMalwareScan { get; set; } = true;
}
public sealed class VerifyWorkflowEvidenceRequest { public bool Accepted { get; set; } public string? Notes { get; set; } }
public sealed class WorkflowLegalHoldRequest { public bool Enabled { get; set; } public string? Reason { get; set; } }
