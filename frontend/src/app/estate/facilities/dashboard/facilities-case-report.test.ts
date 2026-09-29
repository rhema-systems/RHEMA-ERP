import { expect, it } from 'vitest';

import type { ProcedureCaseSummary } from '@/services/procedure-case.service';
import { facilitiesCaseReportRows } from './facilities-case-report';

it('exports the loaded case reference, owner and current stage', () => {
  const item: ProcedureCaseSummary = {
    id: 'case-1',
    module: 'Facilities',
    entityType: 'EstateFacilityComplaint',
    title: 'Water leak',
    referenceNumber: 'FAC-001',
    applicantName: 'Ada',
    status: 'In Progress',
    currentStageIndex: 2,
    currentStageName: 'Complaint Resolution Review',
    currentAssignedRole: 'Facilities Officer',
    usesConfiguredWorkflow: true,
    createdAt: '2026-09-25T08:00:00Z',
  };

  expect(facilitiesCaseReportRows([item])).toEqual([{
    Reference: 'FAC-001',
    Title: 'Water leak',
    Type: 'EstateFacilityComplaint',
    Applicant: 'Ada',
    Status: 'In Progress',
    Stage: 'Complaint Resolution Review',
    'Assigned role': 'Facilities Officer',
    Created: '2026-09-25',
    Updated: '',
  }]);
});
