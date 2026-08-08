'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  RefreshCw,
  ShieldCheck,
  UserRoundCheck,
} from 'lucide-react';

import { useAuth } from '../../hooks/use-auth';
import { useToast } from '../../hooks/use-toast';
import {
  hrIdentityReconciliationService,
  type HrIdentityReconciliationDashboard,
  type HrIdentityReconciliationRun,
  type HrIdentityReconciliationState,
  type HrIdentityUserOption,
  type HrIdentityWorkflowIssue,
} from '../../services/hrIdentityReconciliationService';
import { Badge } from '../ui/badge';
import { Button } from '../ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '../ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import { Label } from '../ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/table';
import { Textarea } from '../ui/textarea';

type ActionDialog =
  | { kind: 'issue'; item: HrIdentityWorkflowIssue }
  | { kind: 'reactivation'; item: HrIdentityReconciliationState }
  | null;

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';

const errorMessage = (error: unknown, fallback: string) => {
  if (error instanceof Error && error.message) return error.message;
  return fallback;
};

const statusVariant = (active: boolean) => (active ? 'default' : 'destructive');

export function HrIdentityReconciliationWorkspace() {
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const { toast } = useToast();
  const [dashboard, setDashboard] =
    useState<HrIdentityReconciliationDashboard | null>(null);
  const [replacementUsers, setReplacementUsers] = useState<
    HrIdentityUserOption[]
  >([]);
  const [loading, setLoading] = useState(true);
  const [working, setWorking] = useState(false);
  const [dialog, setDialog] = useState<ActionDialog>(null);
  const [replacementUserId, setReplacementUserId] = useState('');
  const [reviewNote, setReviewNote] = useState('');

  const canManage =
    hasAnyPermission(['settings.update']) ||
    hasAnyRole(['SuperAdmin', 'TenantAdmin']);

  const loadDashboard = useCallback(async () => {
    setLoading(true);
    try {
      const data = await hrIdentityReconciliationService.getDashboard();
      setDashboard(data);
    } catch (error) {
      toast({
        title: 'Reconciliation unavailable',
        description: errorMessage(
          error,
          'The HR/Identity reconciliation workspace could not be loaded.'
        ),
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    void loadDashboard();
  }, [loadDashboard]);

  const openAction = async (nextDialog: Exclude<ActionDialog, null>) => {
    setDialog(nextDialog);
    setReviewNote('');
    setReplacementUserId(
      nextDialog.kind === 'issue'
        ? (nextDialog.item.suggestedReplacementUserId ?? '')
        : ''
    );
    if (nextDialog.kind === 'issue' && replacementUsers.length === 0) {
      try {
        setReplacementUsers(
          await hrIdentityReconciliationService.getReplacementUsers()
        );
      } catch (error) {
        setDialog(null);
        toast({
          title: 'Replacement users unavailable',
          description: errorMessage(
            error,
            'Eligible HR-linked users could not be loaded.'
          ),
          variant: 'destructive',
        });
      }
    }
  };

  const runNow = async () => {
    setWorking(true);
    try {
      await hrIdentityReconciliationService.run();
      toast({
        title: 'Reconciliation completed',
        description: 'HR employment and approval ownership were reconciled.',
      });
      await loadDashboard();
    } catch (error) {
      toast({
        title: 'Reconciliation failed',
        description: errorMessage(error, 'The reconciliation run failed.'),
        variant: 'destructive',
      });
    } finally {
      setWorking(false);
    }
  };

  const retryRun = async (run: HrIdentityReconciliationRun) => {
    setWorking(true);
    try {
      await hrIdentityReconciliationService.retry(run.id);
      toast({
        title: 'Retry completed',
        description:
          'Failed reconciliation items were processed in a new traceable run.',
      });
      await loadDashboard();
    } catch (error) {
      toast({
        title: 'Retry failed',
        description: errorMessage(
          error,
          'The failed items could not be retried.'
        ),
        variant: 'destructive',
      });
    } finally {
      setWorking(false);
    }
  };

  const submitAction = async () => {
    if (!dialog || reviewNote.trim().length < 10) return;
    if (dialog.kind === 'issue' && !replacementUserId) return;
    setWorking(true);
    try {
      if (dialog.kind === 'issue') {
        await hrIdentityReconciliationService.resolveIssue(
          dialog.item.id,
          replacementUserId,
          reviewNote.trim()
        );
        toast({
          title: 'Approval reassigned',
          description:
            'The pending approval was reassigned with preserved workflow history.',
        });
      } else {
        await hrIdentityReconciliationService.reactivate(
          dialog.item.userId,
          reviewNote.trim()
        );
        toast({
          title: 'Identity reactivated',
          description:
            'Only access suspended by reconciliation was restored; procurement responsibilities remain inactive.',
        });
      }
      setDialog(null);
      await loadDashboard();
    } catch (error) {
      toast({
        title: 'Review could not be completed',
        description: errorMessage(error, 'The controlled action failed.'),
        variant: 'destructive',
      });
    } finally {
      setWorking(false);
    }
  };

  const openIssues = useMemo(
    () => dashboard?.issues.filter((item) => item.status === 'Open') ?? [],
    [dashboard?.issues]
  );

  if (loading && !dashboard) {
    return (
      <div className="flex min-h-48 items-center justify-center text-sm text-muted-foreground">
        Loading HR/Identity controls…
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            HR & Identity Reconciliation
          </h1>
          <p className="text-sm text-muted-foreground">
            Employment status and hierarchy from HR; roles and privileges remain
            Identity-owned.
          </p>
        </div>
        {canManage && (
          <Button onClick={runNow} disabled={working}>
            <RefreshCw
              className={`mr-2 h-4 w-4 ${working ? 'animate-spin' : ''}`}
            />
            Reconcile now
          </Button>
        )}
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {[
          {
            label: 'Linked users',
            value: dashboard?.linkedUsers ?? 0,
            Icon: UserRoundCheck,
          },
          {
            label: 'HR ineligible',
            value: dashboard?.hrIneligibleUsers ?? 0,
            Icon: AlertTriangle,
          },
          {
            label: 'Access suspended',
            value: dashboard?.suspendedUsers ?? 0,
            Icon: ShieldCheck,
          },
          {
            label: 'Reactivation reviews',
            value: dashboard?.reactivationReviews ?? 0,
            Icon: RefreshCw,
          },
          {
            label: 'Open workflow issues',
            value: dashboard?.openWorkflowIssues ?? 0,
            Icon: AlertTriangle,
          },
        ].map(({ label, value, Icon }) => (
          <Card key={label} className="rounded-xl">
            <CardContent className="flex items-center justify-between p-4">
              <div>
                <p className="text-xs text-muted-foreground">{label}</p>
                <p className="text-2xl font-semibold">{value}</p>
              </div>
              <Icon className="h-5 w-5 text-blue-600" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card className="rounded-xl">
        <CardHeader className="p-4 pb-2">
          <CardTitle className="text-base">Linked identity status</CardTitle>
          <CardDescription>
            Role badges are reconciled evidence only; this process never grants
            or removes roles.
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>User / employee</TableHead>
                <TableHead>Department</TableHead>
                <TableHead>Manager</TableHead>
                <TableHead>HR status</TableHead>
                <TableHead>Identity</TableHead>
                <TableHead>Roles</TableHead>
                <TableHead>Last checked</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(dashboard?.states ?? []).map((item) => (
                <TableRow key={item.userId}>
                  <TableCell>
                    <div className="font-medium">{item.userDisplayName}</div>
                    <div className="text-xs text-muted-foreground">
                      {item.employeeNumber} · {item.userName ?? 'No username'}
                    </div>
                  </TableCell>
                  <TableCell>{item.departmentName ?? 'Unassigned'}</TableCell>
                  <TableCell>{item.managerName ?? 'Unassigned'}</TableCell>
                  <TableCell>
                    <Badge variant={statusVariant(item.hrAccessEligible)}>
                      {item.staffStatus}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Badge variant={statusVariant(item.userIsActive)}>
                      {item.userIsActive ? 'Active' : 'Inactive'}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <div className="flex max-w-64 flex-wrap gap-1">
                      {item.roles.length ? (
                        item.roles.map((role) => (
                          <Badge key={role} variant="outline">
                            {role}
                          </Badge>
                        ))
                      ) : (
                        <span className="text-muted-foreground">No roles</span>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>{formatDate(item.lastObservedAtUtc)}</TableCell>
                  <TableCell>
                    {canManage && item.reactivationReviewRequired && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() =>
                          void openAction({ kind: 'reactivation', item })
                        }
                      >
                        Review reactivation
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {(dashboard?.states.length ?? 0) === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={8}
                    className="h-24 text-center text-muted-foreground"
                  >
                    Run reconciliation to establish the first linked-user
                    baseline.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card className="rounded-xl">
        <CardHeader className="p-4 pb-2">
          <CardTitle className="text-base">
            Approval ownership reviews
          </CardTitle>
          <CardDescription>
            Only unresolved hierarchy or segregation-of-duties cases appear
            here.
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Issue</TableHead>
                <TableHead>Current assignee</TableHead>
                <TableHead>Suggested</TableHead>
                <TableHead>Detected</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {openIssues.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>
                    <div className="font-medium">{item.userDisplayName}</div>
                    <div className="text-xs text-muted-foreground">
                      {item.employeeNumber}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="font-medium">{item.issueType}</div>
                    <div className="max-w-xl text-xs text-muted-foreground">
                      {item.reason}
                    </div>
                  </TableCell>
                  <TableCell>
                    {item.staleAssigneeName ?? 'Unassigned'}
                  </TableCell>
                  <TableCell>
                    {item.suggestedReplacementName ?? 'Requires selection'}
                  </TableCell>
                  <TableCell>{formatDate(item.detectedAtUtc)}</TableCell>
                  <TableCell>
                    {canManage && (
                      <Button
                        size="sm"
                        onClick={() => void openAction({ kind: 'issue', item })}
                      >
                        Resolve
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {openIssues.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="h-20 text-center text-muted-foreground"
                  >
                    <CheckCircle2 className="mr-2 inline h-4 w-4 text-emerald-600" />
                    No unresolved approval ownership issues.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card className="rounded-xl">
        <CardHeader className="p-4 pb-2">
          <CardTitle className="text-base">
            Recent reconciliation runs
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Started</TableHead>
                <TableHead>Trigger</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Candidates</TableHead>
                <TableHead>Reviews</TableHead>
                <TableHead>Failures</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(dashboard?.recentRuns ?? []).map((run) => (
                <TableRow key={run.id}>
                  <TableCell>{formatDate(run.startedAtUtc)}</TableCell>
                  <TableCell>{run.trigger}</TableCell>
                  <TableCell>
                    <Badge
                      variant={run.failedCount ? 'destructive' : 'outline'}
                    >
                      {run.status}
                    </Badge>
                  </TableCell>
                  <TableCell>{run.candidateCount}</TableCell>
                  <TableCell>{run.reviewRequiredCount}</TableCell>
                  <TableCell>{run.failedCount}</TableCell>
                  <TableCell>
                    {canManage && run.failedCount > 0 && (
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={working}
                        onClick={() => void retryRun(run)}
                      >
                        Retry failed
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog
        open={dialog !== null}
        onOpenChange={(open) => {
          if (!open && !working) setDialog(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {dialog?.kind === 'issue'
                ? 'Resolve approval ownership'
                : 'Approve identity reactivation'}
            </DialogTitle>
            <DialogDescription>
              {dialog?.kind === 'issue'
                ? 'Choose an eligible active HR-linked user. The server revalidates tenant, role and segregation-of-duties rules.'
                : 'Access is restored only after HR eligibility is revalidated. Suspended procurement responsibilities are not automatically restored.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            {dialog?.kind === 'issue' && (
              <div className="space-y-2">
                <Label>Replacement approver</Label>
                <Select
                  value={replacementUserId}
                  onValueChange={setReplacementUserId}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select an eligible user" />
                  </SelectTrigger>
                  <SelectContent>
                    {replacementUsers.map((option) => (
                      <SelectItem key={option.userId} value={option.userId}>
                        {option.displayName} ·{' '}
                        {option.employeeNumber ?? option.userName}
                        {option.departmentName
                          ? ` · ${option.departmentName}`
                          : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="hr-identity-review-note">Review note</Label>
              <Textarea
                id="hr-identity-review-note"
                value={reviewNote}
                onChange={(event) => setReviewNote(event.target.value)}
                maxLength={2000}
                placeholder="Record the verification and reason (minimum 10 characters)"
              />
              <p className="text-xs text-muted-foreground">
                {reviewNote.trim().length}/2000 characters
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setDialog(null)}
              disabled={working}
            >
              Cancel
            </Button>
            <Button
              onClick={() => void submitAction()}
              disabled={
                working ||
                reviewNote.trim().length < 10 ||
                (dialog?.kind === 'issue' && !replacementUserId)
              }
            >
              {working ? 'Saving…' : 'Confirm reviewed action'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
