'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatCurrencyAmount as formatMoney } from '@/lib/currency';
import { businessPartnerService, type CreateBusinessPartnerContactDto } from '@/services/businessPartnerService';
import { crmService, type CrmAccountContactDto, type CrmAccountDetailDto } from '@/services/crmService';
import {
  AlertTriangle,
  ArrowLeft,
  BarChart3,
  Building2,
  Clock3,
  FileText,
  Globe,
  Mail,
  MessageSquare,
  MapPin,
  Pencil,
  Phone,
  Plus,
  RefreshCw,
  Target,
  Trash2,
  TrendingUp,
  Users,
  Workflow,
} from 'lucide-react';
import { toast } from 'sonner';

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const formatImpact = (value: number) => `${value > 0 ? '+' : ''}${value}`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;
const toOptionalString = (value?: string) => value?.trim() ? value.trim() : undefined;

const createEmptyContactForm = (): CreateBusinessPartnerContactDto => ({
  contactName: '',
  title: '',
  department: '',
  email: '',
  phone: '',
  mobile: '',
  isPrimary: false,
});

const mapContactToForm = (contact: CrmAccountContactDto): CreateBusinessPartnerContactDto => ({
  contactName: contact.contactName,
  title: contact.contactTitle || '',
  department: contact.department || '',
  email: contact.email || '',
  phone: contact.phone || '',
  mobile: contact.mobile || '',
  isPrimary: contact.isPrimary,
});

const buildContactPayload = (form: CreateBusinessPartnerContactDto): CreateBusinessPartnerContactDto => ({
  contactName: form.contactName.trim(),
  title: toOptionalString(form.title),
  department: toOptionalString(form.department),
  email: toOptionalString(form.email),
  phone: toOptionalString(form.phone),
  mobile: toOptionalString(form.mobile),
  isPrimary: form.isPrimary,
});

type AccountTimelineItem = {
  key: string;
  entityType: string;
  title: string;
  subtitle: string;
  href: string;
  dateLabel: string;
  dateValue?: string;
  amount?: number;
  currency?: string;
  badgeVariant: 'default' | 'secondary' | 'outline' | 'destructive';
};

const buildAccountTimeline = (account: CrmAccountDetailDto): AccountTimelineItem[] => [
  ...account.leads.map<AccountTimelineItem>((lead) => ({
    key: `lead-${lead.leadId}`,
    entityType: 'Lead',
    title: lead.fullName,
    subtitle: [lead.leadStatus, lead.companyName || 'No company'].filter(Boolean).join(' | '),
    href: `/crm/leads?leadId=${lead.leadId}`,
    dateLabel: 'Created',
    dateValue: lead.createdAt,
    amount: lead.estimatedValue > 0 ? lead.estimatedValue : undefined,
    currency: account.currency || 'USD',
    badgeVariant: lead.needsFollowUp ? 'secondary' : 'outline',
  })),
  ...account.opportunities.map<AccountTimelineItem>((opportunity) => ({
    key: `opportunity-${opportunity.opportunityId}`,
    entityType: 'Opportunity',
    title: opportunity.name,
    subtitle: [opportunity.stage, opportunity.opportunityType].filter(Boolean).join(' | '),
    href: `/crm/opportunities?businessPartnerId=${account.businessPartnerId}&opportunityId=${opportunity.opportunityId}`,
    dateLabel: 'Expected close',
    dateValue: opportunity.expectedCloseDate,
    amount: opportunity.amount,
    currency: opportunity.currency,
    badgeVariant: opportunity.probability >= 70 ? 'default' : 'secondary',
  })),
  ...account.quotes.map<AccountTimelineItem>((quote) => ({
    key: `quote-${quote.quoteId}`,
    entityType: 'Quote',
    title: quote.quoteName,
    subtitle: [quote.quoteStatus, quote.opportunityName || 'Account quote'].filter(Boolean).join(' | '),
    href: `/crm/quotes?businessPartnerId=${account.businessPartnerId}&quoteId=${quote.quoteId}`,
    dateLabel: 'Valid until',
    dateValue: quote.validUntil,
    amount: quote.value,
    currency: quote.currency,
    badgeVariant: quote.quoteStatus === 'Accepted' ? 'default' : quote.isExpiringSoon ? 'secondary' : 'outline',
  })),
  ...account.activities.map<AccountTimelineItem>((activity) => ({
    key: `activity-${activity.activityId}`,
    entityType: 'Activity',
    title: activity.subject,
    subtitle: [activity.activityType, activity.activityStatus].filter(Boolean).join(' | '),
    href: `/crm/activities?businessPartnerId=${account.businessPartnerId}&activityId=${activity.activityId}`,
    dateLabel: activity.dueDate ? 'Due' : 'Activity date',
    dateValue: activity.dueDate || activity.activityDate,
    badgeVariant: activity.requiresFollowUp ? 'secondary' : 'outline',
  })),
  ...account.projects.map<AccountTimelineItem>((project) => ({
    key: `project-${project.projectId}`,
    entityType: 'Project',
    title: project.title,
    subtitle: [project.status, project.projectCode].filter(Boolean).join(' | '),
    href: `/crm/projects?businessPartnerId=${account.businessPartnerId}&projectId=${project.projectId}`,
    dateLabel: 'Target end',
    dateValue: project.targetEndDate,
    amount: project.value,
    currency: account.currency || 'USD',
    badgeVariant: project.status === 'InProgress' ? 'default' : 'outline',
  })),
  ...account.contracts.map<AccountTimelineItem>((contract) => ({
    key: `contract-${contract.contractId}`,
    entityType: 'Contract',
    title: contract.contractTitle,
    subtitle: [contract.status, contract.contractNumber].filter(Boolean).join(' | '),
    href: `/crm/contracts?businessPartnerId=${account.businessPartnerId}&contractId=${contract.contractId}`,
    dateLabel: 'End date',
    dateValue: contract.endDate,
    amount: contract.contractValue,
    currency: account.currency || 'USD',
    badgeVariant: contract.status === 'Active' ? 'default' : 'outline',
  })),
  ...account.tenders.map<AccountTimelineItem>((tender) => ({
    key: `tender-${tender.entityType}-${tender.entityId}`,
    entityType: 'Tender',
    title: tender.tenderTitle || tender.referenceNumber,
    subtitle: [tender.entityType, tender.status].filter(Boolean).join(' | '),
    href: `/crm/tenders?businessPartnerId=${account.businessPartnerId}&tenderId=${tender.tenderId}&entityType=${tender.entityType}&entityId=${tender.entityId}`,
    dateLabel: 'Recorded',
    dateValue: tender.createdAt,
    amount: tender.amount,
    currency: account.currency || tender.currency || 'USD',
    badgeVariant: tender.entityType === 'Award' ? 'default' : 'outline',
  })),
].sort((left, right) => {
  const leftTime = left.dateValue ? new Date(left.dateValue).getTime() : 0;
  const rightTime = right.dateValue ? new Date(right.dateValue).getTime() : 0;
  return rightTime - leftTime;
});

