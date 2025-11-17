using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlterOrganizationEmployeeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_EmployeePositions_PositionId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_Skills_SkillId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_Tenants_TenantId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PositionSkillRequirement",
                table: "PositionSkillRequirement");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("32715b1c-6308-45dc-9f6d-448600185404"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("53ff0afe-b958-44aa-ae42-7dcf80863fa9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("625a4223-2641-484b-8f38-34a3108cd02f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("792f5e49-edd7-4058-a615-f114756619e4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("98b86b4b-4c2b-4d3c-99f7-2dea8263a072"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("99901b55-7bdd-4787-a80b-6a5c16b11e0a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("df25c4a7-3a5d-42fc-874f-354023468c0a"));

            migrationBuilder.RenameTable(
                name: "PositionSkillRequirement",
                newName: "PositionSkillRequirements");

            migrationBuilder.RenameIndex(
                name: "IX_PositionSkillRequirement_TenantId",
                table: "PositionSkillRequirements",
                newName: "IX_PositionSkillRequirements_TenantId");

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
                name: "IsExpatriate",
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

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EmployeeQualifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

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
                name: "SectionId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffCategoryId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "EmployeePositions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EmployeeIdentificationCards",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContactType",
                table: "EmployeeEmergencyContacts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DigitalAddress",
                table: "EmployeeEmergencyContacts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DigitalAddress",
                table: "EmployeeDependents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhanaCardNumber",
                table: "EmployeeDependents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

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

            migrationBuilder.AddColumn<int>(
                name: "ContractStatus",
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

            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                table: "Departments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PositionSkillRequirements",
                table: "PositionSkillRequirements",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "BenefitPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyType = table.Column<int>(type: "int", nullable: false),
                    PolicyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Recipient = table.Column<int>(type: "int", nullable: false),
                    RelationType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxDependents = table.Column<int>(type: "int", nullable: true),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    FinancialLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TypeDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "EmployeeDependentBenefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeDependentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrolledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BenefitAmountUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                name: "EmployeePositionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                        name: "FK_EmployeePositionHistories_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                        name: "FK_EmployeePositionHistories_Tenants_TenantId",
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
                        onDelete: ReferentialAction.Cascade);
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
                name: "StaffCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_StaffCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffCategories_Tenants_TenantId",
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeePositionBenefits_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeePositionBenefits_Tenants_TenantId",
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
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6446));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6535));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6538));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6539));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6814));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6832));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6838));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6842));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6849));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6855));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6862));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6866));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6873));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6880));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6885));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6889));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6897));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6925));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6931));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6936));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6993));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6997));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6998));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6998));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6999));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7000));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7001));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7002));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7003));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7004));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7005));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7005));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7006));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7007));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7007));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7008));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7133));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7142));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7143));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7144));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7144));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7145));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7146));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7146));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7147));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7147));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7148));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7149));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7149));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7150));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7150));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7218));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7219));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7220));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7221));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7221));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7222));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7223));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7223));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7224));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7224));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7236));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7237));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(7238));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("08202a3b-c632-44b1-99c0-ca4efc715ee2"), null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6704), null, null, null, null, null, null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6704), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("36e8f602-0c16-42f1-9ded-02cff0fd0baa"), null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6679), null, null, null, null, null, null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6678), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("412c1184-7f22-4693-ae17-365566a38ff1"), null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6627), null, null, null, null, null, null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6622), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("9b739761-eee2-4ef4-9fa0-4480f84500b7"), null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6692), null, null, null, null, null, null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6692), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("bba10262-3b79-4960-8f03-949d6e5df666"), null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6668), null, null, null, null, null, null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6667), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c1c27bf7-3edc-4b0c-b929-74c2f1223978"), null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6642), null, null, null, null, null, null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6642), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e56e2432-dd40-4065-b8c1-725968f23663"), null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6656), null, null, null, null, null, null, new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6655), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 17, 11, 19, 9, 321, DateTimeKind.Utc).AddTicks(6196));

            migrationBuilder.CreateIndex(
                name: "IX_WorkStations_ContactPersonId",
                table: "WorkStations",
                column: "ContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DivisionId",
                table: "Employees",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UnitId",
                table: "Employees",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_ReportsToPositionId",
                table: "EmployeePositions",
                column: "ReportsToPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_SectionId",
                table: "EmployeePositions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_StaffCategoryId",
                table: "EmployeePositions",
                column: "StaffCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_UnitId",
                table: "EmployeePositions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DivisionId",
                table: "Departments",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_BenefitPolicies_TenantId",
                table: "BenefitPolicies",
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
                name: "IX_EmployeeDependentBenefits_TenantId",
                table: "EmployeeDependentBenefits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionBenefits_PolicyId",
                table: "EmployeePositionBenefits",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionBenefits_PositionId",
                table: "EmployeePositionBenefits",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionBenefits_TenantId",
                table: "EmployeePositionBenefits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_DepartmentId",
                table: "EmployeePositionHistories",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_EmployeeId",
                table: "EmployeePositionHistories",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_PositionId",
                table: "EmployeePositionHistories",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositionHistories_TenantId",
                table: "EmployeePositionHistories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpatriateAssignments_EmployeeId",
                table: "ExpatriateAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpatriateAssignments_HomeCountryId",
                table: "ExpatriateAssignments",
                column: "HomeCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpatriateAssignments_TenantId",
                table: "ExpatriateAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCategories_TenantId",
                table: "StaffCategories",
                column: "TenantId");

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
                name: "FK_EmployeePositions_EmployeePositions_ReportsToPositionId",
                table: "EmployeePositions",
                column: "ReportsToPositionId",
                principalTable: "EmployeePositions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_Sections_SectionId",
                table: "EmployeePositions",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_StaffCategories_StaffCategoryId",
                table: "EmployeePositions",
                column: "StaffCategoryId",
                principalTable: "StaffCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePositions_Units_UnitId",
                table: "EmployeePositions",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");

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
                name: "FK_Employees_Units_UnitId",
                table: "Employees",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");

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
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirements_Tenants_TenantId",
                table: "PositionSkillRequirements",
                column: "TenantId",
                principalTable: "Tenants",
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
                name: "FK_EmployeePositions_EmployeePositions_ReportsToPositionId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_Sections_SectionId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_StaffCategories_StaffCategoryId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePositions_Units_UnitId",
                table: "EmployeePositions");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Divisions_DivisionId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Units_UnitId",
                table: "Employees");

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
                name: "FK_WorkStations_Employees_ContactPersonId",
                table: "WorkStations");

            migrationBuilder.DropTable(
                name: "Divisions");

            migrationBuilder.DropTable(
                name: "EmployeeContractTypes");

            migrationBuilder.DropTable(
                name: "EmployeeDependentBenefits");

            migrationBuilder.DropTable(
                name: "EmployeePositionBenefits");

            migrationBuilder.DropTable(
                name: "EmployeePositionHistories");

            migrationBuilder.DropTable(
                name: "ExpatriateAssignments");

            migrationBuilder.DropTable(
                name: "StaffCategories");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "BenefitPolicies");

            migrationBuilder.DropIndex(
                name: "IX_WorkStations_ContactPersonId",
                table: "WorkStations");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DivisionId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UnitId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_ReportsToPositionId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_SectionId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_StaffCategoryId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_EmployeePositions_UnitId",
                table: "EmployeePositions");

            migrationBuilder.DropIndex(
                name: "IX_Departments_DivisionId",
                table: "Departments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PositionSkillRequirements",
                table: "PositionSkillRequirements");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("08202a3b-c632-44b1-99c0-ca4efc715ee2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("36e8f602-0c16-42f1-9ded-02cff0fd0baa"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("412c1184-7f22-4693-ae17-365566a38ff1"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9b739761-eee2-4ef4-9fa0-4480f84500b7"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("bba10262-3b79-4960-8f03-949d6e5df666"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c1c27bf7-3edc-4b0c-b929-74c2f1223978"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e56e2432-dd40-4065-b8c1-725968f23663"));

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
                name: "DigitalAddress",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsExpatriate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "SocialSecurityNumber",
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
                name: "UnitId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Notes",
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
                name: "ReportsToPositionId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "RequiresGuarantor",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "RequiresLicense",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "StaffCategoryId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "EmployeePositions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EmployeeIdentificationCards");

            migrationBuilder.DropColumn(
                name: "ContactType",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "DigitalAddress",
                table: "EmployeeEmergencyContacts");

            migrationBuilder.DropColumn(
                name: "DigitalAddress",
                table: "EmployeeDependents");

            migrationBuilder.DropColumn(
                name: "GhanaCardNumber",
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
                name: "ContractStatus",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "TerminationDate",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "EmployeeContractDetails");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Departments");

            migrationBuilder.RenameTable(
                name: "PositionSkillRequirements",
                newName: "PositionSkillRequirement");

            migrationBuilder.RenameIndex(
                name: "IX_PositionSkillRequirements_TenantId",
                table: "PositionSkillRequirement",
                newName: "IX_PositionSkillRequirement_TenantId");

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

            migrationBuilder.AddPrimaryKey(
                name: "PK_PositionSkillRequirement",
                table: "PositionSkillRequirement",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3342));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3440));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3444));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3447));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3949));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3974));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3983));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3991));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4005));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4014));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4023));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4031));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4044));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4125));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4134));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4141));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4153));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4183));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4214));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4227));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4419));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4427));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4429));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4430));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4432));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4434));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4435));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4445));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4447));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5386));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5389));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5390));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5391));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5392));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5393));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5394));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5395));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5396));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5397));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5398));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5399));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5400));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5401));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5402));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5567));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5569));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5571));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5572));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5573));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5574));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5575));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5576));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5582));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5583));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5603));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5606));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("32715b1c-6308-45dc-9f6d-448600185404"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3736), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3736), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("53ff0afe-b958-44aa-ae42-7dcf80863fa9"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3680), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3679), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("625a4223-2641-484b-8f38-34a3108cd02f"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3698), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3697), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("792f5e49-edd7-4058-a615-f114756619e4"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3660), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3659), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("98b86b4b-4c2b-4d3c-99f7-2dea8263a072"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3634), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3628), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("99901b55-7bdd-4787-a80b-6a5c16b11e0a"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3755), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3755), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("df25c4a7-3a5d-42fc-874f-354023468c0a"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3715), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3715), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(2196));

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
        }
    }
}
