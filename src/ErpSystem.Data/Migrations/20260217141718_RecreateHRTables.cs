using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecreateHRTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeShiftPreferences_Shifts_ShiftId",
                table: "EmployeeShiftPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSkills_Skills_SkillId",
                table: "EmployeeSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_EmployeePositions_PositionId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_Skills_SkillId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_Tenants_TenantId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Shifts_ShiftId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Skills_TenantId",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSkills_TenantId",
                table: "EmployeeSkills");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_TenantId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeContractDetails_TenantId",
                table: "EmployeeContractDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PositionSkillRequirement",
                table: "PositionSkillRequirement");

            migrationBuilder.DropIndex(
                name: "IX_PositionSkillRequirement_TenantId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropColumn(
                name: "QualificationName",
                table: "EmployeeQualifications");

            migrationBuilder.RenameTable(
                name: "PositionSkillRequirement",
                newName: "PositionSkillRequirements");

            migrationBuilder.RenameIndex(
                name: "IX_PositionSkillRequirement_SkillId",
                table: "PositionSkillRequirements",
                newName: "IX_PositionSkillRequirements_SkillId");

            migrationBuilder.RenameIndex(
                name: "IX_PositionSkillRequirement_PositionId",
                table: "PositionSkillRequirements",
                newName: "IX_PositionSkillRequirements_PositionId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WorkStations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactPersonId",
                table: "WorkStations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DigitalAddress",
                table: "WorkStations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "WorkStations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "WorkStations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StationType",
                table: "WorkStations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "RequiresCertification",
                table: "Skills",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Skills",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "CompanyAddress",
                table: "EmployeeWorkHistories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCertified",
                table: "EmployeeSkills",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DigitalAddress",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GrossUp",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsExpatriate",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LocationLevelId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationLevelId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "Overtime",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PayTax",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SSFund",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SocialSecurityNumber",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TINNumber",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TerminationDate",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationNotes",
                table: "Employees",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Tier2Only",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CountryId",
                table: "EmployeeQualifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomQualificationName",
                table: "EmployeeQualifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EmployeeQualifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QualificationId",
                table: "EmployeeQualifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "EmployeePositions",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<int>(
                name: "ExpectedHeadcount",
                table: "EmployeePositions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaximumAge",
                table: "EmployeePositions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAge",
                table: "EmployeePositions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumExperienceYears",
                table: "EmployeePositions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfGuarantors",
                table: "EmployeePositions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationLevelId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ReportsToPositionId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresGuarantor",
                table: "EmployeePositions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresLicense",
                table: "EmployeePositions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SalaryGradeId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffLevelId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkMode",
                table: "EmployeePositions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "IdentificationTypeId",
                table: "EmployeeIdentificationCards",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EmployeeIdentificationCards",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedDate",
                table: "EmployeeIdentificationCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "EmployeeEmergencyContacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "EmployeeEmergencyContacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContactType",
                table: "EmployeeEmergencyContacts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CountryId",
                table: "EmployeeEmergencyContacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DigitalAddress",
                table: "EmployeeEmergencyContacts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "EmployeeEmergencyContacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "EmployeeEmergencyContacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EmployeeEmergencyContacts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Relationship",
                table: "EmployeeDependents",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "DigitalAddress",
                table: "EmployeeDependents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityDescription",
                table: "EmployeeDependents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhanaCardNumber",
                table: "EmployeeDependents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDisability",
                table: "EmployeeDependents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeceased",
                table: "EmployeeDependents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEligibleForBenefits",
                table: "EmployeeDependents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmergencyContact",
                table: "EmployeeDependents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "EmployeeDependents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EmployeeDependents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "EmployeeDependents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PicturePath",
                table: "EmployeeDependents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelationshipDescription",
                table: "EmployeeDependents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PayFrequency",
                table: "EmployeeContractDetails",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ConfirmationDate",
                table: "EmployeeContractDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractStatus",
                table: "EmployeeContractDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPensionApplicable",
                table: "EmployeeContractDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxExempt",
                table: "EmployeeContractDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProbationPeriodDays",
                table: "EmployeeContractDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxTreatmentType",
                table: "EmployeeContractDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TerminationDate",
                table: "EmployeeContractDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "EmployeeContractDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingTaxRate",
                table: "EmployeeContractDetails",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "Departments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "PositionSkillRequirements",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRequired",
                table: "PositionSkillRequirements",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PositionSkillRequirements",
                table: "PositionSkillRequirements",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AppraisalGradeDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalGradeDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalGradeDefinitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BenefitPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyType = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    PolicyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PolicyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Recipient = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    MaxDependents = table.Column<int>(type: "int", nullable: true),
                    EmployeeContribution = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    EmployerContribution = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CoverageLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LimitPeriod = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenefitPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Divisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DivisionHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Divisions_Employees_DivisionHeadId",
                        column: x => x.DivisionHeadId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Divisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeContractTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeContractTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeContractTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeGuarantors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DigitalAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmployerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmployerAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmployerPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MonthlyIncome = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    NationalIdType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NationalIdNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NationalIdExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HasSignedGuarantorForm = table.Column<bool>(type: "bit", nullable: false),
                    DateFormSigned = table.Column<DateOnly>(type: "date", nullable: true),
                    GuarantorFormPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastContactDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeGuarantors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeGuarantors_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeGuarantors_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeGuarantors_Employees_VerifiedByEmployeeId",
                        column: x => x.VerifiedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeGuarantors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeReferees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefereeType = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Organization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PositionOrTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Relationship = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsContacted = table.Column<bool>(type: "bit", nullable: false),
                    ContactedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReferenceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeReferees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeReferees_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeReferees_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpatriateAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HomeCountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RelocationAllowance = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RelocationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FamilyAccompanying = table.Column<bool>(type: "bit", nullable: false),
                    AssignmentObjective = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VisaType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VisaExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WorkPermitNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkPermitExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpatriateAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpatriateAssignments_Countries_HomeCountryId",
                        column: x => x.HomeCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpatriateAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpatriateAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAssociates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssociateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HasFixedModule = table.Column<bool>(type: "bit", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PicturePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAssociates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalAssociates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdentificationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssuingAuthorityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuingCountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HasExpiryDate = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentificationTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdentificationTypes_Countries_IssuingCountryId",
                        column: x => x.IssuingCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdentificationTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KpiDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KpiName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MeasurementType = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TolerancePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiDefinitions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    DefaultDaysPerYear = table.Column<int>(type: "int", nullable: false),
                    MaxDaysPerYear = table.Column<int>(type: "int", nullable: false),
                    MaxConsecutiveDays = table.Column<int>(type: "int", nullable: true),
                    MinDaysNotice = table.Column<int>(type: "int", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    RequiresDocumentation = table.Column<bool>(type: "bit", nullable: false),
                    CalendarColor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HasSubTypes = table.Column<bool>(type: "bit", nullable: false),
                    AppliesToAllCategories = table.Column<bool>(type: "bit", nullable: false),
                    AllowCarryOver = table.Column<bool>(type: "bit", nullable: false),
                    MaxCarryOverDays = table.Column<int>(type: "int", nullable: true),
                    CountWeekendsAsLeave = table.Column<bool>(type: "bit", nullable: false),
                    CountHolidaysAsLeave = table.Column<bool>(type: "bit", nullable: false),
                    AllowCashConversion = table.Column<bool>(type: "bit", nullable: false),
                    ApplicableToGenders = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApplicableToDepartments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApplicableToEmploymentTypes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApplicableToPositions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationStructures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationChartNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CustomLabel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVacant = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationChartNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationChartNodes_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrganizationChartNodes_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrganizationChartNodes_OrganizationChartNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalTable: "OrganizationChartNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrganizationChartNodes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationStructures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceAppraisals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    AppraisalType = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RankInPosition = table.Column<int>(type: "int", nullable: true),
                    OverallComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StrengthsIdentified = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AreasForImprovement = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TrainingNeeds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CareerAspirations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendPromotion = table.Column<bool>(type: "bit", nullable: false),
                    RecommendIncrement = table.Column<bool>(type: "bit", nullable: false),
                    RecommendTraining = table.Column<bool>(type: "bit", nullable: false),
                    RecommendTermination = table.Column<bool>(type: "bit", nullable: false),
                    RecommendationNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NextAppraisalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AppealFiled = table.Column<bool>(type: "bit", nullable: false),
                    AppealDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppealReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AppealOutcome = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceAppraisals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisals_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceAppraisals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicHolidays_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Qualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Qualifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryGrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryGrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryGrades_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffLevels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Units_Employees_UnitHeadId",
                        column: x => x.UnitHeadId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Units_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Units_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BenefitPolicyRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BenefitPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationType = table.Column<int>(type: "int", nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenefitPolicyRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenefitPolicyRelations_BenefitPolicies_BenefitPolicyId",
                        column: x => x.BenefitPolicyId,
                        principalTable: "BenefitPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BenefitPolicyRelations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDependentBenefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeDependentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrolledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CoverageStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CoverageEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BenefitAmountUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDependentBenefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDependentBenefits_BenefitPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "BenefitPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDependentBenefits_EmployeeDependents_EmployeeDependentId",
                        column: x => x.EmployeeDependentId,
                        principalTable: "EmployeeDependents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeDependentBenefits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePositionBenefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePositionBenefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePositionBenefits_BenefitPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "BenefitPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionBenefits_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionBenefits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalCriterias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CriteriaName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CriteriaType = table.Column<int>(type: "int", nullable: false),
                    KpiDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalCriterias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalCriterias_KpiDefinitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "KpiDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalCriterias_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    EntitledDays = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UsedDays = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CarriedOverDays = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AdjustmentDays = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeavePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RelieverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeavePlans_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeavePlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeavePlans_Employees_PlannedBy",
                        column: x => x.PlannedBy,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeavePlans_Employees_RelieverId",
                        column: x => x.RelieverId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeavePlans_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeavePlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveSubTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubTypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaxDaysAllowed = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveSubTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveSubTypes_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveSubTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LevelNumber = table.Column<int>(type: "int", nullable: false),
                    RequiresAddress = table.Column<bool>(type: "bit", nullable: false),
                    RequiresContactInfo = table.Column<bool>(type: "bit", nullable: false),
                    AllowsEmployeeAssignment = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationLevels_LocationStructures_StructureId",
                        column: x => x.StructureId,
                        principalTable: "LocationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationLevels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LevelNumber = table.Column<int>(type: "int", nullable: false),
                    RequiresHead = table.Column<bool>(type: "bit", nullable: false),
                    AllowsDirectEmployees = table.Column<bool>(type: "bit", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationLevels_OrganizationStructures_StructureId",
                        column: x => x.StructureId,
                        principalTable: "OrganizationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationLevels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalAttachments_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalAttachments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluatorEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorRole = table.Column<int>(type: "int", nullable: false),
                    EvaluatorWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    IsAuthoritative = table.Column<bool>(type: "bit", nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverallNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluatorEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluatorEvaluations_Employees_EvaluatorId",
                        column: x => x.EvaluatorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluatorEvaluations_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluatorEvaluations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceImprovementPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PerformanceIssues = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExpectedStandards = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ImprovementActions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SupportProvided = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MeasurementCriteria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewSchedule = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: true),
                    OutcomeNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceImprovementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Employees_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalaryGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MinSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MidSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryLevels_SalaryGrades_SalaryGradeId",
                        column: x => x.SalaryGradeId,
                        principalTable: "SalaryGrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryLevels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalEmployeeResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalEmployeeResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalEmployeeResponses_AppraisalCriterias_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "AppraisalCriterias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalEmployeeResponses_PerformanceAppraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "PerformanceAppraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppraisalEmployeeResponses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionCriteriaMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    KpiTargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KpiMaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionCriteriaMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_AppraisalCriterias_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "AppraisalCriterias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionCriteriaMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalDays = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeavePlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RelieverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelieverNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClosureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosureNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_ApprovedByEmployeeId",
                        column: x => x.ApprovedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_RelieverEmployeeId",
                        column: x => x.RelieverEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_LeavePlans_LeavePlanId",
                        column: x => x.LeavePlanId,
                        principalTable: "LeavePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveCategoryAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveSubTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StaffLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCategoryAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveCategoryAllocations_LeaveSubTypes_LeaveSubTypeId",
                        column: x => x.LeaveSubTypeId,
                        principalTable: "LeaveSubTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveCategoryAllocations_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveCategoryAllocations_StaffLevels_StaffLevelId",
                        column: x => x.StaffLevelId,
                        principalTable: "StaffLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveCategoryAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StructureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DigitalAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Locations_LocationLevels_LocationLevelId",
                        column: x => x.LocationLevelId,
                        principalTable: "LocationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Locations_LocationStructures_StructureId",
                        column: x => x.StructureId,
                        principalTable: "LocationStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Locations_Locations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Locations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HeadEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationUnits_Employees_HeadEmployeeId",
                        column: x => x.HeadEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationUnits_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationUnits_OrganizationUnits_ParentUnitId",
                        column: x => x.ParentUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationUnits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PipReviewMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeeAttended = table.Column<bool>(type: "bit", nullable: false),
                    ProgressNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IssuesDiscussed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActionsAgreed = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EmployeeComments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConductedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipReviewMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipReviewMeetings_Employees_ConductedById",
                        column: x => x.ConductedById,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipReviewMeetings_PerformanceImprovementPlans_PipId",
                        column: x => x.PipId,
                        principalTable: "PerformanceImprovementPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipReviewMeetings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryNotches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalaryLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotchNumber = table.Column<int>(type: "int", nullable: false),
                    SalaryAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryNotches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryNotches_SalaryLevels_SalaryLevelId",
                        column: x => x.SalaryLevelId,
                        principalTable: "SalaryLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryNotches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeKpiTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KpiDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionCriteriaMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    WeightOverride = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeKpiTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_KpiDefinitions_KpiDefinitionId",
                        column: x => x.KpiDefinitionId,
                        principalTable: "KpiDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_PositionCriteriaMappings_PositionCriteriaMappingId",
                        column: x => x.PositionCriteriaMappingId,
                        principalTable: "PositionCriteriaMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeKpiTargets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MappingGradeRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionCriteriaMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LowScore = table.Column<int>(type: "int", nullable: false),
                    HighScore = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappingGradeRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MappingGradeRanges_AppraisalGradeDefinitions_GradeDefinitionId",
                        column: x => x.GradeDefinitionId,
                        principalTable: "AppraisalGradeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MappingGradeRanges_PositionCriteriaMappings_PositionCriteriaMappingId",
                        column: x => x.PositionCriteriaMappingId,
                        principalTable: "PositionCriteriaMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MappingGradeRanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationContacts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationContacts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationContacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePositionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePositionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePositionHistories_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionHistories_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeePositionHistories_LocationLevels_LocationLevelId",
                        column: x => x.LocationLevelId,
                        principalTable: "LocationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionHistories_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionHistories_OrganizationLevels_OrganizationLevelId",
                        column: x => x.OrganizationLevelId,
                        principalTable: "OrganizationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionHistories_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePositionHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationUnitHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousHeadEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewHeadEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationUnitHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationUnitHistories_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizationUnitHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSalaryAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NotchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignmentReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSalaryAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryAssignments_SalaryGrades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "SalaryGrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryAssignments_SalaryLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "SalaryLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryAssignments_SalaryNotches_NotchId",
                        column: x => x.NotchId,
                        principalTable: "SalaryNotches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeSalaryAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KpiEvaluationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeKpiTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsSelfEvaluation = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AchievementPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceLinks = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiEvaluationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiEvaluationRecords_EmployeeKpiTargets_EmployeeKpiTargetId",
                        column: x => x.EmployeeKpiTargetId,
                        principalTable: "EmployeeKpiTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KpiEvaluationRecords_Employees_EvaluatorId",
                        column: x => x.EvaluatorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KpiEvaluationRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CriterionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluatorEvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumericScore = table.Column<int>(type: "int", nullable: true),
                    KpiEvaluationRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WeightedScore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CriterionScores_AppraisalCriterias_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "AppraisalCriterias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriterionScores_EvaluatorEvaluations_EvaluatorEvaluationId",
                        column: x => x.EvaluatorEvaluationId,
                        principalTable: "EvaluatorEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriterionScores_KpiEvaluationRecords_KpiEvaluationRecordId",
                        column: x => x.KpiEvaluationRecordId,
                        principalTable: "KpiEvaluationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriterionScores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(6933));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(6998));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7002));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7004));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7275));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7282));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7288));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7293));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7300));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7305));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7310));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7315));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7340));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7347));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7357));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7363));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7371));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7376));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7386));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7391));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7441));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7441));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7445));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7447));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7447));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7448));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7448));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7449));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7522));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7524));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7524));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7526));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7526));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7527));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7527));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7530));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7614));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7615));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7616));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7616));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7617));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7617));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7618));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7618));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7619));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7619));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7632));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7633));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(7634));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 17, 14, 17, 16, 379, DateTimeKind.Utc).AddTicks(6676));

            migrationBuilder.CreateIndex(
                name: "IX_WorkStations_ContactPersonId",
                table: "WorkStations",
                column: "ContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Skill_Tenant_Active",
                table: "Skills",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Skill_Tenant_Category",
                table: "Skills",
                columns: new[] { "TenantId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Skill_Tenant_Name",
                table: "Skills",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_EmployeeId_StartDate",
                table: "ShiftAssignments",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkHistories_EndDate",
                table: "EmployeeWorkHistories",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkHistories_StartDate",
                table: "EmployeeWorkHistories",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSkill_Tenant_Employee_Skill",
                table: "EmployeeSkills",
                columns: new[] { "TenantId", "EmployeeId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftPreferences_EmployeeId_ShiftId",
                table: "EmployeeShiftPreferences",
                columns: new[] { "EmployeeId", "ShiftId" });

            migrationBuilder.CreateIndex(
                name: "IX_Employee_Tenant_EmailAddress",
                table: "Employees",
                columns: new[] { "TenantId", "EmailAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employee_Tenant_EmployeeNumber",
                table: "Employees",
                columns: new[] { "TenantId", "EmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DivisionId",
                table: "Employees",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LocationId",
                table: "Employees",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LocationLevelId",
                table: "Employees",
                column: "LocationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_OrganizationLevelId",
                table: "Employees",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_OrganizationUnitId",
                table: "Employees",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UnitId",
                table: "Employees",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_CountryId",
                table: "EmployeeQualifications",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_IsVerified",
                table: "EmployeeQualifications",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_QualificationId",
                table: "EmployeeQualifications",
                column: "QualificationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePosition_ReportsToPositionId",
                table: "EmployeePositions",
                column: "ReportsToPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePosition_StaffLevelId",
                table: "EmployeePositions",
                column: "StaffLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePosition_Tenant_Active",
                table: "EmployeePositions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePosition_Tenant_Code",
                table: "EmployeePositions",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePosition_Tenant_OrgUnit",
                table: "EmployeePositions",
                columns: new[] { "TenantId", "OrganizationUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePosition_Tenant_Title",
                table: "EmployeePositions",
                columns: new[] { "TenantId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_OrganizationLevelId",
                table: "EmployeePositions",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_OrganizationUnitId",
                table: "EmployeePositions",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_SalaryGradeId",
                table: "EmployeePositions",
                column: "SalaryGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_SectionId",
                table: "EmployeePositions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_UnitId",
                table: "EmployeePositions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeIdentificationCards_DocumentNumber",
                table: "EmployeeIdentificationCards",
                column: "DocumentNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeIdentificationCards_IdentificationTypeId",
                table: "EmployeeIdentificationCards",
                column: "IdentificationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeIdentificationCards_IsVerified",
                table: "EmployeeIdentificationCards",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmergencyContacts_CountryId",
                table: "EmployeeEmergencyContacts",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmergencyContacts_EmployeeId_IsPrimary",
                table: "EmployeeEmergencyContacts",
                columns: new[] { "EmployeeId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmergencyContacts_IsActive",
                table: "EmployeeEmergencyContacts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_Relationship",
                table: "EmployeeDependents",
                column: "Relationship");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContractDetail_Tenant_ContractNumber",
                table: "EmployeeContractDetails",
                columns: new[] { "TenantId", "ContractNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContractDetails_IsActive",
                table: "EmployeeContractDetails",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBiometrics_IsActive",
                table: "EmployeeBiometrics",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DivisionId",
                table: "Departments",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_Date",
                table: "AttendanceRecords",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId_Date",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkillRequirement_Tenant_Position_Skill",
                table: "PositionSkillRequirements",
                columns: new[] { "TenantId", "PositionId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkillRequirement_Tenant_PositionId",
                table: "PositionSkillRequirements",
                columns: new[] { "TenantId", "PositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkillRequirement_Tenant_SkillId",
                table: "PositionSkillRequirements",
                columns: new[] { "TenantId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_AppraisalId",
                table: "AppraisalAttachments",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_FileName",
                table: "AppraisalAttachments",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalAttachments_TenantId",
                table: "AppraisalAttachments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_Code",
                table: "AppraisalCriterias",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_CriteriaName",
                table: "AppraisalCriterias",
                column: "CriteriaName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_CriteriaType",
                table: "AppraisalCriterias",
                column: "CriteriaType");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_KpiDefinitionId",
                table: "AppraisalCriterias",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCriterias_TenantId",
                table: "AppraisalCriterias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEmployeeResponses_AppraisalId",
                table: "AppraisalEmployeeResponses",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEmployeeResponses_CriteriaId",
                table: "AppraisalEmployeeResponses",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalEmployeeResponses_TenantId",
                table: "AppraisalEmployeeResponses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGradeDefinitions_GradeName",
                table: "AppraisalGradeDefinitions",
                column: "GradeName");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGradeDefinitions_TenantId",
                table: "AppraisalGradeDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicy_Tenant_Active",
                table: "BenefitPolicies",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicy_Tenant_Code",
                table: "BenefitPolicies",
                columns: new[] { "TenantId", "PolicyCode" },
                unique: true,
                filter: "[PolicyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicy_Tenant_EffectiveFrom",
                table: "BenefitPolicies",
                columns: new[] { "TenantId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicy_Tenant_Name",
                table: "BenefitPolicies",
                columns: new[] { "TenantId", "PolicyName" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicy_Tenant_Type",
                table: "BenefitPolicies",
                columns: new[] { "TenantId", "PolicyType" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicyRelation_Tenant_Policy_RelationType",
                table: "BenefitPolicyRelations",
                columns: new[] { "TenantId", "BenefitPolicyId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicyRelation_Tenant_PolicyId",
                table: "BenefitPolicyRelations",
                columns: new[] { "TenantId", "BenefitPolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicyRelations_BenefitPolicyId",
                table: "BenefitPolicyRelations",
                column: "BenefitPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_CriteriaId",
                table: "CriterionScores",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_EvaluatorEvaluationId",
                table: "CriterionScores",
                column: "EvaluatorEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_KpiEvaluationRecordId",
                table: "CriterionScores",
                column: "KpiEvaluationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CriterionScores_TenantId",
                table: "CriterionScores",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_Code",
                table: "Divisions",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_DivisionHeadId",
                table: "Divisions",
                column: "DivisionHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_IsActive",
                table: "Divisions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_Name",
                table: "Divisions",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_TenantId",
                table: "Divisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContractTypes_TenantId",
                table: "EmployeeContractTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependentBenefits_EmployeeDependentId",
                table: "EmployeeDependentBenefits",
                column: "EmployeeDependentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependentBenefits_EnrolledDate",
                table: "EmployeeDependentBenefits",
                column: "EnrolledDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependentBenefits_IsActive",
                table: "EmployeeDependentBenefits",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependentBenefits_PolicyId",
                table: "EmployeeDependentBenefits",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependentBenefits_TenantId",
                table: "EmployeeDependentBenefits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGuarantors_CountryId",
                table: "EmployeeGuarantors",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGuarantors_EmployeeId",
                table: "EmployeeGuarantors",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGuarantors_IsActive",
                table: "EmployeeGuarantors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGuarantors_IsPrimary",
                table: "EmployeeGuarantors",
                column: "IsPrimary");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGuarantors_IsVerified",
                table: "EmployeeGuarantors",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGuarantors_TenantId",
                table: "EmployeeGuarantors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeGuarantors_VerifiedByEmployeeId",
                table: "EmployeeGuarantors",
                column: "VerifiedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_EmployeeId",
                table: "EmployeeKpiTargets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_EmployeeId_KpiDefinitionId_PeriodStart_PeriodEnd",
                table: "EmployeeKpiTargets",
                columns: new[] { "EmployeeId", "KpiDefinitionId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_KpiDefinitionId",
                table: "EmployeeKpiTargets",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_PositionCriteriaMappingId",
                table: "EmployeeKpiTargets",
                column: "PositionCriteriaMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeKpiTargets_TenantId",
                table: "EmployeeKpiTargets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionBenefit_Tenant_Position_Policy",
                table: "EmployeePositionBenefits",
                columns: new[] { "TenantId", "PositionId", "PolicyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionBenefits_PolicyId",
                table: "EmployeePositionBenefits",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionBenefits_PositionId",
                table: "EmployeePositionBenefits",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_EmployeeId",
                table: "EmployeePositionHistories",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_EmployeeId_StartDate",
                table: "EmployeePositionHistories",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_EndDate",
                table: "EmployeePositionHistories",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_LocationId",
                table: "EmployeePositionHistories",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_LocationLevelId",
                table: "EmployeePositionHistories",
                column: "LocationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_OrganizationLevelId",
                table: "EmployeePositionHistories",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_OrganizationUnitId",
                table: "EmployeePositionHistories",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_PositionId",
                table: "EmployeePositionHistories",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_StartDate",
                table: "EmployeePositionHistories",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_TenantId",
                table: "EmployeePositionHistories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReferees_EmployeeId",
                table: "EmployeeReferees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReferees_EmployeeId_IsPrimary",
                table: "EmployeeReferees",
                columns: new[] { "EmployeeId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReferees_IsActive",
                table: "EmployeeReferees",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReferees_RefereeType",
                table: "EmployeeReferees",
                column: "RefereeType");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReferees_TenantId",
                table: "EmployeeReferees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryAssignments_EffectiveDate",
                table: "EmployeeSalaryAssignments",
                column: "EffectiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryAssignments_EmployeeId",
                table: "EmployeeSalaryAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryAssignments_GradeId",
                table: "EmployeeSalaryAssignments",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryAssignments_LevelId",
                table: "EmployeeSalaryAssignments",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryAssignments_NotchId",
                table: "EmployeeSalaryAssignments",
                column: "NotchId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSalaryAssignments_TenantId",
                table: "EmployeeSalaryAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorEvaluations_AppraisalId",
                table: "EvaluatorEvaluations",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorEvaluations_AppraisalId_EvaluatorId",
                table: "EvaluatorEvaluations",
                columns: new[] { "AppraisalId", "EvaluatorId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorEvaluations_EvaluatorId",
                table: "EvaluatorEvaluations",
                column: "EvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorEvaluations_TenantId",
                table: "EvaluatorEvaluations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpatriateAssignments_EmployeeId",
                table: "ExpatriateAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpatriateAssignments_EmployeeId_StartDate",
                table: "ExpatriateAssignments",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpatriateAssignments_HomeCountryId",
                table: "ExpatriateAssignments",
                column: "HomeCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpatriateAssignments_TenantId",
                table: "ExpatriateAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAssociates_TenantId",
                table: "ExternalAssociates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationType_Tenant_Code",
                table: "IdentificationTypes",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationType_Tenant_Name",
                table: "IdentificationTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationTypes_IsActive",
                table: "IdentificationTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IdentificationTypes_IssuingCountryId",
                table: "IdentificationTypes",
                column: "IssuingCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_KpiName",
                table: "KpiDefinitions",
                column: "KpiName");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_MeasurementType",
                table: "KpiDefinitions",
                column: "MeasurementType");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_TenantId",
                table: "KpiDefinitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_EmployeeKpiTargetId",
                table: "KpiEvaluationRecords",
                column: "EmployeeKpiTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_EvaluationDate",
                table: "KpiEvaluationRecords",
                column: "EvaluationDate");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_EvaluatorId",
                table: "KpiEvaluationRecords",
                column: "EvaluatorId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_IsSelfEvaluation",
                table: "KpiEvaluationRecords",
                column: "IsSelfEvaluation");

            migrationBuilder.CreateIndex(
                name: "IX_KpiEvaluationRecords_TenantId",
                table: "KpiEvaluationRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeId_LeaveTypeId_Year",
                table: "LeaveBalances",
                columns: new[] { "EmployeeId", "LeaveTypeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_LeaveTypeId",
                table: "LeaveBalances",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_TenantId",
                table: "LeaveBalances",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_Year",
                table: "LeaveBalances",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCategoryAllocations_LeaveSubTypeId",
                table: "LeaveCategoryAllocations",
                column: "LeaveSubTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCategoryAllocations_LeaveTypeId",
                table: "LeaveCategoryAllocations",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCategoryAllocations_StaffLevelId",
                table: "LeaveCategoryAllocations",
                column: "StaffLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCategoryAllocations_TenantId",
                table: "LeaveCategoryAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePlans_DepartmentId",
                table: "LeavePlans",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePlans_EmployeeId",
                table: "LeavePlans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePlans_LeaveTypeId",
                table: "LeavePlans",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePlans_PlannedBy",
                table: "LeavePlans",
                column: "PlannedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePlans_RelieverId",
                table: "LeavePlans",
                column: "RelieverId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePlans_TenantId",
                table: "LeavePlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ApprovedByEmployeeId",
                table: "LeaveRequests",
                column: "ApprovedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId",
                table: "LeaveRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_LeavePlanId",
                table: "LeaveRequests",
                column: "LeavePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_LeaveTypeId",
                table: "LeaveRequests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_RelieverEmployeeId",
                table: "LeaveRequests",
                column: "RelieverEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_StartDate_EndDate",
                table: "LeaveRequests",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_Status",
                table: "LeaveRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_TenantId",
                table: "LeaveRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveSubTypes_LeaveTypeId",
                table: "LeaveSubTypes",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveSubTypes_TenantId",
                table: "LeaveSubTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTypes_TenantId",
                table: "LeaveTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationContacts_TenantId",
                table: "LocationContacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LocContact_EmployeeId",
                table: "LocationContacts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LocContact_Location_Primary",
                table: "LocationContacts",
                columns: new[] { "LocationId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_LocContact_LocationId",
                table: "LocationContacts",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationLevels_StructureId",
                table: "LocationLevels",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "IX_LocLevel_Tenant_Structure_Code",
                table: "LocationLevels",
                columns: new[] { "TenantId", "StructureId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocLevel_Tenant_Structure_LevelNum",
                table: "LocationLevels",
                columns: new[] { "TenantId", "StructureId", "LevelNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Location_CountryId",
                table: "Locations",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Location_LevelId",
                table: "Locations",
                column: "LocationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Location_Parent_Sequence",
                table: "Locations",
                columns: new[] { "ParentLocationId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Location_ParentId",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Location_Structure_Active",
                table: "Locations",
                columns: new[] { "StructureId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Location_Tenant_Structure_Code",
                table: "Locations",
                columns: new[] { "TenantId", "StructureId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocStructure_Tenant_Code",
                table: "LocationStructures",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocStructure_Tenant_Default",
                table: "LocationStructures",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_MappingGradeRanges_GradeDefinitionId",
                table: "MappingGradeRanges",
                column: "GradeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingGradeRanges_PositionCriteriaMappingId",
                table: "MappingGradeRanges",
                column: "PositionCriteriaMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingGradeRanges_TenantId",
                table: "MappingGradeRanges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationChartNodes_EmployeeId",
                table: "OrganizationChartNodes",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationChartNodes_ParentNodeId",
                table: "OrganizationChartNodes",
                column: "ParentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationChartNodes_PositionId",
                table: "OrganizationChartNodes",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationChartNodes_TenantId",
                table: "OrganizationChartNodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgLevel_Structure_Active",
                table: "OrganizationLevels",
                columns: new[] { "StructureId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgLevel_Tenant_Structure_Code",
                table: "OrganizationLevels",
                columns: new[] { "TenantId", "StructureId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgLevel_Tenant_Structure_LevelNum",
                table: "OrganizationLevels",
                columns: new[] { "TenantId", "StructureId", "LevelNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgStructure_Tenant_Active",
                table: "OrganizationStructures",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgStructure_Tenant_Code",
                table: "OrganizationStructures",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgStructure_Tenant_Default",
                table: "OrganizationStructures",
                columns: new[] { "TenantId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnitHistory_Tenant_EffectiveFrom",
                table: "OrganizationUnitHistories",
                columns: new[] { "TenantId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnitHistory_Unit_EffectiveFrom",
                table: "OrganizationUnitHistories",
                columns: new[] { "OrganizationUnitId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnitHistory_UnitId",
                table: "OrganizationUnitHistories",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnit_Active",
                table: "OrganizationUnits",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnit_HeadEmployeeId",
                table: "OrganizationUnits",
                column: "HeadEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnit_Level",
                table: "OrganizationUnits",
                column: "OrganizationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnit_Parent_Sequence",
                table: "OrganizationUnits",
                columns: new[] { "ParentUnitId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnit_ParentId",
                table: "OrganizationUnits",
                column: "ParentUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnit_Tenant_Code",
                table: "OrganizationUnits",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_AppraisalNumber",
                table: "PerformanceAppraisals",
                column: "AppraisalNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_AppraisalType",
                table: "PerformanceAppraisals",
                column: "AppraisalType");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_EmployeeId",
                table: "PerformanceAppraisals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_Status",
                table: "PerformanceAppraisals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_TenantId",
                table: "PerformanceAppraisals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAppraisals_Year",
                table: "PerformanceAppraisals",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_AppraisalId",
                table: "PerformanceImprovementPlans",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_EmployeeId",
                table: "PerformanceImprovementPlans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_PipNumber",
                table: "PerformanceImprovementPlans",
                column: "PipNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_Status",
                table: "PerformanceImprovementPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_SupervisorId",
                table: "PerformanceImprovementPlans",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_TenantId",
                table: "PerformanceImprovementPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_ConductedById",
                table: "PipReviewMeetings",
                column: "ConductedById");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_MeetingDate",
                table: "PipReviewMeetings",
                column: "MeetingDate");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_PipId",
                table: "PipReviewMeetings",
                column: "PipId");

            migrationBuilder.CreateIndex(
                name: "IX_PipReviewMeetings_TenantId",
                table: "PipReviewMeetings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_CriteriaId_PositionId",
                table: "PositionCriteriaMappings",
                columns: new[] { "CriteriaId", "PositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_DepartmentId",
                table: "PositionCriteriaMappings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_PositionId",
                table: "PositionCriteriaMappings",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionCriteriaMappings_TenantId",
                table: "PositionCriteriaMappings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_Date",
                table: "PublicHolidays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_TenantId",
                table: "PublicHolidays",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_Year",
                table: "PublicHolidays",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_Qualifications_TenantId",
                table: "Qualifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrade_Tenant_Active",
                table: "SalaryGrades",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrade_Tenant_Code",
                table: "SalaryGrades",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryGrade_Tenant_EffectiveDate",
                table: "SalaryGrades",
                columns: new[] { "TenantId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryLevel_GradeId",
                table: "SalaryLevels",
                column: "SalaryGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryLevel_Tenant_Active",
                table: "SalaryLevels",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryLevel_Tenant_Grade_Code",
                table: "SalaryLevels",
                columns: new[] { "TenantId", "SalaryGradeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryLevel_Tenant_Grade_Sequence",
                table: "SalaryLevels",
                columns: new[] { "TenantId", "SalaryGradeId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryNotch_LevelId",
                table: "SalaryNotches",
                column: "SalaryLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryNotch_Tenant_Active",
                table: "SalaryNotches",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryNotch_Tenant_Level_NotchNumber",
                table: "SalaryNotches",
                columns: new[] { "TenantId", "SalaryLevelId", "NotchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffLevel_Tenant_Active",
                table: "StaffLevels",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffLevel_Tenant_Code",
                table: "StaffLevels",
                columns: new[] { "TenantId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffLevel_Tenant_Name",
                table: "StaffLevels",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffLevel_Tenant_Rank",
                table: "StaffLevels",
                columns: new[] { "TenantId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_Units_Code",
                table: "Units",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Units_IsActive",
                table: "Units",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Units_SectionId",
                table: "Units",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_TenantId",
                table: "Units",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_UnitHeadId",
                table: "Units",
                column: "UnitHeadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Divisions_DivisionId",
                table: "Departments",
                column: "DivisionId",
                principalTable: "Divisions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeEmergencyContacts_Countries_CountryId",
                table: "EmployeeEmergencyContacts",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeIdentificationCards_IdentificationTypes_IdentificationTypeId",
                table: "EmployeeIdentificationCards",
                column: "IdentificationTypeId",
                principalTable: "IdentificationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_EmployeePositions_ReportsToPositionId",
                table: "EmployeePositions",
                column: "ReportsToPositionId",
                principalTable: "EmployeePositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_OrganizationLevels_OrganizationLevelId",
                table: "EmployeePositions",
                column: "OrganizationLevelId",
                principalTable: "OrganizationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_OrganizationUnits_OrganizationUnitId",
                table: "EmployeePositions",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_SalaryGrades_SalaryGradeId",
                table: "EmployeePositions",
                column: "SalaryGradeId",
                principalTable: "SalaryGrades",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_Sections_SectionId",
                table: "EmployeePositions",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_StaffLevels_StaffLevelId",
                table: "EmployeePositions",
                column: "StaffLevelId",
                principalTable: "StaffLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_Units_UnitId",
                table: "EmployeePositions",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeQualifications_Countries_CountryId",
                table: "EmployeeQualifications",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeQualifications_Qualifications_QualificationId",
                table: "EmployeeQualifications",
                column: "QualificationId",
                principalTable: "Qualifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Divisions_DivisionId",
                table: "Employees",
                column: "DivisionId",
                principalTable: "Divisions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_LocationLevels_LocationLevelId",
                table: "Employees",
                column: "LocationLevelId",
                principalTable: "LocationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Locations_LocationId",
                table: "Employees",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_OrganizationLevels_OrganizationLevelId",
                table: "Employees",
                column: "OrganizationLevelId",
                principalTable: "OrganizationLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_OrganizationUnits_OrganizationUnitId",
                table: "Employees",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Units_UnitId",
                table: "Employees",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeShiftPreferences_Shifts_ShiftId",
                table: "EmployeeShiftPreferences",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSkills_Skills_SkillId",
                table: "EmployeeSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirements_EmployeePositions_PositionId",
                table: "PositionSkillRequirements",
                column: "PositionId",
                principalTable: "EmployeePositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirements_Skills_SkillId",
                table: "PositionSkillRequirements",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirements_Tenants_TenantId",
                table: "PositionSkillRequirements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Shifts_ShiftId",
                table: "ShiftAssignments",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkStations_Employees_ContactPersonId",
                table: "WorkStations",
                column: "ContactPersonId",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Divisions_DivisionId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeEmergencyContacts_Countries_CountryId",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeIdentificationCards_IdentificationTypes_IdentificationTypeId",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_EmployeePositions_ReportsToPositionId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_OrganizationLevels_OrganizationLevelId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_OrganizationUnits_OrganizationUnitId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_SalaryGrades_SalaryGradeId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_Sections_SectionId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_StaffLevels_StaffLevelId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_Units_UnitId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeQualifications_Countries_CountryId",
                table: "EmployeeQualifications");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeQualifications_Qualifications_QualificationId",
                table: "EmployeeQualifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Divisions_DivisionId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_LocationLevels_LocationLevelId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Locations_LocationId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_OrganizationLevels_OrganizationLevelId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_OrganizationUnits_OrganizationUnitId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Units_UnitId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeShiftPreferences_Shifts_ShiftId",
                table: "EmployeeShiftPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSkills_Skills_SkillId",
                table: "EmployeeSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirements_EmployeePositions_PositionId",
                table: "PositionSkillRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirements_Skills_SkillId",
                table: "PositionSkillRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirements_Tenants_TenantId",
                table: "PositionSkillRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Shifts_ShiftId",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkStations_Employees_ContactPersonId",
                table: "WorkStations");

            migrationBuilder.DropTable(
                name: "AppraisalAttachments");

            migrationBuilder.DropTable(
                name: "AppraisalEmployeeResponses");

            migrationBuilder.DropTable(
                name: "BenefitPolicyRelations");

            migrationBuilder.DropTable(
                name: "CriterionScores");

            migrationBuilder.DropTable(
                name: "Divisions");

            migrationBuilder.DropTable(
                name: "EmployeeContractTypes");

            migrationBuilder.DropTable(
                name: "EmployeeDependentBenefits");

            migrationBuilder.DropTable(
                name: "EmployeeGuarantors");

            migrationBuilder.DropTable(
                name: "EmployeePositionBenefits");

            migrationBuilder.DropTable(
                name: "EmployeePositionHistories");

            migrationBuilder.DropTable(
                name: "EmployeeReferees");

            migrationBuilder.DropTable(
                name: "EmployeeSalaryAssignments");

            migrationBuilder.DropTable(
                name: "ExpatriateAssignments");

            migrationBuilder.DropTable(
                name: "ExternalAssociates");

            migrationBuilder.DropTable(
                name: "IdentificationTypes");

            migrationBuilder.DropTable(
                name: "LeaveBalances");

            migrationBuilder.DropTable(
                name: "LeaveCategoryAllocations");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "LocationContacts");

            migrationBuilder.DropTable(
                name: "MappingGradeRanges");

            migrationBuilder.DropTable(
                name: "OrganizationChartNodes");

            migrationBuilder.DropTable(
                name: "OrganizationUnitHistories");

            migrationBuilder.DropTable(
                name: "PipReviewMeetings");

            migrationBuilder.DropTable(
                name: "PublicHolidays");

            migrationBuilder.DropTable(
                name: "Qualifications");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "EvaluatorEvaluations");

            migrationBuilder.DropTable(
                name: "KpiEvaluationRecords");

            migrationBuilder.DropTable(
                name: "BenefitPolicies");

            migrationBuilder.DropTable(
                name: "SalaryNotches");

            migrationBuilder.DropTable(
                name: "LeaveSubTypes");

            migrationBuilder.DropTable(
                name: "StaffLevels");

            migrationBuilder.DropTable(
                name: "LeavePlans");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "AppraisalGradeDefinitions");

            migrationBuilder.DropTable(
                name: "OrganizationUnits");

            migrationBuilder.DropTable(
                name: "PerformanceImprovementPlans");

            migrationBuilder.DropTable(
                name: "EmployeeKpiTargets");

            migrationBuilder.DropTable(
                name: "SalaryLevels");

            migrationBuilder.DropTable(
                name: "LeaveTypes");

            migrationBuilder.DropTable(
                name: "LocationLevels");

            migrationBuilder.DropTable(
                name: "OrganizationLevels");

            migrationBuilder.DropTable(
                name: "PerformanceAppraisals");

            migrationBuilder.DropTable(
                name: "PositionCriteriaMappings");

            migrationBuilder.DropTable(
                name: "SalaryGrades");

            migrationBuilder.DropTable(
                name: "LocationStructures");

            migrationBuilder.DropTable(
                name: "OrganizationStructures");

            migrationBuilder.DropTable(
                name: "AppraisalCriterias");

            migrationBuilder.DropTable(
                name: "KpiDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkStations_ContactPersonId",
                table: "WorkStations");

            migrationBuilder.DropIndex(
                name: "IX_Skill_Tenant_Active",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Skill_Tenant_Category",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Skill_Tenant_Name",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_EmployeeId_StartDate",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeWorkHistories_EndDate",
                table: "EmployeeWorkHistories");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeWorkHistories_StartDate",
                table: "EmployeeWorkHistories");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeSkill_Tenant_Employee_Skill",
                table: "EmployeeSkills");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeShiftPreferences_EmployeeId_ShiftId",
                table: "EmployeeShiftPreferences");

            migrationBuilder.DropIndex(
                name: "IX_Employee_Tenant_EmailAddress",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employee_Tenant_EmployeeNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DivisionId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_LocationId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_LocationLevelId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_OrganizationLevelId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_OrganizationUnitId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UnitId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeQualifications_CountryId",
                table: "EmployeeQualifications");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeQualifications_IsVerified",
                table: "EmployeeQualifications");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeQualifications_QualificationId",
                table: "EmployeeQualifications");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePosition_ReportsToPositionId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePosition_StaffLevelId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePosition_Tenant_Active",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePosition_Tenant_Code",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePosition_Tenant_OrgUnit",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePosition_Tenant_Title",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_OrganizationLevelId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_OrganizationUnitId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_SalaryGradeId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_SectionId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_UnitId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeIdentificationCards_DocumentNumber",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeIdentificationCards_IdentificationTypeId",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeIdentificationCards_IsVerified",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeEmergencyContacts_CountryId",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeEmergencyContacts_EmployeeId_IsPrimary",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeEmergencyContacts_IsActive",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDependents_Relationship",
                table: "EmployeeDependents");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeContractDetail_Tenant_ContractNumber",
                table: "EmployeeContractDetails");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeContractDetails_IsActive",
                table: "EmployeeContractDetails");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeBiometrics_IsActive",
                table: "EmployeeBiometrics");

            migrationBuilder.DropIndex(
                name: "IX_Departments_DivisionId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_Date",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_EmployeeId_Date",
                table: "AttendanceRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PositionSkillRequirements",
                table: "PositionSkillRequirements");

            migrationBuilder.DropIndex(
                name: "IX_PositionSkillRequirement_Tenant_Position_Skill",
                table: "PositionSkillRequirements");

            migrationBuilder.DropIndex(
                name: "IX_PositionSkillRequirement_Tenant_PositionId",
                table: "PositionSkillRequirements");

            migrationBuilder.DropIndex(
                name: "IX_PositionSkillRequirement_Tenant_SkillId",
                table: "PositionSkillRequirements");

            migrationBuilder.DropColumn(
                name: "ContactPersonId",
                table: "WorkStations");

            migrationBuilder.DropColumn(
                name: "DigitalAddress",
                table: "WorkStations");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "WorkStations");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "WorkStations");

            migrationBuilder.DropColumn(
                name: "StationType",
                table: "WorkStations");

            migrationBuilder.DropColumn(
                name: "CompanyAddress",
                table: "EmployeeWorkHistories");

            migrationBuilder.DropColumn(
                name: "IsCertified",
                table: "EmployeeSkills");

            migrationBuilder.DropColumn(
                name: "DigitalAddress",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "GrossUp",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsExpatriate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LocationLevelId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "OrganizationLevelId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Overtime",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PayTax",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SSFund",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SocialSecurityNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TINNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TerminationDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TerminationNotes",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Tier2Only",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "EmployeeQualifications");

            migrationBuilder.DropColumn(
                name: "CustomQualificationName",
                table: "EmployeeQualifications");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EmployeeQualifications");

            migrationBuilder.DropColumn(
                name: "QualificationId",
                table: "EmployeeQualifications");

            migrationBuilder.DropColumn(
                name: "ExpectedHeadcount",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "MaximumAge",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "MinimumAge",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "MinimumExperienceYears",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "NumberOfGuarantors",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "OrganizationLevelId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "ReportsToPositionId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "RequiresGuarantor",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "RequiresLicense",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "SalaryGradeId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "StaffLevelId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "WorkMode",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "IdentificationTypeId",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropColumn(
                name: "VerifiedDate",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropColumn(
                name: "City",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "ContactType",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "DigitalAddress",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "DigitalAddress",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "DisabilityDescription",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "GhanaCardNumber",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "HasDisability",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "IsDeceased",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "IsEligibleForBenefits",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "IsEmergencyContact",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "PicturePath",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "RelationshipDescription",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "ConfirmationDate",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "ContractStatus",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "IsPensionApplicable",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "IsTaxExempt",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "ProbationPeriodDays",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "TaxTreatmentType",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "TerminationDate",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "WithholdingTaxRate",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Departments");

            migrationBuilder.RenameTable(
                name: "PositionSkillRequirements",
                newName: "PositionSkillRequirement");

            migrationBuilder.RenameIndex(
                name: "IX_PositionSkillRequirements_SkillId",
                table: "PositionSkillRequirement",
                newName: "IX_PositionSkillRequirement_SkillId");

            migrationBuilder.RenameIndex(
                name: "IX_PositionSkillRequirements_PositionId",
                table: "PositionSkillRequirement",
                newName: "IX_PositionSkillRequirement_PositionId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "WorkStations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<bool>(
                name: "RequiresCertification",
                table: "Skills",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Skills",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificationName",
                table: "EmployeeQualifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "EmployeePositions",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "EmployeeEmergencyContacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "EmployeeDependents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "PayFrequency",
                table: "EmployeeContractDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "PositionSkillRequirement",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRequired",
                table: "PositionSkillRequirement",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PositionSkillRequirement",
                table: "PositionSkillRequirement",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4765));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4841));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4844));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4847));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5231));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5243));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5252));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5261));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5282));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5293));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5301));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5308));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5340));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5353));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5368));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5376));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5390));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5406));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5414));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5423));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5502));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5504));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5505));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5506));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5507));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5509));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5510));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5511));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5511));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5513));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5514));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5515));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5516));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5516));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5517));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5518));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5600));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5603));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5606));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5606));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5607));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5608));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5609));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5610));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5611));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5611));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5612));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5613));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5614));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5681));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5683));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5685));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5686));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5687));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5687));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5688));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5689));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5690));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5691));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5705));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5706));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(5715));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 12, 13, 13, 21, 190, DateTimeKind.Utc).AddTicks(4447));

            migrationBuilder.CreateIndex(
                name: "IX_Skills_TenantId",
                table: "Skills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSkills_TenantId",
                table: "EmployeeSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                table: "Employees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_TenantId",
                table: "EmployeePositions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContractDetails_TenantId",
                table: "EmployeeContractDetails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkillRequirement_TenantId",
                table: "PositionSkillRequirement",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeShiftPreferences_Shifts_ShiftId",
                table: "EmployeeShiftPreferences",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSkills_Skills_SkillId",
                table: "EmployeeSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirement_EmployeePositions_PositionId",
                table: "PositionSkillRequirement",
                column: "PositionId",
                principalTable: "EmployeePositions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirement_Skills_SkillId",
                table: "PositionSkillRequirement",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirement_Tenants_TenantId",
                table: "PositionSkillRequirement",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Shifts_ShiftId",
                table: "ShiftAssignments",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
