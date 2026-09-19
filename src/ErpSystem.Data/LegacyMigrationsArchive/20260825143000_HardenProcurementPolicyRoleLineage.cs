using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class HardenProcurementPolicyRoleLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "JustificationRequired",
            table: "ProcurementPolicyMethodRules",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "AuthorityRoleId",
            table: "ProcurementPolicyAuthorityRules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "EscalationAuthorityRoleId",
            table: "ProcurementPolicyAuthorityRules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ApproverRoleId",
            table: "ProcurementPolicyExceptionRules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "InitiatorRoleId",
            table: "ProcurementPolicySodRules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ConflictingRoleId",
            table: "ProcurementPolicySodRules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE authorityRule
               SET authorityRule.[AuthorityRoleId] = authorityRole.[Id]
              FROM [dbo].[ProcurementPolicyAuthorityRules] authorityRule
              JOIN [dbo].[AspNetRoles] authorityRole
                ON UPPER(LTRIM(RTRIM(authorityRole.[Name]))) = UPPER(LTRIM(RTRIM(authorityRule.[AuthorityRole])));

            UPDATE authorityRule
               SET authorityRule.[EscalationAuthorityRoleId] = escalationRole.[Id]
              FROM [dbo].[ProcurementPolicyAuthorityRules] authorityRule
              JOIN [dbo].[AspNetRoles] escalationRole
                ON UPPER(LTRIM(RTRIM(escalationRole.[Name]))) = UPPER(LTRIM(RTRIM(authorityRule.[EscalationAuthority])))
             WHERE authorityRule.[EscalationAuthority] IS NOT NULL;

            UPDATE exceptionRule
               SET exceptionRule.[ApproverRoleId] = approverRole.[Id]
              FROM [dbo].[ProcurementPolicyExceptionRules] exceptionRule
              JOIN [dbo].[AspNetRoles] approverRole
                ON UPPER(LTRIM(RTRIM(approverRole.[Name]))) = UPPER(LTRIM(RTRIM(exceptionRule.[ApproverRole])));

            UPDATE sodRule
               SET sodRule.[InitiatorRoleId] = initiatorRole.[Id]
              FROM [dbo].[ProcurementPolicySodRules] sodRule
              JOIN [dbo].[AspNetRoles] initiatorRole
                ON UPPER(LTRIM(RTRIM(initiatorRole.[Name]))) = UPPER(LTRIM(RTRIM(sodRule.[InitiatorRole])));

            UPDATE sodRule
               SET sodRule.[ConflictingRoleId] = conflictingRole.[Id]
              FROM [dbo].[ProcurementPolicySodRules] sodRule
              JOIN [dbo].[AspNetRoles] conflictingRole
                ON UPPER(LTRIM(RTRIM(conflictingRole.[Name]))) = UPPER(LTRIM(RTRIM(sodRule.[ConflictingRole])));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "JustificationRequired",
            table: "ProcurementPolicyMethodRules");

        migrationBuilder.DropColumn(
            name: "AuthorityRoleId",
            table: "ProcurementPolicyAuthorityRules");

        migrationBuilder.DropColumn(
            name: "EscalationAuthorityRoleId",
            table: "ProcurementPolicyAuthorityRules");

        migrationBuilder.DropColumn(
            name: "ApproverRoleId",
            table: "ProcurementPolicyExceptionRules");

        migrationBuilder.DropColumn(
            name: "InitiatorRoleId",
            table: "ProcurementPolicySodRules");

        migrationBuilder.DropColumn(
            name: "ConflictingRoleId",
            table: "ProcurementPolicySodRules");
    }
}
