using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Idempotent development/test prerequisites for exercising supplier onboarding end to end.
/// It intentionally does not create an applicant, token, payment, registration, or supplier;
/// those records must be produced by the workflow under test.
/// </summary>
public sealed class ProcurementSupplierOnboardingTestSeeder
{
    public const string ProfileCode = "TDC-SUPPLIER-ONBOARDING-TEST";
    public const string PolicyCode = "TDC-SUPPLIER-E2E";
    private const string CreatedBy = "Development supplier-onboarding seeder";
    private static readonly DateTime EffectiveFromUtc =
        new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions DecisionJsonOptions = CreateDecisionJsonOptions();
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly IFinanceAccountProvisioningService _financeAccountProvisioning;
    private readonly ILogger<ProcurementSupplierOnboardingTestSeeder> _logger;

    public ProcurementSupplierOnboardingTestSeeder(
        ApplicationDbContext context,
        IFinanceAccountProvisioningService financeAccountProvisioning,
        ILogger<ProcurementSupplierOnboardingTestSeeder> logger)
    {
        _context = context;
        _financeAccountProvisioning = financeAccountProvisioning;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .SingleOrDefaultAsync(item => item.Code == "DEFAULT" && !item.IsDeleted, cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning("Default tenant not found; supplier-onboarding test prerequisites were not seeded.");
            return;
        }

        var receiptAccount = await EnsureAccountAsync(
            tenant.Id,
            "1040",
            "Supplier Onboarding Receipt Clearing",
            AccountType.Asset,
            cancellationToken);
        var revenueAccount = await EnsureAccountAsync(
            tenant.Id,
            "4930",
            "Supplier Onboarding Fee Revenue",
            AccountType.Revenue,
            cancellationToken);
        var taxAccount = await EnsureAccountAsync(
            tenant.Id,
            "2210",
            "Supplier Onboarding Tax Payable",
            AccountType.Liability,
            cancellationToken);

        // Public OTP/token issuance is intentionally blocked unless the tenant opts in.
        // Defer this independent caller mutation until Finance provisioning has completed in
        // its isolated service-owned unit; pending caller work is never carried into that unit.
        tenant.AllowSelfRegistration = true;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = CreatedBy;
        await _context.SaveChangesAsync(cancellationToken);

        await EnsurePaymentMethodsAsync(tenant.Id, receiptAccount.AccountId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var workflow = await _context.WorkflowDefinitions
            .Include(item => item.Steps.Where(step => !step.IsDeleted))
            .Where(item => item.TenantId == tenant.Id && !item.IsDeleted && item.IsActive &&
                           item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            .OrderByDescending(item => item.Name == "Business Partner Approval")
            .ThenBy(item => item.Name)
            .FirstOrDefaultAsync(item => item.Steps.Any(step => step.Order > 0), cancellationToken)
            ?? throw new InvalidOperationException(
                "A Published workflow definition with at least one step is required for supplier-onboarding test data.");

        var approvalStep = workflow.Steps
            .Where(item => !item.IsDeleted && item.Order > 0)
            .OrderByDescending(item => item.Name == "PendingApproval")
            .ThenBy(item => item.Order)
            .First();
        var actorUserId = await _context.Users
            .Where(item => item.TenantId == tenant.Id && item.IsActive)
            .OrderByDescending(item => item.UserName == "admin")
            .ThenBy(item => item.UserName)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (actorUserId == Guid.Empty)
            throw new InvalidOperationException(
                "An active tenant user is required for supplier evidence-pack lifecycle lineage.");

        var profile = await EnsureConfigurationProfileAsync(
            tenant.Id,
            revenueAccount.AccountId,
            taxAccount.AccountId,
            workflow.Id,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var policy = await EnsureEffectiveProcurementPolicyAsync(
            tenant.Id,
            profile,
            actorUserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var category in Enum.GetValues<ProcurementSupplierRegistrationCategory>())
        {
            await EnsureEvidencePackAsync(
                tenant.Id,
                profile,
                workflow,
                actorUserId,
                approvalStep.Order,
                approvalStep.Name,
                category,
                cancellationToken);
        }
        _logger.LogInformation(
            "Supplier-onboarding test prerequisites are ready for tenant {TenantId}: profile {ProfileCode}, " +
            "effective policy {PolicyCode}, three evidence packs, GL accounts, and coded payment methods.",
            tenant.Id,
            ProfileCode,
            policy.Code);
    }

    private async Task<ProvisionedFinanceAccountDto> EnsureAccountAsync(
        Guid tenantId,
        string accountCode,
        string accountName,
        AccountType accountType,
        CancellationToken cancellationToken)
    {
        // Procurement owns this test intent, not Finance account structure. Provisioning keeps
        // tenant, reviewed classification, canonical segments and enabled book mappings under
        // Finance authority; do not restore direct Account/category-caption writes here.
        return await _financeAccountProvisioning.ProvisionAsync(new ProvisionFinanceAccountDto
        {
            TenantId = tenantId,
            AccountCode = accountCode,
            AccountNumber = accountCode,
            AccountName = accountName,
            CoreAccountType = accountType,
            CurrencyCode = "GHS",
            Description = "Development/test posting account for the governed supplier-onboarding token flow.",
            IsSegmented = true
        }, cancellationToken);
    }

    private async Task EnsurePaymentMethodsAsync(
        Guid tenantId,
        Guid receiptAccountId,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new PaymentMethodSeed("CASH", "Cash", PaymentMethodType.Cash, false, false),
            new PaymentMethodSeed("CHQ", "Cheque", PaymentMethodType.Cheque, true, true),
            new PaymentMethodSeed("EFT", "Electronic Funds Transfer", PaymentMethodType.EFT, true, true),
            new PaymentMethodSeed("MOMO", "Mobile Money", PaymentMethodType.MobileMoney, true, true),
            new PaymentMethodSeed("BANK", "Bank Transfer", PaymentMethodType.BankTransfer, true, true)
        };
        var codes = definitions.Select(item => item.Code).ToList();
        var existing = await _context.PaymentMethods
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.Code != null &&
                           codes.Contains(item.Code))
            .ToListAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            var method = existing.SingleOrDefault(item => item.Code == definition.Code);
            if (method is null)
            {
                _context.PaymentMethods.Add(new FinancePaymentMethod
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = definition.Code,
                    Name = definition.Name,
                    Type = definition.Type,
                    Description = $"{definition.Name} payment.",
                    IsActive = true,
                    RequiresBankAccount = definition.RequiresBankAccount,
                    RequiresReference = definition.RequiresReference,
                    DefaultGLAccountId = receiptAccountId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CreatedBy
                });
                continue;
            }

            method.IsActive = true;
            method.DefaultGLAccountId ??= receiptAccountId;
            method.UpdatedAt = DateTime.UtcNow;
            method.UpdatedBy = CreatedBy;
        }
    }

    private async Task<ProcurementConfigurationProfile> EnsureConfigurationProfileAsync(
        Guid tenantId,
        Guid revenueAccountId,
        Guid taxAccountId,
        Guid exemptionWorkflowDefinitionId,
        CancellationToken cancellationToken)
    {
        var profile = await _context.ProcurementConfigurationProfiles
            .Include(item => item.Decisions)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted &&
                                          item.ProfileCode == ProfileCode && item.Version == 1,
                cancellationToken);
        var now = DateTime.UtcNow;
        if (profile is null)
        {
            profile = new ProcurementConfigurationProfile
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000001"),
                TenantId = tenantId,
                ProfileKey = Guid.Parse("70000000-0000-0000-0000-000000000002"),
                ProfileCode = ProfileCode,
                Name = "Supplier Onboarding End-to-End Test Profile",
                Version = 1,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = EffectiveFromUtc,
                IsDefault = true,
                ChangeSummary = "Development-only effective DEC-007 prerequisites for end-to-end supplier onboarding tests.",
                PublishedAt = now,
                CreatedAt = now,
                CreatedBy = CreatedBy
            };
            _context.ProcurementConfigurationProfiles.Add(profile);
        }
        else
        {
            profile.Name = "Supplier Onboarding End-to-End Test Profile";
            profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Published;
            profile.EffectiveFrom = EffectiveFromUtc;
            profile.EffectiveTo = null;
            profile.IsDefault = true;
            profile.PublishedAt ??= now;
            profile.UpdatedAt = now;
            profile.UpdatedBy = CreatedBy;
        }

        var otherEffectiveDefaults = await _context.ProcurementConfigurationProfiles
            .Where(item => item.TenantId == tenantId && item.Id != profile.Id && !item.IsDeleted &&
                           item.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                           item.IsDefault && item.EffectiveFrom <= now &&
                           (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .ToListAsync(cancellationToken);
        foreach (var other in otherEffectiveDefaults)
        {
            other.IsDefault = false;
            other.UpdatedAt = now;
            other.UpdatedBy = CreatedBy;
        }

        var feeValue = new ProcurementSupplierFeeDecisionValueDto
        {
            Mode = ProcurementSupplierOnboardingFeeMode.Paid,
            FeeType = "Supplier onboarding application token",
            Amount = 100m,
            CurrencyCode = "GHS",
            TaxPercent = 0m,
            PaymentChannels = ["CASH", "MOMO", "EFT", "BANK"],
            RevenueAccountId = revenueAccountId,
            TaxAccountId = taxAccountId,
            ExemptionWorkflowDefinitionId = exemptionWorkflowDefinitionId,
            ReceiptNumberFormat = "SUP-REC-{YYYY}-{######}",
            ExemptionRule = "An authorized internal reviewer must approve fee exemptions through the selected shared workflow.",
            RefundRule = "Verified duplicate or failed token payments may be refunded after Finance review and approval.",
            RenewalRule = "The token remains valid until the application reaches Approved or Rejected; a new application requires a new token.",
            EffectiveFrom = EffectiveFromUtc,
            EffectiveTo = null
        };
        var rawJson = JsonSerializer.Serialize(feeValue, DecisionJsonOptions);
        using var document = JsonDocument.Parse(rawJson);
        var validation = ProcurementConfigurationDecisionRegistry.Validate("DEC-007", 1, document.RootElement);
        if (!validation.IsValid || validation.CanonicalJson is null)
            throw new InvalidOperationException(
                $"The seeded DEC-007 value is invalid: {string.Join(" ", validation.Errors)}");

        var decision = profile.Decisions.SingleOrDefault(item =>
            !item.IsDeleted && item.DecisionKey == "DEC-007");
        if (decision is null)
        {
            decision = new ProcurementConfigurationDecision
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000007"),
                TenantId = tenantId,
                ProfileId = profile.Id,
                DecisionKey = "DEC-007",
                SchemaVersion = 1,
                OwnerGroup = "Procurement + Finance",
                CreatedAt = now,
                CreatedBy = CreatedBy
            };
            profile.Decisions.Add(decision);
        }

        decision.Status = ProcurementConfigurationDecisionStatus.Approved;
        decision.ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved;
        decision.EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified;
        decision.ValueJson = validation.CanonicalJson;
        decision.DecisionDate = now;
        decision.EffectiveFrom = validation.EffectiveFrom;
        decision.EffectiveTo = validation.EffectiveTo;
        decision.ApprovedAt = now;
        decision.ApprovalReference = "DEVELOPMENT-SEED-DEC-007";
        decision.SourceLineage = "Development supplier-onboarding end-to-end test fixture";
        decision.Notes = "Test-only profile. Replace with an approved organizational policy before production use.";
        decision.UpdatedAt = now;
        decision.UpdatedBy = CreatedBy;

        return profile;
    }

    private async Task<ProcurementPolicySet> EnsureEffectiveProcurementPolicyAsync(
        Guid tenantId,
        ProcurementConfigurationProfile profile,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var effectivePolicies = await _context.ProcurementPolicySets
            .Include(item => item.SodRules.Where(rule => !rule.IsDeleted))
            .Where(item => item.TenantId == tenantId && !item.IsDeleted &&
                           item.LifecycleStatus == ProcurementPolicyLifecycleStatus.Published &&
                           item.EffectiveFrom <= now &&
                           (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= now))
            .ToListAsync(cancellationToken);

        if (effectivePolicies.Count > 0)
        {
            var defaults = effectivePolicies.Where(item => item.IsDefault).ToList();
            var selected = defaults.Count == 1
                ? defaults[0]
                : defaults.Count == 0 && effectivePolicies.Count == 1
                    ? effectivePolicies[0]
                    : null;
            if (selected is null)
                throw new InvalidOperationException(
                    "Supplier-onboarding E2E seeding found an ambiguous effective procurement-policy selection. " +
                    "Retain one effective default policy before retrying the seed.");

            // Policy-specific SOD declarations are optional. An effective policy owned by the
            // tenant is authoritative business data, so this development seeder must neither
            // mutate nor reject it merely because it does not carry the recommended templates.
            if (!string.Equals(selected.Code, PolicyCode, StringComparison.OrdinalIgnoreCase))
                return selected;

            EnsureRequiredSodControls(selected, actorUserId, now);
            return selected;
        }

        var policy = await _context.ProcurementPolicySets
            .Include(item => item.SodRules.Where(rule => !rule.IsDeleted))
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted &&
                                          item.Code == PolicyCode && item.Version == 1,
                cancellationToken);
        if (policy is null)
        {
            policy = new ProcurementPolicySet
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000501"),
                TenantId = tenantId,
                PolicyKey = Guid.Parse("70000000-0000-0000-0000-000000000502"),
                Code = PolicyCode,
                Name = "Supplier Onboarding End-to-End Control Policy",
                Description = "Development/test-only effective policy for supplier-onboarding maker-checker verification.",
                Version = 1,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                ScopeType = ProcurementPolicyScopeType.TenantBaseline,
                SourceConfigurationProfileId = profile.Id,
                DefaultCurrencyCode = "GHS",
                EffectiveFrom = EffectiveFromUtc,
                IsDefault = true,
                ChangeSummary = "Development supplier-onboarding E2E SOD prerequisites.",
                PublishedAt = now,
                PublishedById = actorUserId,
                CreatedAt = now,
                CreatedBy = CreatedBy,
                CreatedById = actorUserId
            };
            _context.ProcurementPolicySets.Add(policy);
        }
        else
        {
            policy.Name = "Supplier Onboarding End-to-End Control Policy";
            policy.Description = "Development/test-only effective policy for supplier-onboarding maker-checker verification.";
            policy.LifecycleStatus = ProcurementPolicyLifecycleStatus.Published;
            policy.ScopeType = ProcurementPolicyScopeType.TenantBaseline;
            policy.SourceConfigurationProfileId = profile.Id;
            policy.DefaultCurrencyCode = "GHS";
            policy.EffectiveFrom = EffectiveFromUtc;
            policy.EffectiveTo = null;
            policy.IsDefault = true;
            policy.PublishedAt ??= now;
            policy.PublishedById ??= actorUserId;
            policy.RetiredAt = null;
            policy.RetiredById = null;
            policy.UpdatedAt = now;
            policy.UpdatedBy = CreatedBy;
            policy.LastModifiedById = actorUserId;
        }

        EnsureRequiredSodControls(policy, actorUserId, now);
        return policy;
    }

    private static void EnsureRequiredSodControls(
        ProcurementPolicySet policy,
        Guid actorUserId,
        DateTime now)
    {
        var ordinal = 0;
        foreach (var definition in ProcurementSodRequiredControlRegistry.Definitions)
        {
            ordinal++;
            var rule = policy.SodRules.SingleOrDefault(item =>
                !item.IsDeleted &&
                string.Equals(item.RuleCode, definition.Code, StringComparison.OrdinalIgnoreCase));
            if (rule is null)
            {
                rule = new ProcurementPolicySodRule
                {
                    Id = Guid.Parse($"70000000-0000-0000-0000-{600 + ordinal:D12}"),
                    TenantId = policy.TenantId,
                    PolicySetId = policy.Id,
                    RuleCode = definition.Code,
                    CreatedAt = now,
                    CreatedBy = CreatedBy,
                    CreatedById = actorUserId
                };
                policy.SodRules.Add(rule);
            }

            rule.Name = definition.Name;
            rule.InitiatorRole = definition.InitiatorRole;
            rule.ConflictingRole = definition.ConflictingRole;
            rule.EntityType = definition.EntityType;
            rule.Action = definition.Action;
            rule.Enforcement = ProcurementSodEnforcement.HardStop;
            rule.Explanation = definition.Explanation;
            rule.OverrideAction = ProcurementPolicyOverrideAction.Add;
            rule.SourceRuleId = null;
            rule.SourceDecisionKey = definition.SourceDecisionKey;
            rule.Priority = 100;
            rule.IsEnabled = true;
            rule.EffectiveFrom = EffectiveFromUtc;
            rule.EffectiveTo = null;
            rule.UpdatedAt = now;
            rule.UpdatedBy = CreatedBy;
            rule.LastModifiedById = actorUserId;
        }
    }

    private async Task EnsureEvidencePackAsync(
        Guid tenantId,
        ProcurementConfigurationProfile profile,
        WorkflowDefinition workflow,
        Guid actorUserId,
        int approvalStepOrder,
        string approvalStepName,
        ProcurementSupplierRegistrationCategory category,
        CancellationToken cancellationToken)
    {
        var categoryCode = category.ToString().ToUpperInvariant();
        var packCode = $"SUP-{categoryCode}-TEST";
        var pack = await _context.ProcurementSupplierEvidencePackVersions
            .Include(item => item.Requirements)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && !item.IsDeleted &&
                                          item.PackCode == packCode && item.Version == 1,
                cancellationToken);
        var now = DateTime.UtcNow;
        if (pack?.Status == ProcurementSupplierEvidencePackStatus.Published)
            return;

        if (pack is null)
        {
            var ordinal = (int)category + 1;
            pack = new ProcurementSupplierEvidencePackVersion
            {
                Id = Guid.Parse($"70000000-0000-0000-0000-00000000020{ordinal}"),
                TenantId = tenantId,
                PackKey = Guid.Parse($"70000000-0000-0000-0000-00000000021{ordinal}"),
                PackCode = packCode,
                Version = 1,
                Category = category,
                CreatedAt = now,
                CreatedBy = CreatedBy
            };
            _context.ProcurementSupplierEvidencePackVersions.Add(pack);
        }

        pack.Name = $"{category} Supplier Test Evidence Pack";
        pack.Description = "Development/test evidence requirements for the complete supplier-application workflow.";
        pack.Status = ProcurementSupplierEvidencePackStatus.Draft;
        pack.EffectiveFromUtc = EffectiveFromUtc;
        pack.EffectiveToUtc = null;
        pack.SourceConfigurationProfileId = profile.Id;
        pack.SourceConfigurationProfileCode = profile.ProfileCode;
        pack.SourceConfigurationProfileVersion = profile.Version;
        pack.CreationCorrelationId = $"dev-seed-supplier-pack-{categoryCode.ToLowerInvariant()}";
        pack.LastOperation = "Published";
        pack.LastOperationCorrelationId = $"dev-seed-supplier-pack-{categoryCode.ToLowerInvariant()}-published";
        pack.WorkflowDefinitionId = workflow.Id;
        pack.ChangeSummary = "Initial development/test supplier evidence pack.";
        pack.ReviewComment = "Published by the idempotent development seeder for end-to-end testing.";
        pack.UpdatedAt = now;
        pack.UpdatedBy = CreatedBy;

        EnsureRequirement(
            pack,
            $"70000000-0000-0000-0000-0000000003{(int)category + 1}1",
            "BUSINESS-REGISTRATION",
            "Business registration certificate",
            "BusinessRegistration",
            ProcurementSupplierEvidenceValidityMode.NotApplicable,
            null,
            approvalStepOrder,
            approvalStepName);
        EnsureRequirement(
            pack,
            $"70000000-0000-0000-0000-0000000003{(int)category + 1}2",
            "TAX-CLEARANCE",
            "Tax clearance certificate",
            "TaxClearance",
            ProcurementSupplierEvidenceValidityMode.MinimumRemainingDays,
            30,
            approvalStepOrder,
            approvalStepName);

        CapturePack(pack);
        await _context.SaveChangesAsync(cancellationToken);

        pack.Status = ProcurementSupplierEvidencePackStatus.PendingApproval;
        pack.SubmittedById = actorUserId;
        pack.SubmittedAtUtc = now;
        pack.LastOperation = "Submitted";
        pack.LastOperationCorrelationId = $"dev-seed-supplier-pack-{categoryCode.ToLowerInvariant()}-submitted";
        CapturePack(pack);
        await _context.SaveChangesAsync(cancellationToken);

        var workflowInstance = await _context.WorkflowInstances.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.WorkflowDefinitionId == workflow.Id &&
            item.EntityId == pack.Id && !item.IsDeleted,
            cancellationToken);
        if (workflowInstance is null)
        {
            var instanceId = Guid.Parse(
                $"70000000-0000-0000-0000-00000000040{(int)category + 1}");
            workflowInstance = new WorkflowInstance
            {
                Id = instanceId,
                TenantId = tenantId,
                WorkflowDefinitionId = workflow.Id,
                EntityId = pack.Id,
                EntityTypeId = workflow.EntityTypeId,
                Status = WorkflowInstanceStatus.Completed,
                Priority = WorkflowPriority.Normal,
                InitiatedById = actorUserId,
                StartedById = actorUserId,
                CreatedDate = now,
                StartedDate = now,
                CompletedDate = now,
                DataContext = JsonSerializer.Serialize(new
                {
                    sourceType = nameof(ProcurementSupplierEvidencePackVersion),
                    sourceId = pack.Id,
                    sourceReference = $"{pack.PackCode}/v{pack.Version}",
                    seededFor = "supplier-onboarding-e2e"
                }, WebJsonOptions),
                Notes = "Completed development/test workflow lineage for the supplier evidence pack.",
                CreatedAt = now,
                CreatedBy = CreatedBy
            };
            _context.WorkflowInstances.Add(workflowInstance);
            await _context.SaveChangesAsync(cancellationToken);
        }

        pack.Status = ProcurementSupplierEvidencePackStatus.Published;
        pack.WorkflowInstanceId = workflowInstance.Id;
        pack.PublishedById = actorUserId;
        pack.PublishedAtUtc = now;
        pack.LastOperation = "Published";
        pack.LastOperationCorrelationId = $"dev-seed-supplier-pack-{categoryCode.ToLowerInvariant()}-published";
        CapturePack(pack);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureRequirement(
        ProcurementSupplierEvidencePackVersion pack,
        string preferredId,
        string code,
        string name,
        string documentType,
        ProcurementSupplierEvidenceValidityMode validityMode,
        int? minimumRemainingDays,
        int approvalStepOrder,
        string approvalStepName)
    {
        var requirement = pack.Requirements.SingleOrDefault(item =>
            !item.IsDeleted && item.RequirementCode == code);
        if (requirement is null)
        {
            requirement = new ProcurementSupplierEvidenceRequirement
            {
                Id = Guid.Parse(preferredId),
                TenantId = pack.TenantId,
                PackVersionId = pack.Id,
                RequirementCode = code,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = CreatedBy
            };
            pack.Requirements.Add(requirement);
        }

        requirement.Name = name;
        requirement.Description = $"Upload the current {name.ToLowerInvariant()} through the restricted applicant portal.";
        requirement.Kind = ProcurementSupplierEvidenceRequirementKind.Document;
        requirement.DocumentType = documentType;
        requirement.IsMandatory = true;
        requirement.ClassificationScheme = null;
        requirement.AllowedClassificationsJson = null;
        requirement.ValidityMode = validityMode;
        requirement.MinimumRemainingDays = minimumRemainingDays;
        requirement.ApprovalStepOrder = approvalStepOrder;
        requirement.ApprovalStepName = approvalStepName;
        requirement.MaxFileSizeBytes = 10 * 1024 * 1024;
        requirement.AllowedMimeTypesJson = JsonSerializer.Serialize(
            new[] { "application/pdf", "image/jpeg", "image/png" }, WebJsonOptions);
        requirement.UpdatedAt = DateTime.UtcNow;
        requirement.UpdatedBy = CreatedBy;
        requirement.IntegrityHash = Hash(JsonSerializer.Serialize(new
        {
            requirement.RequirementCode,
            requirement.Name,
            requirement.Description,
            requirement.Kind,
            requirement.DocumentType,
            requirement.IsMandatory,
            requirement.ClassificationScheme,
            requirement.AllowedClassificationsJson,
            requirement.ValidityMode,
            requirement.MinimumRemainingDays,
            requirement.ApprovalStepOrder,
            requirement.ApprovalStepName,
            requirement.MaxFileSizeBytes,
            requirement.AllowedMimeTypesJson
        }, WebJsonOptions));
    }

    private static void CapturePack(ProcurementSupplierEvidencePackVersion pack)
    {
        var snapshot = new
        {
            pack.Id,
            pack.TenantId,
            pack.PackKey,
            pack.PackCode,
            pack.Name,
            pack.Description,
            pack.Category,
            pack.Version,
            pack.Status,
            pack.EffectiveFromUtc,
            pack.EffectiveToUtc,
            pack.SourceConfigurationProfileId,
            pack.SourceConfigurationProfileCode,
            pack.SourceConfigurationProfileVersion,
            pack.WorkflowDefinitionId,
            pack.WorkflowInstanceId,
            pack.SupersedesVersionId,
            pack.ChangeSummary,
            pack.ReviewComment,
            Requirements = pack.Requirements.Where(requirement => !requirement.IsDeleted)
                .OrderBy(requirement => requirement.RequirementCode)
                .Select(requirement => new
                {
                    requirement.RequirementCode,
                    requirement.Name,
                    requirement.Description,
                    requirement.Kind,
                    requirement.DocumentType,
                    requirement.IsMandatory,
                    requirement.ClassificationScheme,
                    requirement.AllowedClassificationsJson,
                    requirement.ValidityMode,
                    requirement.MinimumRemainingDays,
                    requirement.ApprovalStepOrder,
                    requirement.ApprovalStepName,
                    requirement.MaxFileSizeBytes,
                    requirement.AllowedMimeTypesJson,
                    requirement.IntegrityHash
                }).ToList()
        };
        pack.LifecycleSnapshotJson = JsonSerializer.Serialize(snapshot, WebJsonOptions);
        pack.IntegrityHash = Hash(pack.LifecycleSnapshotJson);
    }

    private static JsonSerializerOptions CreateDecisionJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        return options;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record PaymentMethodSeed(
        string Code,
        string Name,
        PaymentMethodType Type,
        bool RequiresBankAccount,
        bool RequiresReference);
}
