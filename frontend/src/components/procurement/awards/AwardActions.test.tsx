import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { AwardActions } from './AwardActions';

beforeEach(() => vi.stubGlobal('React', React));
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

const allActions = () => ({
  onNotify: vi.fn(), onPerformanceBond: vi.fn(), onCreate: vi.fn(), onNegotiate: vi.fn(), onCancel: vi.fn(),
});

describe('award action chronology', () => {
  it('orders routine actions as notification, bond, then PO/contract and preserves the bond status', () => {
    render(<AwardActions {...allActions()} performanceBondStatus={<span>Approved</span>} />);
    const routine = screen.getByRole('group', { name: 'Award follow-up actions' });
    expect(within(routine).getAllByRole('button').map((button) => button.textContent)).toEqual([
      'Send Notification', 'Performance BondApproved', 'Create PO / Contract',
    ]);
    expect(within(routine).queryByRole('button', { name: 'Invite for Negotiation' })).not.toBeInTheDocument();
    expect(within(routine).queryByRole('button', { name: 'Cancel Award' })).not.toBeInTheDocument();
    expect(screen.getByText(/any required bond/)).toBeInTheDocument();
  });

  it('keeps exception actions collapsed but discoverable and opens the original action callbacks', () => {
    const actions = allActions();
    render(<AwardActions {...actions} />);
    const summary = screen.getByText('Other actions');
    expect(summary.closest('details')).not.toHaveAttribute('open');
    fireEvent.click(summary);
    expect(summary.closest('details')).toHaveAttribute('open');
    const exceptions = screen.getByRole('group', { name: 'Exceptional award actions' });
    expect(within(exceptions).getAllByRole('button').map((button) => button.textContent)).toEqual([
      'Invite for Negotiation', 'Cancel Award',
    ]);
    for (const name of ['Send Notification', 'Performance Bond', 'Create PO / Contract', 'Invite for Negotiation', 'Cancel Award']) {
      fireEvent.click(screen.getByRole('button', { name, exact: true }));
    }
    for (const action of Object.values(actions)) expect(action).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['onNotify', 'Send Notification'],
    ['onPerformanceBond', 'Performance Bond'],
    ['onCreate', 'Create PO / Contract'],
    ['onNegotiate', 'Invite for Negotiation'],
    ['onCancel', 'Cancel Award'],
  ] as const)('does not expose an action when its permitted callback %s is absent', (key, label) => {
    const actions = allActions();
    render(<AwardActions {...actions} {...{ [key]: undefined }} />);
    fireEvent.click(screen.getByText('Other actions'));
    expect(screen.queryByRole('button', { name: label, exact: true })).not.toBeInTheDocument();
    expect(actions[key]).not.toHaveBeenCalled();
  });

  it('does not invent a bond prerequisite or show an empty exceptions disclosure', () => {
    const onCreate = vi.fn();
    render(<AwardActions onCreate={onCreate} />);
    expect(screen.getByRole('button', { name: 'Create PO / Contract' })).toBeEnabled();
    expect(screen.queryByText('Other actions')).not.toBeInTheDocument();
  });

  it('does not render an empty actions card for a read-only actor', () => {
    const { container } = render(<AwardActions />);
    expect(container).toBeEmptyDOMElement();
  });

  it('retains cancellation access even when an actor has no routine actions', () => {
    render(<AwardActions onCancel={vi.fn()} />);
    expect(screen.queryByRole('group', { name: 'Award follow-up actions' })).not.toBeInTheDocument();
    fireEvent.click(screen.getByText('Other actions'));
    expect(screen.getByRole('button', { name: 'Cancel Award' })).toBeInTheDocument();
  });
});
