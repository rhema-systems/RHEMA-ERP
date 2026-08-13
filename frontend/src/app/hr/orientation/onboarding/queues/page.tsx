'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  Search,
  ClipboardCheck,
  ShieldCheck,
  CheckCircle2,
  Package,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { onboardingPlanService } from '@/services/hr/onboarding.service';
import {
  ONBOARDING_TASK_CATEGORY_OPTIONS,
  ONBOARDING_ASSET_STATUS_OPTIONS,
} from '@/types/hr/onboarding';
import type { OnboardingTask, OnboardingAssetProvisionStatus } from '@/types/hr/onboarding';

type Queue = 'overdue' | 'verification' | 'assignee' | 'assets';

const categoryLabel = (v: string) =>
  ONBOARDING_TASK_CATEGORY_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Everything outstanding across every open plan, rather than per hire.
 *
 * These are cross-plan reads — overdue tasks and the verification queue are the two that get lost
 * when the only view is one plan at a time, because nobody opens a plan they are not working on.
 * The assets queue is here for the same reason: a laptop nobody ordered is invisible until day one.
 */
export default function OnboardingQueuesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [queue, setQueue] = useState<Queue>('overdue');
  const [search, setSearch] = useState('');
  const [assignee, setAssignee] = useState<{ id: string; label: string } | null>(null);
  const [assetStatus, setAssetStatus] =
    useState<OnboardingAssetProvisionStatus>('Pending');
  const [verifyingId, setVerifyingId] = useState<string | null>(null);

  const { data: overdue = [], isLoading: loadingOverdue } = useQuery({
    queryKey: ['hr', 'onboarding-queues', 'overdue'],
    queryFn: () => onboardingPlanService.getOverdueTasks(),
  });

  const { data: awaiting = [], isLoading: loadingAwaiting } = useQuery({
    queryKey: ['hr', 'onboarding-queues', 'verification'],
    queryFn: () => onboardingPlanService.getAllTasksByStatus('PendingVerification'),
  });

  const assigneeId = assignee?.id;
  const { data: mine = [], isLoading: loadingMine } = useQuery({
    queryKey: ['hr', 'onboarding-queues', 'assignee', assigneeId],
    queryFn: () => onboardingPlanService.getTasksByAssignee(assigneeId ?? ''),
    enabled: queue === 'assignee' && !!assigneeId,
  });

  const { data: assets = [], isLoading: loadingAssets } = useQuery({
    queryKey: ['hr', 'onboarding-queues', 'assets', assetStatus],
    queryFn: () => onboardingPlanService.getAllAssetsByStatus(assetStatus),
    enabled: queue === 'assets',
  });

  const verify = async (task: OnboardingTask) => {
    setVerifyingId(task.id);
    try {
      await onboardingPlanService.verifyTask(task.id, { taskId: task.id });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-queues'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-plans'] });
      toast({ title: 'Verified', description: task.taskName });
    } catch (error: any) {
      // Refused when the verifier is the person who completed it — the server's own message says so.
      toast({
        title: 'Could not verify',
        description: error?.message || 'Failed to verify the task.',
        variant: 'destructive',
      });
    } finally {
      setVerifyingId(null);
    }
  };

  const term = search.trim().toLowerCase();
  const matchTask = (t: OnboardingTask) =>
    !term ||
    t.taskName.toLowerCase().includes(term) ||
    (t.assignedToName ?? '').toLowerCase().includes(term);

  const tasks =
    queue === 'overdue' ? overdue : queue === 'verification' ? awaiting : queue === 'assignee' ? mine : [];
  const filteredTasks = tasks.filter(matchTask);
  const filteredAssets = assets.filter(
    (a) => !term || a.assetName.toLowerCase().includes(term),
  );

  const loading =
    queue === 'overdue'
      ? loadingOverdue
      : queue === 'verification'
        ? loadingAwaiting
        : queue === 'assignee'
          ? loadingMine
          : loadingAssets;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Onboarding Task Queues"
        description="What is outstanding across every open plan — overdue work, sign-offs waiting on someone, and kit still to be provisioned."
        backHref="/hr/orientation"
      />

      <MetricTiles
        tiles={[
          {
            label: 'Overdue tasks',
            value: overdue.length,
            icon: ClipboardCheck,
            tone: overdue.length > 0 ? 'danger' : 'default',
          },
          {
            label: 'Awaiting verification',
            value: awaiting.length,
            icon: ShieldCheck,
            tone: awaiting.length > 0 ? 'warning' : 'default',
            hint: 'Done, but needing a second signature',
          },
          {
            label: 'Mandatory & overdue',
            value: overdue.filter((t) => t.isMandatory).length,
            tone: overdue.some((t) => t.isMandatory) ? 'danger' : 'default',
          },
          {
            label: 'Unassigned & overdue',
            value: overdue.filter((t) => !t.assignedToId && !t.assignedOrganizationUnitId).length,
            hint: 'Nobody owns these',
            tone: 'warning',
          },
        ]}
      />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <Tabs value={queue} onValueChange={(v) => setQueue(v as Queue)}>
            <TabsList>
              <TabsTrigger value="overdue">Overdue</TabsTrigger>
              <TabsTrigger value="verification">Verification</TabsTrigger>
              <TabsTrigger value="assignee">By assignee</TabsTrigger>
              <TabsTrigger value="assets">Assets</TabsTrigger>
            </TabsList>
          </Tabs>

          {queue === 'assignee' && (
            <div className="min-w-[260px]">
              <EmployeePicker
                value={assignee?.id ?? null}
                initialLabel={assignee?.label ?? null}
                placeholder="Whose tasks?"
                onChange={(id, label) =>
                  setAssignee(id ? { id, label: label ?? id } : null)
                }
              />
            </div>
          )}

          {queue === 'assets' && (
            <Select
              value={assetStatus}
              onValueChange={(v) => setAssetStatus(v as OnboardingAssetProvisionStatus)}
            >
              <SelectTrigger className="w-[200px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ONBOARDING_ASSET_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          <div className="relative min-w-[220px] flex-1">
            <Search className="text-muted-foreground absolute left-2.5 top-2.5 h-4 w-4" />
            <Input
              placeholder="Search…"
              className="pl-8"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {loading ? (
            <p className="text-muted-foreground p-6 text-sm">Loading…</p>
          ) : queue === 'assets' ? (
            filteredAssets.length === 0 ? (
              <EmptyState
                icon={Package}
                title="Nothing here"
                description={`No assets are ${assetStatus.toLowerCase()}.`}
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Asset</TableHead>
                    <TableHead>Tag / serial</TableHead>
                    <TableHead>Needed by</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Plan</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredAssets.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">
                        {a.assetName}
                        {a.notes && (
                          <div className="text-muted-foreground mt-0.5 line-clamp-1 text-xs">
                            {a.notes}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground text-xs">
                        {[a.assetTag, a.serialNumber].filter(Boolean).join(' · ') || '—'}
                      </TableCell>
                      <TableCell>{fmt(a.requiredByDate)}</TableCell>
                      <TableCell>
                        <StatusBadge status={a.status} />
                      </TableCell>
                      <TableCell>
                        <Link
                          href={`/hr/orientation/onboarding/${a.onboardingPlanId}`}
                          className="hover:underline"
                        >
                          Open plan
                        </Link>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )
          ) : queue === 'assignee' && !assignee ? (
            <EmptyState
              icon={ClipboardCheck}
              title="Choose someone"
              description="Pick an employee to see the onboarding tasks assigned to them."
            />
          ) : filteredTasks.length === 0 ? (
            <EmptyState
              icon={queue === 'verification' ? ShieldCheck : ClipboardCheck}
              title="Nothing outstanding"
              description={
                queue === 'overdue'
                  ? 'No onboarding task is past its due date.'
                  : queue === 'verification'
                    ? 'Nothing is waiting on a sign-off.'
                    : 'This person has no onboarding tasks assigned.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Task</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Assigned to</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[180px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredTasks.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell>
                      <span className="font-medium">{t.taskName}</span>
                      {t.isMandatory && (
                        <Badge variant="secondary" className="ml-2 text-[10px]">
                          Mandatory
                        </Badge>
                      )}
                      {t.completedByName && (
                        <div className="text-muted-foreground mt-0.5 text-xs">
                          Completed by {t.completedByName} {fmt(t.completedDate)}
                        </div>
                      )}
                    </TableCell>
                    <TableCell>{categoryLabel(t.category)}</TableCell>
                    <TableCell className={t.isOverdue ? 'font-medium text-red-600' : undefined}>
                      {fmt(t.dueDate)}
                    </TableCell>
                    <TableCell>
                      {t.assignedToName ??
                        t.assignedOrganizationUnitName ??
                        t.ownerPositionTitle ?? (
                          <span className="text-muted-foreground">Unassigned</span>
                        )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={t.statusName ?? t.status} />
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-2">
                        {t.status === 'PendingVerification' && (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={verifyingId === t.id}
                            onClick={() => verify(t)}
                          >
                            {verifyingId === t.id ? (
                              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            ) : (
                              <CheckCircle2 className="mr-2 h-4 w-4" />
                            )}
                            Verify
                          </Button>
                        )}
                        <Button asChild size="sm" variant="ghost">
                          <Link href={`/hr/orientation/onboarding/${t.onboardingPlanId}`}>
                            Open plan
                          </Link>
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
