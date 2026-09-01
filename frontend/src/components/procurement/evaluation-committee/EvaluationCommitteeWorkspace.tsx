'use client';

import Link from 'next/link';
import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Loader2, RefreshCw, ShieldCheck } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  EvaluationCommitteeActionDialogs,
  type EvaluationCommitteeDialogAction,
} from '@/components/procurement/evaluation-committee/EvaluationCommitteeActionDialogs';
import { EvaluationCommitteeRegister } from '@/components/procurement/evaluation-committee/EvaluationCommitteeRegister';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { procurementEvaluationCommitteeService as service } from '@/services/procurement-evaluation-committee.service';
import type {
  ProcurementEvaluationPhase,
  ProcurementEvaluationSourceType,
} from '@/types/procurement-evaluation-committee';

const phases: ProcurementEvaluationPhase[] = [
  'Technical',
  'Financial',
  'Combined',
];

export function EvaluationCommitteeWorkspace({
  sourceType,
  sourceId,
}: {
  sourceType: ProcurementEvaluationSourceType;
  sourceId: string;
}) {
  const queryClient = useQueryClient();
  const { user, hasPermission } = useAuth();
  const [action, setAction] = useState<EvaluationCommitteeDialogAction>();
  const canAdminister = hasPermission('procurement.tender.administer');
  const canEvaluate = hasPermission('procurement.tender.evaluate');
  const canApprove = hasPermission('procurement.tender.approve');

  const readiness = useQuery({
    queryKey: [
      'procurement-evaluation-committee-readiness',
      sourceType,
      sourceId,
    ],
    queryFn: () => service.readiness(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });
  const control = useQuery({
    queryKey: [
      'procurement-evaluation-committee-control',
      sourceType,
      sourceId,
    ],
    queryFn: () => service.get(sourceType, sourceId),
    enabled: Boolean(readiness.data?.hasControl),
    retry: false,
  });
  const options = useQuery({
    queryKey: [
      'procurement-evaluation-committee-options',
      sourceType,
      sourceId,
    ],
    queryFn: () => service.options(sourceType, sourceId),
    enabled:
      Boolean(sourceId) &&
      Boolean(readiness.data) &&
      (canAdminister || canEvaluate || canApprove),
    retry: false,
  });
  const technicalEligibility = useQuery({
    queryKey: [
      'procurement-evaluation-scorer-eligibility',
      sourceType,
      sourceId,
      'Technical',
    ],
    queryFn: () => service.scorerEligibility(sourceType, sourceId, 'Technical'),
    enabled: canEvaluate && control.data?.status === 'Active',
    retry: false,
  });
  const financialEligibility = useQuery({
    queryKey: [
      'procurement-evaluation-scorer-eligibility',
      sourceType,
      sourceId,
      'Financial',
    ],
    queryFn: () => service.scorerEligibility(sourceType, sourceId, 'Financial'),
    enabled: canEvaluate && control.data?.status === 'Active',
    retry: false,
  });
  const combinedEligibility = useQuery({
    queryKey: [
      'procurement-evaluation-scorer-eligibility',
      sourceType,
      sourceId,
      'Combined',
    ],
    queryFn: () => service.scorerEligibility(sourceType, sourceId, 'Combined'),
    enabled: canEvaluate && control.data?.status === 'Active',
    retry: false,
  });

  const scorerEligibility = {
    Technical: technicalEligibility.data,
    Financial: financialEligibility.data,
    Combined: combinedEligibility.data,
  };

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: [
          'procurement-evaluation-committee-readiness',
          sourceType,
          sourceId,
        ],
      }),
      queryClient.invalidateQueries({
        queryKey: [
          'procurement-evaluation-committee-control',
          sourceType,
          sourceId,
        ],
      }),
      queryClient.invalidateQueries({
        queryKey: [
          'procurement-evaluation-committee-options',
          sourceType,
          sourceId,
        ],
      }),
      ...phases.map((phase) =>
        queryClient.invalidateQueries({
          queryKey: [
            'procurement-evaluation-scorer-eligibility',
            sourceType,
            sourceId,
            phase,
          ],
        })
      ),
    ]);
    await readiness.refetch();
  };

  const backHref =
    sourceType === 'Tender'
      ? `/procurement/tenders/${sourceId}`
      : `/procurement/rfqs/${sourceId}/controls`;
  const sourceLabel =
    sourceType === 'Tender' ? 'Tender' : 'Request for quotation';

  if (readiness.isLoading) {
    return (
      <div className="flex min-h-[420px] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );
  }

  if (readiness.isError || !readiness.data) {
    return (
      <Card>
        <CardContent className="flex min-h-72 flex-col items-center justify-center p-8 text-center">
          <ShieldCheck className="mb-4 h-10 w-10 text-muted-foreground" />
          <h1 className="text-lg font-semibold">
            Evaluation committee controls are unavailable
          </h1>
          <p className="mt-2 max-w-xl text-sm text-muted-foreground">
            {getProcurementProblemMessage(
              readiness.error,
              'The source was not found in this tenant or the current user is not authorized to view it.'
            )}
          </p>
          <Button asChild variant="outline" className="mt-5">
            <Link href={backHref}>Return to {sourceLabel.toLowerCase()}</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div
      className="space-y-6 p-4 md:p-6"
      data-testid="evaluation-committee-workspace"
    >
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <Button asChild variant="ghost" size="sm" className="-ml-3 mb-2">
            <Link href={backHref}>
              <ArrowLeft className="mr-2 h-4 w-4" />
              {sourceLabel}
            </Link>
          </Button>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold">
              Evaluation committee controls
            </h1>
            <Badge variant="outline">{sourceLabel}</Badge>
            {readiness.data.status && (
              <Badge variant="secondary">{readiness.data.status}</Badge>
            )}
          </div>
          <p className="mt-1 max-w-4xl text-sm text-muted-foreground">
            {readiness.data.sourceReference} · history-first composition,
            appointment acceptance, COI, signed attendance, server-derived
            quorum, scorer eligibility, immutable score locks, and independently
            approved recall.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => void refresh()}
          disabled={
            readiness.isFetching || control.isFetching || options.isFetching
          }
        >
          <RefreshCw
            className={`mr-2 h-4 w-4 ${
              readiness.isFetching || control.isFetching ? 'animate-spin' : ''
            }`}
          />
          Refresh controls
        </Button>
      </div>

      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Shared-control boundary</AlertTitle>
        <AlertDescription>
          Reusable committee membership stays in Access &amp; Committees. Shared
          workflow, evidence, identity, responsibility, notification, SOD, and
          audit services remain authoritative. This workspace retains only the
          exact source-specific execution history.
        </AlertDescription>
      </Alert>

      {control.isError && readiness.data.hasControl ? (
        <Alert variant="destructive">
          <AlertTitle>Committee history could not be loaded</AlertTitle>
          <AlertDescription>
            {control.error instanceof Error
              ? control.error.message
              : 'Reload the source-specific committee record.'}
          </AlertDescription>
        </Alert>
      ) : control.isLoading && readiness.data.hasControl ? (
        <div className="flex min-h-64 items-center justify-center">
          <Loader2 className="h-7 w-7 animate-spin" />
        </div>
      ) : (
        <EvaluationCommitteeRegister
          readiness={readiness.data}
          control={control.data}
          scorerEligibility={scorerEligibility}
          currentUserId={user?.id}
          canAdminister={canAdminister}
          canEvaluate={canEvaluate}
          canApprove={canApprove}
          onBind={() => setAction({ type: 'bind' })}
          onActivate={() => setAction({ type: 'activate' })}
          onAppointment={(member, accept) =>
            setAction({ type: 'appointment', member, accept })
          }
          onDeclareCoi={(member) => setAction({ type: 'coi', member })}
          onCreateMeeting={() => setAction({ type: 'meeting' })}
          onSignAttendance={(meeting, member) =>
            setAction({ type: 'attendance', meeting, member })
          }
          onConfirmQuorum={(meeting) => setAction({ type: 'quorum', meeting })}
          onRequestRecall={(scoreSheet) =>
            setAction({ type: 'recall', scoreSheet })
          }
          onDecideRecall={(recall, approve) =>
            setAction({ type: 'recall-decision', recall, approve })
          }
        />
      )}

      <EvaluationCommitteeActionDialogs
        action={action}
        onClose={() => setAction(undefined)}
        onCompleted={refresh}
        readiness={readiness.data}
        control={control.data}
        options={options.data}
      />
    </div>
  );
}
