# Audit Logging Configuration

This document explains how to configure the automatic audit logging system that captures all database changes.

## Overview

The audit logging system automatically intercepts all entity changes (Create, Update, Delete) in Entity Framework and saves them to the `AuditLogs` table. This provides a complete audit trail of all data modifications.

## Configuration Options

### Environment Variables

Add these to your `.env` file:

```bash
# Enable or disable audit logging
AUDIT__ENABLED=true

# Only audit entities that implement IAuditable interface
AUDIT__ONLYAUDITABLEENTITIES=false

# Include system-generated changes (timestamps, etc.)
AUDIT__INCLUDESYSTEMCHANGES=false

# Maximum length for audit value JSON (prevents database issues)
AUDIT__MAXAUDITVALUELENGTH=4000
```

### appsettings.json Configuration

```json
{
  "Audit": {
    "Enabled": true,
    "OnlyAuditableEntities": false,
    "IncludeSystemChanges": false,
    "MaxAuditValueLength": 4000,
    "ExcludedEntityTypes": [
      "ErpSystem.Core.Entities.AuditLog",
      "ErpSystem.Core.Entities.SecurityLog",
      "ErpSystem.Core.Entities.RefreshToken",
      "ErpSystem.Core.Entities.BlacklistedToken"
    ],
    "IncludedEntityTypes": [
      "ErpSystem.Core.Entities.Tenant",
      "ErpSystem.Core.Entities.ApplicationUser"
    ],
    "ExcludedProperties": [
      "Password",
      "PasswordHash",
      "SecurityStamp",
      "ConcurrencyStamp",
      "AuthenticatorKey",
      "RecoveryCodes",
      "TwoFactorSecret",
      "Salt",
      "Hash",
      "Token",
      "RefreshToken",
      "AccessToken",
      "NormalizedUserName",
      "NormalizedEmail",
      "EmailConfirmationToken",
      "PasswordResetToken",
      "TwoFactorCode",
      "BackupCodes"
    ]
  }
}
```

## Configuration Explanations

### Enabled
- **Default**: `true`
- Controls whether audit logging is active globally

### OnlyAuditableEntities
- **Default**: `false`
- When `true`, only entities implementing the `IAuditable` interface will be audited
- When `false`, all entities are audited except those in the exclusion list

### IncludeSystemChanges
- **Default**: `false`
- Whether to include system-generated property changes like timestamps in audit logs
- Setting to `false` reduces audit log noise

### MaxAuditValueLength
- **Default**: `4000`
- Maximum length for serialized JSON values to prevent database constraint violations
- Values longer than this will be truncated

### ExcludedEntityTypes
- List of entity type names that should never be audited
- Prevents infinite loops (e.g., don't audit AuditLog itself)
- Use full type names including namespace

### IncludedEntityTypes
- When `OnlyAuditableEntities` is `false`, this list can specify additional entities to include
- Useful for fine-grained control over what gets audited

### ExcludedProperties
- List of property names that should never have their values recorded in audit logs
- Case-insensitive matching
- Critical for security - prevents passwords and other sensitive data from being logged

## IAuditable Interface

To mark entities for selective auditing, implement the `IAuditable` interface:

```csharp
using ErpSystem.Data.Interceptors;

public class Customer : BaseEntity, IAuditable
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    // ... other properties
}
```

## What Gets Audited

The audit interceptor captures:

1. **Entity Information**:
   - Entity type name (Resource)
   - Primary key value (ResourceId)
   - Action performed (Create, Update, Delete)

2. **Change Data**:
   - Old values (for Update and Delete operations)
   - New values (for Create and Update operations)
   - Only changed properties for Update operations

3. **Context Information**:
   - User ID and username
   - Tenant ID (for multi-tenant isolation)
   - IP address
   - User agent
   - Timestamp

4. **Excluded Information**:
   - Sensitive properties (passwords, tokens, etc.)
   - System-generated properties (if configured)
   - Navigation properties
   - Entities in exclusion list

## Audit Log Table Structure

```sql
CREATE TABLE AuditLogs (
    Id uniqueidentifier PRIMARY KEY,
    UserId uniqueidentifier NOT NULL,
    Username nvarchar(255) NOT NULL,
    Action nvarchar(100) NOT NULL,           -- 'Create', 'Update', 'Delete'
    Resource nvarchar(255) NOT NULL,         -- Entity type name
    ResourceId nvarchar(100) NULL,           -- Primary key value
    OldValues nvarchar(max) NULL,            -- JSON of old values
    NewValues nvarchar(max) NULL,            -- JSON of new values
    IpAddress nvarchar(45) NOT NULL,
    UserAgent nvarchar(500) NULL,
    Timestamp datetime2 NOT NULL,
    TenantId uniqueidentifier NOT NULL,
    CreatedAt datetime2 NOT NULL,
    -- Foreign keys and indexes...
);
```

## Performance Considerations

1. **Selective Auditing**: Use `OnlyAuditableEntities` and inclusion/exclusion lists to limit what gets audited
2. **Property Exclusions**: Exclude large text fields or binary data that aren't needed for auditing
3. **Value Length Limits**: The `MaxAuditValueLength` prevents database issues with large objects
4. **Asynchronous Processing**: The interceptor adds audit entries to the same transaction for consistency
5. **Index Optimization**: The audit table has indexes on commonly queried fields (UserId, Timestamp, Resource)

## Testing Audit Logging

1. **Enable Audit Logging**: Ensure `Audit:Enabled` is `true`
2. **Create/Update/Delete Entities**: Perform CRUD operations through the API
3. **Check Audit Logs**: Query the `/api/auditlog` endpoints to verify logs are being created
4. **Verify User Context**: Ensure all audit entries have proper user and tenant information
5. **Test Exclusions**: Verify that excluded entities and properties are not being logged

## Troubleshooting

### No Audit Logs Created
1. Check that `Audit:Enabled` is `true`
2. Verify user context is available (logged in user)
3. Check if entity type is in exclusion list
4. Look for errors in application logs

### Missing User Information
1. Ensure `ICurrentUserService` is properly configured
2. Verify JWT authentication is working
3. Check that tenant context is available

### Performance Issues
1. Review included/excluded entity types
2. Consider excluding large properties
3. Monitor audit table size and implement cleanup procedures
4. Review database indexes on audit table

### Sensitive Data in Logs
1. Add sensitive property names to `ExcludedProperties`
2. Review existing audit logs for sensitive data
3. Consider data retention policies for audit logs

## Best Practices

1. **Regular Cleanup**: Implement scheduled cleanup of old audit logs
2. **Security Review**: Regularly review what's being audited to ensure no sensitive data is logged
3. **Performance Monitoring**: Monitor the impact of audit logging on application performance  
4. **Access Control**: Restrict access to audit logs to authorized personnel only
5. **Backup Strategy**: Include audit logs in backup and recovery procedures