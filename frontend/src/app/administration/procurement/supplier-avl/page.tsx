'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  ListTree,
  PauseCircle,
  Plus,
  RefreshCw,
  Search,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { procurementSupplierAvlService as service } from '@/services/procurement-supplier-avl.service';
import type {
  SupplierAvlEntry,
  SupplierAvlRegisterStatus,
} from '@/types/procurement-supplier-avl';

const registerStatuses: SupplierAvlRegisterStatus[] = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Published',
  'Rejected',
  'Retired',
];

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString() : '—';
const shortHash = (value?: string) =>
  value ? `${value.slice(0, 12)}…${value.slice(-8)}` : '—';
const toLocalInput = (value: Date) => {
  const offset = value.getTimezoneOffset() * 60_000;
  return new Date(value.getTime() - offset).toISOString().slice(0, 16);
};

export default function SupplierAvlPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canRead =
    hasPermission('procurement.supplier.review') ||
    hasPermission('procurement.supplier.manage') ||
    hasPermission('procurement.supplier.approve');
  const canManage = hasPermission('procurement.supplier.manage');
  const canApprove = hasPermission('procurement.supplier.approve');

  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<SupplierAvlRegisterStatus | 'all'>('all');
  const [selectedId, setSelectedId] = useState<string>();
  const [createOpen, setCreateOpen] = useState(false);
  const [entryOpen, setEntryOpen] = useState(false);
  const [lifecycleAction, setLifecycleAction] = useState<
    'submit' | 'approve' | 'reject' | 'publish'
  >();
  const [entryAction, setEntryAction] = useState<{
    action: 'suspend' | 'reinstate';
    entry: SupplierAvlEntry;
  }>();
  const [reviewYear, setReviewYear] = useState(new Date().getUTCFullYear());
  const [effectiveFrom, setEffectiveFrom] = useState(
    toLocalInput(new Date(Date.UTC(new Date().getUTCFullYear(), 0, 1)))
  );
  const [workflowDefinitionId, setWorkflowDefinitionId] = useState('');
  const [notes, setNotes] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [comment, setComment] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');

  const summary = useQuery({
    queryKey: ['supplier-avl-summary'],
    queryFn: service.summary,
    enabled: canRead,
  });
  const history = useQuery({
    queryKey: ['supplier-avl-history', search, status],
    queryFn: () =>
      service.search({
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        page: 1,
        pageSize: 100,
      }),
    enabled: canRead,
  });
  const detail = useQuery({
    queryKey: ['supplier-avl-detail', selectedId],
    queryFn: () => service.get(selectedId ?? ''),
    enabled: canRead && Boolean(selectedId),
  });
  const suppliers = useQuery({
    queryKey: ['supplier-avl-suppliers'],
    queryFn: service.supplierOptions,
    enabled: canRead,
  });
  const workflows = useQuery({
    queryKey: ['supplier-avl-workflows'],
    queryFn: service.workflowOptions,
    enabled: canRead,
  });

  const refresh = async (id?: string) => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['supplier-avl-summary'] }),
      queryClient.invalidateQueries({ queryKey: ['supplier-avl-history'] }),
      id
        ? queryClient.invalidateQueries({
            queryKey: ['supplier-avl-detail', id],
          })
        : Promise.resolve(),
    ]);
  };

  const createMutation = useMutation({
    mutationFn: service.create,
    onSuccess: async (value) => {
      setCreateOpen(false);
      setSelectedId(value.id);
      await refresh(value.id);
      toast.success('AVL annual review created');
    },
    onError: (error: Error) => toast.error(error.message),
  });
  const addEntryMutation = useMutation({
    mutationFn: () =>
      service.addEntry(detail.data?.id ?? '', {
        businessPartnerId: supplierId,
        registerRowVersion: detail.data?.rowVersion ?? '',
      }),
    onSuccess: async (value) => {
      setEntryOpen(false);
      setSupplierId('');
      await refresh(value.id);
      toast.success('Eligible supplier added to the AVL review');
    },
    onError: (error: Error) => toast.error(error.message),
  });
  const removeEntryMutation = useMutation({
    mutationFn: (entryId: string) =>
      service.removeEntry(
        detail.data?.id ?? '',
        entryId,
        detail.data?.rowVersion ?? ''
      ),
    onSuccess: async (value) => {
      await refresh(value.id);
      toast.success('Draft entry removed');
    },
    onError: (error: Error) => toast.error(error.message),
  });

  const evidence = useMemo(
    () =>
      evidenceReference.trim()
        ? [
            {
              referenceKind: 'ExternalReference' as const,
              reference: evidenceReference.trim(),
              label: 'AVL lifecycle evidence',
              requirementKey: 'AVL-LIFECYCLE',
            },
          ]
        : [],
    [evidenceReference]
  );

  const lifecycleMutation = useMutation({
    mutationFn: async () => {
      if (!detail.data || !lifecycleAction) throw new Error('Select an AVL register');
      const request = {
        rowVersion: detail.data.rowVersion,
        comment,
        evidence,
      };
      return service[lifecycleAction](detail.data.id, request);
    },
    onSuccess: async (value) => {
      setLifecycleAction(undefined);
      setComment('');
      setEvidenceReference('');
      await refresh(value.id);
      toast.success('AVL lifecycle action completed');
    },
    onError: (error: Error) => toast.error(error.message),
  });

  const entryLifecycleMutation = useMutation({
    mutationFn: async () => {
      if (!detail.data || !entryAction) throw new Error('Select an AVL entry');
      const request = {
        rowVersion: entryAction.entry.rowVersion,
        reason: comment,
        evidence,
      };
      return entryAction.action === 'suspend'
        ? service.suspendEntry(detail.data.id, entryAction.entry.id, request)
        : service.reinstateEntry(detail.data.id, entryAction.entry.id, request);
    },
    onSuccess: async (value) => {
      setEntryAction(undefined);
      setComment('');
      setEvidenceReference('');
      await refresh(value.id);
      toast.success('AVL supplier status updated');
    },
    onError: (error: Error) => toast.error(error.message),
  });

  const expiryMutation = useMutation({
    mutationFn: service.processExpiry,
    onSuccess: async ({ processed }) => {
      await refresh(selectedId);
      toast.success(`${processed} AVL publication(s) retired`);
    },
    onError: (error: Error) => toast.error(error.message),
  });

  if (!canRead) {
    return (
      <div className="p-6">
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Access restricted</AlertTitle>
          <AlertDescription>
            A supplier review, management, approval, or Internal Audit role is required.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const selected = detail.data;

  return (
    <div className="space-y-6 p-6" data-testid="supplier-avl-page">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Approved Vendor List</h1>
          <p className="text-sm text-muted-foreground">
            Annual review, approval, effective publication, suspension,
            reinstatement, expiry, and immutable version snapshots.
          </p>
        </div>
        <div className="flex gap-2">
          {canManage && (
            <Button
              variant="outline"
              onClick={() => expiryMutation.mutate()}
              disabled={expiryMutation.isPending}
            >
              <RefreshCw className="mr-2 h-4 w-4" />
              Process expiry
            </Button>
          )}
          {canManage && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              New annual review
            </Button>
          )}
        </div>
      </div>

      {summary.data && !summary.data.policyAvailable && (
        <Alert variant="destructive" data-testid="supplier-avl-policy-gate">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>DEC-011 release configuration required</AlertTitle>
          <AlertDescription>{summary.data.policyReleaseGate}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
        {[
          ['Registers', summary.data?.totalRegisters ?? 0, ListTree],
          ['Draft', summary.data?.draftCount ?? 0, CalendarClock],
          ['Pending', summary.data?.pendingApprovalCount ?? 0, CalendarClock],
          ['Published', summary.data?.publishedCount ?? 0, CheckCircle2],
          ['Active suppliers', summary.data?.currentEntryCount ?? 0, CheckCircle2],
          ['Suspended', summary.data?.suspendedEntryCount ?? 0, PauseCircle],
        ].map(([label, value, Icon]) => (
          <Card key={String(label)}>
            <CardContent className="flex items-center justify-between p-4">
              <div>
                <div className="text-xs text-muted-foreground">{String(label)}</div>
                <div className="text-2xl font-semibold">{String(value)}</div>
              </div>
              <Icon className="h-4 w-4 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">AVL publication history</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <div className="relative min-w-[260px] flex-1">
              <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                placeholder="Register, supplier code, or supplier name"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
            <Select
              value={status}
              onValueChange={(value) =>
                setStatus(value as SupplierAvlRegisterStatus | 'all')
              }
            >
              <SelectTrigger className="w-[210px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {registerStatuses.map((value) => (
                  <SelectItem key={value} value={value}>
                    {value}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Register</TableHead>
                  <TableHead>Year</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Expiry</TableHead>
                  <TableHead>Entries</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(history.data?.items ?? []).map((item) => (
                  <TableRow
                    key={item.id}
                    className="cursor-pointer"
                    onClick={() => setSelectedId(item.id)}
                  >
                    <TableCell className="font-medium">
                      {item.registerCode} / v{item.version}
                    </TableCell>
                    <TableCell>{item.reviewYear}</TableCell>
                    <TableCell>
                      <Badge variant={item.status === 'Published' ? 'default' : 'outline'}>
                        {item.status}
                      </Badge>
                    </TableCell>
                    <TableCell>{formatDate(item.effectiveFromUtc)}</TableCell>
                    <TableCell>{formatDate(item.expiresAtUtc)}</TableCell>
                    <TableCell>
                      {item.activeEntryCount} active · {item.suspendedEntryCount} suspended
                    </TableCell>
                  </TableRow>
                ))}
                {!history.isLoading && !history.data?.items.length && (
                  <TableRow>
                    <TableCell colSpan={6} className="h-24 text-center text-muted-foreground">
                      No AVL annual reviews match this filter.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {selected && (
        <Card data-testid="supplier-avl-detail">
          <CardHeader className="flex flex-row items-start justify-between">
            <div>
              <CardTitle className="text-base">
                {selected.registerCode} / v{selected.version}
              </CardTitle>
              <p className="text-xs text-muted-foreground">
                DEC-011 {shortHash(selected.policyValueHash)} · integrity{' '}
                {shortHash(selected.integrityHash)} · snapshots{' '}
                {selected.publicationSnapshots.length}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              {canManage && selected.allowedActions.includes('AddEntry') && (
                <Button size="sm" variant="outline" onClick={() => setEntryOpen(true)}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add eligible supplier
                </Button>
              )}
              {canManage && selected.allowedActions.includes('Submit') && (
                <Button size="sm" onClick={() => setLifecycleAction('submit')}>
                  Submit
                </Button>
              )}
              {canApprove && selected.allowedActions.includes('Approve') && (
                <Button size="sm" onClick={() => setLifecycleAction('approve')}>
                  Approve
                </Button>
              )}
              {canApprove && selected.allowedActions.includes('Reject') && (
                <Button
                  size="sm"
                  variant="destructive"
                  onClick={() => setLifecycleAction('reject')}
                >
                  Reject
                </Button>
              )}
              {canApprove && selected.allowedActions.includes('Publish') && (
                <Button size="sm" onClick={() => setLifecycleAction('publish')}>
                  Publish
                </Button>
              )}
            </div>
          </CardHeader>
          <CardContent>
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Supplier</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Due diligence</TableHead>
                    <TableHead>Eligibility hash</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {selected.entries.map((entry) => (
                    <TableRow key={entry.id}>
                      <TableCell>
                        <div className="font-medium">{entry.partnerName}</div>
                        <div className="text-xs text-muted-foreground">
                          {entry.partnerCode}
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant={
                            entry.status === 'Active'
                              ? 'default'
                              : entry.status === 'Suspended'
                                ? 'destructive'
                                : 'outline'
                          }
                        >
                          {entry.status}
                        </Badge>
                      </TableCell>
                      <TableCell>{entry.dueDiligenceReviewReference ?? 'Bound'}</TableCell>
                      <TableCell className="font-mono text-xs">
                        {shortHash(entry.eligibilityDecisionHash)}
                      </TableCell>
                      <TableCell className="text-right">
                        {selected.status === 'Draft' && canManage && (
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => removeEntryMutation.mutate(entry.id)}
                          >
                            Remove
                          </Button>
                        )}
                        {selected.status === 'Published' &&
                          canApprove &&
                          entry.status === 'Active' && (
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() =>
                                setEntryAction({ action: 'suspend', entry })
                              }
                            >
                              Suspend
                            </Button>
                          )}
                        {selected.status === 'Published' &&
                          canApprove &&
                          entry.status === 'Suspended' && (
                            <Button
                              size="sm"
                              onClick={() =>
                                setEntryAction({ action: 'reinstate', entry })
                              }
                            >
                              Reinstate
                            </Button>
                          )}
                      </TableCell>
                    </TableRow>
                  ))}
                  {!selected.entries.length && (
                    <TableRow>
                      <TableCell colSpan={5} className="h-20 text-center text-muted-foreground">
                        Add suppliers only after current registration, evidence, due
                        diligence, and pre-AVL eligibility checks pass.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      )}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New AVL annual review</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Review year</Label>
              <Input
                type="number"
                value={reviewYear}
                onChange={(event) => setReviewYear(Number(event.target.value))}
              />
            </div>
            <div>
              <Label>Effective from</Label>
              <Input
                type="datetime-local"
                value={effectiveFrom}
                onChange={(event) => setEffectiveFrom(event.target.value)}
              />
            </div>
            <div>
              <Label>Approval workflow</Label>
              <Select
                value={workflowDefinitionId}
                onValueChange={setWorkflowDefinitionId}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select Published workflow" />
                </SelectTrigger>
                <SelectContent>
                  {(workflows.data ?? []).map((option) => (
                    <SelectItem key={option.id} value={option.id}>
                      {option.name} / v{option.version}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Notes</Label>
              <Textarea value={notes} onChange={(event) => setNotes(event.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={
                createMutation.isPending ||
                !effectiveFrom ||
                !workflowDefinitionId ||
                reviewYear < 2000
              }
              onClick={() =>
                createMutation.mutate({
                  reviewYear,
                  effectiveFromUtc: new Date(effectiveFrom).toISOString(),
                  workflowDefinitionId,
                  notes: notes || undefined,
                })
              }
            >
              Create review
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={entryOpen} onOpenChange={setEntryOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add eligible supplier</DialogTitle>
          </DialogHeader>
          <div>
            <Label>Supplier</Label>
            <Select value={supplierId} onValueChange={setSupplierId}>
              <SelectTrigger>
                <SelectValue placeholder="Select supplier" />
              </SelectTrigger>
              <SelectContent>
                {(suppliers.data ?? []).map((supplier) => (
                  <SelectItem key={supplier.id} value={supplier.id}>
                    {supplier.code} · {supplier.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="mt-2 text-xs text-muted-foreground">
              The server revalidates registration, evidence pack, status, blacklist,
              due diligence, prequalification, and the exact DEC-011 policy.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEntryOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={!supplierId || addEntryMutation.isPending}
              onClick={() => addEntryMutation.mutate()}
            >
              Add supplier
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycleAction || entryAction)}
        onOpenChange={(open) => {
          if (!open) {
            setLifecycleAction(undefined);
            setEntryAction(undefined);
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {lifecycleAction
                ? `${lifecycleAction[0].toUpperCase()}${lifecycleAction.slice(1)} AVL register`
                : `${entryAction?.action === 'suspend' ? 'Suspend' : 'Reinstate'} supplier`}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <Label>Reason / comment</Label>
              <Textarea value={comment} onChange={(event) => setComment(event.target.value)} />
            </div>
            <div>
              <Label>Evidence reference</Label>
              <Input
                value={evidenceReference}
                onChange={(event) => setEvidenceReference(event.target.value)}
                placeholder="Document, minute, workflow, or approval reference"
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setLifecycleAction(undefined);
                setEntryAction(undefined);
              }}
            >
              Cancel
            </Button>
            <Button
              disabled={
                !comment.trim() ||
                !evidenceReference.trim() ||
                lifecycleMutation.isPending ||
                entryLifecycleMutation.isPending
              }
              onClick={() =>
                entryAction
                  ? entryLifecycleMutation.mutate()
                  : lifecycleMutation.mutate()
              }
            >
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
