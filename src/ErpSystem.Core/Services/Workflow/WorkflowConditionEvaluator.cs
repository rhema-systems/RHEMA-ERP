using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Condition evaluator for workflow expressions and variables
/// </summary>
public class WorkflowConditionEvaluator : IWorkflowConditionEvaluator
{
    private readonly ILogger<WorkflowConditionEvaluator> _logger;

    public WorkflowConditionEvaluator(ILogger<WorkflowConditionEvaluator> logger)
    {
        _logger = logger;
    }

    public Task<bool> EvaluateConditionAsync(string conditionExpression, object? dataContext)
    {
        if (string.IsNullOrWhiteSpace(conditionExpression))
        {
            return Task.FromResult(true);
        }

        try
        {
            var context = BuildContext(dataContext);
            var result = EvaluateLogicalExpression(conditionExpression, context);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate workflow condition: {Expression}", conditionExpression);
            return Task.FromResult(false);
        }
    }

    public bool ValidateConditionSyntax(string conditionExpression)
    {
        if (string.IsNullOrWhiteSpace(conditionExpression))
        {
            return true;
        }

        try
        {
            var context = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            _ = EvaluateLogicalExpression(conditionExpression, context);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<IEnumerable<WorkflowVariableInfo>> GetAvailableVariablesAsync(string entityType)
    {
        var variables = new List<WorkflowVariableInfo>
        {
            new()
            {
                Name = "entityId",
                DisplayName = "Entity ID",
                DataType = typeof(Guid),
                Description = "The ID of the entity being processed"
            },
            new()
            {
                Name = "initiatedById",
                DisplayName = "Initiated By",
                DataType = typeof(Guid),
                Description = "The ID of the user who initiated the workflow"
            }
        };

        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Task.FromResult<IEnumerable<WorkflowVariableInfo>>(variables);
        }

        var normalized = entityType.Trim();
        if (normalized.Equals("WorkOrder", StringComparison.OrdinalIgnoreCase))
        {
            variables.AddRange(new[]
            {
                new WorkflowVariableInfo { Name = "workOrderId", DisplayName = "Work Order ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "workOrderNumber", DisplayName = "Work Order Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "assetId", DisplayName = "Asset ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "assetCriticality", DisplayName = "Asset Criticality", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "maintenanceType", DisplayName = "Maintenance Type", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "priority", DisplayName = "Priority", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "priorityLevel", DisplayName = "Priority Level", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "estimatedHours", DisplayName = "Estimated Hours", DataType = typeof(double) },
                new WorkflowVariableInfo { Name = "estimatedCost", DisplayName = "Estimated Cost", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "assignedTechnicianId", DisplayName = "Assigned Technician ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "requesterEmployeeId", DisplayName = "Requester Employee ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "createdDate", DisplayName = "Created Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "scheduledDate", DisplayName = "Scheduled Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "isEmergency", DisplayName = "Is Emergency", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "requiresSafetyInspection", DisplayName = "Requires Safety Inspection", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "requiresQualityControl", DisplayName = "Requires Quality Control", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "requiresManagerApproval", DisplayName = "Requires Manager Approval", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "partsRequired", DisplayName = "Parts Required", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "partsCount", DisplayName = "Parts Count", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "totalPartsValue", DisplayName = "Total Parts Value", DataType = typeof(decimal) }
            });
        }

        if (normalized.Equals("JobCard", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Job Card", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("JOB_CARD", StringComparison.OrdinalIgnoreCase))
        {
            variables.AddRange(new[]
            {
                new WorkflowVariableInfo { Name = "jobCardNumber", DisplayName = "Job Card Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "jobCardStatus", DisplayName = "Job Card Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "approvalStatus", DisplayName = "Approval Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "estimatedCost", DisplayName = "Estimated Cost", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "estimatedHours", DisplayName = "Estimated Hours", DataType = typeof(double) },
                new WorkflowVariableInfo { Name = "priorityLevelId", DisplayName = "Priority Level ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "maintenanceTypeId", DisplayName = "Maintenance Type ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "requiresShutdown", DisplayName = "Requires Shutdown", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "requiresSafetyPermit", DisplayName = "Requires Safety Permit", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "requiresSpecialTools", DisplayName = "Requires Special Tools", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "requestedById", DisplayName = "Requested By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "requestedDate", DisplayName = "Requested Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "requiredCompletionDate", DisplayName = "Required Completion Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "submittedById", DisplayName = "Submitted By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "submittedDate", DisplayName = "Submitted Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "approvedById", DisplayName = "Approved By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "approvedDate", DisplayName = "Approved Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "approvalComments", DisplayName = "Approval Comments", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "assetId", DisplayName = "Asset ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "maintenanceLocation", DisplayName = "Maintenance Location", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "title", DisplayName = "Job Card Title", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "description", DisplayName = "Description", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "problemDescription", DisplayName = "Problem Description", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "preferredTechnicianId", DisplayName = "Preferred Technician ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "preferredTeamId", DisplayName = "Preferred Team ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "contractorId", DisplayName = "Contractor ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "specialInstructions", DisplayName = "Special Instructions", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "safetyRequirements", DisplayName = "Safety Requirements", DataType = typeof(string) }
            });
        }

        if (normalized.Equals("PurchaseOrder", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Purchase Order", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PURCHASE_ORDER", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PO", StringComparison.OrdinalIgnoreCase))
        {
            variables.AddRange(new[]
            {
                new WorkflowVariableInfo { Name = "purchaseOrderNumber", DisplayName = "Purchase Order Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "orderDate", DisplayName = "Order Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "requiredDate", DisplayName = "Required Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "promisedDate", DisplayName = "Promised Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "receivedDate", DisplayName = "Received Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "status", DisplayName = "Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "requestedById", DisplayName = "Requested By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "approvedById", DisplayName = "Approved By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "approvedAt", DisplayName = "Approved At", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "businessPartnerId", DisplayName = "Supplier ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "deliveryWarehouseId", DisplayName = "Delivery Warehouse ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "deliveryAddress", DisplayName = "Delivery Address", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "deliveryInstructions", DisplayName = "Delivery Instructions", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "subTotal", DisplayName = "Subtotal", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "taxAmount", DisplayName = "Tax Amount", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "shippingCost", DisplayName = "Shipping Cost", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "discountAmount", DisplayName = "Discount Amount", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "totalAmount", DisplayName = "Total Amount", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "paymentTerms", DisplayName = "Payment Terms", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "shippingTerms", DisplayName = "Shipping Terms", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "terms", DisplayName = "Terms", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "notes", DisplayName = "Notes", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "businessPartnerOrderNumber", DisplayName = "Supplier Order Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "referenceNumber", DisplayName = "Reference Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "orderType", DisplayName = "Order Type", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "currency", DisplayName = "Currency", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "exchangeRate", DisplayName = "Exchange Rate", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "contractStartDate", DisplayName = "Contract Start Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "contractEndDate", DisplayName = "Contract End Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "contractValue", DisplayName = "Contract Value", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "contractUsedValue", DisplayName = "Contract Used Value", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "contractRemainingValue", DisplayName = "Contract Remaining Value", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "sourceRequisitionId", DisplayName = "Source Requisition ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "sourceRequisitionNumber", DisplayName = "Source Requisition Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "tenderAwardId", DisplayName = "Tender Award ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "tenderNumber", DisplayName = "Tender Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "contractId", DisplayName = "Contract ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "contractNumber", DisplayName = "Contract Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "budgetId", DisplayName = "Budget ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "itemsCount", DisplayName = "Items Count", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "totalOrderedQuantity", DisplayName = "Total Ordered Quantity", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "totalReceivedQuantity", DisplayName = "Total Received Quantity", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "totalRemainingQuantity", DisplayName = "Total Remaining Quantity", DataType = typeof(decimal) }
            });
        }

        if (normalized.Equals("PurchaseRequisition", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Purchase Requisition", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PURCHASE_REQUISITION", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("PR", StringComparison.OrdinalIgnoreCase))
        {
            variables.AddRange(new[]
            {
                new WorkflowVariableInfo { Name = "requisitionNumber", DisplayName = "Requisition Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "requisitionDate", DisplayName = "Requisition Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "requiredDate", DisplayName = "Required Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "status", DisplayName = "Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "priority", DisplayName = "Priority", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "requestedById", DisplayName = "Requested By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "department", DisplayName = "Department", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "costCenter", DisplayName = "Cost Center", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "justification", DisplayName = "Justification", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "notes", DisplayName = "Notes", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "requisitionType", DisplayName = "Requisition Type", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "budgetId", DisplayName = "Budget ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "budgetCode", DisplayName = "Budget Code", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "budgetAllocated", DisplayName = "Budget Allocated", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "budgetRemaining", DisplayName = "Budget Remaining", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "budgetValidated", DisplayName = "Budget Validated", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "projectId", DisplayName = "Project ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "projectCode", DisplayName = "Project Code", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "projectName", DisplayName = "Project Name", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "deliveryWarehouseId", DisplayName = "Delivery Warehouse ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "deliveryAddress", DisplayName = "Delivery Address", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "deliveryInstructions", DisplayName = "Delivery Instructions", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "isAutoGenerated", DisplayName = "Is Auto Generated", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "generatedFrom", DisplayName = "Generated From", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "sourcePlanId", DisplayName = "Source Plan ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "approvalLevel", DisplayName = "Approval Level", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "requiredApprovalLevel", DisplayName = "Required Approval Level", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "currentApproverId", DisplayName = "Current Approver ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "approvalHistory", DisplayName = "Approval History", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "revisionNumber", DisplayName = "Revision Number", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "lastAmendedAt", DisplayName = "Last Amended At", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "lastAmendedById", DisplayName = "Last Amended By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "amendmentNotes", DisplayName = "Amendment Notes", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "currency", DisplayName = "Currency", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "preferredBusinessPartnerId", DisplayName = "Preferred Business Partner ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "approvedById", DisplayName = "Approved By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "approvedAt", DisplayName = "Approved At", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "rejectionReason", DisplayName = "Rejection Reason", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "totalAmount", DisplayName = "Total Amount", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "itemsCount", DisplayName = "Items Count", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "totalQuantity", DisplayName = "Total Quantity", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "totalEstimatedValue", DisplayName = "Total Estimated Value", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "pendingItems", DisplayName = "Pending Items", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "orderedItems", DisplayName = "Ordered Items", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "receivedItems", DisplayName = "Received Items", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "cancelledItems", DisplayName = "Cancelled Items", DataType = typeof(int) }
            });
        }

        if (normalized.Equals("Tender", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ProcurementTender", StringComparison.OrdinalIgnoreCase))
        {
            variables.AddRange(new[]
            {
                new WorkflowVariableInfo { Name = "tenderId", DisplayName = "Tender ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "tenderNumber", DisplayName = "Tender Number", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "title", DisplayName = "Title", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "tenderType", DisplayName = "Tender Type", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "status", DisplayName = "Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "estimatedValue", DisplayName = "Estimated Value", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "currency", DisplayName = "Currency", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "submissionDeadline", DisplayName = "Submission Deadline", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "openingDate", DisplayName = "Opening Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "requiresPrequalification", DisplayName = "Requires Prequalification", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "allowPartialBids", DisplayName = "Allow Partial Bids", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "useQCBSEvaluation", DisplayName = "Use QCBS Evaluation", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "technicalWeight", DisplayName = "Technical Weight (%)", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "financialWeight", DisplayName = "Financial Weight (%)", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "createdById", DisplayName = "Created By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "createdAt", DisplayName = "Created At", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "bidCount", DisplayName = "Bid Count", DataType = typeof(int) },
                new WorkflowVariableInfo { Name = "invitationCount", DisplayName = "Invitation Count", DataType = typeof(int) }
            });
        }

        if (normalized.Equals("Project", StringComparison.OrdinalIgnoreCase))
        {
            variables.AddRange(new[]
            {
                new WorkflowVariableInfo { Name = "projectId", DisplayName = "Project ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "projectCode", DisplayName = "Project Code", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "title", DisplayName = "Title", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "status", DisplayName = "Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "projectTypeId", DisplayName = "Project Type ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "projectPriorityId", DisplayName = "Project Priority ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "projectManagerId", DisplayName = "Project Manager ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "sponsorId", DisplayName = "Sponsor ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "startDate", DisplayName = "Start Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "targetEndDate", DisplayName = "Target End Date", DataType = typeof(DateTime) },
                new WorkflowVariableInfo { Name = "estimatedBudget", DisplayName = "Estimated Budget", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "approvedBudget", DisplayName = "Approved Budget", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "actualCost", DisplayName = "Actual Cost", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "progressPercent", DisplayName = "Progress Percent", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "approvalRequired", DisplayName = "Approval Required", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "methodology", DisplayName = "Methodology", DataType = typeof(string) }
            });
        }

        if (normalized.Equals("BusinessPartner", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Business Partner", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Supplier", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Contractor", StringComparison.OrdinalIgnoreCase))
        {
            variables.AddRange(new[]
            {
                new WorkflowVariableInfo { Name = "businessPartnerId", DisplayName = "Business Partner ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "partnerCode", DisplayName = "Partner Code", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "partnerName", DisplayName = "Partner Name", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "partnerType", DisplayName = "Partner Type", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "registrationStatus", DisplayName = "Registration Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "approvalStatus", DisplayName = "Approval Status", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "isActive", DisplayName = "Is Active", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "isPreferred", DisplayName = "Is Preferred", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "isBlacklisted", DisplayName = "Is Blacklisted", DataType = typeof(bool) },
                new WorkflowVariableInfo { Name = "performanceRating", DisplayName = "Performance Rating", DataType = typeof(decimal) },
                new WorkflowVariableInfo { Name = "riskLevel", DisplayName = "Risk Level", DataType = typeof(string) },
                new WorkflowVariableInfo { Name = "createdById", DisplayName = "Created By ID", DataType = typeof(Guid) },
                new WorkflowVariableInfo { Name = "createdAt", DisplayName = "Created At", DataType = typeof(DateTime) }
            });
        }

        return Task.FromResult<IEnumerable<WorkflowVariableInfo>>(variables);
    }

    private static Dictionary<string, object?> BuildContext(object? dataContext)
    {
        if (dataContext == null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (dataContext is Dictionary<string, object?> dictionary)
        {
            return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        if (dataContext is IDictionary<string, object> objectDictionary)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in objectDictionary)
            {
                result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        if (dataContext is JsonElement element)
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(element.GetRawText());
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                {
                    result[kvp.Key] = ConvertJsonElement(kvp.Value);
                }
            }
            return result;
        }

        var json = JsonSerializer.Serialize(dataContext);
        var jsonDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (jsonDict != null)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in jsonDict)
            {
                result[kvp.Key] = ConvertJsonElement(kvp.Value);
            }
            return result;
        }

        var reflectionDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in dataContext.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            reflectionDict[prop.Name] = prop.GetValue(dataContext);
        }
        return reflectionDict;
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetDecimal(out var dec) ? dec : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => null
        };
    }

    private static bool EvaluateLogicalExpression(string expression, Dictionary<string, object?> context)
    {
        var orParts = SplitByOperator(expression, "||");
        foreach (var part in orParts)
        {
            var andParts = SplitByOperator(part, "&&");
            var andResult = true;
            foreach (var andPart in andParts)
            {
                if (!EvaluateComparison(andPart, context))
                {
                    andResult = false;
                    break;
                }
            }

            if (andResult)
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateComparison(string rawExpression, Dictionary<string, object?> context)
    {
        var expression = rawExpression.Trim();
        if (string.IsNullOrEmpty(expression))
        {
            return true;
        }

        var operators = new[]
        {
            ">=",
            "<=",
            "!=",
            "==",
            ">",
            "<"
        };

        foreach (var op in operators)
        {
            var index = IndexOfOperator(expression, op);
            if (index > -1)
            {
                var left = expression[..index].Trim();
                var right = expression[(index + op.Length)..].Trim();

                var leftValue = ResolveOperand(left, context);
                var rightValue = ResolveOperand(right, context);

                return Compare(leftValue, rightValue, op);
            }
        }

        if (TryMatchFunction(expression, "contains", out var funcLeft, out var funcRight))
        {
            var leftValue = ResolveOperand(funcLeft, context);
            var rightValue = ResolveOperand(funcRight, context);
            return Contains(leftValue, rightValue);
        }

        if (TryMatchFunction(expression, "in", out funcLeft, out funcRight))
        {
            var leftValue = ResolveOperand(funcLeft, context);
            var list = ParseList(funcRight, context);
            return list.Any(item => Compare(leftValue, item, "=="));
        }

        var singleValue = ResolveOperand(expression, context);
        return ToBool(singleValue);
    }

    private static bool TryMatchFunction(string expression, string function, out string left, out string right)
    {
        var token = $" {function} ";
        var index = IndexOfOperator(expression, token);
        if (index > -1)
        {
            left = expression[..index].Trim();
            right = expression[(index + token.Length)..].Trim();
            return true;
        }

        left = string.Empty;
        right = string.Empty;
        return false;
    }

    private static List<object?> ParseList(string raw, Dictionary<string, object?> context)
    {
        var trimmed = raw.Trim();
        if ((trimmed.StartsWith("[") && trimmed.EndsWith("]")) ||
            (trimmed.StartsWith("(") && trimmed.EndsWith(")")))
        {
            trimmed = trimmed[1..^1];
        }

        var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Select(p => ResolveOperand(p, context)).ToList();
    }

    private static bool Compare(object? leftValue, object? rightValue, string op)
    {
        if (op is "==" or "!=")
        {
            var equal = AreEqual(leftValue, rightValue);
            return op == "==" ? equal : !equal;
        }

        if (leftValue == null || rightValue == null)
        {
            return false;
        }

        if (TryConvertToDecimal(leftValue, out var leftDec) && TryConvertToDecimal(rightValue, out var rightDec))
        {
            return op switch
            {
                ">" => leftDec > rightDec,
                "<" => leftDec < rightDec,
                ">=" => leftDec >= rightDec,
                "<=" => leftDec <= rightDec,
                _ => false
            };
        }

        if (TryConvertToDateTime(leftValue, out var leftDate) && TryConvertToDateTime(rightValue, out var rightDate))
        {
            return op switch
            {
                ">" => leftDate > rightDate,
                "<" => leftDate < rightDate,
                ">=" => leftDate >= rightDate,
                "<=" => leftDate <= rightDate,
                _ => false
            };
        }

        if (leftValue is IComparable leftComparable && rightValue is IComparable)
        {
            var comparison = leftComparable.CompareTo(rightValue);
            return op switch
            {
                ">" => comparison > 0,
                "<" => comparison < 0,
                ">=" => comparison >= 0,
                "<=" => comparison <= 0,
                _ => false
            };
        }

        return false;
    }

    private static bool Contains(object? leftValue, object? rightValue)
    {
        if (leftValue == null || rightValue == null)
        {
            return false;
        }

        if (leftValue is string leftStr)
        {
            return leftStr.Contains(rightValue.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        if (leftValue is IEnumerable<object?> enumerable)
        {
            return enumerable.Any(item => AreEqual(item, rightValue));
        }

        return false;
    }

    private static bool AreEqual(object? leftValue, object? rightValue)
    {
        if (leftValue == null && rightValue == null)
        {
            return true;
        }

        if (leftValue == null || rightValue == null)
        {
            return false;
        }

        if (TryConvertToDecimal(leftValue, out var leftDec) && TryConvertToDecimal(rightValue, out var rightDec))
        {
            return leftDec == rightDec;
        }

        if (leftValue is string leftStr && rightValue is string rightStr)
        {
            return string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
        }

        return leftValue.Equals(rightValue);
    }

    private static bool TryConvertToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case decimal dec:
                result = dec;
                return true;
            case double dbl:
                result = (decimal)dbl;
                return true;
            case float flt:
                result = (decimal)flt;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case string str when decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool TryConvertToDateTime(object value, out DateTime result)
    {
        switch (value)
        {
            case DateTime dt:
                result = dt;
                return true;
            case string str when DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed):
                result = parsed;
                return true;
            default:
                result = DateTime.MinValue;
                return false;
        }
    }

    private static bool ToBool(object? value)
    {
        if (value == null)
        {
            return false;
        }

        if (value is bool boolVal)
        {
            return boolVal;
        }

        if (TryConvertToDecimal(value, out var dec))
        {
            return dec != 0;
        }

        var str = value.ToString();
        return bool.TryParse(str, out var parsed) && parsed;
    }

    private static object? ResolveOperand(string operand, Dictionary<string, object?> context)
    {
        var trimmed = operand.Trim();

        if ((trimmed.StartsWith("\"") && trimmed.EndsWith("\"")) ||
            (trimmed.StartsWith("'") && trimmed.EndsWith("'")))
        {
            return trimmed[1..^1];
        }

        if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (bool.TryParse(trimmed, out var boolValue))
        {
            return boolValue;
        }

        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
        {
            return dec;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return date;
        }

        return GetValueFromContext(context, trimmed);
    }

    private static object? GetValueFromContext(Dictionary<string, object?> context, string key)
    {
        if (context.TryGetValue(key, out var value))
        {
            return value;
        }

        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        object? current = context;
        foreach (var part in parts)
        {
            if (current == null)
            {
                return null;
            }

            if (current is Dictionary<string, object?> dict && dict.TryGetValue(part, out var nested))
            {
                current = nested;
                continue;
            }

            var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            current = prop?.GetValue(current);
        }

        return current;
    }

    private static int IndexOfOperator(string expression, string op)
    {
        var inQuotes = false;
        for (var i = 0; i <= expression.Length - op.Length; i++)
        {
            var ch = expression[i];
            if (ch is '"' or '\'')
            {
                inQuotes = !inQuotes;
            }

            if (inQuotes)
            {
                continue;
            }

            if (expression.AsSpan(i, op.Length).Equals(op, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> SplitByOperator(string expression, string op)
    {
        var parts = new List<string>();
        var inQuotes = false;
        var start = 0;
        for (var i = 0; i <= expression.Length - op.Length; i++)
        {
            var ch = expression[i];
            if (ch is '"' or '\'')
            {
                inQuotes = !inQuotes;
            }

            if (inQuotes)
            {
                continue;
            }

            if (expression.AsSpan(i, op.Length).Equals(op, StringComparison.Ordinal))
            {
                parts.Add(expression[start..i].Trim());
                start = i + op.Length;
                i += op.Length - 1;
            }
        }

        parts.Add(expression[start..].Trim());
        return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
    }
}
