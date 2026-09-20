using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 16 slice 8. Rentable company assets and what an employee is charged for holding one —
    /// <b>AST-9</b> and <b>AST-10</b>, decision D2. Three columns on <c>CompanyAssets</c>, seven on
    /// <c>AssetAssignments</c>, and two indexes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The split between the two tables is the design.</b> <c>IsRentable</c> and
    /// <c>StandardRentalAmount</c> belong to the <i>asset</i>: the flat is worth what it is worth
    /// whoever lives in it, and the flag is what lets the register answer "what property do we let
    /// to staff" without inferring it from whatever happens to carry a rent. What one employee
    /// actually pays belongs to the <i>assignment</i>, because it is a term of their tenancy — and
    /// the difference between the two is exactly what makes the arrangement a taxable benefit.
    /// </para>
    /// <para>
    /// <b><c>RentalEffectiveTo</c> is the column that keeps a payslip honest.</b> Three acts end a
    /// custody in this module — a return, a loss or damage report, and a completed transfer — and
    /// all three now close this window. Left open, payroll goes on deducting rent for a house the
    /// employee moved out of, which nobody notices until somebody has been overcharged for months.
    /// </para>
    /// <para>
    /// <b>No deduction lives here.</b> These columns are a declaration payroll reads through
    /// <c>GET Assets/payroll/rental-deductions</c>; the deduction itself is payroll's, which is
    /// another module and read-only to this one. Registered as <b>16.6</b> in
    /// <c>docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md</c> for the sweep after the module.
    /// </para>
    /// <para>
    /// ⚠ <b>Every add is guarded and every drop is conditional</b>, so the migration is safe to
    /// re-run against a database at either state — including one built from the EF model rather than
    /// from the chain, which is how this repo's <c>rebuild-db</c> works. The scaffold was correct as
    /// generated: ten column adds, two indexes, nothing inferred and nothing renamed. (Worth
    /// confirming rather than assuming — EF inferred a wrong <c>RENAME</c> in this area at slice 3.)
    /// </para>
    /// </remarks>
    public partial class AddAssetRentalTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── the asset: is this something we let, and at what rate (AST-9) ──────────────
            //
            // `IsRentable` takes a DEFAULT of 0 rather than being left to the column default,
            // because every asset that existed before this column did was not rentable — that is
            // the truthful description of them, not a placeholder. The same reasoning as
            // `Source` = HrCreated in slice 2b.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IsRentable' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [IsRentable] bit NOT NULL CONSTRAINT [DF_CompanyAssets_IsRentable] DEFAULT (0);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'StandardRentalAmount' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [StandardRentalAmount] decimal(18,2) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RentalCurrencyCode' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    ALTER TABLE [dbo].[CompanyAssets] ADD [RentalCurrencyCode] nvarchar(3) NULL;");

            // ── the tenancy: what THIS employee pays, and for how long (AST-10) ────────────
            //
            // `RentalAmount` is nullable on purpose and zero is not the same as null: zero means the
            // asset is provided free — a stated arrangement, and usually a taxable one — while null
            // means nobody has said. Collapsing the two would lose the difference between "free
            // accommodation" and "not set up yet", which is the difference between a correct payslip
            // and a missing benefit.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RentalAmount' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [RentalAmount] decimal(18,2) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RentalCurrencyCode' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [RentalCurrencyCode] nvarchar(3) NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RentalFrequency' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [RentalFrequency] int NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RentalEffectiveFrom' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [RentalEffectiveFrom] date NULL;");

            // ⚠ The one that keeps a payslip honest. Closed by all three acts that end a custody.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RentalEffectiveTo' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [RentalEffectiveTo] date NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'IsBenefitInKind' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [IsBenefitInKind] bit NOT NULL CONSTRAINT [DF_AssetAssignments_IsBenefitInKind] DEFAULT (0);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'BenefitInKindValue' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    ALTER TABLE [dbo].[AssetAssignments] ADD [BenefitInKindValue] decimal(18,2) NULL;");

            // ── indexes ────────────────────────────────────────────────────────────────────
            //
            // Both are read by the projection payroll pulls, which filters on the rental window and
            // on the rentable flag before anything else.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_IsRentable' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    CREATE INDEX [IX_CompanyAssets_IsRentable] ON [dbo].[CompanyAssets] ([IsRentable]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_RentalEffectiveFrom' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    CREATE INDEX [IX_AssetAssignments_RentalEffectiveFrom] ON [dbo].[AssetAssignments] ([RentalEffectiveFrom]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This discards every rental arrangement the module holds — what each employee was
            // charged, in what currency, over what window, and what any subsidy was worth. Nothing
            // else carries it: payroll consumes the projection and keeps its own deduction rows, so
            // reversing this leaves the deductions in payroll with nothing in HR explaining them.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetAssignments_RentalEffectiveFrom' AND object_id = OBJECT_ID('dbo.AssetAssignments'))
    DROP INDEX [IX_AssetAssignments_RentalEffectiveFrom] ON [dbo].[AssetAssignments];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyAssets_IsRentable' AND object_id = OBJECT_ID('dbo.CompanyAssets'))
    DROP INDEX [IX_CompanyAssets_IsRentable] ON [dbo].[CompanyAssets];");

            // The two defaulted columns carry named constraints, which SQL Server will not drop
            // implicitly with the column.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_AssetAssignments_IsBenefitInKind')
    ALTER TABLE [dbo].[AssetAssignments] DROP CONSTRAINT [DF_AssetAssignments_IsBenefitInKind];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CompanyAssets_IsRentable')
    ALTER TABLE [dbo].[CompanyAssets] DROP CONSTRAINT [DF_CompanyAssets_IsRentable];");

            foreach (var (table, column) in new[]
            {
                ("AssetAssignments", "BenefitInKindValue"),
                ("AssetAssignments", "IsBenefitInKind"),
                ("AssetAssignments", "RentalEffectiveTo"),
                ("AssetAssignments", "RentalEffectiveFrom"),
                ("AssetAssignments", "RentalFrequency"),
                ("AssetAssignments", "RentalCurrencyCode"),
                ("AssetAssignments", "RentalAmount"),
                ("CompanyAssets", "RentalCurrencyCode"),
                ("CompanyAssets", "StandardRentalAmount"),
                ("CompanyAssets", "IsRentable"),
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = '{column}' AND object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];");
            }
        }
    }
}