function ContactDialog({
  open,
  mode,
  form,
  saving,
  onOpenChange,
  onSubmit,
  onChange,
}: {
  open: boolean;
  mode: 'create' | 'edit';
  form: CreateBusinessPartnerContactDto;
  saving: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onChange: <K extends keyof CreateBusinessPartnerContactDto>(field: K, value: CreateBusinessPartnerContactDto[K]) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'Add Account Contact' : 'Edit Account Contact'}</DialogTitle>
          <DialogDescription>
            Manage the BusinessPartner contact record that powers this CRM account.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="contact-name">Contact Name</Label>
            <Input
              id="contact-name"
              value={form.contactName}
              onChange={(event) => onChange('contactName', event.target.value)}
              placeholder="Irene Mensah"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contact-title">Title</Label>
            <Input
              id="contact-title"
              value={form.title || ''}
              onChange={(event) => onChange('title', event.target.value)}
              placeholder="Commercial Director"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contact-department">Department</Label>
            <Input
              id="contact-department"
              value={form.department || ''}
              onChange={(event) => onChange('department', event.target.value)}
              placeholder="Commercial"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contact-email">Email</Label>
            <Input
              id="contact-email"
              type="email"
              value={form.email || ''}
              onChange={(event) => onChange('email', event.target.value)}
              placeholder="irene@atlas.example"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contact-phone">Phone</Label>
            <Input
              id="contact-phone"
              value={form.phone || ''}
              onChange={(event) => onChange('phone', event.target.value)}
              placeholder="+1 555 0100"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="contact-mobile">Mobile</Label>
            <Input
              id="contact-mobile"
              value={form.mobile || ''}
              onChange={(event) => onChange('mobile', event.target.value)}
              placeholder="+1 555 0101"
            />
          </div>

          <div className="flex items-center justify-between rounded-lg border p-4 md:col-span-2">
            <div>
              <div className="font-medium">Primary Contact</div>
              <div className="text-sm text-muted-foreground">
                Keep this contact synchronized with the account summary.
              </div>
            </div>
            <Switch
              checked={form.isPrimary}
              onCheckedChange={(checked) => onChange('isPrimary', checked)}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={onSubmit} disabled={saving}>
            {saving ? 'Saving...' : mode === 'create' ? 'Add Contact' : 'Save Contact'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function CrmAccountDetailPage() {
  const params = useParams();
  const businessPartnerId = (params?.businessPartnerId as string | undefined) ?? '';

  const [account, setAccount] = useState<CrmAccountDetailDto | null>(null);
  const [referenceTime, setReferenceTime] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [contactDialogOpen, setContactDialogOpen] = useState(false);
  const [deleteContactOpen, setDeleteContactOpen] = useState(false);
  const [contactFormMode, setContactFormMode] = useState<'create' | 'edit'>('create');
  const [selectedContactId, setSelectedContactId] = useState('');
  const [contactForm, setContactForm] = useState<CreateBusinessPartnerContactDto>(createEmptyContactForm());
  const [contactSaving, setContactSaving] = useState(false);

  const loadAccount = async () => {
    try {
      setLoading(true);
      setAccount(await crmService.getAccountDetail(businessPartnerId, 12));
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM account'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setReferenceTime(Date.now());
  }, []);

  useEffect(() => {
    if (!businessPartnerId) {
      return;
    }

    void loadAccount();
  }, [businessPartnerId]);

  const openCreateContactDialog = () => {
    setContactFormMode('create');
    setSelectedContactId('');
    setContactForm(createEmptyContactForm());
    setContactDialogOpen(true);
  };

  const openEditContactDialog = (contact: CrmAccountContactDto) => {
    setContactFormMode('edit');
    setSelectedContactId(contact.contactId);
    setContactForm(mapContactToForm(contact));
    setContactDialogOpen(true);
  };

  const submitContact = async () => {
    if (!contactForm.contactName.trim()) {
      toast.error('Contact name is required.');
      return;
    }

    try {
      setContactSaving(true);
      const payload = buildContactPayload(contactForm);

      if (contactFormMode === 'create') {
        await businessPartnerService.createPartnerContact(businessPartnerId, payload);
      } else {
        await businessPartnerService.updatePartnerContact(businessPartnerId, selectedContactId, payload);
      }

      toast.success(contactFormMode === 'create' ? 'Contact added.' : 'Contact updated.');
      setContactDialogOpen(false);
      await loadAccount();
    } catch (error: unknown) {
      toast.error(getMessage(error, `Failed to ${contactFormMode} account contact`));
    } finally {
      setContactSaving(false);
    }
  };

  const deleteContact = async () => {
    if (!selectedContactId) {
      return;
    }

    try {
      setContactSaving(true);
      await businessPartnerService.deletePartnerContact(businessPartnerId, selectedContactId);
      toast.success('Contact deleted.');
      setDeleteContactOpen(false);
      setSelectedContactId('');
      await loadAccount();
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to delete account contact'));
    } finally {
      setContactSaving(false);
    }
  };

  const setPrimaryContact = async (contactId: string) => {
    try {
      setContactSaving(true);
      await businessPartnerService.setPrimaryPartnerContact(businessPartnerId, contactId);
      toast.success('Primary contact updated.');
      await loadAccount();
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to update primary contact'));
    } finally {
      setContactSaving(false);
    }
  };

  const summaryCards = useMemo(() => {
    if (!account) {
      return [];
    }

    return [
      {
        label: 'Open Pipeline',
        value: account.openOpportunityCount.toLocaleString(),
        hint: formatMoney(account.openOpportunityValue, account.currency || 'USD'),
        icon: TrendingUp,
      },
      {
        label: 'Weighted Pipeline',
        value: formatMoney(account.weightedPipelineValue, account.currency || 'USD'),
        hint: `${account.relatedLeadCount} related leads`,
        icon: Target,
      },
      {
        label: 'Active Quotes',
        value: account.activeQuoteCount.toLocaleString(),
        hint: formatMoney(account.activeQuoteValue, account.currency || 'USD'),
        icon: FileText,
      },
      {
        label: 'Delivery Footprint',
        value: account.activeProjectCount.toLocaleString(),
        hint: `${account.activeContractCount} active contracts`,
        icon: Building2,
      },
      {
        label: 'Tender Context',
        value: account.tenderAwardCount.toLocaleString(),
        hint: `${account.tenderBidCount} bids | ${account.tenderInvitationCount} invitations`,
        icon: Users,
      },
      {
        label: 'Account Health',
        value: formatScore(account.healthScore),
        hint: `${account.healthCategory}${account.hasOpenFollowUp ? ' | Follow-up required' : ''}`,
        icon: AlertTriangle,
      },
    ];
  }, [account]);

  const timelineItems = useMemo(() => account ? buildAccountTimeline(account) : [], [account]);
  const upcomingTimelineItems = useMemo(() => {
    return timelineItems
      .filter((item) =>
        item.dateValue
        && (referenceTime === null || new Date(item.dateValue).getTime() >= referenceTime))
      .sort((left, right) => new Date(left.dateValue || '').getTime() - new Date(right.dateValue || '').getTime())
      .slice(0, 6);
  }, [referenceTime, timelineItems]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-3">
          <Button asChild variant="ghost" className="px-0 text-muted-foreground hover:bg-transparent">
            <Link href="/crm">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to CRM
            </Link>
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">{account?.partnerName || 'CRM Account'}</h1>
            <p className="text-muted-foreground">
              BusinessPartner-backed account detail across pipeline, delivery, and follow-up signals.
            </p>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/accounts">
              Accounts
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/opportunities?businessPartnerId=${businessPartnerId}`}>
              Open Pipeline
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/activities?businessPartnerId=${businessPartnerId}`}>
              Activities
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/quotes?businessPartnerId=${businessPartnerId}`}>
              Open Quotes
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/projects?businessPartnerId=${businessPartnerId}`}>
              Projects
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/contracts?businessPartnerId=${businessPartnerId}`}>
              Contracts
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/tenders?businessPartnerId=${businessPartnerId}`}>
              Tenders
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/readiness?businessPartnerId=${businessPartnerId}`}>
              Readiness
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/risk?businessPartnerId=${businessPartnerId}`}>
              Risk
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/contacts?businessPartnerId=${businessPartnerId}`}>
              Contacts
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/collaboration?businessPartnerId=${businessPartnerId}`}>
              <Workflow className="mr-2 h-4 w-4" />
              Collaboration
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/crm/service?businessPartnerId=${businessPartnerId}`}>
              <MessageSquare className="mr-2 h-4 w-4" />
              Service
            </Link>
          </Button>
          <Button asChild>
            <Link href={`/crm/opportunities?businessPartnerId=${businessPartnerId}&new=1`}>
              New Opportunity
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/reports">
              <BarChart3 className="mr-2 h-4 w-4" />
              Reports
            </Link>
          </Button>
          <Button variant="outline" onClick={() => void loadAccount()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
        {summaryCards.map((item) => {
          const Icon = item.icon;

          return (
            <Card key={item.label}>
              <CardHeader className="pb-2">
                <CardDescription className="flex items-center gap-2">
                  <Icon className="h-4 w-4" />
                  {item.label}
                </CardDescription>
                <CardTitle>{item.value}</CardTitle>
              </CardHeader>
              <CardContent className="pt-0 text-xs text-muted-foreground">{item.hint}</CardContent>
            </Card>
          );
        })}
      </div>

      {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM account detail...</div> : null}

      {!loading && !account ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            This CRM account could not be found.
          </CardContent>
        </Card>
      ) : null}

      {!loading && account ? (
        <>
          <div className="grid gap-4 xl:grid-cols-[1.3fr_1fr]">
            <Card>
              <CardHeader>
                <CardTitle>Account Profile</CardTitle>
                <CardDescription>
                  ERP-native account identity anchored on the existing business partner record.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline">{account.partnerCode}</Badge>
                  <Badge variant="secondary">{account.partnerType}</Badge>
                  <Badge variant={account.isAtRisk ? 'destructive' : 'outline'}>
                    {account.healthCategory}
                  </Badge>
                  <Badge variant="secondary">{formatScore(account.healthScore)}</Badge>
                  {account.hasOpenFollowUp ? <Badge variant="secondary">Follow-Up</Badge> : null}
                  {account.isOnCreditHold ? <Badge variant="destructive">Credit Hold</Badge> : null}
                </div>

                <div className="grid gap-3 md:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Commercial</div>
                    <div className="mt-2 space-y-2 text-sm">
                      <div className="font-medium">{account.customerType || 'Unclassified customer'}</div>
                      <div>{account.registrationStatus}</div>
                      <div>{account.salesTerritory || 'No sales territory assigned'}</div>
                      <div>{account.paymentTerms || 'Payment terms not set'}</div>
                    </div>
                  </div>

                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Relationship</div>
                    <div className="mt-2 space-y-2 text-sm">
                      <div>{account.primaryContactName || 'Primary contact not set'}</div>
                      <div>{account.primaryContactTitle || 'No contact title'}</div>
                      <div>{formatDate(account.customerSince)}</div>
                      <div>{account.riskLevel || 'No risk level assigned'}</div>
                    </div>
                  </div>
                </div>

                <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Email</div>
                    <div className="mt-2 flex items-center gap-2">
                      <Mail className="h-4 w-4 text-muted-foreground" />
                      <span>{account.primaryEmail || 'Not provided'}</span>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Phone</div>
                    <div className="mt-2 flex items-center gap-2">
                      <Phone className="h-4 w-4 text-muted-foreground" />
                      <span>{account.primaryPhone || 'Not provided'}</span>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Website</div>
                    <div className="mt-2 flex items-center gap-2">
                      <Globe className="h-4 w-4 text-muted-foreground" />
                      <span>{account.website || 'Not provided'}</span>
                    </div>
                  </div>
                  <div className="rounded-lg border p-4 text-sm">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Location</div>
                    <div className="mt-2 flex items-center gap-2">
                      <MapPin className="h-4 w-4 text-muted-foreground" />
                      <span>{[account.physicalCity, account.physicalCountry].filter(Boolean).join(', ') || 'Not provided'}</span>
                    </div>
                  </div>
                </div>

                {account.notes ? (
                  <div className="rounded-lg border bg-muted/30 p-4 text-sm">
                    <div className="mb-2 text-xs uppercase tracking-wide text-muted-foreground">Notes</div>
                    {account.notes}
                  </div>
                ) : null}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Commercial & Health Snapshot</CardTitle>
                <CardDescription>Key signals carried forward from the ERP source-of-truth modules.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="rounded-xl border bg-muted/30 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Account Health</div>
                      <div className="mt-2 text-3xl font-semibold">{formatScore(account.healthScore)}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {account.healthCategory}{account.riskLevel ? ` | ${account.riskLevel} risk` : ''}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={account.isAtRisk ? 'destructive' : 'outline'}>
                        {account.isAtRisk ? 'Escalate' : 'Stable'}
                      </Badge>
                      {account.hasOpenFollowUp ? <Badge variant="secondary">Needs Attention</Badge> : null}
                    </div>
                  </div>
                  <Progress value={account.healthScore} className="mt-4" />
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Credit Limit</div>
                    <div className="mt-2 text-xl font-semibold">
                      {account.creditLimit ? formatMoney(account.creditLimit, account.currency || 'USD') : 'Not set'}
                    </div>
                  </div>
                  <div className="rounded-lg border p-4">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Outstanding</div>
                    <div className="mt-2 text-xl font-semibold">
                      {account.outstandingBalance ? formatMoney(account.outstandingBalance, account.currency || 'USD') : 'None'}
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border p-4">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <div className="font-medium">Next Milestone</div>
                      <div className="text-sm text-muted-foreground">{formatDate(account.nextMilestoneDate)}</div>
                    </div>
                    <Badge variant={account.expiringContractCount > 0 ? 'destructive' : 'outline'}>
                      {account.expiringContractCount} expiring contracts
                    </Badge>
                  </div>
                </div>

                <div className="space-y-3 text-sm">
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <span>Projects</span>
                    <span className="font-medium">
                      {account.activeProjectCount} active | {formatMoney(account.projectValue, account.currency || 'USD')}
                    </span>
                  </div>
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <span>Contracts</span>
                    <span className="font-medium">
                      {account.activeContractCount} active | {formatMoney(account.contractValue, account.currency || 'USD')}
                    </span>
                  </div>
                  <div className="flex items-center justify-between rounded-lg border p-3">
                    <span>Tenders</span>
                    <span className="font-medium">
                      {account.tenderAwardCount} awards | {formatMoney(account.tenderAwardedValue, account.currency || 'USD')}
                    </span>
                  </div>
                </div>

                <div className="space-y-2">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Top Health Signals</div>
                  {!account.healthSignals.length ? (
                    <div className="rounded-lg border p-4 text-sm text-muted-foreground">
                      No health signals are available yet for this account.
                    </div>
                  ) : (
                    account.healthSignals.slice(0, 4).map((signal) => (
                      <div key={`${signal.label}-${signal.scoreImpact}`} className="flex items-center justify-between rounded-lg border p-3 text-sm">
                        <div>
                          <div className="font-medium">{signal.label}</div>
                          <div className="text-xs text-muted-foreground">{signal.direction}</div>
                        </div>
                        <Badge variant={signal.scoreImpact < 0 ? 'destructive' : 'secondary'}>
                          {formatImpact(signal.scoreImpact)}
                        </Badge>
                      </div>
                    ))
                  )}
                </div>

                {account.creditHoldReason ? (
                  <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                    <div className="font-medium">Credit hold reason</div>
                    <div className="mt-1">{account.creditHoldReason}</div>
                  </div>
                ) : null}
              </CardContent>
            </Card>
          </div>

          <Tabs defaultValue="pipeline" className="space-y-4">
            <TabsList>
              <TabsTrigger value="pipeline">Pipeline</TabsTrigger>
              <TabsTrigger value="delivery">Delivery</TabsTrigger>
              <TabsTrigger value="activity">Contacts & Activity</TabsTrigger>
              <TabsTrigger value="timeline">Timeline</TabsTrigger>
              <TabsTrigger value="health">Health</TabsTrigger>
            </TabsList>

            <TabsContent value="pipeline" className="space-y-4">
              <div className="grid gap-4 xl:grid-cols-2">
                <Card>
                  <CardHeader>
                    <CardTitle>Related Leads</CardTitle>
                    <CardDescription>Lead records already tied to this account context.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {!account.leads.length ? (
                      <div className="py-10 text-center text-muted-foreground">No related leads available.</div>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Lead</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead>Next Follow-Up</TableHead>
                            <TableHead className="text-right">Value</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {account.leads.map((lead) => (
                            <TableRow key={lead.leadId}>
                              <TableCell>
                                <div className="font-medium">
                                  <Link href={`/crm/leads?leadId=${lead.leadId}`} className="hover:underline">
                                    {lead.fullName}
                                  </Link>
                                </div>
                                <div className="text-xs text-muted-foreground">
                                  {lead.companyName || 'No company'}{lead.jobTitle ? ` | ${lead.jobTitle}` : ''}
                                </div>
                              </TableCell>
                              <TableCell>
                                <Badge variant={lead.needsFollowUp ? 'secondary' : 'outline'}>{lead.leadStatus}</Badge>
                              </TableCell>
                              <TableCell>{formatDate(lead.nextFollowUpDate)}</TableCell>
                              <TableCell className="text-right">{formatMoney(lead.estimatedValue, account.currency || 'USD')}</TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Open Opportunities</CardTitle>
                    <CardDescription>Commercial work in flight for this account.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {!account.opportunities.length ? (
                      <div className="py-10 text-center text-muted-foreground">No open opportunities available.</div>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Opportunity</TableHead>
                            <TableHead>Stage</TableHead>
                            <TableHead>Close</TableHead>
                            <TableHead className="text-right">Amount</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {account.opportunities.map((opportunity) => (
                            <TableRow key={opportunity.opportunityId}>
                              <TableCell>
                                <div className="font-medium">
                                  <Link
                                    href={`/crm/opportunities?businessPartnerId=${businessPartnerId}&opportunityId=${opportunity.opportunityId}`}
                                    className="hover:underline"
                                  >
                                    {opportunity.name}
                                  </Link>
                                </div>
                                <div className="text-xs text-muted-foreground">
                                  {opportunity.leadName || opportunity.leadSource || 'General pipeline'}
                                </div>
                              </TableCell>
                              <TableCell>
                                <Badge variant={opportunity.probability >= 70 ? 'default' : 'secondary'}>
                                  {opportunity.stage}
                                </Badge>
                              </TableCell>
                              <TableCell>{formatDate(opportunity.expectedCloseDate)}</TableCell>
                              <TableCell className="text-right">
                                <div>{formatMoney(opportunity.amount, opportunity.currency)}</div>
                                <div className="text-xs text-muted-foreground">
                                  {opportunity.probability}% | {formatMoney(opportunity.weightedValue, opportunity.currency)}
                                </div>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </CardContent>
                </Card>
              </div>

              <Card>
                <CardHeader>
                  <CardTitle>Quotes</CardTitle>
                  <CardDescription>Quote and proposal activity linked through account or opportunity context.</CardDescription>
                </CardHeader>
                <CardContent>
                  {!account.quotes.length ? (
                    <div className="py-10 text-center text-muted-foreground">No quotes are linked yet.</div>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Quote</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Valid Until</TableHead>
                          <TableHead className="text-right">Value</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {account.quotes.map((quote) => (
                          <TableRow key={quote.quoteId}>
                            <TableCell>
                              <div className="font-medium">
                                <Link
                                  href={`/crm/quotes?businessPartnerId=${businessPartnerId}&quoteId=${quote.quoteId}`}
                                  className="hover:underline"
                                >
                                  {quote.quoteName}
                                </Link>
                              </div>
                              <div className="text-xs text-muted-foreground">
                                {quote.opportunityName || `Opportunity ${quote.opportunityId}`}
                              </div>
                            </TableCell>
                            <TableCell>
                              <div className="flex flex-wrap gap-2">
                                <Badge variant={quote.quoteStatus === 'Accepted' ? 'default' : 'outline'}>
                                  {quote.quoteStatus}
                                </Badge>
                                {quote.isExpiringSoon ? <Badge variant="secondary">Expiring Soon</Badge> : null}
                              </div>
                            </TableCell>
                            <TableCell>{formatDate(quote.validUntil)}</TableCell>
                            <TableCell className="text-right">{formatMoney(quote.value, quote.currency)}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="delivery" className="space-y-4">
              <div className="grid gap-4 xl:grid-cols-2">
                <Card>
                  <CardHeader>
                    <CardTitle>Projects</CardTitle>
                    <CardDescription>Active delivery exposure already tied to this account.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {!account.projects.length ? (
                      <div className="py-10 text-center text-muted-foreground">No projects found.</div>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Project</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead>Target End</TableHead>
                            <TableHead className="text-right">Value</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                        {account.projects.map((project) => (
                          <TableRow key={project.projectId}>
                            <TableCell>
                              <div className="font-medium">
                                <Link href={`/crm/projects?businessPartnerId=${businessPartnerId}&projectId=${project.projectId}`} className="hover:underline">
                                  {project.title}
                                </Link>
                              </div>
                              <div className="text-xs text-muted-foreground">
                                {project.projectCode} | {project.relationshipType}
                              </div>
                            </TableCell>
                              <TableCell>
                                <Badge variant="outline">{project.status}</Badge>
                              </TableCell>
                              <TableCell>{formatDate(project.targetEndDate)}</TableCell>
                              <TableCell className="text-right">
                                <div>{formatMoney(project.value, account.currency || 'USD')}</div>
                                <div className="text-xs text-muted-foreground">{project.progressPercent.toFixed(0)}% complete</div>
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
                    <CardTitle>Contracts</CardTitle>
                    <CardDescription>Commercial commitments currently active or closing out.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {!account.contracts.length ? (
                      <div className="py-10 text-center text-muted-foreground">No contracts found.</div>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Contract</TableHead>
                            <TableHead>Status</TableHead>
                            <TableHead>End Date</TableHead>
                            <TableHead className="text-right">Value</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                        {account.contracts.map((contract) => (
                          <TableRow key={contract.contractId}>
                            <TableCell>
                              <div className="font-medium">
                                <Link href={`/crm/contracts?businessPartnerId=${businessPartnerId}&contractId=${contract.contractId}`} className="hover:underline">
                                  {contract.contractTitle}
                                </Link>
                              </div>
                              <div className="text-xs text-muted-foreground">
                                {contract.contractNumber} | {contract.relationshipType}
                              </div>
                            </TableCell>
                              <TableCell>
                                <Badge variant="outline">{contract.status}</Badge>
                              </TableCell>
                              <TableCell>{formatDate(contract.endDate)}</TableCell>
                              <TableCell className="text-right">
                                {formatMoney(contract.contractValue, account.currency || 'USD')}
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </CardContent>
                </Card>
              </div>

              <Card>
                <CardHeader>
                  <CardTitle>Tender Timeline</CardTitle>
                  <CardDescription>Invitation, bid, and award records reused directly from procurement.</CardDescription>
                </CardHeader>
                <CardContent>
                  {!account.tenders.length ? (
                    <div className="py-10 text-center text-muted-foreground">No tender context is linked yet.</div>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Reference</TableHead>
                          <TableHead>Entity</TableHead>
                          <TableHead>Status</TableHead>
                          <TableHead>Created</TableHead>
                          <TableHead className="text-right">Amount</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {account.tenders.map((tender) => (
                          <TableRow key={tender.entityId}>
                            <TableCell className="font-medium">
                              <Link
                                href={`/crm/tenders?businessPartnerId=${businessPartnerId}&tenderId=${tender.tenderId}&entityType=${tender.entityType}&entityId=${tender.entityId}`}
                                className="hover:underline"
                              >
                                {tender.referenceNumber}
                              </Link>
                            </TableCell>
                            <TableCell>{tender.entityType}</TableCell>
                            <TableCell>
                              <Badge variant="outline">{tender.status}</Badge>
                            </TableCell>
                            <TableCell>{formatDate(tender.createdAt)}</TableCell>
                            <TableCell className="text-right">
                              {formatMoney(tender.amount, account.currency || 'USD')}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="activity" className="space-y-4">
              <div className="grid gap-4 xl:grid-cols-2">
                <Card>
                  <CardHeader>
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <CardTitle>Contacts</CardTitle>
                        <CardDescription>Primary and supporting contacts already stored against the business partner.</CardDescription>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Button asChild size="sm" variant="outline">
                          <Link href={`/crm/contacts?businessPartnerId=${businessPartnerId}`}>
                            Open Workspace
                          </Link>
                        </Button>
                        <Button size="sm" onClick={openCreateContactDialog}>
                          <Plus className="mr-2 h-4 w-4" />
                          Add Contact
                        </Button>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    {!account.contacts.length ? (
                      <div className="space-y-4 py-10 text-center text-muted-foreground">
                        <div>No account contacts are available.</div>
                        <Button variant="outline" onClick={openCreateContactDialog}>
                          <Plus className="mr-2 h-4 w-4" />
                          Add First Contact
                        </Button>
                      </div>
                    ) : (
                      <div className="space-y-3">
                        {account.contacts.map((contact) => (
                          <div key={contact.contactId} className="rounded-lg border p-4">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">{contact.contactName}</div>
                                <div className="text-sm text-muted-foreground">
                                  {contact.contactTitle || 'No title'}{contact.department ? ` | ${contact.department}` : ''}
                                </div>
                              </div>
                              <div className="flex flex-wrap gap-2">
                                {contact.isPrimary ? <Badge>Primary</Badge> : <Badge variant="outline">Contact</Badge>}
                                {!contact.isPrimary ? (
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    disabled={contactSaving}
                                    onClick={() => void setPrimaryContact(contact.contactId)}
                                  >
                                    Make Primary
                                  </Button>
                                ) : null}
                              </div>
                            </div>
                            <div className="mt-3 grid gap-2 text-sm text-muted-foreground sm:grid-cols-2">
                              <div>{contact.email || 'No email provided'}</div>
                              <div>{contact.phone || contact.mobile || 'No phone provided'}</div>
                            </div>
                            <div className="mt-3 flex flex-wrap gap-2">
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => openEditContactDialog(contact)}
                              >
                                <Pencil className="mr-2 h-3.5 w-3.5" />
                                Edit
                              </Button>
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                  setSelectedContactId(contact.contactId);
                                  setDeleteContactOpen(true);
                                }}
                              >
                                <Trash2 className="mr-2 h-3.5 w-3.5" />
                                Delete
                              </Button>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Activity Queue</CardTitle>
                    <CardDescription>Recent activities and follow-up items touching this account.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {!account.activities.length ? (
                      <div className="py-10 text-center text-muted-foreground">No CRM activities are linked yet.</div>
                    ) : (
                      <div className="space-y-3">
                        {account.activities.map((activity) => (
                          <div key={activity.activityId} className="rounded-lg border p-4">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <div className="font-medium">
                                  <Link
                                    href={`/crm/activities?businessPartnerId=${businessPartnerId}&activityId=${activity.activityId}`}
                                    className="hover:underline"
                                  >
                                    {activity.subject}
                                  </Link>
                                </div>
                                <div className="text-sm text-muted-foreground">
                                  {activity.activityType} | {activity.activityStatus}
                                </div>
                                {activity.opportunityName || activity.leadName ? (
                                  <div className="text-xs text-muted-foreground">
                                    {[activity.opportunityName, activity.leadName].filter(Boolean).join(' | ')}
                                  </div>
                                ) : null}
                              </div>
                              <Badge variant={activity.requiresFollowUp ? 'secondary' : 'outline'}>
                                {activity.requiresFollowUp ? 'Follow-Up' : 'Tracked'}
                              </Badge>
                            </div>
                            <div className="mt-3 grid gap-2 text-sm text-muted-foreground sm:grid-cols-2">
                              <div>Activity date: {formatDate(activity.activityDate)}</div>
                              <div>Due date: {formatDate(activity.dueDate)}</div>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </CardContent>
                </Card>
              </div>
            </TabsContent>

            <TabsContent value="timeline" className="space-y-4">
              <div className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
                <Card>
                  <CardHeader>
                    <CardTitle>Timeline & Milestones</CardTitle>
                    <CardDescription>Cross-entity account journey stitched together from the current CRM-linked source records.</CardDescription>
                  </CardHeader>
                  <CardContent>
                    {!timelineItems.length ? (
                      <div className="py-10 text-center text-muted-foreground">No timeline items are available for this account yet.</div>
                    ) : (
                      <div className="space-y-3">
                        {timelineItems.map((item) => (
                          <div key={item.key} className="rounded-lg border p-4">
                            <div className="flex flex-wrap items-start justify-between gap-3">
                              <div className="space-y-1">
                                <div className="flex flex-wrap items-center gap-2">
                                  <Badge variant={item.badgeVariant}>{item.entityType}</Badge>
                                  <Link href={item.href} className="font-medium hover:underline">
                                    {item.title}
                                  </Link>
                                </div>
                                <div className="text-sm text-muted-foreground">{item.subtitle}</div>
                              </div>
                              <div className="text-right text-sm">
                                <div className="font-medium">{formatDate(item.dateValue)}</div>
                                <div className="text-xs text-muted-foreground">{item.dateLabel}</div>
                              </div>
                            </div>
                            {item.amount ? (
                              <div className="mt-3 text-sm text-muted-foreground">
                                Value {formatMoney(item.amount, item.currency || account.currency || 'USD')}
                              </div>
                            ) : null}
                          </div>
                        ))}
                      </div>
                    )}
                  </CardContent>
                </Card>

                <div className="space-y-4">
                  <Card>
                    <CardHeader>
                      <CardTitle>Upcoming</CardTitle>
                      <CardDescription>Near-term milestones that are still ahead of us.</CardDescription>
                    </CardHeader>
                    <CardContent>
                      {!upcomingTimelineItems.length ? (
                        <div className="py-10 text-center text-muted-foreground">No upcoming milestones are currently scheduled.</div>
                      ) : (
                        <div className="space-y-3">
                          {upcomingTimelineItems.map((item) => (
                            <div key={`${item.key}-upcoming`} className="flex items-start justify-between rounded-lg border p-3">
                              <div>
                                <div className="font-medium">{item.title}</div>
                                <div className="text-sm text-muted-foreground">{item.entityType} | {item.dateLabel}</div>
                              </div>
                              <div className="text-right text-sm">
                                <div>{formatDate(item.dateValue)}</div>
                                {item.amount ? (
                                  <div className="text-xs text-muted-foreground">
                                    {formatMoney(item.amount, item.currency || account.currency || 'USD')}
                                  </div>
                                ) : null}
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  <Card>
                    <CardHeader>
                      <CardTitle>Timeline Summary</CardTitle>
                      <CardDescription>Quick reading of this account’s commercial and delivery journey.</CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-3 text-sm">
                      <div className="flex items-center justify-between rounded-lg border p-3">
                        <span>Timeline items</span>
                        <span className="font-medium">{timelineItems.length}</span>
                      </div>
                      <div className="flex items-center justify-between rounded-lg border p-3">
                        <span>Open pipeline milestones</span>
                        <span className="font-medium">{account.opportunities.length + account.quotes.length}</span>
                      </div>
                      <div className="flex items-center justify-between rounded-lg border p-3">
                        <span>Delivery milestones</span>
                        <span className="font-medium">{account.projects.length + account.contracts.length}</span>
                      </div>
                      <div className="flex items-center justify-between rounded-lg border p-3">
                        <span>Follow-up items</span>
                        <span className="font-medium">{account.activities.filter((activity) => activity.requiresFollowUp).length}</span>
                      </div>
                      <div className="flex items-center justify-between rounded-lg border p-3">
                        <span>Next milestone</span>
                        <span className="font-medium">{formatDate(account.nextMilestoneDate)}</span>
                      </div>
                      <div className="rounded-lg border bg-muted/30 p-4 text-muted-foreground">
                        <div className="mb-2 flex items-center gap-2 font-medium text-foreground">
                          <Clock3 className="h-4 w-4" />
                          Why this matters
                        </div>
                        This timeline mixes recorded history with upcoming milestone dates so account teams can see momentum, risk, and renewal pressure in one place.
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </div>
            </TabsContent>

            <TabsContent value="health" className="space-y-4">
              <Card>
                <CardHeader>
                  <CardTitle>Health Signals</CardTitle>
                  <CardDescription>
                    Scoring is derived from live risk, performance, project, contract, quote, and follow-up signals.
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="grid gap-3 md:grid-cols-3">
                    <div className="rounded-lg border p-4">
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Health Score</div>
                      <div className="mt-2 text-2xl font-semibold">{formatScore(account.healthScore)}</div>
                    </div>
                    <div className="rounded-lg border p-4">
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Category</div>
                      <div className="mt-2 text-2xl font-semibold">{account.healthCategory}</div>
                    </div>
                    <div className="rounded-lg border p-4">
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Next Milestone</div>
                      <div className="mt-2 text-2xl font-semibold">{formatDate(account.nextMilestoneDate)}</div>
                    </div>
                  </div>

                  {!account.healthSignals.length ? (
                    <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
                      No health signals are available yet for this account.
                    </div>
                  ) : (
                    <div className="space-y-3">
                      {account.healthSignals.map((signal) => (
                        <div key={`${signal.label}-${signal.scoreImpact}`} className="flex items-center justify-between rounded-lg border p-4">
                          <div>
                            <div className="font-medium">{signal.label}</div>
                            <div className="text-sm text-muted-foreground">{signal.direction} signal</div>
                          </div>
                          <Badge variant={signal.scoreImpact < 0 ? 'destructive' : 'secondary'}>
                            {formatImpact(signal.scoreImpact)}
                          </Badge>
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>

          <ContactDialog
            open={contactDialogOpen}
            mode={contactFormMode}
            form={contactForm}
            saving={contactSaving}
            onOpenChange={setContactDialogOpen}
            onSubmit={() => void submitContact()}
            onChange={(field, value) => setContactForm((current) => ({ ...current, [field]: value }))}
          />

          <Dialog open={deleteContactOpen} onOpenChange={setDeleteContactOpen}>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Delete Account Contact</DialogTitle>
                <DialogDescription>
                  This removes the selected BusinessPartner contact from the CRM account.
                </DialogDescription>
              </DialogHeader>
              <DialogFooter>
                <Button variant="outline" onClick={() => setDeleteContactOpen(false)}>
                  Cancel
                </Button>
                <Button variant="destructive" onClick={() => void deleteContact()} disabled={contactSaving}>
                  {contactSaving ? 'Deleting...' : 'Delete Contact'}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </>
      ) : null}
    </div>
  );
}
