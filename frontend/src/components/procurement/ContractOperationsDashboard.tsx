'use client';

import { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import {
  AlertTriangle,
  Banknote,
  CalendarClock,
  CheckCircle2,
  CircleDollarSign,
  FileWarning,
  Gauge,
  Loader2,
  RefreshCw,
  Search,
  ShieldAlert,
} from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Progress } from '@/components/ui/progress';
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
import {
  contractService,
  type ContractOperationsDetail,
  type ContractOperationsPortfolio,
  type ContractOperationsPrompt,
} from '@/services/contractService';
import { useAuth } from '@/hooks/use-auth';

interface ContractOperationsDashboardProps {
  contractId?: string;
}

const money = (amount: number, currency: string) =>
  `${currency} ${amount.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;

const date = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      }).format(new Date(value))
    : '—';

const riskClass = (risk: string) =>
  risk === 'Critical'
    ? 'border-red-300 bg-red-50 text-red-800'
    : risk === 'High'
      ? 'border-orange-300 bg-orange-50 text-orange-800'
      : risk === 'Medium'
        ? 'border-amber-300 bg-amber-50 text-amber-800'
        : 'border-emerald-300 bg-emerald-50 text-emerald-800';

export function ContractOperationsDashboard({
  contractId,
}: ContractOperationsDashboardProps) {
  const { hasPermission } = useAuth();
  const canPublishPrompts = hasPermission('procurement.contract.manage');
  const [detail, setDetail] = useState<ContractOperationsDetail | null>(null);
  const [portfolio, setPortfolio] = useState<ContractOperationsPortfolio | null>(null);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [risk, setRisk] = useState('all');

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      if (contractId) {
        setDetail(await contractService.getContractOperationsDetail(contractId));
      } else {
        setPortfolio(
          await contractService.getContractOperations(
            search || undefined,
            status === 'all' ? undefined : status,
            risk === 'all' ? undefined : risk
          )
        );
      }
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : 'Unable to load contract operations';
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [contractId, risk, search, status]);

  useEffect(() => {
    void load();
  }, [load]);

  const processAlerts = async () => {
    try {
      setProcessing(true);
      const result = await contractService.processContractOperationsAlerts(contractId);
      toast.success(
        `${result.publishedAlertCount} prompt(s) published; ${result.alreadyPublishedCount} already current today.`
      );
      await load();
    } catch (cause) {
      toast.error(cause instanceof Error ? cause.message : 'Unable to publish prompts');
    } finally {
      setProcessing(false);
    }
  };

  if (loading) {
    return (
      <Card data-testid="contract-operations-dashboard">
        <CardContent className="flex min-h-52 items-center justify-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          Loading tenant-safe contract operations…
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Alert variant="destructive" data-testid="contract-operations-dashboard">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>Contract operations unavailable</AlertTitle>
        <AlertDescription className="mt-2 flex flex-wrap items-center gap-3">
          <span>{error}</span>
          <Button size="sm" variant="outline" onClick={() => void load()}>
            Retry
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  if (contractId && detail) {
    return (
      <ContractOperationsDetailView
        detail={detail}
        processing={processing}
        canPublishPrompts={canPublishPrompts}
        onRefresh={load}
        onProcess={processAlerts}
      />
    );
  }

  if (!contractId && portfolio) {
    return (
      <ContractOperationsPortfolioView
        portfolio={portfolio}
        search={search}
        status={status}
        risk={risk}
        processing={processing}
        canPublishPrompts={canPublishPrompts}
        setSearch={setSearch}
        setStatus={setStatus}
        setRisk={setRisk}
        onRefresh={load}
        onProcess={processAlerts}
      />
    );
  }

  return (
    <Alert data-testid="contract-operations-dashboard">
      <AlertTitle>No contract operations data</AlertTitle>
      <AlertDescription>No tenant-safe records were returned.</AlertDescription>
    </Alert>
  );
}

function ContractOperationsDetailView({
  detail,
  processing,
  canPublishPrompts,
  onRefresh,
  onProcess,
}: {
  detail: ContractOperationsDetail;
  processing: boolean;
  canPublishPrompts: boolean;
  onRefresh: () => Promise<void>;
  onProcess: () => Promise<void>;
}) {
  const item = detail.summary;
  const held = Math.max(0, item.retentionHeldAmount - item.retentionReleasedAmount);

  return (
    <div className="space-y-4" data-testid="contract-operations-dashboard">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-lg font-semibold">Contract operations control</h2>
            <Badge variant="outline" className={riskClass(item.overallRisk)}>
              {item.overallRisk} risk
            </Badge>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            Live spend, milestone, payment, retention, SLA/KPI, expiry, renewal, and
            supplier-risk evidence. Prompts never auto-post a financial penalty.
          </p>
        </div>
        <div className="flex gap-2">
          <Button size="sm" variant="outline" onClick={() => void onRefresh()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {canPublishPrompts && (
            <Button size="sm" onClick={() => void onProcess()} disabled={processing}>
              {processing ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <FileWarning className="mr-2 h-4 w-4" />
              )}
              Publish current prompts
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <MetricCard
          icon={CircleDollarSign}
          label="Contract value"
          value={money(item.contractValue, item.currency)}
          hint={`${money(item.committedSpend, item.currency)} committed`}
        />
        <MetricCard
          icon={Banknote}
          label="Spend to date"
          value={money(item.paidAmount, item.currency)}
          hint={`${money(item.outstandingAmount, item.currency)} outstanding`}
        />
        <MetricCard
          icon={ShieldAlert}
          label="Retention held"
          value={money(held, item.currency)}
          hint={`${detail.retentionPercentage.toFixed(2)}% contract term`}
        />
        <MetricCard
          icon={CheckCircle2}
          label="Milestones"
          value={`${item.completedMilestones} / ${item.totalMilestones}`}
          hint={`${item.overdueMilestones} overdue`}
        />
        <MetricCard
          icon={CalendarClock}
          label="Renewal"
          value={item.renewalStatus}
          hint={item.endDate ? `${date(item.endDate)} (${item.daysToExpiry} days)` : 'No end date'}
        />
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <AlertTriangle className="h-4 w-4 text-amber-600" />
            Automated prompts ({detail.prompts.length})
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {detail.prompts.length === 0 ? (
            <div className="rounded-md border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">
              No current delay, payment, retention, expiry, SLA/KPI, currency, or
              supplier-risk prompt was detected.
            </div>
          ) : (
            detail.prompts.map((prompt) => <PromptCard key={prompt.key} prompt={prompt} />)
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Gauge className="h-4 w-4" />
              SLA and KPI measures
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3 sm:grid-cols-2">
            {detail.kpis.map((kpi) => (
              <div key={kpi.key} className="rounded-md border p-3">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm font-medium">{kpi.label}</span>
                  <Badge variant={kpi.status === 'Breach' ? 'destructive' : 'outline'}>
                    {kpi.status}
                  </Badge>
                </div>
                <p className="mt-2 text-xl font-semibold">
                  {kpi.score === undefined ? '—' : `${kpi.score.toFixed(2)}%`}
                </p>
                {kpi.target !== undefined && (
                  <Progress className="mt-2" value={Math.max(0, Math.min(100, kpi.score ?? 0))} />
                )}
                <p className="mt-2 truncate text-xs text-muted-foreground">
                  {kpi.sourceReference}
                  {kpi.target !== undefined ? ` · target ${kpi.target.toFixed(2)}%` : ''}
                </p>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Controlled lineage</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <Line label="Activation" value={detail.lineage.activationStatus} />
            <Line
              label="Configuration"
              value={
                detail.lineage.configurationProfileId
                  ? `v${detail.lineage.configurationProfileVersion} · ${detail.lineage.configurationProfileId}`
                  : 'Unavailable'
              }
            />
            <Line
              label="Policy"
              value={
                detail.lineage.policySetId
                  ? `v${detail.lineage.policyVersion} · ${detail.lineage.policySetId}`
                  : 'Unavailable'
              }
            />
            <Line
              label="Workflow"
              value={detail.lineage.workflowInstanceId ?? detail.lineage.workflowDefinitionId ?? 'Unavailable'}
            />
            <div>
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                Decision register
              </p>
              <div className="flex flex-wrap gap-1">
                {detail.decisionKeys.map((key) => (
                  <Badge key={key} variant="secondary">
                    {key}
                  </Badge>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Milestone and payment position</CardTitle>
        </CardHeader>
        <CardContent className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Milestone</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Planned</TableHead>
                <TableHead className="text-right">Amount</TableHead>
                <TableHead className="text-right">Days late</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {detail.milestones.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center text-muted-foreground">
                    No milestones recorded.
                  </TableCell>
                </TableRow>
              ) : (
                detail.milestones.map((milestone) => (
                  <TableRow key={milestone.id}>
                    <TableCell className="font-medium">{milestone.name}</TableCell>
                    <TableCell>{milestone.status}</TableCell>
                    <TableCell>{date(milestone.plannedDate)}</TableCell>
                    <TableCell className="text-right">
                      {money(milestone.paymentAmount, item.currency)}
                    </TableCell>
                    <TableCell className="text-right">{milestone.daysLate}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}

function ContractOperationsPortfolioView({
  portfolio,
  search,
  status,
  risk,
  processing,
  canPublishPrompts,
  setSearch,
  setStatus,
  setRisk,
  onRefresh,
  onProcess,
}: {
  portfolio: ContractOperationsPortfolio;
  search: string;
  status: string;
  risk: string;
  processing: boolean;
  canPublishPrompts: boolean;
  setSearch: (value: string) => void;
  setStatus: (value: string) => void;
  setRisk: (value: string) => void;
  onRefresh: () => Promise<void>;
  onProcess: () => Promise<void>;
}) {
  return (
    <div className="space-y-5" data-testid="contract-operations-dashboard">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Contract operations</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Tenant-safe portfolio oversight and independently controlled penalty prompts.
          </p>
        </div>
        {canPublishPrompts && (
          <Button onClick={() => void onProcess()} disabled={processing}>
            {processing ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <FileWarning className="mr-2 h-4 w-4" />
            )}
            Publish current prompts
          </Button>
        )}
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard icon={Gauge} label="Contracts" value={String(portfolio.totalContracts)} />
        <MetricCard
          icon={CheckCircle2}
          label="Active"
          value={String(portfolio.activeContracts)}
        />
        <MetricCard
          icon={AlertTriangle}
          label="With prompts"
          value={String(portfolio.contractsWithPrompts)}
        />
        <MetricCard
          icon={ShieldAlert}
          label="Critical prompts"
          value={String(portfolio.criticalPromptCount)}
        />
      </div>

      <Card>
        <CardContent className="grid gap-3 pt-6 md:grid-cols-[1fr_180px_180px_auto]">
          <div className="relative">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              onKeyDown={(event) => event.key === 'Enter' && void onRefresh()}
              placeholder="Contract, title, or supplier"
            />
          </div>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger>
              <SelectValue placeholder="Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="Suspended">Suspended</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Terminated">Terminated</SelectItem>
            </SelectContent>
          </Select>
          <Select value={risk} onValueChange={setRisk}>
            <SelectTrigger>
              <SelectValue placeholder="Risk" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All risks</SelectItem>
              <SelectItem value="Critical">Critical</SelectItem>
              <SelectItem value="High">High</SelectItem>
              <SelectItem value="Medium">Medium</SelectItem>
              <SelectItem value="Low">Low</SelectItem>
            </SelectContent>
          </Select>
          <Button variant="outline" onClick={() => void onRefresh()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Apply
          </Button>
        </CardContent>
      </Card>

      <div className="grid gap-3 lg:grid-cols-2">
        {portfolio.currencyTotals.map((total) => (
          <Card key={total.currency}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm">{total.currency} portfolio</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
              <Line label="Contract" value={money(total.contractValue, total.currency)} />
              <Line label="Committed" value={money(total.committedSpend, total.currency)} />
              <Line label="Paid" value={money(total.paidAmount, total.currency)} />
              <Line label="Outstanding" value={money(total.outstandingAmount, total.currency)} />
              <Line label="Retention" value={money(total.retentionHeldAmount, total.currency)} />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardContent className="overflow-x-auto pt-6">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Contract</TableHead>
                <TableHead>Supplier</TableHead>
                <TableHead>Risk</TableHead>
                <TableHead>Milestones</TableHead>
                <TableHead className="text-right">Paid</TableHead>
                <TableHead>Expiry</TableHead>
                <TableHead className="text-right">Prompts</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {portfolio.items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center text-muted-foreground">
                    No contracts match the current tenant-safe filters.
                  </TableCell>
                </TableRow>
              ) : (
                portfolio.items.map((item) => (
                  <TableRow key={item.contractId}>
                    <TableCell>
                      <Link
                        href={`/procurement/contracts/${item.contractId}`}
                        className="font-medium text-blue-700 hover:underline"
                      >
                        {item.contractNumber}
                      </Link>
                      <p className="max-w-56 truncate text-xs text-muted-foreground">
                        {item.contractTitle}
                      </p>
                    </TableCell>
                    <TableCell>{item.businessPartnerName}</TableCell>
                    <TableCell>
                      <Badge variant="outline" className={riskClass(item.overallRisk)}>
                        {item.overallRisk}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {item.completedMilestones}/{item.totalMilestones}
                      {item.overdueMilestones > 0 && (
                        <span className="ml-2 text-xs text-red-700">
                          {item.overdueMilestones} late
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      {money(item.paidAmount, item.currency)}
                    </TableCell>
                    <TableCell>{item.renewalStatus}</TableCell>
                    <TableCell className="text-right">{item.promptCount}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div className="flex flex-wrap gap-1">
        {portfolio.decisionKeys.map((key) => (
          <Badge key={key} variant="secondary">
            {key}
          </Badge>
        ))}
      </div>
    </div>
  );
}

function MetricCard({
  icon: Icon,
  label,
  value,
  hint,
}: {
  icon: typeof Gauge;
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <Card>
      <CardContent className="pt-5">
        <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
          <Icon className="h-4 w-4" />
          {label}
        </div>
        <p className="mt-2 text-xl font-semibold">{value}</p>
        {hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}
      </CardContent>
    </Card>
  );
}

function PromptCard({ prompt }: { prompt: ContractOperationsPrompt }) {
  return (
    <div className={`rounded-md border p-3 ${riskClass(prompt.severity)}`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Badge variant="outline" className="bg-white/70">
            {prompt.severity}
          </Badge>
          <p className="font-medium">{prompt.title}</p>
        </div>
        <span className="text-xs">{prompt.sourceReference}</span>
      </div>
      <p className="mt-2 text-sm">{prompt.message}</p>
      <p className="mt-2 text-xs">
        <span className="font-semibold">Action:</span> {prompt.recommendedAction}
      </p>
      <div className="mt-2 flex flex-wrap gap-2 text-xs">
        <span>{prompt.calculationBasis}</span>
        <Badge variant="outline" className="bg-white/70">
          Independent approval required
        </Badge>
        <Badge variant="outline" className="bg-white/70">
          No auto-posting
        </Badge>
      </div>
    </div>
  );
}

function Line({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="break-all font-medium">{value}</p>
    </div>
  );
}
