using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyAdvanceRecoveryService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    IVendorPaymentService vendorPaymentService) : IQuantitySurveyAdvanceRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName) ? UserId.ToString() : currentUser.UserName.Trim();
    private string ActorRoles => string.Join(",", currentUser.Roles.Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));

    public async Task<QuantitySurveyAdvanceRecoveryWorkspaceDto> GetWorkspaceAsync(Guid projectId, CancellationToken token = default)
    {
        await RequireProjectAsync(projectId);
        var agreements = await Query().Where(value => value.ProjectId == projectId)
            .OrderByDescending(value => value.PreparedAt).ToListAsync(token);
        var mapped = new List<QuantitySurveyAdvanceRecoveryAgreementDto>();
        foreach (var agreement in agreements) mapped.Add(await MapAsync(agreement, token));

        var usedPayments = agreements.Where(value => value.Status != QuantitySurveyAdvanceRecoveryStatuses.Rejected)
            .Select(value => value.VendorPaymentId).ToHashSet();
        var contractIds = await LinkedContractIdsAsync(projectId, token);
        var contracts = await db.Set<Contract>().AsNoTracking().Include(value => value.BusinessPartner)
            .Where(value => value.TenantId == TenantId && contractIds.Contains(value.Id) && !value.IsDeleted &&
                            value.ContractType == "Works" && value.Status == "Active")
            .ToListAsync(token);
        var eligible = new List<QuantitySurveyAdvancePaymentLookupDto>();
        foreach (var contract in contracts)
        {
            var payments = await vendorPaymentService.GetPostedSupplierAdvancesAsync(contract.BusinessPartnerId, token);
            foreach (var payment in payments.Where(value =>
                         !usedPayments.Contains(value.Id) &&
                         string.Equals(contract.Currency, value.CurrencyCode, StringComparison.OrdinalIgnoreCase)))
            {
                eligible.Add(new(payment.Id, contract.Id, contract.ContractNumber, contract.BusinessPartner.PartnerName,
                    payment.PaymentNumber, payment.PaymentDate, payment.CurrencyCode, payment.TotalAmount,
                    payment.AllocatedAmount, Math.Max(0m, payment.TotalAmount - payment.AllocatedAmount)));
            }
        }
        return new() { Agreements = mapped, EligibleAdvances = eligible };
    }

    public async Task<QuantitySurveyAdvanceRecoveryAgreementDto> CreateAsync(Guid projectId,
        CreateQuantitySurveyAdvanceRecoveryRequest request, string correlationId, CancellationToken token = default)
    {
        if (request.ClientRequestId == Guid.Empty || request.ContractId == Guid.Empty || request.VendorPaymentId == Guid.Empty)
            throw Validation("Select a controlled Works contract and posted Finance supplier advance.");
        if (request.RecoveryPercentage <= 0m || request.RecoveryPercentage > 100m)
            throw Validation("Recovery percentage must be greater than zero and no more than 100 percent.");
        var reason = RequiredReason(request.Reason);
        await RequireProjectAsync(projectId);
        var requestHash = Hash(new { projectId, request.ContractId, request.VendorPaymentId,
            RecoveryPercentage = Round(request.RecoveryPercentage), reason });
        Guid id = Guid.Empty;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var retry = await db.QuantitySurveyAdvanceRecoveryAgreements.FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.ClientRequestId == request.ClientRequestId && !value.IsDeleted, token);
            if (retry is not null)
            {
                if (retry.ProjectId != projectId || !FixedEquals(retry.RequestHash, requestHash)) throw RetryConflict();
                id = retry.Id; await transaction.CommitAsync(token); return;
            }
            var linked = await LinkedContractIdsAsync(projectId, token);
            var contract = await db.Set<Contract>().Include(value => value.BusinessPartner).FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == request.ContractId && linked.Contains(value.Id) && !value.IsDeleted &&
                value.ContractType == "Works" && value.Status == "Active", token)
                ?? throw Conflict("The selected contract is not an active Works contract linked to this project.");
            var payment = (await vendorPaymentService.GetPostedSupplierAdvancesAsync(contract.BusinessPartnerId, token))
                .FirstOrDefault(value => value.Id == request.VendorPaymentId)
                ?? throw Conflict("The selected Finance payment is not a posted supplier advance for the Works contractor.");
            if (!string.Equals(payment.CurrencyCode, contract.Currency, StringComparison.OrdinalIgnoreCase))
                throw Conflict("The supplier advance currency does not match the Works contract currency.");
            if (payment.TotalAmount <= 0m || payment.AllocatedAmount >= payment.TotalAmount)
                throw Conflict("The supplier advance has no recoverable balance.");
            if (await db.QuantitySurveyAdvanceRecoveryAgreements.AnyAsync(value => value.TenantId == TenantId &&
                value.VendorPaymentId == payment.Id && !value.IsDeleted && value.Status != QuantitySurveyAdvanceRecoveryStatuses.Rejected, token))
                throw Conflict("This Finance supplier advance is already governed by an active recovery agreement.");

            var policy = await ResolvePolicyAsync(token);
            var now = DateTime.UtcNow;
            var count = await db.QuantitySurveyAdvanceRecoveryAgreements.IgnoreQueryFilters()
                .CountAsync(value => value.TenantId == TenantId && value.PreparedAt.Year == now.Year, token);
            var entity = new QuantitySurveyAdvanceRecoveryAgreement
            {
                Id = Guid.NewGuid(), TenantId = TenantId, ProjectId = projectId, ContractId = contract.Id,
                VendorPaymentId = payment.Id, ClientRequestId = request.ClientRequestId, RequestHash = requestHash,
                RecoveryNumber = $"ADVREC-{now:yyyy}-{count + 1:00000}",
                ContractNumberSnapshot = contract.ContractNumber, ContractorNameSnapshot = contract.BusinessPartner.PartnerName,
                PaymentNumberSnapshot = payment.PaymentNumber, PaymentDateSnapshot = payment.PaymentDate,
                CurrencyCodeSnapshot = payment.CurrencyCode, OriginalAdvanceAmount = Round(payment.TotalAmount),
                RecoveryPercentage = decimal.Round(request.RecoveryPercentage, 4, MidpointRounding.AwayFromZero),
                ConfigurationProfileId = policy.ProfileId, ValuationDecisionId = policy.DecisionId, PolicyHash = policy.Hash,
                PreparedById = UserId, PreparedAt = now, CorrelationId = Correlation(correlationId),
                CreatedAt = now, CreatedBy = UserName, CreatedById = UserId
            };
            db.QuantitySurveyAdvanceRecoveryAgreements.Add(entity);
            AddRevision(entity, QuantitySurveyAuditEventMap.CreateAdvanceRecoveryAgreement, reason, null, Snapshot(entity), correlationId);
            AddAudit(entity, QuantitySurveyAuditEventMap.CreateAdvanceRecoveryAgreement, null, Snapshot(entity), correlationId);
            await SaveAsync(token); id = entity.Id;
            await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return await MapAsync(await RequiredAsync(id, false, token), token);
    }

    public Task<QuantitySurveyAdvanceRecoveryAgreementDto> SubmitAsync(Guid id,
        QuantitySurveyAdvanceRecoveryActionRequest request, string correlationId, CancellationToken token = default) =>
        MutateAsync(id, request, correlationId, QuantitySurveyAuditEventMap.SubmitAdvanceRecoveryAgreement, token, entity =>
        {
            if (entity.Status != QuantitySurveyAdvanceRecoveryStatuses.Draft)
                throw Conflict("Only a Draft recovery agreement can be submitted.");
            entity.Status = QuantitySurveyAdvanceRecoveryStatuses.PendingApproval;
            entity.ApprovalStatus = "Pending"; entity.SubmittedById = UserId; entity.SubmittedAt = DateTime.UtcNow;
        });

    public Task<QuantitySurveyAdvanceRecoveryAgreementDto> ApproveAsync(Guid id,
        QuantitySurveyAdvanceRecoveryActionRequest request, string correlationId, CancellationToken token = default) =>
        MutateAsync(id, request, correlationId, QuantitySurveyAuditEventMap.ApproveAdvanceRecoveryAgreement, token, entity =>
        {
            if (entity.Status != QuantitySurveyAdvanceRecoveryStatuses.PendingApproval)
                throw Conflict("Only a submitted recovery agreement can be approved.");
            if (entity.PreparedById == UserId || entity.SubmittedById == UserId)
                throw Conflict("The preparer or submitter cannot approve the advance recovery agreement.");
            entity.Status = QuantitySurveyAdvanceRecoveryStatuses.Approved;
            entity.ApprovalStatus = "Approved"; entity.ApprovedById = UserId; entity.ApprovedAt = DateTime.UtcNow;
            entity.RejectionReason = null;
        });

    public Task<QuantitySurveyAdvanceRecoveryAgreementDto> RejectAsync(Guid id,
        QuantitySurveyAdvanceRecoveryActionRequest request, string correlationId, CancellationToken token = default) =>
        MutateAsync(id, request, correlationId, QuantitySurveyAuditEventMap.RejectAdvanceRecoveryAgreement, token, entity =>
        {
            if (entity.Status != QuantitySurveyAdvanceRecoveryStatuses.PendingApproval)
                throw Conflict("Only a submitted recovery agreement can be rejected.");
            if (entity.PreparedById == UserId || entity.SubmittedById == UserId)
                throw Conflict("The preparer or submitter cannot reject the advance recovery agreement.");
            entity.Status = QuantitySurveyAdvanceRecoveryStatuses.Rejected;
            entity.ApprovalStatus = "Rejected"; entity.ApprovedById = UserId; entity.ApprovedAt = DateTime.UtcNow;
            entity.RejectionReason = RequiredReason(request.Reason);
        });

    public async Task<IReadOnlyList<QuantitySurveyAdvanceRecoveryRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default)
    {
        var entity = await RequiredAsync(id, false, token); await RequireProjectAsync(entity.ProjectId);
        return await db.QuantitySurveyAdvanceRecoveryRevisions.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.AgreementId == id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new QuantitySurveyAdvanceRecoveryRevisionDto(value.Id, value.Action, value.ActorUserId,
                value.ActorName, value.ActorRoles, value.CorrelationId, value.Reason, value.BeforeJson,
                value.AfterJson, value.CreatedAt)).ToListAsync(token);
    }

    private async Task<QuantitySurveyAdvanceRecoveryAgreementDto> MutateAsync(Guid id,
        QuantitySurveyAdvanceRecoveryActionRequest request, string correlationId, string action, CancellationToken token,
        Action<QuantitySurveyAdvanceRecoveryAgreement> mutate)
    {
        if (request.ClientRequestId == Guid.Empty) throw Validation("A client request identifier is required.");
        var reason = RequiredReason(request.Reason);
        var hash = Hash(new { action, reason });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var entity = await RequiredAsync(id, true, token); await RequireProjectAsync(entity.ProjectId);
            if (entity.LastMutationClientRequestId == request.ClientRequestId && FixedEquals(entity.LastMutationRequestHash, hash))
            { await transaction.CommitAsync(token); return; }
            ApplyRowVersion(entity, request.RowVersion);
            var contract = await db.Set<Contract>().AsNoTracking().FirstOrDefaultAsync(value =>
                value.TenantId == TenantId && value.Id == entity.ContractId && !value.IsDeleted, token)
                ?? throw Conflict("The linked Works contract is no longer available.");
            var payment = (await vendorPaymentService.GetPostedSupplierAdvancesAsync(contract.BusinessPartnerId, token))
                .FirstOrDefault(value => value.Id == entity.VendorPaymentId)
                ?? throw Conflict("The linked Finance supplier advance is no longer eligible for this contractor.");
            if (!string.Equals(payment.CurrencyCode, entity.CurrencyCodeSnapshot, StringComparison.OrdinalIgnoreCase) ||
                payment.TotalAmount != entity.OriginalAdvanceAmount)
                throw Conflict("The Finance supplier-advance source no longer matches the frozen recovery terms.");
            var policy = await ResolvePolicyAsync(token);
            if (entity.ConfigurationProfileId != policy.ProfileId || entity.ValuationDecisionId != policy.DecisionId ||
                !FixedEquals(entity.PolicyHash, policy.Hash))
                throw Conflict("The effective QS advance-recovery policy changed. Prepare a new agreement.");
            var before = Snapshot(entity); mutate(entity);
            entity.LastMutationClientRequestId = request.ClientRequestId; entity.LastMutationRequestHash = hash;
            entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = UserName; entity.LastModifiedById = UserId;
            AddRevision(entity, action, reason, before, Snapshot(entity), correlationId);
            AddAudit(entity, action, before, Snapshot(entity), correlationId);
            await SaveAsync(token); await transaction.CommitAsync(token);
        });
        db.ChangeTracker.Clear();
        return await MapAsync(await RequiredAsync(id, false, token), token);
    }

    private async Task<QuantitySurveyAdvanceRecoveryAgreementDto> MapAsync(
        QuantitySurveyAdvanceRecoveryAgreement entity, CancellationToken token)
    {
        var payment = await db.Set<VendorPayment>().AsNoTracking().FirstAsync(value => value.TenantId == TenantId && value.Id == entity.VendorPaymentId, token);
        var certificates = await db.Set<ProjectPaymentCertificate>().AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.QuantitySurveyAdvanceRecoveryAgreementId == entity.Id && !value.IsDeleted)
            .OrderBy(value => value.IssueDate).ThenBy(value => value.CreatedAt).ToListAsync(token);
        var invoiceIds = certificates.Where(value => value.VendorInvoiceId.HasValue).Select(value => value.VendorInvoiceId!.Value).ToHashSet();
        var allocations = invoiceIds.Count == 0 ? [] : await db.Set<VendorPaymentAllocation>().AsNoTracking()
            .Where(value => value.TenantId == TenantId && value.VendorPaymentId == entity.VendorPaymentId &&
                            invoiceIds.Contains(value.VendorInvoiceId) && !value.IsDeleted).ToListAsync(token);
        decimal running = entity.OriginalAdvanceAmount;
        var ledger = new List<QuantitySurveyAdvanceRecoveryLedgerEntryDto>();
        foreach (var certificate in certificates)
        {
            var approved = certificate.Status is ProjectPaymentCertificateStatuses.Approved or ProjectPaymentCertificateStatuses.Paid;
            if (approved) running = Math.Max(0m, running - certificate.AdvanceRecoveryAmount);
            var financeApplied = certificate.VendorInvoiceId.HasValue
                ? allocations.Where(value => value.VendorInvoiceId == certificate.VendorInvoiceId)
                    .Sum(value => value.PaymentCurrencyAmount != 0m ? value.PaymentCurrencyAmount : value.AllocatedAmount)
                : 0m;
            ledger.Add(new(certificate.Id, certificate.CertificateNumber ?? certificate.Id.ToString(), certificate.IssueDate,
                certificate.Status, certificate.AdvanceRecoveryAmount, certificate.VendorInvoiceId, Round(financeApplied), Round(running)));
        }
        var approvedRecovery = certificates.Where(value => value.Status is ProjectPaymentCertificateStatuses.Approved or ProjectPaymentCertificateStatuses.Paid)
            .Sum(value => value.AdvanceRecoveryAmount);
        var pendingRecovery = certificates.Where(value => value.Status is ProjectPaymentCertificateStatuses.Draft or ProjectPaymentCertificateStatuses.Issued)
            .Sum(value => value.AdvanceRecoveryAmount);
        var qsFinanceApplied = ledger.Sum(value => value.FinanceAppliedAmount);
        return new()
        {
            Id = entity.Id, ProjectId = entity.ProjectId, ContractId = entity.ContractId, VendorPaymentId = entity.VendorPaymentId,
            RecoveryNumber = entity.RecoveryNumber, Status = entity.Status, ApprovalStatus = entity.ApprovalStatus,
            ContractNumber = entity.ContractNumberSnapshot, ContractorName = entity.ContractorNameSnapshot,
            PaymentNumber = entity.PaymentNumberSnapshot, PaymentDate = entity.PaymentDateSnapshot,
            Currency = entity.CurrencyCodeSnapshot, OriginalAdvanceAmount = entity.OriginalAdvanceAmount,
            RecoveryPercentage = entity.RecoveryPercentage, ApprovedRecoveryAmount = Round(approvedRecovery),
            PendingRecoveryAmount = Round(pendingRecovery), RemainingRecoveryBalance = QuantitySurveyAdvanceRecoveryRules.Remaining(entity.OriginalAdvanceAmount, approvedRecovery),
            FinanceAllocatedAmount = Round(payment.AllocatedAmount), FinanceAvailableAmount = Round(Math.Max(0m, payment.TotalAmount - payment.AllocatedAmount)),
            FinanceAppliedToQsCertificates = Round(qsFinanceApplied), ReconciliationDifference = Round(approvedRecovery - qsFinanceApplied),
            PreparedById = entity.PreparedById, ApprovedById = entity.ApprovedById, PreparedAt = entity.PreparedAt,
            ApprovedAt = entity.ApprovedAt, RejectionReason = entity.RejectionReason,
            RowVersion = Convert.ToBase64String(entity.RowVersion), Ledger = ledger
        };
    }

    private async Task<HashSet<Guid>> LinkedContractIdsAsync(Guid projectId, CancellationToken token)
    {
        var fromValuations = await db.Set<ProjectInterimValuation>().AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.ContractId.HasValue && !value.IsDeleted)
            .Select(value => value.ContractId!.Value).ToListAsync(token);
        var fromCertificates = await db.Set<ProjectPaymentCertificate>().AsNoTracking().Where(value => value.TenantId == TenantId &&
                value.ProjectId == projectId && value.ContractId.HasValue && !value.IsDeleted)
            .Select(value => value.ContractId!.Value).ToListAsync(token);
        return fromValuations.Concat(fromCertificates).ToHashSet();
    }

    private async Task<PolicySnapshot> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value => value.TenantId == TenantId &&
                !value.IsDeleted && value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
                value.EffectiveFrom <= now && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderByDescending(value => value.EffectiveFrom).ThenByDescending(value => value.Version).FirstOrDefaultAsync(token)
            ?? throw Conflict("No Published QS configuration profile is effective for advance recovery.");
        var decision = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().FirstOrDefaultAsync(value =>
            value.TenantId == TenantId && value.ProfileId == profile.Id && value.DecisionKey == "QS-DEC-008" && !value.IsDeleted &&
            value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
            value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
            value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified &&
            (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= now) && (!value.EffectiveTo.HasValue || value.EffectiveTo >= now), token)
            ?? throw Conflict("The effective QS-DEC-008 valuation and certificate decision is not approved and evidence-verified.");
        QsValuationCertificateValue value;
        try
        {
            value = JsonSerializer.Deserialize<QsValuationCertificateValue>(decision.ValueJson, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Conflict("QS-DEC-008 contains invalid valuation and certificate policy data.");
        }
        if (!value.ApplyAdvanceRecovery) throw Conflict("Advance recovery is disabled by the effective QS-DEC-008 policy.");
        return new(profile.Id, decision.Id, Hash(new { Profile = profile.Id, Decision = decision.Id, value.ApplyAdvanceRecovery }));
    }

    private IQueryable<QuantitySurveyAdvanceRecoveryAgreement> Query(bool tracked = false)
    {
        var query = db.QuantitySurveyAdvanceRecoveryAgreements.Where(value => value.TenantId == TenantId && !value.IsDeleted);
        return tracked ? query : query.AsNoTracking();
    }
    private async Task<QuantitySurveyAdvanceRecoveryAgreement> RequiredAsync(Guid id, bool tracked, CancellationToken token) =>
        await Query(tracked).FirstOrDefaultAsync(value => value.Id == id, token)
        ?? throw new QuantitySurveyAdvanceRecoveryNotFoundException("The governed advance recovery agreement was not found.");
    private async Task RequireProjectAsync(Guid projectId)
    {
        if (!await projectService.HasProjectAccessAsync(projectId))
            throw new UnauthorizedAccessException("You are not permitted to access this project.");
    }
    private void AddRevision(QuantitySurveyAdvanceRecoveryAgreement entity, string action, string reason,
        object? before, object after, string correlationId) => db.QuantitySurveyAdvanceRecoveryRevisions.Add(new()
    {
        TenantId = TenantId, AgreementId = entity.Id, Action = action, ActorUserId = UserId, ActorName = UserName,
        ActorRoles = ActorRoles, CorrelationId = Correlation(correlationId), Reason = reason,
        BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
        AfterJson = JsonSerializer.Serialize(after, JsonOptions), CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
    });
    private void AddAudit(QuantitySurveyAdvanceRecoveryAgreement entity, string action, object? before, object after, string correlationId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId, UserId = UserId, Username = UserName, Action = action,
            Resource = "QuantitySurveyAdvanceRecoveryAgreement", ResourceId = entity.Id.ToString(),
            OldValues = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            NewValues = JsonSerializer.Serialize(new { correlationId = Correlation(correlationId), value = after }, JsonOptions),
            IpAddress = "api", UserAgent = "QuantitySurvey", Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = UserName, CreatedById = UserId
        });
    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The advance recovery agreement changed. Refresh and retry."); }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("IX_QsAdvanceRecovery", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("The posted supplier advance or client request is already linked to another recovery agreement."); }
    }
    private static object Snapshot(QuantitySurveyAdvanceRecoveryAgreement value) => new
    {
        value.Id, value.ProjectId, value.ContractId, value.VendorPaymentId, value.RecoveryNumber,
        value.Status, value.ApprovalStatus, value.OriginalAdvanceAmount, value.RecoveryPercentage,
        value.ConfigurationProfileId, value.ValuationDecisionId, value.PreparedById, value.SubmittedById,
        value.ApprovedById, value.PreparedAt, value.SubmittedAt, value.ApprovedAt, value.RejectionReason
    };
    private static void ApplyRowVersion(QuantitySurveyAdvanceRecoveryAgreement value, string encoded)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw Validation("The advance recovery row version is invalid. Refresh and retry."); }
        if (expected.Length != value.RowVersion.Length || !CryptographicOperations.FixedTimeEquals(expected, value.RowVersion))
            throw Conflict("The advance recovery agreement changed. Refresh and retry.");
    }
    private static string RequiredReason(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim().Length is < 5 or > 2000
        ? throw Validation("Reason must contain 5 to 2000 characters.") : value.Trim();
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(100, value.Trim().Length)];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static bool FixedEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static QuantitySurveyAdvanceRecoveryValidationException Validation(string value) => new(value);
    private static QuantitySurveyAdvanceRecoveryConflictException Conflict(string value) => new(value);
    private static QuantitySurveyAdvanceRecoveryConflictException RetryConflict() => Conflict("This client request identifier is already bound to different advance recovery inputs.");
    private sealed record PolicySnapshot(Guid ProfileId, Guid DecisionId, string Hash);
}
