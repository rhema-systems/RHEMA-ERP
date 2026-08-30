using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyContractCommercialTermsService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService) : IQuantitySurveyContractCommercialTermsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private Guid TenantId => currentUser.TenantId is { } value && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("A valid tenant context is required.");
    private Guid UserId => Guid.TryParse(currentUser.UserId, out var value) && value != Guid.Empty
        ? value : throw new UnauthorizedAccessException("An authenticated application user is required.");
    private string UserName => string.IsNullOrWhiteSpace(currentUser.UserName)
        ? UserId.ToString() : currentUser.UserName.Trim();

    public async Task<QuantitySurveyContractCommercialTermsWorkspaceDto> GetWorkspaceAsync(
        Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await RequiredContractAsync(contractId, tracked: false, cancellationToken);
        var projectId = await RequireProjectIdAsync(contract, cancellationToken);
        await RequireProjectAccessAsync(projectId);
        var policy = await ResolvePolicyAsync(cancellationToken);
        var paymentTerms = await PaymentTermQuery().OrderBy(value => value.DisplayOrder).ThenBy(value => value.Name)
            .Select(value => new QuantitySurveyContractPaymentTermLookupDto(
                value.Id, value.Code, value.Name, value.DueDays, value.ApplicableTo))
            .ToListAsync(cancellationToken);
        var documents = await GovernedDocumentQuery(contract.Id)
            .OrderBy(value => value.DocumentType).ThenBy(value => value.FileName)
            .Select(value => new QuantitySurveyContractDocumentLookupDto(
                value.Id, value.DocumentType, value.FileName,
                value.CentralDocumentRecordId!.Value, value.CentralDocumentVersionId!.Value))
            .ToListAsync(cancellationToken);
        var blockers = await ReadinessBlockersAsync(contract, policy, cancellationToken);
        return new()
        {
            Terms = Map(contract, projectId),
            PaymentTerms = paymentTerms,
            ContractDocuments = documents,
            IsEditable = string.Equals(contract.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            MaximumRetentionPercentage = policy.Retention.MaximumRetentionPercent,
            MaximumDefectsLiabilityDays = policy.Retention.DefectsLiabilityDays,
            SectionalTakeoverReleasePercentage = policy.Retention.SectionalTakeoverReleasePercent,
            AllowRetentionBond = policy.Retention.AllowRetentionBond,
            ControlProvisionalSums = policy.Controls.ControlProvisionalSums,
            ControlContingencies = policy.Controls.ControlContingencies,
            ControlDefectsLiability = policy.Controls.ControlDefectsLiability,
            ControlSectionalTakeover = policy.Controls.ControlSectionalTakeover,
            ControlSubcontracts = policy.Controls.ControlSubcontracts,
            ControlClaimClauses = policy.Controls.ControlClaimClauses,
            RequireCommercialTermsDocument = policy.Controls.RequireCommercialTermsDocument,
            ReadinessBlockers = blockers
        };
    }

    public async Task<QuantitySurveyContractCommercialTermsWorkspaceDto> ConfigureAsync(
        Guid contractId, ConfigureQuantitySurveyContractCommercialTermsRequest request,
        string correlationId, CancellationToken cancellationToken = default)
    {
        if (request.ClientRequestId == Guid.Empty)
            throw Validation("A client request identifier is required.");
        var requestHash = Hash(new
        {
            contractId,
            request.PaymentTermId,
            request.ProvisionalSumAmount,
            request.ContingencyAmount,
            request.RetentionPercentage,
            request.DefectsLiabilityDays,
            RetentionClause = Normalize(request.RetentionClause),
            request.AllowSectionalTakeover,
            SectionalTakeoverClause = Normalize(request.SectionalTakeoverClause),
            request.AllowSubcontracting,
            request.SubcontractPaymentTermId,
            SubcontractTerms = Normalize(request.SubcontractTerms),
            request.ClaimNoticePeriodDays,
            ClaimClause = Normalize(request.ClaimClause),
            request.CommercialTermsContractDocumentId
        });
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var contract = await RequiredContractAsync(contractId, tracked: true, cancellationToken);
            var projectId = await RequireProjectIdAsync(contract, cancellationToken);
            await RequireProjectAccessAsync(projectId);
            if (!string.Equals(contract.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                throw Conflict("Commercial terms can be changed only while the canonical Procurement contract is Draft.");
            if (contract.CommercialTermsClientRequestId == request.ClientRequestId)
            {
                if (!FixedEquals(contract.CommercialTermsRequestHash, requestHash))
                    throw Conflict("This client request identifier is already bound to different commercial terms.");
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            ApplyRowVersion(contract, request.RowVersion);
            var policy = await ResolvePolicyAsync(cancellationToken);
            var paymentTerm = await RequirePaymentTermAsync(request.PaymentTermId, cancellationToken);
            PaymentTerm? subcontractPaymentTerm = null;
            if (request.SubcontractPaymentTermId.HasValue)
                subcontractPaymentTerm = await RequirePaymentTermAsync(
                    request.SubcontractPaymentTermId.Value, cancellationToken);
            ContractDocument? document = null;
            if (request.CommercialTermsContractDocumentId.HasValue)
                document = await GovernedDocumentQuery(contract.Id).SingleOrDefaultAsync(value =>
                    value.Id == request.CommercialTermsContractDocumentId.Value, cancellationToken)
                    ?? throw Validation("Select a current, malware-clean central-DMS document owned by this contract.");
            var issues = Validate(contract, request, policy, paymentTerm, subcontractPaymentTerm, document);
            if (issues.Count > 0)
                throw Validation(string.Join(" ", issues));
            var before = Snapshot(contract);
            contract.PaymentTermId = paymentTerm.Id;
            contract.PaymentTerms = $"{paymentTerm.Name} ({paymentTerm.Code})";
            contract.ProvisionalSumAmount = request.ProvisionalSumAmount;
            contract.ContingencyAmount = request.ContingencyAmount;
            contract.RetentionPercentage = request.RetentionPercentage;
            contract.DefectsLiabilityDays = request.DefectsLiabilityDays;
            contract.WarrantyPeriodDays = request.DefectsLiabilityDays;
            contract.RetentionClause = Normalize(request.RetentionClause);
            contract.AllowSectionalTakeover = request.AllowSectionalTakeover;
            contract.SectionalTakeoverClause = request.AllowSectionalTakeover
                ? Normalize(request.SectionalTakeoverClause) : null;
            contract.AllowSubcontracting = request.AllowSubcontracting;
            contract.SubcontractPaymentTermId = request.AllowSubcontracting
                ? subcontractPaymentTerm?.Id : null;
            contract.SubcontractTerms = request.AllowSubcontracting
                ? Normalize(request.SubcontractTerms) : null;
            contract.ClaimNoticePeriodDays = policy.Controls.ControlClaimClauses
                ? request.ClaimNoticePeriodDays : null;
            contract.ClaimClause = policy.Controls.ControlClaimClauses
                ? Normalize(request.ClaimClause) : null;
            contract.CommercialTermsContractDocumentId = document?.Id;
            contract.CommercialTermsConfigurationProfileId = policy.Profile.Id;
            contract.ContractControlsDecisionId = policy.ControlsDecision.Id;
            contract.RetentionDecisionId = policy.RetentionDecision.Id;
            contract.CommercialTermsClientRequestId = request.ClientRequestId;
            contract.CommercialTermsRequestHash = requestHash;
            contract.CommercialTermsPolicyHash = policy.Hash;
            contract.CommercialTermsConfiguredAt = DateTime.UtcNow;
            contract.CommercialTermsConfiguredById = UserId;
            contract.UpdatedAt = DateTime.UtcNow;
            contract.UpdatedBy = UserName;
            contract.LastModifiedById = UserId;
            db.AuditLogs.Add(new AuditLog
            {
                TenantId = TenantId,
                UserId = UserId,
                Username = UserName,
                Action = QuantitySurveyAuditEventMap.ConfigureContractCommercialTerms,
                Resource = "Contract",
                ResourceId = contract.Id.ToString(),
                OldValues = JsonSerializer.Serialize(before, JsonOptions),
                NewValues = JsonSerializer.Serialize(new
                {
                    correlationId = Correlation(correlationId),
                    clientRequestId = request.ClientRequestId,
                    value = Snapshot(contract)
                }, JsonOptions),
                IpAddress = "api",
                UserAgent = "QuantitySurvey",
                Timestamp = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = UserId
            });
            try
            {
                await SetMutationContextAsync(contract, cancellationToken);
                await SaveAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            finally
            {
                await ClearMutationContextAsync(cancellationToken);
            }
        });
        db.ChangeTracker.Clear();
        return await GetWorkspaceAsync(contractId, cancellationToken);
    }

    private IQueryable<PaymentTerm> PaymentTermQuery() => db.PaymentTerms.AsNoTracking().Where(value =>
        value.TenantId == TenantId && !value.IsDeleted && value.IsActive &&
        (value.ApplicableTo == "All" || value.ApplicableTo == "Supplier" || value.ApplicableTo == "Contractor"));

    private IQueryable<ContractDocument> GovernedDocumentQuery(Guid contractId) =>
        db.ContractDocuments.AsNoTracking()
            .Include(value => value.FileUploadRecord)
            .Include(value => value.CentralDocumentRecord)
            .Include(value => value.CentralDocumentVersion)
            .Where(value => value.TenantId == TenantId && value.ContractId == contractId && !value.IsDeleted &&
                            value.FileUploadRecordId.HasValue && value.CentralDocumentRecordId.HasValue &&
                            value.CentralDocumentVersionId.HasValue && value.FileUploadRecord != null &&
                            value.FileUploadRecord.TenantId == TenantId && !value.FileUploadRecord.IsDeleted &&
                            value.FileUploadRecord.VirusScanStatus == FileVirusScanStatus.Clean &&
                            value.CentralDocumentRecord != null && value.CentralDocumentRecord.TenantId == TenantId &&
                            !value.CentralDocumentRecord.IsDeleted && value.CentralDocumentRecord.LifecycleStatus == "Active" &&
                            value.CentralDocumentVersion != null && value.CentralDocumentVersion.TenantId == TenantId &&
                            !value.CentralDocumentVersion.IsDeleted &&
                            value.CentralDocumentVersion.DocumentRecordId == value.CentralDocumentRecordId &&
                            value.CentralDocumentVersion.FileUploadRecordId == value.FileUploadRecordId &&
                            value.CentralDocumentVersion.Status == "Published" &&
                            value.CentralDocumentRecord.CurrentVersion == value.CentralDocumentVersion.VersionNumber);

    private async Task<Contract> RequiredContractAsync(Guid contractId, bool tracked, CancellationToken token)
    {
        var query = db.Contracts.Include(value => value.Tender).ThenInclude(value => value.SourcePurchaseRequisition)
            .Where(value => value.TenantId == TenantId && value.Id == contractId && !value.IsDeleted &&
                            value.ContractType == "Works");
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(token)
               ?? throw new QuantitySurveyContractCommercialTermsNotFoundException(
                   "The tenant-owned Works contract was not found.");
    }

    private async Task<Guid> RequireProjectIdAsync(Contract contract, CancellationToken token)
    {
        var ids = new HashSet<Guid>();
        if (contract.Tender.SourcePurchaseRequisition?.ProjectId is { } requisitionProjectId &&
            requisitionProjectId != Guid.Empty)
            ids.Add(requisitionProjectId);
        var directIds = await db.Projects.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.ContractId == contract.Id && !value.IsDeleted)
            .Select(value => value.Id).ToListAsync(token);
        ids.UnionWith(directIds);
        var packageIds = await db.ProjectPackages.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.ContractId == contract.Id && !value.IsDeleted)
            .Select(value => value.ProjectId).Distinct().ToListAsync(token);
        ids.UnionWith(packageIds);
        if (ids.Count != 1)
            throw Conflict(ids.Count == 0
                ? "The Works contract is not linked to a governed project."
                : "The Works contract resolves to more than one project; correct its source lineage before configuring commercial terms.");
        return ids.Single();
    }

    private async Task RequireProjectAccessAsync(Guid projectId)
    {
        if (!await projectService.HasProjectAccessAsync(projectId))
            throw new UnauthorizedAccessException("The governed project is outside the current user scope.");
    }

    private async Task<PaymentTerm> RequirePaymentTermAsync(Guid id, CancellationToken token) =>
        await PaymentTermQuery().SingleOrDefaultAsync(value => value.Id == id, token)
        ?? throw Validation("Select an active supplier or contractor payment term from the current tenant.");

    private async Task<Policy> ResolvePolicyAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var profile = await db.QuantitySurveyConfigurationProfiles.AsNoTracking().Where(value =>
                value.TenantId == TenantId && !value.IsDeleted &&
                value.LifecycleStatus == QuantitySurveyConfigurationProfileStatus.Published &&
                value.PublishedAt != null && value.EffectiveFrom <= now &&
                (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .OrderByDescending(value => value.IsDefault).ThenByDescending(value => value.Version)
            .FirstOrDefaultAsync(token)
            ?? throw Conflict("No Published QS configuration is effective for this tenant and date.");
        var decisions = await db.QuantitySurveyConfigurationDecisions.AsNoTracking().Where(value =>
                value.TenantId == TenantId && value.ProfileId == profile.Id && !value.IsDeleted &&
                (value.DecisionKey == "QS-DEC-009" || value.DecisionKey == "QS-DEC-012") &&
                value.Status == QuantitySurveyConfigurationDecisionStatus.Approved &&
                value.ApprovalStatus == QuantitySurveyConfigurationApprovalStatus.Approved &&
                value.EvidenceStatus == QuantitySurveyConfigurationEvidenceStatus.Verified &&
                (!value.EffectiveFrom.HasValue || value.EffectiveFrom <= now) &&
                (!value.EffectiveTo.HasValue || value.EffectiveTo >= now))
            .ToListAsync(token);
        var retentionDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-009")
            ?? throw Conflict("The effective QS-DEC-009 retention decision is not approved and evidence-verified.");
        var controlsDecision = decisions.SingleOrDefault(value => value.DecisionKey == "QS-DEC-012")
            ?? throw Conflict("The effective QS-DEC-012 contract-controls decision is not approved and evidence-verified.");
        try
        {
            var retention = JsonSerializer.Deserialize<QsRetentionValue>(retentionDecision.ValueJson, JsonOptions)
                ?? throw new JsonException();
            var controls = JsonSerializer.Deserialize<QsContractControlsValue>(controlsDecision.ValueJson, JsonOptions)
                ?? throw new JsonException();
            return new(profile, retentionDecision, controlsDecision, retention, controls, Hash(new
            {
                profile.Id,
                RetentionDecisionId = retentionDecision.Id,
                RetentionValueJson = retentionDecision.ValueJson,
                ControlsDecisionId = controlsDecision.Id,
                ControlsValueJson = controlsDecision.ValueJson
            }));
        }
        catch (JsonException exception)
        {
            throw Validation($"The effective QS commercial-terms policy is invalid: {exception.Message}");
        }
    }

    private static IReadOnlyList<string> Validate(Contract contract,
        ConfigureQuantitySurveyContractCommercialTermsRequest request, Policy policy,
        PaymentTerm paymentTerm, PaymentTerm? subcontractPaymentTerm, ContractDocument? document) =>
        QuantitySurveyContractCommercialTermsRules.Validate(new(
            contract.ContractValue, request.ProvisionalSumAmount, request.ContingencyAmount,
            request.RetentionPercentage, request.DefectsLiabilityDays, request.AllowSectionalTakeover,
            request.AllowSubcontracting, subcontractPaymentTerm is not null, request.ClaimNoticePeriodDays,
            paymentTerm.Id != Guid.Empty, document is not null, request.RetentionClause,
            request.SectionalTakeoverClause, request.SubcontractTerms, request.ClaimClause,
            policy.Retention.MaximumRetentionPercent, policy.Retention.DefectsLiabilityDays,
            policy.Retention.SectionalTakeoverReleasePercent, policy.Controls.ControlProvisionalSums,
            policy.Controls.ControlContingencies, policy.Controls.ControlDefectsLiability,
            policy.Controls.ControlSectionalTakeover, policy.Controls.ControlSubcontracts,
            policy.Controls.ControlClaimClauses, policy.Controls.RequireCommercialTermsDocument));

    private async Task<IReadOnlyList<string>> ReadinessBlockersAsync(
        Contract contract, Policy policy, CancellationToken token)
    {
        var paymentTerm = contract.PaymentTermId.HasValue &&
                          await PaymentTermQuery().AnyAsync(value => value.Id == contract.PaymentTermId.Value, token);
        var subcontractPaymentTerm = contract.SubcontractPaymentTermId.HasValue &&
                                     await PaymentTermQuery().AnyAsync(value => value.Id == contract.SubcontractPaymentTermId.Value, token);
        var document = contract.CommercialTermsContractDocumentId.HasValue &&
                       await GovernedDocumentQuery(contract.Id).AnyAsync(value =>
                           value.Id == contract.CommercialTermsContractDocumentId.Value, token);
        var issues = QuantitySurveyContractCommercialTermsRules.Validate(new(
            contract.ContractValue, contract.ProvisionalSumAmount, contract.ContingencyAmount,
            contract.RetentionPercentage, contract.DefectsLiabilityDays, contract.AllowSectionalTakeover,
            contract.AllowSubcontracting, subcontractPaymentTerm, contract.ClaimNoticePeriodDays,
            paymentTerm, document, contract.RetentionClause, contract.SectionalTakeoverClause,
            contract.SubcontractTerms, contract.ClaimClause, policy.Retention.MaximumRetentionPercent,
            policy.Retention.DefectsLiabilityDays, policy.Retention.SectionalTakeoverReleasePercent,
            policy.Controls.ControlProvisionalSums, policy.Controls.ControlContingencies,
            policy.Controls.ControlDefectsLiability, policy.Controls.ControlSectionalTakeover,
            policy.Controls.ControlSubcontracts, policy.Controls.ControlClaimClauses,
            policy.Controls.RequireCommercialTermsDocument)).ToList();
        if (contract.CommercialTermsConfigurationProfileId != policy.Profile.Id ||
            contract.ContractControlsDecisionId != policy.ControlsDecision.Id ||
            contract.RetentionDecisionId != policy.RetentionDecision.Id ||
            !FixedEquals(contract.CommercialTermsPolicyHash, policy.Hash))
            issues.Add("Commercial terms were not configured under the currently effective QS policy.");
        return issues;
    }

    private static QuantitySurveyContractCommercialTermsDto Map(Contract value, Guid projectId) => new()
    {
        ContractId = value.Id,
        ProjectId = projectId,
        ContractNumber = value.ContractNumber,
        ContractTitle = value.ContractTitle,
        ContractStatus = value.Status,
        ContractValue = value.ContractValue,
        Currency = value.Currency,
        PaymentTermId = value.PaymentTermId,
        ProvisionalSumAmount = value.ProvisionalSumAmount,
        ContingencyAmount = value.ContingencyAmount,
        RetentionPercentage = value.RetentionPercentage,
        DefectsLiabilityDays = value.DefectsLiabilityDays,
        RetentionClause = value.RetentionClause,
        AllowSectionalTakeover = value.AllowSectionalTakeover,
        SectionalTakeoverClause = value.SectionalTakeoverClause,
        AllowSubcontracting = value.AllowSubcontracting,
        SubcontractPaymentTermId = value.SubcontractPaymentTermId,
        SubcontractTerms = value.SubcontractTerms,
        ClaimNoticePeriodDays = value.ClaimNoticePeriodDays,
        ClaimClause = value.ClaimClause,
        CommercialTermsContractDocumentId = value.CommercialTermsContractDocumentId,
        ConfigurationProfileId = value.CommercialTermsConfigurationProfileId,
        ContractControlsDecisionId = value.ContractControlsDecisionId,
        RetentionDecisionId = value.RetentionDecisionId,
        PolicyHash = value.CommercialTermsPolicyHash,
        ConfiguredAt = value.CommercialTermsConfiguredAt,
        RowVersion = Convert.ToBase64String(value.RowVersion)
    };

    private static object Snapshot(Contract value) => new
    {
        value.Id,
        value.ContractNumber,
        value.ContractValue,
        value.Currency,
        value.PaymentTermId,
        value.ProvisionalSumAmount,
        value.ContingencyAmount,
        value.RetentionPercentage,
        value.DefectsLiabilityDays,
        value.RetentionClause,
        value.AllowSectionalTakeover,
        value.SectionalTakeoverClause,
        value.AllowSubcontracting,
        value.SubcontractPaymentTermId,
        value.SubcontractTerms,
        value.ClaimNoticePeriodDays,
        value.ClaimClause,
        value.CommercialTermsContractDocumentId,
        value.CommercialTermsConfigurationProfileId,
        value.ContractControlsDecisionId,
        value.RetentionDecisionId,
        value.CommercialTermsPolicyHash,
        value.CommercialTermsConfiguredAt,
        value.CommercialTermsConfiguredById
    };

    private static void ApplyRowVersion(Contract value, string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            throw Validation("Refresh the contract before changing its commercial terms.");
        byte[] expected;
        try { expected = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw Validation("The contract row version is invalid. Refresh and retry."); }
        if (expected.Length != value.RowVersion.Length ||
            !CryptographicOperations.FixedTimeEquals(expected, value.RowVersion))
            throw Conflict("The contract changed. Refresh the commercial terms and retry.");
    }

    private async Task SaveAsync(CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        { throw Conflict("The contract changed. Refresh the commercial terms and retry."); }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: >= 51941 and <= 51949 } ||
            exception.InnerException?.Message.Contains("commercial terms", StringComparison.OrdinalIgnoreCase) == true)
        { throw Conflict("The commercial-terms request conflicts with a tenant, relationship, or database control."); }
    }

    private async Task SetMutationContextAsync(Contract contract, CancellationToken token)
    {
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sys.sp_set_session_context @key=N'qs_contract_terms_id', @value={0};",
            [contract.Id.ToString()], token);
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sys.sp_set_session_context @key=N'qs_contract_terms_actor', @value={0};",
            [UserId.ToString()], token);
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sys.sp_set_session_context @key=N'qs_contract_terms_hash', @value={0};",
            [contract.CommercialTermsRequestHash ?? string.Empty], token);
    }

    private async Task ClearMutationContextAsync(CancellationToken token)
    {
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sys.sp_set_session_context @key=N'qs_contract_terms_id', @value=NULL;", token);
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sys.sp_set_session_context @key=N'qs_contract_terms_actor', @value=NULL;", token);
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sys.sp_set_session_context @key=N'qs_contract_terms_hash', @value=NULL;", token);
    }

    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));
    private static bool FixedEquals(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Correlation(string value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N") : value.Trim()[..Math.Min(value.Trim().Length, 100)];
    private static QuantitySurveyContractCommercialTermsValidationException Validation(string message) => new(message);
    private static QuantitySurveyContractCommercialTermsConflictException Conflict(string message) => new(message);

    private sealed record Policy(
        QuantitySurveyConfigurationProfile Profile,
        QuantitySurveyConfigurationDecision RetentionDecision,
        QuantitySurveyConfigurationDecision ControlsDecision,
        QsRetentionValue Retention,
        QsContractControlsValue Controls,
        string Hash);
}
