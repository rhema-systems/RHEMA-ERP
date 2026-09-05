import { describe, expect, it } from 'vitest';

import { resolveProcurementUatRoute } from './ProcurementUatRouteGuide';

describe('resolveProcurementUatRoute', () => {
  it.each([
    ['/procurement/planning/plans/plan-1', 'budget-plan'],
    ['/procurement/planning/app-submissions', 'ghaneps-exchange'],
    ['/procurement/purchase-requisitions/pr-1', 'purchase-requisition'],
    ['/procurement/sourcing-cases', 'sourcing-release-case'],
    [
      '/procurement/tenders/tender-1/document-controls',
      'tender-documents-publication',
    ],
    [
      '/procurement/rfqs/rfq-1/document-controls',
      'tender-documents-publication',
    ],
    [
      '/procurement/tenders/tender-1/committee-controls',
      'evaluation-committee',
    ],
    ['/procurement/tenders/tender-1/controls', 'bid-opening-evaluation'],
    ['/procurement/tenders/tender-1/award-readiness', 'award'],
    ['/procurement/purchase-orders/po-1', 'purchase-order-commitment'],
    ['/procurement/contracts/contract-1', 'contract-activation'],
  ] as const)('maps %s to %s', (path, expected) => {
    expect(resolveProcurementUatRoute(path)?.currentStage).toBe(expected);
  });

  it('does not render on unrelated procurement master-data pages', () => {
    expect(
      resolveProcurementUatRoute('/procurement/business-partners')
    ).toBeUndefined();
  });

  it.each([
    '/procurement/tender-documents',
    '/procurement/tender-documents/',
    '/procurement/tender-documents/template-1',
    '/procurement/tender-documents/template-1/',
  ])('does not imply transaction progress on reusable template setup %s', (path) => {
    expect(resolveProcurementUatRoute(path)).toBeUndefined();
  });

  it('captures the tender id for tender sub-pages', () => {
    expect(
      resolveProcurementUatRoute(
        '/procurement/tenders/tender-1/document-controls'
      )
    ).toMatchObject({ tenderId: 'tender-1' });
  });
});
