'use client';

import { hasCustomerRole } from '@/lib/business-partner-roles';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import {
  crmService,
  type CreateCrmOpportunityDto,
  type CrmLeadListItemDto,
  type CrmOpportunityDetailDto,
  type CrmOpportunityListItemDto,
  type PagedResult,
} from '@/services/crmService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { SalesHandoffActions } from '../components/SalesHandoffActions';
import {
  BriefcaseBusiness,
  CalendarClock,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Target,
  Trash2,
  TrendingUp,
} from 'lucide-react';
import { toast } from 'sonner';

const OPPORTUNITY_STAGE_OPTIONS = ['Prospecting', 'Qualification', 'Proposal', 'Negotiation', 'Closed Won', 'Closed Lost'];
const OPPORTUNITY_TYPE_OPTIONS = ['New Business', 'Existing Customer', 'Renewal', 'Upsell'];
const LEAD_SOURCE_OPTIONS = ['Referral', 'Website', 'Campaign', 'Tender', 'Partner', 'Direct', 'Unknown'];

const addDays = (days: number) => {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return date.toISOString().slice(0, 10);
};

const createEmptyOpportunityForm = (businessPartnerId?: string, leadId?: string, opportunityType?: string): CreateCrmOpportunityDto => ({
  name: '',
  description: '',
  businessPartnerId: businessPartnerId || '',
  leadId: leadId || '',
  stage: 'Prospecting',
  probability: 10,
  amount: 0,
  currency: 'USD',
  expectedCloseDate: addDays(30),
  actualCloseDate: '',
  leadSource: 'Unknown',
  opportunityType: opportunityType || 'New Business',
  assignedToId: undefined,
  competitors: '',
  notes: '',
  lossReason: '',
});

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';

const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;
const resolveChainHref = (entityType: string, entityId: string) => {
  switch (entityType) {
    case 'Lead':
      return `/crm/leads?leadId=${entityId}`;
    case 'Opportunity':
      return `/crm/opportunities?opportunityId=${entityId}`;
    case 'Quote':
      return `/crm/quotes?quoteId=${entityId}`;
    case 'SalesOrder':
      return `/sales/orders/${entityId}`;
    case 'Contract':
      return `/procurement/contracts/${entityId}`;
    case 'Project':
      return `/development/projects/${entityId}`;
    default:
      return null;
  }
};

const toOptionalString = (value?: string) => value?.trim() ? value.trim() : undefined;

const buildOpportunityPayload = (form: CreateCrmOpportunityDto): CreateCrmOpportunityDto => ({
  name: form.name.trim(),
  description: toOptionalString(form.description),
  businessPartnerId: form.businessPartnerId || undefined,
  leadId: form.leadId || undefined,
  stage: form.stage,
  probability: Number(form.probability) || 0,
  amount: Number(form.amount) || 0,
  currency: form.currency.trim().toUpperCase() || 'USD',
  expectedCloseDate: form.expectedCloseDate,
  actualCloseDate: form.actualCloseDate || undefined,
  leadSource: form.leadSource,
  opportunityType: form.opportunityType,
  assignedToId: form.assignedToId || undefined,
  competitors: toOptionalString(form.competitors),
  notes: toOptionalString(form.notes),
  lossReason: toOptionalString(form.lossReason),
});

const mapOpportunityToForm = (opportunity: CrmOpportunityDetailDto): CreateCrmOpportunityDto => ({
  name: opportunity.name,
  description: opportunity.description || '',
  businessPartnerId: opportunity.businessPartnerId || '',
  leadId: opportunity.leadId || '',
  stage: opportunity.stage,
  probability: opportunity.probability,
  amount: opportunity.amount,
  currency: opportunity.currency,
  expectedCloseDate: opportunity.expectedCloseDate.slice(0, 10),
  actualCloseDate: opportunity.actualCloseDate?.slice(0, 10) || '',
  leadSource: opportunity.leadSource || 'Unknown',
  opportunityType: opportunity.opportunityType || 'New Business',
  assignedToId: opportunity.assignedToId,
  competitors: opportunity.competitors || '',
  notes: opportunity.notes || '',
  lossReason: opportunity.lossReason || '',
});

