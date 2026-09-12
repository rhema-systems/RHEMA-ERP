import React, { useEffect } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ProcurementControlAccordion } from './ProcurementControlAccordion';

afterEach(cleanup);

describe('ProcurementControlAccordion', () => {
  it('starts compact, exposes a focusable native button, and reveals all details', () => {
    render(<ProcurementControlAccordion title="Budget policy" summary="Approved" status={<span>Ready</span>}><p>Detailed policy evidence</p></ProcurementControlAccordion>);
    const trigger = screen.getByRole('button', { name: /Budget policy/ });
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('Detailed policy evidence')).not.toBeVisible();
    expect(screen.getByText('Ready')).toBeVisible();
    trigger.focus();
    expect(trigger).toHaveFocus();
    expect(trigger.tagName).toBe('BUTTON');
    fireEvent.click(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('region')).toHaveAttribute('id', trigger.getAttribute('aria-controls'));
    expect(screen.getByText('Detailed policy evidence')).toBeVisible();
    fireEvent.click(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
  });

  it('keeps blockers and recovery actions visible outside the collapsed details', () => {
    const retry = vi.fn();
    render(<ProcurementControlAccordion title="Compliance gate" notice="Signed contract is missing" actions={<button onClick={retry}>Refresh checks</button>}><p>Other successful checks</p></ProcurementControlAccordion>);
    expect(screen.getByText('Signed contract is missing')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Refresh checks' }));
    expect(retry).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: /Compliance gate/ })).toHaveAttribute('aria-expanded', 'false');
  });

  it('never unmounts controls or clears user input on collapse', () => {
    const evaluate = vi.fn();
    const unmount = vi.fn();
    function Control() {
      useEffect(() => { evaluate(); return unmount; }, []);
      return <input aria-label="Evidence reference" defaultValue="" />;
    }
    render(<ProcurementControlAccordion title="Evidence"><Control /></ProcurementControlAccordion>);
    expect(evaluate).toHaveBeenCalledOnce();
    const trigger = screen.getByRole('button', { name: /Evidence/ });
    fireEvent.click(trigger);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'REF-001' } });
    fireEvent.click(trigger);
    fireEvent.click(trigger);
    expect(screen.getByRole('textbox')).toHaveValue('REF-001');
    expect(evaluate).toHaveBeenCalledOnce();
    expect(unmount).not.toHaveBeenCalled();
    expect(screen.getByRole('region').className).not.toMatch(/max-h|overflow-hidden/);
  });
});
