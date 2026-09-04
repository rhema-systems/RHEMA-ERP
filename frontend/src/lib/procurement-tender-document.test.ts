import { describe, expect, it } from 'vitest';

import {
  applyTenderDocumentContentArtifact,
  hasTenderDocumentAction,
  isTenderDocumentContentArtifactApproved,
  pendingMandatoryAcknowledgements,
  validateTenderDocumentChange,
  validateTenderDocumentIssue,
  validateTenderDocumentTemplate,
} from './procurement-tender-document';
import type {
  CreateProcurementTenderDocumentChangeRequest,
  IssueProcurementTenderDocumentRegisterRequest,
  ProcurementTenderDocumentChange,
  SaveProcurementTenderDocumentTemplate,
} from '@/types/procurement-tender-document';

const template = (): SaveProcurementTenderDocumentTemplate => ({
  templateCode: 'TDC-NCT-GOODS',
  name: 'Standard NCT goods document',
  documentTypeCode: 'TENDER-DOCUMENT',
  effectiveFromUtc: '2026-08-01T00:00:00.000Z',
  policySetId: 'policy-1',
  policySetCode: 'TDC-POLICY',
  policySetVersion: 3,
  sourceConfigurationProfileId: 'profile-1',
  contentReference: 'workflow-evidence://document/1',
  contentWorkflowEvidenceDocumentId: 'evidence-1',
  contentChecksumSha256: 'a'.repeat(64),
  workflowDefinitionId: 'workflow-1',
  applicableMethods: ['NationalCompetitiveTendering'],
});

const issue = (): IssueProcurementTenderDocumentRegisterRequest => ({
  sourceType: 'Tender',
  sourceId: 'tender-1',
  recipientName: 'Approved Supplier',
  amountPaid: 250,
  paymentReference: 'PAY-001',
  receiptNumber: 'RECEIPT-001',
  issueChannel: 'ExternalPortal',
  evidenceReference: 'workflow-evidence://payment/1',
  registerRowVersion: 'AQID',
});

const change = (
  changeType: CreateProcurementTenderDocumentChangeRequest['changeType']
): CreateProcurementTenderDocumentChangeRequest => ({
  sourceType: 'Tender',
  sourceId: 'tender-1',
  changeType,
  requiresAcknowledgement: true,
  reason: 'Approved correction to the controlled issue.',
  workflowDefinitionId: 'workflow-1',
  evidenceReference: 'workflow-evidence://change/1',
  registerRowVersion: 'AQID',
});

describe('controlled tender document presentation', () => {
  it('requires exact policy, workflow, method, content, and checksum lineage', () => {
    expect(validateTenderDocumentTemplate(template())).toBeUndefined();
    expect(
      validateTenderDocumentTemplate({ ...template(), contentChecksumSha256: 'short' })
    ).toContain('SHA-256');
    expect(
      validateTenderDocumentTemplate({ ...template(), applicableMethods: [] })
    ).toContain('procurement method');
    expect(
      validateTenderDocumentTemplate({
        ...template(),
        contentWorkflowEvidenceDocumentId: undefined,
      })
    ).toContain('workflow evidence document');
    expect(
      validateTenderDocumentTemplate({
        ...template(),
        effectiveFromUtc: 'not-a-date',
      })
    ).toContain('invalid');
  });

  it('allows metadata-only Draft creation while retaining strict publication validation', () => {
    const metadataOnly = {
      ...template(),
      contentWorkflowEvidenceDocumentId: undefined,
      contentReference: '',
      contentChecksumSha256: '',
    };

    expect(
      validateTenderDocumentTemplate(metadataOnly, { requireContent: false })
    ).toBeUndefined();
    expect(validateTenderDocumentTemplate(metadataOnly)).toContain(
      'workflow evidence document'
    );
    expect(
      validateTenderDocumentTemplate(
        { ...metadataOnly, contentReference: 'partial/path' },
        { requireContent: false }
      )
    ).toContain('workflow evidence document');
  });

  it('derives the immutable content ID, path, and checksum from one approved artifact', () => {
    const artifact = {
      id: 'evidence-2',
      documentName: 'NCT source document',
      fileName: 'nct.docx',
      filePath: 'workflow-evidence/default/nct.docx',
      sha256: 'b'.repeat(64),
      version: 2,
      isCurrent: true,
      verificationStatus: 1,
      malwareScanStatus: 1,
      workflowInstanceId: 'instance-1',
      workflowName: 'Document control',
      entityType: 'TenderDocument',
      entityId: 'entity-1',
      stepName: 'Content verification',
    };

    expect(isTenderDocumentContentArtifactApproved(artifact)).toBe(true);
    expect(
      applyTenderDocumentContentArtifact(
        { ...template(), contentFileUploadRecordId: 'legacy-upload' },
        artifact
      )
    ).toMatchObject({
      contentWorkflowEvidenceDocumentId: 'evidence-2',
      contentFileUploadRecordId: undefined,
      contentReference: 'workflow-evidence/default/nct.docx',
      contentChecksumSha256: 'b'.repeat(64),
    });
    expect(
      isTenderDocumentContentArtifactApproved({
        ...artifact,
        malwareScanStatus: 0,
      })
    ).toBe(false);
    expect(
      isTenderDocumentContentArtifactApproved({
        ...artifact,
        verificationStatus: 0,
      })
    ).toBe(false);
  });

  it('enforces exact paid issue data and rejects payment on a free issue', () => {
    expect(validateTenderDocumentIssue(issue(), 'Paid', 250)).toBeUndefined();
    expect(
      validateTenderDocumentIssue(
        { ...issue(), paymentReference: '' },
        'Paid',
        250
      )
    ).toContain('payment reference');
    expect(validateTenderDocumentIssue(issue(), 'Free', 0)).toContain(
      'cannot carry'
    );
  });

  it('rejects retroactive or non-forward extensions', () => {
    const request = {
      ...change('SubmissionDeadlineExtension'),
      newValueUtc: '2026-08-10T00:00:00.000Z',
    };
    const register = {
      effectiveSubmissionDeadlineUtc: '2026-08-12T00:00:00.000Z',
      effectiveBidValidityUntilUtc: '2026-09-12T00:00:00.000Z',
    };
    expect(
      validateTenderDocumentChange(
        request,
        register,
        new Date('2026-08-01T00:00:00.000Z')
      )
    ).toContain('move forward');
  });

  it('requires the exact approved replacement version for an addendum', () => {
    const register = {
      effectiveSubmissionDeadlineUtc: '2026-08-12T00:00:00.000Z',
      effectiveBidValidityUntilUtc: '2026-09-12T00:00:00.000Z',
    };
    expect(
      validateTenderDocumentChange(
        change('Addendum'),
        register,
        new Date('2026-08-01T00:00:00.000Z')
      )
    ).toContain('approved replacement version');
  });

  it('reports pending acknowledgements and trusts server allowed actions', () => {
    const controlledChange = {
      status: 'Approved',
      requiresAcknowledgement: true,
      recipients: [
        { acknowledgement: undefined },
        { acknowledgement: { outcome: 'Acknowledged' } },
      ],
    } as ProcurementTenderDocumentChange;
    expect(pendingMandatoryAcknowledgements(controlledChange)).toBe(1);
    expect(
      hasTenderDocumentAction(['IssueDocument'], 'issuedocument')
    ).toBe(true);
    expect(hasTenderDocumentAction([], 'IssueDocument')).toBe(false);
  });
});
