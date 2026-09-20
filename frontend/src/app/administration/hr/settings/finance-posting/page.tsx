'use client';

/**
 * HR → Finance posting settings and register (HR finish plan lane 8).
 *
 * Three tabs, in the order an administrator has to work them: map the five ACCOUNT ROLES to
 * Finance accounts; enable the EVENTS that post (enabling is refused until the event's roles are
 * mapped — the API enforces it, this screen only shows why); watch the REGISTER, which is every
 * event that fired, whether Finance took it, and the queue of what to post once mapping is done.
 *
 * ⚠ Write is HR.Company.Admin (the policy-settings line). HR reads; administration maps, enables,
 * retries and reverses. A reversal creates a Finance journal.
 */

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Landmark, RefreshCw, Undo2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { FinanceAccountPicker } from '@/components/hr/common/FinanceAccountPicker';
import { HR_ADMIN_ROLES } from '@/components/hr/common/PermissionGate';
import { FinancePostingStatusBadge, fmtPostingMoney } from '@/components/hr/common/FinancePostingCard';
import { financePostingService } from '@/services/hr/finance-posting.service';
import {
  HR_FINANCE_POSTING_STATUSES,
  type HrFinanceAccountMapping,
  type HrFinancePostingRecord,
  type HrFinancePostingRule,
  type HrFinancePostingStatus,
} from '@/types/hr/finance-posting';

const SETTINGS_KEY = ['hr', 'finance-posting', 'settings'] as const;
const SUMMARY_KEY = ['hr', 'finance-posting', 'summary'] as const;

const fmtDate = (value?: string | null) => (value ? new Date(value).toLocaleDateString() : '—');

