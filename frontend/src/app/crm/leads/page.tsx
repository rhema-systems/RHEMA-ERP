'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
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
  type CreateCrmLeadDto,
  type CrmLeadDetailDto,
  type CrmLeadListItemDto,
  type PagedResult,
} from '@/services/crmService';
import { SalesHandoffActions } from '../components/SalesHandoffActions';
import {
  CalendarClock,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Target,
  Trash2,
  TrendingUp,
  UserRound,
} from 'lucide-react';
import { toast } from 'sonner';

const LEAD_STATUS_OPTIONS = ['New', 'Contacted', 'Qualified', 'Unqualified', 'Converted'];
const LEAD_SOURCE_OPTIONS = ['Referral', 'Website', 'Campaign', 'Tender', 'Partner', 'Direct', 'Unknown'];

const emptyLeadForm = (): CreateCrmLeadDto => ({
  firstName: '',
  lastName: '',
  companyName: '',
  jobTitle: '',
  email: '',
  phone: '',
  mobile: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  state: '',
  postalCode: '',
  country: '',
  leadSource: 'Referral',
  leadStatus: 'New',
  qualificationScore: 0,
  estimatedValue: 0,
  lastContactDate: '',
  nextFollowUpDate: '',
  assignedToId: undefined,
  notes: '',
});

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';

const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;

const toOptionalString = (value?: string) => value?.trim() ? value.trim() : undefined;

const buildLeadPayload = (form: CreateCrmLeadDto): CreateCrmLeadDto => ({
  firstName: form.firstName.trim(),
  lastName: form.lastName.trim(),
  companyName: toOptionalString(form.companyName),
  jobTitle: toOptionalString(form.jobTitle),
  email: toOptionalString(form.email),
  phone: toOptionalString(form.phone),
  mobile: toOptionalString(form.mobile),
  addressLine1: toOptionalString(form.addressLine1),
  addressLine2: toOptionalString(form.addressLine2),
  city: toOptionalString(form.city),
  state: toOptionalString(form.state),
  postalCode: toOptionalString(form.postalCode),
  country: toOptionalString(form.country),
  leadSource: form.leadSource,
  leadStatus: form.leadStatus,
  qualificationScore: Number(form.qualificationScore) || 0,
  estimatedValue: Number(form.estimatedValue) || 0,
  lastContactDate: form.lastContactDate || undefined,
  nextFollowUpDate: form.nextFollowUpDate || undefined,
  assignedToId: form.assignedToId || undefined,
  notes: toOptionalString(form.notes),
});

const mapLeadToForm = (lead: CrmLeadDetailDto): CreateCrmLeadDto => ({
  firstName: lead.firstName,
  lastName: lead.lastName,
  companyName: lead.companyName || '',
  jobTitle: lead.jobTitle || '',
  email: lead.email || '',
  phone: lead.phone || '',
  mobile: lead.mobile || '',
  addressLine1: lead.addressLine1 || '',
  addressLine2: lead.addressLine2 || '',
  city: lead.city || '',
  state: lead.state || '',
  postalCode: lead.postalCode || '',
  country: lead.country || '',
  leadSource: lead.leadSource || 'Unknown',
  leadStatus: lead.leadStatus || 'New',
  qualificationScore: lead.qualificationScore,
  estimatedValue: lead.estimatedValue,
  lastContactDate: lead.lastContactDate?.slice(0, 10) || '',
  nextFollowUpDate: lead.nextFollowUpDate?.slice(0, 10) || '',
  assignedToId: lead.assignedToId,
  notes: lead.notes || '',
});

