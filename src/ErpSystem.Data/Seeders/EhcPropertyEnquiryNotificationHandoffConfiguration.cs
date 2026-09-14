namespace ErpSystem.Data.Seeders;

/// <summary>
/// Routes new public property enquiries to Helpdesk for triage, then sends the Sales-ready
/// handoff and subsequent enquiries to Sales and Marketing. Configuration is idempotent and
/// does not create or modify tickets, users, CRM records, or any business transactions.
/// </summary>
public static class EhcPropertyEnquiryNotificationHandoffConfiguration
{
    public const string Sql = """
        UPDATE NotificationTopics
        SET IsActive=0
        WHERE [Key]=N'EhcTicket.PropertyEnquiryCreated.Internal' AND IsSystem=1 AND IsDeleted=0;

        INSERT NotificationTopics(Id,TenantId,[Key],Name,Description,EntityType,IsSystem,IsRequired,IsActive,EnableInApp,EnableEmail,EnableSms,InAppTitleTemplate,InAppBodyTemplate,ActionUrlTemplate,CreatedAt,IsDeleted)
        SELECT NEWID(),t.Id,n.TopicKey,n.TopicName,n.Description,N'EhcTicket',1,1,1,1,1,0,
          N'Property enquiry {{ticketNumber}}',N'{{businessPartnerName}} has an enquiry about {{listingName}} ({{listingReference}}).',
          n.ActionUrlTemplate,SYSUTCDATETIME(),0
        FROM Tenants t CROSS JOIN (VALUES
          (N'EhcTicket.PropertyEnquiryTriageCreated.Internal',N'New property enquiry for Helpdesk triage',N'Helpdesk must assign the enquiry to Sales and move it out of New before Sales can follow up.',N'/helpdesk/tickets/{{ticketId}}'),
          (N'EhcTicket.PropertyEnquiryTriageMessage.Internal',N'Property enquiry triage follow-up',N'Helpdesk follow-up before the property enquiry has been handed to Sales.',N'/helpdesk/tickets/{{ticketId}}'),
          (N'EhcTicket.PropertyEnquirySalesReady.Internal',N'Property enquiry ready for Sales',N'Property enquiry assigned to Sales and advanced from New.',N'/sales/property-enquiries?id={{ticketId}}')
        ) n(TopicKey,TopicName,Description,ActionUrlTemplate)
        WHERE t.IsDeleted=0 AND NOT EXISTS(
          SELECT 1 FROM NotificationTopics nt WHERE nt.TenantId=t.Id AND nt.[Key]=n.TopicKey);

        INSERT NotificationTopicRecipients(Id,TenantId,TopicId,RecipientKind,RecipientValue,IsSystem,SendInApp,SendEmail,SendSms,CreatedAt,IsDeleted)
        SELECT NEWID(),topic.TenantId,topic.Id,N'Role',recipient.RoleName,1,1,1,0,SYSUTCDATETIME(),0
        FROM NotificationTopics topic
        JOIN (VALUES
          (N'EhcTicket.PropertyEnquiryTriageCreated.Internal',N'HelpdeskAgent'),
          (N'EhcTicket.PropertyEnquiryTriageCreated.Internal',N'HelpdeskSupervisor'),
          (N'EhcTicket.PropertyEnquiryTriageCreated.Internal',N'HelpdeskManager'),
          (N'EhcTicket.PropertyEnquiryTriageMessage.Internal',N'HelpdeskAgent'),
          (N'EhcTicket.PropertyEnquiryTriageMessage.Internal',N'HelpdeskSupervisor'),
          (N'EhcTicket.PropertyEnquiryTriageMessage.Internal',N'HelpdeskManager'),
          (N'EhcTicket.PropertyEnquirySalesReady.Internal',N'Sales User'),
          (N'EhcTicket.PropertyEnquirySalesReady.Internal',N'Marketing User'),
          (N'EhcTicket.PropertyEnquiryMessage.Internal',N'Sales User'),
          (N'EhcTicket.PropertyEnquiryMessage.Internal',N'Marketing User')
        ) recipient(TopicKey,RoleName) ON recipient.TopicKey=topic.[Key]
        WHERE topic.IsDeleted=0 AND topic.IsActive=1
        AND NOT EXISTS(
          SELECT 1 FROM NotificationTopicRecipients existing
          WHERE existing.TopicId=topic.Id AND existing.RecipientKind=N'Role' AND existing.RecipientValue=recipient.RoleName);
        """;
}
