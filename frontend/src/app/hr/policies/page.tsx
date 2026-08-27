'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  BarChart3,
  BookText,
  Loader2,
  Pencil,
  Plus,
  Send,
  Trash2,
  Upload,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
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
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { useToast } from '@/hooks/use-toast';
import {
  hrPoliciesService,
  POLICY_CATEGORIES,
  type HrPolicy,
  type HrPolicyCategory,
  type HrPolicyStatus,
  type PolicyComplianceFilter,
  type SaveHrPolicyRequest,
} from '@/services/hr/policies.service';
import { cn } from '@/lib/utils';

/**
 * The policy library — the desk that publishes it and watches who has signed
 * (area 25 slice 12d).
 *
 * The compliance drawer defaults to the OUTSTANDING slice, because that is the work: the four
 * totals always cover the whole audience, but the rows are a page — a tenant-wide policy has
 * thousands of them, and returning all of them measured 2.3 MB.
 *
 * ⚠ The audience builder here is intentionally simple (everyone, or one organisation unit).
 * The full rule builder lives on announcements; a policy addressed by a complicated rule set is
 * a policy nobody can explain the scope of, which is a poor property for something people sign.
 */

const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

const STATUS_STYLE: Record<HrPolicyStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  Published: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  Archived: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
};

const EMPTY: SaveHrPolicyRequest = {
  title: '',
  summary: '',
  category: 'General',
  versionLabel: '',
  requiresAcknowledgement: false,
  acknowledgementText: 'I confirm that I have read and understood this policy.',
  acknowledgementDueDays: 30,
  audiences: [{ targetType: 'AllEmployees', targetId: null, isExclusion: false }],
};