export default function HrFinancePostingSettingsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canAdmin = hasAnyPermission(['HR.Company.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const { data: settings, isLoading } = useQuery({ queryKey: SETTINGS_KEY, queryFn: () => financePostingService.getSettings() });

  const invalidateAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'finance-posting'] });
  };
  const onError = (title: string) => (e: Error) => toast({ variant: 'destructive', title, description: e.message });

  const upsertMapping = useMutation({
    mutationFn: (p: { role: HrFinanceAccountMapping['role']; accountId: string }) => financePostingService.upsertMapping(p),
    onSuccess: async () => { toast({ title: 'Account mapped' }); await invalidateAll(); },
    onError: onError('Could not map the account'),
  });
  const clearMapping = useMutation({
    mutationFn: (role: HrFinanceAccountMapping['role']) => financePostingService.clearMapping(role),
    onSuccess: async () => { toast({ title: 'Mapping removed' }); await invalidateAll(); },
    onError: onError('Could not remove the mapping'),
  });
  const upsertRule = useMutation({
    mutationFn: (rule: HrFinancePostingRule & { isEnabled: boolean }) =>
      financePostingService.upsertRule({
        eventCode: rule.eventCode,
        isEnabled: rule.isEnabled,
        postOnActionDate: rule.postOnActionDate,
        notes: rule.notes,
      }),
    onSuccess: async (_, v) => { toast({ title: v.isEnabled ? 'Event enabled' : 'Event disabled' }); await invalidateAll(); },
    onError: onError('Could not change the rule'),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Finance posting"
        description="Which Finance accounts HR posts to, which HR money events post, and whether each one reached the ledger."
        backHref="/administration/hr/settings"
      />

      {isLoading || !settings ? (
        <Skeleton className="h-64 w-full" />
      ) : (
        <>
          {settings.accountingBookProblem && (
            <Alert variant="destructive">
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Finance's accounting book cannot be resolved</AlertTitle>
              <AlertDescription>
                {settings.accountingBookProblem} No HR event can post until Finance sets the subledger posting book.
              </AlertDescription>
            </Alert>
          )}
          {!canAdmin && (
            <Alert>
              <AlertTitle>Read-only</AlertTitle>
              <AlertDescription>
                Mapping accounts, enabling events, retrying and reversing are administration actions (HR.Company.Admin).
              </AlertDescription>
            </Alert>
          )}
          <p className="text-sm text-muted-foreground">
            Functional currency <span className="font-medium">{settings.functionalCurrencyCode}</span>
            {settings.accountingBookCode && <> · accounting book <span className="font-medium">{settings.accountingBookCode}</span></>}
            . Finance owns what an account is, the period, the balance and the journal; HR owns the event and the record of whether it posted.
          </p>

          <Tabs defaultValue="accounts">
            <TabsList>
              <TabsTrigger value="accounts">Account roles</TabsTrigger>
              <TabsTrigger value="events">Events</TabsTrigger>
              <TabsTrigger value="register">Register</TabsTrigger>
            </TabsList>

            <TabsContent value="accounts" className="mt-4 space-y-4">
              {settings.mappings.map((m) => (
                <Card key={m.role}>
                  <CardHeader className="pb-3">
                    <CardTitle className="flex items-center justify-between gap-3 text-base">
                      <span>{m.roleName}</span>
                      <Badge variant="outline">{m.requiredAccountType} account</Badge>
                    </CardTitle>
                    <CardDescription>{m.roleDescription}</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    <FinanceAccountPicker
                      id={`role-${m.role}`}
                      label="Finance account"
                      value={m.accountId}
                      disabled={!canAdmin || upsertMapping.isPending}
                      onChange={(accountId) => {
                        if (accountId) upsertMapping.mutate({ role: m.role, accountId });
                        else if (m.accountId) clearMapping.mutate(m.role);
                      }}
                      description={
                        m.accountId
                          ? m.accountIsActive === false
                            ? `⚠ ${m.accountCode} ${m.accountName} is inactive in Finance — events using this role will refuse until it is re-mapped.`
                            : `Mapped to ${m.accountCode} ${m.accountName}.`
                          : 'Not mapped. Events that use this role cannot be enabled.'
                      }
                    />
                  </CardContent>
                </Card>
              ))}
            </TabsContent>

            <TabsContent value="events" className="mt-4 space-y-4">
              {settings.rules.map((rule) => (
                <Card key={rule.eventCode}>
                  <CardHeader className="pb-3">
                    <CardTitle className="flex flex-wrap items-center justify-between gap-3 text-base">
                      <span className="flex items-center gap-2">
                        <Landmark className="h-4 w-4" aria-hidden />
                        {rule.name}
                        <Badge variant="outline">{rule.area}</Badge>
                      </span>
                      <span className="flex items-center gap-3">
                        {rule.isReady ? (
                          <span className="flex items-center gap-1 text-xs text-muted-foreground"><CheckCircle2 className="h-3 w-3" /> ready</span>
                        ) : (
                          <span className="flex items-center gap-1 text-xs text-destructive"><AlertTriangle className="h-3 w-3" /> map {rule.missingRoles.join(', ')}</span>
                        )}
                        <Label htmlFor={`rule-${rule.eventCode}`} className="text-xs">{rule.isEnabled ? 'Posting' : 'Off'}</Label>
                        <Switch
                          id={`rule-${rule.eventCode}`}
                          checked={rule.isEnabled}
                          disabled={!canAdmin || upsertRule.isPending || (!rule.isEnabled && !rule.isReady)}
                          onCheckedChange={(checked) => upsertRule.mutate({ ...rule, isEnabled: checked })}
                        />
                      </span>
                    </CardTitle>
                    <CardDescription>{rule.trigger}</CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-2 text-sm">
                    <p>{rule.treatment}</p>
                    <p className="text-xs text-muted-foreground">
                      Debits {rule.debitRoles.join(', ')} · credits {rule.creditRoles.join(', ')} · Finance source type {rule.sourceDocumentType}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {rule.postedCount} posted · {rule.pendingCount} waiting (unposted or failed)
                      {rule.pendingCount > 0 && !rule.isEnabled && ' — enable the event, then post them from the register'}
                    </p>
                  </CardContent>
                </Card>
              ))}
            </TabsContent>

            <TabsContent value="register" className="mt-4">
              <Register canAdmin={canAdmin} />
            </TabsContent>
          </Tabs>
        </>
      )}
    </div>
  );
}

