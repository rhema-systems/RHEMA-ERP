import React from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { Sidebar } from './sidebar';

vi.stubGlobal('React', React);

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

vi.mock('../../hooks/use-auth', () => ({
  useAuth: () => ({
    hasAnyRole: () => true,
    hasAnyPermission: () => true,
  }),
}));

function getCollapsedSidebar(container: HTMLElement) {
  const sidebar = container.querySelector('.w-16');
  if (!(sidebar instanceof HTMLElement)) {
    throw new Error('Expected a collapsed sidebar rail.');
  }
  return sidebar;
}

describe('Sidebar collapsed hover behavior', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.clearAllTimers();
    vi.useRealTimers();
  });

  it('expands on pointer hover and returns to the collapsed rail after leaving', () => {
    const { container } = render(<Sidebar defaultCollapsed />);
    const sidebar = getCollapsedSidebar(container);
    expect(sidebar).toBeInTheDocument();

    fireEvent.mouseEnter(sidebar);
    expect(sidebar).toHaveClass('w-64');
    expect(screen.getByText('Dashboard')).toBeInTheDocument();

    fireEvent.mouseLeave(sidebar);
    act(() => vi.advanceTimersByTime(421));
    expect(sidebar).toHaveClass('w-16');
  });

  it('keeps the sidebar open when the toggle is clicked during hover expansion', () => {
    const { container } = render(<Sidebar defaultCollapsed />);
    const sidebar = getCollapsedSidebar(container);

    fireEvent.mouseEnter(sidebar);
    fireEvent.click(screen.getByRole('button', { name: 'Keep sidebar expanded' }));
    expect(sidebar).toHaveClass('w-64');

    fireEvent.mouseLeave(sidebar);
    act(() => vi.advanceTimersByTime(421));
    expect(sidebar).toHaveClass('w-64');
    expect(screen.getByRole('button', { name: 'Collapse sidebar' })).toBeInTheDocument();
  });
});
