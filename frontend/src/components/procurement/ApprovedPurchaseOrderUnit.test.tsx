import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ApprovedPurchaseOrderUnit } from './ApprovedPurchaseOrderUnit';

describe('approved purchase order unit', () => {
  it('visibly retains EACH without requiring an unavailable dropdown option', () => {
    render(<ApprovedPurchaseOrderUnit unit="EACH" />);
    const field = screen.getByRole('textbox', { name: 'Approved source unit' });
    expect(field).toHaveValue('EACH');
    expect(field).toHaveAttribute('readonly');
    expect(field).toHaveAttribute('title', 'Unit retained from the approved source');
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });
});
