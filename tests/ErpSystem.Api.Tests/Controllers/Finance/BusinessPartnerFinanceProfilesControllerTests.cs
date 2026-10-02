using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class BusinessPartnerFinanceProfilesControllerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expense_account_dates_are_checked_at_profile_start(bool accountEffective)
    {
        await using var fixture = new Fixture();
        var request = fixture.ApRequest();
        request.EffectiveFrom = DateTime.UtcNow.Date.AddYears(1);
        fixture.Expense.EffectiveDate = request.EffectiveFrom.AddDays(accountEffective ? 0 : 1);
        await fixture.SeedAsync();
        var result = await fixture.Controller.CreateApDraft(fixture.Partner.Id, request, default);
        if (accountEffective) result.Result.Should().BeOfType<OkObjectResult>();
        else Problem(result).Title.Should().Be("AP_EXPENSE_ACCOUNT_INVALID");
    }

    [Theory]
    [InlineData("Supplier", "Vendor")]
    [InlineData("Customer", "Client")]
    public async Task Existing_payment_term_aliases_remain_usable(string roleName, string applicability)
    {
        await using var fixture = new Fixture();
        fixture.Role.RoleType = Enum.Parse<BusinessPartnerRoleType>(roleName);
        fixture.Term.ApplicableTo = applicability;
        await fixture.SeedAsync();
        if (fixture.Role.RoleType == BusinessPartnerRoleType.Supplier)
        {
            (await fixture.Controller.CreateApDraft(fixture.Partner.Id, fixture.ApRequest(), default))
                .Result.Should().BeOfType<OkObjectResult>();
        }
        else
        {
            (await fixture.Controller.CreateArDraft(fixture.Partner.Id, new()
            {
                BusinessPartnerRoleId = fixture.Role.Id, EffectiveFrom = new(2026, 1, 1), PaymentTermId = fixture.Term.Id
            }, default)).Result.Should().BeOfType<OkObjectResult>();
        }
    }

    [Fact]
    public async Task Saved_ap_defaults_survive_reload_and_cannot_be_approved_by_submitter()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var created = await fixture.Controller.CreateApDraft(fixture.Partner.Id, fixture.ApRequest(), default);
        var saved = (BusinessPartnerApProfileDto)created.Result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        saved.DefaultExpenseAccountId.Should().Be(fixture.Expense.Id);
        saved.DefaultTaxGroupId.Should().Be(fixture.TaxGroup.Id);
        saved.PaymentTermId.Should().Be(fixture.Term.Id);
        var get = await fixture.Controller.Get(fixture.Partner.Id, default);
        var reloaded = (BusinessPartnerFinanceProfileSetDto)get.Result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        reloaded.Roles.Single().ApProfiles.Single().DefaultExpenseAccountId.Should().Be(fixture.Expense.Id);
        (await fixture.Controller.SubmitAp(fixture.Partner.Id, saved.Id, default)).Result.Should().BeOfType<OkObjectResult>();
        var denied = await fixture.Controller.ApproveAp(fixture.Partner.Id, saved.Id, new(), default);
        Problem(denied).Title.Should().Be("MAKER_CHECKER_REQUIRED");
        fixture.UserId = Guid.NewGuid();
        var approved = await fixture.Controller.ApproveAp(fixture.Partner.Id, saved.Id, new(), default);
        ((BusinessPartnerApProfileDto)approved.Result.Should().BeOfType<OkObjectResult>().Subject.Value!).Status.Should().Be("Approved");
        var edit = await fixture.Controller.UpdateApDraft(fixture.Partner.Id, saved.Id, fixture.ApRequest(), default);
        Problem(edit).Title.Should().Be("PROFILE_NOT_DRAFT");
    }

    [Fact]
    public async Task Saved_ar_credit_limit_is_persisted_and_survives_reload()
    {
        await using var fixture = new Fixture();
        fixture.Role.RoleType = BusinessPartnerRoleType.Customer;
        fixture.Term.ApplicableTo = "Customer";
        await fixture.SeedAsync();

        var created = await fixture.Controller.CreateArDraft(fixture.Partner.Id, new()
        {
            BusinessPartnerRoleId = fixture.Role.Id,
            EffectiveFrom = new DateTime(2026, 10, 1),
            PaymentTermId = fixture.Term.Id,
            CreditLimit = 12_500m,
            IsWithholdingAgent = true
        }, default);

        var saved = (BusinessPartnerArProfileDto)created.Result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        saved.CreditLimit.Should().Be(12_500m);
        (await fixture.Context.BusinessPartnerArProfileVersions.SingleAsync()).CreditLimit.Should().Be(12_500m);

        var reloaded = (BusinessPartnerFinanceProfileSetDto)(await fixture.Controller.Get(fixture.Partner.Id, default))
            .Result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        reloaded.Roles.Single().ArProfiles.Single().CreditLimit.Should().Be(12_500m);

        var updated = await fixture.Controller.UpdateArDraft(fixture.Partner.Id, saved.Id, new()
        {
            BusinessPartnerRoleId = fixture.Role.Id,
            EffectiveFrom = new DateTime(2026, 10, 1),
            PaymentTermId = fixture.Term.Id,
            CreditLimit = 15_000m,
            IsWithholdingAgent = false
        }, default);

        ((BusinessPartnerArProfileDto)updated.Result.Should().BeOfType<OkObjectResult>().Subject.Value!)
            .CreditLimit.Should().Be(15_000m);
        (await fixture.Context.BusinessPartnerArProfileVersions.SingleAsync()).CreditLimit.Should().Be(15_000m);
    }

    [Fact]
    public async Task Pending_approval_queue_is_shared_with_checkers_but_excludes_the_maker()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var makerId = fixture.UserId;
        var created = await fixture.Controller.CreateApDraft(fixture.Partner.Id, fixture.ApRequest(), default);
        var profileId = ((BusinessPartnerApProfileDto)((OkObjectResult)created.Result!).Value!).Id;
        await fixture.Controller.SubmitAp(fixture.Partner.Id, profileId, default);

        var makerResult = await fixture.Controller.GetPendingApprovals(default);
        ((IReadOnlyList<BusinessPartnerFinanceProfileApprovalQueueItemDto>)
            ((OkObjectResult)makerResult.Result!).Value!).Should().BeEmpty();

        fixture.UserId = Guid.NewGuid();
        var checkerResult = await fixture.Controller.GetPendingApprovals(default);
        var pending = (IReadOnlyList<BusinessPartnerFinanceProfileApprovalQueueItemDto>)
            ((OkObjectResult)checkerResult.Result!).Value!;
        pending.Should().ContainSingle();
        pending.Single().ProfileId.Should().Be(profileId);
        pending.Single().BusinessPartnerId.Should().Be(fixture.Partner.Id);
        pending.Single().SubmittedById.Should().Be(makerId);
        pending.Single().Ledger.Should().Be("ap");
    }

    [Theory]
    [InlineData("expense-tenant", "AP_EXPENSE_ACCOUNT_INVALID")]
    [InlineData("expense-inactive", "AP_EXPENSE_ACCOUNT_INVALID")]
    [InlineData("expense-control", "AP_EXPENSE_ACCOUNT_INVALID")]
    [InlineData("expense-revenue", "AP_EXPENSE_ACCOUNT_INVALID")]
    [InlineData("expense-not-postable", "AP_EXPENSE_ACCOUNT_INVALID")]
    [InlineData("tax-tenant", "AP_TAX_GROUP_INVALID")]
    [InlineData("tax-inactive", "AP_TAX_GROUP_INVALID")]
    [InlineData("tax-sales", "AP_TAX_GROUP_INVALID")]
    [InlineData("term-tenant", "PROFILE_PAYMENT_TERM_INVALID")]
    [InlineData("term-inactive", "PROFILE_PAYMENT_TERM_INVALID")]
    [InlineData("term-customer", "PROFILE_PAYMENT_TERM_INVALID")]
    public async Task Invalid_defaults_fail_before_creating_any_profile(string mutation, string code)
    {
        await using var fixture = new Fixture();
        switch (mutation)
        {
            case "expense-tenant": fixture.Expense.TenantId = Guid.NewGuid(); break;
            case "expense-inactive": fixture.Expense.Status = AccountStatus.Inactive; break;
            case "expense-control": fixture.Expense.IsControlAccount = true; break;
            case "expense-revenue": fixture.Expense.AccountType = AccountType.Revenue; break;
            case "expense-not-postable": fixture.Expense.AllowDirectPosting = false; break;
            case "tax-tenant": fixture.TaxGroup.TenantId = Guid.NewGuid(); break;
            case "tax-inactive": fixture.TaxGroup.IsActive = false; break;
            case "tax-sales": fixture.TaxGroup.Applicability = TaxApplicability.Sales; break;
            case "term-tenant": fixture.Term.TenantId = Guid.NewGuid(); break;
            case "term-inactive": fixture.Term.IsActive = false; break;
            case "term-customer": fixture.Term.ApplicableTo = "Customer"; break;
        }
        await fixture.SeedAsync();
        var result = await fixture.Controller.CreateApDraft(fixture.Partner.Id, fixture.ApRequest(), default);
        Problem(result).Title.Should().Be(code);
        fixture.Context.BusinessPartnerApProfileVersions.Should().BeEmpty();
    }

    [Fact]
    public async Task Withholding_only_group_cannot_be_saved_as_the_invoice_tax_default()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var withholding = new Tax
        {
            TenantId = fixture.TenantId, Code = "WHT-ONLY", Name = "WHT only", Rate = 7.5m,
            EffectiveFrom = new(2026, 1, 1), Applicability = TaxApplicability.Purchases,
            Category = TaxCategory.Withholding, IsActive = true
        };
        fixture.Context.AddRange(withholding, new TaxGroupComponent
        {
            TenantId = fixture.TenantId, TaxGroupId = fixture.TaxGroup.Id,
            TaxId = withholding.Id, Tax = withholding, TaxGroup = fixture.TaxGroup
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Controller.CreateApDraft(fixture.Partner.Id, fixture.ApRequest(), default);
        Problem(result).Title.Should().Be("AP_TAX_GROUP_WITHHOLDING_ONLY");
        fixture.Context.BusinessPartnerApProfileVersions.Should().BeEmpty();
    }

    [Fact]
    public async Task Disabled_account_between_draft_and_submission_does_not_advance_profile()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var created = await fixture.Controller.CreateApDraft(fixture.Partner.Id, fixture.ApRequest(), default);
        var id = ((BusinessPartnerApProfileDto)((OkObjectResult)created.Result!).Value!).Id;
        fixture.Expense.AllowDirectPosting = false;
        await fixture.Context.SaveChangesAsync();
        Problem(await fixture.Controller.SubmitAp(fixture.Partner.Id, id, default)).Title.Should().Be("AP_EXPENSE_ACCOUNT_INVALID");
        (await fixture.Context.BusinessPartnerApProfileVersions.SingleAsync()).Status.Should().Be(BusinessPartnerFinanceProfileStatus.Draft);
    }

    [Fact]
    public async Task Customer_profile_rejects_supplier_only_payment_term()
    {
        await using var fixture = new Fixture();
        fixture.Role.RoleType = BusinessPartnerRoleType.Customer;
        await fixture.SeedAsync();
        var result = await fixture.Controller.CreateArDraft(fixture.Partner.Id, new()
        {
            BusinessPartnerRoleId = fixture.Role.Id, EffectiveFrom = new(2026, 1, 1), PaymentTermId = fixture.Term.Id
        }, default);
        var problem = (ProblemDetails)result.Result.Should().BeOfType<ObjectResult>().Subject.Value!;
        problem.Title.Should().Be("PROFILE_PAYMENT_TERM_INVALID");
        fixture.Context.BusinessPartnerArProfileVersions.Should().BeEmpty();
    }

    [Fact]
    public async Task Foreign_partner_role_is_not_available_to_current_tenant()
    {
        await using var fixture = new Fixture();
        fixture.Role.TenantId = Guid.NewGuid();
        await fixture.SeedAsync();
        Problem(await fixture.Controller.CreateApDraft(fixture.Partner.Id, fixture.ApRequest(), default)).Title
            .Should().Be("BUSINESS_PARTNER_ROLE_NOT_FOUND");
        fixture.Context.BusinessPartnerApProfileVersions.Should().BeEmpty();
    }

    private static ProblemDetails Problem(ActionResult<BusinessPartnerApProfileDto> result) =>
        (ProblemDetails)result.Result.Should().BeOfType<ObjectResult>().Subject.Value!;

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; set; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public BusinessPartnerFinanceProfilesController Controller { get; }
        public BusinessPartner Partner { get; }
        public BusinessPartnerRole Role { get; }
        public Account Expense { get; }
        public TaxGroup TaxGroup { get; }
        public PaymentTerm Term { get; }

        public Fixture()
        {
            Context = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var user = new Mock<ICurrentUserProvider>();
            user.SetupGet(value => value.TenantId).Returns(TenantId);
            user.SetupGet(value => value.UserId).Returns(() => UserId);
            Controller = new(Context, user.Object) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
            Partner = new() { TenantId = TenantId, PartnerCode = "PROFILE-TEST", PartnerName = "Profile test", PartnerType = "Supplier", TaxIdentificationNumber = "PROFILE-TIN", IsActive = true };
            Role = new() { TenantId = TenantId, BusinessPartnerId = Partner.Id, BusinessPartner = Partner, RoleType = BusinessPartnerRoleType.Supplier, Status = BusinessPartnerRoleStatus.Active };
            Expense = new() { TenantId = TenantId, AccountCode = "5000", AccountNumber = "5000", AccountName = "Purchases", AccountType = AccountType.Expense, Status = AccountStatus.Active, AllowDirectPosting = true };
            TaxGroup = new() { TenantId = TenantId, Code = "PURCHASE", Name = "Purchase tax", Applicability = TaxApplicability.Purchases, IsActive = true };
            Term = new() { TenantId = TenantId, Code = "NET30", Name = "Net 30", DueDays = 30, ApplicableTo = "Supplier", IsActive = true };
        }

        public async Task SeedAsync()
        {
            Context.AddRange(Partner, Role, Expense, TaxGroup, Term);
            await Context.SaveChangesAsync();
        }

        public SaveBusinessPartnerApProfileRequest ApRequest() => new()
        {
            BusinessPartnerRoleId = Role.Id, EffectiveFrom = new(2026, 1, 1), PaymentTermId = Term.Id,
            DefaultExpenseAccountId = Expense.Id, DefaultTaxGroupId = TaxGroup.Id
        };
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
