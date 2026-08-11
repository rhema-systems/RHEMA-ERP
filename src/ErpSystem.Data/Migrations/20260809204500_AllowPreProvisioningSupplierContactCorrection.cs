using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260809204500_AllowPreProvisioningSupplierContactCorrection")]
public partial class AllowPreProvisioningSupplierContactCorrection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(BuildCorrectedTriggerSql());
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            AllowControlledSupplierApplicantContactCorrection.ControlledTriggerSql);
    }

    private static string BuildCorrectedTriggerSql()
    {
        const string originalGatePattern =
            @"AND d\.BusinessPartnerId IS NOT NULL\s+" +
            @"AND i\.BusinessPartnerId = d\.BusinessPartnerId";
        const string preProvisioningGate =
            """
            AND ISNULL(i.BusinessPartnerId,
                               '00000000-0000-0000-0000-000000000000') =
                               ISNULL(d.BusinessPartnerId,
                                   '00000000-0000-0000-0000-000000000000')
            """;
        const string originalRegistrationLineagePattern =
            @"AND registration\.Status = 'Approved'\s+" +
            @"AND registration\.BusinessPartnerId =\s+" +
            @"i\.BusinessPartnerId\)\)\)\)";
        const string preProvisioningRegistrationLineage =
            """
            AND registration.Status = 'Approved'
            AND registration.BusinessPartnerId IS NOT NULL
            AND (i.BusinessPartnerId IS NULL
                 OR registration.BusinessPartnerId = i.BusinessPartnerId)))))
            """;

        var original =
            AllowControlledSupplierApplicantContactCorrection.ControlledTriggerSql;
        var subjectGate = new Regex(
            originalGatePattern,
            RegexOptions.CultureInvariant);
        var registrationGate = new Regex(
            originalRegistrationLineagePattern,
            RegexOptions.CultureInvariant);
        if (subjectGate.Matches(original).Count != 1 ||
            registrationGate.Matches(original).Count != 1)
        {
            throw new InvalidOperationException(
                "The supplier contact-correction trigger template did not match its governed replacement anchors.");
        }

        var corrected = subjectGate.Replace(original, preProvisioningGate, 1);
        corrected = registrationGate.Replace(
            corrected,
            preProvisioningRegistrationLineage,
            1);
        return corrected;
    }
}
