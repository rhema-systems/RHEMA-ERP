'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { movementSubtypeService } from '@/services/hr/movement-subtype.service';
import { useToast } from '@/hooks/use-toast';
import { SUBTYPE_FOR_MOVEMENT } from '@/types/hr/movement-subtypes';
import type {
  StaffPromotionDetail,
  StaffTransferDetail,
  StaffDemotionDetail,
  StaffSecondmentDetail,
} from '@/types/hr/movement-subtypes';
import type { StaffMovementType } from '@/types/hr/movements';

type SubtypeDetail =
  | StaffPromotionDetail
  | StaffTransferDetail
  | StaffDemotionDetail
  | StaffSecondmentDetail;

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-4 py-1">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right">{value ?? '—'}</dd>
    </div>
  );
}

const yesNo = (v?: boolean) => (v ? 'Yes' : 'No');

/**
 * The type-specific half of a movement.
 *
 * Only four of the seven movement types carry one — a lateral move, a redesignation and an acting
 * appointment are fully described by the movement itself. The panel says so rather than showing an
 * empty "add detail" affordance for a type that can never have one, and the server agrees: a
 * promotion detail on a demotion is refused, as is a second detail row.
 */
export function MovementSubtypePanel({
  movementId,
  movementType,
}: {
  movementId: string;
  movementType: StaffMovementType;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);

  const kind = SUBTYPE_FOR_MOVEMENT[movementType];

  const { data: detail, isLoading } = useQuery<SubtypeDetail | null>({
    queryKey: ['hr', 'movement', movementId, 'subtype', kind],
    enabled: !!kind,
    queryFn: (): Promise<SubtypeDetail | null> => {
      switch (kind) {
        case 'promotion':
          return movementSubtypeService.getPromotionByMovement(movementId);
        case 'transfer':
          return movementSubtypeService.getTransferByMovement(movementId);
        case 'demotion':
          return movementSubtypeService.getDemotionByMovement(movementId);
        case 'secondment':
          return movementSubtypeService.getSecondmentByMovement(movementId);
        default:
          return Promise.resolve(null);
      }
    },
  });

  const createDetail = useMutation<SubtypeDetail>({
    mutationFn: (): Promise<SubtypeDetail> => {
      const base = { movementId };
      switch (kind) {
        case 'promotion':
          return movementSubtypeService.createPromotion({ ...base, type: 'MeritBased' });
        case 'transfer':
          return movementSubtypeService.createTransfer({
            ...base,
            type: 'Interdepartmental',
            reasonCategory: 'OperationalNeeds',
          });
        case 'demotion':
          return movementSubtypeService.createDemotion({ ...base, reason: 'PerformanceIssues' });
        case 'secondment':
          return movementSubtypeService.createSecondment({
            ...base,
            type: 'Internal',
            startDate: new Date().toISOString(),
            endDate: new Date(Date.now() + 180 * 86400000).toISOString(),
            termsAndConditions: '',
            objectives: '',
          });
        default:
          throw new Error('This movement type carries no detail record.');
      }
    },
    onSuccess: () => {
      toast({ title: 'Detail added' });
      setCreating(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'movement', movementId] });
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
    onSettled: () => setCreating(false),
  });

  if (!kind) {
    return (
      <Card>
        <CardContent className="p-6 text-sm text-muted-foreground">
          A {movementType.replace(/([A-Z])/g, ' $1').trim().toLowerCase()} has no type-specific
          detail — everything about it is on the movement itself.
        </CardContent>
      </Card>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!detail) {
    return (
      <Card>
        <CardContent className="p-6">
          <EmptyState
            title={`No ${kind} detail yet`}
            description={`This movement has no ${kind} detail record. One can be added while it is still being prepared.`}
          />
          <div className="mt-4 flex justify-center">
            <Button
              onClick={() => {
                setCreating(true);
                createDetail.mutate();
              }}
              disabled={creating}
            >
              {creating ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Plus className="mr-2 h-4 w-4" />
              )}
              Add {kind} detail
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base capitalize">{kind} detail</CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-x-8 text-sm sm:grid-cols-2">
          {kind === 'promotion' && (
            <>
              <Row label="Type" value={(detail as any).typeName} />
              <Row
                label="Grade bands up"
                value={
                  <span>
                    {(detail as any).gradeLevelIncrease}
                    <Badge variant="outline" className="ml-2">
                      computed
                    </Badge>
                  </span>
                }
              />
              <Row label="Acting promotion" value={yesNo((detail as any).isActingPromotion)} />
              <Row label="Acting until" value={fmtDate((detail as any).actingPeriodEndDate)} />
              <Row label="Extra responsibilities" value={(detail as any).additionalResponsibilities} />
              <Row label="Training required" value={yesNo((detail as any).requiresTraining)} />
            </>
          )}

          {kind === 'transfer' && (
            <>
              <Row label="Type" value={(detail as any).typeName} />
              <Row label="Reason" value={(detail as any).reasonCategoryName} />
              <Row label="Relocation" value={yesNo((detail as any).requiresRelocation)} />
              <Row label="Relocation allowance" value={money((detail as any).relocationAllowance)} />
              <Row label="Housing assistance" value={yesNo((detail as any).housingAssistanceProvided)} />
              <Row label="Transition (days)" value={(detail as any).transitionPeriodDays} />
              <Row label="Replacement" value={(detail as any).replacementEmployeeName} />
              <Row label="Inter-company" value={yesNo((detail as any).isInterCompany)} />
              {(detail as any).isInterCompany && (
                <Row label="Employment continues" value={yesNo((detail as any).employmentContinues)} />
              )}
            </>
          )}

          {kind === 'demotion' && (
            <>
              <Row label="Reason" value={(detail as any).reasonName} />
              <Row
                label="Grade bands down"
                value={
                  <span>
                    {(detail as any).gradeLevelDecrease}
                    <Badge variant="outline" className="ml-2">
                      computed
                    </Badge>
                  </span>
                }
              />
              <Row label="Disciplinary" value={yesNo((detail as any).isDisciplinaryAction)} />
              <Row label="Performance related" value={yesNo((detail as any).isPerformanceRelated)} />
              <Row label="Employee notified" value={fmtDate((detail as any).notificationDate)} />
              <Row label="Right to appeal" value={yesNo((detail as any).rightToAppeal)} />
              <Row label="Appeal deadline" value={fmtDate((detail as any).appealDeadline)} />
              <Row label="Employee's response" value={(detail as any).employeeResponse} />
            </>
          )}

          {kind === 'secondment' && (
            <>
              <Row label="Type" value={(detail as any).typeName} />
              <Row
                label="Period"
                value={`${fmtDate((detail as any).startDate)} → ${fmtDate((detail as any).endDate)}`}
              />
              <Row label="External" value={yesNo((detail as any).isExternal)} />
              <Row label="Host organisation" value={(detail as any).hostOrganization} />
              <Row label="Salary paid by home" value={yesNo((detail as any).salaryPaidByHomeOrganization)} />
              <Row label="Allowance" value={money((detail as any).secondmentAllowance)} />
              <Row label="Return guaranteed" value={yesNo((detail as any).returnGuaranteed)} />
              <Row
                label="Extension"
                value={
                  (detail as any).extensionAllowed
                    ? `Up to ${(detail as any).maxExtensionMonths ?? '—'} months`
                    : 'Not permitted'
                }
              />
            </>
          )}
        </dl>

        {(kind === 'promotion' || kind === 'demotion') && (
          <p className="mt-4 text-xs text-muted-foreground">
            The grade-band count is computed from the movement&apos;s salary grades, ordered by the
            bottom of each band — salary grades carry no explicit rank. It is recomputed whenever the
            movement or its detail is edited.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
