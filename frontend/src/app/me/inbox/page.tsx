'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowUpRight,
  CheckSquare,
  ClipboardList,
  Inbox,
  ListTodo,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  mePortalService,
  type PortalActionItem,
  type PortalApprovalItem,
  type PortalTaskItem,
} from '@/services/hr/me-portal.service';

/**
 * Approvals & Tasks (area 25 slice 11) — everything waiting on you, in three registers:
 * workflow approvals addressed to you (directly or through a role), workflow tasks assigned
 * to you, and the acknowledge/respond/accept acts the HR areas scattered, consolidated.
 *
 * READ AND NAVIGATE, deliberately: an approval row opens the record it asks you to sign,
 * and the approval act happens THERE, on the module's own surface — the only path that
 * carries the whole outcome (the generic inbox endpoints consume the approval but leave the
 * record stranded; measured, cross-module defect #15). No approve button here is a feature.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : null);

/** "StaffMovement" → "Staff Movement" — display only, never sent back. */
const humanizeEntityType = (t: string) => t.replace(/([a-z0-9])([A-Z])/g, '$1 $2');

const isOverdue = (v?: string | null) => !!v && new Date(v).getTime() < Date.now();

function ApprovalRow({ item }: { item: PortalApprovalItem }) {
  const heading = item.entityNumber
    ? `${item.entityNumber}${item.entityName ? ` — ${item.entityName}` : ''}`
    : item.entityName ?? humanizeEntityType(item.entityType);
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b px-4 py-3 last:border-b-0">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{heading}</span>
          <Badge variant="outline">{humanizeEntityType(item.entityType)}</Badge>
          {item.priority !== 'Normal' && <Badge variant="secondary">{item.priority}</Badge>}
          {item.approverRole && (
            <Badge variant="secondary" title={`Addressed to the ${item.approverRole} role`}>
              as {item.approverRole}
            </Badge>
          )}
        </div>
        <p className="mt-0.5 text-sm text-muted-foreground">
          {item.stepName}
          {' · requested '}
          {fmtDate(item.requestedDate)}
          {item.dueDate && (
            <span className={isOverdue(item.dueDate) ? 'font-medium text-destructive' : undefined}>
              {' · due '}
              {fmtDate(item.dueDate)}
            </span>
          )}
        </p>
      </div>
      {item.actionUrl ? (
        <Button variant="outline" size="sm" asChild>
          <Link href={item.actionUrl}>
            Open record
            <ArrowUpRight className="ml-1 h-3.5 w-3.5" />
          </Link>
        </Button>
      ) : (
        <span className="text-xs text-muted-foreground">No record link</span>
      )}
    </div>
  );
}

function TaskRow({ item }: { item: PortalTaskItem }) {
  const heading = item.entityNumber
    ? `${item.entityNumber}${item.entityName ? ` — ${item.entityName}` : ''}`
    : item.entityName ?? humanizeEntityType(item.entityType);
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b px-4 py-3 last:border-b-0">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{heading}</span>
          <Badge variant="outline">{humanizeEntityType(item.entityType)}</Badge>
        </div>
        <p className="mt-0.5 text-sm text-muted-foreground">
          {item.stepName}
          {' · assigned '}
          {fmtDate(item.createdDate)}
          {item.dueDate && (
            <span className={isOverdue(item.dueDate) ? 'font-medium text-destructive' : undefined}>
              {' · due '}
              {fmtDate(item.dueDate)}
            </span>
          )}
        </p>
      </div>
      {item.actionUrl ? (
        <Button variant="outline" size="sm" asChild>
          <Link href={item.actionUrl}>
            Open record
            <ArrowUpRight className="ml-1 h-3.5 w-3.5" />
          </Link>
        </Button>
      ) : (
        <span className="text-xs text-muted-foreground">No record link</span>
      )}
    </div>
  );
}

const ACTION_KIND_LABEL: Record<string, string> = {
  MovementResponse: 'Movement',
  SurchargeResponse: 'Asset charge',
  AssetAcknowledgement: 'Asset',
  DisciplineNotice: 'Discipline',
  GrievanceResponse: 'Grievance',
  RiskAssessmentAcknowledgement: 'Safety',
  TrainingBondAcceptance: 'Training bond',
};

function ActionItemRow({ item }: { item: PortalActionItem }) {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b px-4 py-3 last:border-b-0">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{item.title}</span>
          <Badge variant="outline">{ACTION_KIND_LABEL[item.kind] ?? item.kind}</Badge>
        </div>
        {(item.detail || item.date) && (
          <p className="mt-0.5 text-sm text-muted-foreground">
            {item.detail}
            {item.detail && item.date ? ' · ' : ''}
            {fmtDate(item.date)}
          </p>
        )}
      </div>
      <Button variant="outline" size="sm" asChild>
        <Link href={item.actionUrl}>
          Open
          <ArrowUpRight className="ml-1 h-3.5 w-3.5" />
        </Link>
      </Button>
    </div>
  );
}

export default function MyInboxPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['me', 'inbox'],
    queryFn: () => mePortalService.getInbox(),
  });

  const approvals = data?.approvals ?? [];
  const tasks = data?.tasks ?? [];
  const actionItems = data?.actionItems ?? [];
  const empty = !isLoading && !isError && approvals.length + tasks.length + actionItems.length === 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Approvals & Tasks"
        description="Everything waiting on you — approvals to give, tasks assigned to you, and things to acknowledge or respond to. Each row opens the record where the action happens."
        backHref="/me"
      />

      {isError && (
        <p className="text-sm text-muted-foreground">
          Your inbox could not be loaded right now. Try again in a moment.
        </p>
      )}

      {empty && (
        <EmptyState
          icon={Inbox}
          title="All caught up"
          description="Nothing is waiting on you. Approvals, assigned tasks and acknowledgements will appear here the moment they need you."
        />
      )}

      {approvals.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <CheckSquare className="h-4 w-4" />
              Approvals to give
              <Badge variant="secondary">{approvals.length}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {approvals.map((a) => (
              <ApprovalRow key={a.approvalId} item={a} />
            ))}
          </CardContent>
        </Card>
      )}

      {tasks.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <ListTodo className="h-4 w-4" />
              Tasks assigned to you
              <Badge variant="secondary">{tasks.length}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {tasks.map((t) => (
              <TaskRow key={t.stepInstanceId} item={t} />
            ))}
          </CardContent>
        </Card>
      )}

      {actionItems.length > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              <ClipboardList className="h-4 w-4" />
              To acknowledge or respond to
              <Badge variant="secondary">{actionItems.length}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {actionItems.map((i) => (
              <ActionItemRow key={`${i.kind}-${i.entityId}`} item={i} />
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
