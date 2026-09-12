import { describe, expect, it } from 'vitest';
import {
  getPrequalificationActions,
  prequalificationApplicationStatusLabel,
  prequalificationStatusLabel,
  validatePrequalificationDraft,
} from './procurement-prequalification';
import {
  ProcurementPrequalificationApplicationStatus as ApplicationStatus,
  ProcurementPrequalificationStatus as Status,
  ProcurementQualifiedListEntryStatus as EntryStatus,
  type CreateProcurementPrequalificationExercise,
  type ProcurementPrequalificationExercise,
} from '@/types/procurement-prequalification';

const request = (): CreateProcurementPrequalificationExercise => ({
  reference: 'PQ-2026-001',
  title: 'Works contractors',
  description: 'Prequalify works contractors with statutory and technical evidence.',
  categoryIds: ['category-1'],
  opensAtUtc: '2026-07-23T10:00:00.000Z',
  closesAtUtc: '2026-07-30T10:00:00.000Z',
  validityMonths: 12,
  passingScore: 70,
  policySetId: 'policy-1',
  workflowDefinitionId: 'workflow-1',
  criteria: [
    { code: 'LEGAL', name: 'Legal', weight: 40, minimumScore: 60, isMandatory: true, requiresEvidence: true, sortOrder: 1 },
    { code: 'TECH', name: 'Technical', weight: 60, minimumScore: 60, isMandatory: true, requiresEvidence: true, sortOrder: 2 },
  ],
});

const exercise = (status: Status): ProcurementPrequalificationExercise => ({
  id: 'exercise-1',
  reference: 'PQ-2026-001',
  title: 'Works contractors',
  description: 'Controlled workflow',
  status,
  opensAtUtc: '2026-07-23T10:00:00.000Z',
  closesAtUtc: '2026-07-30T10:00:00.000Z',
  applicationCount: 1,
  qualifiedCount: 0,
  validityMonths: 12,
  passingScore: 70,
  policySetId: 'policy-1',
  policySetCode: 'TDC-POLICY',
  policySetVersion: 3,
  sourceConfigurationProfileId: 'profile-1',
  workflowDefinitionId: 'workflow-1',
  integrityHash: 'a'.repeat(64),
  rowVersion: 'AQID',
  categories: [],
  criteria: [],
  applications: [{
    id: 'application-1',
    applicationNumber: 'PQ-2026-001-APP-0001',
    businessPartnerId: 'partner-1',
    supplierName: 'Supplier',
    status: ApplicationStatus.EvaluatedQualified,
    submittedAtUtc: '2026-07-24T10:00:00.000Z',
    totalScore: 80,
    passed: true,
    rowVersion: 'AQID',
    categoryIds: ['category-1'],
    evidence: [],
    scores: [],
  }],
  qualifiedEntries: [],
  milestones: [],
});

describe('procurement prequalification presentation', () => {
  it('accepts a complete locked draft', () => {
    expect(validatePrequalificationDraft(request())).toBeNull();
  });

  it('omits workflow selection only after the server explicitly confirms no process', () => {
    const direct = request();
    direct.workflowDefinitionId = null;
    expect(validatePrequalificationDraft(direct, false)).toBeNull();
    expect(validatePrequalificationDraft(direct)).toContain('workflow');
    direct.criteria[0].weight = 1;
    expect(validatePrequalificationDraft(direct, false)).toContain('100');
  });

  it('rejects criterion weights that do not total 100', () => {
    const invalid = request();
    invalid.criteria[1].weight = 50;
    expect(validatePrequalificationDraft(invalid)).toContain('100');
  });

  it('requires at least one mandatory criterion', () => {
    const invalid = request();
    invalid.criteria.forEach((item) => { item.isMandatory = false; });
    expect(validatePrequalificationDraft(invalid)).toContain('mandatory');
  });

  it('opens approval only after every application is evaluated', () => {
    const item = exercise(Status.UnderEvaluation);
    expect(getPrequalificationActions(item, new Date('2026-07-31T00:00:00Z')).canSubmitDecision).toBe(true);
    item.applications[0].status = ApplicationStatus.Submitted;
    expect(getPrequalificationActions(item, new Date('2026-07-31T00:00:00Z')).canSubmitDecision).toBe(false);
  });

  it('derives due expiry and exposes stable status labels', () => {
    const item = exercise(Status.Approved);
    item.qualifiedEntries = [{
      id: 'entry-1',
      businessPartnerId: 'partner-1',
      supplierName: 'Supplier',
      categoryId: 'category-1',
      categoryCode: 'WORKS',
      categoryName: 'Works',
      validFromUtc: '2026-01-01T00:00:00Z',
      expiresAtUtc: '2026-07-01T00:00:00Z',
      status: EntryStatus.Active,
      approvalReference: 'APPROVAL-1',
      integrityHash: 'b'.repeat(64),
    }];
    expect(getPrequalificationActions(item, new Date('2026-07-23T00:00:00Z')).canExpire).toBe(true);
    expect(prequalificationStatusLabel[Status.Approved]).toContain('qualified');
    expect(prequalificationApplicationStatusLabel[ApplicationStatus.Approved]).toBe('Qualified');
  });
});