export default function HrPoliciesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [tab, setTab] = useState<HrPolicyStatus | 'all'>('Published');
  const [editing, setEditing] = useState<HrPolicy | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<SaveHrPolicyRequest>(EMPTY);
  const [uploading, setUploading] = useState<HrPolicy | null>(null);
  const [viewing, setViewing] = useState<HrPolicy | null>(null);
  const [filter, setFilter] = useState<PolicyComplianceFilter>('Outstanding');
  const [page, setPage] = useState(1);

  const { data: policies = [], isLoading, isError } = useQuery({
    queryKey: ['hr', 'policies', tab],
    queryFn: () => hrPoliciesService.getAll(tab === 'all' ? undefined : tab),
  });

  const viewingId = viewing?.id ?? '';
  const { data: compliance, isFetching: complianceLoading } = useQuery({
    queryKey: ['hr', 'policies', viewingId, 'compliance', filter, page],
    queryFn: () => hrPoliciesService.getCompliance(viewingId, filter, page),
    enabled: viewingId !== '',
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'policies'] });

  const open = (policy: HrPolicy | null) => {
    if (policy) {
      setEditing(policy);
      setForm({
        title: policy.title,
        summary: policy.summary ?? '',
        category: policy.category,
        versionLabel: policy.versionLabel ?? '',
        effectiveFrom: policy.effectiveFrom ?? null,
        reviewOn: policy.reviewOn ?? null,
        requiresAcknowledgement: policy.requiresAcknowledgement,
        acknowledgementText: policy.acknowledgementText ?? EMPTY.acknowledgementText,
        acknowledgementDueDays: policy.acknowledgementDueDays ?? null,
        supersedesPolicyId: policy.supersedesPolicyId ?? null,
        audiences: policy.audiences.map((a) => ({
          targetType: a.targetType,
          targetId: a.targetId ?? null,
          isExclusion: a.isExclusion,
        })),
      });
    } else {
      setCreating(true);
      setForm(EMPTY);
    }
  };

  const close = () => {
    setEditing(null);
    setCreating(false);
    setForm(EMPTY);
  };

  const save = useMutation({
    mutationFn: () =>
      editing ? hrPoliciesService.update(editing.id, form) : hrPoliciesService.create(form),
    onSuccess: () => {
      refresh();
      close();
      toast({ title: 'Saved', description: 'Attach the document, then publish when ready.' });
    },
    onError: (e: any) =>
      toast({ title: 'Not saved', description: e?.message, variant: 'destructive' }),
  });

  const publish = useMutation({
    mutationFn: (id: string) => hrPoliciesService.publish(id),
    onSuccess: (p) => {
      refresh();
      toast({
        title: 'Published',
        description: `"${p.title}" now applies to ${p.audienceCount ?? 'its audience'}.`,
      });
    },
    onError: (e: any) =>
      toast({ title: 'Not published', description: e?.message, variant: 'destructive' }),
  });

  const archive = useMutation({
    mutationFn: (id: string) => hrPoliciesService.archive(id),
    onSuccess: () => {
      refresh();
      toast({ title: 'Withdrawn', description: 'Signatures already collected are kept.' });
    },
    onError: (e: any) =>
      toast({ title: 'Not withdrawn', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => hrPoliciesService.deleteDraft(id),
    onSuccess: () => {
      refresh();
      toast({ title: 'Draft deleted' });
    },
    onError: (e: any) =>
      toast({ title: 'Not deleted', description: e?.message, variant: 'destructive' }),
  });

  const formOpen = creating || editing !== null;
  const totalPages = compliance ? Math.max(1, Math.ceil(compliance.totalRows / compliance.pageSize)) : 1;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Policy Library"
        description="The policies staff can read, and the ones they are asked to sign. A published policy is superseded rather than edited, so the signatures already collected keep their meaning."
        backHref="/hr"
        actions={
          <Button onClick={() => open(null)}>
            <Plus className="mr-1 h-4 w-4" /> New policy
          </Button>
        }
      />

      <Tabs value={tab} onValueChange={(v) => setTab(v as HrPolicyStatus | 'all')}>
        <TabsList>
          <TabsTrigger value="Published">Published</TabsTrigger>
          <TabsTrigger value="Draft">Drafts</TabsTrigger>
          <TabsTrigger value="Archived">Withdrawn</TabsTrigger>
          <TabsTrigger value="all">All</TabsTrigger>
        </TabsList>
      </Tabs>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-28" />
          <Skeleton className="h-28" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          The library could not be loaded right now. Try again in a moment.
        </p>
      ) : policies.length === 0 ? (
        <EmptyState
          icon={BookText}
          title="Nothing here"
          description={
            tab === 'Draft'
              ? 'No drafts in progress.'
              : 'No policies with this status yet.'
          }
          action={tab === 'Draft' ? <Button onClick={() => open(null)}>New policy</Button> : undefined}
        />
      ) : (
        <div className="space-y-3">
          {policies.map((p) => (
            <Card key={p.id}>
              <CardContent className="space-y-3 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{p.title}</span>
                  <Badge variant="outline">{p.categoryName}</Badge>
                  {p.versionLabel && (
                    <Badge variant="secondary" className="font-mono">{p.versionLabel}</Badge>
                  )}
                  <Badge className={cn('border-0', STATUS_STYLE[p.status])}>{p.statusName}</Badge>
                  {p.requiresAcknowledgement && (
                    <Badge variant="outline">Signature required</Badge>
                  )}
                  {!p.hasDocument && (
                    <Badge variant="destructive">No document</Badge>
                  )}
                  <span className="ml-auto font-mono text-xs text-muted-foreground">
                    {p.policyNumber}
                  </span>
                </div>

                {p.summary && <p className="text-sm text-muted-foreground">{p.summary}</p>}

                {p.supersedesTitle && (
                  <p className="text-xs text-muted-foreground">
                    Supersedes: {p.supersedesTitle}
                  </p>
                )}

                <div className="flex flex-wrap items-center gap-2">
                  {p.audiences.map((rule, i) => (
                    <Badge
                      key={rule.id ?? i}
                      variant={rule.isExclusion ? 'destructive' : 'outline'}
                      className="font-normal"
                    >
                      {rule.isExclusion ? 'except ' : ''}
                      {rule.targetName ?? rule.targetType}
                    </Badge>
                  ))}
                  {p.requiresAcknowledgement && p.status === 'Published' && (
                    <span className="text-xs text-muted-foreground">
                      {p.signedCount ?? 0} of {p.audienceCount ?? '?'} signed
                      {p.declinedCount ? ` · ${p.declinedCount} declined` : ''}
                    </span>
                  )}
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  {p.status === 'Draft' && (
                    <>
                      <Button size="sm" onClick={() => publish.mutate(p.id)} disabled={publish.isPending}>
                        {publish.isPending ? (
                          <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Send className="mr-1 h-3.5 w-3.5" />
                        )}
                        Publish
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => open(p)}>
                        <Pencil className="mr-1 h-3.5 w-3.5" /> Edit
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setUploading(p)}>
                        <Upload className="mr-1 h-3.5 w-3.5" />
                        {p.hasDocument ? 'Replace document' : 'Attach document'}
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => remove.mutate(p.id)}>
                        <Trash2 className="mr-1 h-3.5 w-3.5" /> Delete
                      </Button>
                    </>
                  )}
                  {p.status === 'Published' && (
                    <>
                      {p.requiresAcknowledgement && (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => { setViewing(p); setFilter('Outstanding'); setPage(1); }}
                        >
                          <BarChart3 className="mr-1 h-3.5 w-3.5" /> Who has signed
                        </Button>
                      )}
                      <Button variant="outline" size="sm" onClick={() => archive.mutate(p.id)}>
                        <Archive className="mr-1 h-3.5 w-3.5" /> Withdraw
                      </Button>
                    </>
                  )}
                  <span className="ml-auto text-xs text-muted-foreground">
                    {p.publishedAt ? `published ${fmtDate(p.publishedAt)}` : 'not published'}
                  </span>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* ── Compliance ───────────────────────────────────────────────── */}
      <Dialog open={!!viewing} onOpenChange={(o) => !o && setViewing(null)}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Who has signed — {viewing?.title}</DialogTitle>
            <DialogDescription>
              Worked out from who the policy applies to today, so somebody who joined since it was
              published shows as outstanding.
            </DialogDescription>
          </DialogHeader>

          {compliance && (
            <>
              <div className="grid grid-cols-4 gap-2 text-center">
                {[
                  { label: 'Applies to', value: compliance.audienceCount, tone: '' },
                  { label: 'Signed', value: compliance.signedCount, tone: 'text-emerald-600' },
                  { label: 'Declined', value: compliance.declinedCount, tone: 'text-red-600' },
                  { label: 'Outstanding', value: compliance.outstandingCount, tone: 'text-amber-600' },
                ].map((s) => (
                  <div key={s.label} className="rounded-md border p-2">
                    <div className={cn('text-xl font-bold', s.tone)}>{s.value}</div>
                    <div className="text-xs text-muted-foreground">{s.label}</div>
                  </div>
                ))}
              </div>
              <div className="text-sm text-muted-foreground">
                {compliance.compliancePercent}% acknowledged
              </div>

              <Tabs
                value={filter}
                onValueChange={(v) => { setFilter(v as PolicyComplianceFilter); setPage(1); }}
              >
                <TabsList>
                  <TabsTrigger value="Outstanding">Outstanding</TabsTrigger>
                  <TabsTrigger value="Signed">Signed</TabsTrigger>
                  <TabsTrigger value="Declined">Declined</TabsTrigger>
                  <TabsTrigger value="All">Everyone</TabsTrigger>
                </TabsList>
              </Tabs>

              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Unit</TableHead>
                      <TableHead>Outcome</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {complianceLoading ? (
                      <TableRow>
                        <TableCell colSpan={3}>
                          <Skeleton className="h-8" />
                        </TableCell>
                      </TableRow>
                    ) : compliance.rows.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={3} className="text-sm text-muted-foreground">
                          Nobody in this group.
                        </TableCell>
                      </TableRow>
                    ) : (
                      compliance.rows.map((r) => (
                        <TableRow key={r.employeeId}>
                          <TableCell>
                            <div className="font-medium">{r.employeeName}</div>
                            <div className="font-mono text-xs text-muted-foreground">
                              {r.employeeNumber}
                            </div>
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {r.organizationUnitName ?? '—'}
                          </TableCell>
                          <TableCell>
                            {r.outcome === 'Signed' && (
                              <span className="text-sm text-emerald-700 dark:text-emerald-400">
                                Signed {fmtDate(r.signedAt)}
                              </span>
                            )}
                            {r.outcome === 'Declined' && (
                              <span className="text-sm text-red-700 dark:text-red-400">
                                Declined — {r.declineReason}
                              </span>
                            )}
                            {!r.outcome && (
                              <span className="text-sm text-muted-foreground">Outstanding</span>
                            )}
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>

              {totalPages > 1 && (
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">
                    Page {compliance.page} of {totalPages} · {compliance.totalRows} people
                  </span>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={compliance.page <= 1}
                      onClick={() => setPage((p) => p - 1)}
                    >
                      Previous
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={compliance.page >= totalPages}
                      onClick={() => setPage((p) => p + 1)}
                    >
                      Next
                    </Button>
                  </div>
                </div>
              )}
            </>
          )}
        </DialogContent>
      </Dialog>

      {/* ── Editor ───────────────────────────────────────────────────── */}
      <Dialog open={formOpen} onOpenChange={(o) => !o && close()}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit draft' : 'New policy'}</DialogTitle>
            <DialogDescription>
              Save the draft, attach the document, then publish. A published policy cannot be
              edited — publish a new version that supersedes it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="title">Title</Label>
              <Input
                id="title"
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="category">Category</Label>
                <Select
                  value={form.category}
                  onValueChange={(v) => setForm({ ...form, category: v as HrPolicyCategory })}
                >
                  <SelectTrigger id="category">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {POLICY_CATEGORIES.map((c) => (
                      <SelectItem key={c.value} value={c.value}>
                        {c.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="versionLabel">Version</Label>
                <Input
                  id="versionLabel"
                  value={form.versionLabel ?? ''}
                  onChange={(e) => setForm({ ...form, versionLabel: e.target.value })}
                  placeholder="v1.0"
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="summary">Summary</Label>
              <Textarea
                id="summary"
                rows={2}
                value={form.summary ?? ''}
                onChange={(e) => setForm({ ...form, summary: e.target.value })}
              />
            </div>

            <div className="space-y-3 rounded-lg border p-3">
              <div className="flex items-center gap-2">
                <Checkbox
                  id="requiresAck"
                  checked={form.requiresAcknowledgement}
                  onCheckedChange={(c) =>
                    setForm({ ...form, requiresAcknowledgement: c === true })
                  }
                />
                <Label htmlFor="requiresAck" className="font-normal">
                  Staff must acknowledge this policy
                </Label>
              </div>

              {form.requiresAcknowledgement && (
                <>
                  <div className="space-y-1.5">
                    <Label htmlFor="ackText">What they are agreeing to</Label>
                    <Textarea
                      id="ackText"
                      rows={2}
                      value={form.acknowledgementText ?? ''}
                      onChange={(e) => setForm({ ...form, acknowledgementText: e.target.value })}
                    />
                    <p className="text-xs text-muted-foreground">
                      This wording is copied onto every signature, so changing it later cannot
                      rewrite what somebody already agreed to.
                    </p>
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="dueDays">Days to acknowledge</Label>
                    <Input
                      id="dueDays"
                      type="number"
                      min={1}
                      value={form.acknowledgementDueDays ?? ''}
                      onChange={(e) =>
                        setForm({
                          ...form,
                          acknowledgementDueDays: e.target.value ? Number(e.target.value) : null,
                        })
                      }
                    />
                  </div>
                </>
              )}
            </div>

            <div className="space-y-1.5">
              <Label>Who it applies to</Label>
              <p className="text-sm text-muted-foreground">
                Everyone. A narrower audience can be set through the API; a policy addressed by a
                complicated rule is one whose scope nobody can explain, which is a poor property
                for something people sign.
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={close}>
              Cancel
            </Button>
            <Button
              onClick={() => save.mutate()}
              disabled={save.isPending || form.title.trim() === ''}
            >
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Document ─────────────────────────────────────────────────── */}
      <Dialog open={!!uploading} onOpenChange={(o) => !o && setUploading(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach the policy document</DialogTitle>
            <DialogDescription>
              The document staff will read. A policy cannot be published without one.
            </DialogDescription>
          </DialogHeader>
          {uploading && (
            <DocumentUploadField
              label="Policy document"
              endpoint={hrPoliciesService.documentEndpoint(uploading.id)}
              onUploaded={() => {
                setUploading(null);
                refresh();
                toast({ title: 'Attached' });
              }}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
