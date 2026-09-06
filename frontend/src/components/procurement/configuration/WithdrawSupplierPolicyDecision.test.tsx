import React from 'react';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ProcurementConfigurationDecision } from '@/types/procurement-configuration';

const mocks = vi.hoisted(() => ({ withdraw: vi.fn(), toast: vi.fn() }));
vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));
vi.mock('@/services/procurement-configuration.service', () => ({
  procurementConfigurationService: { withdrawDecision: mocks.withdraw },
}));
import { WithdrawSupplierPolicyDecision } from './WithdrawSupplierPolicyDecision';

const decision = {
  id: 'decision-11',
  decisionKey: 'DEC-011',
  status: 'Approved',
  rowVersion: 'row-11',
} as ProcurementConfigurationDecision;
const changed = vi.fn();
function mount(
  overrides: Partial<
    React.ComponentProps<typeof WithdrawSupplierPolicyDecision>
  > = {}
) {
  return render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { mutations: { retry: false } } })
      }
    >
      <WithdrawSupplierPolicyDecision
        profileId="profile-3"
        profileStatus="Published"
        decision={decision}
        canManage
        onChanged={changed}
        {...overrides}
      />
    </QueryClientProvider>
  );
}

describe('optional supplier policy withdrawal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.withdraw.mockResolvedValue({});
  });
  afterEach(cleanup);

  it.each([
    { canManage: false },
    { profileStatus: 'Draft' as const },
    { profileStatus: 'Retired' as const },
    { decision: { ...decision, decisionKey: 'DEC-001' } },
    { decision: { ...decision, status: 'Proposed' as const } },
  ])('does not offer withdrawal outside its scope: %j', (overrides) => {
    mount(overrides);
    expect(
      screen.queryByRole('button', {
        name: 'Withdraw optional supplier policy',
      })
    ).not.toBeInTheDocument();
  });

  it('requires a reason and sends the exact decision concurrency token after confirmation', async () => {
    mount();
    fireEvent.click(
      screen.getByRole('button', { name: 'Withdraw optional supplier policy' })
    );
    expect(screen.getByRole('dialog')).toHaveTextContent(
      'The other 13 decisions are not changed'
    );
    expect(
      screen.getByRole('button', { name: 'Withdraw decision' })
    ).toBeDisabled();
    fireEvent.change(
      screen.getByLabelText('Approved business reason or change reference'),
      { target: { value: '  Approved architecture alignment U10-004  ' } }
    );
    fireEvent.click(screen.getByRole('button', { name: 'Withdraw decision' }));
    await waitFor(() =>
      expect(mocks.withdraw).toHaveBeenCalledWith('profile-3', 'DEC-011', {
        rowVersion: 'row-11',
        reason: 'Approved architecture alignment U10-004',
      })
    );
    await waitFor(() =>
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    );
    expect(changed).toHaveBeenCalledTimes(1);
  });

  it('retains the reason and server ProblemDetails on failure', async () => {
    mocks.withdraw.mockRejectedValue({
      response: {
        detail: 'Decision changed. Reload it.',
        code: 'CONFIG_CONCURRENCY',
      },
    });
    mount();
    fireEvent.click(
      screen.getByRole('button', { name: 'Withdraw optional supplier policy' })
    );
    fireEvent.change(
      screen.getByLabelText('Approved business reason or change reference'),
      { target: { value: 'Approved alignment' } }
    );
    fireEvent.click(screen.getByRole('button', { name: 'Withdraw decision' }));
    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Decision changed. Reload it. (CONFIG_CONCURRENCY)'
      )
    );
    expect(
      screen.getByLabelText('Approved business reason or change reference')
    ).toHaveValue('Approved alignment');
    expect(changed).not.toHaveBeenCalled();
  });

  it('keeps cancellation non-mutating and withdrawn history read-only', () => {
    const view = mount();
    fireEvent.click(
      screen.getByRole('button', { name: 'Withdraw optional supplier policy' })
    );
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(mocks.withdraw).not.toHaveBeenCalled();
    view.unmount();
    mount({ decision: { ...decision, status: 'Withdrawn' } });
    expect(
      screen.getByText(/values, evidence and assessment history remain/)
    ).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
