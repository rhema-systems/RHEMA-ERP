import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { InventoryCostValue } from './InventoryCostValue';

describe('inventory stored cost display', () => {
  it('shows the supplied stored amount with the configured currency and source label', () => {
    render(<InventoryCostValue value={1907.09} kind="item" currencyCode="GHS" />);
    const amount = screen.getByText(/1,907\.09/);
    expect(amount).toHaveTextContent('GHS');
    expect(amount).toHaveAttribute('title', expect.stringContaining('Stored item-wide'));
    expect(amount).toHaveAttribute('title', expect.stringContaining('not the exact-bin posting cost'));
    expect(amount).not.toHaveTextContent('$');
  });
  it('keeps a real stored zero rather than substituting another cost', () => {
    render(<InventoryCostValue value={0} kind="warehouse" />);
    expect(screen.getByText('0.00')).toHaveAttribute('title', expect.stringContaining('warehouse assignment'));
  });
  it.each([undefined, null, Number.NaN])('shows unavailable for %s, never a fabricated zero', value => {
    render(<InventoryCostValue value={value} kind="item" />);
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('0.00')).not.toBeInTheDocument();
  });
  it('does not assume dollars when the base-currency reference is unavailable', () => {
    render(<InventoryCostValue value={1918.85} kind="count" />);
    expect(screen.getByText('1,918.85')).toHaveAttribute('title', expect.stringContaining('base currency'));
    expect(screen.getByText('1,918.85')).toHaveAttribute('title', expect.stringContaining('Saved valuation'));
  });
});
