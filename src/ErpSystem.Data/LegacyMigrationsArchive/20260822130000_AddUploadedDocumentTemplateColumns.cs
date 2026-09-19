using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822130000_AddUploadedDocumentTemplateColumns")]
public partial class AddUploadedDocumentTemplateColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Compatibility marker only. The identical schema is owned by
        // 20260820120000_AddDmsGenerationTemplateWordSource, whose Up operation is
        // guarded for databases that previously applied this duplicate first.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The earlier schema-owning migration removes these columns when rolling back.
    }
}
