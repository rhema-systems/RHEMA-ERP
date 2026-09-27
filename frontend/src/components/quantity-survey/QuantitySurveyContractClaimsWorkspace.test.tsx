import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { QuantitySurveyContractClaimsWorkspace } from './QuantitySurveyContractClaimsWorkspace';
import { quantitySurveyContractClaimService as service, type ContractClaim } from '@/services/quantity-survey-contract-claim.service';

const permissions = vi.hoisted(() => ({ manage: true }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: (permission: string) => permission === 'quantity-survey.workspace.read' || permissions.manage }) }));
vi.mock('@/services/quantity-survey-contract-claim.service', () => ({ quantitySurveyContractClaimService: { workspace: vi.fn(), save: vi.fn(), submit: vi.fn() } }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
afterEach(() => { cleanup(); vi.clearAllMocks(); permissions.manage = true; });
Object.defineProperty(Element.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
Object.defineProperty(Element.prototype, 'hasPointerCapture', { configurable: true, value: () => false });
Object.defineProperty(Element.prototype, 'setPointerCapture', { configurable: true, value: vi.fn() });
Object.defineProperty(Element.prototype, 'releasePointerCapture', { configurable: true, value: vi.fn() });
const claim = { id: 'claim', projectId: 'project', contractId: 'contract', contractorBusinessPartnerId: 'partner', contractNumber: 'CTR-UAT', claimNumber: 'CLM-UAT', claimType: 'LossAndExpense', title: 'Recorded UAT claim', basis: 'Synthetic contractor claim basis', claimedAmount: 100, status: 'Draft', approvalStatus: 'Draft', rowVersion: 'one', currency: 'GHS', contractorName: 'Contractor', settledAmount: 0, disputeStatus: 'None', settlementStatus: 'NotApplicable', evidence: [] } as ContractClaim;
const workspace = (claims: ContractClaim[]) => ({ contracts: [{ id: 'contract', number: 'CTR-UAT', title: 'Works', contractorId: 'partner', contractor: 'Contractor', contractSum: 11000, currency: 'GHS' }], variations: [], extensionsOfTime: [], approvedBoqVersions: [], claims });
async function open(claims: ContractClaim[]) {
  vi.mocked(service.workspace).mockResolvedValue(workspace(claims));
  render(<QuantitySurveyContractClaimsWorkspace projectId="project" />);
  await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh' })).toBeEnabled());
}
it('hydrates a saved claim on first load and refresh and submits through staff intake', async () => {
  await open([claim]);
  expect(screen.getByDisplayValue(claim.title)).toBeInTheDocument();
  expect(screen.getByDisplayValue('100')).toBeInTheDocument();
  fireEvent.change(within(screen.getByText('Action reason / review note').parentElement!).getByRole('textbox'), { target: { value: 'Received contractor evidence for independent review' } });
  vi.mocked(service.submit).mockResolvedValue({ ...claim, status: 'Submitted' });
  vi.mocked(service.workspace).mockResolvedValue(workspace([{ ...claim, status: 'Submitted' }]));
  fireEvent.click(screen.getByRole('button', { name: 'Submit to QS' }));
  await waitFor(() => expect(service.submit).toHaveBeenCalledWith('project', 'claim', expect.objectContaining({ rowVersion: 'one' }), false));
  expect(await screen.findByRole('button', { name: 'Record QS vetting' })).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Save Draft' })).not.toBeInTheDocument();
});
it('preserves staff draft and request identity after save failure', async () => {
  await open([claim]);
  vi.mocked(service.save).mockRejectedValue(new Error('Claim changed; refresh and retry.'));
  fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Claim changed; refresh and retry.');
  expect(screen.getByDisplayValue(claim.title)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Save Draft' }));
  await waitFor(() => expect(service.save).toHaveBeenCalledTimes(2));
  expect(vi.mocked(service.save).mock.calls[1]).toEqual(vi.mocked(service.save).mock.calls[0]);
  expect(vi.mocked(service.save).mock.calls[0][2]).toBe(false);
});
it('does not offer intake to read-only staff', async () => {
  permissions.manage = false;
  await open([]);
  expect(screen.queryByRole('button', { name: 'New claim' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Save Draft' })).not.toBeInTheDocument();
  expect(screen.getByText('No contractor claims have been recorded.')).toBeInTheDocument();
});