function LeadDialog({
  open,
  title,
  description,
  form,
  saving,
  onOpenChange,
  onSubmit,
  onChange,
}: {
  open: boolean;
  title: string;
  description: string;
  form: CreateCrmLeadDto;
  saving: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onChange: <K extends keyof CreateCrmLeadDto>(field: K, value: CreateCrmLeadDto[K]) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="lead-first-name">First Name</Label>
            <Input
              id="lead-first-name"
              value={form.firstName}
              onChange={(event) => onChange('firstName', event.target.value)}
              placeholder="Ama"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-last-name">Last Name</Label>
            <Input
              id="lead-last-name"
              value={form.lastName}
              onChange={(event) => onChange('lastName', event.target.value)}
              placeholder="Mensah"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-company">Company</Label>
            <Input
              id="lead-company"
              value={form.companyName || ''}
              onChange={(event) => onChange('companyName', event.target.value)}
              placeholder="Atlas Infrastructure"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-job-title">Job Title</Label>
            <Input
              id="lead-job-title"
              value={form.jobTitle || ''}
              onChange={(event) => onChange('jobTitle', event.target.value)}
              placeholder="Commercial Manager"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-email">Email</Label>
            <Input
              id="lead-email"
              type="email"
              value={form.email || ''}
              onChange={(event) => onChange('email', event.target.value)}
              placeholder="ama@atlas.example"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-phone">Phone</Label>
            <Input
              id="lead-phone"
              value={form.phone || ''}
              onChange={(event) => onChange('phone', event.target.value)}
              placeholder="+1 555 0100"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-mobile">Mobile</Label>
            <Input
              id="lead-mobile"
              value={form.mobile || ''}
              onChange={(event) => onChange('mobile', event.target.value)}
              placeholder="+1 555 0101"
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
            <Label>Lead Status</Label>
            <Select value={form.leadStatus} onValueChange={(value) => onChange('leadStatus', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select status" />
              </SelectTrigger>
              <SelectContent>
                {LEAD_STATUS_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-score">Qualification Score</Label>
            <Input
              id="lead-score"
              type="number"
              min={0}
              max={100}
              value={form.qualificationScore}
              onChange={(event) => onChange('qualificationScore', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-estimated-value">Estimated Value</Label>
            <Input
              id="lead-estimated-value"
              type="number"
              min={0}
              value={form.estimatedValue}
              onChange={(event) => onChange('estimatedValue', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-last-contact">Last Contact</Label>
            <Input
              id="lead-last-contact"
              type="date"
              value={form.lastContactDate || ''}
              onChange={(event) => onChange('lastContactDate', event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-next-follow-up">Next Follow-Up</Label>
            <Input
              id="lead-next-follow-up"
              type="date"
              value={form.nextFollowUpDate || ''}
              onChange={(event) => onChange('nextFollowUpDate', event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-city">City</Label>
            <Input
              id="lead-city"
              value={form.city || ''}
              onChange={(event) => onChange('city', event.target.value)}
              placeholder="Accra"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lead-country">Country</Label>
            <Input
              id="lead-country"
              value={form.country || ''}
              onChange={(event) => onChange('country', event.target.value)}
              placeholder="Ghana"
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="lead-address">Address</Label>
            <Input
              id="lead-address"
              value={form.addressLine1 || ''}
              onChange={(event) => onChange('addressLine1', event.target.value)}
              placeholder="Airport Residential Area"
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="lead-notes">Notes</Label>
            <Textarea
              id="lead-notes"
              value={form.notes || ''}
              onChange={(event) => onChange('notes', event.target.value)}
              placeholder="Capture qualification context, objections, and next steps."
              rows={4}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={onSubmit} disabled={saving}>
            {saving ? 'Saving...' : 'Save Lead'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function CrmLeadsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [status, setStatus] = useState(searchParams.get('status') || 'all');
  const [followUpOnly, setFollowUpOnly] = useState(searchParams.get('followUpOnly') === 'true');
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmLeadListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedLeadId, setSelectedLeadId] = useState(searchParams.get('leadId') || '');
  const [selectedLead, setSelectedLead] = useState<CrmLeadDetailDto | null>(null);

  const [formOpen, setFormOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [form, setForm] = useState<CreateCrmLeadDto>(emptyLeadForm());
  const [saving, setSaving] = useState(false);

  const loadLeads = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getLeads({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        followUpOnly,
      });

      setResult(data);

      const requestedLeadId = searchParams.get('leadId');
      if (requestedLeadId && requestedPage === 1) {
        setSelectedLeadId(requestedLeadId);
        return;
      }

      if (selectedLeadId && data.items.some((item) => item.leadId === selectedLeadId)) {
        return;
      }

      setSelectedLeadId(data.items[0]?.leadId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM leads'));
    } finally {
      setLoading(false);
    }
  };

  const loadLeadDetail = async (leadId: string) => {
    if (!leadId) {
      setSelectedLead(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedLead(await crmService.getLead(leadId));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM lead detail'));
      setSelectedLead(null);
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    void loadLeads(page);
  }, [page, status, followUpOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadLeads(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadLeadDetail(selectedLeadId);
  }, [selectedLeadId]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Leads',
        value: result?.totalCount.toLocaleString() || '0',
        hint: `${items.filter((item) => item.leadStatus === 'Qualified').length} qualified on this page`,
        icon: UserRound,
      },
      {
        label: 'Follow-Up Queue',
        value: items.filter((item) => item.needsFollowUp).length.toLocaleString(),
        hint: 'Leads needing action soon',
        icon: CalendarClock,
      },
      {
        label: 'Qualified Value',
        value: formatMoney(
          items.filter((item) => item.leadStatus === 'Qualified').reduce((sum, item) => sum + item.estimatedValue, 0),
        ),
        hint: 'Current page only',
        icon: TrendingUp,
      },
      {
        label: 'Opportunity Attach',
        value: items.reduce((sum, item) => sum + item.opportunityCount, 0).toLocaleString(),
        hint: 'Linked opportunities on this page',
        icon: Target,
      },
    ];
  }, [result]);

  const openCreateDialog = () => {
    setFormMode('create');
    setForm(emptyLeadForm());
    setFormOpen(true);
  };

  const openEditDialog = async (leadId?: string) => {
    const targetLeadId = leadId || selectedLeadId;
    if (!targetLeadId) {
      return;
    }

    try {
      const detail = targetLeadId === selectedLead?.leadId && selectedLead
        ? selectedLead
        : await crmService.getLead(targetLeadId);

      setSelectedLead(detail);
      setSelectedLeadId(detail.leadId);
      setFormMode('edit');
      setForm(mapLeadToForm(detail));
      setFormOpen(true);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM lead for editing'));
    }
  };

  const submitLead = async () => {
    if (!form.firstName.trim() || !form.lastName.trim()) {
      toast.error('First name and last name are required.');
      return;
    }

    try {
      setSaving(true);
      const payload = buildLeadPayload(form);
      const lead = formMode === 'create'
        ? await crmService.createLead(payload)
        : await crmService.updateLead(selectedLeadId, payload);

      toast.success(formMode === 'create' ? 'Lead created.' : 'Lead updated.');
      setFormOpen(false);
      setSelectedLeadId(lead.leadId);
      setPage(1);
      await loadLeads(1);
      await loadLeadDetail(lead.leadId);
    } catch (error: unknown) {
      toast.error(getMessage(error, `Failed to ${formMode} CRM lead`));
    } finally {
      setSaving(false);
    }
  };

  const deleteLead = async () => {
    if (!selectedLeadId) {
      return;
    }

    try {
      setSaving(true);
      await crmService.deleteLead(selectedLeadId);
      toast.success('Lead deleted.');
      setDeleteOpen(false);
      setSelectedLead(null);
      setSelectedLeadId('');
      setPage(1);
      await loadLeads(1);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to delete CRM lead'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Leads</h1>
          <p className="text-muted-foreground">
            Manage lead intake and qualification directly on top of the ERP-native CRM overview slice.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm">Back to Overview</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadLeads(page)}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={openCreateDialog}>
            <Plus className="mr-2 h-4 w-4" />
            New Lead
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

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Shape the lead queue by qualification status, search, and follow-up urgency.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-[1.6fr_0.8fr_auto]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search lead, company, email, or phone"
            />
          </div>

          <Select value={status} onValueChange={(value) => {
            setPage(1);
            setStatus(value);
          }}>
            <SelectTrigger>
              <SelectValue placeholder="Filter by status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {LEAD_STATUS_OPTIONS.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            variant={followUpOnly ? 'default' : 'outline'}
            onClick={() => {
              setPage(1);
              setFollowUpOnly((current) => !current);
            }}
          >
            {followUpOnly ? 'Showing follow-up only' : 'Follow-up only'}
          </Button>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Lead Queue</CardTitle>
            <CardDescription>
              {result ? `${result.totalCount} lead records matched` : 'Loading lead records'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading leads...</div> : null}

            {!loading && !(result?.items.length) ? (
              <div className="py-16 text-center text-muted-foreground">No CRM leads matched the current filters.</div>
            ) : null}

            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Lead</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Next Follow-Up</TableHead>
                    <TableHead className="text-right">Value</TableHead>
                    <TableHead className="w-[120px] text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((lead) => (
                    <TableRow
                      key={lead.leadId}
                      className={selectedLeadId === lead.leadId ? 'bg-muted/40' : ''}
                      onClick={() => setSelectedLeadId(lead.leadId)}
                    >
                      <TableCell>
                        <div className="font-medium">{lead.fullName}</div>
                        <div className="text-xs text-muted-foreground">
                          {lead.companyName || 'No company'}{lead.jobTitle ? ` | ${lead.jobTitle}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={lead.leadStatus === 'Qualified' ? 'default' : 'outline'}>
                            {lead.leadStatus}
                          </Badge>
                          {lead.needsFollowUp ? <Badge variant="secondary">Follow-Up</Badge> : null}
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(lead.nextFollowUpDate)}</TableCell>
                      <TableCell className="text-right">
                        <div>{formatMoney(lead.estimatedValue)}</div>
                        <div className="text-xs text-muted-foreground">{lead.opportunityCount} opportunities</div>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={(event) => {
                              event.stopPropagation();
                              void openEditDialog(lead.leadId);
                            }}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={(event) => {
                              event.stopPropagation();
                              setSelectedLeadId(lead.leadId);
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
            <CardTitle>Lead Detail</CardTitle>
            <CardDescription>
              Qualification context and opportunity chain for the selected lead.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-16 text-center text-muted-foreground">Loading lead detail...</div> : null}

            {!detailLoading && !selectedLead ? (
              <div className="py-16 text-center text-muted-foreground">Select a lead to inspect and manage it.</div>
            ) : null}

            {!detailLoading && selectedLead ? (
              <>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="text-xl font-semibold">{selectedLead.fullName}</div>
                    <div className="text-sm text-muted-foreground">
                      {selectedLead.companyName || 'No company'}{selectedLead.jobTitle ? ` | ${selectedLead.jobTitle}` : ''}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={selectedLead.leadStatus === 'Qualified' ? 'default' : 'outline'}>
                      {selectedLead.leadStatus}
                    </Badge>
                    {selectedLead.needsFollowUp ? <Badge variant="secondary">Needs Follow-Up</Badge> : null}
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Contact</div>
                    <div className="mt-2 space-y-2">
                      <div>{selectedLead.email || 'No email'}</div>
                      <div>{selectedLead.phone || selectedLead.mobile || 'No phone'}</div>
                      <div>{[selectedLead.city, selectedLead.country].filter(Boolean).join(', ') || 'No location'}</div>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Qualification</div>
                    <div className="mt-2 space-y-2">
                      <div>Score: {selectedLead.qualificationScore}/100</div>
                      <div>Estimated Value: {formatMoney(selectedLead.estimatedValue)}</div>
                      <div>Next Follow-Up: {formatDate(selectedLead.nextFollowUpDate)}</div>
                    </div>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" onClick={() => void openEditDialog()}>
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit Lead
                  </Button>
                  <Button variant="outline" onClick={() => setDeleteOpen(true)}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Delete
                  </Button>
                  <Button asChild>
                    <Link href={`/crm/opportunities?leadId=${selectedLead.leadId}&new=1`}>
                      <Plus className="mr-2 h-4 w-4" />
                      Create Opportunity
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/activities?leadId=${selectedLead.leadId}`}>
                      View Activities
                    </Link>
                  </Button>
                  {selectedLead.convertedBusinessPartnerId ? (
                    <SalesHandoffActions
                      context={{
                        businessPartnerId: selectedLead.convertedBusinessPartnerId,
                        businessPartnerName: selectedLead.companyName || selectedLead.fullName,
                        leadId: selectedLead.leadId,
                        leadName: selectedLead.fullName,
                        currency: 'GHS',
                        estimatedValue: selectedLead.estimatedValue,
                        contextLabel: 'Converted Lead',
                      }}
                      showUnavailableHint={false}
                    />
                  ) : null}
                </div>

                {selectedLead.convertedBusinessPartnerId ? (
                  <div className="rounded-lg border bg-muted/30 p-4 text-sm">
                    <div className="font-medium">Converted Account</div>
                    <div className="mt-1 text-muted-foreground">
                      Converted on {formatDate(selectedLead.convertedDate)}.
                    </div>
                    <Button asChild variant="link" className="mt-2 px-0">
                      <Link href={`/crm/accounts/${selectedLead.convertedBusinessPartnerId}`}>
                        Open CRM account
                      </Link>
                    </Button>
                  </div>
                ) : null}

                {selectedLead.notes ? (
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="mb-2 font-medium">Notes</div>
                    <div className="text-muted-foreground">{selectedLead.notes}</div>
                  </div>
                ) : null}

                <div className="space-y-3">
                  <div className="font-medium">Opportunity Chain</div>
                  {!selectedLead.opportunities.length ? (
                    <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
                      No opportunities are linked to this lead yet.
                    </div>
                  ) : (
                    selectedLead.opportunities.map((opportunity) => (
                      <div key={opportunity.opportunityId} className="rounded-lg border p-4">
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <div className="font-medium">
                              <Link
                                href={`/crm/opportunities?leadId=${selectedLead.leadId}&opportunityId=${opportunity.opportunityId}`}
                                className="hover:underline"
                              >
                                {opportunity.name}
                              </Link>
                            </div>
                            <div className="text-sm text-muted-foreground">
                              {opportunity.businessPartnerName || 'Unassigned account'} | {opportunity.opportunityType}
                            </div>
                          </div>
                          <Badge variant={opportunity.probability >= 70 ? 'default' : 'secondary'}>
                            {opportunity.stage}
                          </Badge>
                        </div>
                        <div className="mt-3 grid gap-2 text-sm text-muted-foreground sm:grid-cols-2">
                          <div>Expected Close: {formatDate(opportunity.expectedCloseDate)}</div>
                          <div>
                            {formatMoney(opportunity.amount, opportunity.currency)} | {opportunity.probability}% win
                          </div>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <LeadDialog
        open={formOpen}
        title={formMode === 'create' ? 'Create CRM Lead' : 'Edit CRM Lead'}
        description="Capture lead details without creating a duplicate customer master record."
        form={form}
        saving={saving}
        onOpenChange={setFormOpen}
        onSubmit={() => void submitLead()}
        onChange={(field, value) => setForm((current) => ({ ...current, [field]: value }))}
      />

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete CRM Lead</DialogTitle>
            <DialogDescription>
              This removes the lead record. Leads with linked opportunities cannot be deleted.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={() => void deleteLead()} disabled={saving}>
              {saving ? 'Deleting...' : 'Delete Lead'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
