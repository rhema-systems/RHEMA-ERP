namespace ErpSystem.Data.Seeders;

/// <summary>Idempotent configuration only; never creates or rewrites tickets or users.</summary>
public static class EhcPropertyEnquiryConfiguration
{
    public const string Sql = """
        IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name=N'enquiry.property.access')
        INSERT Permissions(Id,Name,DisplayName,Description,Category,IsSystemPermission,CreatedAt,IsDeleted)
        VALUES(NEWID(),N'enquiry.property.access',N'Property enquiry follow-up',N'View and reply to external property enquiries',N'Sales',1,SYSUTCDATETIME(),0);

        INSERT RolePermissions(RoleId,PermissionId,GrantedAt,GrantedBy)
        SELECT r.Id,p.Id,SYSUTCDATETIME(),N'Property enquiry configuration'
        FROM AspNetRoles r CROSS JOIN Permissions p
        WHERE p.Name=N'enquiry.property.access' AND r.Name IN(N'Sales User',N'Marketing User',N'HelpdeskAgent',N'HelpdeskSupervisor',N'HelpdeskManager',N'TenantAdmin',N'SuperAdmin')
        AND NOT EXISTS(SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);

        INSERT EhcTicketCategories(Id,TenantId,Code,Name,Description,AppliesToType,CreatedAt,IsDeleted)
        SELECT NEWID(),t.Id,N'PROPERTY-LISTING',N'Property listing enquiry',N'Business partner enquiries for published estate listings',1,SYSUTCDATETIME(),0
        FROM Tenants t WHERE t.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM EhcTicketCategories c WHERE c.TenantId=t.Id AND c.Code=N'PROPERTY-LISTING');

        INSERT NotificationTopics(Id,TenantId,[Key],Name,Description,EntityType,IsSystem,IsRequired,IsActive,EnableInApp,EnableEmail,EnableSms,InAppTitleTemplate,InAppBodyTemplate,ActionUrlTemplate,CreatedAt,IsDeleted)
        SELECT NEWID(),t.Id,n.TopicKey,n.TopicName,N'Property enquiry follow-up for Sales and Marketing',N'EhcTicket',1,1,1,1,1,0,
          N'Property enquiry {{ticketNumber}}',N'{{businessPartnerName}} has an enquiry about {{listingName}} ({{listingReference}}).',
          N'/sales/property-enquiries?id={{ticketId}}',SYSUTCDATETIME(),0
        FROM Tenants t CROSS JOIN (VALUES(N'EhcTicket.PropertyEnquiryCreated.Internal',N'New property enquiry'),(N'EhcTicket.PropertyEnquiryMessage.Internal',N'Property enquiry follow-up')) n(TopicKey,TopicName)
        WHERE t.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM NotificationTopics nt WHERE nt.TenantId=t.Id AND nt.[Key]=n.TopicKey);

        INSERT NotificationTopicRecipients(Id,TenantId,TopicId,RecipientKind,RecipientValue,IsSystem,SendInApp,SendEmail,SendSms,CreatedAt,IsDeleted)
        SELECT NEWID(),t.TenantId,t.Id,N'Role',r.RoleName,1,1,1,0,SYSUTCDATETIME(),0
        FROM NotificationTopics t CROSS JOIN (VALUES(N'Sales User'),(N'Marketing User')) r(RoleName)
        WHERE t.[Key] IN(N'EhcTicket.PropertyEnquiryCreated.Internal',N'EhcTicket.PropertyEnquiryMessage.Internal') AND t.IsDeleted=0
        AND NOT EXISTS(SELECT 1 FROM NotificationTopicRecipients nr WHERE nr.TopicId=t.Id AND nr.RecipientKind=N'Role' AND nr.RecipientValue=r.RoleName);
        """;
}
