'use client';

import { useEffect, useMemo, useState } from 'react';
import { usePathname } from 'next/navigation';

import { ProcurementUatFlowSidebar } from '@/components/procurement/ProcurementUatFlowSidebar';
import type {
  ProcurementUatStageId,
  ProcurementUatStageStates,
} from '@/lib/procurement-uat-flow';
import { procurementTenderDocumentService } from '@/services/procurement-tender-document.service';
import { getTenderById, type TenderDetailDto } from '@/services/tenderService';
import type { ProcurementTenderDocumentReadiness } from '@/types/procurement-tender-document';

const stageOrder: readonly ProcurementUatStageId[] = [
  'budget-plan',
  'ghaneps-exchange',
  'purchase-requisition',
  'sourcing-release-case',
  'tender-rfq-preparation',
  'tender-documents-publication',
  'supplier-bidding',
  'evaluation-committee',
  'bid-opening-evaluation',
  'award',
  'purchase-order-commitment',
  'contract-activation',
];

const stageLinks: Record<ProcurementUatStageId, string> = {
  'budget-plan': '/procurement/planning/plans',
  'ghaneps-exchange': '/procurement/planning/app-submissions',
  'purchase-requisition': '/procurement/purchase-requisitions',
  'sourcing-release-case': '/procurement/sourcing-cases',
  'tender-rfq-preparation': '/procurement/tenders',
  'tender-documents-publication': '/procurement/tender-documents',
  'supplier-bidding': '/procurement/tenders',
  'evaluation-committee': '/procurement/tenders',
  'bid-opening-evaluation': '/procurement/tenders',
  award: '/procurement/awards',
  'purchase-order-commitment': '/procurement/purchase-orders',
  'contract-activation': '/procurement/contracts',
};

const stageRoles: Record<ProcurementUatStageId, string> = {
  'budget-plan': 'Budget owner and Procurement Planner',
  'ghaneps-exchange': 'Procurement Officer',
  'purchase-requisition': 'Requisitioner and configured approver',
  'sourcing-release-case': 'Procurement Officer',
  'tender-rfq-preparation': 'Procurement Officer',
  'tender-documents-publication': 'Procurement Officer',
  'supplier-bidding': 'Invited suppliers',
  'evaluation-committee': 'Committee chair and members',
  'bid-opening-evaluation': 'Evaluation committee',
  award: 'Head of Procurement and award approver',
  'purchase-order-commitment': 'Procurement and Finance approvers',
  'contract-activation': 'Procurement, Legal, and contract approver',
};

export interface ProcurementUatRoute {
  currentStage: ProcurementUatStageId;
  tenderId?: string;
}

export function resolveProcurementUatRoute(
  pathname: string
): ProcurementUatRoute | undefined {
  if (!pathname.startsWith('/procurement/')) return undefined;
  // Reusable template setup has no transaction source or bidding progress. Source-specific
  // tender/RFQ document-control workspaces still participate in the procurement flow below.
  if (/^\/procurement\/tender-documents(?:\/[^/]+)?\/?$/.test(pathname))
    return undefined;

  const tenderId = pathname.match(/^\/procurement\/tenders\/([^/]+)/)?.[1];
  if (
    pathname.includes('/planning/app-submissions') ||
    pathname.includes('/ghaneps-exchange')
  )
    return { currentStage: 'ghaneps-exchange', tenderId };
  if (pathname.startsWith('/procurement/planning'))
    return { currentStage: 'budget-plan' };
  if (pathname.startsWith('/procurement/purchase-requisitions'))
    return { currentStage: 'purchase-requisition' };
  if (pathname.startsWith('/procurement/sourcing-cases'))
    return { currentStage: 'sourcing-release-case' };
  if (
    pathname.startsWith('/procurement/tender-documents') ||
    pathname.includes('/document-controls')
  )
    return { currentStage: 'tender-documents-publication', tenderId };
  if (pathname.includes('/committee-controls'))
    return { currentStage: 'evaluation-committee', tenderId };
  if (
    pathname.startsWith('/procurement/bids') ||
    pathname.startsWith('/procurement/evaluations') ||
    pathname.includes('/compare-bids') ||
    pathname.includes('/evaluation-report') ||
    pathname.includes('/controls')
  )
    return { currentStage: 'bid-opening-evaluation', tenderId };
  if (
    pathname.startsWith('/procurement/awards') ||
    pathname.includes('/award-readiness') ||
    pathname.includes('/create-award')
  )
    return { currentStage: 'award', tenderId };
  if (pathname.startsWith('/procurement/purchase-orders'))
    return { currentStage: 'purchase-order-commitment' };
  if (
    pathname.startsWith('/procurement/contracts') ||
    pathname.startsWith('/procurement/contract-operations')
  )
    return { currentStage: 'contract-activation' };
  if (
    pathname.startsWith('/procurement/tenders') ||
    pathname.startsWith('/procurement/rfqs')
  )
    return { currentStage: 'tender-rfq-preparation', tenderId };

  return undefined;
}

function contextualTenderStage(
  routeStage: ProcurementUatStageId,
  tender?: TenderDetailDto
): ProcurementUatStageId {
  if (!tender || routeStage !== 'tender-rfq-preparation') return routeStage;
  const status = tender.status.toLowerCase();
  if (status === 'approved') return 'tender-documents-publication';
  if (status === 'published')
    return tender.bidCount > 0 ? 'evaluation-committee' : 'supplier-bidding';
  if (status === 'closed')
    return tender.bidCount > 0 ? 'evaluation-committee' : 'supplier-bidding';
  if (status === 'awarded') return 'award';
  return routeStage;
}

