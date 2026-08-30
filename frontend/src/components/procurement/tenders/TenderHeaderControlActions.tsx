'use client';

import React from 'react';
import Link from 'next/link';
import { Award, Share2, Users } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { useAuth } from '@/hooks/use-auth';
import { getTenderHeaderActions } from '@/lib/procurement-tender-header-actions';
import type { ProcurementMethodType } from '@/types/procurement-policy';

export function TenderHeaderControlActions({
  tenderId,
  tenderType,
  sourcingCaseId,
  sourcingMethod,
}: {
  tenderId: string;
  tenderType?: string;
  sourcingCaseId?: string;
  sourcingMethod?: ProcurementMethodType | number;
}) {
  const { hasPermission } = useAuth();
  const actions = getTenderHeaderActions({
    tenderId,
    tenderType,
    sourcingCaseId,
    sourcingMethod,
    canReadProcurementRecords: hasPermission('procurement.records.read'),
  });

  return (
    <>
      {actions.showCommitteeControls && (
        <Button asChild variant="outline">
          <Link href={actions.committeeControlsHref}>
            <Users className="mr-2 h-4 w-4" />
            Committee Controls
          </Link>
        </Button>
      )}
      {actions.showAwardReadiness && (
        <Button asChild variant="outline">
          <Link href={actions.awardReadinessHref}>
            <Award className="mr-2 h-4 w-4" />
            Award Readiness
          </Link>
        </Button>
      )}
      {actions.showGhanepsExchange && (
        <Button asChild variant="outline">
          <Link href={actions.ghanepsExchangeHref}>
            <Share2 className="mr-2 h-4 w-4" />
            GHANEPS Exchange
          </Link>
        </Button>
      )}
    </>
  );
}
