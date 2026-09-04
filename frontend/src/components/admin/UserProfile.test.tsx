import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { UserProfile, validateTemporaryPasswordReset } from './UserProfile';

const user = {
  id: 'user-1',
  username: 'procurementapprover',
  email: 'approver@example.test',
  roles: ['TDC_HEAD_OF_PROCUREMENT'],
  isActive: true,
  createdAt: new Date('2026-01-01T00:00:00.000Z'),
};

describe('UserProfile temporary-password reset', () => {
  it('validates a strong matching temporary password and reason', () => {
    expect(
      validateTemporaryPasswordReset(
        'StrongTemporary1!',
        'StrongTemporary1!',
        'User requested account recovery.'
      )
    ).toBeUndefined();
    expect(
      validateTemporaryPasswordReset(
        'StrongTemporary1!',
        'DifferentTemporary1!',
        'User requested account recovery.'
      )
    ).toContain('match');
  });

  it('retains secrets on failure and clears and closes after success', async () => {
    const reset = vi
      .fn()
      .mockRejectedValueOnce(
        new Error('The password was used recently. (USER_PASSWORD_REUSED)')
      )
      .mockResolvedValueOnce(undefined);
    render(
      <UserProfile user={user} onClose={vi.fn()} onResetPassword={reset} />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Reset Password' }));
    const newPassword = screen.getByLabelText('New Temporary Password');
    const confirmation = screen.getByLabelText('Confirm Temporary Password');
    const reason = screen.getByLabelText('Administrative reason');

    fireEvent.change(newPassword, { target: { value: 'StrongTemporary1!' } });
    fireEvent.change(confirmation, {
      target: { value: 'StrongTemporary1!' },
    });
    fireEvent.change(reason, {
      target: { value: 'User requested account recovery.' },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Set temporary password' })
    );

    expect(
      await screen.findByText(
        'The password was used recently. (USER_PASSWORD_REUSED)'
      )
    ).toBeInTheDocument();
    expect(newPassword).toHaveValue('StrongTemporary1!');
    expect(confirmation).toHaveValue('StrongTemporary1!');
    expect(reason).toHaveValue('User requested account recovery.');

    fireEvent.click(
      screen.getByRole('button', { name: 'Set temporary password' })
    );

    await waitFor(() => {
      expect(
        screen.queryByRole('dialog', {
          name: 'Set temporary password for procurementapprover',
        })
      ).not.toBeInTheDocument();
    });
    expect(reset).toHaveBeenLastCalledWith('user-1', {
      newPassword: 'StrongTemporary1!',
      reason: 'User requested account recovery.',
    });

    fireEvent.click(screen.getByRole('button', { name: 'Reset Password' }));
    expect(screen.getByLabelText('New Temporary Password')).toHaveValue('');
    expect(screen.getByLabelText('Confirm Temporary Password')).toHaveValue('');
    expect(screen.getByLabelText('Administrative reason')).toHaveValue('');
  });
});
