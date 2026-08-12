SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 52000, 'The QS E2E fixture cannot run against a system database.', 1;

DECLARE @TenantId uniqueidentifier =
(
    SELECT TOP (1) Id
    FROM dbo.Tenants
    WHERE IsDeleted = 0
    ORDER BY CASE WHEN Id = '00000000-0000-0000-0000-000000000001' THEN 0 ELSE 1 END, CreatedAt
);
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @EffectiveFrom datetime2 = CONVERT(date, @Now);
DECLARE @AdminId uniqueidentifier = '58cafd8b-42ce-4f67-0dbb-08de862e82ee';
DECLARE @OfficerId uniqueidentifier = '77af28cf-66d3-49c0-0dbd-08de862e82ee';
DECLARE @ApproverId uniqueidentifier = '9e475ce9-34ad-4a6d-0dbc-08de862e82ee';
DECLARE @ExternalContractorUserId uniqueidentifier =
(
    SELECT TOP (1) Id FROM dbo.Users
    WHERE TenantId = @TenantId AND UserName = N'external' AND IsActive = 1
);
DECLARE @ExternalConsultantUserId uniqueidentifier =
(
    SELECT TOP (1) Id FROM dbo.Users
    WHERE TenantId = @TenantId AND UserName = N'helpdesk.agent' AND IsActive = 1
);
DECLARE @ProjectId uniqueidentifier = (SELECT Id FROM dbo.Projects WHERE TenantId = @TenantId AND ProjectCode = N'PRJ-DEMO-2001' AND IsDeleted = 0);
DECLARE @ProjectTypeId uniqueidentifier = (SELECT ProjectTypeId FROM dbo.Projects WHERE Id = @ProjectId);
DECLARE @LocationId uniqueidentifier = COALESCE(
    (SELECT LocationId FROM dbo.Projects WHERE Id = @ProjectId),
    (SELECT TOP (1) Id FROM dbo.Locations WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 ORDER BY Name));
DECLARE @ContractorId uniqueidentifier =
(
    SELECT TOP (1) Id FROM dbo.BusinessPartners
    WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
      AND PartnerType IN (N'Contractor', N'Both', N'Supplier')
    ORDER BY CASE PartnerType WHEN N'Contractor' THEN 0 WHEN N'Both' THEN 1 ELSE 2 END, CreatedAt
);
DECLARE @ConsultantId uniqueidentifier =
(
    SELECT TOP (1) Id FROM dbo.BusinessPartners
    WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
      AND Id <> @ContractorId
      AND PartnerType NOT IN (N'Contractor', N'Both')
      AND RegistrationStatus IN (N'Active', N'Approved')
    ORDER BY CASE WHEN PartnerType = N'Customer' THEN 0 ELSE 1 END, CreatedAt
);

IF @TenantId IS NULL OR @ProjectId IS NULL OR @ProjectTypeId IS NULL OR @LocationId IS NULL
   OR @ContractorId IS NULL OR @ConsultantId IS NULL
    THROW 52001, 'QS E2E prerequisites are missing: tenant, PRJ-DEMO-2001, project type, location, active contractor, or active consultant partner.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @AdminId AND TenantId = @TenantId AND IsActive = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @OfficerId AND TenantId = @TenantId AND IsActive = 1)
   OR NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @ApproverId AND TenantId = @TenantId AND IsActive = 1)
    THROW 52002, 'QS E2E users admin, employee, and manager must exist and be active in the fixture tenant.', 1;
