import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  isAuthenticated: vi.fn(),
  getStoredUser: vi.fn(),
  logout: vi.fn(),
  changePassword: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mocks.replace }),
}));

vi.mock('@/services/auth', () => ({
  authService: {
    isAuthenticated: mocks.isAuthenticated,
    getStoredUser: mocks.getStoredUser,
    logout: mocks.logout,
  },
}));

vi.mock('@/services/profile', () => ({
  profileService: { changePassword: mocks.changePassword },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import ChangeTemporaryPasswordPage from './page';

describe('temporary password activation page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.isAuthenticated.mockReturnValue(true);
    mocks.getStoredUser.mockReturnValue({
      id: 'supplier-user',
      username: 'supplier@example.test',
    });
  });

  it('keeps the page and focused password fields readable in every theme', () => {
    render(<ChangeTemporaryPasswordPage />);

    expect(screen.getByRole('main')).toHaveClass(
      'bg-slate-50',
      'text-slate-950'
    );
    for (const label of [
      'Temporary password',
      'New password',
      'Confirm new password',
    ]) {
      expect(screen.getByLabelText(label)).toHaveClass(
        'bg-white',
        'text-slate-950',
        'caret-slate-950',
        'focus:bg-white',
        'focus-visible:bg-white'
      );
    }
  });
});
