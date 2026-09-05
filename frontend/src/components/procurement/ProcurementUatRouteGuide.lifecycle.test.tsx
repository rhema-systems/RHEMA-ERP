import React from 'react';
import { act, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/procurement/tenders/tender-1',
}));
vi.mock('@/services/procurement-tender-document.service', () => ({
  procurementTenderDocumentService: {
    readiness: vi
      .fn()
      .mockResolvedValue({ ready: true, effectiveTemplateReference: 'DOC/v1' }),
  },
}));

import { tenderDetailQueryKey } from '@/services/tenderService';
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

  await act(async () => {
    client.setQueryData(tenderDetailQueryKey(tender.id), {
      ...tender,
      status: 'Published',
    });
  });
  expect(
    await screen.findByRole('link', {
      name: 'Current action: Open tender invitations',
    })
  ).toHaveAttribute('href', '/procurement/tenders/tender-1?tab=invitations');

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
