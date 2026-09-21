/* Existing QS E2E fixtures only. The caller must supply @TenantId and @ActorId
   and run this in a transaction. Retain the published template identity and all
   access/retention settings. Customer-authored requirements are never changed.
   This is the measurement mapping already used by seed-quantity-survey-e2e.sql. */
UPDATE dbo.CentralDocumentMetadataTemplates
SET RequiredFieldsJson=N'["measurementSheetId","projectId","boqLineKey","evidenceType","checksumSha256"]',
    UpdatedAt=SYSUTCDATETIME(),
    UpdatedBy=N'QS measurement template reconciliation',
    LastModifiedById=@ActorId
WHERE TenantId=@TenantId
  AND TemplateCode=N'QS-MEAS-EVD'
  AND Module=N'QuantitySurvey'
  AND DocumentType=N'QS measurement evidence'
  AND AccessProfile=N'QS project and audit scope'
  AND CreatedBy=N'QS E2E Seeder'
  AND IsDeleted=0
  AND RequiredFieldsJson=N'["projectId","contractId","recordReference","evidenceDate"]';
