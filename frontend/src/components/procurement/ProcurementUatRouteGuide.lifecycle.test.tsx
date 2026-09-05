import React from 'react';
import { act, render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/procurement/tenders/tender-1'),
}));
vi.mock('@/services/procurement-tender-document.service', () => ({
  procurementTenderDocumentService: {
    readiness: vi.fn().mockResolvedValue({
      ready: true,
      effectiveTemplateReference: 'DOC/v1',
      allowsNewRecipient: true,
    }),
  },
}));
vi.mock('@/services/procurement-evaluation-committee.service', () => ({
  procurementEvaluationCommitteeService: {
    readiness: vi.fn().mockResolvedValue(null),
    get: vi.fn().mockResolvedValue(null),
  },
}));
vi.mock('@/services/tenderEvaluationService', () => ({
  getEvaluationById: vi.fn(),
}));
vi.mock('@/services/tenderBidService', () => ({ getBidById: vi.fn() }));
vi.mock('@/services/tenderService', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/services/tenderService')>()),
  getTenderById: vi.fn(),
}));
vi.mock('@/services/purchasingService', () => ({
  purchasingService: {
    getPurchaseRequisitionById: vi.fn().mockResolvedValue(null),
    getPurchaseRequisitionSubmissionReadiness: vi.fn().mockResolvedValue(null),
  },
}));
vi.mock('@/services/procurementPlanningService', () => ({
  procurementPlanService: { getPlanById: vi.fn().mockResolvedValue(null) },
}));

import {
  getTenderById,
  tenderDetailQueryKey,
  type TenderDetailDto,
} from '@/services/tenderService';
import { usePathname } from 'next/navigation';
import { procurementTenderDocumentService } from '@/services/procurement-tender-document.service';
import { ProcurementUatRouteGuide } from './ProcurementUatRouteGuide';
import { getEvaluationById } from '@/services/tenderEvaluationService';
import { getBidById } from '@/services/tenderBidService';
import { procurementEvaluationCommitteeService } from '@/services/procurement-evaluation-committee.service';

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(usePathname).mockReturnValue('/procurement/tenders/tender-1');
});

function createClient(options: ConstructorParameters<typeof QueryClient>[0]) {
  const client = new QueryClient(options);
  vi.mocked(getTenderById).mockImplementation(
    async (id) =>
      client.getQueryData<TenderDetailDto>(tenderDetailQueryKey(id))!
  );
  return client;
}

