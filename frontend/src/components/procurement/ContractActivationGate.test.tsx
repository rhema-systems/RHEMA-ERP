import React from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ContractActivationGate } from './ContractActivationGate';
import { contractService, type ContractActivationOverview } from '@/services/contractService';

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: () => true }),
}));
vi.mock('@/services/contractService', () => ({
  contractService: { getActivationOverview: vi.fn(), submitActivation: vi.fn() },
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const overview = (changes: Partial<ContractActivationOverview> = {}): ContractActivationOverview => ({
  contractId: 'contract-1', contractNumber: 'CTR-UAT', contractStatus: 'Draft',
  isReady: true, canSubmit: true, canDecide: false, canActivate: false,
  requiredEvidenceKeys: [], decisionKeys: [], checks: [], history: [], ...changes,
});

beforeEach(() => {
  vi.mocked(contractService.getActivationOverview).mockResolvedValue(overview());
  vi.mocked(contractService.submitActivation).mockResolvedValue({} as never);
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

async function setup() {
  const changed = vi.fn();
  render(<ContractActivationGate contractId="contract-1" contractRowVersion="version-1"
    documents={[]} onContractChanged={changed} />);
  const submit = await screen.findByRole('button', { name: 'Submit to shared workflow' });
  fireEvent.change(screen.getByRole('textbox', { name: 'Submission reason' }), {
    target: { value: 'UAT contract ready for approval' },
  });
  return { submit, changed };
}

describe('contract activation evidence requirements', () => {
  it('keeps the server error and submission reason visible after failure', async () => {
    vi.mocked(contractService.submitActivation).mockRejectedValue(new Error('Configured approval workflow is unavailable.'));
    const { submit, changed } = await setup();
    fireEvent.click(submit);
    expect(await screen.findByRole('alert')).toHaveTextContent('Configured approval workflow is unavailable.');
    expect(screen.getByRole('textbox', { name: 'Submission reason' })).toHaveValue('UAT contract ready for approval');
    expect(changed).not.toHaveBeenCalled();
    expect(submit).toBeEnabled();
  });

  it('submits without uploads when the server requires no supporting documents', async () => {
    const { submit, changed } = await setup();
    expect(submit).toBeEnabled();
    expect(screen.queryByText(/Upload the required contract evidence/)).not.toBeInTheDocument();
    fireEvent.click(submit);
    await waitFor(() => expect(contractService.submitActivation).toHaveBeenCalledWith('contract-1', {
      reason: 'UAT contract ready for approval', contractRowVersion: 'version-1',
      idempotencyKey: expect.any(String), evidence: [],
    }));
    await waitFor(() => expect(changed).toHaveBeenCalledOnce());
  });

  it('still blocks submission when a configured evidence requirement is unfilled', async () => {
    vi.mocked(contractService.getActivationOverview).mockResolvedValue(overview({
      requiredEvidenceKeys: ['signed-contract'],
    }));
    const { submit } = await setup();
    expect(screen.getByRole('combobox', { name: 'signed-contract' })).toBeVisible();
    expect(screen.getByText(/Upload the required contract evidence/)).toBeVisible();
    expect(submit).toBeDisabled();
    fireEvent.click(submit);
    expect(contractService.submitActivation).not.toHaveBeenCalled();
  });

  it.each(['Failed', 1] as const)('retains failed server gates with status %s even without evidence requirements', async (status) => {
    vi.mocked(contractService.getActivationOverview).mockResolvedValue(overview({
      isReady: false,
      checks: [{ key: 'budget', label: 'Budget', status, code: 'BUDGET_UNAVAILABLE',
        message: 'Insufficient budget', isRequired: true }],
    }));
    const { submit } = await setup();
    expect(submit).toBeDisabled();
    fireEvent.click(submit);
    expect(contractService.submitActivation).not.toHaveBeenCalled();
  });
});
