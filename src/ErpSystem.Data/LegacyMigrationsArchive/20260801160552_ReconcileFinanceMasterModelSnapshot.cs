using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Reconciles the model snapshot after merging the independently migrated
/// finance-hardening and master lines. The executable schema changes are
/// already carried by their original migrations, so this migration is
/// intentionally metadata-only.
/// </summary>
public partial class ReconcileFinanceMasterModelSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
