using System.Reflection;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptInspectionRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SaveAndSubmitAllowOptionalCommentsWithoutRemovingOtherRequiredFields(string? comment)
    {
        var save = new SaveProcurementReceiptInspectionRequest
        {
            Comment = comment, IdempotencyKey = "optional-comment-save",
            Lines = [new() { PurchaseOrderReceiptItemId = Guid.NewGuid(), AcceptedQuantity = 1 }]
        };
        var submit = new SubmitProcurementReceiptInspectionRequest
        {
            Comment = comment, RowVersion = "AQID",
            Evidence = [new() { ActionKey = "SubmitReceiptInspection", RequirementKey = "Waybill", EvidenceReference = "WB-1" }]
        };
        Validator.TryValidateObject(save, new ValidationContext(save), [], true).Should().BeTrue();
        Validator.TryValidateObject(submit, new ValidationContext(submit), [], true).Should().BeTrue();
        save.IdempotencyKey = "";
        submit.RowVersion = "";
        Validator.TryValidateObject(save, new ValidationContext(save), [], true).Should().BeFalse();
        Validator.TryValidateObject(submit, new ValidationContext(submit), [], true).Should().BeFalse();
    }

    [Fact]
    public void OptionalCommentsRetainLengthLimitsAndDecisionReasonRequirements()
    {
        var save = new SaveProcurementReceiptInspectionRequest
        {
            Comment = new string('x', 1001), IdempotencyKey = "save",
            Lines = [new() { PurchaseOrderReceiptItemId = Guid.NewGuid(), AcceptedQuantity = 1 }]
        };
        Validator.TryValidateObject(save, new ValidationContext(save), [], true).Should().BeFalse();
        var decision = new DecideProcurementReceiptInspectionRequest { Approved = false, Comment = "", RowVersion = "AQID" };
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(decision, new ValidationContext(decision), errors, true).Should().BeFalse();
        errors.Should().Contain(result => result.MemberNames.Contains(nameof(decision.Comment)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RoutineAuditStillRecordsTheActionWhenTheUserOmitsAComment(string? comment)
    {
        ProcurementReceiptInspectionService.RoutineInspectionActionComment(comment, "Saved (no comment provided).")
            .Should().Be("Saved (no comment provided).");
        ProcurementReceiptInspectionService.RoutineInspectionActionComment("  User note  ", "Saved")
            .Should().Be("User note");
        var original = new SaveProcurementReceiptInspectionRequest { Comment = null, IdempotencyKey = "same-operation" };
        var replay = new SaveProcurementReceiptInspectionRequest { Comment = comment, IdempotencyKey = "same-operation" };
        ProcurementReceiptInspectionService.SaveActionFingerprint(replay)
            .Should().Be(ProcurementReceiptInspectionService.SaveActionFingerprint(original));
    }

    [Fact]
    public void ControlEventEvidenceRetainsRequirementsWithoutDuplicatingOneUpload()
    {
        var uploadId = Guid.NewGuid();
        var evidence = new[]
        {
            new ProcurementReceiptInspectionEvidence
            {
                ReferenceKind = ProcurementReceiptInspectionEvidenceKind.CentralDocumentUpload,
                FileUploadRecordId = uploadId,
                ActionKey = "SubmitReceiptInspection",
                RequirementKey = "Waybill",
                EvidenceReference = "WB-001"
            },
            new ProcurementReceiptInspectionEvidence
            {
                ReferenceKind = ProcurementReceiptInspectionEvidenceKind.CentralDocumentUpload,
                FileUploadRecordId = uploadId,
                ActionKey = "SubmitReceiptInspection",
                RequirementKey = "Delivery note",
                EvidenceReference = "WB-001"
            }
        };

        var result = ProcurementReceiptInspectionService.BuildControlEventEvidence(evidence);

        result.Should().ContainSingle();
        result[0].ReferenceId.Should().Be(uploadId);
        result[0].RequirementKey.Should().Contain("Waybill").And.Contain("Delivery note");
    }

    [Fact]
    public void LegacyDraftWorkflowRebindRemainsNarrowAndGoverned()
    {
        var migration = new INVREQFU004AllowDraftInspectionWorkflowRebind();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        var sql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(item => item.Sql));

        sql.Should().Contain("d.Status = 0");
        sql.Should().Contain("i.Status = 1");
        sql.Should().Contain("TDC0502_RECEIPT_INSPECTION_CASE_ID");
        sql.Should().Contain("TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID");
        sql.Should().Contain("entityType.Code = N''PROCUREMENT_RECEIPT_INSPECTION''");
        sql.Should().Contain("workflow.LifecycleStatus = 1");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Dec013EvidenceRequirementsAcceptPublishedCamelCaseEnumValues()
    {
        const string publishedDecision = """
            {
              "documentType": "grnAndMrn",
              "coexistenceRule": "bothFromSingleReceipt",
              "evidenceRequirements": ["Delivery note", "Delivery note", "  Waybill  "]
            }
            """;

        ProcurementReceiptInspectionService.ParseEvidenceRequirementKeys(publishedDecision)
            .Should().Equal("Delivery note", "Waybill");
    }

    [Theory]
    [InlineData(10, 10, 0, 0, ProcurementReceiptDisposition.Accepted)]
    [InlineData(10, 0, 10, 0, ProcurementReceiptDisposition.Rejected)]
    [InlineData(10, 6, 4, 0, ProcurementReceiptDisposition.PartiallyAccepted)]
    [InlineData(10, 6, 0, 4, ProcurementReceiptDisposition.Pending)]
    public void DispositionIsDerivedFromAuthoritativeQuantities(
        decimal received,
        decimal accepted,
        decimal rejected,
        decimal pending,
        ProcurementReceiptDisposition disposition)
    {
        ProcurementReceiptInspectionRules.Evaluate(received, accepted, rejected)
            .Should().Be((accepted, rejected, pending, disposition));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, -1, 0)]
    [InlineData(10, 0, -1)]
    [InlineData(10, 8, 3)]
    public void InvalidOrOverAcceptedQuantitiesFailClosed(
        decimal received,
        decimal accepted,
        decimal rejected)
    {
        var action = () => ProcurementReceiptInspectionRules.Evaluate(
            received, accepted, rejected);

        action.Should().Throw<ProcurementReceiptInspectionValidationException>()
            .Which.Code.Should().Be("RCV_INSPECTION_QUANTITY_INVALID");
    }

    [Fact]
    public void OnlyDraftCanBeEditedOrSubmitted()
    {
        ProcurementReceiptInspectionRules.CanEdit(
            ProcurementReceiptInspectionStatus.Draft).Should().BeTrue();
        ProcurementReceiptInspectionRules.CanSubmit(
            ProcurementReceiptInspectionStatus.Draft).Should().BeTrue();
        ProcurementReceiptInspectionRules.CanEdit(
            ProcurementReceiptInspectionStatus.Rejected).Should().BeFalse();
        ProcurementReceiptInspectionRules.CanSubmit(
            ProcurementReceiptInspectionStatus.PendingApproval).Should().BeFalse();
    }

    [Theory]
    [InlineData(10, 5, 10, 15, 10)]
    [InlineData(4, 12.5, 6, 7.5, 9.5)]
    [InlineData(0, 0, 8, 3.25, 3.25)]
    public void WarehouseAverageCostUsesExistingAndReceivedValue(
        decimal existingQuantity,
        decimal existingAverageCost,
        decimal receivedQuantity,
        decimal receivedUnitCost,
        decimal expected)
    {
        ProcurementReceiptInspectionRules.CalculateWeightedAverageCost(
                existingQuantity,
                existingAverageCost,
                receivedQuantity,
                receivedUnitCost)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptInspectionStatus.QualityHold, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.ReturnPending, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.PendingApproval, 0, 4, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Rejected, 0, 4, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 1, 4, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 0, false)]
    public void ApEligibilityUsesOnlyGovernedAcceptedQuantity(
        ProcurementReceiptInspectionStatus status,
        decimal pending,
        decimal eligible,
        bool expected)
    {
        ProcurementReceiptInspectionRules.IsApEligible(status, pending, eligible)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 0, true)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.QualityHold, 0, 0, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Rejected, 0, 0, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 1, 0, false)]
    public void ApMatchingTreatsOnlyClosedZeroEligibleRejectionsAsResolved(
        ProcurementReceiptInspectionStatus status,
        decimal pending,
        decimal eligible,
        bool expected)
    {
        ProcurementReceiptInspectionRules.IsApMatchingResolved(status, pending, eligible)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, ProcurementReceiptResolutionStatus.Dispatched, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, ProcurementReceiptResolutionStatus.ReplacementReceived, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Pending, ProcurementReceiptResolutionStatus.Dispatched, false)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, ProcurementReceiptResolutionStatus.Authorized, false)]
    public void ClosureRequiresSupplierAcknowledgementAndCompletedResolution(
        ProcurementReceiptSupplierAcknowledgementStatus acknowledgement,
        ProcurementReceiptResolutionStatus resolution,
        bool expected)
    {
        ProcurementReceiptInspectionRules.CanClose(acknowledgement, resolution)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Pending, true, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Pending, false, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Disputed, true, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Disputed, false, false)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, true, false)]
    public void SupplierCanAcknowledgeAPreviouslyDisputedRejectionNote(
        ProcurementReceiptSupplierAcknowledgementStatus status,
        bool acknowledged,
        bool expected)
    {
        ProcurementReceiptInspectionRules.CanSupplierRespond(status, acknowledged)
            .Should().Be(expected);
    }
}
