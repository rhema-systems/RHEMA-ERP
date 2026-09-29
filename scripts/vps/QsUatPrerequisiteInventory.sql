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
(N'uat.qs.consultant'),(N'uat.qs.engineer'),
(N'tdc0102-checker-201531'),(N'procurementevaluator'),(N'finance.manager'),
(N'ap.officer'),(N'financereviewer'),(N'financeapprover'),(N'admin');
DECLARE @ReadyLand TABLE (LandReference nvarchar(180));
-- Match the NEW-project selector (no existing project/current-selection exception).
-- Candidate count is intentionally calculated before BoundaryVerified filtering,
-- matching EstateManagedAssetService.GetProjectReadyLandDemarcationsAsync.
;WITH Candidates AS (
 SELECT d.Id,d.BoundaryVerified,d.DemarcationNumber,a.Id AssetId,a.AssetCode,a.ProjectCode,a.Name,
 COUNT(*) OVER(PARTITION BY a.Id) CandidateCount,
 LTRIM(RTRIM(a.AssetCode))+N'-PORTION-'+
 CASE WHEN d.DemarcationNumber<1000 THEN RIGHT(N'000'+CONVERT(nvarchar(12),d.DemarcationNumber),3)
 ELSE CONVERT(nvarchar(12),d.DemarcationNumber) END LandReference
 FROM dbo.EstateLandDemarcations d JOIN dbo.EstateManagedAssets a ON a.Id=d.EstateManagedAssetId
 WHERE d.TenantId=@Tenant AND a.TenantId=@Tenant AND d.IsDeleted=0 AND a.IsDeleted=0
 AND a.AssetType=N'Land' AND a.Status=N'LandBank'
 AND (d.IsReadyForProjectManagement=1 OR a.IsReadyForProjectManagement=1)
 AND d.IsPublishedToExternalPortal=0 AND a.IsPublishedToExternalPortal=0
 AND NOT EXISTS(SELECT 1 FROM dbo.EstateLandDemarcations child
   WHERE child.ParentDemarcationId=d.Id AND child.IsDeleted=0 AND child.TenantId=@Tenant)
)
INSERT @ReadyLand SELECT c.LandReference FROM Candidates c
WHERE c.BoundaryVerified=1 AND NOT EXISTS(
 SELECT 1 FROM dbo.ProjectDevelopmentProfiles p WHERE p.TenantId=@Tenant AND p.IsDeleted=0
 AND (LTRIM(RTRIM(p.LandReference)) COLLATE Latin1_General_100_CI_AS=c.LandReference COLLATE Latin1_General_100_CI_AS
 OR (c.CandidateCount=1 AND LTRIM(RTRIM(p.LandReference))<>N'' AND
 LTRIM(RTRIM(p.LandReference)) COLLATE Latin1_General_100_CI_AS IN
 (LTRIM(RTRIM(c.AssetCode)),LTRIM(RTRIM(c.ProjectCode)),LTRIM(RTRIM(c.Name)),CONVERT(nvarchar(36),c.AssetId)))))
;
DECLARE @DecisionKeys TABLE(DecisionKey nvarchar(10));
INSERT @DecisionKeys VALUES(N'QS-DEC-001'),(N'QS-DEC-002'),(N'QS-DEC-003'),(N'QS-DEC-004'),
(N'QS-DEC-005'),(N'QS-DEC-006'),(N'QS-DEC-007'),(N'QS-DEC-008'),(N'QS-DEC-009'),
(N'QS-DEC-010'),(N'QS-DEC-011'),(N'QS-DEC-012'),(N'QS-DEC-013'),(N'QS-DEC-014'),
(N'QS-DEC-015'),(N'QS-DEC-016'),(N'QS-DEC-017');
DECLARE @Workflows TABLE(Code nvarchar(60));
INSERT @Workflows VALUES(N'QS_BOQ'),(N'QS_ESTIMATE'),(N'QS_MEASUREMENT'),(N'QS_VALUATION'),
(N'QS_PAYMENT_CERTIFICATE'),(N'QS_VARIATION'),(N'QS_CLAIM'),(N'QS_FINAL_ACCOUNT'),(N'QS_RETENTION_RELEASE');
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
SELECT N'QS profile',p.ProfileCode+N' v'+CONVERT(nvarchar(10),p.Version),
 CASE WHEN p.LifecycleStatus=0 THEN N'Draft; configuration review and publication required'
 ELSE N'Published but outside its effective dates; review dates through configuration lifecycle' END
