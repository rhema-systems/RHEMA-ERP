using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLandAcquisitionMapAndProjectHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentDate",
                table: "StatutoryConsents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "StampDutyPayments",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssessmentAuthority",
                table: "StampDutyAssessments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateGapReason",
                table: "OwnershipHistories",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationNumber",
                table: "OwnershipHistories",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationType",
                table: "OwnershipHistories",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentOwner",
                table: "OwnershipHistories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipEndDate",
                table: "OwnershipHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OwnershipPercentage",
                table: "OwnershipHistories",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipStartDate",
                table: "OwnershipHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenureType",
                table: "OwnershipHistories",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessAddress1",
                table: "OwnershipHistories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessAddress2",
                table: "OwnershipHistories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessContact1",
                table: "OwnershipHistories",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessContact2",
                table: "OwnershipHistories",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessName1",
                table: "OwnershipHistories",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessName2",
                table: "OwnershipHistories",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessOathSwornBefore1",
                table: "OwnershipHistories",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessOathSwornBefore2",
                table: "OwnershipHistories",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WitnessOathSwornDate1",
                table: "OwnershipHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WitnessOathSwornDate2",
                table: "OwnershipHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessRelationship1",
                table: "OwnershipHistories",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessRelationship2",
                table: "OwnershipHistories",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WitnessSwornOath1",
                table: "OwnershipHistories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WitnessSwornOath2",
                table: "OwnershipHistories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AgreementDay",
                table: "NegotiationOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AgreementGenerated",
                table: "NegotiationOffers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AgreementMonth",
                table: "NegotiationOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementYear",
                table: "NegotiationOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "NegotiationOffers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SellerQuote",
                table: "NegotiationOffers",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFloodProne",
                table: "LandPhysicalAssessments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SoilType",
                table: "LandPhysicalAssessments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topography",
                table: "LandPhysicalAssessments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterpartySignatory",
                table: "LandInstruments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedBy",
                table: "LandInstruments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetNumber",
                table: "LandAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "LandAssets",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "LandAssets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnershipVerification",
                table: "LandAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParcelIdentifier",
                table: "LandAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "LandAssets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "LandAssets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Size",
                table: "LandAssets",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeUnit",
                table: "LandAssets",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LandAssets",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZoningClassification",
                table: "LandAssets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFamilyLand",
                table: "LandAgreements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStoolLand",
                table: "LandAgreements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PartyDetails",
                table: "LandAgreements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentSchedule",
                table: "LandAgreements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RootOfTitle",
                table: "LandAgreements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialConditions",
                table: "LandAgreements",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WitnessDetails",
                table: "LandAgreements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AreaSize",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaUnit",
                table: "CadastralSurveys",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoundaryCoordinates",
                table: "CadastralSurveys",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CadastralSurveys",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MainPortion",
                table: "CadastralSurveys",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MapSheetNumber",
                table: "CadastralSurveys",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NorthEastLat",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NorthEastLng",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NorthWestLat",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NorthWestLng",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegionalSurveyorName",
                table: "CadastralSurveys",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegionalSurveyorSignedDate",
                table: "CadastralSurveys",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SouthEastLat",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SouthEastLng",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SouthWestLat",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SouthWestLng",
                table: "CadastralSurveys",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SurveyorSignedDate",
                table: "CadastralSurveys",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentDate",
                table: "StatutoryConsents");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "StampDutyPayments");

            migrationBuilder.DropColumn(
                name: "AssessmentAuthority",
                table: "StampDutyAssessments");

            migrationBuilder.DropColumn(
                name: "DateGapReason",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "IdentificationNumber",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "IdentificationType",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "IsCurrentOwner",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "OwnershipEndDate",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "OwnershipPercentage",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "OwnershipStartDate",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "TenureType",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessAddress1",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessAddress2",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessContact1",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessContact2",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessName1",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessName2",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessOathSwornBefore1",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessOathSwornBefore2",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessOathSwornDate1",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessOathSwornDate2",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessRelationship1",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessRelationship2",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessSwornOath1",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "WitnessSwornOath2",
                table: "OwnershipHistories");

            migrationBuilder.DropColumn(
                name: "AgreementDay",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "AgreementGenerated",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "AgreementMonth",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "AgreementYear",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "SellerQuote",
                table: "NegotiationOffers");

            migrationBuilder.DropColumn(
                name: "IsFloodProne",
                table: "LandPhysicalAssessments");

            migrationBuilder.DropColumn(
                name: "SoilType",
                table: "LandPhysicalAssessments");

            migrationBuilder.DropColumn(
                name: "Topography",
                table: "LandPhysicalAssessments");

            migrationBuilder.DropColumn(
                name: "CounterpartySignatory",
                table: "LandInstruments");

            migrationBuilder.DropColumn(
                name: "ExecutedBy",
                table: "LandInstruments");

            migrationBuilder.DropColumn(
                name: "AssetNumber",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "OwnershipVerification",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "ParcelIdentifier",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "SizeUnit",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "ZoningClassification",
                table: "LandAssets");

            migrationBuilder.DropColumn(
                name: "IsFamilyLand",
                table: "LandAgreements");

            migrationBuilder.DropColumn(
                name: "IsStoolLand",
                table: "LandAgreements");

            migrationBuilder.DropColumn(
                name: "PartyDetails",
                table: "LandAgreements");

            migrationBuilder.DropColumn(
                name: "PaymentSchedule",
                table: "LandAgreements");

            migrationBuilder.DropColumn(
                name: "RootOfTitle",
                table: "LandAgreements");

            migrationBuilder.DropColumn(
                name: "SpecialConditions",
                table: "LandAgreements");

            migrationBuilder.DropColumn(
                name: "WitnessDetails",
                table: "LandAgreements");

            migrationBuilder.DropColumn(
                name: "AreaSize",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "AreaUnit",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "BoundaryCoordinates",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "MainPortion",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "MapSheetNumber",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "NorthEastLat",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "NorthEastLng",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "NorthWestLat",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "NorthWestLng",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "RegionalSurveyorName",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "RegionalSurveyorSignedDate",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "SouthEastLat",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "SouthEastLng",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "SouthWestLat",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "SouthWestLng",
                table: "CadastralSurveys");

            migrationBuilder.DropColumn(
                name: "SurveyorSignedDate",
                table: "CadastralSurveys");
        }
    }
}