it('updates the next action from live tender cache changes without a page reload', async () => {
  const client = createClient({
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
  expect(
    within(
      screen.getByRole('group', { name: 'Next: Supplier bidding' })
    ).getByText('Responsible: Eligible suppliers')
  ).toBeInTheDocument();
  expect(
    screen.queryByText('Responsible: Invited suppliers')
  ).not.toBeInTheDocument();

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
    vi.mocked(procurementTenderDocumentService.readiness).mockResolvedValueOnce(
      {
        ready: true,
        effectiveTemplateReference: 'DOC/v1',
        ...(allowsNewRecipient === undefined ? {} : { allowsNewRecipient }),
      } as Awaited<
        ReturnType<typeof procurementTenderDocumentService.readiness>
      >
    );
    const client = createClient({
      defaultOptions: { queries: { retry: false, staleTime: Infinity } },
    });
    client.setQueryData(tenderDetailQueryKey('tender-1'), {
      id: 'tender-1',
      tenderNumber: 'TND-001',
      status: 'Published',
      bidCount: 0,
      sourcingCaseId: 'case-1',
    });
    render(
      <QueryClientProvider client={client}>
        <ProcurementUatRouteGuide />
      </QueryClientProvider>
    );
    expect(
      await screen.findByRole('link', {
        name: 'Current action: Open tender invitations',
      })
    ).toHaveAttribute('href', '/procurement/tenders/tender-1?tab=invitations');
    expect(
      screen.queryByRole('link', {
        name: 'Current action: View tender bids',
      })
    ).not.toBeInTheDocument();
    client.clear();
  }
);

it('reflects blocked readiness from the document workspace shared cache without a page reload', async () => {
  const client = createClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const readinessKey = [
    'procurement-tender-document-readiness',
    'Tender',
    'tender-1',
    false,
  ];
  const readiness = {
    ready: true,
    effectiveTemplateReference: 'DOC/v1',
    allowsNewRecipient: true,
    blockedReasons: [],
  };
  client.setQueryData(tenderDetailQueryKey('tender-1'), {
    id: 'tender-1',
    tenderNumber: 'TND-001',
    status: 'Approved',
    bidCount: 0,
    sourcingCaseId: 'case-1',
  });
  client.setQueryData(readinessKey, readiness);
  render(
    <QueryClientProvider client={client}>
      <ProcurementUatRouteGuide />
    </QueryClientProvider>
  );
  const current = screen.getByRole('group', {
    name: 'Current: Controlled documents and publication',
  });
  expect(within(current).getByText('Ready')).toBeInTheDocument();
  await act(async () => {
    client.setQueryData(readinessKey, {
      ...readiness,
      ready: false,
      blockedReasons: ['The submission deadline has passed.'],
    });
  });
  expect(await within(current).findByText('Blocked')).toBeInTheDocument();
  expect(within(current).queryByText('Ready')).not.toBeInTheDocument();
  expect(
    screen.getByRole('status', { name: 'Current stage blockers' })
  ).toHaveTextContent('The submission deadline has passed.');
  client.clear();
});

it('returns from ready approved documents to tender publication rather than linking to itself', () => {
  vi.mocked(usePathname).mockReturnValue(
    '/procurement/tenders/tender-1/document-controls'
  );
  const client = createClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  client.setQueryData(tenderDetailQueryKey('tender-1'), {
    id: 'tender-1',
    tenderNumber: 'TND-001',
    status: 'Approved',
    bidCount: 0,
    sourcingCaseId: 'case-1',
  });
  client.setQueryData(
    ['procurement-tender-document-readiness', 'Tender', 'tender-1', false],
    {
      ready: true,
      effectiveTemplateReference: 'DOC/v1',
      isSourcePublished: false,
      allowsNewRecipient: true,
      blockedReasons: [],
    }
  );
  render(
    <QueryClientProvider client={client}>
      <ProcurementUatRouteGuide />
    </QueryClientProvider>
  );
  expect(
    screen.getByRole('link', {
      name: 'Current action: Return to tender publication',
    })
  ).toHaveAttribute('href', '/procurement/tenders/tender-1');
  client.clear();
  vi.mocked(usePathname).mockReturnValue('/procurement/tenders/tender-1');
});

it.each(['evaluation', 'bid'])(
  'resolves a %s detail page to its real tender and retained milestones',
  async (family) => {
    vi.mocked(usePathname).mockReturnValue(
      family === 'evaluation'
        ? '/procurement/evaluations/eval-1'
        : '/procurement/bids/bid-1'
    );
    vi.mocked(getEvaluationById).mockResolvedValue({
      id: 'eval-1',
      tenderBidId: 'bid-1',
      status: 'Submitted',
    } as Awaited<ReturnType<typeof getEvaluationById>>);
    vi.mocked(getBidById).mockResolvedValue({
      id: 'bid-1',
      tenderId: 'tender-1',
      status: 'Evaluated',
    } as Awaited<ReturnType<typeof getBidById>>);
    const client = createClient({
      defaultOptions: { queries: { retry: false, staleTime: Infinity } },
    });
    client.setQueryData(tenderDetailQueryKey('tender-1'), {
      id: 'tender-1',
      tenderNumber: 'TND-001',
      status: 'Evaluated',
      bidCount: 1,
      sourcePurchaseRequisitionId: 'pr-1',
      sourcingCaseId: 'case-1',
      sourcingReleaseId: 'release-1',
    });
    client.setQueryData(
      ['procurement-tender-document-readiness', 'Tender', 'tender-1', false],
      {
        ready: false,
        blockedReasons: ['The submission deadline has passed.'],
      }
    );
    client.setQueryData(
      ['procurement-evaluation-committee-readiness', 'Tender', 'tender-1'],
      {
        hasControl: true,
        compositionReady: true,
        appointmentsReady: true,
        declarationsReady: true,
        quorumMet: true,
      }
    );
    client.setQueryData(
      ['procurement-evaluation-committee-control', 'Tender', 'tender-1'],
      { meetings: [] }
    );
    client.setQueryData(['procurement-uat-source-requisition', 'pr-1'], {
      id: 'pr-1',
      requisitionNumber: 'PR-001',
      status: 'Approved',
      linkage: { sourcePlanId: 'plan-1', budgetId: 'budget-1' },
    });
    client.setQueryData(['procurement-uat-source-submission', 'pr-1'], {
      sourcePlanId: 'plan-1',
      appSubmissionStatus: 'Acknowledged',
      appAcknowledgementReference: 'APP-ACK-1',
      appAcknowledgedAtUtc: '2026-09-05T18:00:00Z',
    });
    client.setQueryData(['procurement-uat-source-plan', 'plan-1'], {
      id: 'plan-1',
      planNumber: 'PLAN-001',
      status: 'Published',
      budgets: [{ id: 'budget-1', status: 'Approved' }],
    });
    render(
      <QueryClientProvider client={client}>
        <ProcurementUatRouteGuide />
      </QueryClientProvider>
    );
    expect(await screen.findByText('TND-001')).toBeInTheDocument();
    expect(await screen.findByText('9 complete')).toBeInTheDocument();
    expect(
      screen.getByLabelText('Process progress coverage')
    ).toHaveTextContent('Step 9 of 12');
    expect(
      within(
        screen.getByRole('group', {
          name: 'Current: Bid opening and evaluation',
        })
      ).getByText('Complete')
    ).toBeInTheDocument();
    expect(
      within(
        screen.getByRole('group', {
          name: 'Prerequisite: Evaluation committee and meeting',
        })
      ).getByText('Complete')
    ).toBeInTheDocument();
    expect(
      screen.queryByText('Blocked: The submission deadline has passed.')
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'Next action: Open award readiness' })
    ).toHaveAttribute('href', '/procurement/tenders/tender-1/award-readiness');
    client.clear();
  }
);

