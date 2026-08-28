import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React, { useState, type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getFiscalYears: vi.fn(),
}));

vi.mock('@/services/procurementPlanningService', () => ({
  procurementPlanService: {
    getFiscalYears: mocks.getFiscalYears,
  },
}));

vi.mock('@/components/ui/select', () => ({
  Select: ({
    children,
    value,
    onValueChange,
    disabled,
  }: {
    children: ReactNode;
    value?: string;
    onValueChange?: (value: string) => void;
    disabled?: boolean;
  }) => (
    <select
      aria-label="Fiscal year"
      value={value ?? ''}
      disabled={disabled}
      onChange={(event) => onValueChange?.(event.target.value)}
    >
      {children}
    </select>
  ),
  SelectTrigger: ({ children }: { children: ReactNode }) => <>{children}</>,
  SelectValue: () => null,
  SelectContent: ({ children }: { children: ReactNode }) => <>{children}</>,
  SelectItem: ({
    value,
    disabled,
  }: {
    children: ReactNode;
    value: string;
    disabled?: boolean;
  }) => (
    <option value={value} disabled={disabled}>
      {value}
    </option>
  ),
}));

import { FiscalYearSelect } from './FiscalYearSelect';

const availableYear = (id: string, year: number) => ({
  id,
  fiscalYearCode: `FY${year}`,
  fiscalYearName: `Fiscal Year ${year}`,
  year,
  startDate: `${year}-01-01T00:00:00`,
  endDate: `${year}-12-31T23:59:59`,
  status: 'Open',
  isClosed: false,
  isLocked: false,
});

function ControlledFiscalYear({ autoSelect = false }: { autoSelect?: boolean }) {
  const [year, setYear] = useState(2024);

  return (
    <>
      <FiscalYearSelect
        value={year}
        onValueChange={setYear}
        autoSelectFirstAvailable={autoSelect}
      />
      <output data-testid="selected-year">{year}</output>
    </>
  );
}

describe('FiscalYearSelect', () => {
  beforeEach(() => {
    mocks.getFiscalYears.mockReset();
  });

  it('retains the Finance year identity when the user selects FY2026', async () => {
    mocks.getFiscalYears.mockResolvedValue([
      availableYear('finance-year-2024', 2024),
      availableYear('finance-year-2026', 2026),
    ]);
    render(<ControlledFiscalYear />);

    const select = await screen.findByRole('combobox', { name: 'Fiscal year' });
    await waitFor(() => expect(select).toHaveValue('finance-year-2024'));
    fireEvent.change(select, { target: { value: 'finance-year-2026' } });

    expect(screen.getByTestId('selected-year')).toHaveTextContent('2026');
    expect(select).toHaveValue('finance-year-2026');
  });

  it('auto-selects the current available year and keeps closed years disabled', async () => {
    mocks.getFiscalYears.mockResolvedValue([
      availableYear('finance-year-2026', 2026),
      {
        ...availableYear('finance-year-2025', 2025),
        status: 'Closed',
        isClosed: true,
      },
    ]);
    render(<ControlledFiscalYear autoSelect />);

    await waitFor(() => expect(screen.getByTestId('selected-year')).toHaveTextContent('2026'));
    expect(screen.getByRole('option', { name: 'finance-year-2025' })).toBeDisabled();
  });
});