function baseStageStates(
  currentStage: ProcurementUatStageId
): ProcurementUatStageStates {
  const currentIndex = stageOrder.indexOf(currentStage);
  const previous = currentIndex > 0 ? stageOrder[currentIndex - 1] : undefined;
  const next =
    currentIndex < stageOrder.length - 1
      ? stageOrder[currentIndex + 1]
      : undefined;
  const result: ProcurementUatStageStates = {
    [currentStage]: {
      status: 'in-progress',
      responsibleRole: stageRoles[currentStage],
      context:
        "Use this page's live status and readiness controls before continuing.",
    },
  };
  if (previous)
    result[previous] = {
      status: 'unknown',
      href: stageLinks[previous],
      actionLabel: 'Review prerequisite',
      context: 'Confirm the linked prerequisite record is complete.',
    };
  if (next)
    result[next] = {
      status: 'not-started',
      responsibleRole: stageRoles[next],
      href: stageLinks[next],
      actionLabel: 'Open next stage',
    };
  return result;
}

function tenderStageStates(
  tender: TenderDetailDto,
  readiness?: ProcurementTenderDocumentReadiness
): ProcurementUatStageStates {
  const status = tender.status.toLowerCase();
  const prepared = ['approved', 'published', 'closed', 'awarded'].includes(
    status
  );
  const published = ['published', 'closed', 'awarded'].includes(status);
  const biddingFinished = ['closed', 'awarded'].includes(status);
  const tenderHref = `/procurement/tenders/${tender.id}`;
  const documentHref = `${tenderHref}/document-controls`;

  return {
    'purchase-requisition': tender.sourcePurchaseRequisitionId
      ? {
          status: 'complete',
          context: 'The tender retains its approved requisition lineage.',
          href: `/procurement/purchase-requisitions/${tender.sourcePurchaseRequisitionId}`,
          actionLabel: 'Open requisition',
        }
      : {
          status: 'blocked',
          blockers: ['No source purchase requisition is linked.'],
        },
    'sourcing-release-case': tender.sourcingCaseId
      ? {
          status: 'complete',
          context: 'The sourcing case and immutable release are linked.',
          href: `/procurement/sourcing-cases?caseId=${tender.sourcingCaseId}`,
          actionLabel: 'Open sourcing case',
        }
      : {
          status: 'blocked',
          blockers: ['No governed sourcing case is linked.'],
        },
    'tender-rfq-preparation': {
      status:
        status === 'cancelled' || status === 'rejected'
          ? 'failed'
          : prepared
            ? 'complete'
            : 'in-progress',
      responsibleRole: 'Procurement Officer and configured approver',
      context: `${tender.tenderNumber} is ${tender.status}.`,
      href: tenderHref,
      actionLabel: 'Open tender',
    },
    'tender-documents-publication': published
      ? {
          status: 'complete',
          context: 'The tender has been published.',
          href: documentHref,
          actionLabel: 'Open document register',
        }
      : readiness
        ? {
            status: readiness.ready ? 'ready' : 'blocked',
            blockers: readiness.blockedReasons,
            responsibleRole: 'Procurement Officer',
            context: readiness.effectiveTemplateReference
              ? `Effective template: ${readiness.effectiveTemplateReference}`
              : 'Controlled documents must be bound and issued before publication.',
            href: documentHref,
            actionLabel: 'Open document register',
          }
        : {
            status: prepared ? 'ready' : 'not-started',
            responsibleRole: 'Procurement Officer',
            href: documentHref,
            actionLabel: 'Check publication readiness',
          },
    'supplier-bidding': {
      status: biddingFinished
        ? tender.bidCount > 0
          ? 'complete'
          : 'blocked'
        : published
          ? 'in-progress'
          : 'not-started',
      blockers:
        biddingFinished && tender.bidCount === 0
          ? ['Bidding closed without a submitted bid.']
          : undefined,
      responsibleRole: 'Invited suppliers',
      context: published
        ? `${tender.bidCount} bid${tender.bidCount === 1 ? '' : 's'} recorded.`
        : 'Publication must complete before suppliers can bid.',
      href: tenderHref,
      actionLabel: 'Open tender invitations',
    },
    award: {
      status: status === 'awarded' ? 'complete' : 'not-started',
      responsibleRole: 'Independent award approver',
      href: `${tenderHref}/award-readiness`,
      actionLabel: 'Open award readiness',
    },
  };
}

export function ProcurementUatRouteGuide() {
  const pathname = usePathname();
  const route = useMemo(() => resolveProcurementUatRoute(pathname), [pathname]);
  const [tender, setTender] = useState<TenderDetailDto>();
  const [readiness, setReadiness] =
    useState<ProcurementTenderDocumentReadiness>();

  useEffect(() => {
    let active = true;
    setTender(undefined);
    setReadiness(undefined);
    if (!route?.tenderId)
      return () => {
        active = false;
      };

    void Promise.allSettled([
      getTenderById(route.tenderId),
      procurementTenderDocumentService.readiness('Tender', route.tenderId),
    ]).then(([tenderResult, readinessResult]) => {
      if (!active) return;
      if (tenderResult.status === 'fulfilled') setTender(tenderResult.value);
      if (readinessResult.status === 'fulfilled')
        setReadiness(readinessResult.value);
    });

    return () => {
      active = false;
    };
  }, [route?.tenderId]);

  if (!route) return null;

  const currentStage = contextualTenderStage(route.currentStage, tender);
  const stageStates: ProcurementUatStageStates = {
    ...baseStageStates(currentStage),
    ...(tender ? tenderStageStates(tender, readiness) : {}),
  };

  return (
    <ProcurementUatFlowSidebar
      currentStage={currentStage}
      stageStates={stageStates}
      recordReference={tender?.tenderNumber}
      className="w-72 shrink-0"
    />
  );
}
