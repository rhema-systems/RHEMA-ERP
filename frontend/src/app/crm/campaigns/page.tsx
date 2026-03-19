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
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import {
  crmService,
  type CreateCrmCampaignDto,
  type CreateCrmCampaignMemberDto,
  type CrmCampaignDetailDto,
  type CrmCampaignListItemDto,
  type CrmCampaignMemberDto,
  type CrmLeadListItemDto,
  type PagedResult,
} from '@/services/crmService';
import {
  DollarSign,
  Megaphone,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Target,
  Trash2,
  TrendingUp,
  UserPlus,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';

const CAMPAIGN_STATUS_OPTIONS = ['Planning', 'Active', 'Paused', 'Completed', 'Cancelled'];
const CAMPAIGN_TYPE_OPTIONS = ['Email', 'Social Media', 'Online', 'Event', 'Tender', 'Partner', 'Referral', 'Outbound', 'Other'];
const MEMBER_STATUS_OPTIONS = ['Active', 'Responded', 'Qualified', 'Converted', 'Unsubscribed', 'Bounced'];
const RESPONSE_TYPE_OPTIONS = ['Opened', 'Clicked', 'Replied', 'Meeting Booked', 'Qualified', 'Converted', 'Unsubscribed'];

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatPercent = (value: number) => `${value.toFixed(1)}%`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;
const toOptionalString = (value?: string) => value?.trim() ? value.trim() : undefined;

const emptyCampaignForm = (): CreateCrmCampaignDto => ({
  name: '',
  campaignType: 'Email',
  description: '',
  startDate: new Date().toISOString().slice(0, 10),
  endDate: '',
  campaignStatus: 'Planning',
  budget: 0,
  actualCost: 0,
  expectedRevenue: 0,
  actualRevenue: 0,
  targetAudience: 0,
  actualAudience: 0,
  responseCount: 0,
  leadsGenerated: 0,
  opportunitiesGenerated: 0,
  notes: '',
});

const emptyMemberForm = (leadId?: string): CreateCrmCampaignMemberDto => ({
  leadId: leadId || '',
  memberStatus: 'Active',
  responseDate: '',
  responseType: '',
  notes: '',
});

const mapCampaignToForm = (campaign: CrmCampaignDetailDto): CreateCrmCampaignDto => ({
  name: campaign.name,
  campaignType: campaign.campaignType,
  description: campaign.description || '',
  startDate: campaign.startDate.slice(0, 10),
  endDate: campaign.endDate?.slice(0, 10) || '',
  campaignStatus: campaign.campaignStatus,
  budget: campaign.budget,
  actualCost: campaign.actualCost,
  expectedRevenue: campaign.expectedRevenue,
  actualRevenue: campaign.actualRevenue,
  targetAudience: campaign.targetAudience,
  actualAudience: campaign.actualAudience,
  responseCount: campaign.responseCount,
  leadsGenerated: campaign.leadsGenerated,
  opportunitiesGenerated: campaign.opportunitiesGenerated,
  notes: campaign.notes || '',
});

const mapMemberToForm = (member: CrmCampaignMemberDto): CreateCrmCampaignMemberDto => ({
  leadId: member.leadId,
  memberStatus: member.memberStatus,
  responseDate: member.responseDate?.slice(0, 10) || '',
  responseType: member.responseType || '',
  notes: member.notes || '',
});

const buildCampaignPayload = (form: CreateCrmCampaignDto): CreateCrmCampaignDto => ({
  name: form.name.trim(),
  campaignType: form.campaignType,
  description: toOptionalString(form.description),
  startDate: form.startDate,
  endDate: form.endDate || undefined,
  campaignStatus: form.campaignStatus,
  budget: Number(form.budget) || 0,
  actualCost: Number(form.actualCost) || 0,
  expectedRevenue: Number(form.expectedRevenue) || 0,
  actualRevenue: Number(form.actualRevenue) || 0,
  targetAudience: Number(form.targetAudience) || 0,
  actualAudience: Number(form.actualAudience) || 0,
  responseCount: Number(form.responseCount) || 0,
  leadsGenerated: Number(form.leadsGenerated) || 0,
  opportunitiesGenerated: Number(form.opportunitiesGenerated) || 0,
  notes: toOptionalString(form.notes),
});

const buildMemberPayload = (form: CreateCrmCampaignMemberDto): CreateCrmCampaignMemberDto => ({
  leadId: form.leadId,
  memberStatus: form.memberStatus,
  responseDate: form.responseDate || undefined,
  responseType: form.responseType || undefined,
  notes: toOptionalString(form.notes),
});

function CampaignDialog({
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
  form: CreateCrmCampaignDto;
  saving: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onChange: <K extends keyof CreateCrmCampaignDto>(field: K, value: CreateCrmCampaignDto[K]) => void;
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
            <Label htmlFor="campaign-name">Campaign Name</Label>
            <Input
              id="campaign-name"
              value={form.name}
              onChange={(event) => onChange('name', event.target.value)}
              placeholder="Q3 renewal acceleration"
            />
          </div>

          <div className="space-y-2">
            <Label>Campaign Type</Label>
            <Select value={form.campaignType} onValueChange={(value) => onChange('campaignType', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select type" />
              </SelectTrigger>
              <SelectContent>
                {CAMPAIGN_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Status</Label>
            <Select value={form.campaignStatus} onValueChange={(value) => onChange('campaignStatus', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select status" />
              </SelectTrigger>
              <SelectContent>
                {CAMPAIGN_STATUS_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-start-date">Start Date</Label>
            <Input
              id="campaign-start-date"
              type="date"
              value={form.startDate}
              onChange={(event) => onChange('startDate', event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-end-date">End Date</Label>
            <Input
              id="campaign-end-date"
              type="date"
              value={form.endDate || ''}
              onChange={(event) => onChange('endDate', event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-budget">Budget</Label>
            <Input
              id="campaign-budget"
              type="number"
              min={0}
              value={form.budget}
              onChange={(event) => onChange('budget', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-actual-cost">Actual Cost</Label>
            <Input
              id="campaign-actual-cost"
              type="number"
              min={0}
              value={form.actualCost}
              onChange={(event) => onChange('actualCost', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-expected-revenue">Expected Revenue</Label>
            <Input
              id="campaign-expected-revenue"
              type="number"
              min={0}
              value={form.expectedRevenue}
              onChange={(event) => onChange('expectedRevenue', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-actual-revenue">Actual Revenue</Label>
            <Input
              id="campaign-actual-revenue"
              type="number"
              min={0}
              value={form.actualRevenue}
              onChange={(event) => onChange('actualRevenue', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-target-audience">Target Audience</Label>
            <Input
              id="campaign-target-audience"
              type="number"
              min={0}
              value={form.targetAudience}
              onChange={(event) => onChange('targetAudience', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-actual-audience">Actual Audience</Label>
            <Input
              id="campaign-actual-audience"
              type="number"
              min={0}
              value={form.actualAudience}
              onChange={(event) => onChange('actualAudience', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-response-count">Responses</Label>
            <Input
              id="campaign-response-count"
              type="number"
              min={0}
              value={form.responseCount}
              onChange={(event) => onChange('responseCount', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-leads-generated">Leads Generated</Label>
            <Input
              id="campaign-leads-generated"
              type="number"
              min={0}
              value={form.leadsGenerated}
              onChange={(event) => onChange('leadsGenerated', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-opportunities-generated">Opportunities Generated</Label>
            <Input
              id="campaign-opportunities-generated"
              type="number"
              min={0}
              value={form.opportunitiesGenerated}
              onChange={(event) => onChange('opportunitiesGenerated', Number(event.target.value))}
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="campaign-description">Description</Label>
            <Textarea
              id="campaign-description"
              rows={3}
              value={form.description || ''}
              onChange={(event) => onChange('description', event.target.value)}
              placeholder="High-level commercial objective, audience, and motion."
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="campaign-notes">Notes</Label>
            <Textarea
              id="campaign-notes"
              rows={3}
              value={form.notes || ''}
              onChange={(event) => onChange('notes', event.target.value)}
              placeholder="Execution notes, learning agenda, or commercial context."
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={onSubmit} disabled={saving || !form.name.trim()}>
            {saving ? 'Saving...' : 'Save campaign'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function CampaignMemberDialog({
  open,
  title,
  description,
  form,
  leads,
  saving,
  leadLocked,
  onOpenChange,
  onSubmit,
  onChange,
}: {
  open: boolean;
  title: string;
  description: string;
  form: CreateCrmCampaignMemberDto;
  leads: CrmLeadListItemDto[];
  saving: boolean;
  leadLocked: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onChange: <K extends keyof CreateCrmCampaignMemberDto>(field: K, value: CreateCrmCampaignMemberDto[K]) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            <Label>Lead</Label>
            <Select disabled={leadLocked} value={form.leadId} onValueChange={(value) => onChange('leadId', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select a lead" />
              </SelectTrigger>
              <SelectContent>
                {leads.map((lead) => (
                  <SelectItem key={lead.leadId} value={lead.leadId}>
                    {lead.fullName} {lead.companyName ? `| ${lead.companyName}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Member Status</Label>
            <Select value={form.memberStatus} onValueChange={(value) => onChange('memberStatus', value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select status" />
              </SelectTrigger>
              <SelectContent>
                {MEMBER_STATUS_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="campaign-member-response-date">Response Date</Label>
            <Input
              id="campaign-member-response-date"
              type="date"
              value={form.responseDate || ''}
              onChange={(event) => onChange('responseDate', event.target.value)}
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label>Response Type</Label>
            <Select value={form.responseType || 'none'} onValueChange={(value) => onChange('responseType', value === 'none' ? '' : value)}>
              <SelectTrigger>
                <SelectValue placeholder="Select response type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">None</SelectItem>
                {RESPONSE_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="campaign-member-notes">Notes</Label>
            <Textarea
              id="campaign-member-notes"
              rows={3}
              value={form.notes || ''}
              onChange={(event) => onChange('notes', event.target.value)}
              placeholder="Contact outcome, objection, or handoff note."
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={onSubmit} disabled={saving || !form.leadId}>
            {saving ? 'Saving...' : 'Save member'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function CrmCampaignsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const scopedBusinessPartnerId = searchParams.get('businessPartnerId') || '';
  const scopedLeadId = searchParams.get('leadId') || '';
  const requestedCampaignId = searchParams.get('campaignId') || '';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [status, setStatus] = useState(searchParams.get('status') || 'all');
  const [campaignType, setCampaignType] = useState(searchParams.get('campaignType') || 'all');
  const [activeOnly, setActiveOnly] = useState(searchParams.get('activeOnly') === 'true');
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmCampaignListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedCampaignId, setSelectedCampaignId] = useState(requestedCampaignId);
  const [selectedCampaign, setSelectedCampaign] = useState<CrmCampaignDetailDto | null>(null);

  const [availableLeads, setAvailableLeads] = useState<CrmLeadListItemDto[]>([]);
  const [campaignDialogOpen, setCampaignDialogOpen] = useState(false);
  const [memberDialogOpen, setMemberDialogOpen] = useState(false);
  const [campaignSaving, setCampaignSaving] = useState(false);
  const [memberSaving, setMemberSaving] = useState(false);
  const [editingCampaignId, setEditingCampaignId] = useState<string | null>(null);
  const [editingMemberId, setEditingMemberId] = useState<string | null>(null);
  const [campaignForm, setCampaignForm] = useState<CreateCrmCampaignDto>(emptyCampaignForm());
  const [memberForm, setMemberForm] = useState<CreateCrmCampaignMemberDto>(emptyMemberForm(scopedLeadId));

  const loadCampaigns = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getCampaigns({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        campaignType: campaignType === 'all' ? undefined : campaignType,
        activeOnly,
        businessPartnerId: scopedBusinessPartnerId || undefined,
        leadId: scopedLeadId || undefined,
      });

      setResult(data);

      if (requestedCampaignId && requestedPage === 1 && data.items.some((item) => item.campaignId === requestedCampaignId)) {
        setSelectedCampaignId(requestedCampaignId);
        return;
      }

      if (selectedCampaignId && data.items.some((item) => item.campaignId === selectedCampaignId)) {
        return;
      }

      setSelectedCampaignId(data.items[0]?.campaignId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM campaigns'));
    } finally {
      setLoading(false);
    }
  };

  const loadCampaignDetail = async (campaignId: string) => {
    if (!campaignId) {
      setSelectedCampaign(null);
      return;
    }

    try {
      setDetailLoading(true);
      setSelectedCampaign(await crmService.getCampaign(campaignId));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM campaign detail'));
      setSelectedCampaign(null);
    } finally {
      setDetailLoading(false);
    }
  };

  const loadLeads = async () => {
    try {
      const data = await crmService.getLeads({ page: 1, pageSize: 100 });
      setAvailableLeads(data.items);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load leads for campaign membership'));
    }
  };

  useEffect(() => {
    void loadCampaigns(page);
  }, [page, status, campaignType, activeOnly, scopedBusinessPartnerId, scopedLeadId]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadCampaigns(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    void loadCampaignDetail(selectedCampaignId);
  }, [selectedCampaignId]);

  useEffect(() => {
    void loadLeads();
  }, []);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      { label: 'Visible Campaigns', value: result?.totalCount.toLocaleString() || '0', hint: 'Campaigns in the current CRM scope', icon: Megaphone },
      { label: 'Active Campaigns', value: items.filter((item) => item.isActive).length.toLocaleString(), hint: `${items.filter((item) => item.isEndingSoon).length} ending soon`, icon: Target },
      { label: 'Members', value: items.reduce((sum, item) => sum + item.memberCount, 0).toLocaleString(), hint: `${items.reduce((sum, item) => sum + item.respondedMemberCount, 0)} responded`, icon: Users },
      { label: 'Weighted Pipeline', value: formatMoney(items.reduce((sum, item) => sum + item.weightedPipelineValue, 0)), hint: `${items.reduce((sum, item) => sum + item.influencedAccountCount, 0)} influenced accounts`, icon: TrendingUp },
    ];
  }, [result]);

  const selectedSummary = result?.items.find((item) => item.campaignId === selectedCampaignId);

  const resetCampaignDialog = () => {
    setEditingCampaignId(null);
    setCampaignForm(emptyCampaignForm());
    setCampaignDialogOpen(false);
  };

  const resetMemberDialog = () => {
    setEditingMemberId(null);
    setMemberForm(emptyMemberForm(scopedLeadId));
    setMemberDialogOpen(false);
  };

  const submitCampaign = async () => {
    try {
      setCampaignSaving(true);
      const payload = buildCampaignPayload(campaignForm);

      if (editingCampaignId) {
        const updated = await crmService.updateCampaign(editingCampaignId, payload);
        setSelectedCampaignId(updated.campaignId);
        toast.success('Campaign updated');
      } else {
        const created = await crmService.createCampaign(payload);
        setSelectedCampaignId(created.campaignId);
        toast.success('Campaign created');
      }

      resetCampaignDialog();
      await loadCampaigns(1);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to save campaign'));
    } finally {
      setCampaignSaving(false);
    }
  };

  const submitMember = async () => {
    if (!selectedCampaignId) {
      return;
    }

    try {
      setMemberSaving(true);
      const payload = buildMemberPayload(memberForm);

      if (editingMemberId) {
        await crmService.updateCampaignMember(selectedCampaignId, editingMemberId, payload);
        toast.success('Campaign member updated');
      } else {
        await crmService.addCampaignMember(selectedCampaignId, payload);
        toast.success('Campaign member added');
      }

      resetMemberDialog();
      await loadCampaigns(page);
      await loadCampaignDetail(selectedCampaignId);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to save campaign member'));
    } finally {
      setMemberSaving(false);
    }
  };

  const handleDeleteCampaign = async (campaignId: string) => {
    if (!window.confirm('Delete this campaign and its campaign members?')) {
      return;
    }

    try {
      await crmService.deleteCampaign(campaignId);
      toast.success('Campaign deleted');
      setSelectedCampaignId('');
      setSelectedCampaign(null);
      await loadCampaigns(1);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to delete campaign'));
    }
  };

  const handleDeleteMember = async (memberId: string) => {
    if (!selectedCampaignId || !window.confirm('Remove this lead from the campaign?')) {
      return;
    }

    try {
      await crmService.deleteCampaignMember(selectedCampaignId, memberId);
      toast.success('Campaign member removed');
      await loadCampaigns(page);
      await loadCampaignDetail(selectedCampaignId);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to delete campaign member'));
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Campaigns</h1>
          <p className="text-muted-foreground">
            Campaigns tied directly to existing leads, accounts, and live pipeline impact instead of a duplicate CRM-owned marketing model.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/leads">Leads</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/opportunities">Opportunities</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/accounts">Accounts</Link>
          </Button>
          <Button variant="outline" onClick={() => {
            setEditingCampaignId(null);
            setCampaignForm(emptyCampaignForm());
            setCampaignDialogOpen(true);
          }}>
            <Plus className="mr-2 h-4 w-4" />
            New Campaign
          </Button>
          <Button variant="outline" onClick={() => void loadCampaigns(page)}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
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

      {(scopedBusinessPartnerId || scopedLeadId || status !== 'all' || campaignType !== 'all' || activeOnly) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This workspace is narrowed to a CRM account, lead, or campaign motion segment.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {scopedBusinessPartnerId ? <Badge variant="secondary">Account scoped</Badge> : null}
            {scopedLeadId ? <Badge variant="secondary">Lead scoped</Badge> : null}
            {status !== 'all' ? <Badge variant="secondary">Status: {status}</Badge> : null}
            {campaignType !== 'all' ? <Badge variant="secondary">Type: {campaignType}</Badge> : null}
            {activeOnly ? <Badge variant="secondary">Active only</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/campaigns">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search by campaign name, status, type, and active influence.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 lg:grid-cols-[1.4fr_0.8fr_0.8fr]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
              <Input className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search campaign, type, or notes" />
            </div>

            <Select value={status} onValueChange={(value) => {
              setPage(1);
              setStatus(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Campaign status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {CAMPAIGN_STATUS_OPTIONS.map((option) => <SelectItem key={option} value={option}>{option}</SelectItem>)}
              </SelectContent>
            </Select>

            <Select value={campaignType} onValueChange={(value) => {
              setPage(1);
              setCampaignType(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Campaign type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All types</SelectItem>
                {CAMPAIGN_TYPE_OPTIONS.map((option) => <SelectItem key={option} value={option}>{option}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>

          <div className="flex flex-wrap items-center gap-3 rounded-lg border px-4 py-3">
            <Switch checked={activeOnly} onCheckedChange={(checked) => {
              setPage(1);
              setActiveOnly(checked);
            }} />
            <div>
              <div className="text-sm font-medium">Active campaigns only</div>
              <div className="text-xs text-muted-foreground">Limit the register to live campaign motions still affecting pipeline.</div>
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.1fr_1fr]">
        <Card>
          <CardHeader>
            <div className="flex items-start justify-between gap-3">
              <div>
                <CardTitle>Campaign Register</CardTitle>
                <CardDescription>Campaigns ranked by live commercial effect and response signal.</CardDescription>
              </div>
              <Badge variant="secondary">{result?.totalCount || 0} campaigns</Badge>
            </div>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading campaigns...</div> : null}
            {!loading && !result?.items.length ? <div className="py-10 text-center text-muted-foreground">No campaigns match the current CRM scope.</div> : null}
            {!loading && result?.items.length ? (
              <div className="space-y-4">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Campaign</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Members</TableHead>
                      <TableHead className="text-right">Response</TableHead>
                      <TableHead className="text-right">Pipeline</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {result.items.map((item) => (
                      <TableRow key={item.campaignId} className={item.campaignId === selectedCampaignId ? 'bg-muted/40' : ''} onClick={() => setSelectedCampaignId(item.campaignId)}>
                        <TableCell>
                          <div className="font-medium">{item.name}</div>
                          <div className="text-xs text-muted-foreground">{item.campaignType} | {formatDate(item.startDate)} to {formatDate(item.endDate)}</div>
                        </TableCell>
                        <TableCell>
                          <Badge variant={item.isActive ? 'default' : 'secondary'}>{item.campaignStatus}</Badge>
                          {item.isEndingSoon ? <div className="text-xs text-amber-600">Ending soon</div> : null}
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{item.memberCount}</div>
                          <div className="text-xs text-muted-foreground">{item.respondedMemberCount} responded</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatPercent(item.responseRate)}</div>
                          <div className="text-xs text-muted-foreground">{item.responseCount} responses</div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div>{formatMoney(item.weightedPipelineValue)}</div>
                          <div className="text-xs text-muted-foreground">{item.influencedAccountCount} accounts</div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>

                <div className="flex items-center justify-between">
                  <div className="text-sm text-muted-foreground">Page {result.page} of {Math.max(1, Math.ceil(result.totalCount / result.pageSize))}</div>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" disabled={result.page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>Previous</Button>
                    <Button variant="outline" size="sm" disabled={result.page * result.pageSize >= result.totalCount} onClick={() => setPage((current) => current + 1)}>Next</Button>
                  </div>
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <CardTitle>Campaign Detail</CardTitle>
                <CardDescription>Member activity, influenced accounts, and linked opportunities for the selected campaign.</CardDescription>
              </div>
              {selectedCampaign ? (
                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" size="sm" onClick={() => {
                    setEditingCampaignId(selectedCampaign.campaignId);
                    setCampaignForm(mapCampaignToForm(selectedCampaign));
                    setCampaignDialogOpen(true);
                  }}>
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => {
                    setEditingMemberId(null);
                    setMemberForm(emptyMemberForm(scopedLeadId));
                    setMemberDialogOpen(true);
                  }}>
                    <UserPlus className="mr-2 h-4 w-4" />
                    Add Member
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => void handleDeleteCampaign(selectedCampaign.campaignId)}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Delete
                  </Button>
                </div>
              ) : null}
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {detailLoading ? <div className="py-10 text-center text-muted-foreground">Loading campaign detail...</div> : null}
            {!detailLoading && !selectedCampaign ? <div className="py-10 text-center text-muted-foreground">Select a campaign to inspect its CRM impact.</div> : null}
            {!detailLoading && selectedCampaign ? (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant={selectedCampaign.isActive ? 'default' : 'secondary'}>{selectedCampaign.campaignStatus}</Badge>
                  <Badge variant="outline">{selectedCampaign.campaignType}</Badge>
                  {selectedCampaign.isEndingSoon ? <Badge variant="outline">Ending soon</Badge> : null}
                  {selectedSummary ? <Badge variant="secondary">{selectedSummary.memberCount} members</Badge> : null}
                </div>

                <div>
                  <div className="text-xl font-semibold">{selectedCampaign.name}</div>
                  <div className="text-sm text-muted-foreground">Runs from {formatDate(selectedCampaign.startDate)} to {formatDate(selectedCampaign.endDate)}</div>
                </div>

                <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription className="flex items-center gap-2"><DollarSign className="h-4 w-4" /> Budget</CardDescription>
                      <CardTitle>{formatMoney(selectedCampaign.budget)}</CardTitle>
                    </CardHeader>
                    <CardContent className="pt-0 text-xs text-muted-foreground">Actual cost {formatMoney(selectedCampaign.actualCost)}</CardContent>
                  </Card>
                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription className="flex items-center gap-2"><TrendingUp className="h-4 w-4" /> Pipeline</CardDescription>
                      <CardTitle>{formatMoney(selectedCampaign.weightedPipelineValue)}</CardTitle>
                    </CardHeader>
                    <CardContent className="pt-0 text-xs text-muted-foreground">{selectedCampaign.openOpportunityCount} open opportunities</CardContent>
                  </Card>
                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription className="flex items-center gap-2"><Users className="h-4 w-4" /> Response</CardDescription>
                      <CardTitle>{formatPercent(selectedCampaign.responseRate)}</CardTitle>
                    </CardHeader>
                    <CardContent className="pt-0 text-xs text-muted-foreground">{selectedCampaign.respondedMemberCount} responded members</CardContent>
                  </Card>
                  <Card>
                    <CardHeader className="pb-2">
                      <CardDescription className="flex items-center gap-2"><Target className="h-4 w-4" /> ROI</CardDescription>
                      <CardTitle>{formatPercent(selectedCampaign.roiPercent)}</CardTitle>
                    </CardHeader>
                    <CardContent className="pt-0 text-xs text-muted-foreground">Actual revenue {formatMoney(selectedCampaign.actualRevenue)}</CardContent>
                  </Card>
                </div>

                {selectedCampaign.description ? <div className="rounded-lg border p-4 text-sm text-muted-foreground">{selectedCampaign.description}</div> : null}

                <Card>
                  <CardHeader>
                    <CardTitle>Campaign Members</CardTitle>
                  </CardHeader>
                  <CardContent>
                    {!selectedCampaign.members.length ? <div className="py-8 text-center text-muted-foreground">No members added yet.</div> : null}
                    {selectedCampaign.members.length ? (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Lead</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead className="text-right">Pipeline</TableHead>
                            <TableHead className="text-right">Actions</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {selectedCampaign.members.map((member) => (
                            <TableRow key={member.memberId}>
                              <TableCell>
                                <div className="font-medium"><Link href={`/crm/leads?leadId=${member.leadId}`} className="hover:underline">{member.leadName}</Link></div>
                                <div className="text-xs text-muted-foreground">{member.companyName || 'No company'} | {member.convertedBusinessPartnerName || member.leadStatus}</div>
                              </TableCell>
                              <TableCell>
                                <Badge variant="secondary">{member.memberStatus}</Badge>
                                {member.needsFollowUp ? <div className="text-xs text-amber-600">Needs follow-up</div> : null}
                              </TableCell>
                              <TableCell className="text-right">
                                <div>{formatMoney(member.weightedPipelineValue)}</div>
                                <div className="text-xs text-muted-foreground">{member.openOpportunityCount} open</div>
                              </TableCell>
                              <TableCell className="text-right">
                                <div className="flex justify-end gap-2">
                                  <Button variant="ghost" size="icon" onClick={() => {
                                    setEditingMemberId(member.memberId);
                                    setMemberForm(mapMemberToForm(member));
                                    setMemberDialogOpen(true);
                                  }}>
                                    <Pencil className="h-4 w-4" />
                                  </Button>
                                  <Button variant="ghost" size="icon" onClick={() => void handleDeleteMember(member.memberId)}>
                                    <Trash2 className="h-4 w-4" />
                                  </Button>
                                </div>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    ) : null}
                  </CardContent>
                </Card>

                <div className="grid gap-4 xl:grid-cols-2">
                  <Card>
                    <CardHeader>
                      <CardTitle>Influenced Accounts</CardTitle>
                    </CardHeader>
                    <CardContent>
                      {!selectedCampaign.influencedAccounts.length ? <div className="py-8 text-center text-muted-foreground">No influenced accounts detected yet.</div> : null}
                      {selectedCampaign.influencedAccounts.length ? (
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Account</TableHead>
                              <TableHead className="text-right">Converted Leads</TableHead>
                              <TableHead className="text-right">Pipeline</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {selectedCampaign.influencedAccounts.map((account) => (
                              <TableRow key={account.businessPartnerId}>
                                <TableCell>
                                  <div className="font-medium"><Link href={`/crm/accounts/${account.businessPartnerId}`} className="hover:underline">{account.partnerName}</Link></div>
                                  <div className="text-xs text-muted-foreground">{account.partnerCode} | {account.partnerType}</div>
                                </TableCell>
                                <TableCell className="text-right">{account.convertedLeadCount}</TableCell>
                                <TableCell className="text-right">
                                  <div>{formatMoney(account.weightedPipelineValue)}</div>
                                  <div className="text-xs text-muted-foreground">{account.openOpportunityCount} open</div>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      ) : null}
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle>Open Opportunities</CardTitle>
                    </CardHeader>
                    <CardContent>
                      {!selectedCampaign.opportunities.length ? <div className="py-8 text-center text-muted-foreground">No open opportunities linked yet.</div> : null}
                      {selectedCampaign.opportunities.length ? (
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Opportunity</TableHead>
                              <TableHead>Stage</TableHead>
                              <TableHead className="text-right">Weighted</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {selectedCampaign.opportunities.map((opportunity) => (
                              <TableRow key={opportunity.opportunityId}>
                                <TableCell>
                                  <div className="font-medium"><Link href={`/crm/opportunities?opportunityId=${opportunity.opportunityId}`} className="hover:underline">{opportunity.name}</Link></div>
                                  <div className="text-xs text-muted-foreground">{opportunity.businessPartnerName || opportunity.leadName || opportunity.leadSource}</div>
                                </TableCell>
                                <TableCell><Badge variant={opportunity.probability >= 70 ? 'default' : 'secondary'}>{opportunity.stage}</Badge></TableCell>
                                <TableCell className="text-right">
                                  <div>{formatMoney(opportunity.weightedValue, opportunity.currency)}</div>
                                  <div className="text-xs text-muted-foreground">{formatDate(opportunity.expectedCloseDate)}</div>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      ) : null}
                    </CardContent>
                  </Card>
                </div>

                {selectedCampaign.notes ? (
                  <Card>
                    <CardHeader>
                      <CardTitle>Notes</CardTitle>
                    </CardHeader>
                    <CardContent className="text-sm text-muted-foreground">{selectedCampaign.notes}</CardContent>
                  </Card>
                ) : null}
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <CampaignDialog
        open={campaignDialogOpen}
        title={editingCampaignId ? 'Edit campaign' : 'Create campaign'}
        description={editingCampaignId ? 'Adjust campaign timing, economics, and notes.' : 'Create a CRM campaign without duplicating lead or account ownership.'}
        form={campaignForm}
        saving={campaignSaving}
        onOpenChange={(open) => {
          if (!open) {
            resetCampaignDialog();
            return;
          }

          setCampaignDialogOpen(true);
        }}
        onSubmit={() => void submitCampaign()}
        onChange={(field, value) => setCampaignForm((current) => ({ ...current, [field]: value }))}
      />

      <CampaignMemberDialog
        open={memberDialogOpen}
        title={editingMemberId ? 'Edit campaign member' : 'Add campaign member'}
        description={editingMemberId ? 'Update response status and notes.' : 'Attach an existing CRM lead to this campaign.'}
        form={memberForm}
        leads={availableLeads}
        saving={memberSaving}
        leadLocked={Boolean(editingMemberId)}
        onOpenChange={(open) => {
          if (!open) {
            resetMemberDialog();
            return;
          }

          setMemberDialogOpen(true);
        }}
        onSubmit={() => void submitMember()}
        onChange={(field, value) => setMemberForm((current) => ({ ...current, [field]: value }))}
      />
    </div>
  );
}
