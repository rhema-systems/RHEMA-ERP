'use client';

import Link from 'next/link';
import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Loader2, RefreshCw } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  TenderDocumentAcknowledgementDialog,
  TenderDocumentActionDialogs,
  TenderDocumentDecisionDialog,
} from '@/components/procurement/tender-documents/TenderDocumentActionDialogs';
import { TenderDocumentRegister } from '@/components/procurement/tender-documents/TenderDocumentRegister';
import { useAuth } from '@/hooks/use-auth';
import { procurementTenderDocumentService as service } from '@/services/procurement-tender-document.service';
import type {
  ProcurementTenderDocumentAcknowledgementOutcome,
  ProcurementTenderDocumentChange,
  ProcurementTenderDocumentSourceType,
} from '@/types/procurement-tender-document';

export function TenderDocumentRegisterWorkspace({
  sourceType,
  sourceId,
  external = false,
}: {
  sourceType: ProcurementTenderDocumentSourceType;
  sourceId: string;
  external?: boolean;
}) {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage =
    !external && hasPermission('procurement.tender.administer');
  const canApprove = !external && hasPermission('procurement.tender.approve');
  const [decision, setDecision] = useState<{
    change: ProcurementTenderDocumentChange;
    action: 'Approve' | 'Reject';
  }>();
  const [acknowledgement, setAcknowledgement] = useState<{
    issuanceId?: string;
    changeRecipientId?: string;
    outcome: ProcurementTenderDocumentAcknowledgementOutcome;
  }>();

  const readiness = useQuery({
    queryKey: [
      'procurement-tender-document-readiness',
      sourceType,
      sourceId,
      external,
    ],
    queryFn: () => service.readiness(sourceType, sourceId),
    enabled: Boolean(sourceId),
  });
  const register = useQuery({
    queryKey: [
      'procurement-tender-document-register',
      sourceType,
      sourceId,
      external,
    ],
    queryFn: () => service.getRegister(sourceType, sourceId),
    enabled: Boolean(readiness.data?.hasRegister),
  });
  const approvedTemplates = useQuery({
    queryKey: [
      'procurement-tender-document-approved-options',
      readiness.data?.method,
    ],
    queryFn: () =>
      service.searchTemplates({
        status: 'Published',
        method: readiness.data?.method,
        effectiveAtUtc: new Date().toISOString(),
        page: 1,
        pageSize: 200,
      }),
    enabled: !external && Boolean(readiness.data?.method),
  });
  const workflows = useQuery({
    queryKey: ['procurement-tender-document-workflows'],
    queryFn: service.workflowOptions,
    enabled: !external,
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: [
          'procurement-tender-document-readiness',
          sourceType,
          sourceId,
        ],
      }),
      queryClient.invalidateQueries({
        queryKey: [
          'procurement-tender-document-register',
          sourceType,
          sourceId,
        ],
      }),
    ]);
    const refreshedReadiness = await readiness.refetch();
    if (refreshedReadiness.data?.hasRegister) await register.refetch();
  };

  const backHref = external
    ? `/external-portal/tenders/${sourceId}`
    : sourceType === 'Tender'
      ? `/procurement/tenders/${sourceId}`
      : '/procurement/rfqs';
  const sourceLabel =
    sourceType === 'Tender' ? 'Tender' : 'Request for quotation';

  if (readiness.isLoading)
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );

  if (readiness.isError || !readiness.data)
    return (
      <div className="space-y-4 p-6">
        <Button asChild variant="ghost">
          <Link href={backHref}>
            <ArrowLeft className="mr-2 h-4 w-4" /> {sourceLabel}
          </Link>
        </Button>
        <Alert variant="destructive">
          <AlertTitle>Controlled document register unavailable</AlertTitle>
          <AlertDescription>
            The source is missing, outside the current tenant or business
            partner, or this account is not authorized to view it.
          </AlertDescription>
        </Alert>
      </div>
    );

  return (
    <div
      className="space-y-6 p-6"
      data-testid={
        external
          ? 'external-tender-document-register-page'
          : 'tender-document-register-page'
      }
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Button asChild variant="ghost" className="mb-2 px-0">
            <Link href={backHref}>
              <ArrowLeft className="mr-2 h-4 w-4" /> {sourceLabel}
            </Link>
          </Button>
          <h1 className="text-2xl font-semibold">
            {external
              ? 'My controlled tender documents'
              : 'Tender document control register'}
          </h1>
          <p className="text-sm text-muted-foreground">
            {readiness.data.sourceReference} · exact approved version, issue
            receipts, addenda, extensions and acknowledgements
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge
            variant={readiness.data.ready ? 'default' : 'destructive'}
          >
            {readiness.data.ready ? 'Ready' : 'Blocked'}
          </Badge>
          <Button
            variant="outline"
            size="sm"
            onClick={() => void refresh()}
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
        </div>
      </div>

      <TenderDocumentRegister
        readiness={readiness.data}
        register={register.data}
        external={external}
        canApprove={canApprove}
        onDecision={(change, action) => setDecision({ change, action })}
        onAcknowledge={(target, outcome) =>
          setAcknowledgement({ ...target, outcome })
        }
      />

      {!external && (
        <TenderDocumentActionDialogs
          readiness={readiness.data}
          register={register.data}
          approvedTemplates={approvedTemplates.data?.items ?? []}
          workflows={workflows.data ?? []}
          canManage={canManage}
          onChanged={refresh}
        />
      )}

      <TenderDocumentDecisionDialog
        change={decision?.change}
        action={decision?.action ?? 'Approve'}
        open={Boolean(decision)}
        onOpenChange={(open) => !open && setDecision(undefined)}
        onChanged={refresh}
      />

      <TenderDocumentAcknowledgementDialog
        open={Boolean(acknowledgement)}
        onOpenChange={(open) => !open && setAcknowledgement(undefined)}
        issuanceId={acknowledgement?.issuanceId}
        changeRecipientId={acknowledgement?.changeRecipientId}
        outcome={acknowledgement?.outcome ?? 'Acknowledged'}
        onChanged={refresh}
      />
    </div>
  );
}