it('keeps missing or forbidden evidence unverified without retrying or changing lifecycle facts', async () => {
  vi.mocked(usePathname).mockReturnValue(
    '/procurement/tenders/tender-1/award-readiness'
  );
  vi.mocked(
    procurementEvaluationCommitteeService.readiness
  ).mockRejectedValueOnce(new Error('Forbidden'));
  const client = createClient({
    defaultOptions: { queries: { retry: true, staleTime: Infinity } },
  });
  client.setQueryData(tenderDetailQueryKey('tender-1'), {
    id: 'tender-1',
    tenderNumber: 'TND-001',
    status: 'Evaluated',
    bidCount: 1,
    sourcePurchaseRequisitionId: 'pr-1',
    sourcingCaseId: 'case-1',
    sourcingReleaseId: 'release-1',
  });
  render(
    <QueryClientProvider client={client}>
      <ProcurementUatRouteGuide />
    </QueryClientProvider>
  );
  expect(await screen.findByText('6 complete')).toBeInTheDocument();
  expect(screen.getByLabelText('Process progress coverage')).toHaveTextContent(
    'Step 10 of 12 · 5 unverified'
  );
  expect(screen.queryByText('0/12 complete')).not.toBeInTheDocument();
  expect(
    within(
      screen.getByRole('group', {
        name: 'Prerequisite: Bid opening and evaluation',
      })
    ).getByText('Complete')
  ).toBeInTheDocument();
  expect(procurementEvaluationCommitteeService.readiness).toHaveBeenCalledTimes(
    1
  );
  client.clear();
});

it('refreshes a retained tender query on navigation after a local-state scoring submission', async () => {
  const client = createClient({
    defaultOptions: { queries: { retry: false, staleTime: 300_000 } },
  });
  const source = {
    id: 'tender-1',
    tenderNumber: 'TND-001',
    status: 'Closed',
    bidCount: 1,
    sourcePurchaseRequisitionId: 'pr-1',
    sourcingCaseId: 'case-1',
    sourcingReleaseId: 'release-1',
  } as TenderDetailDto;
  client.setQueryData(tenderDetailQueryKey('tender-1'), source);
  const view = render(
    <QueryClientProvider client={client}>
      <ProcurementUatRouteGuide />
    </QueryClientProvider>
  );
  expect(await screen.findByText('TND-001')).toBeInTheDocument();
  await act(async () => {});
  vi.mocked(getTenderById).mockResolvedValue({
    ...source,
    status: 'Evaluated',
  });
  vi.mocked(usePathname).mockReturnValue(
    '/procurement/tenders/tender-1/award-readiness'
  );
  view.rerender(
    <QueryClientProvider client={client}>
      <ProcurementUatRouteGuide />
    </QueryClientProvider>
  );
  expect(
    await within(
      screen.getByRole('group', {
        name: 'Prerequisite: Bid opening and evaluation',
      })
    ).findByText('Complete')
  ).toBeInTheDocument();
  expect(
    client.getQueryData<TenderDetailDto>(tenderDetailQueryKey('tender-1'))
      ?.status
  ).toBe('Evaluated');
  client.clear();
});
