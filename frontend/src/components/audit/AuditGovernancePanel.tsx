'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  ArchiveRestore,
  LockKeyhole,
  ShieldCheck,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/hooks/use-auth';
import { auditGovernanceService } from '@/services/audit-governance.service';
import type {
  AuditLifecycleCommand,
  AuditRecordGovernance,
} from '@/types/audit-governance';

type LifecycleCommand = 'hold' | 'release' | 'archive' | 'restore';

const dateTime = (value: string) =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));

const request = (reason: string): AuditLifecycleCommand => {
  const key =
    globalThis.crypto?.randomUUID?.() ??
    `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return { reason: reason.trim(), requestKey: key, correlationId: key };
};

export function AuditGovernancePanel({
  storeKey,
  recordId,
}: {
  storeKey: string;
  recordId: string;
}) {
  const { hasPermission } = useAuth();
  const queryClient = useQueryClient();
  const [reason, setReason] = useState('');
  const canManage = hasPermission('settings.update');
  const queryKey = ['audit-record-governance', storeKey, recordId];
  const governance = useQuery({
    queryKey,
    queryFn: () => auditGovernanceService.get(storeKey, recordId),
  });
  const command = useMutation({
    mutationFn: async (action: LifecycleCommand) => {
      const payload = request(reason);
      if (action === 'hold')
        return auditGovernanceService.placeLegalHold(
          storeKey,
          recordId,
          payload
        );
      if (action === 'release')
        return auditGovernanceService.releaseLegalHold(
          storeKey,
          recordId,
          payload
        );
      if (action === 'archive')
        return auditGovernanceService.archive(storeKey, recordId, payload);
      return auditGovernanceService.restore(storeKey, recordId, payload);
    },
    onSuccess: (value: AuditRecordGovernance) => {
      queryClient.setQueryData(queryKey, value);
      setReason('');
    },
  });

  if (governance.isLoading) {
    return (
      <div className="rounded-md border p-3 text-sm text-muted-foreground">
        Loading shared retention controls…
      </div>
    );
  }
  if (governance.isError || !governance.data) {
    return (
      <Alert variant="destructive">
        <LockKeyhole className="h-4 w-4" />
        <AlertTitle>Retention controls unavailable</AlertTitle>
        <AlertDescription>{governance.error?.message}</AlertDescription>
      </Alert>
    );
  }

  const item = governance.data;
  return (
    <div className="space-y-3 rounded-md border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2 font-medium">
            <ShieldCheck className="h-4 w-4 text-emerald-700" />
            Shared audit retention
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            Immutable for {item.retentionDays.toLocaleString()} days · retain
            through {dateTime(item.retainUntilUtc)}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Badge variant="outline">Immutable</Badge>
          {item.isLegalHold && <Badge variant="secondary">Legal hold</Badge>}
          {item.isArchived && <Badge variant="secondary">Archived</Badge>}
        </div>
      </div>

      {item.archiveReference && (
        <p className="break-all font-mono text-[11px] text-muted-foreground">
          {item.archiveReference}
        </p>
      )}

      {canManage && (
        <div className="space-y-2 border-t pt-3">
          <Label htmlFor={`audit-governance-reason-${recordId}`}>
            Governance reason
          </Label>
          <div className="flex flex-wrap gap-2">
            <Input
              id={`audit-governance-reason-${recordId}`}
              className="min-w-64 flex-1"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Required reason (minimum 5 characters)"
              maxLength={1000}
            />
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={command.isPending || reason.trim().length < 5}
              onClick={() =>
                command.mutate(item.isLegalHold ? 'release' : 'hold')
              }
            >
              <LockKeyhole className="mr-2 h-4 w-4" />
              {item.isLegalHold ? 'Release hold' : 'Place hold'}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={command.isPending || reason.trim().length < 5}
              onClick={() =>
                command.mutate(item.isArchived ? 'restore' : 'archive')
              }
            >
              {item.isArchived ? (
                <ArchiveRestore className="mr-2 h-4 w-4" />
              ) : (
                <Archive className="mr-2 h-4 w-4" />
              )}
              {item.isArchived ? 'Restore' : 'Archive'}
            </Button>
          </div>
          {command.isError && (
            <p className="text-sm text-destructive">{command.error.message}</p>
          )}
        </div>
      )}

      {item.actions.length > 0 && (
        <p className="text-xs text-muted-foreground">
          {item.actions.length} lifecycle action(s); latest by{' '}
          {item.actions.at(-1)?.actorName}. Integrity{' '}
          {item.actions.every((action) => action.integrityValid)
            ? 'verified'
            : 'requires review'}
          .
        </p>
      )}
    </div>
  );
}
