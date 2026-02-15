using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Data migration to fix WorkOrders with Guid.Empty RequestedById values
    /// </summary>
    public partial class FixWorkOrderRequestedByIdData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update any WorkOrders that have Guid.Empty as RequestedById to NULL
            migrationBuilder.Sql(@"
                UPDATE [dbo].[WorkOrders]
                SET RequestedById = NULL
                WHERE RequestedById = '00000000-0000-0000-0000-000000000000';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No down migration needed - we can't restore invalid data
        }
    }
}
