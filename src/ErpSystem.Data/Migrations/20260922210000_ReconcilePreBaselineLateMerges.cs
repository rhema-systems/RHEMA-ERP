using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Replays guarded schema changes that were merged after the disposable baseline was generated
/// but carried timestamps earlier than the baseline. The original migration metadata remains
/// excluded, so these changes execute once, here, after the baseline and normal later migrations.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922210000_ReconcilePreBaselineLateMerges")]
public sealed class ReconcilePreBaselineLateMerges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        new SheChecklistBuilderReplay().Apply(migrationBuilder);
        new GuarantorIdentityReplay().Apply(migrationBuilder);
        new OrganizationUnitHistoryReplay().Apply(migrationBuilder);
        new CertificationModelReplay().Apply(migrationBuilder);
        new ProbationSourceReplay().Apply(migrationBuilder);
        new EmployeePayBasisReplay().Apply(migrationBuilder);
        new SalaryAssignmentWithdrawalReplay().Apply(migrationBuilder);
        new SalaryStructurePolicyReplay().Apply(migrationBuilder);
        new NamedSetsReplay().Apply(migrationBuilder);
        new UnitFinanceAccountReplay().Apply(migrationBuilder);
        new SubRecordGeographyReplay().Apply(migrationBuilder);
        new TeamTermsReplay().Apply(migrationBuilder);
        new TeamMeetingsReplay().Apply(migrationBuilder);
        new TeamApprovalReasonsReplay().Apply(migrationBuilder);
        new ManpowerBudgetSalaryScaleReplay().Apply(migrationBuilder);
        new StaffRequisitionBudgetReplay().Apply(migrationBuilder);
        new StaffRequisitionCostReplay().Apply(migrationBuilder);
        new EmployeeSalaryChangeReplay().Apply(migrationBuilder);
        new CandidateIdentityReplay().Apply(migrationBuilder);
        new CriteriaCatalogueReplay().Apply(migrationBuilder);
        new PreEmploymentProvidersReplay().Apply(migrationBuilder);
        new UnionContactsReplay().Apply(migrationBuilder);
        new JobDescriptionGradeReplay().Apply(migrationBuilder);
        new DisabilityTypesReplay().Apply(migrationBuilder);
        new DropContractLeaveColumnsReplay().Apply(migrationBuilder);
        new TalentSegmentOwnershipReplay().Apply(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This migration reconciles schema that may already be owned by archived migrations on
        // upgraded databases. A rollback must not remove their data or shared current-model schema.
    }

    private sealed class SheChecklistBuilderReplay : AddSheChecklistBuilder
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class GuarantorIdentityReplay : AddGuarantorIdTypeAndDocuments
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class OrganizationUnitHistoryReplay : AddOrganizationUnitHistoryNotes
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class CertificationModelReplay : AddCertificationModel
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class ProbationSourceReplay : AddProbationSourceAndContractKind
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class EmployeePayBasisReplay : AddEmployeePayBasis
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class SalaryAssignmentWithdrawalReplay : AddSalaryAssignmentWithdrawal
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class SalaryStructurePolicyReplay : AddSalaryStructurePolicy
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class NamedSetsReplay : AddNamedSets
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class UnitFinanceAccountReplay : AddUnitFinanceAccount
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class SubRecordGeographyReplay : AddSubRecordGeographyAndRelationshipTypes
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class TeamTermsReplay : AddTeamTermsObjectivesAndTasks
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class TeamMeetingsReplay : AddTeamMeetingsAndReviews
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class TeamApprovalReasonsReplay : AddTeamApprovalRejectionReasons
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class ManpowerBudgetSalaryScaleReplay : AddManpowerBudgetLineSalaryScale
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class StaffRequisitionBudgetReplay : AddStaffRequisitionBudgetLink
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class StaffRequisitionCostReplay : AddStaffRequisitionCostPayeeAndApproval
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class EmployeeSalaryChangeReplay : AddEmployeeSalaryChangeRequest
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class CandidateIdentityReplay : AddCandidateIdentityLanguagesAndCertification
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class CriteriaCatalogueReplay : AddCriteriaCatalogueValues
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class PreEmploymentProvidersReplay : AddPreEmploymentCheckProviders
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class UnionContactsReplay : AddUnionContactsDocumentsLogo
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class JobDescriptionGradeReplay : AddJobDescriptionProposedGrade
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class DisabilityTypesReplay : AddDisabilityTypes
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class DropContractLeaveColumnsReplay : DropContractLeaveColumns
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }

    private sealed class TalentSegmentOwnershipReplay : AddTalentSegmentOwnership
    { public void Apply(MigrationBuilder builder) => base.Up(builder); }
}
