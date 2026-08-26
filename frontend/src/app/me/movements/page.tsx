'use client';

/**
 * Area 25 slice 7 — My Movements: the portal's career hub (spec destinations #18/#19,
 * re-homed from /hr/movements/mine).
 *
 * Everything personal rides the token-derived employee-portal routes; the respond act uses
 * the portal arm, whose 409 guard only opens while the movement is genuinely awaiting THIS
 * employee's answer (the state the slice-7 adapter fix made reachable — nothing set
 * EmployeeAcceptancePending after area 8 moved approvals onto the engine). Checklist tasks
 * still ride staff-movements/checklist/mine — tasks can be owed on anyone's movement.
 *
 * The "waiting for my approval" section is MANAGER work and deliberately links to the desk
 * movement detail, where the workflow approval actions live.
 */

import { useState } from 'react';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  CheckCircle2,
  XCircle,
  Route,
  UserCog,
  ArrowLeftRight,
  Briefcase,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { mePortalService } from '@/services/hr/me-portal.service';
import { movementService } from '@/services/hr/movement.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyMovementsPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [responding, setResponding] = useState<{ id: string; accepted: boolean } | null>(null);
  const [comments, setComments] = useState('');

  const { data: dash, isLoading } = useQuery({
    queryKey: ['me', 'movements', 'dashboard'],
    queryFn: () => mePortalService.getMovementsDashboard(),
  });

  const { data: tasks = [] } = useQuery({
    queryKey: ['me', 'movements', 'my-checklist'],
    queryFn: () => movementService.getMyChecklistItems(),
  });

  // Movements this caller is an approver for. Approving a report's transfer is an ordinary
  // line-manager job, and the HR register is closed to them, so this is where that work lives.
  const { data: toApprove = [] } = useQuery({
    queryKey: ['me', 'movements', 'awaiting-my-approval'],
    queryFn: () => movementService.getAwaitingMyApproval(),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['me', 'movements'] });

  const respond = useMutation({
    mutationFn: () => {
      if (!responding) throw new Error('No movement selected.');
      return mePortalService.respondToMovement(responding.id, responding.accepted, comments || undefined);
    },
    onSuccess: () => {
      toast({ title: responding?.accepted ? 'Movement accepted' : 'Movement declined' });
      setResponding(null);
      setComments('');
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  const completeTask = useMutation({
    mutationFn: (itemId: string) => movementService.completeChecklistItem(itemId),
    onSuccess: () => {
      toast({ title: 'Task completed' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Refused', description: error?.message, variant: 'destructive' }),
  });

  const awaitingMe = dash?.pendingResponseMovements ?? [];
  const movements = dash?.recentMovements ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Movements"
        description={
          dash?.currentPositionTitle
            ? `${dash.currentPositionTitle}${dash.currentOrganizationUnitName ? ` · ${dash.currentOrganizationUnitName}` : ''}${dash.currentRoleStartDate ? ` · since ${fmtDate(dash.currentRoleStartDate)}` : ''}`
            : 'Changes to your position, unit or reporting line, and any tasks you owe on a movement.'
        }
        backHref="/me"
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" asChild>
              <Link href="/me/movements/career-path">
                <Route className="mr-2 h-4 w-4" /> Career timeline
              </Link>
            </Button>
            <Button variant="outline" size="sm" asChild>
              <Link href="/me/movements/acting">
                <UserCog className="mr-2 h-4 w-4" /> Acting
              </Link>
            </Button>
            <Button variant="outline" size="sm" asChild>
              <Link href="/me/movements/secondments">
                <ArrowLeftRight className="mr-2 h-4 w-4" /> Secondments
              </Link>
            </Button>
          </div>
        }
      />

      {dash && (
        <MetricTiles
          tiles={[
            { label: 'Movements', value: dash.totalMovements, icon: Briefcase },
            { label: 'Promotions', value: dash.totalPromotions },
            {
              label: 'Awaiting your response',
              value: dash.pendingResponseCount,
              tone: dash.pendingResponseCount > 0 ? ('warning' as const) : undefined,
            },
            {
              label: 'Active temporary roles',
              value: dash.activeActingAppointmentCount + dash.activeSecondmentCount,
              hint:
                dash.activeActingAppointmentCount + dash.activeSecondmentCount > 0
                  ? 'See Acting / Secondments'
                  : undefined,
            },
          ]}
        />
      )}

      {awaitingMe.length > 0 && (
        <Card className="border-primary/40">
          <CardHeader>
            <CardTitle className="text-base">Awaiting your response</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {awaitingMe.map((m) => (
              <div
                key={m.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3"
              >
                <div className="text-sm">
                  <Link href={`/me/movements/${m.id}`} className="font-medium hover:underline">
                    {m.movementNumber}
                  </Link>{' '}
                  — {m.movementTypeName} to {m.newPositionTitle} in {m.newOrganizationUnitName},
                  effective {fmtDate(m.effectiveDate)}
                </div>
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => setResponding({ id: m.id, accepted: true })}>
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Accept
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => setResponding({ id: m.id, accepted: false })}
                  >
                    <XCircle className="mr-2 h-4 w-4" />
                    Decline
                  </Button>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {toApprove.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Waiting for my approval</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Effective</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {toApprove.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/movements/${m.id}`} className="hover:underline">
                        {m.movementNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{m.employeeName}</TableCell>
                    <TableCell>{m.movementTypeName}</TableCell>
                    <TableCell>{fmtDate(m.effectiveDate)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
          <CardContent className="pt-0 text-xs text-muted-foreground">
            Approve or refuse from the movement itself — the approval actions there come from the
            workflow, which knows whether the step is yours.
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">My movement history</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : movements.length === 0 ? (
            <EmptyState
              title="No movements"
              description="Nothing has been raised about your position."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {movements.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell className="font-medium">
                      <Link href={`/me/movements/${m.id}`} className="hover:underline">
                        {m.movementNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{m.movementTypeName}</TableCell>
                    <TableCell>
                      <div>{m.newPositionTitle}</div>
                      <div className="text-xs text-muted-foreground">
                        {m.newOrganizationUnitName}
                      </div>
                    </TableCell>
                    <TableCell>{fmtDate(m.effectiveDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={m.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Tasks assigned to me</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {tasks.length === 0 ? (
            <EmptyState title="Nothing outstanding" description="No movement tasks are assigned to you." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Task</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {tasks.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell>
                      {t.taskDescription}
                      {t.isRequired && (
                        <Badge variant="outline" className="ml-2">
                          Required
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell>{t.category}</TableCell>
                    <TableCell>{fmtDate(t.dueDate)}</TableCell>
                    <TableCell className="text-right">
                      <Button size="sm" variant="outline" onClick={() => completeTask.mutate(t.id)}>
                        Mark complete
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={responding !== null} onOpenChange={(open) => !open && setResponding(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {responding?.accepted ? 'Accept this movement' : 'Decline this movement'}
            </DialogTitle>
            <DialogDescription>
              This is your own response and it is recorded against your name. Nobody else, HR
              included, can give it for you.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="comments">Comments</Label>
            <Textarea
              id="comments"
              rows={3}
              value={comments}
              onChange={(e) => setComments(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setResponding(null)}>
              Cancel
            </Button>
            <Button onClick={() => respond.mutate()} disabled={respond.isPending}>
              {respond.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {responding?.accepted ? 'Accept' : 'Decline'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