function Register({ canAdmin }: { canAdmin: boolean }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [status, setStatus] = useState<HrFinancePostingStatus | 'all'>('all');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [reversing, setReversing] = useState<HrFinancePostingRecord | null>(null);
  const [reason, setReason] = useState('');

  const query = useMemo(
    () => ({ status: status === 'all' ? undefined : status, search: search || undefined, pageNumber: page, pageSize: 25 }),
    [status, search, page],
  );
  const recordsKey = ['hr', 'finance-posting', 'records', query] as const;
  const { data: summary } = useQuery({ queryKey: SUMMARY_KEY, queryFn: () => financePostingService.getSummary() });
  const { data, isLoading } = useQuery({ queryKey: recordsKey, queryFn: () => financePostingService.getRecords(query) });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'finance-posting'] });
  const retry = useMutation({
    mutationFn: (id: string) => financePostingService.retry(id),
    onSuccess: async (r) => {
      toast({ title: r.status === 'Posted' ? `Posted as ${r.journalEntryNumber}` : `Recorded as ${r.status}`, description: r.statusReason ?? undefined });
      await refresh();
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Finance did not accept the posting', description: e.message }),
  });
  const reverse = useMutation({
    mutationFn: ({ id, why }: { id: string; why: string }) => financePostingService.reverse(id, why),
    onSuccess: async (r) => {
      toast({ title: `Reversed as ${r.reversalJournalEntryNumber ?? 'a Finance reversal'}` });
      setReversing(null);
      setReason('');
      await refresh();
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not reverse', description: e.message }),
  });

  return (
    <div className="space-y-4">
      {summary && (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-6">
          {(['posted', 'failed', 'unposted', 'skipped', 'reversed'] as const).map((k) => (
            <Card key={k}>
              <CardContent className="p-3">
                <div className="text-xs uppercase text-muted-foreground">{k}</div>
                <div className="text-xl font-semibold">{summary[k]}</div>
              </CardContent>
            </Card>
          ))}
          <Card>
            <CardContent className="p-3">
              <div className="text-xs uppercase text-muted-foreground">posted total</div>
              <div className="text-xl font-semibold">{fmtPostingMoney(summary.postedAmount, summary.functionalCurrencyCode)}</div>
            </CardContent>
          </Card>
        </div>
      )}

      <div className="flex flex-wrap gap-3">
        <Select value={status} onValueChange={(v) => { setStatus(v as HrFinancePostingStatus | 'all'); setPage(1); }}>
          <SelectTrigger className="w-44"><SelectValue placeholder="Status" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {HR_FINANCE_POSTING_STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
          </SelectContent>
        </Select>
        <Input
          className="w-64"
          placeholder="Claim, advance or journal number"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
        />
      </div>

      {isLoading || !data ? (
        <Skeleton className="h-48 w-full" />
      ) : data.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No posting rows match. An event appears here the first time it fires — approve or pay a claim to see one.</p>
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Event</TableHead>
                <TableHead>Source</TableHead>
                <TableHead className="text-right">Amount</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Detail</TableHead>
                <TableHead className="w-40" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((r) => (
                <TableRow key={r.id}>
                  <TableCell className="whitespace-nowrap">{r.eventName}</TableCell>
                  <TableCell className="font-medium whitespace-nowrap">{r.sourceReference}</TableCell>
                  <TableCell className="text-right whitespace-nowrap">{fmtPostingMoney(r.amount, r.currencyCode)}</TableCell>
                  <TableCell className="whitespace-nowrap">{fmtDate(r.postingDate)}</TableCell>
                  <TableCell><FinancePostingStatusBadge record={r} /></TableCell>
                  <TableCell className="max-w-md text-xs text-muted-foreground">
                    {r.status === 'Reversed' ? `${r.reversalJournalEntryNumber ?? ''} ${r.reversalReason ?? ''}` : r.statusReason ?? r.description}
                  </TableCell>
                  <TableCell>
                    {canAdmin && (
                      <div className="flex gap-1">
                        {r.canRetry && (
                          <Button size="sm" variant="outline" disabled={retry.isPending} onClick={() => retry.mutate(r.id)}>
                            <RefreshCw className="mr-1 h-3 w-3" /> Post
                          </Button>
                        )}
                        {r.canReverse && (
                          <Button size="sm" variant="ghost" onClick={() => setReversing(r)}>
                            <Undo2 className="mr-1 h-3 w-3" /> Reverse
                          </Button>
                        )}
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>{data.totalCount} row{data.totalCount === 1 ? '' : 's'}</span>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" disabled={!data.hasPrevious} onClick={() => setPage((p) => p - 1)}>Previous</Button>
              <Button size="sm" variant="outline" disabled={!data.hasNext} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          </div>
        </>
      )}

      <Dialog open={!!reversing} onOpenChange={(o) => !o && setReversing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reverse {reversing?.journalEntryNumber} in Finance</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            Finance posts an exact reversal of {reversing?.eventName.toLowerCase()} for {reversing?.sourceReference}. The source document is not changed.
          </p>
          <div className="space-y-2">
            <Label htmlFor="register-reversal-reason">Reason</Label>
            <Textarea id="register-reversal-reason" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="At least five characters" />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReversing(null)}>Cancel</Button>
            <Button variant="destructive" disabled={reason.trim().length < 5 || reverse.isPending}
              onClick={() => reversing && reverse.mutate({ id: reversing.id, why: reason.trim() })}>
              Reverse
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
