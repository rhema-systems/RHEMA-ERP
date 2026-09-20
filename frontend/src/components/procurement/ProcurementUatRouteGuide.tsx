'use client';

import React, { useEffect, useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { usePathname } from 'next/navigation';

import { ProcurementUatFlowSidebar } from '@/components/procurement/ProcurementUatFlowSidebar';
import type {
  ProcurementUatStageId,
  ProcurementUatStageStates,
} from '@/lib/procurement-uat-flow';
import { procurementTenderDocumentService } from '@/services/procurement-tender-document.service';
import {
  getTenderById,
  tenderDetailQueryKey,
  type TenderDetailDto,
} from '@/services/tenderService';
import type { ProcurementTenderDocumentReadiness } from '@/types/procurement-tender-document';
import { getEvaluationById } from '@/services/tenderEvaluationService';
import { getBidById } from '@/services/tenderBidService';
import { procurementEvaluationCommitteeService } from '@/services/procurement-evaluation-committee.service';
import type {
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeReadiness,
} from '@/types/procurement-evaluation-committee';
import {
  purchasingService,
  type PurchaseRequisitionDetailDto,
  type PurchaseRequisitionSubmissionReadinessDto,
} from '@/services/purchasingService';
import {
  procurementPlanService,
  type ProcurementPlanDetailDto,
} from '@/services/procurementPlanningService';

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
  'supplier-bidding': 'Eligible suppliers',
  'evaluation-committee': 'Committee chair and members',
  'bid-opening-evaluation': 'Evaluation committee',
  award: 'Head of Procurement and award approver',
  'purchase-order-commitment': 'Procurement and Finance approvers',
  'contract-activation': 'Procurement, Legal, and contract approver',
};

export interface ProcurementUatRoute {
  currentStage: ProcurementUatStageId;
  tenderId?: string;
  bidId?: string;
  evaluationId?: string;
}

export function resolveProcurementUatRoute(
  pathname: string
): ProcurementUatRoute | undefined {
  if (!pathname.startsWith('/procurement/')) return undefined;
  // Reusable template setup has no transaction source or bidding progress. Source-specific
  // tender/RFQ document-control workspaces still participate in the procurement flow below.
  if (/^\/procurement\/tender-documents(?:\/[^/]+)?\/?$/.test(pathname))
    return undefined;

  const recordId = (family: string) => {
    const id = pathname.match(
      new RegExp(`^/procurement/${family}/([^/]+)`)
    )?.[1];
    return id && !['new', 'create'].includes(id) ? id : undefined;
  };
  const tenderId = recordId('tenders');
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
    return {
      currentStage: 'bid-opening-evaluation',
      tenderId,
      bidId: recordId('bids'),
      evaluationId: recordId('evaluations'),
    };
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
  if (['evaluated', 'awarded'].includes(status)) return 'award';
  if (status === 'underevaluation') return 'bid-opening-evaluation';
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
      status: 'unknown',
      responsibleRole: stageRoles[next],
      href: stageLinks[next],
      actionLabel: 'Open next stage',
    };
  return result;
}

