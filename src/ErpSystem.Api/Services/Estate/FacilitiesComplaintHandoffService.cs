using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public sealed class FacilitiesComplaintHandoffService(
    ApplicationDbContext db,
    IEhcTicketService ticketService)
{
    private static bool IsComplaint(ProcedureCase procedureCase) =>
        string.Equals(procedureCase.Module, "Facilities", StringComparison.OrdinalIgnoreCase)
        && string.Equals(procedureCase.EntityType, "EstateFacilityComplaint", StringComparison.OrdinalIgnoreCase);

    public async Task EnsureTicketForHandoffAsync(
        ProcedureCase procedureCase,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (!IsComplaint(procedureCase)
            || !string.Equals(procedureCase.CurrentStageName, "Complaint Resolution Review", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var ticket = await FindTicketAsync(procedureCase, cancellationToken);
        if (ticket is null)
        {
            var category = await db.EhcTicketCategories.AsNoTracking()
                .Where(item => item.TenantId == procedureCase.TenantId && !item.IsDeleted
                    && item.ParentCategoryId == null && item.AppliesToType == EhcTicketType.Complaint)
                .OrderBy(item => item.Code == "COMPLAINTS" ? 0 : 1)
                .ThenBy(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (category is null)
            {
                throw new InvalidOperationException("Configure a Helpdesk complaint category before routing this Facilities complaint.");
            }

            var sourceReference = procedureCase.ReferenceNumber ?? procedureCase.Id.ToString();
            var propertyUnit = FieldValue(procedureCase, "propertyUnit") ?? "Not recorded";
            var details = FieldValue(procedureCase, "complaintDescription") ?? procedureCase.Description ?? procedureCase.Title;
            var created = await ticketService.CreateInternalTicketAsync(new CreateEhcTicketRequestDto
            {
                TicketType = EhcTicketType.Complaint,
                CategoryId = category.Id,
                Priority = ResolvePriority(FieldValue(procedureCase, "priority")),
                Source = EhcTicketSource.Internal,
                Subject = procedureCase.Title,
                Description = $"Facilities case {sourceReference}. Property/unit: {propertyUnit}. {details}",
                RelatedEntityType = "ProcedureCase",
                RelatedEntityReference = procedureCase.Id.ToString()
            }, cancellationToken);

            await SaveLinkAsync(procedureCase, created.Id, created.TicketNumber, userId, now, cancellationToken);
            return;
        }

        await SaveLinkAsync(procedureCase, ticket.Id, ticket.TicketNumber, userId, now, cancellationToken);
    }

    public async Task EnsureCloseoutReadyAsync(ProcedureCase procedureCase, CancellationToken cancellationToken = default)
    {
        if (!IsComplaint(procedureCase)
            || !string.Equals(procedureCase.CurrentStageName, "Complaint Closeout", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var ticket = await FindTicketAsync(procedureCase, cancellationToken);
        if (ticket is null)
        {
            throw new InvalidOperationException("Complaint closeout requires a linked Helpdesk ticket.");
        }

        if (ticket.Status is not EhcTicketStatus.Resolved and not EhcTicketStatus.Closed)
        {
            throw new InvalidOperationException($"Complaint closeout cannot be submitted because Helpdesk ticket {ticket.TicketNumber} is still {ticket.Status}.");
        }

        var feedback = FieldValue(procedureCase, "requesterFeedbackStatus");
        if (string.IsNullOrWhiteSpace(feedback) || string.Equals(feedback, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Record the requester feedback outcome before closing the Facilities complaint.");
        }

        if (string.IsNullOrWhiteSpace(FieldValue(procedureCase, "closureNotes")))
        {
            throw new InvalidOperationException("Record closeout notes before closing the Facilities complaint.");
        }
    }

    public async Task AddTicketStatusFieldsAsync(
        ProcedureCase procedureCase,
        List<ProcedureCaseFieldDto> fields,
        CancellationToken cancellationToken = default)
    {
        if (!IsComplaint(procedureCase)) return;

        var ticket = await FindTicketAsync(procedureCase, cancellationToken);
        if (ticket is null) return;

        AddOrReplace(fields, "helpdeskTicketId", "Helpdesk ticket ID", ticket.Id.ToString());
        AddOrReplace(fields, "helpdeskTicketReference", "Helpdesk ticket reference", ticket.TicketNumber);
        AddOrReplace(fields, "helpdeskTicketStatus", "Helpdesk ticket status", ticket.Status.ToString());
        AddOrReplace(fields, "helpdeskResolutionComplete", "Helpdesk resolution complete",
            ticket.Status is EhcTicketStatus.Resolved or EhcTicketStatus.Closed ? "true" : "false");
    }

    private async Task<EhcTicket?> FindTicketAsync(ProcedureCase procedureCase, CancellationToken cancellationToken)
    {
        var query = db.EhcTickets.AsNoTracking().Where(item =>
            item.TenantId == procedureCase.TenantId && !item.IsDeleted && item.TicketType == EhcTicketType.Complaint
            && item.RelatedEntityType == "ProcedureCase" && item.RelatedEntityReference == procedureCase.Id.ToString());
        var ticketId = FieldValue(procedureCase, "helpdeskTicketId");
        if (Guid.TryParse(ticketId, out var id))
        {
            var byId = await query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (byId is not null) return byId;
        }

        var reference = FieldValue(procedureCase, "helpdeskTicketReference");
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var byReference = await query.FirstOrDefaultAsync(item => item.TicketNumber == reference, cancellationToken);
            if (byReference is not null) return byReference;
        }

        var sourceId = procedureCase.Id.ToString();
        return await query.FirstOrDefaultAsync(item =>
            item.RelatedEntityType == "ProcedureCase" && item.RelatedEntityReference == sourceId,
            cancellationToken);
    }

    private async Task SaveLinkAsync(
        ProcedureCase procedureCase,
        Guid ticketId,
        string ticketNumber,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var tenantId = procedureCase.TenantId;
        await UpsertFieldAsync(tenantId, procedureCase.Id, "helpdeskTicketId", "Helpdesk ticket ID", ticketId.ToString(), userId, now, cancellationToken);
        await UpsertFieldAsync(tenantId, procedureCase.Id, "helpdeskTicketReference", "Helpdesk ticket reference", ticketNumber, userId, now, cancellationToken);
        if (!procedureCase.Activities.Any(item => item.Action == "Helpdesk ticket linked" && item.Details == ticketNumber))
        {
            db.ProcedureCaseActivities.Add(new ProcedureCaseActivity
            {
                TenantId = tenantId,
                ProcedureCaseId = procedureCase.Id,
                Action = "Helpdesk ticket linked",
                StageName = procedureCase.CurrentStageName,
                Details = ticketNumber,
                PerformedById = userId,
                PerformedAt = now,
                CreatedAt = now,
                CreatedById = userId,
                CreatedBy = "System"
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertFieldAsync(
        Guid tenantId, Guid caseId, string key, string label, string value,
        Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var field = await db.ProcedureCaseFields.FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.ProcedureCaseId == caseId && item.Key == key && !item.IsDeleted,
            cancellationToken);
        if (field is null)
        {
            db.ProcedureCaseFields.Add(new ProcedureCaseField
            {
                TenantId = tenantId,
                ProcedureCaseId = caseId,
                Key = key,
                Label = label,
                Value = value,
                CreatedAt = now,
                CreatedById = userId,
                CreatedBy = "System"
            });
        }
        else
        {
            field.Value = value;
            field.UpdatedAt = now;
            field.LastModifiedById = userId;
        }
    }

    private static string? FieldValue(ProcedureCase procedureCase, string key) =>
        procedureCase.Fields.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

    private static EhcTicketPriority ResolvePriority(string? priority) => priority?.Trim().ToLowerInvariant() switch
    {
        "low" => EhcTicketPriority.Low,
        "high" => EhcTicketPriority.High,
        "urgent" or "critical" => EhcTicketPriority.Critical,
        _ => EhcTicketPriority.Medium
    };

    private static void AddOrReplace(List<ProcedureCaseFieldDto> fields, string key, string label, string value)
    {
        var index = fields.FindIndex(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        var field = new ProcedureCaseFieldDto(Guid.Empty, key, label, "text", value, null);
        if (index < 0) fields.Add(field);
        else fields[index] = field;
    }
}
