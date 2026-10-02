import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { InterfaceStyleToggle } from './InterfaceStyleToggle';

const mocks = vi.hoisted(() => ({
  style: 'executive' as 'executive' | 'immersive',
  setStyle: vi.fn(),
}));

vi.mock('../../contexts/InterfaceStyleContext', () => ({
  useInterfaceStyle: () => ({ interfaceStyle: mocks.style, setInterfaceStyle: mocks.setStyle }),
}));

describe('InterfaceStyleToggle', () => {
  it('shows compact icon buttons and exposes the selected interface state', () => {
    const { container } = render(<InterfaceStyleToggle />);

    expect(screen.getByRole('group', { name: 'Interface style' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Executive interface' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Immersive interface' })).toHaveAttribute('aria-pressed', 'false');
    expect(container.querySelectorAll('[aria-hidden="true"]')).toHaveLength(2);

    fireEvent.click(screen.getByRole('button', { name: 'Immersive interface' }));
    expect(mocks.setStyle).toHaveBeenCalledWith('immersive');
  });
});