function OpportunityDialog({
  open,
  title,
  description,
  form,
  saving,
  accounts,
  leads,
  onOpenChange,
  onSubmit,
  onChange,
}: {
  open: boolean;
  title: string;
  description: string;
  form: CreateCrmOpportunityDto;
  saving: boolean;
  accounts: BusinessPartnerDto[];
  leads: CrmLeadListItemDto[];
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onChange: <K extends keyof CreateCrmOpportunityDto>(field: K, value: CreateCrmOpportunityDto[K]) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="opportunity-name">Opportunity Name</Label>
            <Input
              id="opportunity-name"
              value={form.name}
              onChange={(event) => onChange('name', event.target.value)}
              placeholder="Atlas corridor expansion"
            />
          </div>

          <div className="space-y-2">
            <Label>CRM Account</Label>
            <Select value={form.businessPartnerId || 'none'} onValueChange={(value) => onChange('businessPartnerId', value === 'none' ? '' : value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select account" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No account yet</SelectItem>
                {accounts.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.partnerName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Lead</Label>
            <Select value={form.leadId || 'none'} onValueChange={(value) => onChange('leadId', value === 'none' ? '' : value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select lead" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No linked lead</SelectItem>
                {leads.map((lead) => (
                  <SelectItem key={lead.leadId} value={lead.leadId}>
                    {lead.fullName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Stage</Label>
            <Select value={form.stage} onValueChange={(value) => onChange('stage', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select stage" />
              </SelectTrigger>
              <SelectContent>
                {OPPORTUNITY_STAGE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Opportunity Type</Label>
            <Select value={form.opportunityType} onValueChange={(value) => onChange('opportunityType', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select type" />
              </SelectTrigger>
              <SelectContent>
                {OPPORTUNITY_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="opportunity-amount">Amount</Label>
            <Input
              id="opportunity-amount"
              type="number"
              min={0}
              value={form.amount}
              onChange={(event) => onChange('amount', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="opportunity-currency">Currency</Label>
            <Input
              id="opportunity-currency"
              value={form.currency}
              onChange={(event) => onChange('currency', event.target.value.toUpperCase())}
              maxLength={3}
              placeholder="USD"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="opportunity-probability">Probability</Label>
            <Input
              id="opportunity-probability"
              type="number"
              min={0}
              max={100}
              value={form.probability}
              onChange={(event) => onChange('probability', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label>Lead Source</Label>
            <Select value={form.leadSource} onValueChange={(value) => onChange('leadSource', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select source" />
              </SelectTrigger>
              <SelectContent>
                {LEAD_SOURCE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="opportunity-expected-close">Expected Close</Label>
            <Input
              id="opportunity-expected-close"
              type="date"
              value={form.expectedCloseDate}
              onChange={(event) => onChange('expectedCloseDate', event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="opportunity-actual-close">Actual Close</Label>
            <Input
              id="opportunity-actual-close"
              type="date"
              value={form.actualCloseDate || ''}
              onChange={(event) => onChange('actualCloseDate', event.target.value)}
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="opportunity-description">Description</Label>
            <Textarea
              id="opportunity-description"
              value={form.description || ''}
              onChange={(event) => onChange('description', event.target.value)}
              placeholder="Capture scope, value drivers, and buyer context."
              rows={3}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="opportunity-competitors">Competitors</Label>
            <Input
              id="opportunity-competitors"
              value={form.competitors || ''}
              onChange={(event) => onChange('competitors', event.target.value)}
              placeholder="Contoso, Fabrikam"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="opportunity-loss-reason">Loss Reason</Label>
            <Input
              id="opportunity-loss-reason"
              value={form.lossReason || ''}
              onChange={(event) => onChange('lossReason', event.target.value)}
              placeholder="Required only when closed lost"
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="opportunity-notes">Notes</Label>
            <Textarea
              id="opportunity-notes"
              value={form.notes || ''}
              onChange={(event) => onChange('notes', event.target.value)}
              placeholder="Call notes, commercial blockers, and next steps."
              rows={4}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={onSubmit} disabled={saving}>
            {saving ? 'Saving...' : 'Save Opportunity'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function CrmOpportunitiesPage() {
  const searchParams = useSearchParams();
  const resolvedSearchParams = searchParams ?? new URLSearchParams();
  const scopedBusinessPartnerId = resolvedSearchParams.get('businessPartnerId') || '';
  const scopedLeadId = resolvedSearchParams.get('leadId') || '';
  const requestedOpportunityId = resolvedSearchParams.get('opportunityId') || '';

  const [search, setSearch] = useState(resolvedSearchParams.get('search') || '');
  const [stage, setStage] = useState(resolvedSearchParams.get('stage') || 'all');
  const [opportunityType, setOpportunityType] = useState(resolvedSearchParams.get('opportunityType') || 'all');
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmOpportunityListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedOpportunityId, setSelectedOpportunityId] = useState(requestedOpportunityId);
  const [selectedOpportunity, setSelectedOpportunity] = useState<CrmOpportunityDetailDto | null>(null);
  const requestedIdRef = useRef(requestedOpportunityId);
  requestedIdRef.current = requestedOpportunityId;
  const detailRequest = useRef(0);

  useEffect(() => {
    if (requestedOpportunityId) {
      setSelectedOpportunityId(requestedOpportunityId);
      setPage(1);
    }
  }, [requestedOpportunityId]);


  const [accounts, setAccounts] = useState<BusinessPartnerDto[]>([]);
  const [leads, setLeads] = useState<CrmLeadListItemDto[]>([]);

  const [formOpen, setFormOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [form, setForm] = useState<CreateCrmOpportunityDto>(createEmptyOpportunityForm(scopedBusinessPartnerId, scopedLeadId));
  const [saving, setSaving] = useState(false);
  const [newRequestHandled, setNewRequestHandled] = useState(false);

  const loadLookups = async () => {
    try {
      const [partnerData, leadData] = await Promise.all([
        businessPartnerService.getAllPartnersForDropdown(),
        crmService.getLeads({ page: 1, pageSize: 100 }),
      ]);

      const filteredAccounts = partnerData
        .filter((partner) => partner.status !== 'Inactive')
        .filter((partner) => hasCustomerRole(partner.partnerType) || !!partner.customerType)
        .sort((left, right) => left.partnerName.localeCompare(right.partnerName));

      setAccounts(filteredAccounts);
      setLeads(leadData.items);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM opportunity lookups'));
    }
  };

  const loadOpportunities = async (requestedPage: number = page) => {
    const requestedIdAtLoad = requestedIdRef.current;
    try {
      setLoading(true);
      const data = await crmService.getOpportunities({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        stage: stage === 'all' ? undefined : stage,
        businessPartnerId: scopedBusinessPartnerId || undefined,
        leadId: scopedLeadId || undefined,
        opportunityType: opportunityType === 'all' ? undefined : opportunityType,
      });

      if (requestedIdAtLoad !== requestedIdRef.current) return;
      setResult(data);

      if (requestedOpportunityId && requestedPage === 1) {
        setSelectedOpportunityId(requestedOpportunityId);
        return;
      }

      if (selectedOpportunityId && data.items.some((item) => item.opportunityId === selectedOpportunityId)) {
        return;
      }

      setSelectedOpportunityId(data.items[0]?.opportunityId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM opportunities'));
    } finally {
      setLoading(false);
    }
  };

  const loadOpportunityDetail = async (opportunityId: string) => {
    const request = ++detailRequest.current;
    setSelectedOpportunity(null);
    if (!opportunityId) {
      setDetailLoading(false);
      return;
    }

    try {
      setDetailLoading(true);
      const detail = await crmService.getOpportunity(opportunityId);
      if (request === detailRequest.current) setSelectedOpportunity(detail);
    } catch (error: unknown) {
      if (request !== detailRequest.current) return;
      toast.error(getMessage(error, 'Failed to load CRM opportunity detail'));
      setSelectedOpportunity(null);
    } finally {
      if (request === detailRequest.current) setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadLookups();
  }, []);

  useEffect(() => {
    void loadOpportunities(page);
  }, [page, stage, scopedBusinessPartnerId, scopedLeadId, opportunityType]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadOpportunities(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadOpportunityDetail(selectedOpportunityId);
    return () => { detailRequest.current++; };
  }, [selectedOpportunityId]);

  useEffect(() => {
    if (newRequestHandled || resolvedSearchParams.get('new') !== '1') {
      return;
    }

    setFormMode('create');
    setForm(createEmptyOpportunityForm(
      scopedBusinessPartnerId,
      scopedLeadId,
      resolvedSearchParams.get('opportunityType') || undefined,
    ));
    setFormOpen(true);
    setNewRequestHandled(true);
  }, [newRequestHandled, resolvedSearchParams, scopedBusinessPartnerId, scopedLeadId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];
    const weightedValue = items.reduce((sum, item) => sum + item.weightedValue, 0);

    return [
      {
        label: 'Visible Opportunities',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'Filtered pipeline rows',
        icon: BriefcaseBusiness,
      },
      {
        label: 'Weighted Pipeline',
        value: formatMoney(weightedValue),
        hint: 'Current page only',
        icon: TrendingUp,
      },
      {
        label: 'Closing Soon',
        value: items.filter((item) => item.isClosingSoon).length.toLocaleString(),
        hint: 'Expected to close within 30 days',
        icon: CalendarClock,
      },
      {
        label: 'Active Accounts',
        value: new Set(items.map((item) => item.businessPartnerId).filter(Boolean)).size.toLocaleString(),
        hint: 'Distinct account coverage on this page',
        icon: Target,
      },
    ];
  }, [result]);

  const scopedAccountName = accounts.find((account) => account.id === scopedBusinessPartnerId)?.partnerName;
  const scopedLeadName = leads.find((lead) => lead.leadId === scopedLeadId)?.fullName;

  const openCreateDialog = () => {
    setFormMode('create');
    setForm(createEmptyOpportunityForm(
      scopedBusinessPartnerId,
      scopedLeadId,
      opportunityType !== 'all' ? opportunityType : undefined,
    ));
    setFormOpen(true);
  };

  const openEditDialog = async (opportunityId?: string) => {
    const targetOpportunityId = opportunityId || selectedOpportunityId;
    if (!targetOpportunityId) {
      return;
    }

    try {
      const detail = targetOpportunityId === selectedOpportunity?.opportunityId && selectedOpportunity
        ? selectedOpportunity
        : await crmService.getOpportunity(targetOpportunityId);

      setSelectedOpportunity(detail);
      setSelectedOpportunityId(detail.opportunityId);
      setFormMode('edit');
      setForm(mapOpportunityToForm(detail));
      setFormOpen(true);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM opportunity for editing'));
    }
  };

  const submitOpportunity = async () => {
    if (!form.name.trim()) {
      toast.error('Opportunity name is required.');
      return;
    }

    try {
      setSaving(true);
      const payload = buildOpportunityPayload(form);
      const opportunity = formMode === 'create'
        ? await crmService.createOpportunity(payload)
        : await crmService.updateOpportunity(selectedOpportunityId, payload);

      toast.success(formMode === 'create' ? 'Opportunity created.' : 'Opportunity updated.');
      setFormOpen(false);
      setSelectedOpportunityId(opportunity.opportunityId);
      setPage(1);
      await loadOpportunities(1);
      await loadOpportunityDetail(opportunity.opportunityId);
    } catch (error: unknown) {
      toast.error(getMessage(error, `Failed to ${formMode} CRM opportunity`));
    } finally {
      setSaving(false);
    }
  };

  const deleteOpportunity = async () => {
    if (!selectedOpportunityId) {
      return;
    }

    try {
      setSaving(true);
      await crmService.deleteOpportunity(selectedOpportunityId);
      toast.success('Opportunity deleted.');
      setDeleteOpen(false);
      setSelectedOpportunity(null);
      setSelectedOpportunityId('');
      setPage(1);
      await loadOpportunities(1);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to delete CRM opportunity'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Opportunities</h1>
          <p className="text-muted-foreground">
            Manage deal flow while keeping accounts anchored on BusinessPartner and leads anchored on the existing sales model.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/renewals">Renewals</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/conversions">Conversions</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadOpportunities(page)}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={openCreateDialog}>
            <Plus className="mr-2 h-4 w-4" />
            New Opportunity
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {metrics.map((metric) => {
          const Icon = metric.icon;

          return (
            <Card key={metric.label}>
              <CardHeader className="pb-2">
                <CardDescription className="flex items-center gap-2">
                  <Icon className="h-4 w-4" />
                  {metric.label}
                </CardDescription>
                <CardTitle>{metric.value}</CardTitle>
              </CardHeader>
              <CardContent className="pt-0 text-xs text-muted-foreground">{metric.hint}</CardContent>
            </Card>
          );
        })}
      </div>

      {(scopedBusinessPartnerId || scopedLeadId || opportunityType !== 'all') ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>
              This workspace was opened from a CRM drill-in and is currently filtered to that context.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account: {scopedAccountName || scopedBusinessPartnerId}</Badge> : null}
            {scopedLeadId ? <Badge variant="secondary">Lead: {scopedLeadName || scopedLeadId}</Badge> : null}
            {opportunityType !== 'all' ? <Badge variant="secondary">Type: {opportunityType}</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/opportunities">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search the opportunity list and focus on specific pipeline stages or commercial types.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-[1.4fr_0.8fr_0.8fr]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search name, description, type, or source"
            />
          </div>

          <Select value={stage} onValueChange={(value) => {
            setPage(1);
            setStage(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Filter by stage" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All stages</SelectItem>
              {OPPORTUNITY_STAGE_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={opportunityType} onValueChange={(value) => {
            setPage(1);
            setOpportunityType(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Filter by type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All types</SelectItem>
              {OPPORTUNITY_TYPE_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Pipeline Queue</CardTitle>
            <CardDescription>
              {result ? `${result.totalCount} opportunities matched` : 'Loading opportunity records'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading opportunities...</div> : null}

            {!loading && !(result?.items.length) ? (
              <div className="py-16 text-center text-muted-foreground">No CRM opportunities matched the current filters.</div>
            ) : null}

            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Opportunity</TableHead>
                    <TableHead>Stage</TableHead>
                    <TableHead>Expected Close</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                    <TableHead className="w-[120px] text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((opportunity) => (
                    <TableRow
                      key={opportunity.opportunityId}
                      className={selectedOpportunityId === opportunity.opportunityId ? 'bg-muted/40' : ''}
                      onClick={() => setSelectedOpportunityId(opportunity.opportunityId)}
                    >
                      <TableCell>
                        <div className="font-medium">{opportunity.name}</div>
                        <div className="text-xs text-muted-foreground">
                          {opportunity.businessPartnerName || 'Unassigned account'}{opportunity.leadName ? ` | ${opportunity.leadName}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={opportunity.stage === 'Closed Won' ? 'default' : opportunity.stage === 'Closed Lost' ? 'destructive' : 'outline'}>
                            {opportunity.stage}
                          </Badge>
                          {opportunity.isClosingSoon ? <Badge variant="secondary">Closing Soon</Badge> : null}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(opportunity.expectedCloseDate)}</TableCell>
                      <TableCell className="text-right">
                        <div>{formatMoney(opportunity.amount, opportunity.currency)}</div>
                        <div className="text-xs text-muted-foreground">
                          {opportunity.probability}% | {formatMoney(opportunity.weightedValue, opportunity.currency)}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={(event) => {
                              event.stopPropagation();
                              void openEditDialog(opportunity.opportunityId);
                            }}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={(event) => {
                              event.stopPropagation();
                              setSelectedOpportunityId(opportunity.opportunityId);
                              setDeleteOpen(true);
                            }}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : null}

            <div className="flex items-center justify-between gap-3">
              <div className="text-sm text-muted-foreground">
                Page {result?.page || 1} of {Math.max(result?.totalPages || 1, 1)}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  disabled={!result || result.page <= 1 || loading}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  disabled={!result || result.page >= result.totalPages || loading}
                  onClick={() => setPage((current) => current + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Opportunity Detail</CardTitle>
            <CardDescription>Inspect account, lead, commercial notes, and close timing for the selected deal.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading opportunity detail...</div> : null}

            {!detailLoading && !selectedOpportunity ? (
              <div className="py-16 text-center text-muted-foreground">Select an opportunity to inspect and manage it.</div>
            ) : null}

            {!detailLoading && selectedOpportunity ? (
              <>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="text-xl font-semibold">{selectedOpportunity.name}</div>
                    <div className="text-sm text-muted-foreground">
                      {selectedOpportunity.businessPartnerName || 'Unassigned account'}{selectedOpportunity.leadName ? ` | ${selectedOpportunity.leadName}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge
                      variant={selectedOpportunity.stage === 'Closed Won'
                        ? 'default'
                        : selectedOpportunity.stage === 'Closed Lost'
                          ? 'destructive'
                          : 'outline'}
                    >
                      {selectedOpportunity.stage}
                    </Badge>
                    {selectedOpportunity.isClosingSoon ? <Badge variant="secondary">Closing Soon</Badge> : null}
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Commercial</div>
                    <div className="mt-2 space-y-2">
                      <div>{formatMoney(selectedOpportunity.amount, selectedOpportunity.currency)}</div>
                      <div>{selectedOpportunity.probability}% probability</div>
                      <div>{selectedOpportunity.opportunityType}</div>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Timing</div>
                    <div className="mt-2 space-y-2">
                      <div>Expected Close: {formatDate(selectedOpportunity.expectedCloseDate)}</div>
                      <div>Actual Close: {formatDate(selectedOpportunity.actualCloseDate)}</div>
                      <div>Created: {formatDate(selectedOpportunity.createdAt)}</div>
                    </div>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" onClick={() => void openEditDialog()}>
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit Opportunity
                  </Button>
                  <Button variant="outline" onClick={() => setDeleteOpen(true)}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Delete
                  </Button>
                  {selectedOpportunity.businessPartnerId ? (
                    <Button asChild>
                      <Link href={`/crm/accounts/${selectedOpportunity.businessPartnerId}`}>
                        Open Account
                      </Link>
                    </Button>
                  ) : null}
                  <Button asChild variant="outline">
                    <Link href={`/crm/activities?opportunityId=${selectedOpportunity.opportunityId}`}>
                      View Activities
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/quotes?opportunityId=${selectedOpportunity.opportunityId}`}>
                      View Quotes
                    </Link>
                  </Button>
                  <SalesHandoffActions
                    context={{
                      businessPartnerId: selectedOpportunity.businessPartnerId,
                      businessPartnerName: selectedOpportunity.businessPartnerName,
                      leadId: selectedOpportunity.leadId,
                      leadName: selectedOpportunity.leadName,
                      opportunityId: selectedOpportunity.opportunityId,
                      opportunityName: selectedOpportunity.name,
                      currency: selectedOpportunity.currency,
                      estimatedValue: selectedOpportunity.amount,
                      contextLabel: 'Opportunity',
                    }}
                  />
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Description</div>
                    <div className="text-muted-foreground">{selectedOpportunity.description || 'No description provided.'}</div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Competition</div>
                    <div className="text-muted-foreground">{selectedOpportunity.competitors || 'No competitor notes recorded.'}</div>
                  </div>
                </div>

                {selectedOpportunity.notes ? (
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Notes</div>
                    <div className="text-muted-foreground">{selectedOpportunity.notes}</div>
                  </div>
                ) : null}

                {selectedOpportunity.lossReason ? (
                  <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                    <div className="mb-2 font-medium">Loss Reason</div>
                    <div>{selectedOpportunity.lossReason}</div>
                  </div>
                ) : null}

                <div className="grid gap-3 lg:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="mb-3 flex items-center justify-between gap-3">
                      <div className="font-medium">Quotes</div>
                      <Badge variant="outline">{selectedOpportunity.quotes.length}</Badge>
                    </div>
                    {!selectedOpportunity.quotes.length ? (
                      <div className="text-sm text-muted-foreground">No quotes are linked to this opportunity yet.</div>
                    ) : (
                      <div className="space-y-3">
                        {selectedOpportunity.quotes.map((quote) => (
                          <div key={quote.quoteId} className="rounded-lg border bg-muted/20 p-3">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">
                                  <Link href={`/crm/quotes?opportunityId=${selectedOpportunity.opportunityId}&quoteId=${quote.quoteId}`} className="hover:underline">
                                    {quote.quoteName}
                                  </Link>
                                </div>
                                <div className="text-xs text-muted-foreground">
                                  {quote.documentNumber} | {formatDate(quote.validUntil)}
                                </div>
                              </div>
                              <div className="flex flex-wrap gap-2">
                                <Badge variant={quote.isAccepted ? 'default' : 'outline'}>{quote.quoteStatus}</Badge>
                                {quote.isExpiringSoon ? <Badge variant="secondary">Expiring Soon</Badge> : null}
                              </div>
                            </div>
                            <div className="mt-2 text-sm text-muted-foreground">
                              {formatMoney(quote.value, quote.currency)}
                              {quote.leadName ? ` | ${quote.leadName}` : ''}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  <div className="rounded-lg border p-4">
                    <div className="mb-3 flex items-center justify-between gap-3">
                      <div className="font-medium">Conversion Chain</div>
                      <Badge variant="outline">{selectedOpportunity.conversionChain.nodes.length} steps</Badge>
                    </div>
                    {!selectedOpportunity.conversionChain.nodes.length ? (
                      <div className="text-sm text-muted-foreground">No conversion chain has been assembled yet.</div>
                    ) : (
                      <div className="space-y-3">
                        {selectedOpportunity.conversionChain.nodes.map((node) => {
                          const href = resolveChainHref(node.entityType, node.entityId);

                          return (
                            <div key={`${node.entityType}-${node.entityId}`} className="rounded-lg border bg-muted/20 p-3">
                              <div className="flex items-start justify-between gap-3">
                                <div>
                                  <div className="text-xs uppercase tracking-wide text-muted-foreground">
                                    {node.stage} | {node.relationshipType}
                                  </div>
                                  <div className="mt-1 font-medium">
                                    {href ? <Link href={href} className="hover:underline">{node.title}</Link> : node.title}
                                  </div>
                                  <div className="text-sm text-muted-foreground">
                                    {node.status}
                                    {node.referenceCode ? ` | ${node.referenceCode}` : ''}
                                  </div>
                                </div>
                                {node.amount !== undefined ? (
                                  <div className="text-right text-sm">
                                    <div className="font-medium">{formatMoney(node.amount, node.currency || selectedOpportunity.currency)}</div>
                                    <div className="text-muted-foreground">{formatDate(node.referenceDate)}</div>
                                  </div>
                                ) : null}
                              </div>
                              {node.relationshipNote ? (
                                <div className="mt-2 text-sm text-muted-foreground">{node.relationshipNote}</div>
                              ) : null}
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                </div>

                <div className="grid gap-3 lg:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="mb-3 flex items-center justify-between gap-3">
                      <div className="font-medium">Related Contracts</div>
                      <Badge variant="outline">{selectedOpportunity.relatedContracts.length}</Badge>
                    </div>
                    {!selectedOpportunity.relatedContracts.length ? (
                      <div className="text-sm text-muted-foreground">No related contracts are linked yet.</div>
                    ) : (
                      <div className="space-y-3">
                        {selectedOpportunity.relatedContracts.map((contract) => (
                          <div key={contract.contractId} className="rounded-lg border bg-muted/20 p-3">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">
                                  <Link href={`/procurement/contracts/${contract.contractId}`} className="hover:underline">
                                    {contract.contractTitle}
                                  </Link>
                                </div>
                                <div className="text-xs text-muted-foreground">
                                  {contract.contractNumber} | {contract.relationshipType}
                                </div>
                              </div>
                              <Badge variant="outline">{contract.status}</Badge>
                            </div>
                            <div className="mt-2 text-sm text-muted-foreground">
                              {formatMoney(contract.contractValue, selectedOpportunity.currency)} | Ends {formatDate(contract.endDate)}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  <div className="rounded-lg border p-4">
                    <div className="mb-3 flex items-center justify-between gap-3">
                      <div className="font-medium">Related Projects</div>
                      <Badge variant="outline">{selectedOpportunity.relatedProjects.length}</Badge>
                    </div>
                    {!selectedOpportunity.relatedProjects.length ? (
                      <div className="text-sm text-muted-foreground">No related projects are linked yet.</div>
                    ) : (
                      <div className="space-y-3">
                        {selectedOpportunity.relatedProjects.map((project) => (
                          <div key={project.projectId} className="rounded-lg border bg-muted/20 p-3">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">
                                  <Link href={`/development/projects/${project.projectId}`} className="hover:underline">
                                    {project.title}
                                  </Link>
                                </div>
                                <div className="text-xs text-muted-foreground">
                                  {project.projectCode} | {project.relationshipType}
                                </div>
                              </div>
                              <Badge variant="outline">{project.status}</Badge>
                            </div>
                            <div className="mt-2 text-sm text-muted-foreground">
                              {formatMoney(project.value, selectedOpportunity.currency)} | {project.progressPercent.toFixed(0)}% complete
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <OpportunityDialog
        open={formOpen}
        title={formMode === 'create' ? 'Create CRM Opportunity' : 'Edit CRM Opportunity'}
        description="Link pipeline work to existing leads and BusinessPartner-backed accounts without duplicating core ERP data."
        form={form}
        saving={saving}
        accounts={accounts}
        leads={leads}
        onOpenChange={setFormOpen}
        onSubmit={() => void submitOpportunity()}
        onChange={(field, value) => setForm((current) => ({ ...current, [field]: value }))}
      />

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete CRM Opportunity</DialogTitle>
            <DialogDescription>
              This removes the opportunity record. Opportunities with linked quotes cannot be deleted.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={() => void deleteOpportunity()} disabled={saving}>
              {saving ? 'Deleting...' : 'Delete Opportunity'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
