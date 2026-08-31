import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { FinanceCloseTemplate } from '@/types/finance';

const mocks = vi.hoisted(() => ({
  getFinanceCloseTemplates: vi.fn(),
  hasPermission: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: mocks.hasPermission }),
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mocks.toast }),
}));

vi.mock('@/services/finance/finance-data.service', () => ({
  financeDataService: {
    getFinanceCloseTemplates: mocks.getFinanceCloseTemplates,
  },
}));

import FinanceCloseTemplatesPage from './page';

const approvedTemplate: FinanceCloseTemplate = {
  id: 'month-end-v1',
  templateCode: 'TDC-MONTH-END',
  name: 'TDC month-end close',
  closeType: 'MonthEnd',
  version: 1,
  status: 'Approved',
  isActive: true,
  isSystemDefault: true,
  description: 'Month-end controls',
  createdAt: '2026-08-01T00:00:00Z',
  canEdit: false,
  canApprove: false,
  tasks: [
    {
      id: 'task-2',
      taskCode: 'TRIAL_BALANCE',
      title: 'Review trial balance',
      category: 'Ledger Control',
      sequence: 20,
      isMandatory: true,
      isAutomated: true,
      checkCode: 'TRIAL_BALANCE',
      dueDaysAfterPeriodEnd: 2,
      dependsOnTaskCode: 'AP_CONTROL_RECONCILIATION',
      instructions: 'Investigate and resolve any out-of-balance condition.',
    },
    {
      id: 'task-1',
      taskCode: 'AP_CONTROL_RECONCILIATION',
      title: 'Reconcile AP control',
      category: 'Subledger Control',
      sequence: 10,
      isMandatory: true,
      isAutomated: false,
      dueDaysAfterPeriodEnd: 1,
    },
  ],
};

describe('FinanceCloseTemplatesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getFinanceCloseTemplates.mockResolvedValue([approvedTemplate]);
    mocks.hasPermission.mockReturnValue(true);
  });

  it('shows saved template steps without creating a new version', async () => {
    render(<FinanceCloseTemplatesPage />);

    fireEvent.click(await screen.findByRole('button', { name: /view steps/i }));

    const dialog = screen.getByRole('dialog', { name: 'TDC month-end close v1' });
    expect(within(dialog).getByText('10. Reconcile AP control')).toBeInTheDocument();
    expect(within(dialog).getByText('20. Review trial balance')).toBeInTheDocument();
    expect(within(dialog).getByText(/depends on:/i)).toHaveTextContent('AP_CONTROL_RECONCILIATION');
    expect(within(dialog).getByText(/investigate and resolve/i)).toBeInTheDocument();
  });

  it('keeps step visibility read-only for users without Finance administration permission', async () => {
    mocks.hasPermission.mockReturnValue(false);
    render(<FinanceCloseTemplatesPage />);

    expect(await screen.findByRole('button', { name: /view steps/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /create next version/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit draft/i })).not.toBeInTheDocument();
  });
});
