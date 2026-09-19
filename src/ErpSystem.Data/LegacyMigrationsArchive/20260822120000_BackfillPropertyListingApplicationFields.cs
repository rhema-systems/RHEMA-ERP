using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822120000_BackfillPropertyListingApplicationFields")]
public partial class BackfillPropertyListingApplicationFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @marker nvarchar(100) = N'migration:20260822120000';

            ;WITH ListingCaseFields AS
            (
                SELECT
                    procedureCase.Id AS ProcedureCaseId,
                    procedureCase.TenantId,
                    seed.[Key],
                    seed.Label,
                    seed.FieldType,
                    seed.Value,
                    seed.OptionsJson
                FROM ProcedureCases procedureCase
                OUTER APPLY
                (
                    SELECT TOP (1) asset.*
                    FROM EstateManagedAssets asset
                    WHERE asset.TenantId = procedureCase.TenantId
                      AND asset.IsDeleted = 0
                      AND (
                          CHARINDEX(asset.AssetCode, COALESCE(procedureCase.Description, N'')) > 0
                          OR CHARINDEX(asset.AssetCode, COALESCE(procedureCase.Title, N'')) > 0
                      )
                    ORDER BY LEN(asset.AssetCode) DESC
                ) listing
                CROSS APPLY
                (
                    VALUES
                        (N'applicationReference', N'Property request reference', N'text', procedureCase.ReferenceNumber, NULL),
                        (N'sourceWorkspace', N'Source workspace', N'text', N'External Portal - Property Listings', NULL),
                        (N'sourceReference', N'Customer Business Partner reference', N'text', NULL, NULL),
                        (N'customerAccountReference', N'Customer account reference', N'text', NULL, NULL),
                        (N'customerName', N'Customer / company name', N'text', procedureCase.ApplicantName, NULL),
                        (N'propertyUnit', N'Property / unit', N'text', COALESCE(listing.ProjectUnitCode, listing.AssetCode), NULL),
                        (N'listingReference', N'Listing reference', N'text', listing.AssetCode, NULL),
                        (N'listingType', N'Published listing type', N'text', listing.ExternalListingType, NULL),
                        (N'requestType', N'Request type', N'text', CASE WHEN procedureCase.Title LIKE N'Purchase bid%' THEN N'Purchase bid' ELSE N'Rental request' END, NULL),
                        (N'listingPrice', N'Published price / rent', N'text', CONVERT(nvarchar(100), CASE WHEN procedureCase.Title LIKE N'Purchase bid%' THEN COALESCE(listing.ExternalSalePrice, listing.ExternalListingPrice) ELSE COALESCE(listing.ExternalMonthlyRent, listing.ExternalListingPrice) END), NULL),
                        (N'offerAmount', N'Purchase offer amount', N'text', NULL, NULL),
                        (N'currency', N'Currency', N'text', listing.ExternalListingCurrency, NULL),
                        (N'requestedLeaseTerm', N'Requested lease term', N'text', CASE WHEN listing.ExternalLeaseTermMonths IS NULL THEN NULL ELSE CONCAT(listing.ExternalLeaseTermMonths, N' months') END, NULL),
                        (N'requestMessage', N'Customer message', N'textarea', procedureCase.Description, NULL),
                        (N'customerValidationStatus', N'Customer validation status', N'select', N'Pending', N'["Pending","Validated","Failed"]'),
                        (N'listingValidationStatus', N'Listing validation status', N'select', N'Pending', N'["Pending","Validated","Failed"]'),
                        (N'availabilityCheck', N'Availability check', N'select', N'Pending', N'["Pending","Available","Unavailable"]'),
                        (N'commercialReviewStatus', N'Commercial review status', N'select', N'Pending', N'["Pending","Reviewed","Exception required"]'),
                        (N'decisionStatus', N'Management decision', N'select', N'Pending review', N'["Pending review","Approved","Rejected","More information required"]'),
                        (N'reservationStatus', N'Reservation status', N'select', N'Not reserved', N'["Not reserved","Reserved","Released"]'),
                        (N'customerNotificationStatus', N'Customer notification status', N'text', N'Not notified', NULL),
                        (N'customerAcceptanceStatus', N'Customer acceptance status', N'text', N'Pending', NULL),
                        (N'customerAcceptanceDate', N'Customer acceptance date', N'date', NULL, NULL),
                        (N'agreementTemplateReference', N'Agreement template reference', N'text', NULL, NULL),
                        (N'generatedAgreementReference', N'Generated agreement reference', N'text', NULL, NULL),
                        (N'signedAgreementReference', N'Signed agreement reference', N'text', NULL, NULL),
                        (N'moveInDate', N'Approved move-in date', N'date', NULL, NULL),
                        (N'billingStartDate', N'Billing start date', N'date', NULL, NULL),
                        (N'billingStartStatus', N'Billing start status', N'text', CASE WHEN procedureCase.Title LIKE N'Purchase bid%' THEN N'Not applicable' ELSE N'Blocked - agreement pending' END, NULL),
                        (N'receivedDate', N'Received date', N'date', CASE WHEN procedureCase.ReceivedDate IS NULL THEN NULL ELSE CONVERT(nvarchar(10), CAST(procedureCase.ReceivedDate AS date), 23) END, NULL),
                        (N'applicationStatus', N'Request status', N'select', N'Submitted', N'["Submitted","Under review","Approved","Rejected","Closed"]'),
                        (N'notes', N'Property request notes', N'textarea', procedureCase.Description, NULL)
                ) seed([Key], Label, FieldType, Value, OptionsJson)
                WHERE procedureCase.EntityType = N'EstatePropertyManagementListingApplication'
                  AND procedureCase.IsDeleted = 0
            )
            INSERT INTO ProcedureCaseFields
            (
                Id,
                TenantId,
                ProcedureCaseId,
                [Key],
                Label,
                FieldType,
                Value,
                OptionsJson,
                IsDeleted,
                CreatedAt,
                CreatedBy
            )
            SELECT
                NEWID(),
                source.TenantId,
                source.ProcedureCaseId,
                source.[Key],
                source.Label,
                source.FieldType,
                source.Value,
                source.OptionsJson,
                0,
                SYSUTCDATETIME(),
                @marker
            FROM ListingCaseFields source
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM ProcedureCaseFields existing
                WHERE existing.TenantId = source.TenantId
                  AND existing.ProcedureCaseId = source.ProcedureCaseId
                  AND existing.[Key] = source.[Key]
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM ProcedureCaseFields
            WHERE CreatedBy = N'migration:20260822120000';
            """);
    }
}