IF @ExternalContractorUserId IS NULL OR @ExternalConsultantUserId IS NULL
    THROW 52013, 'QS E2E external and helpdesk.agent users must exist and be active for contractor and consultant acceptance.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Security and assigned project scope. Role and permission masters remain owned by the shared seeder. */
    DECLARE @OfficerRoleId uniqueidentifier = (SELECT Id FROM dbo.AspNetRoles WHERE Name = N'TDC_QUANTITY_SURVEYOR');
    DECLARE @ApproverRoleId uniqueidentifier = (SELECT Id FROM dbo.AspNetRoles WHERE Name = N'TDC_SUPERVISING_QUANTITY_SURVEYOR');
    DECLARE @AssistantRoleId uniqueidentifier = (SELECT Id FROM dbo.AspNetRoles WHERE Name = N'TDC_ASSISTANT_QUANTITY_SURVEYOR');
    IF @OfficerRoleId IS NULL OR @ApproverRoleId IS NULL OR @AssistantRoleId IS NULL
        THROW 52003, 'Run the existing QuantitySurveyAccessControlSeeder before the QS E2E fixture.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId = @OfficerId AND RoleId = @OfficerRoleId)
        INSERT dbo.UserRoles (UserId, RoleId) VALUES (@OfficerId, @OfficerRoleId);
    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId = @ApproverId AND RoleId = @ApproverRoleId)
        INSERT dbo.UserRoles (UserId, RoleId) VALUES (@ApproverId, @ApproverRoleId);
    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId = @AdminId AND RoleId = @ApproverRoleId)
        INSERT dbo.UserRoles (UserId, RoleId) VALUES (@AdminId, @ApproverRoleId);

    IF NOT EXISTS (SELECT 1 FROM dbo.ProjectMembers WHERE ProjectId = @ProjectId AND UserId = @OfficerId AND Role = N'QuantitySurveyor' AND IsDeleted = 0)
        INSERT dbo.ProjectMembers (Id,ProjectId,UserId,Role,IsActive,JoinedAt,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (NEWID(),@ProjectId,@OfficerId,N'QuantitySurveyor',1,@Now,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    IF NOT EXISTS (SELECT 1 FROM dbo.ProjectMembers WHERE ProjectId = @ProjectId AND UserId = @ApproverId AND Role = N'SupervisingQuantitySurveyor' AND IsDeleted = 0)
        INSERT dbo.ProjectMembers (Id,ProjectId,UserId,Role,IsActive,JoinedAt,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (NEWID(),@ProjectId,@ApproverId,N'SupervisingQuantitySurveyor',1,@Now,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    IF NOT EXISTS (SELECT 1 FROM dbo.ProjectMembers WHERE ProjectId = @ProjectId AND UserId = @AdminId AND Role = N'QSAdministrator' AND IsDeleted = 0)
        INSERT dbo.ProjectMembers (Id,ProjectId,UserId,Role,IsActive,JoinedAt,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (NEWID(),@ProjectId,@AdminId,N'QSAdministrator',1,@Now,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);

    /* Reuse existing development identities and partner masters for real external-actor acceptance. */
    DECLARE @ContractorMembershipId uniqueidentifier =
        (SELECT Id FROM dbo.BusinessPartnerUsers WHERE BusinessPartnerId=@ContractorId AND UserId=@ExternalContractorUserId);
    IF @ContractorMembershipId IS NULL
        INSERT dbo.BusinessPartnerUsers
            (Id,BusinessPartnerId,UserId,Role,IsActive,GrantedAt,GrantedById,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (NEWID(),@ContractorId,@ExternalContractorUserId,N'Contractor Representative',1,@Now,@AdminId,N'QS E2E contractor portal identity.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    ELSE
        UPDATE dbo.BusinessPartnerUsers
        SET Role=N'Contractor Representative',IsActive=1,GrantedAt=@Now,GrantedById=@AdminId,
            Notes=N'QS E2E contractor portal identity.',IsDeleted=0,DeletedAt=NULL,DeletedBy=NULL,
            UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId
        WHERE Id=@ContractorMembershipId;

    DECLARE @ConsultantMembershipId uniqueidentifier =
        (SELECT Id FROM dbo.BusinessPartnerUsers WHERE BusinessPartnerId=@ConsultantId AND UserId=@ExternalConsultantUserId);
    IF @ConsultantMembershipId IS NULL
        INSERT dbo.BusinessPartnerUsers
            (Id,BusinessPartnerId,UserId,Role,IsActive,GrantedAt,GrantedById,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (NEWID(),@ConsultantId,@ExternalConsultantUserId,N'Consultant Representative',1,@Now,@AdminId,N'QS E2E consultant portal identity.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    ELSE
        UPDATE dbo.BusinessPartnerUsers
        SET Role=N'Consultant Representative',IsActive=1,GrantedAt=@Now,GrantedById=@AdminId,
            Notes=N'QS E2E consultant portal identity.',IsDeleted=0,DeletedAt=NULL,DeletedBy=NULL,
            UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId
        WHERE Id=@ConsultantMembershipId;

    DECLARE @ContractorAccessId uniqueidentifier =
        (SELECT Id FROM dbo.ProjectExternalAccessPolicies WHERE ProjectId=@ProjectId AND BusinessPartnerId=@ContractorId AND ArtifactType=N'Project' AND ArtifactId IS NULL);
    IF @ContractorAccessId IS NULL
        INSERT dbo.ProjectExternalAccessPolicies
            (Id,ProjectId,BusinessPartnerId,ArtifactType,ArtifactId,AccessLevel,CanComment,CanUpload,CanApprove,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (NEWID(),@ProjectId,@ContractorId,N'Project',NULL,N'Contribute',1,1,1,N'QS E2E contractor valuation submission access.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    ELSE
        UPDATE dbo.ProjectExternalAccessPolicies
        SET AccessLevel=N'Contribute',CanComment=1,CanUpload=1,CanApprove=1,
            Notes=N'QS E2E contractor valuation submission access.',IsDeleted=0,DeletedAt=NULL,DeletedBy=NULL,
            UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId
        WHERE Id=@ContractorAccessId;

    DECLARE @ConsultantAccessId uniqueidentifier =
        (SELECT Id FROM dbo.ProjectExternalAccessPolicies WHERE ProjectId=@ProjectId AND BusinessPartnerId=@ConsultantId AND ArtifactType=N'Project' AND ArtifactId IS NULL);
    IF @ConsultantAccessId IS NULL
        INSERT dbo.ProjectExternalAccessPolicies
            (Id,ProjectId,BusinessPartnerId,ArtifactType,ArtifactId,AccessLevel,CanComment,CanUpload,CanApprove,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (NEWID(),@ProjectId,@ConsultantId,N'Project',NULL,N'Contribute',1,1,1,N'QS E2E consultant valuation endorsement access.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    ELSE
        UPDATE dbo.ProjectExternalAccessPolicies
        SET AccessLevel=N'Contribute',CanComment=1,CanUpload=1,CanApprove=1,
            Notes=N'QS E2E consultant valuation endorsement access.',IsDeleted=0,DeletedAt=NULL,DeletedBy=NULL,
            UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId
        WHERE Id=@ConsultantAccessId;

    /* Published shared-workflow definitions, one for each QS-owned entity family. */
    DECLARE @WorkflowCode nvarchar(80), @WorkflowName nvarchar(200), @EntityTypeId uniqueidentifier, @WorkflowId uniqueidentifier;
    DECLARE workflow_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT Code, Name, Id FROM dbo.WorkflowEntityTypes
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND Code IN
        (N'QS_BOQ',N'QS_ESTIMATE',N'QS_ESCALATION',N'QS_MEASUREMENT',N'QS_VALUATION',N'QS_PAYMENT_CERTIFICATE',N'QS_RETENTION_RELEASE',N'QS_MATERIAL_DEDUCTION',N'QS_VARIATION',N'QS_CLAIM',N'QS_SUBCONTRACT',N'QS_FINAL_ACCOUNT');
    OPEN workflow_cursor;
    FETCH NEXT FROM workflow_cursor INTO @WorkflowCode,@WorkflowName,@EntityTypeId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @WorkflowId = (SELECT TOP (1) Id FROM dbo.WorkflowDefinitions WHERE TenantId=@TenantId AND EntityTypeId=@EntityTypeId AND LifecycleStatus=1 AND IsActive=1 AND IsDeleted=0 ORDER BY Version DESC);
        IF @WorkflowId IS NULL
        BEGIN
            SET @WorkflowId = NEWID();
            INSERT dbo.WorkflowDefinitions
                (Id,Name,Description,EntityTypeId,Version,IsActive,Configuration,ChangeSummary,DefinitionKey,LifecycleStatus,PublishedAt,PublishedById,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
            VALUES
                (@WorkflowId,N'QS E2E '+@WorkflowName+N' Approval',N'Governed two-stage technical review and independent approval for '+@WorkflowName+N'.',@EntityTypeId,1,1,N'{"fixture":"QS-E2E","makerChecker":true}',N'Controlled QS E2E workflow baseline.',NEWID(),1,@Now,@AdminId,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
            INSERT dbo.WorkflowSteps
                (Id,WorkflowDefinitionId,Name,Description,StepType,[Order],IsStartStep,IsEndStep,AssignmentType,AssignmentConfiguration,IsRequired,RequiredRole,EstimatedHours,Configuration,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
            VALUES
                (NEWID(),@WorkflowId,N'Technical review',N'QS technical review by an assigned quantity surveyor.',2,1,1,0,N'Role',N'{"role":"TDC_QUANTITY_SURVEYOR"}',1,N'TDC_QUANTITY_SURVEYOR',24,N'{"makerChecker":true}',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId),
                (NEWID(),@WorkflowId,N'Independent approval',N'Independent approval by the supervising quantity surveyor.',2,2,0,1,N'Role',N'{"role":"TDC_SUPERVISING_QUANTITY_SURVEYOR"}',1,N'TDC_SUPERVISING_QUANTITY_SURVEYOR',24,N'{"makerChecker":true,"independent":true}',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        END;
        FETCH NEXT FROM workflow_cursor INTO @WorkflowCode,@WorkflowName,@EntityTypeId;
    END;
    CLOSE workflow_cursor;
    DEALLOCATE workflow_cursor;

    IF (SELECT COUNT(*) FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId
        WHERE d.TenantId=@TenantId AND d.IsDeleted=0 AND d.IsActive=1 AND d.LifecycleStatus=1 AND e.Code LIKE N'QS[_]%') < 12
        THROW 52004, 'All twelve published QS workflow definitions are required.', 1;

    /* Controlled DMS metadata and report templates used by QS selectors. */
    DECLARE @TemplateCode nvarchar(80), @DocumentType nvarchar(150), @TemplateId uniqueidentifier;
    DECLARE template_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT Code, DocumentType FROM (VALUES
        (N'QS-MEAS-EVD',N'QS measurement evidence'),(N'QS-VAL-EVD',N'QS valuation evidence'),
        (N'QS-CERT-EVD',N'QS payment certificate evidence'),(N'QS-VAR-EVD',N'QS variation and claim evidence')) t(Code,DocumentType);
    OPEN template_cursor;
    FETCH NEXT FROM template_cursor INTO @TemplateCode,@DocumentType;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.CentralDocumentMetadataTemplates WHERE TenantId=@TenantId AND TemplateCode=@TemplateCode AND IsDeleted=0)
            INSERT dbo.CentralDocumentMetadataTemplates
                (Id,Module,DocumentType,TemplateCode,SourceLabel,RequiredFieldsJson,RelationshipsJson,RetentionRule,AccessProfile,IsActive,PublishedAt,PublishedById,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
            VALUES
                (NEWID(),N'QuantitySurvey',@DocumentType,@TemplateCode,N'Quantity Survey controlled evidence',N'["projectId","contractId","recordReference","evidenceDate"]',N'["Project","Contract","Workflow"]',N'Contract life plus 7 years',N'QS project and audit scope',1,@Now,@AdminId,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        FETCH NEXT FROM template_cursor INTO @TemplateCode,@DocumentType;
    END;
    CLOSE template_cursor;
    DEALLOCATE template_cursor;

    /* Keep the controlled templates aligned with the metadata actually emitted by each QS owner. */
    UPDATE dbo.CentralDocumentMetadataTemplates
    SET Module=N'QuantitySurvey',
        DocumentType=CASE TemplateCode
            WHEN N'QS-MEAS-EVD' THEN N'QS measurement evidence'
            WHEN N'QS-VAL-EVD' THEN N'QS valuation evidence'
            WHEN N'QS-CERT-EVD' THEN N'PaymentCertificate'
            WHEN N'QS-VAR-EVD' THEN N'Variation Evidence'
        END,
        RequiredFieldsJson=CASE TemplateCode
            WHEN N'QS-MEAS-EVD' THEN N'["measurementSheetId","projectId","boqLineKey","evidenceType","checksumSha256"]'
            WHEN N'QS-VAL-EVD' THEN N'["valuationWorksheetId","projectId","interimValuationId","evidenceType","checksumSha256"]'
            WHEN N'QS-CERT-EVD' THEN N'["projectId","valuationWorksheetId","certificateNumber","checksumSha256"]'
            WHEN N'QS-VAR-EVD' THEN N'["variationOrderId","projectId","checksumSha256"]'
        END,
        RelationshipsJson=N'["Project","Contract","Workflow"]',
        RetentionRule=N'Contract life plus 7 years',
        AccessProfile=N'QS project and audit scope',
        IsActive=1,
        UpdatedAt=@Now,
        UpdatedBy=N'QS E2E Seeder',
        LastModifiedById=@AdminId
    WHERE TenantId=@TenantId AND TemplateCode IN (N'QS-MEAS-EVD',N'QS-VAL-EVD',N'QS-CERT-EVD',N'QS-VAR-EVD') AND IsDeleted=0;

    INSERT dbo.CentralDocumentAccessRules
        (Id,AccessProfile,Module,RoleName,PermissionKey,CanView,CanUpload,CanAnnotate,CanApprove,CanArchive,IsActive,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
    SELECT NEWID(),N'QS project and audit scope',N'QuantitySurvey',NULL,v.PermissionKey,
           1,v.CanUpload,v.CanAnnotate,v.CanApprove,0,1,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId
    FROM (VALUES
        (N'quantity-survey.workspace.read',CONVERT(bit,0),CONVERT(bit,0),CONVERT(bit,0)),
        (N'quantity-survey.measurements.manage',CONVERT(bit,1),CONVERT(bit,1),CONVERT(bit,0)),
        (N'quantity-survey.valuations.manage',CONVERT(bit,1),CONVERT(bit,1),CONVERT(bit,0)),
        (N'quantity-survey.certificates.manage',CONVERT(bit,1),CONVERT(bit,1),CONVERT(bit,0)),
        (N'quantity-survey.transactions.approve',CONVERT(bit,0),CONVERT(bit,1),CONVERT(bit,1)),
        (N'quantity-survey.audit.read',CONVERT(bit,0),CONVERT(bit,0),CONVERT(bit,0))
    ) v(PermissionKey,CanUpload,CanAnnotate,CanApprove)
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.CentralDocumentAccessRules r
        WHERE r.TenantId=@TenantId AND r.AccessProfile=N'QS project and audit scope'
          AND r.Module=N'QuantitySurvey' AND r.PermissionKey=v.PermissionKey AND r.IsDeleted=0);

    IF NOT EXISTS (SELECT 1 FROM dbo.CentralDocumentRetentionPolicies WHERE TenantId=@TenantId AND PolicyCode=N'QS-RET-7Y' AND IsDeleted=0)
        INSERT dbo.CentralDocumentRetentionPolicies
            (Id,PolicyCode,Name,Module,DocumentType,RetentionDays,RequiresLegalHoldReview,AllowArchive,AllowDestruction,IsActive,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (NEWID(),N'QS-RET-7Y',N'Quantity Survey controlled-document retention',N'QuantitySurvey',NULL,2555,1,1,0,1,N'Seven-year minimum retention with legal-hold review and no direct destruction.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);

    DECLARE @ValuationReportId uniqueidentifier=(SELECT TOP(1) Id FROM dbo.Reports WHERE TenantId=@TenantId AND IsDeleted=0 AND Name=N'Valuation Statement');
    DECLARE @CertificateReportId uniqueidentifier=(SELECT TOP(1) Id FROM dbo.Reports WHERE TenantId=@TenantId AND IsDeleted=0 AND Name=N'Payment Certificate Register');
    IF @ValuationReportId IS NULL OR @CertificateReportId IS NULL THROW 52005, 'Published QS statutory reports must be seeded first.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ReportTemplates WHERE TenantId=@TenantId AND TemplateKey=N'QS-VALUATION-E2E' AND Version=1 AND IsDeleted=0)
        INSERT dbo.ReportTemplates (Id,ReportId,TemplateKey,Version,Name,Description,Category,Type,Audience,Cadence,Status,DefaultOutputFormat,OutputFormats,SavedFilters,GenerationMetadata,IsCustom,UsageCount,Tags,Configuration,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (NEWID(),@ValuationReportId,N'QS-VALUATION-E2E',1,N'QS Valuation Statement',N'Governed valuation statement template.',N'Quantity Survey',N'Table',N'Audit',N'AdHoc',N'Published',N'PDF',N'["Online","PDF","XLSX"]',N'{}',N'{"fixture":"QS-E2E"}',0,0,N'["QS","Valuation"]',N'{"scoped":true}',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    IF NOT EXISTS (SELECT 1 FROM dbo.ReportTemplates WHERE TenantId=@TenantId AND TemplateKey=N'QS-CERTIFICATE-E2E' AND Version=1 AND IsDeleted=0)
        INSERT dbo.ReportTemplates (Id,ReportId,TemplateKey,Version,Name,Description,Category,Type,Audience,Cadence,Status,DefaultOutputFormat,OutputFormats,SavedFilters,GenerationMetadata,IsCustom,UsageCount,Tags,Configuration,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (NEWID(),@CertificateReportId,N'QS-CERTIFICATE-E2E',1,N'QS Payment Certificate',N'Governed interim payment certificate template.',N'Quantity Survey',N'Table',N'Audit',N'AdHoc',N'Published',N'PDF',N'["Online","PDF","XLSX"]',N'{}',N'{"fixture":"QS-E2E"}',0,0,N'["QS","Certificate"]',N'{"scoped":true}',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);

    /* Central-DMS approval pack shared by the 17 independently approved configuration decisions. */
    DECLARE @EvidenceRecordId uniqueidentifier=(SELECT Id FROM dbo.CentralDocumentRecords WHERE TenantId=@TenantId AND DocumentReference=N'QS-E2E-CONFIG-APPROVAL' AND IsDeleted=0);
    DECLARE @EvidenceVersionId uniqueidentifier;
    IF @EvidenceRecordId IS NULL
    BEGIN
        SET @EvidenceRecordId=NEWID(); SET @EvidenceVersionId=NEWID();
        INSERT dbo.CentralDocumentRecords
            (Id,DocumentReference,Title,SourceModule,SourceLabel,SourceEntityType,SourceRecordReference,MetadataTemplateCode,RepositoryStatus,CurrentVersion,VersionStatus,AnnotationStatus,CommentStatus,AccessProfile,RetentionStatus,LifecycleStatus,EffectiveDate,PublishedAt,PublishedById,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (@EvidenceRecordId,N'QS-E2E-CONFIG-APPROVAL',N'QS E2E configuration approval pack',N'QuantitySurvey',N'Quantity Survey configuration',N'QuantitySurveyConfigurationProfile',N'TDC-QUANTITY-SURVEY/v1',N'QS-VAL-EVD',N'Managed',N'1.0',N'Published',N'NotRequired',N'Open',N'QS configuration and audit',N'Active',N'Active',@EffectiveFrom,@Now,@AdminId,N'Controlled E2E approval evidence; no runtime transaction evidence is fabricated.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        INSERT dbo.CentralDocumentVersions
            (Id,DocumentRecordId,VersionNumber,Status,FileName,ContentType,FileSize,CreatedByUserId,PublishedAt,PublishedById,ChangeSummary,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (@EvidenceVersionId,@EvidenceRecordId,N'1.0',N'Published',N'qs-e2e-configuration-approval.json',N'application/json',0,@AdminId,@Now,@AdminId,N'Initial controlled fixture approval pack.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    END
    ELSE
        SET @EvidenceVersionId=(SELECT TOP(1) Id FROM dbo.CentralDocumentVersions WHERE TenantId=@TenantId AND DocumentRecordId=@EvidenceRecordId AND Status=N'Published' AND IsDeleted=0 ORDER BY PublishedAt DESC);
    IF @EvidenceVersionId IS NULL THROW 52006, 'The QS configuration approval pack lacks a published DMS version.', 1;

    /* Publish the existing tenant profile with selector-backed values. */
    DECLARE @ProfileId uniqueidentifier=(SELECT TOP(1) Id FROM dbo.QuantitySurveyConfigurationProfiles WHERE TenantId=@TenantId AND ProfileCode=N'TDC-QUANTITY-SURVEY' AND Version=1 AND IsDeleted=0 ORDER BY CreatedAt);
    IF @ProfileId IS NULL THROW 52007, 'The existing QS configuration profile seeder must run before the E2E fixture.', 1;
    DECLARE @BoqWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_BOQ' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @EstimateWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_ESTIMATE' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @EscalationWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_ESCALATION' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @MeasurementWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_MEASUREMENT' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @ValuationWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_VALUATION' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @CertificateWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_PAYMENT_CERTIFICATE' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @RetentionWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_RETENTION_RELEASE' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @MaterialWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_MATERIAL_DEDUCTION' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @VariationWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_VARIATION' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @ClaimWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_CLAIM' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @SubcontractWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_SUBCONTRACT' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @FinalWorkflow uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.WorkflowDefinitions d JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId WHERE d.TenantId=@TenantId AND e.Code=N'QS_FINAL_ACCOUNT' AND d.LifecycleStatus=1 AND d.IsActive=1 AND d.IsDeleted=0 ORDER BY d.Version DESC);
    DECLARE @MeasurementTemplate uniqueidentifier=(SELECT Id FROM dbo.CentralDocumentMetadataTemplates WHERE TenantId=@TenantId AND TemplateCode=N'QS-MEAS-EVD' AND IsDeleted=0);
    DECLARE @ValuationDmsTemplate uniqueidentifier=(SELECT Id FROM dbo.CentralDocumentMetadataTemplates WHERE TenantId=@TenantId AND TemplateCode=N'QS-VAL-EVD' AND IsDeleted=0);
    DECLARE @CertificateDmsTemplate uniqueidentifier=(SELECT Id FROM dbo.CentralDocumentMetadataTemplates WHERE TenantId=@TenantId AND TemplateCode=N'QS-CERT-EVD' AND IsDeleted=0);
    DECLARE @VariationDmsTemplate uniqueidentifier=(SELECT Id FROM dbo.CentralDocumentMetadataTemplates WHERE TenantId=@TenantId AND TemplateCode=N'QS-VAR-EVD' AND IsDeleted=0);
    DECLARE @ValuationTemplate uniqueidentifier=(SELECT Id FROM dbo.ReportTemplates WHERE TenantId=@TenantId AND TemplateKey=N'QS-VALUATION-E2E' AND Version=1 AND IsDeleted=0);
    DECLARE @CertificateTemplate uniqueidentifier=(SELECT Id FROM dbo.ReportTemplates WHERE TenantId=@TenantId AND TemplateKey=N'QS-CERTIFICATE-E2E' AND Version=1 AND IsDeleted=0);
    DECLARE @ExpenseAccount uniqueidentifier=(SELECT TOP(1) Id FROM dbo.Accounts WHERE TenantId=@TenantId AND IsDeleted=0 AND Status=1 AND AllowDirectPosting=1 AND AccountType=5 ORDER BY CASE WHEN AccountCode=N'6500' THEN 0 ELSE 1 END,AccountCode);
    DECLARE @ApAccount uniqueidentifier=(SELECT TOP(1) Id FROM dbo.Accounts WHERE TenantId=@TenantId AND IsDeleted=0 AND Status=1 AND IsControlAccount=1 AND AccountType=2 ORDER BY CASE WHEN AccountCode=N'2000' THEN 0 ELSE 1 END,AccountCode);
    DECLARE @PaymentTerm uniqueidentifier=(SELECT TOP(1) Id FROM dbo.PaymentTerms WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1 AND ApplicableTo IN (N'All',N'Supplier',N'Contractor') ORDER BY CASE WHEN Code=N'NET30' THEN 0 ELSE 1 END,DisplayOrder);
    /*
       The legacy Finance development seed can contain only the sales VAT group.
       Keep the QS fixture selector-governed by materialising the Finance-owned
       purchase equivalent from the active standard VAT components when the
       normal GH-PURCH-STD group is absent. Never substitute a WHT-only group.
    */
    DECLARE @TaxGroup uniqueidentifier=
    (
        SELECT TOP(1) Id
        FROM dbo.TaxGroups
        WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1
          AND Applicability IN (2,3)
          AND Code IN (N'GH-PURCH-STD',N'QS-E2E-PURCH-STD')
        ORDER BY CASE WHEN Code=N'GH-PURCH-STD' THEN 0 ELSE 1 END,Code
    );
    IF @TaxGroup IS NULL
    BEGIN
        DECLARE @SalesVatGroup uniqueidentifier=
        (
            SELECT TOP(1) Id
            FROM dbo.TaxGroups
            WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1
              AND Code IN (N'GH-SALES-STD',N'VAT-STD-SCHEME')
            ORDER BY CASE WHEN Code=N'GH-SALES-STD' THEN 0 ELSE 1 END,Code
        );
        IF @SalesVatGroup IS NULL
            THROW 52014, 'An active Finance standard VAT group is required to create the QS purchase-tax fixture.', 1;

        SET @TaxGroup='D6000000-0000-4000-8000-000000000030';
        INSERT dbo.TaxGroups
            (Id,Code,Name,Description,Applicability,IsDefault,IsActive,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (@TaxGroup,N'QS-E2E-PURCH-STD',N'QS E2E Ghana Standard Purchase Tax',
             N'Controlled acceptance-only purchase equivalent of the tenant Finance standard VAT group.',
             2,0,1,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);

        INSERT dbo.TaxGroupComponents
            (Id,TaxGroupId,TaxId,CalculationOrder,CompoundBasis,AppliesOnTaxCodes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        SELECT NEWID(),@TaxGroup,c.TaxId,c.CalculationOrder,c.CompoundBasis,c.AppliesOnTaxCodes,
               @Now,N'QS E2E Seeder',@AdminId,0,@TenantId
        FROM dbo.TaxGroupComponents c
        JOIN dbo.Taxes t ON t.Id=c.TaxId AND t.TenantId=c.TenantId
        WHERE c.TenantId=@TenantId AND c.TaxGroupId=@SalesVatGroup
          AND c.IsDeleted=0 AND t.IsDeleted=0 AND t.IsActive=1
          AND t.Category IN (1,2);
    END
    DECLARE @WithholdingTax uniqueidentifier=(SELECT TOP(1) Id FROM dbo.Taxes WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1 AND Code=N'WHT-WORKS');
    DECLARE @ReportIds nvarchar(max)=(SELECT N'["'+STRING_AGG(CONVERT(nvarchar(max),LOWER(CONVERT(varchar(36),Id))),N'","')+N'"]' FROM dbo.Reports WHERE TenantId=@TenantId AND IsDeleted=0 AND Type=N'quantity-survey' AND Status IN (N'published',N'Published'));
    IF @ExpenseAccount IS NULL OR @ApAccount IS NULL OR @PaymentTerm IS NULL OR @TaxGroup IS NULL OR @WithholdingTax IS NULL
        THROW 52015, 'QS E2E Finance selectors are incomplete: expense, AP, payment term, purchase VAT group, or Works WHT.', 1;
    IF (SELECT COUNT(*) FROM dbo.TaxGroupComponents WHERE TenantId=@TenantId AND TaxGroupId=@TaxGroup AND IsDeleted=0) < 1
        THROW 52016, 'The QS E2E purchase-tax group must contain at least one active governed component.', 1;
    DECLARE @From nvarchar(30)=CONVERT(nvarchar(30),CONVERT(date,@EffectiveFrom),126);
    DECLARE @ValueJson nvarchar(max);

    DECLARE decision_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT DecisionKey FROM dbo.QuantitySurveyConfigurationDecisions WHERE ProfileId=@ProfileId AND TenantId=@TenantId AND IsDeleted=0 ORDER BY DecisionKey;
    DECLARE @DecisionKey nvarchar(20), @DecisionId uniqueidentifier;
    OPEN decision_cursor; FETCH NEXT FROM decision_cursor INTO @DecisionKey;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @ValueJson = CASE @DecisionKey
        WHEN N'QS-DEC-001' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"officerRoleIds":["'+LOWER(CONVERT(varchar(36),@OfficerRoleId))+N'"],"approverRoleIds":["'+LOWER(CONVERT(varchar(36),@ApproverRoleId))+N'"],"oversightRoleIds":["00000000-0000-0000-0000-000000000001"],"currencyCode":"GHS","operationalAuthorityLimit":500000,"seniorAuthorityLimit":5000000,"executiveAuthorityLimit":50000000,"enforceProjectScope":true,"enforceContractScope":true,"enforceSectionScope":true}'
        WHEN N'QS-DEC-002' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"allowedStandards":["smm7","cesmm4","tdcLocal"],"defaultStandard":"cesmm4","projectTypeIds":["'+LOWER(CONVERT(varchar(36),@ProjectTypeId))+N'"],"requireCostCode":true,"requireTrade":true,"requireWorkPackage":true}'
        WHEN N'QS-DEC-003' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"requiredVersionTypes":["approved","revised","remeasurement","finalAccount"],"boqWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@BoqWorkflow))+N'","estimateWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@EstimateWorkflow))+N'","approvedVersionsImmutable":true,"requireWorkflowBeforeUse":true,"requireLineLevelComparison":true}'
        WHEN N'QS-DEC-004' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"components":["material","labour","plant","subcontract","overhead","profit","contingency","wastage","transport"],"maximumOverheadPercent":15,"maximumProfitPercent":15,"maximumContingencyPercent":10,"maximumWastagePercent":10,"decimalPlaces":2}'
        WHEN N'QS-DEC-005' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"dimensions":["projectType","location","period","material","plant","equipment"],"updateCadenceMonths":3,"projectTypeIds":["'+LOWER(CONVERT(varchar(36),@ProjectTypeId))+N'"],"locationIds":["'+LOWER(CONVERT(varchar(36),@LocationId))+N'"],"requireMarketEvidence":true}'
        WHEN N'QS-DEC-006' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"indexSources":["gssPbci","controlledManualImport"],"formula":"fixedCoefficientIndexRatio","materialCoefficient":55,"labourCoefficient":25,"plantCoefficient":15,"otherCoefficient":5,"approvalWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@EscalationWorkflow))+N'","importFormat":"Controlled Excel"}'
        WHEN N'QS-DEC-007' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"workflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@MeasurementWorkflow))+N'","metadataTemplateId":"'+LOWER(CONVERT(varchar(36),@MeasurementTemplate))+N'","jointAttendanceRoleIds":["'+LOWER(CONVERT(varchar(36),@OfficerRoleId))+N'"],"consultantRoleIds":["'+LOWER(CONVERT(varchar(36),@ApproverRoleId))+N'"],"requireContractorSignature":true,"requireConsultantSignature":true}'
        WHEN N'QS-DEC-008' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"valuationWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@ValuationWorkflow))+N'","certificateWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@CertificateWorkflow))+N'","valuationTemplateId":"'+LOWER(CONVERT(varchar(36),@ValuationTemplate))+N'","certificateTemplateId":"'+LOWER(CONVERT(varchar(36),@CertificateTemplate))+N'","valuationEvidenceMetadataTemplateId":"'+LOWER(CONVERT(varchar(36),@ValuationDmsTemplate))+N'","certificateMetadataTemplateId":"'+LOWER(CONVERT(varchar(36),@CertificateDmsTemplate))+N'","certificateExpenseAccountId":"'+LOWER(CONVERT(varchar(36),@ExpenseAccount))+N'","certificateAccountsPayableAccountId":"'+LOWER(CONVERT(varchar(36),@ApAccount))+N'","certificatePaymentTermId":"'+LOWER(CONVERT(varchar(36),@PaymentTerm))+N'","certificateTaxGroupId":"'+LOWER(CONVERT(varchar(36),@TaxGroup))+N'","certificateWithholdingTaxId":"'+LOWER(CONVERT(varchar(36),@WithholdingTax))+N'","taxHandling":"financeCalculated","requireContractorSubmission":true,"requireConsultantEndorsement":true,"requireSupportingEvidence":true,"requirePreviousCertificate":true,"applyAdvanceRecovery":true,"applyRetention":true}'
        WHEN N'QS-DEC-009' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"maximumRetentionPercent":10,"practicalCompletionReleasePercent":50,"sectionalTakeoverReleasePercent":0,"defectsReleasePercent":50,"defectsLiabilityDays":365,"approvalWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@RetentionWorkflow))+N'","allowRetentionBond":true}'
        WHEN N'QS-DEC-010' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"allowMaterialsOnSite":true,"allowOffSiteMaterials":false,"deductTdcSuppliedMaterials":true,"valuationBasis":"lowerOfCostOrApprovedRate","requireInventoryReconciliation":true,"approvalWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@MaterialWorkflow))+N'"}'
        WHEN N'QS-DEC-011' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"variationWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@VariationWorkflow))+N'","claimWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@ClaimWorkflow))+N'","variationEvidenceMetadataTemplateId":"'+LOWER(CONVERT(varchar(36),@VariationDmsTemplate))+N'","allowedTypes":["Variation","Claim","Daywork","Additional Work","Site Instruction","Change Order"],"updateContractSum":true,"updateBudget":true,"updateForecast":true,"updateCertificate":true}'
        WHEN N'QS-DEC-012' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"controlProvisionalSums":true,"controlContingencies":true,"controlDefectsLiability":true,"controlSectionalTakeover":true,"controlSubcontracts":true,"controlClaimClauses":true,"controlBackCharges":true,"controlContraCharges":true,"requireCommercialTermsDocument":false,"subcontractWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@SubcontractWorkflow))+N'","finalAccountWorkflowDefinitionId":"'+LOWER(CONVERT(varchar(36),@FinalWorkflow))+N'"}'
        WHEN N'QS-DEC-013' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"channels":["externalPortal","controlledExcel","signedPdf"],"allowedFileExtensions":[".xlsx",".csv",".pdf",".docx"],"maximumFileSizeMb":50,"requirePortalIdentity":true,"requireEvidence":true,"requireSignature":true}'
        WHEN N'QS-DEC-014' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"allowedTools":[],"exchangeModes":["controlledExcel","api","signedPdf"],"requireStagingAndReconciliation":true,"metadataTemplateIds":["'+LOWER(CONVERT(varchar(36),@MeasurementTemplate))+N'","'+LOWER(CONVERT(varchar(36),@ValuationDmsTemplate))+N'"]}'
        WHEN N'QS-DEC-015' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"reportIds":'+@ReportIds+N',"viewerRoleIds":["'+LOWER(CONVERT(varchar(36),@OfficerRoleId))+N'","'+LOWER(CONVERT(varchar(36),@ApproverRoleId))+N'"],"exportFormats":["PDF","XLSX","CSV"],"enforceScopedDrilldown":true,"allowScheduledDistribution":false}'
        WHEN N'QS-DEC-016' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"modules":["projects","procurement","inventory","contracts","accountsPayable","generalLedger","documentManagement","workflow"],"postingMode":"controlledEvent","requireIdempotencyKey":true,"requireReconciliation":true,"prohibitDuplicatePosting":true}'
        WHEN N'QS-DEC-017' THEN N'{"effectiveFrom":"'+@From+N'","effectiveTo":null,"sourceTypes":["excel","csv","legacyDatabase","physicalRecord","centralDms"],"ownerRoleIds":["'+LOWER(CONVERT(varchar(36),@OfficerRoleId))+N'"],"reviewerRoleIds":["'+LOWER(CONVERT(varchar(36),@ApproverRoleId))+N'"],"signOffRoleIds":["00000000-0000-0000-0000-000000000001"],"requireStaging":true,"requireReconciliation":true,"requireSignedAcceptance":true}' END;
        SET @DecisionId=(SELECT Id FROM dbo.QuantitySurveyConfigurationDecisions WHERE ProfileId=@ProfileId AND DecisionKey=@DecisionKey AND IsDeleted=0);
        IF @ValueJson IS NULL OR ISJSON(@ValueJson)<>1 THROW 52008, 'A QS decision fixture value is missing or invalid JSON.', 1;
        IF (SELECT LifecycleStatus FROM dbo.QuantitySurveyConfigurationProfiles WHERE Id=@ProfileId)=0
            UPDATE dbo.QuantitySurveyConfigurationDecisions SET SchemaVersion=1,Status=2,ApprovalStatus=1,EvidenceStatus=2,ValueJson=@ValueJson,DecisionDate=@Now,EffectiveFrom=@EffectiveFrom,EffectiveTo=NULL,ApprovedById=@ApproverId,ApprovedAt=@Now,ApprovalReference=N'QS-E2E-CONTROL-BOARD',SourceLineage=N'Controlled local E2E fixture using live selector-owned records.',Notes=N'Golden path configuration; Phase 7 remains outside this fixture.',UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId WHERE Id=@DecisionId;
        IF NOT EXISTS (SELECT 1 FROM dbo.QuantitySurveyConfigurationEvidenceLinks WHERE TenantId=@TenantId AND DecisionId=@DecisionId AND CentralDocumentVersionId=@EvidenceVersionId AND IsDeleted=0)
            INSERT dbo.QuantitySurveyConfigurationEvidenceLinks (Id,ProfileId,DecisionId,CentralDocumentRecordId,CentralDocumentVersionId,EvidenceType,Checksum,LinkedById,LinkedAt,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
            VALUES (NEWID(),@ProfileId,@DecisionId,@EvidenceRecordId,@EvidenceVersionId,N'Approved decision register',CONVERT(varchar(64),HASHBYTES('SHA2_256',@ValueJson),2),@AdminId,@Now,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        FETCH NEXT FROM decision_cursor INTO @DecisionKey;
    END;
    CLOSE decision_cursor; DEALLOCATE decision_cursor;
    IF (SELECT COUNT(*) FROM dbo.QuantitySurveyConfigurationDecisions WHERE ProfileId=@ProfileId AND Status=2 AND ApprovalStatus=1 AND EvidenceStatus=2 AND IsDeleted=0)<>17
        THROW 52009, 'All 17 QS decisions must be approved and evidence-verified.', 1;
    IF (SELECT LifecycleStatus FROM dbo.QuantitySurveyConfigurationProfiles WHERE Id=@ProfileId)=0
        UPDATE dbo.QuantitySurveyConfigurationProfiles SET LifecycleStatus=1,EffectiveFrom=@EffectiveFrom,EffectiveTo=NULL,IsDefault=1,PublishedAt=@Now,PublishedById=@ApproverId,ChangeSummary=N'Published governed QS E2E configuration with 17 controlled decisions.',UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId WHERE Id=@ProfileId;
    IF NOT EXISTS (SELECT 1 FROM dbo.QuantitySurveyConfigurationRevisions WHERE TenantId=@TenantId AND ProfileId=@ProfileId AND CorrelationId=N'qs-e2e-profile-publication')
        INSERT dbo.QuantitySurveyConfigurationRevisions (Id,ProfileId,Action,Result,CorrelationId,ActorUserId,ActorName,ActorRoles,Reason,AfterJson,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (NEWID(),@ProfileId,N'QuantitySurvey.Configuration.SeedE2E',N'Succeeded',N'qs-e2e-profile-publication',@AdminId,N'admin',N'SuperAdmin',N'Create a governed, repeatable local E2E baseline.',N'{"profile":"TDC-QUANTITY-SURVEY","version":1,"decisions":17,"status":"Published"}',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);

    /*
       Contract activation and Works closeout resolve the canonical TDC-PROCUREMENT
       family, not the supplier-onboarding test profile. Reuse the tenant profile
       created by ProcurementConfigurationProfileSeeder and publish it only when
       no complete effective version already exists. This is a development E2E
       fixture; it never mutates an existing Published decision register.
    */
    DECLARE @ProcProfile uniqueidentifier =
    (
        SELECT TOP (1) p.Id
        FROM dbo.ProcurementConfigurationProfiles p
        WHERE p.TenantId=@TenantId AND p.ProfileCode=N'TDC-PROCUREMENT'
          AND p.LifecycleStatus=1 AND p.IsDeleted=0 AND p.EffectiveFrom<=@Now
          AND (p.EffectiveTo IS NULL OR p.EffectiveTo>=@Now)
          AND (SELECT COUNT(*) FROM dbo.ProcurementConfigurationDecisions d
               WHERE d.ProfileId=p.Id AND d.TenantId=@TenantId AND d.IsDeleted=0)=14
          AND (SELECT COUNT(*) FROM dbo.ProcurementConfigurationDecisions d
               WHERE d.ProfileId=p.Id AND d.TenantId=@TenantId AND d.IsDeleted=0
                 AND d.Status=2 AND d.ApprovalStatus=1 AND d.EvidenceStatus<>0)=14
        ORDER BY p.IsDefault DESC,p.Version DESC
    );
    IF @ProcProfile IS NULL
    BEGIN
        SET @ProcProfile=(SELECT TOP (1) Id FROM dbo.ProcurementConfigurationProfiles
            WHERE TenantId=@TenantId AND ProfileCode=N'TDC-PROCUREMENT'
              AND LifecycleStatus=0 AND IsDeleted=0 ORDER BY Version DESC);
        IF @ProcProfile IS NULL
            THROW 52010, 'Run the Procurement configuration-profile seeder before the QS E2E fixture.', 1;

        DECLARE @ProcWorkflow uniqueidentifier, @ProcWorkflowEntity nvarchar(150);
        SELECT TOP (1) @ProcWorkflow=d.Id,@ProcWorkflowEntity=e.Code
        FROM dbo.WorkflowDefinitions d
        JOIN dbo.WorkflowEntityTypes e ON e.Id=d.EntityTypeId
        WHERE d.TenantId=@TenantId AND d.LifecycleStatus=1 AND d.IsActive=1
          AND d.IsDeleted=0 AND e.TenantId=@TenantId AND e.IsActive=1 AND e.IsDeleted=0
        ORDER BY CASE WHEN e.Code=N'PROCUREMENT_CONTRACT' THEN 0 ELSE 1 END,d.Version DESC;
        IF @ProcWorkflow IS NULL
            THROW 52010, 'A Published shared workflow is required for the Procurement E2E profile.', 1;

        DECLARE @ProcFrom nvarchar(30)=CONVERT(nvarchar(30),CONVERT(date,@EffectiveFrom),126);
        DECLARE @ProcWorkflowText nvarchar(36)=LOWER(CONVERT(nvarchar(36),@ProcWorkflow));
        DECLARE @ProcValue nvarchar(max);
        DECLARE @ProcDecisionKey nvarchar(20), @ProcDecisionId uniqueidentifier;
        DECLARE proc_decision_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT DecisionKey,Id FROM dbo.ProcurementConfigurationDecisions
            WHERE ProfileId=@ProcProfile AND TenantId=@TenantId AND IsDeleted=0 ORDER BY DecisionKey;
        OPEN proc_decision_cursor;
        FETCH NEXT FROM proc_decision_cursor INTO @ProcDecisionKey,@ProcDecisionId;
        WHILE @@FETCH_STATUS=0
        BEGIN
            SET @ProcValue=CASE @ProcDecisionKey
                WHEN N'DEC-001' THEN N'{"category":"works","serviceClass":"Construction works","method":"nationalCompetitiveTendering","currencyCode":"GHS","lowerBound":0,"upperBound":50000000,"lowerInclusive":true,"upperInclusive":true,"statutoryReference":"Public Procurement Act","effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-002' THEN N'{"authorityLevel":"Entity Tender Committee","currencyCode":"GHS","lowerBound":0,"upperBound":50000000,"lowerInclusive":true,"upperInclusive":true,"escalationAuthority":"Central Tender Review Committee","applicableCategories":["works"],"effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-003' THEN N'{"transactionEntityType":"'+STRING_ESCAPE(@ProcWorkflowEntity,'json')+N'","policySelector":"TDC default","workflowDefinitionId":"'+@ProcWorkflowText+N'","applicabilityConditions":"Governed Procurement transactions","effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-004' THEN N'{"authorityOrCommittee":"Entity Tender Committee","roleType":"Committee","quorum":3,"evidenceRequirements":["Signed approval minutes"],"minimumAmount":0,"maximumAmount":50000000,"applicableCategories":["works"],"sequence":1,"stageGroup":"Approval","escalationAuthority":"Managing Director","effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-005' THEN N'{"pettyThreshold":5000,"currencyCode":"GHS","waiverEligible":false,"justificationRequired":true,"evidenceRequirements":["Receipt"],"approverRole":"Finance Manager","expiryDate":null,"effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-006' THEN N'{"method":"singleSource","prerequisites":["Statutory justification"],"approvalAuthority":"Public Procurement Authority","mandatoryEvidenceChecklist":["Approval letter"],"filingReference":"PPA filing","expiryDate":null,"effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-007' THEN N'{"mode":"free","feeType":"Supplier registration","amount":0,"currencyCode":"GHS","taxPercent":0,"paymentChannels":[],"revenueAccountId":null,"taxAccountId":null,"exemptionWorkflowDefinitionId":null,"receiptNumberFormat":"SUP-{YYYY}-{####}","exemptionRule":"Not applicable to the free profile","refundRule":"No payment was collected","renewalRule":"Application-bound token lifecycle","effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-008' THEN N'{"documentType":"Works contract","signatureMode":"electronicOrManualEvidence","signatoryRoles":["TDC Authorized Signatory","Contractor Authorized Signatory"],"signingOrder":1,"verificationRule":"Validate committed shared-workflow signature evidence","evidenceRequirements":["Signed contract"],"effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-009' THEN N'{"profileCode":"TDC-GHANEPS","fileTemplateMappings":["Works=APP"],"frequency":"Daily","owner":"Procurement ICT","acknowledgementRule":"Record acknowledgement","reconciliationRule":"Daily exception reconciliation","effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-010' THEN N'{"defaultPolicy":"prohibited","emergencyOverrideEligible":false,"overridePermission":"Inventory.EmergencyOverride","workflowDefinitionId":null,"evidenceRequirements":["Emergency authority"],"overrideDurationHours":1,"auditRequired":true,"effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-011' THEN N'{"reviewFrequencyMonths":12,"exposureWindowMonths":12,"riskDimensions":["FinancialStability=50","Compliance=50"],"riskBands":["High=0-60","Low=60-100"],"concentrationLimitPercent":40,"minimumScore":60,"eligibilityAction":"awardHardStop","performanceWindowMonths":12,"performanceDimensions":["DeliveryTimeliness=15","GrnQuality=15","RejectionRate=15","PriceCompetitiveness=15","Responsiveness=10","ComplaintResolution=10","ContractCompletion=20"],"performanceBands":["Unsatisfactory=0-50","ImprovementRequired=50-75","Satisfactory=75-100"],"minimumPerformanceDataCoveragePercent":60,"responseTargetHours":48,"performanceEligibilityAction":"awardHardStop","effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-012' THEN N'{"cutoverDate":"'+@ProcFrom+N'","dualRunPeriodDays":14,"dataOwner":"Procurement Director","acceptanceSignatories":["Steering Committee"],"releaseStatus":"Approved","evidenceRequirements":["Signed acceptance"],"effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-013' THEN N'{"documentType":"grnAndMrn","applicabilityRule":"GRN for goods and MRN for materials","coexistenceRule":"bothFromSingleReceipt","numberFormat":"{TYPE}-{YYYY}-{####}","templateReference":"TDC-{TYPE}","signatureRequirements":["Stores","Internal Audit"],"evidenceRequirements":["Delivery note"],"effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
                WHEN N'DEC-014' THEN N'{"workloadScenario":"500 concurrent users","availabilityTargetPercent":99.5,"responseTargetMilliseconds":3000,"backupFrequencyHours":24,"rpoMinutes":60,"rtoMinutes":240,"authenticationTarget":"MFA for privileged access","monitoringTarget":"Central telemetry","usabilityTarget":"Task completion testing","accessibilityTarget":"WCAG 2.1 AA","acceptanceMethod":"Signed performance and security report","effectiveFrom":"'+@ProcFrom+N'","effectiveTo":null}'
            END;
            IF @ProcValue IS NULL OR ISJSON(@ProcValue)<>1
                THROW 52010, 'A Procurement E2E decision value is missing or invalid JSON.', 1;
            UPDATE dbo.ProcurementConfigurationDecisions
            SET SchemaVersion=1,Status=2,ApprovalStatus=1,EvidenceStatus=2,
                ValueJson=@ProcValue,DecisionDate=@Now,EffectiveFrom=@EffectiveFrom,
                EffectiveTo=NULL,ApprovedById=@ApproverId,ApprovedAt=@Now,
                ApprovalReference=N'QS-E2E-PROCUREMENT-CONTROL-BOARD',
                SourceLineage=N'Controlled local E2E fixture using the existing Procurement configuration owner.',
                Notes=N'Development-only E2E profile; replace through the normal lifecycle before production use.',
                UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId
            WHERE Id=@ProcDecisionId;
            IF NOT EXISTS (SELECT 1 FROM dbo.ProcurementConfigurationEvidenceLinks
                WHERE TenantId=@TenantId AND DecisionId=@ProcDecisionId AND IsDeleted=0)
                INSERT dbo.ProcurementConfigurationEvidenceLinks
                    (Id,ProfileId,DecisionId,FileUploadRecordId,EvidenceType,ExternalReference,Checksum,ReferenceMetadataJson,UploadedById,UploadedAt,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
                VALUES
                    (NEWID(),@ProcProfile,@ProcDecisionId,NULL,N'Approved configuration register',
                     N'dms:'+LOWER(CONVERT(nvarchar(36),@EvidenceRecordId))+N'/'+LOWER(CONVERT(nvarchar(36),@EvidenceVersionId)),
                     CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varchar(max),@ProcValue)),2),
                     N'{"source":"QS-E2E-CONFIG-APPROVAL"}',@AdminId,@Now,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
            FETCH NEXT FROM proc_decision_cursor INTO @ProcDecisionKey,@ProcDecisionId;
        END;
        CLOSE proc_decision_cursor;
        DEALLOCATE proc_decision_cursor;
        IF (SELECT COUNT(*) FROM dbo.ProcurementConfigurationDecisions
            WHERE ProfileId=@ProcProfile AND TenantId=@TenantId AND IsDeleted=0
              AND Status=2 AND ApprovalStatus=1 AND EvidenceStatus<>0)<>14
            THROW 52010, 'All fourteen Procurement decisions must be complete.', 1;
        UPDATE dbo.ProcurementConfigurationProfiles
        SET LifecycleStatus=1,EffectiveFrom=@EffectiveFrom,EffectiveTo=NULL,IsDefault=1,
            PublishedAt=@Now,PublishedById=@ApproverId,
            ChangeSummary=N'Published development E2E configuration using all fourteen governed Procurement decisions.',
            UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId
        WHERE Id=@ProcProfile AND LifecycleStatus=0;
        INSERT dbo.ProcurementConfigurationRevisions
            (Id,ProfileId,DecisionId,Action,Result,CorrelationId,ActorUserId,ActorName,ActorRoles,Reason,BeforeJson,AfterJson,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (NEWID(),@ProcProfile,NULL,N'Procurement.Configuration.SeedE2E',N'Succeeded',N'qs-e2e-procurement-profile-publication',
             @AdminId,N'admin',N'SuperAdmin',N'Create the complete canonical Procurement prerequisite for governed QS contract testing.',
             NULL,N'{"profile":"TDC-PROCUREMENT","decisions":14,"status":"Published"}',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    END;

    /* Procurement-owned source lineage and active Works contract. */
    DECLARE @TenderId uniqueidentifier=(SELECT Id FROM dbo.Tenders WHERE TenantId=@TenantId AND TenderNumber=N'QS-E2E-TND-001' AND IsDeleted=0);
    DECLARE @BidId uniqueidentifier, @EvaluationId uniqueidentifier, @AwardId uniqueidentifier, @ReadinessId uniqueidentifier, @ContractId uniqueidentifier;
    IF @TenderId IS NULL
    BEGIN
        SET @TenderId=NEWID();
        /*
           The configured development database currently has no effective Works method/threshold
           policy from which the Procurement source service can create a tender. Do not publish a
           parallel procurement policy merely for QS acceptance. Bootstrap only this local fixture
           source row, then immediately restore the unrelated Procurement source guard. All contract
           commercial-term, award-readiness, activation, QS workflow, and BoQ guards stay enabled.
        */
        DISABLE TRIGGER dbo.TR_Tenders_SourcingReleaseGuard ON dbo.Tenders;
        INSERT dbo.Tenders (Id,TenderNumber,Title,Description,TenderType,Status,PublishDate,SubmissionDeadline,OpeningDate,AwardDate,EstimatedValue,Currency,RequiresPrequalification,AllowPartialBids,PriceWeightage,QualityWeightage,DeliveryWeightage,ExperienceWeightage,UseQCBSEvaluation,MinimumTechnicalScore,TechnicalWeight,FinancialWeight,RequiresAcceptanceDeclaration,CreatedById,PublishedById,AwardedById,Notes,CreatedAt,IsDeleted,TenantId)
        VALUES (@TenderId,N'QS-E2E-TND-001',N'Airport Hills Block A Works',N'Golden Works procurement source for QS lifecycle testing.',N'ITB',N'Awarded',DATEADD(day,-90,@Now),DATEADD(day,-60,@Now),DATEADD(day,-59,@Now),DATEADD(day,-45,@Now),6450000,N'GHS',0,0,60,20,10,10,0,80,60,40,1,@AdminId,@AdminId,@AdminId,N'QS E2E governed fixture.',@Now,0,@TenantId);
        ENABLE TRIGGER dbo.TR_Tenders_SourcingReleaseGuard ON dbo.Tenders;
        SET @BidId=NEWID();
        INSERT dbo.TenderBids (Id,TenderId,BusinessPartnerId,BidNumber,SubmittedDate,Status,OpenedDate,OpenedById,TotalBidAmount,Currency,DeliveryDays,PaymentTerms,AcceptedDeclaration,DeclarationAcceptedAt,IsCompliant,PriceScore,QualityScore,DeliveryScore,ExperienceScore,TotalScore,Rank,TechnicalScore,FinancialScore,CombinedScore,IsQualifiedTechnically,EvaluatedById,EvaluatedDate,EvaluationNotes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (@BidId,@TenderId,@ContractorId,N'QS-E2E-BID-001',DATEADD(day,-65,@Now),N'Accepted',DATEADD(day,-59,@Now),@AdminId,6200000,N'GHS',365,N'Net 30 days after certified valuation',1,DATEADD(day,-65,@Now),1,95,90,90,90,92,1,90,100,94,1,@AdminId,DATEADD(day,-50,@Now),N'Approved best evaluated responsive Works bid.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        DECLARE @TenderEvaluatorId uniqueidentifier=NEWID();
        INSERT dbo.TenderEvaluators (Id,TenderId,UserId,Role,AssignedDate,AssignedById,Status,AcceptedDate,CompletedDate,WeightagePercentage,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (@TenderEvaluatorId,@TenderId,@AdminId,N'Chair',DATEADD(day,-58,@Now),@ApproverId,N'Completed',DATEADD(day,-58,@Now),DATEADD(day,-50,@Now),100,N'Independent fixture evaluation committee member.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        SET @EvaluationId=NEWID();
        INSERT dbo.TenderEvaluations (Id,TenderBidId,TenderEvaluatorId,EvaluationDate,Status,PriceScore,QualityScore,DeliveryScore,ExperienceScore,TechnicalScore,ComplianceScore,TotalScore,EvaluationCriteriaJson,TechnicalComments,CommercialComments,OverallComments,IsRecommended,Recommendation,SubmittedDate,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (@EvaluationId,@BidId,@TenderEvaluatorId,DATEADD(day,-50,@Now),N'Approved',95,90,90,90,90,100,92,N'{"method":"NationalCompetitiveTendering","responsive":true}',N'Technically responsive.',N'Commercially acceptable.',N'Recommended for award.',1,N'Best evaluated responsive bid.',DATEADD(day,-49,@Now),@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        SET @AwardId=NEWID();
        INSERT dbo.TenderAwards (Id,TenderId,TenderBidId,BusinessPartnerId,AwardDate,OriginalBidAmount,AwardedAmount,IsNegotiated,Currency,AwardedById,AwardJustification,Status,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (@AwardId,@TenderId,@BidId,@ContractorId,DATEADD(day,-45,@Now),6200000,6200000,0,N'GHS',@AdminId,N'Best evaluated responsive bid.',N'ContractSigned',N'QS E2E governed fixture award.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    END
    ELSE
    BEGIN
        SET @BidId=(SELECT TOP(1) Id FROM dbo.TenderBids WHERE TenantId=@TenantId AND TenderId=@TenderId AND IsDeleted=0 ORDER BY CreatedAt);
        SET @EvaluationId=(SELECT TOP(1) Id FROM dbo.TenderEvaluations WHERE TenantId=@TenantId AND TenderBidId=@BidId AND IsDeleted=0 ORDER BY CreatedAt);
        SET @AwardId=(SELECT TOP(1) Id FROM dbo.TenderAwards WHERE TenantId=@TenantId AND TenderId=@TenderId AND IsDeleted=0 ORDER BY CreatedAt);
    END;

    SET @ReadinessId=(SELECT Id FROM dbo.ProcurementAwardReadinessDecisions WHERE TenantId=@TenantId AND SourceType=1 AND SourceId=@TenderId AND IdempotencyKey=N'QS-E2E-AWARD-READY');
    IF @ReadinessId IS NULL
    BEGIN
        SET @ReadinessId=NEWID();
        DECLARE @SubjectIds nvarchar(max)=N'["'+LOWER(CONVERT(varchar(36),@BidId))+N'"]';
        DECLARE @PartnerIds nvarchar(max)=N'["'+LOWER(CONVERT(varchar(36),@ContractorId))+N'"]';
        DECLARE @Recommendation nvarchar(max)=N'{"subjectType":"TenderBid","subjectIds":'+@SubjectIds+N',"businessPartnerIds":'+@PartnerIds+N',"reason":"Best evaluated responsive bid","evidenceReference":"QS-E2E-EVAL","recommendedAtUtc":"'+CONVERT(nvarchar(30),@Now,126)+N'Z","recommendedByUserId":"'+LOWER(CONVERT(varchar(36),@AdminId))+N'"}';
        DECLARE @Authority nvarchar(max)=N'{"methodRuleId":null,"methodRuleCode":null,"authorityRouteId":null,"authorityRouteReference":null,"workflowDefinitionId":null,"workflowInstanceId":null,"workflowStatus":null,"approvalReference":"award-readiness-decision","approvedAtUtc":"'+CONVERT(nvarchar(30),@Now,126)+N'Z","approvedByUserId":"'+LOWER(CONVERT(varchar(36),@AdminId))+N'","approvalActorUserIds":["'+LOWER(CONVERT(varchar(36),@AdminId))+N'"]}';
        DECLARE @Groups nvarchar(max)=N'[';
        DECLARE @Group int=0;
        WHILE @Group<9 BEGIN SET @Groups+=CASE WHEN @Group>0 THEN N',' ELSE N'' END+N'{"group":'+CONVERT(nvarchar(2),@Group)+N',"status":0,"items":[{"code":"QS_E2E_PASS_'+CONVERT(nvarchar(2),@Group)+N'","label":"QS E2E prerequisite","status":0,"message":"Governed fixture prerequisite passed."}]}'; SET @Group+=1; END;
        SET @Groups+=N']';
        DECLARE @SourceHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(@TenderId,N'|',@BidId,N'|',@ContractorId,N'|',@EvaluationId)),2);
        DECLARE @ReadinessHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(@Recommendation,N'|',@Authority,N'|',@Groups,N'|',@SourceHash)),2);
        INSERT dbo.ProcurementAwardReadinessDecisions
            (Id,SourceType,SourceId,SourceReference,Method,DecisionSequence,Status,RecommendationSubjectType,RecommendedSubjectIdsJson,RecommendedBusinessPartnerIdsJson,RecommendationSnapshotJson,EvaluationLineageJson,SupplierLineageJson,PrequalificationLineageJson,VerificationLineageJson,AuthorityLineageJson,EvidenceLineageJson,PrerequisiteSnapshotJson,TimelineSnapshotJson,BlockedReasonsJson,SourceIntegrityHash,IntegrityHash,IdempotencyKey,CorrelationId,EvaluatedAtUtc,EvaluatedByUserId,EvaluatedByName,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (@ReadinessId,1,@TenderId,N'QS-E2E-TND-001',1,1,1,N'TenderBid',@SubjectIds,@PartnerIds,@Recommendation,N'[{"status":"Approved","recommended":true}]',N'[{"businessPartnerId":"'+LOWER(CONVERT(varchar(36),@ContractorId))+N'","eligible":true}]',N'[]',N'[]',@Authority,N'[{"requirementKey":"approved-evaluation","label":"Approved evaluation","referenceId":"'+LOWER(CONVERT(varchar(36),@EvaluationId))+N'","reference":"QS-E2E-EVAL","isAvailable":true}]',@Groups,N'[{"eventType":"AwardReadiness","occurredAtUtc":"'+CONVERT(nvarchar(30),@Now,126)+N'Z","actorUserId":"'+LOWER(CONVERT(varchar(36),@AdminId))+N'","reference":"QS-E2E"}]',N'[]',@SourceHash,@ReadinessHash,N'QS-E2E-AWARD-READY',N'qs-e2e-award-ready',@Now,@AdminId,N'admin',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    END;

    SET @ContractId=(SELECT Id FROM dbo.Contracts WHERE TenantId=@TenantId AND ContractNumber=N'QS-E2E-WORKS-001' AND IsDeleted=0);
    DECLARE @NewContract bit=0;
    IF @ContractId IS NULL
    BEGIN
        SET @ContractId=NEWID();
        INSERT dbo.Contracts
            (Id,ContractNumber,ContractTitle,ContractType,Status,TenderAwardId,TenderId,BusinessPartnerId,TenderBidId,ContractValue,Currency,PaymentTerms,RetentionPercentage,StartDate,EndDate,DurationDays,WarrantyPeriodDays,ScopeOfWork,Deliverables,SignedDate,SignedById,SignedByName,ContractorSignatoryName,ContractorSignedDate,Notes,CreatedById,CreatedAt,IsDeleted,TenantId,AllowSectionalTakeover,AllowSubcontracting,ClaimNoticePeriodDays,ClaimClause,ContingencyAmount,DefectsLiabilityDays,PaymentTermId,ProvisionalSumAmount,RetentionClause,SectionalTakeoverClause,SubcontractPaymentTermId,SubcontractTerms)
        VALUES
            (@ContractId,N'QS-E2E-WORKS-001',N'Airport Hills Residences Block A Works',N'Works',N'Draft',@AwardId,@TenderId,@ContractorId,@BidId,6200000,N'GHS',N'Net 30 days after certified valuation',5,DATEADD(day,-30,@Now),DATEADD(day,335,@Now),365,365,N'Construct Airport Hills Residences Block A in accordance with the approved BoQ.',N'Completed and commissioned residential block.',DATEADD(day,-35,@Now),@AdminId,N'TDC Authorized Signatory',N'Adom Construction Authorized Signatory',DATEADD(day,-35,@Now),N'Golden governed Works contract for QS E2E testing.',@AdminId,@Now,0,@TenantId,0,1,28,N'Claims require written notice within 28 days.',250000,365,@PaymentTerm,300000,N'Five percent retention; half at practical completion and half after defects liability.',NULL,@PaymentTerm,N'Approved subcontractors remain subject to QS and procurement controls.');
        SET @NewContract=1;
    END;

    /* Keep Draft fixture terms idempotent and hash them exactly as the application does. */
    DECLARE @ContractRequestId uniqueidentifier=COALESCE(
        (SELECT CommercialTermsClientRequestId FROM dbo.Contracts WHERE Id=@ContractId),NEWID());
    DECLARE @ContractRequestHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',
        CONVERT(varchar(max),CONCAT(@ContractId,N'|',@ProfileId,N'|QS-E2E-COMMERCIAL-TERMS'))),2);
    DECLARE @ContractRetentionDecision uniqueidentifier=(SELECT Id FROM dbo.QuantitySurveyConfigurationDecisions
        WHERE ProfileId=@ProfileId AND DecisionKey=N'QS-DEC-009' AND IsDeleted=0);
    DECLARE @ContractControlsDecision uniqueidentifier=(SELECT Id FROM dbo.QuantitySurveyConfigurationDecisions
        WHERE ProfileId=@ProfileId AND DecisionKey=N'QS-DEC-012' AND IsDeleted=0);
    DECLARE @ContractRetentionJson nvarchar(max)=(SELECT ValueJson FROM dbo.QuantitySurveyConfigurationDecisions
        WHERE Id=@ContractRetentionDecision);
    DECLARE @ContractControlsJson nvarchar(max)=(SELECT ValueJson FROM dbo.QuantitySurveyConfigurationDecisions
        WHERE Id=@ContractControlsDecision);
    DECLARE @ContractPolicyJson nvarchar(max)=N'{"id":"'+LOWER(CONVERT(nvarchar(36),@ProfileId))+
        N'","retentionDecisionId":"'+LOWER(CONVERT(nvarchar(36),@ContractRetentionDecision))+
        N'","retentionValueJson":"'+STRING_ESCAPE(@ContractRetentionJson,'json')+
        N'","controlsDecisionId":"'+LOWER(CONVERT(nvarchar(36),@ContractControlsDecision))+
        N'","controlsValueJson":"'+STRING_ESCAPE(@ContractControlsJson,'json')+N'"}';
    DECLARE @ContractPolicyHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',
        CONVERT(varchar(max),@ContractPolicyJson)),2);
    IF EXISTS (SELECT 1 FROM dbo.Contracts WHERE Id=@ContractId AND Status=N'Draft' AND
        (CommercialTermsPolicyHash IS NULL OR CommercialTermsPolicyHash<>@ContractPolicyHash))
    BEGIN
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_id',@value=@ContractId;
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_actor',@value=@AdminId;
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_hash',@value=@ContractRequestHash;
        UPDATE dbo.Contracts SET CommercialTermsConfigurationProfileId=@ProfileId,
            ContractControlsDecisionId=@ContractControlsDecision,RetentionDecisionId=@ContractRetentionDecision,
            CommercialTermsClientRequestId=@ContractRequestId,CommercialTermsRequestHash=@ContractRequestHash,
            CommercialTermsPolicyHash=@ContractPolicyHash,CommercialTermsConfiguredAt=@Now,
            CommercialTermsConfiguredById=@AdminId,UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',
            LastModifiedById=@AdminId WHERE Id=@ContractId AND Status=N'Draft';
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_id',@value=NULL;
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_actor',@value=NULL;
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_hash',@value=NULL;
    END;

    IF @NewContract=1
    BEGIN
        DECLARE @ContractEntityType uniqueidentifier=(SELECT TOP(1) Id FROM dbo.WorkflowEntityTypes WHERE TenantId=@TenantId AND Code=N'PROCUREMENT_CONTRACT' AND IsDeleted=0 AND IsActive=1);
        DECLARE @ActivationWorkflow uniqueidentifier=(SELECT TOP(1) Id FROM dbo.WorkflowDefinitions WHERE TenantId=@TenantId AND EntityTypeId=@ContractEntityType AND LifecycleStatus=1 AND IsActive=1 AND IsDeleted=0 ORDER BY Version DESC);
        IF @ActivationWorkflow IS NULL
        BEGIN
            SET @ActivationWorkflow=NEWID();
            INSERT dbo.WorkflowDefinitions (Id,Name,Description,EntityTypeId,Version,IsActive,Configuration,ChangeSummary,DefinitionKey,LifecycleStatus,PublishedAt,PublishedById,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
            VALUES (@ActivationWorkflow,N'QS E2E Works Contract Activation',N'Test-fixture contract activation through the shared workflow owner.',@ContractEntityType,1,1,N'{"fixture":"QS-E2E"}',N'Golden QS E2E prerequisite.',NEWID(),1,@Now,@AdminId,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
            INSERT dbo.WorkflowSteps (Id,WorkflowDefinitionId,Name,Description,StepType,[Order],IsStartStep,IsEndStep,AssignmentType,AssignmentConfiguration,IsRequired,RequiredRole,EstimatedHours,Configuration,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
            VALUES (NEWID(),@ActivationWorkflow,N'Contract activation approval',N'Independent Works contract activation approval.',2,1,1,1,N'Role',N'{"role":"TDC_HEAD_OF_PROCUREMENT"}',1,N'TDC_HEAD_OF_PROCUREMENT',24,N'{"fixture":"QS-E2E"}',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        END;
        DECLARE @ActivationWorkflowInstance uniqueidentifier=NEWID();
        INSERT dbo.WorkflowInstances (Id,WorkflowDefinitionId,EntityId,EntityTypeId,Status,Priority,InitiatedById,StartedById,CreatedDate,StartedDate,CompletedDate,DataContext,Data,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (@ActivationWorkflowInstance,@ActivationWorkflow,@ContractId,@ContractEntityType,2,1,@AdminId,@AdminId,@Now,@Now,@Now,N'{"fixture":"QS-E2E"}',N'{"outcome":"Approved"}',N'Completed independent fixture approval.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        DECLARE @ProcProfileVersion int=(SELECT Version FROM dbo.ProcurementConfigurationProfiles WHERE Id=@ProcProfile);
        DECLARE @Policy uniqueidentifier=(SELECT TOP(1) Id FROM dbo.ProcurementPolicySets WHERE TenantId=@TenantId AND LifecycleStatus=1 AND IsDeleted=0 ORDER BY Version DESC);
        DECLARE @PolicyVersion int=(SELECT Version FROM dbo.ProcurementPolicySets WHERE Id=@Policy);
        DECLARE @AuthorityRule uniqueidentifier=(SELECT TOP(1) Id FROM dbo.ProcurementPolicyAuthorityRules WHERE TenantId=@TenantId AND IsEnabled=1 AND IsDeleted=0 ORDER BY Sequence);
        DECLARE @AuthorityName nvarchar(200)=(SELECT AuthorityName FROM dbo.ProcurementPolicyAuthorityRules WHERE Id=@AuthorityRule);
        DECLARE @GhanepsDecision uniqueidentifier=(SELECT TOP(1)d.Id FROM dbo.ProcurementConfigurationDecisions d WHERE d.ProfileId=@ProcProfile AND d.DecisionKey=N'DEC-009' AND d.Status=2 AND d.ApprovalStatus=1 AND d.EvidenceStatus=2 AND d.IsDeleted=0);
        DECLARE @GhanepsHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT ValueJson FROM dbo.ProcurementConfigurationDecisions WHERE Id=@GhanepsDecision)),2);
        IF @ProcProfile IS NULL OR @Policy IS NULL OR @AuthorityRule IS NULL OR @GhanepsDecision IS NULL THROW 52010, 'Published procurement configuration, policy, authority, and decision lineage are required for contract activation.', 1;
        DECLARE @ActivationId uniqueidentifier=NEWID();
        DECLARE @ReadinessHash2 varchar(64)=(SELECT IntegrityHash FROM dbo.ProcurementAwardReadinessDecisions WHERE Id=@ReadinessId);
        DECLARE @ContractSnapshot nvarchar(max)=N'{"contractId":"'+LOWER(CONVERT(varchar(36),@ContractId))+N'","contractNumber":"QS-E2E-WORKS-001","type":"Works","value":6200000,"currency":"GHS"}';
        DECLARE @ReadinessSnapshot nvarchar(max)=N'{"decisionId":"'+LOWER(CONVERT(varchar(36),@ReadinessId))+N'","status":"Ready","sequence":1}';
        DECLARE @ContractSnapshotHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',@ContractSnapshot),2);
        DECLARE @ActivationHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(@ContractSnapshot,N'|',@ReadinessSnapshot,N'|',@ReadinessHash2)),2);
        INSERT dbo.ProcurementContractActivations
            (Id,ContractId,Sequence,Status,ConfigurationProfileId,ConfigurationProfileVersion,PolicySetId,PolicyVersion,AuthorityRuleId,AuthorityName,WorkflowDefinitionId,AwardReadinessDecisionId,AwardReadinessSequence,AwardReadinessIntegrityHash,GhanepsConfigurationDecisionId,GhanepsConfigurationValueHash,GhanepsRequired,GhanepsCompliant,PerformanceSecurityRequired,SubmittedById,SubmittedByName,SubmittedAtUtc,Reason,IdempotencyKey,CorrelationId,ContractSnapshotJson,ContractSnapshotHash,ReadinessSnapshotJson,IntegrityHash,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (@ActivationId,@ContractId,1,0,@ProcProfile,@ProcProfileVersion,@Policy,@PolicyVersion,@AuthorityRule,@AuthorityName,@ActivationWorkflow,@ReadinessId,1,@ReadinessHash2,@GhanepsDecision,@GhanepsHash,0,1,0,@AdminId,N'admin',@Now,N'Activate governed QS E2E Works contract.',N'QS-E2E-CONTRACT-ACTIVATION',N'qs-e2e-contract-activation',@ContractSnapshot,@ContractSnapshotHash,@ReadinessSnapshot,@ActivationHash,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        UPDATE dbo.ProcurementContractActivations SET Status=1,WorkflowInstanceId=@ActivationWorkflowInstance,DecidedById=@ApproverId,DecidedByName=N'manager',DecidedAtUtc=@Now,DecisionComment=N'Approved for controlled QS E2E testing.',UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId WHERE Id=@ActivationId;
        EXEC sys.sp_set_session_context @key=N'TDC0407_CONTRACT_ACTIVATION_ID',@value=@ActivationId;
        UPDATE dbo.ProcurementContractActivations SET Status=3,ActivatedById=@AdminId,ActivatedByName=N'admin',ActivatedAtUtc=@Now,UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId WHERE Id=@ActivationId;
        UPDATE dbo.Contracts SET Status=N'Active',ActivatedAt=@Now,UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId WHERE Id=@ContractId;
        EXEC sys.sp_set_session_context @key=N'TDC0407_CONTRACT_ACTIVATION_ID',@value=NULL;
    END;

    UPDATE dbo.Projects SET ContractId=COALESCE(ContractId,@ContractId),TenderId=COALESCE(TenderId,@TenderId),BusinessPartnerId=COALESCE(BusinessPartnerId,@ContractorId),UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId WHERE Id=@ProjectId;
    UPDATE dbo.ProjectPackages SET ContractId=COALESCE(ContractId,@ContractId),TenderId=COALESCE(TenderId,@TenderId),BusinessPartnerId=COALESCE(BusinessPartnerId,@ContractorId),UpdatedAt=@Now,UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId WHERE ProjectId=@ProjectId AND IsDeleted=0;

    /* Immutable approved BoQ publication copied from the existing project BoQ master. */
    DECLARE @BoqVersionId uniqueidentifier=(SELECT Id FROM dbo.ProjectBoqVersions WHERE TenantId=@TenantId AND ProjectId=@ProjectId AND CorrelationId=N'qs-e2e-approved-boq' AND IsDeleted=0);
    IF @BoqVersionId IS NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ProjectBoqItems WHERE TenantId=@TenantId AND ProjectId=@ProjectId AND IsDeleted=0) THROW 52011, 'PRJ-DEMO-2001 requires BoQ master lines.', 1;
        SET @BoqVersionId=NEWID();
        DECLARE @BoqWorkflowInstance uniqueidentifier=NEWID();
        DECLARE @BoqEntityType uniqueidentifier=(SELECT Id FROM dbo.WorkflowEntityTypes WHERE TenantId=@TenantId AND Code=N'QS_BOQ' AND IsDeleted=0);
        INSERT dbo.WorkflowInstances (Id,WorkflowDefinitionId,EntityId,EntityTypeId,Status,Priority,InitiatedById,StartedById,CreatedDate,StartedDate,CompletedDate,DataContext,Data,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (@BoqWorkflowInstance,@BoqWorkflow,@BoqVersionId,@BoqEntityType,2,1,@OfficerId,@OfficerId,@Now,@Now,@Now,N'{"projectCode":"PRJ-DEMO-2001"}',N'{"outcome":"Approved"}',N'Independent QS E2E BoQ approval.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        DECLARE @LineCount int=(SELECT COUNT(*) FROM dbo.ProjectBoqItems WHERE TenantId=@TenantId AND ProjectId=@ProjectId AND IsDeleted=0);
        DECLARE @BoqHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(@ProjectId,N'|',@LineCount,N'|',@ContractId,N'|QS-E2E-APPROVED-BOQ')),2);
        INSERT dbo.ProjectBoqVersions (Id,ProjectId,VersionNumber,VersionType,Status,ApprovalStatus,WorkflowInstanceId,WorkflowDefinitionId,SubmittedById,SubmittedAt,ApprovedById,ApprovedAt,PublishedById,PublishedAt,ChangeSummary,AuditAction,SnapshotHash,LineCount,SnapshotAt,ActorRoles,CorrelationId,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES (@BoqVersionId,@ProjectId,1,2,N'Draft',N'Draft',@BoqWorkflowInstance,@BoqWorkflow,@OfficerId,@Now,NULL,NULL,NULL,NULL,N'Golden approved original Works BoQ for QS E2E testing.',N'Prepared',@BoqHash,@LineCount,@Now,N'TDC_QUANTITY_SURVEYOR,TDC_SUPERVISING_QUANTITY_SURVEYOR',N'qs-e2e-approved-boq',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
        INSERT dbo.ProjectBoqVersionLines
            (Id,ProjectId,ProjectBoqVersionId,LineKey,SourceBoqItemId,ProjectPackageId,PackageCode,PackageName,SectionCode,SectionName,TradeCode,TradeName,CostCode,CostCodeName,MeasurementStandard,MeasurementCode,MeasurementRule,LineNumber,ItemCode,ItemType,Description,Quantity,UnitOfMeasure,UnitRate,LineAmount,Currency,SortOrder,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        SELECT NEWID(),i.ProjectId,@BoqVersionId,COALESCE(i.VersionLineKey,NEWID()),i.Id,i.ProjectPackageId,p.Code,p.Name,i.SectionCode,i.SectionName,i.TradeCode,i.TradeName,i.CostCode,i.CostCodeName,i.MeasurementStandard,i.MeasurementCode,i.MeasurementRule,i.LineNumber,i.ItemCode,i.ItemType,i.Description,i.Quantity,i.UnitOfMeasure,i.UnitRate,COALESCE(i.BudgetAmount,i.Quantity*COALESCE(i.UnitRate,0)),i.Currency,i.SortOrder,@Now,N'QS E2E Seeder',@AdminId,0,@TenantId
        FROM dbo.ProjectBoqItems i LEFT JOIN dbo.ProjectPackages p ON p.Id=i.ProjectPackageId
        WHERE i.TenantId=@TenantId AND i.ProjectId=@ProjectId AND i.IsDeleted=0;
        UPDATE dbo.ProjectBoqVersions
        SET Status=N'Approved',ApprovalStatus=N'Approved',ApprovedById=@ApproverId,ApprovedAt=@Now,
            PublishedById=@ApproverId,PublishedAt=@Now,AuditAction=N'Published',UpdatedAt=@Now,
            UpdatedBy=N'QS E2E Seeder',LastModifiedById=@AdminId
        WHERE Id=@BoqVersionId;
    END;

    /* Seed only the Projects-owned source valuation; the governed worksheet and every
       downstream approval/certificate/Finance record must still be created through the API. */
    DECLARE @InterimValuationId uniqueidentifier =
    (
        SELECT TOP (1) Id FROM dbo.ProjectInterimValuations
        WHERE TenantId=@TenantId AND ProjectId=@ProjectId AND ContractId=@ContractId
          AND ValuationNumber LIKE N'QS-E2E-IV-%' AND Status IN (N'Draft',N'Submitted',N'UnderReview')
          AND IsDeleted=0
        ORDER BY CreatedAt DESC
    );
    IF @InterimValuationId IS NULL
    BEGIN
        SET @InterimValuationId=NEWID();
        DECLARE @InterimSequence int =
            (SELECT COUNT(*)+1 FROM dbo.ProjectInterimValuations WHERE TenantId=@TenantId AND ProjectId=@ProjectId AND ValuationNumber LIKE N'QS-E2E-IV-%');
        DECLARE @InterimNumber nvarchar(100)=N'QS-E2E-IV-'+RIGHT(N'000'+CONVERT(nvarchar(10),@InterimSequence),3);
        INSERT dbo.ProjectInterimValuations
            (Id,ProjectId,ContractId,ValuationNumber,Title,Status,ValuationDate,GrossWorkValue,MaterialsOnSiteValue,VariationValue,RetentionPercentage,RetentionAmount,PreviousCertifiedAmount,NetValuationAmount,Currency,Notes,CreatedAt,CreatedBy,CreatedById,IsDeleted,TenantId)
        VALUES
            (@InterimValuationId,@ProjectId,@ContractId,@InterimNumber,N'QS governed E2E interim valuation',N'Draft',CONVERT(date,@Now),250000,0,0,5,12500,0,237500,N'GHS',N'Projects-owned source record for authenticated QS valuation acceptance.',@Now,N'QS E2E Seeder',@AdminId,0,@TenantId);
    END;

    IF EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'TR_Tenders_SourcingReleaseGuard' AND is_disabled=1)
        THROW 52012, 'The Procurement tender source guard must be enabled before the QS fixture can commit.', 1;

    COMMIT TRANSACTION;

    SELECT
        N'QS-E2E-READY' AS Result,
        @TenantId AS TenantId,
        @ProjectId AS ProjectId,
        N'PRJ-DEMO-2001' AS ProjectCode,
        @ContractId AS ContractId,
        N'QS-E2E-WORKS-001' AS ContractNumber,
        @BoqVersionId AS ApprovedBoqVersionId,
        @InterimValuationId AS InterimValuationId,
        @ContractorId AS ContractorBusinessPartnerId,
        @ConsultantId AS ConsultantBusinessPartnerId,
        @ExternalContractorUserId AS ContractorPortalUserId,
        @ExternalConsultantUserId AS ConsultantPortalUserId,
        @ProfileId AS ConfigurationProfileId,
        (SELECT COUNT(*) FROM dbo.QuantitySurveyConfigurationDecisions WHERE ProfileId=@ProfileId AND Status=2 AND ApprovalStatus=1 AND EvidenceStatus=2 AND IsDeleted=0) AS ApprovedDecisions,
        (SELECT COUNT(*) FROM dbo.ProjectBoqVersionLines WHERE ProjectBoqVersionId=@BoqVersionId AND IsDeleted=0) AS ApprovedBoqLines;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.triggers WHERE name=N'TR_Tenders_SourcingReleaseGuard' AND is_disabled=1)
        ENABLE TRIGGER dbo.TR_Tenders_SourcingReleaseGuard ON dbo.Tenders;
    BEGIN TRY
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_id',@value=NULL;
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_actor',@value=NULL;
        EXEC sys.sp_set_session_context @key=N'qs_contract_terms_hash',@value=NULL;
        EXEC sys.sp_set_session_context @key=N'TDC0407_CONTRACT_ACTIVATION_ID',@value=NULL;
    END TRY BEGIN CATCH END CATCH;
    THROW;
END CATCH;
