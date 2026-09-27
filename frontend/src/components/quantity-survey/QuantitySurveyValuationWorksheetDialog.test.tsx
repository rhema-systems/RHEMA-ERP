import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { QuantitySurveyValuationWorksheetDialog } from './QuantitySurveyValuationWorksheetDialog';
import { quantitySurveyValuationWorksheetService as service } from '@/services/quantity-survey-valuation-worksheet.service';

vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));
vi.mock('@/services/quantity-survey-valuation-worksheet.service', () => ({
  quantitySurveyValuationWorksheetService: { lookups: vi.fn(), get: vi.fn(), save: vi.fn() },
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); });

it('preserves the reviewed quantity, note and retry identity when the server rejects a save', async () => {
  vi.mocked(service.lookups).mockResolvedValue({ approvedBoqVersions: [
    { id: 'boq-4', label: 'Approved BOQ v4', versionNumber: 4, lineCount: 1 },
  ], contractors: [], consultants: [] });
  vi.mocked(service.get).mockResolvedValue({
    projectId: 'project-1', projectInterimValuationId: 'valuation-1', interimValuationLabel: 'Valuation 1',
    boqVersionNumber: 4, supportingEvidenceRequired: true, portalIdentityRequired: false,
    externalSignatureRequired: false, contractorAttestationText: '', consultantAttestationText: '',
    certificateReady: false, measuredToDateValue: 10000, previouslyCertifiedValue: 0,
    currentClaimedValue: 10000, currentCertifiedValue: 10000, currentPeriodCertifiedValue: 10000,
    disputedValue: 0, retentionToDateValue: 0, currentRetentionValue: 0, netCurrentValue: 10000,
    projectBoqVersionId: 'boq-4', status: 'Draft', approvalStatus: 'Draft', retentionPercentage: 0,
    evidence: [], contractorSubmissionRequired: false, consultantEndorsementRequired: false,
    lines: [{ projectBoqVersionLineId: 'line-1', label: '1.01', description: 'Excavation',
      boqLineKey: 'line-key-1', sequence: 1, currency: 'GHS', disputedQuantity: 0,
      measuredToDateValue: 10000, previouslyCertifiedValue: 0, currentClaimedValue: 10000,
      currentCertifiedValue: 10000, currentPeriodCertifiedValue: 10000, disputedValue: 0,
      retentionToDateValue: 0, currentRetentionValue: 0, netCurrentValue: 10000,
      boqQuantity: 10, unitOfMeasure: 'EA', unitRate: 1000, measuredToDateQuantity: 10,
      previouslyCertifiedQuantity: 0, currentClaimedQuantity: 10, currentCertifiedQuantity: 10,
      previousRetentionValue: 0, reviewNote: '' }],
  });
  vi.mocked(service.save).mockRejectedValue(new Error('The selected evidence is no longer current.'));
  render(<QuantitySurveyValuationWorksheetDialog projectId="project-1" interimValuationId="valuation-1"
    interimValuationStatus="Draft" currency="GHS" />);
  fireEvent.click(screen.getByRole('button', { name: 'Worksheet' }));
  const note = await screen.findByRole('textbox', { name: 'Review note 1.01' });
  fireEvent.change(note, { target: { value: 'Reviewed against recorded measurement' } });
  const save = screen.getByRole('button', { name: 'Save worksheet' });
  fireEvent.click(save);
  expect(await screen.findByRole('alert')).toHaveTextContent('The selected evidence is no longer current.');
  expect(note).toHaveValue('Reviewed against recorded measurement');
  expect(screen.getByRole('spinbutton', { name: 'Current certified 1.01' })).toHaveValue(10);
  expect(save).toBeEnabled();
  fireEvent.click(save);
  await waitFor(() => expect(service.save).toHaveBeenCalledTimes(2));
  expect(vi.mocked(service.save).mock.calls[1]).toEqual(vi.mocked(service.save).mock.calls[0]);
});
