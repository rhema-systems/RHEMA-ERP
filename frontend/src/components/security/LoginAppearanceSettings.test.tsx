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
        onBackgroundUpload={vi.fn()}
        onBackgroundReset={vi.fn()}
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
        onBackgroundUpload={vi.fn()}
        onBackgroundReset={vi.fn()}
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
        onBackgroundUpload={vi.fn()}
        onBackgroundReset={vi.fn()}
        errorMessage="Login appearance settings could not be loaded."
      />
    );

    expect(screen.getByRole('heading', { name: 'Login Page Appearance' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Login appearance settings could not be loaded.'
    );
  });

  it('uploads and restores a tenant-specific background for each design', () => {
    const onBackgroundUpload = vi.fn();
    const onBackgroundReset = vi.fn();
    const file = new File(['image'], 'campus.webp', { type: 'image/webp' });

    render(
      <LoginAppearanceSettings
        value="LightCorporate"
        onValueChange={vi.fn()}
        lightBackgroundUrl="/api/public/config/login/background/light-id"
        onBackgroundUpload={onBackgroundUpload}
        onBackgroundReset={onBackgroundReset}
      />
    );

    const input = document.getElementById('login-background-upload-LightCorporate') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [file] } });
    expect(onBackgroundUpload).toHaveBeenCalledWith('LightCorporate', file);

    fireEvent.click(screen.getByRole('button', { name: /restore default/i }));
    expect(onBackgroundReset).toHaveBeenCalledWith('LightCorporate');
  });
});
