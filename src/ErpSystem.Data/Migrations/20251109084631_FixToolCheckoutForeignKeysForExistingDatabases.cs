using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixToolCheckoutForeignKeysForExistingDatabases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old ToolCheckout FKs to Employees table if they exist (for existing databases)
            migrationBuilder.Sql(@"
                IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Employees_CheckedOutById')
                BEGIN
                    ALTER TABLE ToolCheckouts DROP CONSTRAINT FK_ToolCheckouts_Employees_CheckedOutById;
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Employees_CheckedInById')
                BEGIN
                    ALTER TABLE ToolCheckouts DROP CONSTRAINT FK_ToolCheckouts_Employees_CheckedInById;
                END
            ");

            // Add new ToolCheckout FKs to Users table (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Users_CheckedOutById')
                BEGIN
                    ALTER TABLE ToolCheckouts ADD CONSTRAINT FK_ToolCheckouts_Users_CheckedOutById
                    FOREIGN KEY (CheckedOutById) REFERENCES Users(Id) ON DELETE NO ACTION;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Users_CheckedInById')
                BEGIN
                    ALTER TABLE ToolCheckouts ADD CONSTRAINT FK_ToolCheckouts_Users_CheckedInById
                    FOREIGN KEY (CheckedInById) REFERENCES Users(Id) ON DELETE NO ACTION;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new ToolCheckout FKs to Users table if they exist
            migrationBuilder.Sql(@"
                IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Users_CheckedOutById')
                BEGIN
                    ALTER TABLE ToolCheckouts DROP CONSTRAINT FK_ToolCheckouts_Users_CheckedOutById;
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Users_CheckedInById')
                BEGIN
                    ALTER TABLE ToolCheckouts DROP CONSTRAINT FK_ToolCheckouts_Users_CheckedInById;
                END
            ");

            // Re-create old ToolCheckout FKs to Employees table (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Employees_CheckedOutById')
                BEGIN
                    ALTER TABLE ToolCheckouts ADD CONSTRAINT FK_ToolCheckouts_Employees_CheckedOutById
                    FOREIGN KEY (CheckedOutById) REFERENCES Employees(Id) ON DELETE NO ACTION;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_ToolCheckouts_Employees_CheckedInById')
                BEGIN
                    ALTER TABLE ToolCheckouts ADD CONSTRAINT FK_ToolCheckouts_Employees_CheckedInById
                    FOREIGN KEY (CheckedInById) REFERENCES Employees(Id) ON DELETE NO ACTION;
                END
            ");
        }
    }
}
