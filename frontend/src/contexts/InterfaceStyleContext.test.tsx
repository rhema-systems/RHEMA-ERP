import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { InterfaceStyleProvider, getInterfaceStyleStorageKey, useInterfaceStyle } from './InterfaceStyleContext';

vi.mock('../services/auth', () => ({
  authService: {
    getStoredUser: () => ({ id: 'user-42', username: 'dashboard.user' }),
  },
}));

function StyleHarness() {
  const { interfaceStyle, setInterfaceStyle } = useInterfaceStyle();
  return (
    <>
      <output>{interfaceStyle}</output>
      <button type="button" onClick={() => setInterfaceStyle('immersive')}>Use immersive</button>
      <button type="button" onClick={() => setInterfaceStyle('executive')}>Use executive</button>
    </>
  );
}

describe('InterfaceStyleProvider', () => {
  beforeEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset.interfaceStyle;
  });

  it('persists the selected style for the signed-in user and applies it to the document', async () => {
    render(<InterfaceStyleProvider><StyleHarness /></InterfaceStyleProvider>);

    fireEvent.click(screen.getByRole('button', { name: 'Use immersive' }));

    expect(screen.getByText('immersive')).toBeInTheDocument();
    expect(document.documentElement.dataset.interfaceStyle).toBe('immersive');
    expect(localStorage.getItem('erp-interface-style:user-42')).toBe('immersive');
  });

  it('restores an existing user preference on mount', async () => {
    localStorage.setItem(getInterfaceStyleStorageKey(), 'immersive');
    render(<InterfaceStyleProvider><StyleHarness /></InterfaceStyleProvider>);

    await waitFor(() => expect(screen.getByText('immersive')).toBeInTheDocument());
    expect(document.documentElement.dataset.interfaceStyle).toBe('immersive');
  });
});