export function tenderStageStates(
  tender: TenderDetailDto,
  readiness?: ProcurementTenderDocumentReadiness,
  committee?: ProcurementEvaluationCommitteeReadiness,
  control?: ProcurementEvaluationCommitteeControl
): ProcurementUatStageStates {
  const status = tender.status.toLowerCase();
  const evaluated = ['evaluated', 'awarded'].includes(status);
  const evaluationStarted =
    status === 'underevaluation' ||
    tender.bids?.some((bid) =>
      ['underevaluation', 'evaluated'].includes(bid.status.toLowerCase())
    );
  const prepared = [
    'approved',
    'published',
    'closed',
    'underevaluation',
    'evaluated',
    'awarded',
  ].includes(status);
  // Publication is a retained milestone. A deadline expiry only blocks a future
  // publication action; it cannot roll an already-published source back to Blocked.
  const published =
    Boolean(tender.publishDate) ||
    readiness?.isSourcePublished === true ||
    ['published', 'closed', 'underevaluation', 'evaluated', 'awarded'].includes(
      status
    );
  const biddingFinished = [
    'closed',
    'underevaluation',
    'evaluated',
    'awarded',
  ].includes(status);
  const committeeConfirmed =
    Boolean(
      control?.meetings?.some(
        (meeting) =>
          meeting.quorumMet &&
          Boolean(meeting.quorumIntegrityHash) &&
          ['QuorumConfirmed', 'Closed'].includes(meeting.status)
      )
    ) ||
    Boolean(
      committee?.hasControl &&
        committee.compositionReady &&
        committee.appointmentsReady &&
        committee.declarationsReady &&
        committee.quorumMet
    );
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
    'sourcing-release-case':
      tender.sourcingCaseId && tender.sourcingReleaseId
        ? {
            status: 'complete',
            context: 'The sourcing case and immutable release are linked.',
            href: `/procurement/sourcing-cases?caseId=${tender.sourcingCaseId}`,
            actionLabel: 'Open sourcing case',
          }
        : {
            status: 'blocked',
            blockers: [
              'The governed sourcing case or immutable release is not linked.',
            ],
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
              : 'Approved controlled documents must be bound and available before publication.',
            href: documentHref,
            actionLabel: 'Open document register',
          }
        : {
            status: prepared ? 'unknown' : 'not-started',
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
      responsibleRole: 'Eligible suppliers',
      context: published
        ? `${tender.bidCount} bid${tender.bidCount === 1 ? '' : 's'} recorded.`
        : 'Publication must complete before suppliers can bid.',
      href: `${tenderHref}?tab=${biddingFinished || readiness?.allowsNewRecipient ? 'bids' : 'invitations'}`,
      actionLabel:
        biddingFinished || readiness?.allowsNewRecipient
          ? 'View tender bids'
          : 'Open tender invitations',
    },
    'evaluation-committee': {
      status: committeeConfirmed
        ? 'complete'
        : committee
          ? committee.hasControl
            ? 'in-progress'
            : 'not-started'
          : 'unknown',
      responsibleRole: 'Evaluation committee coordinator',
      context: committeeConfirmed
        ? 'The source-specific committee has a confirmed quorum and attendance record.'
        : 'Review member acceptance, attendance and quorum in the source-specific committee.',
      href: tender.sourcingCaseId
        ? `${tenderHref}/committee-controls`
        : `${tenderHref}?tab=evaluators`,
      actionLabel: 'Open evaluation committee',
    },
    'bid-opening-evaluation': {
      status: evaluated
        ? 'complete'
        : evaluationStarted
          ? 'in-progress'
          : biddingFinished
            ? 'ready'
            : 'not-started',
      responsibleRole: stageRoles['bid-opening-evaluation'],
      context: evaluated
        ? 'The tender evaluation is complete.'
        : 'Open eligible bids and submit all required committee score sheets.',
      href: `${tenderHref}?tab=bids`,
      actionLabel: 'Open tender bids',
    },
    award: {
      status:
        status === 'awarded'
          ? 'complete'
          : evaluated
            ? 'in-progress'
            : 'not-started',
      context:
        evaluated && status !== 'awarded'
          ? 'Evaluation is complete. Check award readiness and independent approval.'
          : undefined,
      responsibleRole: 'Independent award approver',
      href: `${tenderHref}/award-readiness`,
      actionLabel: 'Open award readiness',
    },
  };
}

export function upstreamStageStates(
  requisition?: PurchaseRequisitionDetailDto,
  submission?: PurchaseRequisitionSubmissionReadinessDto,
  plan?: ProcurementPlanDetailDto
): ProcurementUatStageStates {
  const states: ProcurementUatStageStates = {};
  if (requisition) {
    const approved =
      Boolean(requisition.approvedAt) ||
      ['approved', 'partiallyfulfilled', 'fulfilled'].includes(
        requisition.status.toLowerCase()
      );
    states['purchase-requisition'] = {
      status: approved
        ? 'complete'
        : ['rejected', 'cancelled'].includes(requisition.status.toLowerCase())
          ? 'failed'
          : 'in-progress',
      context: `${requisition.requisitionNumber} is ${requisition.status}.`,
      href: `/procurement/purchase-requisitions/${requisition.id}`,
      actionLabel: 'Open requisition',
    };
  }
  if (plan) {
    const approvedPlan =
      Boolean(plan.approvedDate) ||
      ['approved', 'published'].includes(plan.status.toLowerCase());
    const budget = plan.budgets?.find(
      (item) => item.id === (requisition?.linkage?.budgetId ?? plan.budgetId)
    );
    const approvedBudget =
      Boolean(budget?.approvedDate) ||
      budget?.status.toLowerCase() === 'approved';
    states['budget-plan'] = {
      status:
        approvedPlan && approvedBudget
          ? 'complete'
          : budget
            ? 'in-progress'
            : 'unknown',
      context:
        approvedPlan && approvedBudget
          ? `${plan.planNumber} and its linked budget retain approval evidence.`
          : 'Review the source plan and its linked budget approval.',
      href: `/procurement/planning/plans/${plan.id}`,
      actionLabel: 'Open procurement plan',
    };
  }
  if (submission) {
    const acknowledged =
      submission.appSubmissionStatus === 'Acknowledged' &&
      Boolean(
        submission.appAcknowledgementReference &&
          submission.appAcknowledgedAtUtc
      );
    const exempt =
      submission.basis === 'ApprovedException' &&
      Boolean(
        submission.exceptionApprovalReference &&
          submission.exceptionApprovedAtUtc
      );
    states['ghaneps-exchange'] = {
      status: acknowledged
        ? 'complete'
        : exempt
          ? 'skipped'
          : submission.appSubmissionId
            ? 'in-progress'
            : 'unknown',
      context: acknowledged
        ? `${submission.appSubmissionNumber ?? 'APP'} acknowledgement is recorded.`
        : exempt
          ? 'The requisition retains an approved APP exception.'
          : 'APP acknowledgement has not been verified for this source.',
      href: stageLinks['ghaneps-exchange'],
      actionLabel: 'Open APP exchange',
    };
  }
  return states;
}

