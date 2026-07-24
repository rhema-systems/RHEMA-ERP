using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

public sealed record ProcurementSodRequiredControlDefinition(
    string Code,
    string Name,
    string InitiatorRole,
    string ConflictingRole,
    string EntityType,
    string Action,
    string Explanation,
    string SourceRequirement = "GOV-005",
    string SourceDecisionKey = "DEC-004");

public static class ProcurementSodRequiredControlRegistry
{
    public static IReadOnlyList<ProcurementSodRequiredControlDefinition> Definitions { get; } =
        new ProcurementSodRequiredControlDefinition[]
        {
            new("SOD-INITIATOR-APPROVER", "Transaction initiator versus approver", "Initiator", "Approver",
                "ProcurementTransaction", "Approve",
                "The same user cannot initiate and approve the same procurement transaction."),
            new("SOD-PO-CREATOR-RECEIVER", "Purchase-order creator versus receiver", "PurchaseOrderCreator", "ReceivingOfficer",
                "PurchaseOrder", "ConfirmReceipt",
                "The same user cannot create a purchase order and confirm receipt against it."),
            new("SOD-SUPPLIER-CONTROLLER-AWARD", "Supplier-onboarding controller versus award approver", "SupplierOnboardingController", "AwardApprover",
                "TenderAward", "ApproveAward",
                "The same user cannot control supplier onboarding and approve an award to that supplier."),
            new("SOD-STOCK-ISSUER-ADJUSTMENT", "Stock issuer versus adjustment approver", "StockIssuer", "StockAdjustmentApprover",
                "StockAdjustment", "ApproveAdjustment",
                "The same user cannot issue stock and approve the related stock adjustment."),
            new("SOD-INVOICE-PROCESSOR-PAYMENT", "Invoice processor versus payment approver", "InvoiceProcessor", "PaymentApprover",
                "VendorPayment", "ApprovePayment",
                "The same user cannot process an invoice and approve payment for it."),
            new("SOD-EVALUATOR-AWARD-APPROVER", "Sole evaluator versus award approver", "TenderEvaluator", "AwardApprover",
                "TenderAward", "ApproveAward",
                "A tender evaluator cannot act as the sole award approver for the same sourcing event.")
        };

    public static bool TryGet(string? code, out ProcurementSodRequiredControlDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static bool MatchesRequiredShape(
        ProcurementSodRequiredControlDefinition definition,
        string initiatorRole,
        string conflictingRole,
        string entityType,
        string action,
        ProcurementSodEnforcement enforcement) =>
        enforcement == ProcurementSodEnforcement.HardStop &&
        EqualsNormalized(definition.InitiatorRole, initiatorRole) &&
        EqualsNormalized(definition.ConflictingRole, conflictingRole) &&
        EqualsNormalized(definition.EntityType, entityType) &&
        EqualsNormalized(definition.Action, action);

    private static bool EqualsNormalized(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
