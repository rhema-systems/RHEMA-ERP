import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { LoginAppearanceSettings } from './LoginAppearanceSettings';

describe('LoginAppearanceSettings', () => {
  it('shows the persisted default and lets an administrator select the other design', () => {
    const onValueChange = vi.fn();

    render(
      <LoginAppearanceSettings
        value="LightCorporate"
        onValueChange={onValueChange}
      />
    );

    expect(screen.getByRole('radio', { name: 'Light Corporate' })).toBeChecked();
    expect(screen.getByText('Default login page')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('radio', { name: 'Dark Premium' }));

    expect(onValueChange).toHaveBeenCalledWith('DarkPremium');
  });

  it('prevents changes while a save is in progress', () => {
    render(
      <LoginAppearanceSettings
        value="DarkPremium"
        onValueChange={vi.fn()}
        disabled
      />
    );

    expect(screen.getByRole('radio', { name: 'Light Corporate' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: 'Dark Premium' })).toBeDisabled();
  });

  it('shows a useful error without hiding the settings context', () => {
    render(
      <LoginAppearanceSettings
        value="LightCorporate"
        onValueChange={vi.fn()}
        errorMessage="Login appearance settings could not be loaded."
      />
    );

    expect(screen.getByRole('heading', { name: 'Login Page Appearance' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Login appearance settings could not be loaded.'
    );
  });
});
