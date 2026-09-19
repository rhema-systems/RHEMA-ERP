import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import {
  FinanceDateInput,
  formatFinanceDateInput,
  parseFinanceDateInput,
} from './finance-date-input';

Object.assign(globalThis, { React });

describe('FinanceDateInput', () => {
  it('round-trips a calendar date without a UTC day shift', () => {
    const date = parseFinanceDateInput('2026-09-01');

    expect(date).toBeDefined();
    expect(date?.getFullYear()).toBe(2026);
    expect(date?.getMonth()).toBe(8);
    expect(date?.getDate()).toBe(1);
    expect(formatFinanceDateInput(date)).toBe('2026-09-01');
  });

  it('rejects an invalid calendar date', () => {
    expect(parseFinanceDateInput('2026-02-31')).toBeUndefined();
  });

  it('emits the selected date and exposes the configured range', () => {
    const onChange = vi.fn();
    render(
      <FinanceDateInput
        aria-label="Invoice date"
        value={new Date(2026, 7, 30)}
        min={new Date(2026, 7, 1)}
        max={new Date(2026, 8, 30)}
        onChange={onChange}
      />
    );

    const input = screen.getByLabelText('Invoice date');
    expect(input).toHaveAttribute('type', 'date');
    expect(input).toHaveAttribute('min', '2026-08-01');
    expect(input).toHaveAttribute('max', '2026-09-30');

    fireEvent.input(input, { target: { value: '2026-09-01' } });
    expect(formatFinanceDateInput(onChange.mock.calls[0][0])).toBe(
      '2026-09-01'
    );
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it('retains the selected date when an unrelated control rerenders the form', () => {
    function Harness() {
      const [date, setDate] = React.useState<Date | undefined>(new Date(2026, 7, 30));
      const [, setRevision] = React.useState(0);

      return (
        <>
          <FinanceDateInput
            aria-label="Invoice date"
            value={date}
            onChange={setDate}
          />
          <button
            type="button"
            onClick={() => setRevision((current) => current + 1)}
          >
            Open supplier
          </button>
        </>
      );
    }

    render(<Harness />);
    const input = screen.getByLabelText<HTMLInputElement>('Invoice date');
    fireEvent.input(input, { target: { value: '2026-09-01' } });
    fireEvent.click(screen.getByRole('button', { name: 'Open supplier' }));

    expect(input.value).toBe('2026-09-01');
  });
});
