-- Read-only inventory for TDC_QS_END_TO_END_UAT_WALKTHROUGH.html.
-- Existing business documents are not a substitute for the walkthrough's fresh run.
SET NOCOUNT ON;
IF DB_NAME()<>@ExpectedDatabase THROW 51997,'QS prerequisite database mismatch.',1;
DECLARE @Tenant uniqueidentifier=(SELECT Id FROM dbo.Tenants WHERE Code=N'DEFAULT' AND IsDeleted=0 AND Status=1);
IF @Tenant IS NULL THROW 51996,'Active DEFAULT tenant is missing.',1;
DECLARE @Actors TABLE(UserName nvarchar(256));
INSERT @Actors VALUES
(N'uat.qs.preparer'),(N'uat.qs.reviewer'),(N'uat.qs.approver'),
(N'procurementofficer'),(N'procurementapprover'),(N'uat.qs.contractor'),
(N'tdc0102-checker-201531'),(N'procurementevaluator'),(N'finance.manager'),
(N'ap.officer'),(N'financereviewer'),(N'financeapprover'),(N'admin');
SELECT N'Actor' Category,a.UserName Reference,
 CASE WHEN u.Id IS NULL THEN N'Missing'
 WHEN u.IsActive=0 THEN N'Inactive'
 WHEN NOT EXISTS(SELECT 1 FROM dbo.UserTenants m WHERE m.UserId=u.Id AND m.TenantId=@Tenant
 AND m.Status=0 AND m.IsDeleted=0 AND (m.ExpiresAt IS NULL OR m.ExpiresAt>SYSUTCDATETIME())) THEN N'No active DEFAULT membership'
 ELSE N'Active; permissions and assignments still require verification' END Finding
FROM @Actors a LEFT JOIN dbo.Users u ON u.UserName=a.UserName
UNION ALL
SELECT N'Actor role',u.UserName,r.Name
FROM @Actors a JOIN dbo.Users u ON u.UserName=a.UserName
JOIN dbo.UserRoles ur ON ur.UserId=u.Id JOIN dbo.AspNetRoles r ON r.Id=ur.RoleId
UNION ALL
SELECT N'Contractor partner',p.PartnerCode,p.PartnerName+N' / '+p.RegistrationStatus
FROM dbo.Users u JOIN dbo.BusinessPartnerUsers x ON x.UserId=u.Id AND x.TenantId=@Tenant AND x.IsActive=1 AND x.IsDeleted=0
JOIN dbo.BusinessPartners p ON p.Id=x.BusinessPartnerId AND p.TenantId=x.TenantId AND p.IsActive=1 AND p.IsDeleted=0
WHERE u.UserName=N'uat.qs.contractor'
UNION ALL
SELECT N'Contractor partner',N'uat.qs.contractor',N'Missing active partner link; external portal bid cannot be verified'
WHERE NOT EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.BusinessPartnerUsers x ON x.UserId=u.Id
 JOIN dbo.BusinessPartners p ON p.Id=x.BusinessPartnerId AND p.TenantId=x.TenantId
 WHERE u.UserName=N'uat.qs.contractor' AND x.TenantId=@Tenant AND x.IsActive=1 AND x.IsDeleted=0 AND p.IsActive=1 AND p.IsDeleted=0)
UNION ALL
SELECT N'QS profile',p.ProfileCode+N' v'+CONVERT(nvarchar(10),p.Version),N'Published and effective; verify each decision and route below'
FROM dbo.QuantitySurveyConfigurationProfiles p WHERE p.TenantId=@Tenant AND p.IsDeleted=0 AND p.LifecycleStatus=1
 AND p.EffectiveFrom<=SYSUTCDATETIME() AND (p.EffectiveTo IS NULL OR p.EffectiveTo>=SYSUTCDATETIME())
UNION ALL
SELECT N'QS profile',N'DEFAULT',N'No published effective QS configuration profile'
WHERE NOT EXISTS(SELECT 1 FROM dbo.QuantitySurveyConfigurationProfiles p WHERE p.TenantId=@Tenant AND p.IsDeleted=0 AND p.LifecycleStatus=1
 AND p.EffectiveFrom<=SYSUTCDATETIME() AND (p.EffectiveTo IS NULL OR p.EffectiveTo>=SYSUTCDATETIME()))
UNION ALL
SELECT N'QS decision',p.ProfileCode+N' / '+d.DecisionKey,
 N'Status='+CONVERT(nvarchar(10),d.Status)+N'; Approval='+CONVERT(nvarchar(10),d.ApprovalStatus)+N'; Evidence='+CONVERT(nvarchar(10),d.EvidenceStatus)
FROM dbo.QuantitySurveyConfigurationDecisions d JOIN dbo.QuantitySurveyConfigurationProfiles p ON p.Id=d.ProfileId AND p.TenantId=d.TenantId
WHERE p.TenantId=@Tenant AND p.IsDeleted=0 AND p.LifecycleStatus=1 AND d.IsDeleted=0
 AND p.EffectiveFrom<=SYSUTCDATETIME() AND (p.EffectiveTo IS NULL OR p.EffectiveTo>=SYSUTCDATETIME())
UNION ALL
SELECT N'QS workflow',t.Code,COALESCE(w.Name,N'No active published definition; inspect configured QS decision binding')
FROM dbo.WorkflowEntityTypes t LEFT JOIN dbo.WorkflowDefinitions w ON w.EntityTypeId=t.Id AND w.TenantId=t.TenantId
 AND w.IsDeleted=0 AND w.IsActive=1 AND w.LifecycleStatus=1
WHERE t.TenantId=@Tenant AND t.IsDeleted=0 AND t.IsActive=1 AND t.Code IN
 (N'QS_BOQ',N'QS_ESTIMATE',N'QS_MEASUREMENT',N'QS_VALUATION',N'QS_PAYMENT_CERTIFICATE',N'QS_VARIATION',N'QS_CLAIM',N'QS_FINAL_ACCOUNT',N'QS_RETENTION_RELEASE')
ORDER BY Category,Reference,Finding;
