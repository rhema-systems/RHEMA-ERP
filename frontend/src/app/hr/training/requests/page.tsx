'use client';

import { useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Plus, Search, MoreHorizontal, Eye, Send, Trash2, ClipboardList, CheckCircle2, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { trainingRequestService } from '@/services/hr/training-request.service';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { TRAINING_REQUEST_STATUS_OPTIONS } from '@/types/hr/training-delivery';
import type { TrainingRequestSummary } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  TRAINING_REQUEST_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

export default function TrainingRequestsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [view, setView] = useState(searchParams?.get('view') === 'pending' ? 'pending' : 'all');
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<TrainingRequestSummary | null>(null);
  const [approveTarget, setApproveTarget] = useState<TrainingRequestSummary | null>(null);
  const [rejectTarget, setRejectTarget] = useState<TrainingRequestSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'requests', view],
    queryFn: () =>
      view === 'pending' ? trainingRequestService.getPendingApproval() : trainingRequestService.getAll(),
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const programOptions = (programs ?? []).map((p) => ({
    value: p.id,
    label: `${p.programCode} — ${p.programName}`,
  }));

  const approveForm = useForm<{ linkedProgramId: string }>({ defaultValues: { linkedProgramId: '' } });
  const rejectForm = useForm<{ rejectionReason: string }>({ defaultValues: { rejectionReason: '' } });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'requests'] });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (r) =>
        r.requestNumber.toLowerCase().includes(term) ||
        r.requestedTrainingTitle.toLowerCase().includes(term) ||
        r.employeeName.toLowerCase().includes(term),
    );
  }, [data, search]);

  const act = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await invalidate();
      toast({ title: label });
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training Requests"
        description="Ad-hoc asks for training. Approving one can link it to a catalog programme so it becomes schedulable."
        backHref="/hr/training"
        actions={
          <Button onClick={() => router.push('/hr/training/requests/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Request
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <Tabs value={view} onValueChange={setView}>
              <TabsList>
                <TabsTrigger value="all">All requests</TabsTrigger>
                <TabsTrigger value="pending">Awaiting approval</TabsTrigger>
              </TabsList>
            </Tabs>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search requests…"
                className="pl-8"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Request</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Training wanted</TableHead>
                  <TableHead>Linked programme</TableHead>
                  <TableHead>Raised</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={ClipboardList}
                        title={view === 'pending' ? 'Nothing awaiting approval' : 'No training requests'}
                        description={
                          view === 'pending'
                            ? 'Submitted requests appear here for a decision.'
                            : 'Raise a request for training that is not in the catalog yet.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((r) => (
                    <TableRow
                      key={r.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/training/requests/${r.id}`)}
                    >
                      <TableCell className="font-mono text-xs">{r.requestNumber}</TableCell>
                      <TableCell className="font-medium">{r.employeeName}</TableCell>
                      <TableCell>{r.requestedTrainingTitle}</TableCell>
                      <TableCell className="text-muted-foreground">{r.linkedProgramName || '—'}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(r.requestDate).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={statusLabel(r.status)} />
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/hr/training/requests/${r.id}`);
                              }}
                            >
                              <Eye className="mr-2 h-4 w-4" /> View details
                            </DropdownMenuItem>
                            {r.status === 'Draft' && (
                              <DropdownMenuItem
                                onClick={(e) => {
                                  e.stopPropagation();
                                  act('Submitted', () => trainingRequestService.submit(r.id));
                                }}
                              >
                                <Send className="mr-2 h-4 w-4" /> Submit
                              </DropdownMenuItem>
                            )}
                            {r.status === 'Submitted' && (
                              <>
                                <DropdownMenuItem
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    approveForm.reset({ linkedProgramId: '' });
                                    setApproveTarget(r);
                                  }}
                                >
                                  <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                                </DropdownMenuItem>
                                <DropdownMenuItem
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    rejectForm.reset({ rejectionReason: '' });
                                    setRejectTarget(r);
                                  }}
                                >
                                  <XCircle className="mr-2 h-4 w-4" /> Reject
                                </DropdownMenuItem>
                              </>
                            )}
                            {r.status === 'Draft' && (
                              <>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem
                                  className="text-destructive focus:text-destructive"
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setDeleteTarget(r);
                                  }}
                                >
                                  <Trash2 className="mr-2 h-4 w-4" /> Delete
                                </DropdownMenuItem>
                              </>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={approveTarget !== null} onOpenChange={(o) => !o && setApproveTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Approve request</DialogTitle>
            <DialogDescription>
              Optionally link it to a catalog programme — that is what turns a free-text ask into
              something you can schedule.
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <SelectField
              form={approveForm}
              name="linkedProgramId"
              label="Link to programme"
              options={programOptions}
              allowEmpty
              emptyLabel="Do not link"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                if (!approveTarget) return;
                const ok = await act('Approved', () =>
                  trainingRequestService.approve(approveTarget.id, {
                    linkedProgramId: approveForm.getValues('linkedProgramId') || null,
                  }),
                );
                if (ok) setApproveTarget(null);
              }}
            >
              Approve
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rejectTarget !== null} onOpenChange={(o) => !o && setRejectTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject request</DialogTitle>
            <DialogDescription>The reason is shown to whoever raised it.</DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <TextareaField form={rejectForm} name="rejectionReason" label="Reason" rows={3} required />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRejectTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={async () => {
                if (!rejectTarget) return;
                const reason = rejectForm.getValues('rejectionReason').trim();
                if (!reason) {
                  toast({ title: 'A reason is required', variant: 'destructive' });
                  return;
                }
                const ok = await act('Rejected', () =>
                  trainingRequestService.reject(rejectTarget.id, { rejectionReason: reason }),
                );
                if (ok) setRejectTarget(null);
              }}
            >
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Delete request"
        description={deleteTarget ? `Delete "${deleteTarget.requestedTrainingTitle}"?` : ''}
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!deleteTarget) return false;
          const ok = await act('Deleted', () => trainingRequestService.remove(deleteTarget.id));
          if (ok) setDeleteTarget(null);
          return ok;
        }}
      />
    </div>
  );
}
