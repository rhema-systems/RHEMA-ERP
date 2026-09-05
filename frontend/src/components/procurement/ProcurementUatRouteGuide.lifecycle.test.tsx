import React from 'react';
import { act, render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/procurement/tenders/tender-1'),
}));
vi.mock('@/services/procurement-tender-document.service', () => ({
  procurementTenderDocumentService: {
    readiness: vi
      .fn()
      .mockResolvedValue({
        ready: true,
        effectiveTemplateReference: 'DOC/v1',
        allowsNewRecipient: true,
      }),
  },
}));

import { tenderDetailQueryKey } from '@/services/tenderService';
import { usePathname } from 'next/navigation';
import { procurementTenderDocumentService } from '@/services/procurement-tender-document.service';
import { ProcurementUatRouteGuide } from './ProcurementUatRouteGuide';

it('updates the next action from live tender cache changes without a page reload', async () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const tender = {
    id: 'tender-1',
    tenderNumber: 'TND-001',
    status: 'Approved',
    bidCount: 0,
    sourcingCaseId: 'case-1',
  };
  client.setQueryData(tenderDetailQueryKey(tender.id), tender);
  render(
    <QueryClientProvider client={client}>
      <ProcurementUatRouteGuide />
    </QueryClientProvider>
  );
  expect(
    await screen.findByRole('link', {
      name: 'Current action: Open document register',
    })
  ).toHaveAttribute('href', '/procurement/tenders/tender-1/document-controls');
  expect(within(screen.getByRole('group', { name: 'Next: Supplier bidding' }))
    .getByText('Responsible: Eligible suppliers')).toBeInTheDocument();
  expect(screen.queryByText('Responsible: Invited suppliers')).not.toBeInTheDocument();

  await act(async () => {
    client.setQueryData(tenderDetailQueryKey(tender.id), {
      ...tender,
      status: 'Published',
    });
  });
  expect(
    await screen.findByRole('link', {
      name: 'Current action: View tender bids',
    })
  ).toHaveAttribute('href', '/procurement/tenders/tender-1?tab=bids');

  await act(async () => {
    client.setQueryData(tenderDetailQueryKey(tender.id), {
      ...tender,
      status: 'Published',
      bidCount: 1,
    });
  });
  expect(
    await screen.findByRole('link', {
      name: 'Current action: Open evaluation committee',
    })
  ).toHaveAttribute('href', '/procurement/tenders/tender-1/committee-controls');
  client.clear();
});

it.each([false, undefined])(
  'retains the invitation action when the server does not identify an open recipient route (%s)',
  async (allowsNewRecipient) => {
    vi.mocked(procurementTenderDocumentService.readiness).mockResolvedValueOnce({
      ready: true,
      effectiveTemplateReference: 'DOC/v1',
      ...(allowsNewRecipient === undefined ? {} : { allowsNewRecipient }),
    } as Awaited<ReturnType<typeof procurementTenderDocumentService.readiness>>);
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, staleTime: Infinity } },
    });
    client.setQueryData(tenderDetailQueryKey('tender-1'), {
      id: 'tender-1', tenderNumber: 'TND-001', status: 'Published',
      bidCount: 0, sourcingCaseId: 'case-1',
    });
    render(
      <QueryClientProvider client={client}>
        <ProcurementUatRouteGuide />
      </QueryClientProvider>
    );
    expect(await screen.findByRole('link', {
      name: 'Current action: Open tender invitations',
    })).toHaveAttribute('href', '/procurement/tenders/tender-1?tab=invitations');
    expect(screen.queryByRole('link', {
      name: 'Current action: View tender bids',
    })).not.toBeInTheDocument();
    client.clear();
  }
);

it('reflects blocked readiness from the document workspace shared cache without a page reload', async () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const readinessKey = ['procurement-tender-document-readiness', 'Tender', 'tender-1', false];
  const readiness = { ready: true, effectiveTemplateReference: 'DOC/v1', allowsNewRecipient: true, blockedReasons: [] };
  client.setQueryData(tenderDetailQueryKey('tender-1'), {
    id: 'tender-1', tenderNumber: 'TND-001', status: 'Approved', bidCount: 0, sourcingCaseId: 'case-1',
  });
  client.setQueryData(readinessKey, readiness);
  render(
    <QueryClientProvider client={client}>
      <ProcurementUatRouteGuide />
    </QueryClientProvider>
  );
  const current = screen.getByRole('group', { name: 'Current: Controlled documents and publication' });
  expect(within(current).getByText('Ready')).toBeInTheDocument();
  await act(async () => {
    client.setQueryData(readinessKey, { ...readiness, ready: false, blockedReasons: ['The submission deadline has passed.'] });
  });
  expect(await within(current).findByText('Blocked')).toBeInTheDocument();
  expect(within(current).queryByText('Ready')).not.toBeInTheDocument();
  expect(screen.getByRole('status', { name: 'Current stage blockers' })).toHaveTextContent('The submission deadline has passed.');
  client.clear();
});

it('returns from ready approved documents to tender publication rather than linking to itself', () => {
  vi.mocked(usePathname).mockReturnValue('/procurement/tenders/tender-1/document-controls');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } });
  client.setQueryData(tenderDetailQueryKey('tender-1'), { id: 'tender-1', tenderNumber: 'TND-001', status: 'Approved', bidCount: 0, sourcingCaseId: 'case-1' });
  client.setQueryData(['procurement-tender-document-readiness', 'Tender', 'tender-1', false], {
    ready: true, effectiveTemplateReference: 'DOC/v1', isSourcePublished: false, allowsNewRecipient: true, blockedReasons: [],
  });
  render(<QueryClientProvider client={client}><ProcurementUatRouteGuide /></QueryClientProvider>);
  expect(screen.getByRole('link', { name: 'Current action: Return to tender publication' })).toHaveAttribute('href', '/procurement/tenders/tender-1');
  client.clear();
  vi.mocked(usePathname).mockReturnValue('/procurement/tenders/tender-1');
});
