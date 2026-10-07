import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { RoomBooking } from '@/types/hr/company-schedule';

vi.stubGlobal('React', React);

const mocks = vi.hoisted(() => ({
  search: vi.fn(),
  hasPermission: vi.fn(),
  employeeId: 'EMP-ME',
}));

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ user: { employeeId: mocks.employeeId }, hasPermission: mocks.hasPermission }),
}));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/hr/company-schedule.service', () => ({
  roomBookingService: { search: mocks.search, exportCsv: vi.fn(), approve: vi.fn(), cancel: vi.fn(), remove: vi.fn() },
}));
// Radix's menu opens on pointer events jsdom does not drive; rendered flat, each row's menu can be read directly.
vi.mock('@/components/ui/dropdown-menu', async () => {
  const { createElement: h } = await import('react');
  return {
    DropdownMenu: ({ children }: { children: React.ReactNode }) => h('div', null, children),
    DropdownMenuTrigger: ({ children }: { children: React.ReactNode }) => h('div', null, children),
    DropdownMenuContent: ({ children }: { children: React.ReactNode }) => h('div', { 'data-testid': 'menu' }, children),
    DropdownMenuItem: ({ children, onClick }: { children: React.ReactNode; onClick?: () => void }) =>
      h('button', { type: 'button', onClick }, children),
    DropdownMenuSeparator: () => h('hr', { 'data-testid': 'separator' }),
  };
});

import RoomBookingsPage from './page';

const booking = (number: string, status: RoomBooking['status'], over: Partial<RoomBooking> = {}) =>
  ({
    id: number,
    bookingNumber: number,
    roomName: 'Boardroom',
    startDateTime: '2026-10-01T09:00:00Z',
    endDateTime: '2026-10-01T12:00:00Z',
    purpose: `Purpose of ${number}`,
    expectedAttendees: 12,
    bookedById: 'EMP-OTHER',
    bookedByName: 'Akpene Amoah',
    status,
    isCancelled: status === 'Cancelled',
    ...over,
  }) as unknown as RoomBooking;

const rows = [
  booking('BK-COMPLETED', 'Completed'),
  booking('BK-CANCELLED', 'Cancelled'),
  booking('BK-NOSHOW', 'NoShow'),
  booking('BK-CONFIRMED', 'Confirmed'),
  booking('BK-MINE', 'Tentative', { bookedById: 'emp-me' }), // the booker, in another letter case
  booking('BK-THEIRS', 'Tentative'),
];

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <RoomBookingsPage />
    </QueryClientProvider>,
  );
}

const rowOf = async (number: string) => (await screen.findByText(number)).closest('tr') as HTMLElement;
const offered = (row: HTMLElement) =>
  within(row).queryByTestId('menu')
    ? within(within(row).getByTestId('menu')).queryAllByRole('button').map((b) => b.textContent?.trim())
    : null;

describe('Room bookings register — the row menu', () => {
  beforeEach(() => {
    mocks.search.mockResolvedValue({
      items: rows,
      totalCount: rows.length,
      page: 1,
      pageSize: 25,
      totalPages: 1,
      hasNext: false,
      hasPrevious: false,
    });
  });

  describe('for the HR desk, without Admin', () => {
    beforeEach(() => mocks.hasPermission.mockReturnValue(false));

    it.each(['BK-COMPLETED', 'BK-CANCELLED', 'BK-NOSHOW'])('gives %s no ⋯ — nothing applies to it', async (number) => {
      renderPage();
      const row = await rowOf(number);
      expect(within(row).queryByText('Actions')).toBeNull();
      expect(offered(row)).toBeNull();
    });

    it('offers Cancel, and not Approve, on a confirmed booking', async () => {
      renderPage();
      expect(offered(await rowOf('BK-CONFIRMED'))).toEqual(['Cancel']);
    });

    it('never offers Approve to the booking’s own booker (F-66)', async () => {
      renderPage();
      expect(offered(await rowOf('BK-MINE'))).toEqual(['Cancel']);
    });

    it('offers Approve and Cancel on somebody else’s tentative booking', async () => {
      renderPage();
      expect(offered(await rowOf('BK-THEIRS'))).toEqual(['Approve', 'Cancel']);
    });
  });

  describe('for Admin', () => {
    beforeEach(() => mocks.hasPermission.mockImplementation((p: string) => p === 'HR.Company.Admin'));

    it('offers Delete alone on a completed booking, with no separator above it', async () => {
      renderPage();
      const row = await rowOf('BK-COMPLETED');
      expect(offered(row)).toEqual(['Delete']);
      expect(within(row).queryByTestId('separator')).toBeNull();
    });

    it('offers Cancel, then Delete below a separator, on a confirmed booking', async () => {
      renderPage();
      const row = await rowOf('BK-CONFIRMED');
      expect(offered(row)).toEqual(['Cancel', 'Delete']);
      expect(within(row).getByTestId('separator')).not.toBeNull();
    });
  });
});