FROM dbo.QuantitySurveyConfigurationProfiles p WHERE p.TenantId=@Tenant AND p.IsDeleted=0
 AND (p.LifecycleStatus=0 OR (p.LifecycleStatus=1 AND (p.EffectiveFrom>SYSUTCDATETIME() OR p.EffectiveTo<SYSUTCDATETIME())))
UNION ALL
SELECT N'QS decision',p.ProfileCode+N' / '+d.DecisionKey,
 N'Profile v'+CONVERT(nvarchar(10),p.Version)+N'; Status='+
 CASE d.Status WHEN 0 THEN N'Draft' WHEN 1 THEN N'Proposed' WHEN 2 THEN N'Approved' WHEN 3 THEN N'Rejected' ELSE N'Unknown' END+
 N'; Approval='+CASE d.ApprovalStatus WHEN 0 THEN N'Pending' WHEN 1 THEN N'Approved' WHEN 2 THEN N'Rejected' ELSE N'Unknown' END+
 N'; Evidence='+CASE d.EvidenceStatus WHEN 0 THEN N'Missing' WHEN 1 THEN N'Attached' WHEN 2 THEN N'Verified' ELSE N'Unknown' END+
 CASE WHEN d.EffectiveFrom IS NULL OR d.EffectiveFrom>SYSUTCDATETIME() OR d.EffectiveTo<SYSUTCDATETIME()
 THEN N'; Not currently effective' ELSE N'; Currently effective' END
FROM dbo.QuantitySurveyConfigurationDecisions d JOIN dbo.QuantitySurveyConfigurationProfiles p ON p.Id=d.ProfileId AND p.TenantId=d.TenantId
WHERE p.TenantId=@Tenant AND p.IsDeleted=0 AND p.LifecycleStatus IN (0,1) AND d.IsDeleted=0
UNION ALL
SELECT N'QS decision',p.ProfileCode+N' v'+CONVERT(nvarchar(10),p.Version)+N' / '+k.DecisionKey,N'Missing decision; configure and submit for owner review'
FROM dbo.QuantitySurveyConfigurationProfiles p CROSS JOIN @DecisionKeys k
WHERE p.TenantId=@Tenant AND p.IsDeleted=0 AND p.LifecycleStatus IN (0,1)
 AND NOT EXISTS(SELECT 1 FROM dbo.QuantitySurveyConfigurationDecisions d WHERE d.ProfileId=p.Id AND d.TenantId=@Tenant AND d.IsDeleted=0 AND d.DecisionKey=k.DecisionKey)
UNION ALL
SELECT N'QS workflow',e.Code,COALESCE(w.Name,N'No active published definition; inspect configured QS decision binding')
FROM @Workflows e LEFT JOIN dbo.WorkflowEntityTypes t ON t.Code=e.Code AND t.TenantId=@Tenant AND t.IsDeleted=0 AND t.IsActive=1
LEFT JOIN dbo.WorkflowDefinitions w ON w.EntityTypeId=t.Id AND w.TenantId=t.TenantId
 AND w.IsDeleted=0 AND w.IsActive=1 AND w.LifecycleStatus=1
UNION ALL
SELECT N'Estate ready land',N'Available for new project',
 N'Count='+CONVERT(nvarchar(12),COUNT(*))+CASE WHEN COUNT(*)=0 THEN N'; No selectable demarcated land; prepare an unassigned verified leaf portion in Estate Land Management'
 ELSE N'; Select the required portion from Estate in New Project; assignment is made during UAT' END FROM @ReadyLand
UNION ALL
SELECT N'Estate ready land reference',LandReference,N'Selectable in New Project; verify this is the intended UAT land' FROM @ReadyLand
ORDER BY Category,Reference,Finding;
