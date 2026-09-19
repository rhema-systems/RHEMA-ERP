using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260915093000_AddProcedureCaseOrganizationScope")]
public sealed class AddProcedureCaseOrganizationScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationLevelId",
            table: "ProcedureCases",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationUnitId",
            table: "ProcedureCases",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProcedureCases_OrganizationLevelId",
            table: "ProcedureCases",
            column: "OrganizationLevelId");

        migrationBuilder.CreateIndex(
            name: "IX_ProcedureCases_OrganizationUnitId",
            table: "ProcedureCases",
            column: "OrganizationUnitId");

        migrationBuilder.CreateIndex(
            name: "IX_ProcedureCases_TenantId_OrganizationUnitId",
            table: "ProcedureCases",
            columns: new[] { "TenantId", "OrganizationUnitId" });

        migrationBuilder.AddForeignKey(
            name: "FK_ProcedureCases_OrganizationLevels_OrganizationLevelId",
            table: "ProcedureCases",
            column: "OrganizationLevelId",
            principalTable: "OrganizationLevels",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ProcedureCases_OrganizationUnits_OrganizationUnitId",
            table: "ProcedureCases",
            column: "OrganizationUnitId",
            principalTable: "OrganizationUnits",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ProcedureCases_OrganizationLevels_OrganizationLevelId",
            table: "ProcedureCases");

        migrationBuilder.DropForeignKey(
            name: "FK_ProcedureCases_OrganizationUnits_OrganizationUnitId",
            table: "ProcedureCases");

        migrationBuilder.DropIndex(
            name: "IX_ProcedureCases_TenantId_OrganizationUnitId",
            table: "ProcedureCases");

        migrationBuilder.DropIndex(
            name: "IX_ProcedureCases_OrganizationUnitId",
            table: "ProcedureCases");

        migrationBuilder.DropIndex(
            name: "IX_ProcedureCases_OrganizationLevelId",
            table: "ProcedureCases");

        migrationBuilder.DropColumn(
            name: "OrganizationUnitId",
            table: "ProcedureCases");

        migrationBuilder.DropColumn(
            name: "OrganizationLevelId",
            table: "ProcedureCases");
    }
}
