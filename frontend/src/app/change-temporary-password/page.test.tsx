import React from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import ChangeTemporaryPasswordPage from './page';

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getStoredUser: vi.fn(),
  isAuthenticated: vi.fn(),
  logout: vi.fn(),
  changePassword: vi.fn(),
  success: vi.fn(),
  error: vi.fn(),
}));

vi.mock('next/navigation', () => ({ useRouter: () => ({ replace: mocks.replace }) }));
vi.mock('@/services/auth', () => ({
  authService: {
    getStoredUser: mocks.getStoredUser,
    isAuthenticated: mocks.isAuthenticated,
    logout: mocks.logout,
  },
}));
vi.mock('@/services/profile', () => ({
  profileService: { changePassword: mocks.changePassword },
}));
vi.mock('sonner', () => ({ toast: { success: mocks.success, error: mocks.error } }));

function fillPasswords(confirmation = 'NewFixture2!') {
  fireEvent.change(screen.getByLabelText('Temporary password'), {
    target: { value: 'OldFixture1!' },
  });
  fireEvent.change(screen.getByLabelText('New password'), {
    target: { value: 'NewFixture2!' },
  });
  fireEvent.change(screen.getByLabelText('Confirm new password'), {
    target: { value: confirmation },
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.getStoredUser.mockReturnValue(null);
  mocks.isAuthenticated.mockReturnValue(true);
  mocks.logout.mockReset().mockResolvedValue(undefined);
  mocks.changePassword.mockReset().mockResolvedValue(undefined);
});
afterEach(cleanup);

describe('shared temporary-password change page', () => {
  it('keeps the page and focused password fields readable in every theme', () => {
    render(<ChangeTemporaryPasswordPage />);
    expect(screen.getByRole('main')).toHaveClass('bg-slate-50', 'text-slate-950');
    for (const label of ['Temporary password', 'New password', 'Confirm new password']) {
      expect(screen.getByLabelText(label)).toHaveClass(
        'bg-white',
        'text-slate-950',
        'caret-slate-950',
        'focus:bg-white',
        'focus-visible:bg-white'
      );
    }
  });

  it.each(['Financial Controller', 'ExternalUser'])(
    'uses account-neutral wording for %s',
    role => {
      mocks.getStoredUser.mockReturnValue({ roles: [role] });
      render(<ChangeTemporaryPasswordPage />);
      expect(screen.getByRole('button', { name: 'Change password' })).toBeEnabled();
      expect(screen.getByText(/Choose a new password to continue using your account/)).toBeInTheDocument();
      expect(screen.queryByText(/activat|supplier/i)).not.toBeInTheDocument();
    }
  );

  it('retains mismatch validation without sending a password-change request', () => {
    render(<ChangeTemporaryPasswordPage />);
    fillPasswords('DifferentFixture3!');
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));
    expect(mocks.error).toHaveBeenCalledWith('The new passwords do not match.');
    expect(mocks.changePassword).not.toHaveBeenCalled();
    expect(mocks.logout).not.toHaveBeenCalled();
  });

  it('shows the neutral pending label, disables repeat submission, and signs out after success', async () => {
    let complete = () => {};
    mocks.changePassword.mockImplementation(() => new Promise<void>(resolve => { complete = resolve; }));
    render(<ChangeTemporaryPasswordPage />);
    fillPasswords();
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));
    expect(screen.getByRole('button', { name: 'Changing password…' })).toBeDisabled();
    expect(mocks.changePassword).toHaveBeenCalledWith({
      currentPassword: 'OldFixture1!',
      newPassword: 'NewFixture2!',
    });
    expect(mocks.logout).not.toHaveBeenCalled();
    await act(async () => complete());
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith('/login'));
    expect(mocks.logout).toHaveBeenCalledOnce();
    expect(mocks.success).toHaveBeenCalledWith('Password replaced. Sign in with your new password.');
  });

  it('retains entered values and restores the button when the server rejects a change', async () => {
    mocks.changePassword.mockRejectedValueOnce(new Error('Password does not meet policy.'));
    render(<ChangeTemporaryPasswordPage />);
    fillPasswords();
    fireEvent.click(screen.getByRole('button', { name: 'Change password' }));
    await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('Password does not meet policy.'));
    expect(screen.getByLabelText('New password')).toHaveValue('NewFixture2!');
    expect(screen.getByRole('button', { name: 'Change password' })).toBeEnabled();
    expect(mocks.logout).not.toHaveBeenCalled();
    expect(mocks.replace).not.toHaveBeenCalled();
  });

  it('keeps the existing unauthenticated redirect', () => {
    mocks.isAuthenticated.mockReturnValue(false);
    render(<ChangeTemporaryPasswordPage />);
    expect(mocks.replace).toHaveBeenCalledWith('/login');
  });
});
