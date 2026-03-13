'use client';

import { useEffect, useMemo, useState } from 'react';
import { format } from 'date-fns';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { currencyService, type CurrencyListDto } from '@/services/financeCommonService';
import {
  CreateProjectBillingScheduleDto,
  CreateProjectInvoiceRequestDto,
  ProjectBillingSummaryReportItemDto,
  ProjectContractLookupDto,
  ProjectContractMilestoneLookupDto,
  ProjectDetailDto,
  ProjectInvoiceRequestDto,
  ProjectInvoiceRequestQueueItemDto,
  ProjectLookupDto,
  projectService,
} from '@/services/projectService';
import { toast } from 'sonner';

const DEFAULT_CURRENCIES = ['USD'];
const formatCurrencyLabel = (currency: CurrencyListDto) => `${currency.code} | ${currency.name}`;

const emptySchedule: CreateProjectBillingScheduleDto = {
  name: '',
  billingType: 'Milestone',
  amount: 0,
  billingDate: new Date().toISOString().slice(0, 10),
  status: 'Draft',
  description: '',
  isBillable: true,
};

const emptyInvoice: CreateProjectInvoiceRequestDto = {
  requestedAmount: 0,
  currency: 'USD',
  status: 'Draft',
  notes: '',
};

export default function ProjectBillingPage() {
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('all');
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [summary, setSummary] = useState<ProjectBillingSummaryReportItemDto[]>([]);
  const [invoiceQueue, setInvoiceQueue] = useState<ProjectInvoiceRequestQueueItemDto[]>([]);
  const [contracts, setContracts] = useState<ProjectContractLookupDto[]>([]);
  const [contractMilestones, setContractMilestones] = useState<ProjectContractMilestoneLookupDto[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [queueFilter, setQueueFilter] = useState<string>('all');
  const [schedule, setSchedule] = useState<CreateProjectBillingScheduleDto>(emptySchedule);
  const [invoice, setInvoice] = useState<CreateProjectInvoiceRequestDto>(emptyInvoice);
  const [savingSchedule, setSavingSchedule] = useState(false);
  const [savingInvoice, setSavingInvoice] = useState(false);
  const currencyOptions = useMemo(() => {
    const codes = currencies
      .filter((currency) => currency.isActive)
      .map((currency) => currency.code.trim().toUpperCase())
      .filter(Boolean);
    const values = codes.length > 0 ? codes : DEFAULT_CURRENCIES;
    return invoice.currency && !values.includes(invoice.currency) ? [invoice.currency, ...values] : values;
  }, [currencies, invoice.currency]);

  const load = async (projectId?: string) => {
    try {
      setLoading(true);
      const [projectItems, summaryItems, queueItems, loadedCurrencies] = await Promise.all([
        projectService.lookupProjects(),
        projectService.getBillingSummaryReport(50),
        projectService.getInvoiceRequestQueueReport(200, queueFilter === 'all' ? undefined : queueFilter),
        currencyService.getActive().catch(() => []),
      ]);

      setProjects(projectItems);
      setSummary(summaryItems);
      setInvoiceQueue(queueItems);
      setCurrencies(loadedCurrencies);

      const resolvedProjectId = projectId === 'all'
        ? undefined
        : projectId && projectId !== 'all'
          ? projectId
          : selectedProjectId !== 'all'
            ? selectedProjectId
            : projectItems[0]?.id;

      if (resolvedProjectId) {
        setSelectedProjectId(resolvedProjectId);
        const detail = await projectService.getProjectById(resolvedProjectId);
        setProject(detail);
        const contractItems = await projectService.getContractLookup(detail.businessPartnerId);
        setContracts(contractItems);
        const activeContractId = detail.contractId || contractItems[0]?.id;
        if (activeContractId) {
          setSchedule((prev) => ({ ...prev, contractId: prev.contractId || activeContractId }));
          setInvoice((prev) => ({ ...prev, contractId: prev.contractId || activeContractId }));
          setContractMilestones(await projectService.getContractMilestones(activeContractId));
        } else {
          setContractMilestones([]);
        }
      } else {
        setSelectedProjectId('all');
        setProject(null);
        setContracts([]);
        setContractMilestones([]);
      }
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project billing');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    load(selectedProjectId);
  }, [queueFilter]);

  const refreshProject = async (projectId: string) => {
    setSelectedProjectId(projectId);
    if (projectId === 'all') {
      setProject(null);
      return;
    }

    try {
      const detail = await projectService.getProjectById(projectId);
      setProject(detail);
      const contractItems = await projectService.getContractLookup(detail.businessPartnerId);
      setContracts(contractItems);
      const activeContractId = detail.contractId || contractItems[0]?.id;
      if (activeContractId) {
        setSchedule((prev) => ({ ...prev, contractId: prev.contractId || activeContractId }));
        setInvoice((prev) => ({ ...prev, contractId: prev.contractId || activeContractId }));
        setContractMilestones(await projectService.getContractMilestones(activeContractId));
      } else {
        setContractMilestones([]);
      }
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project billing');
    }
  };

  const saveSchedule = async () => {
    if (!project) {
      toast.error('Select a project first');
      return;
    }

    try {
      setSavingSchedule(true);
      await projectService.addBillingSchedule(project.id, schedule);
      setSchedule(emptySchedule);
      await load(project.id);
      toast.success('Billing schedule saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save billing schedule');
    } finally {
      setSavingSchedule(false);
    }
  };

  const saveInvoice = async () => {
    if (!project) {
      toast.error('Select a project first');
      return;
    }

    try {
      setSavingInvoice(true);
      await projectService.createInvoiceRequest(project.id, invoice);
      setInvoice(emptyInvoice);
      await load(project.id);
      toast.success('Invoice request created');
    } catch (error: any) {
      toast.error(error.message || 'Failed to create invoice request');
    } finally {
      setSavingInvoice(false);
    }
  };

  const generateInvoice = async (billingScheduleId: string) => {
    try {
      await projectService.generateInvoiceRequestFromSchedule(billingScheduleId);
      if (project) {
        await load(project.id);
      }
      toast.success('Invoice request generated from billing schedule');
    } catch (error: any) {
      toast.error(error.message || 'Failed to generate invoice request');
    }
  };

  const removeSchedule = async (billingScheduleId: string) => {
    if (!window.confirm('Delete this billing schedule?')) {
      return;
    }

    try {
      await projectService.deleteBillingSchedule(billingScheduleId);
      if (project) {
        await load(project.id);
      }
      toast.success('Billing schedule deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete billing schedule');
    }
  };

  const cards = useMemo(() => {
    const totalScheduled = summary.reduce((sum, item) => sum + item.scheduledBillingAmount, 0);
    const totalRequested = summary.reduce((sum, item) => sum + item.invoiceRequestedAmount, 0);
    const collectedCash = summary.reduce((sum, item) => sum + item.collectedCashAmount, 0);
    const readyToBill = summary.reduce((sum, item) => sum + item.readyBillingScheduleCount, 0);
    const overdueSchedules = summary.reduce((sum, item) => sum + item.overdueBillingScheduleCount, 0);
    const draftRequests = summary.reduce((sum, item) => sum + item.draftInvoiceRequestCount, 0);
    const submittedRequests = summary.reduce((sum, item) => sum + item.submittedInvoiceRequestCount, 0);
    const sentToFinance = summary.reduce((sum, item) => sum + item.sentToFinanceInvoiceRequestCount, 0);
    const invoicedRequests = summary.reduce((sum, item) => sum + item.invoicedInvoiceRequestCount, 0);
    const paidRequests = summary.reduce((sum, item) => sum + item.paidInvoiceRequestCount, 0);
    const recognizedRevenue = summary.reduce((sum, item) => sum + item.recognizedRevenue, 0);
    const totalMargin = summary.reduce((sum, item) => sum + item.marginAmount, 0);
    const marginPressure = summary.filter((item) => item.marginAmount < 0).length;

    return { totalScheduled, totalRequested, collectedCash, readyToBill, overdueSchedules, draftRequests, submittedRequests, sentToFinance, invoicedRequests, paidRequests, recognizedRevenue, totalMargin, marginPressure };
  }, [summary]);

  const selectedSummary = useMemo(
    () => (project ? summary.find((item) => item.projectId === project.id) ?? null : null),
    [project, summary],
  );

  const generateRevenue = async () => {
    if (!project) return;
    try {
      await projectService.generateRevenueRecognition(project.id);
      await load(project.id);
      toast.success('Revenue recognition generated');
    } catch (error: any) {
      toast.error(error.message || 'Failed to generate revenue recognition');
    }
  };

  const generateReadyInvoices = async () => {
    if (!project) return;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const readySchedules = project.billingSchedules.filter((item) => {
      const billingDate = new Date(item.billingDate);
      billingDate.setHours(0, 0, 0, 0);
      return item.status !== 'Invoiced' && item.isBillable && billingDate <= today;
    });
    if (readySchedules.length === 0) {
      toast.error('No due billing schedules found');
      return;
    }

    try {
      await Promise.all(readySchedules.map((item) => projectService.generateInvoiceRequestFromSchedule(item.id)));
      await load(project.id);
      toast.success(`Generated ${readySchedules.length} invoice request(s)`);
    } catch (error: any) {
      toast.error(error.message || 'Failed to generate invoice requests');
    }
  };

  const submitInvoiceRequest = async (invoiceRequestId: string) => {
    try {
      await projectService.submitInvoiceRequest(invoiceRequestId);
      if (project) {
        await load(project.id);
      }
      toast.success('Invoice request submitted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to submit invoice request');
    }
  };

  const sendInvoiceRequestToFinance = async (invoiceRequest: ProjectInvoiceRequestDto) => {
    const defaultReference = invoiceRequest.externalReference || invoiceRequest.requestNumber;
    const externalReference = window.prompt('Finance reference / exported invoice number', defaultReference);
    if (externalReference === null) {
      return;
    }

    try {
      await projectService.sendInvoiceRequestToFinance(invoiceRequest.id, {
        externalReference: externalReference.trim() || undefined,
      });
      if (project) {
        await load(project.id);
      }
      toast.success('Invoice request sent to finance');
    } catch (error: any) {
      toast.error(error.message || 'Failed to send invoice request to finance');
    }
  };

  const markInvoiceRequestInvoiced = async (invoiceRequest: ProjectInvoiceRequestDto) => {
    const defaultReference = invoiceRequest.externalReference || invoiceRequest.requestNumber;
    const externalReference = window.prompt('Invoice number / finance reference', defaultReference);
    if (externalReference === null) {
      return;
    }

    try {
      await projectService.markInvoiceRequestInvoiced(invoiceRequest.id, {
        externalReference: externalReference.trim() || undefined,
      });
      if (project) {
        await load(project.id);
      }
      toast.success('Invoice request marked as invoiced');
    } catch (error: any) {
      toast.error(error.message || 'Failed to mark invoice request as invoiced');
    }
  };

  const markInvoiceRequestPaid = async (invoiceRequestId: string) => {
    try {
      await projectService.markInvoiceRequestPaid(invoiceRequestId);
      if (project) {
        await load(project.id);
      }
      toast.success('Invoice request marked as paid');
    } catch (error: any) {
      toast.error(error.message || 'Failed to mark invoice request as paid');
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Project Billing</h1>
        <p className="text-muted-foreground">Manage billing schedules, invoice requests, and revenue tracking.</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
        <Card><CardHeader className="pb-2"><CardDescription>Projects Tracked</CardDescription><CardTitle>{summary.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Scheduled Amount</CardDescription><CardTitle>{cards.totalScheduled.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Invoice Requests</CardDescription><CardTitle>{cards.totalRequested.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Collected Cash</CardDescription><CardTitle>{cards.collectedCash.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Recognized Revenue</CardDescription><CardTitle>{cards.recognizedRevenue.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Margin Pressure</CardDescription><CardTitle>{cards.marginPressure}</CardTitle></CardHeader></Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Billing Queue Snapshot</CardTitle>
          <CardDescription>Current billing and finance handoff totals.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Ready To Bill</div>
            <div className="mt-1 text-2xl font-semibold">{cards.readyToBill}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Overdue Schedules</div>
            <div className="mt-1 text-2xl font-semibold">{cards.overdueSchedules}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Draft Requests</div>
            <div className="mt-1 text-2xl font-semibold">{cards.draftRequests}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Submitted Requests</div>
            <div className="mt-1 text-2xl font-semibold">{cards.submittedRequests}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Sent To Finance</div>
            <div className="mt-1 text-2xl font-semibold">{cards.sentToFinance}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Invoiced</div>
            <div className="mt-1 text-2xl font-semibold">{cards.invoicedRequests}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Paid</div>
            <div className="mt-1 text-2xl font-semibold">{cards.paidRequests}</div>
          </div>
          <div className="rounded-lg border p-4">
            <div className="text-sm text-muted-foreground">Total Margin</div>
            <div className={`mt-1 text-2xl font-semibold ${cards.totalMargin < 0 ? 'text-red-600' : ''}`}>{cards.totalMargin.toLocaleString()}</div>
          </div>
        </CardContent>
      </Card>

      {project && selectedSummary ? (
        <Card>
          <CardHeader className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
            <div>
              <CardTitle>Commercial Readiness</CardTitle>
              <CardDescription>{project.projectCode} | {project.title}</CardDescription>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant={selectedSummary.marginAmount < 0 ? 'destructive' : 'outline'}>
                Margin {selectedSummary.marginPercent.toFixed(2)}%
              </Badge>
              <Badge variant={selectedSummary.readyBillingScheduleCount > 0 ? 'secondary' : 'outline'}>
                {selectedSummary.readyBillingScheduleCount > 0 ? `${selectedSummary.readyBillingScheduleCount} ready schedule(s)` : 'No ready schedules'}
              </Badge>
              <Badge variant={selectedSummary.overdueBillingScheduleCount > 0 ? 'destructive' : 'outline'}>
                {selectedSummary.overdueBillingScheduleCount > 0 ? `${selectedSummary.overdueBillingScheduleCount} overdue` : 'No overdue schedules'}
              </Badge>
              <Button variant="outline" size="sm" onClick={generateReadyInvoices}>Generate Ready Invoices</Button>
              <Button variant="outline" size="sm" onClick={generateRevenue}>Generate Revenue Recognition</Button>
            </div>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Scheduled</div><div className="mt-1 text-xl font-semibold">{selectedSummary.scheduledBillingAmount.toLocaleString()}</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Requested</div><div className="mt-1 text-xl font-semibold">{selectedSummary.invoiceRequestedAmount.toLocaleString()}</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Collected</div><div className="mt-1 text-xl font-semibold">{selectedSummary.collectedCashAmount.toLocaleString()}</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Unbilled</div><div className="mt-1 text-xl font-semibold">{selectedSummary.unbilledAmount.toLocaleString()}</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Coverage</div><div className="mt-1 text-xl font-semibold">{selectedSummary.billingCoveragePercent.toFixed(2)}%</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Recognized Revenue</div><div className="mt-1 text-xl font-semibold">{selectedSummary.recognizedRevenue.toLocaleString()}</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Revenue Gap</div><div className="mt-1 text-xl font-semibold">{selectedSummary.revenueGapAmount.toLocaleString()}</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Actual Cost</div><div className="mt-1 text-xl font-semibold">{selectedSummary.actualCost.toLocaleString()}</div></div>
            <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Margin</div><div className={`mt-1 text-xl font-semibold ${selectedSummary.marginAmount < 0 ? 'text-red-600' : ''}`}>{selectedSummary.marginAmount.toLocaleString()}</div></div>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <div>
            <CardTitle>Invoice Request Handoff Queue</CardTitle>
            <CardDescription>Invoice request queue across accessible projects.</CardDescription>
          </div>
            <div className="w-full md:w-56">
              <Select value={queueFilter} onValueChange={setQueueFilter}>
                <SelectTrigger><SelectValue placeholder="Filter queue" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  <SelectItem value="Draft">Draft</SelectItem>
                  <SelectItem value="Submitted">Submitted</SelectItem>
                  <SelectItem value="SentToFinance">Sent To Finance</SelectItem>
                  <SelectItem value="Invoiced">Invoiced</SelectItem>
                  <SelectItem value="Paid">Paid</SelectItem>
                </SelectContent>
              </Select>
            </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading finance queue...</div>
          ) : !invoiceQueue.length ? (
            <div className="py-10 text-center text-muted-foreground">No invoice requests match the current queue filter.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Request</TableHead>
                  <TableHead>Project</TableHead>
                  <TableHead>Stage</TableHead>
                  <TableHead>Amount</TableHead>
                  <TableHead>Schedule</TableHead>
                  <TableHead>Outstanding</TableHead>
                  <TableHead>Reference</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {invoiceQueue.map((item) => (
                  <TableRow key={item.invoiceRequestId}>
                    <TableCell>
                      <div className="font-medium">{item.requestNumber}</div>
                      <div className="text-sm text-muted-foreground">{format(new Date(item.requestedAt), 'MMM dd, yyyy HH:mm')}</div>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{item.projectCode}</div>
                      <div className="text-sm text-muted-foreground">{item.projectTitle}</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={item.status === 'Paid' ? 'secondary' : item.status === 'Invoiced' ? 'secondary' : item.status === 'SentToFinance' ? 'secondary' : item.status === 'Submitted' ? 'outline' : 'destructive'}>
                        {item.queueStage}
                      </Badge>
                    </TableCell>
                    <TableCell>{item.currency} {item.requestedAmount.toLocaleString()}</TableCell>
                    <TableCell>
                      {item.billingScheduleName ? (
                        <div>
                          <div className="font-medium">{item.billingScheduleName}</div>
                          <div className="text-sm text-muted-foreground">{item.billingDate ? format(new Date(item.billingDate), 'MMM dd, yyyy') : 'No billing date'}</div>
                        </div>
                      ) : (
                        <span className="text-muted-foreground">Manual request</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge variant={item.daysOutstanding > 7 ? 'destructive' : 'outline'}>
                        {item.daysOutstanding} day{item.daysOutstanding === 1 ? '' : 's'}
                      </Badge>
                    </TableCell>
                    <TableCell>{item.externalReference || 'Pending'}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-2">
                        <Button variant="ghost" size="sm" onClick={() => refreshProject(item.projectId)}>Open Project</Button>
                        {item.status === 'Draft' ? (
                          <Button variant="outline" size="sm" onClick={() => submitInvoiceRequest(item.invoiceRequestId)}>Submit</Button>
                        ) : null}
                        {(item.status === 'Draft' || item.status === 'Submitted') ? (
                          <Button variant="outline" size="sm" onClick={() => sendInvoiceRequestToFinance({
                            id: item.invoiceRequestId,
                            requestNumber: item.requestNumber,
                            requestedAmount: item.requestedAmount,
                            currency: item.currency,
                            status: item.status,
                            requestedAt: item.requestedAt,
                            submittedAt: item.submittedAt,
                            externalReference: item.externalReference,
                            projectId: item.projectId,
                            billingScheduleId: item.billingScheduleId,
                            contractId: item.contractId,
                            notes: item.notes,
                          })}>
                            Send To Finance
                          </Button>
                        ) : null}
                        {item.canMarkInvoiced ? (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => markInvoiceRequestInvoiced({
                              id: item.invoiceRequestId,
                              requestNumber: item.requestNumber,
                              requestedAmount: item.requestedAmount,
                              currency: item.currency,
                              status: item.status,
                              requestedAt: item.requestedAt,
                              submittedAt: item.submittedAt,
                              externalReference: item.externalReference,
                              projectId: item.projectId,
                              billingScheduleId: item.billingScheduleId,
                              contractId: item.contractId,
                              notes: item.notes,
                            })}
                          >
                            Mark Invoiced
                          </Button>
                        ) : null}
                        {item.canMarkPaid ? (
                          <Button variant="outline" size="sm" onClick={() => markInvoiceRequestPaid(item.invoiceRequestId)}>
                            Mark Paid
                          </Button>
                        ) : null}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Billing Summary</CardTitle>
            <CardDescription>{loading ? 'Loading...' : 'Billing status across active projects'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-10 text-center text-muted-foreground">Loading billing summary...</div>
            ) : !summary.length ? (
              <div className="py-10 text-center text-muted-foreground">No billing data available.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Project</TableHead>
                    <TableHead>Ready</TableHead>
                    <TableHead>Overdue</TableHead>
                    <TableHead>Scheduled</TableHead>
                    <TableHead>Requested</TableHead>
                    <TableHead>Collected</TableHead>
                    <TableHead>Recognized</TableHead>
                    <TableHead>Coverage</TableHead>
                    <TableHead>Pending</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {summary.map((item) => (
                    <TableRow key={item.projectId}>
                      <TableCell>
                        <div className="font-medium">{item.projectCode}</div>
                        <div className="text-sm text-muted-foreground">{item.projectTitle}</div>
                      </TableCell>
                      <TableCell>{item.readyBillingScheduleCount}</TableCell>
                      <TableCell>{item.overdueBillingScheduleCount}</TableCell>
                      <TableCell>{item.scheduledBillingAmount.toLocaleString()}</TableCell>
                      <TableCell>{item.invoiceRequestedAmount.toLocaleString()}</TableCell>
                      <TableCell>{item.collectedCashAmount.toLocaleString()}</TableCell>
                      <TableCell>{item.recognizedRevenue.toLocaleString()}</TableCell>
                      <TableCell>{item.billingCoveragePercent.toFixed(2)}%</TableCell>
                      <TableCell>{item.unbilledAmount.toLocaleString()}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Create Billing Schedule</CardTitle>
            <CardDescription>Create a billing schedule for the selected project.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {contractMilestones.length > 0 ? (
              <div className="grid gap-3 md:grid-cols-2">
                {contractMilestones.slice(0, 4).map((item) => (
                  <div key={item.id} className="rounded-lg border bg-slate-50 p-3 dark:bg-slate-900/40">
                    <div className="font-medium">{item.milestoneName}</div>
                    <div className="mt-1 text-sm text-muted-foreground">{item.paymentAmount.toLocaleString()} | {item.paymentPercentage}%</div>
                    <div className="mt-1 text-xs text-muted-foreground">{item.plannedDate ? format(new Date(item.plannedDate), 'MMM dd, yyyy') : 'No planned date'} | {item.status}</div>
                  </div>
                ))}
              </div>
            ) : null}
            <div className="grid gap-2">
              <Label>Project</Label>
              <Select value={selectedProjectId} onValueChange={(value) => refreshProject(value)}>
                <SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">Select project</SelectItem>
                  {projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.projectCode} | {item.title}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <Label>Name</Label>
              <Input value={schedule.name} onChange={(e) => setSchedule((prev) => ({ ...prev, name: e.target.value }))} />
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Contract</Label>
                <Select value={schedule.contractId || project?.contractId || 'none'} onValueChange={async (value) => {
                  const contractId = value === 'none' ? undefined : value;
                  setSchedule((prev) => ({ ...prev, contractId, contractMilestoneId: undefined }));
                  setContractMilestones(contractId ? await projectService.getContractMilestones(contractId) : []);
                }}>
                  <SelectTrigger><SelectValue placeholder="Optional contract" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No contract</SelectItem>
                    {contracts.map((item) => <SelectItem key={item.id} value={item.id}>{item.contractNumber} | {item.contractTitle}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Billing Type</Label>
                <Select value={schedule.billingType} onValueChange={(value) => setSchedule((prev) => ({ ...prev, billingType: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Milestone">Milestone</SelectItem>
                    <SelectItem value="FixedPrice">Fixed Price</SelectItem>
                    <SelectItem value="TimeAndMaterials">Time and Materials</SelectItem>
                    <SelectItem value="Retainer">Retainer</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Milestone</Label>
                <Select value={schedule.milestoneId || 'none'} onValueChange={(value) => setSchedule((prev) => ({ ...prev, milestoneId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Optional milestone" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No milestone</SelectItem>
                    {project?.milestones.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Contract Milestone</Label>
                <Select value={schedule.contractMilestoneId || 'none'} onValueChange={(value) => {
                  const milestoneId = value === 'none' ? undefined : value;
                  const selected = contractMilestones.find((item) => item.id === value);
                  setSchedule((prev) => ({
                    ...prev,
                    contractMilestoneId: milestoneId,
                    name: selected ? selected.milestoneName : prev.name,
                    amount: selected ? selected.paymentAmount : prev.amount,
                    billingPercentage: selected ? selected.paymentPercentage : prev.billingPercentage,
                    billingDate: selected?.plannedDate || prev.billingDate,
                  }));
                }}>
                  <SelectTrigger><SelectValue placeholder="Optional contract milestone" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No contract milestone</SelectItem>
                    {contractMilestones.map((item) => <SelectItem key={item.id} value={item.id}>{item.milestoneName}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Amount</Label>
                <Input type="number" value={schedule.amount} onChange={(e) => setSchedule((prev) => ({ ...prev, amount: Number(e.target.value || '0') }))} />
              </div>
              <div className="grid gap-2">
                <Label>Billing Date</Label>
                <Input type="date" value={String(schedule.billingDate).slice(0, 10)} onChange={(e) => setSchedule((prev) => ({ ...prev, billingDate: e.target.value }))} />
              </div>
            </div>
            <div className="grid gap-2">
              <Label>Description</Label>
              <Input value={schedule.description || ''} onChange={(e) => setSchedule((prev) => ({ ...prev, description: e.target.value }))} />
            </div>
            <Button onClick={saveSchedule} disabled={savingSchedule || !project || !schedule.name}>
              {savingSchedule ? 'Saving...' : 'Save Billing Schedule'}
            </Button>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Billing Schedules</CardTitle>
            <CardDescription>{project ? `${project.projectCode} | ${project.title}` : 'Select a project to manage schedules'}</CardDescription>
          </CardHeader>
          <CardContent>
            {!project ? (
              <div className="py-10 text-center text-muted-foreground">Select a project to manage billing schedules.</div>
            ) : !project.billingSchedules.length ? (
              <div className="py-10 text-center text-muted-foreground">No billing schedules created yet.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Name</TableHead>
                    <TableHead>Date</TableHead>
                    <TableHead>Amount</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {project.billingSchedules.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <div className="font-medium">{item.name}</div>
                        <div className="text-sm text-muted-foreground">{item.billingType}{item.contractMilestoneId ? ' | Contract milestone linked' : ''}</div>
                      </TableCell>
                      <TableCell>{format(new Date(item.billingDate), 'MMM dd, yyyy')}</TableCell>
                      <TableCell>{item.amount.toLocaleString()}</TableCell>
                      <TableCell><Badge variant="outline">{item.status}</Badge></TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button variant="outline" size="sm" disabled={item.status === 'Invoiced'} onClick={() => generateInvoice(item.id)}>Generate Invoice</Button>
                          <Button variant="ghost" size="sm" onClick={() => removeSchedule(item.id)}>Delete</Button>
                        </div>
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
            <CardTitle>Create Invoice Request</CardTitle>
            <CardDescription>Create an invoice request for the selected project.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-2">
              <Label>Billing Schedule</Label>
              <Select value={invoice.billingScheduleId || 'none'} onValueChange={(value) => {
                const selected = project?.billingSchedules.find((item) => item.id === value);
                setInvoice((prev) => ({
                  ...prev,
                  billingScheduleId: value === 'none' ? undefined : value,
                  requestedAmount: selected?.amount ?? prev.requestedAmount,
                }));
              }}>
                <SelectTrigger><SelectValue placeholder="Optional schedule" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Manual invoice request</SelectItem>
                  {project?.billingSchedules.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2">
                <Label>Requested Amount</Label>
                <Input type="number" value={invoice.requestedAmount} onChange={(e) => setInvoice((prev) => ({ ...prev, requestedAmount: Number(e.target.value || '0') }))} />
              </div>
              <div className="grid gap-2">
                <Label>Currency</Label>
                <Select value={invoice.currency || currencyOptions[0]} onValueChange={(value) => setInvoice((prev) => ({ ...prev, currency: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((code) => {
                      const currency = currencies.find((item) => item.code.toUpperCase() === code.toUpperCase());
                      return (
                        <SelectItem key={code} value={code}>
                          {currency ? formatCurrencyLabel(currency) : code}
                        </SelectItem>
                      );
                    })}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              Contract: {contracts.find((item) => item.id === (invoice.contractId || project?.contractId))?.contractNumber || 'Not linked'}
            </div>
            <div className="grid gap-2">
              <Label>External Reference</Label>
              <Input value={invoice.externalReference || ''} onChange={(e) => setInvoice((prev) => ({ ...prev, externalReference: e.target.value }))} />
            </div>
            <div className="grid gap-2">
              <Label>Notes</Label>
              <Input value={invoice.notes || ''} onChange={(e) => setInvoice((prev) => ({ ...prev, notes: e.target.value }))} />
            </div>
            <Button onClick={saveInvoice} disabled={savingInvoice || !project || invoice.requestedAmount <= 0}>
              {savingInvoice ? 'Saving...' : 'Create Invoice Request'}
            </Button>

            <div className="space-y-3 border-t pt-4">
              <div className="text-sm font-medium">Recent Invoice Requests</div>
              {!project?.invoiceRequests.length ? (
                <div className="text-sm text-muted-foreground">No invoice requests created yet.</div>
              ) : (
                project.invoiceRequests.slice(0, 6).map((item) => (
                    <div key={item.id} className="rounded-lg border p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <div className="font-medium">{item.requestNumber}</div>
                          <div className="text-sm text-muted-foreground">{item.currency} {item.requestedAmount.toLocaleString()}</div>
                      </div>
                      <Badge variant="outline">{item.status}</Badge>
                      </div>
                      {item.externalReference ? (
                        <div className="mt-2 text-xs text-muted-foreground">Finance ref: {item.externalReference}</div>
                      ) : null}
                      <div className="mt-2 text-xs text-muted-foreground">{format(new Date(item.requestedAt), 'MMM dd, yyyy HH:mm')}</div>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {item.status === 'Draft' ? (
                          <Button variant="outline" size="sm" onClick={() => submitInvoiceRequest(item.id)}>Submit</Button>
                        ) : null}
                        {(item.status === 'Draft' || item.status === 'Submitted') ? (
                          <Button variant="outline" size="sm" onClick={() => sendInvoiceRequestToFinance(item)}>
                            Send to Finance
                          </Button>
                        ) : null}
                        {(item.status === 'Submitted' || item.status === 'SentToFinance') ? (
                          <Button variant="outline" size="sm" onClick={() => markInvoiceRequestInvoiced(item)}>
                            Mark Invoiced
                          </Button>
                        ) : null}
                        {(item.status === 'SentToFinance' || item.status === 'Invoiced') ? (
                          <Button variant="outline" size="sm" onClick={() => markInvoiceRequestPaid(item.id)}>
                            Mark Paid
                          </Button>
                        ) : null}
                      </div>
                    </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