export function ProcurementUatRouteGuide() {
  const pathname = usePathname();
  const queryClient = useQueryClient();
  const route = useMemo(() => resolveProcurementUatRoute(pathname), [pathname]);
  const evaluationId = route?.evaluationId ?? '';
  const { data: evaluation } = useQuery({
    queryKey: ['procurement-uat-evaluation-source', evaluationId],
    queryFn: () => getEvaluationById(evaluationId),
    enabled: Boolean(evaluationId),
    retry: false,
  });
  const bidId =
    route?.bidId ??
    (!evaluation?.tenderId ? evaluation?.tenderBidId : undefined) ??
    '';
  const { data: bid } = useQuery({
    queryKey: ['procurement-uat-bid-source', bidId],
    queryFn: () => getBidById(bidId),
    enabled: Boolean(bidId),
    retry: false,
  });
  const tenderId =
    route?.tenderId ?? evaluation?.tenderId ?? bid?.tenderId ?? '';
  const { data: tender } = useQuery({
    queryKey: tenderDetailQueryKey(tenderId),
    queryFn: () => getTenderById(tenderId),
    enabled: Boolean(tenderId),
    retry: false,
    staleTime: 0,
  });
  // Evaluation forms also use local state. Refresh the shared source when moving
  // between its pages, even when this layout and the tender query key are retained.
  useEffect(() => {
    if (!tenderId) return;
    void queryClient.invalidateQueries(
      { queryKey: tenderDetailQueryKey(tenderId), exact: true },
      { cancelRefetch: false }
    );
  }, [pathname, queryClient, tenderId]);
  const { data: readiness } = useQuery({
    queryKey: [
      'procurement-tender-document-readiness',
      'Tender',
      tenderId,
      false,
    ],
    queryFn: () =>
      procurementTenderDocumentService.readiness('Tender', tenderId),
    enabled: Boolean(tenderId),
    retry: false,
  });
  const { data: committee } = useQuery({
    queryKey: [
      'procurement-evaluation-committee-readiness',
      'Tender',
      tenderId,
    ],
    queryFn: () =>
      procurementEvaluationCommitteeService.readiness('Tender', tenderId),
    enabled: Boolean(tenderId && tender?.sourcingCaseId && tender.bidCount > 0),
    retry: false,
  });
  const { data: control } = useQuery({
    queryKey: ['procurement-evaluation-committee-control', 'Tender', tenderId],
    queryFn: () =>
      procurementEvaluationCommitteeService.get('Tender', tenderId),
    enabled: Boolean(tenderId && committee?.hasControl),
    retry: false,
  });
  const requisitionId = tender?.sourcePurchaseRequisitionId ?? '';
  const { data: requisition } = useQuery({
    queryKey: ['procurement-uat-source-requisition', requisitionId],
    queryFn: () => purchasingService.getPurchaseRequisitionById(requisitionId),
    enabled: Boolean(requisitionId),
    retry: false,
  });
  const { data: submission } = useQuery({
    queryKey: ['procurement-uat-source-submission', requisitionId],
    queryFn: () =>
      purchasingService.getPurchaseRequisitionSubmissionReadiness(
        requisitionId
      ),
    enabled: Boolean(requisitionId),
    retry: false,
  });
  const planId =
    requisition?.linkage?.sourcePlanId ?? submission?.sourcePlanId ?? '';
  const { data: plan } = useQuery({
    queryKey: ['procurement-uat-source-plan', planId],
    queryFn: () => procurementPlanService.getPlanById(planId),
    enabled: Boolean(planId),
    retry: false,
  });

  if (!route) return null;

  const currentStage = contextualTenderStage(route.currentStage, tender);
  const stageStates: ProcurementUatStageStates = {
    ...baseStageStates(currentStage),
    ...(tender ? tenderStageStates(tender, readiness, committee, control) : {}),
    ...upstreamStageStates(requisition, submission, plan),
  };
  if (
    pathname?.endsWith('/document-controls') &&
    tender?.status === 'Approved' &&
    readiness?.ready &&
    readiness.isSourcePublished === false
  ) {
    stageStates['tender-documents-publication'] = {
      ...stageStates['tender-documents-publication'],
      href: `/procurement/tenders/${tenderId}`,
      actionLabel: 'Return to tender publication',
    };
  }

  return (
    <ProcurementUatFlowSidebar
      currentStage={currentStage}
      stageStates={stageStates}
      recordReference={tender?.tenderNumber}
      className="w-72 shrink-0"
    />
  );
}
