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
import { businessPartnerService, type CreateBusinessPartnerContactDto } from '@/services/businessPartnerService';
import {
  crmService,
  type CrmAccountOverviewDto,
  type CrmContactListItemDto,
  type PagedResult,
} from '@/services/crmService';
import {
  AlertTriangle,
  ArrowRight,
  Building2,
  Mail,
  Pencil,
  Phone,
  Plus,
  RefreshCw,
  Search,
  Star,
  Trash2,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';

const PARTNER_TYPE_OPTIONS = ['Customer', 'Both', 'Vendor', 'Prospect'];

type ContactFormState = CreateBusinessPartnerContactDto & {
  businessPartnerId: string;
};

type AccountOption = Pick<CrmAccountOverviewDto, 'businessPartnerId' | 'partnerCode' | 'partnerName' | 'partnerType'>;

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString() : 'None';
const formatScore = (value: number) => `${value.toFixed(0)}/100`;
const getMessage = (error: unknown, fallback: string) => error instanceof Error ? error.message : fallback;
const toOptionalString = (value?: string) => value?.trim() ? value.trim() : undefined;

const createEmptyContactForm = (businessPartnerId: string = ''): ContactFormState => ({
  businessPartnerId,
  contactName: '',
  title: '',
  department: '',
  email: '',
  phone: '',
  mobile: '',
  isPrimary: false,
});

const mapContactToForm = (contact: CrmContactListItemDto): ContactFormState => ({
  businessPartnerId: contact.businessPartnerId,
  contactName: contact.contactName,
  title: contact.contactTitle || '',
  department: contact.department || '',
  email: contact.email || '',
  phone: contact.phone || '',
  mobile: contact.mobile || '',
  isPrimary: contact.isPrimary,
});

const buildContactPayload = (form: ContactFormState): CreateBusinessPartnerContactDto => ({
  contactName: form.contactName.trim(),
  title: toOptionalString(form.title),
  department: toOptionalString(form.department),
  email: toOptionalString(form.email),
  phone: toOptionalString(form.phone),
  mobile: toOptionalString(form.mobile),
  isPrimary: form.isPrimary,
});

async function loadAllCrmAccounts(): Promise<AccountOption[]> {
  const combined: AccountOption[] = [];
  let page = 1;
  let totalPages = 1;

  do {
    const response = await crmService.getAccounts({ page, pageSize: 100 });
    combined.push(...response.items.map((item) => ({
      businessPartnerId: item.businessPartnerId,
      partnerCode: item.partnerCode,
      partnerName: item.partnerName,
      partnerType: item.partnerType,
    })));

    totalPages = Math.max(1, Math.ceil(response.totalCount / response.pageSize));
    page += 1;
  } while (page <= totalPages);

  return combined.sort((left, right) => left.partnerName.localeCompare(right.partnerName));
}

function ContactDialog({
  open,
  mode,
  form,
  accounts,
  saving,
  onOpenChange,
  onSubmit,
  onChange,
}: {
  open: boolean;
  mode: 'create' | 'edit';
  form: ContactFormState;
  accounts: AccountOption[];
  saving: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onChange: <K extends keyof ContactFormState>(field: K, value: ContactFormState[K]) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{mode === 'create' ? 'Add CRM Contact' : 'Edit CRM Contact'}</DialogTitle>
          <DialogDescription>
            Contacts stay anchored to the existing BusinessPartner account record used across ERP and CRM.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            <Label>CRM Account</Label>
            <Select
              value={form.businessPartnerId || 'none'}
              onValueChange={(value) => onChange('businessPartnerId', value === 'none' ? '' : value)}
              disabled={mode === 'edit'}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select account" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Select account</SelectItem>
                {accounts.map((account) => (
                  <SelectItem key={account.businessPartnerId} value={account.businessPartnerId}>
                    {account.partnerName} | {account.partnerCode}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="crm-contact-name">Contact Name</Label>
            <Input
              id="crm-contact-name"
              value={form.contactName}
              onChange={(event) => onChange('contactName', event.target.value)}
              placeholder="Irene Mensah"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="crm-contact-title">Title</Label>
            <Input
              id="crm-contact-title"
              value={form.title || ''}
              onChange={(event) => onChange('title', event.target.value)}
              placeholder="Commercial Director"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="crm-contact-department">Department</Label>
            <Input
              id="crm-contact-department"
              value={form.department || ''}
              onChange={(event) => onChange('department', event.target.value)}
              placeholder="Commercial"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="crm-contact-email">Email</Label>
            <Input
              id="crm-contact-email"
              type="email"
              value={form.email || ''}
              onChange={(event) => onChange('email', event.target.value)}
              placeholder="irene@atlas.test"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="crm-contact-phone">Phone</Label>
            <Input
              id="crm-contact-phone"
              value={form.phone || ''}
              onChange={(event) => onChange('phone', event.target.value)}
              placeholder="+2335551000"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="crm-contact-mobile">Mobile</Label>
            <Input
              id="crm-contact-mobile"
              value={form.mobile || ''}
              onChange={(event) => onChange('mobile', event.target.value)}
              placeholder="+2335552000"
            />
          </div>

          <div className="flex items-center justify-between rounded-lg border px-4 py-3">
            <div>
              <div className="text-sm font-medium">Primary Contact</div>
              <div className="text-xs text-muted-foreground">Synchronize this person with the account summary.</div>
            </div>
            <Switch checked={form.isPrimary} onCheckedChange={(checked) => onChange('isPrimary', checked)} />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={onSubmit} disabled={saving}>
            {saving ? 'Saving...' : mode === 'create' ? 'Add Contact' : 'Save Changes'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function CrmContactsPage() {
  const searchParams = useSearchParams() ?? new URLSearchParams();
  const requestedContactId = searchParams.get('contactId') || '';
  const initialBusinessPartnerId = searchParams.get('businessPartnerId') || 'all';
  const initialPartnerType = searchParams.get('partnerType') || 'all';
  const initialDepartment = searchParams.get('department') || '';
  const initialPrimaryOnly = searchParams.get('primaryOnly') === 'true';
  const initialAtRiskOnly = searchParams.get('atRiskOnly') === 'true';

  const [search, setSearch] = useState(searchParams.get('search') || '');
  const [businessPartnerId, setBusinessPartnerId] = useState(initialBusinessPartnerId);
  const [partnerType, setPartnerType] = useState(initialPartnerType);
  const [department, setDepartment] = useState(initialDepartment);
  const [primaryOnly, setPrimaryOnly] = useState(initialPrimaryOnly);
  const [atRiskOnly, setAtRiskOnly] = useState(initialAtRiskOnly);
  const [page, setPage] = useState(1);

  const [result, setResult] = useState<PagedResult<CrmContactListItemDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [accountOptions, setAccountOptions] = useState<AccountOption[]>([]);
  const [accountOptionsLoading, setAccountOptionsLoading] = useState(true);
  const [selectedContactId, setSelectedContactId] = useState(requestedContactId);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create');
  const [contactForm, setContactForm] = useState<ContactFormState>(() => createEmptyContactForm(initialBusinessPartnerId === 'all' ? '' : initialBusinessPartnerId));
  const [saving, setSaving] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  const selectedContact = useMemo(
    () => result?.items.find((item) => item.contactId === selectedContactId) || null,
    [result, selectedContactId],
  );

  const selectedAccountName = useMemo(
    () => accountOptions.find((item) => item.businessPartnerId === businessPartnerId)?.partnerName || '',
    [accountOptions, businessPartnerId],
  );

  const departmentOptions = useMemo(
    () => Array.from(new Set(
      (result?.items || [])
        .map((item) => item.department)
        .filter((value): value is string => Boolean(value)),
    )).sort((left, right) => left.localeCompare(right)),
    [result],
  );

  const loadContacts = async (requestedPage: number = page) => {
    try {
      setLoading(true);
      const data = await crmService.getContacts({
        page: requestedPage,
        pageSize: 12,
        search: search || undefined,
        businessPartnerId: businessPartnerId === 'all' ? undefined : businessPartnerId,
        department: department || undefined,
        primaryOnly,
        atRiskOnly,
        partnerType: partnerType === 'all' ? undefined : partnerType,
      });

      setResult(data);

      if (requestedContactId && requestedPage === 1 && data.items.some((item) => item.contactId === requestedContactId)) {
        setSelectedContactId(requestedContactId);
        return;
      }

      if (selectedContactId && data.items.some((item) => item.contactId === selectedContactId)) {
        return;
      }

      setSelectedContactId(data.items[0]?.contactId || '');
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM contacts'));
    } finally {
      setLoading(false);
    }
  };

  const loadAccountOptions = async () => {
    try {
      setAccountOptionsLoading(true);
      setAccountOptions(await loadAllCrmAccounts());
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to load CRM account options'));
    } finally {
      setAccountOptionsLoading(false);
    }
  };

  useEffect(() => {
    void loadAccountOptions();
  }, []);

  useEffect(() => {
    void loadContacts(page);
  }, [page, businessPartnerId, department, partnerType, primaryOnly, atRiskOnly]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setPage(1);
      void loadContacts(1);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [search]);

  const metrics = useMemo(() => {
    const items = result?.items || [];

    return [
      {
        label: 'Visible Contacts',
        value: result?.totalCount.toLocaleString() || '0',
        hint: 'CRM account contacts under the current filters',
        icon: Users,
      },
      {
        label: 'Primary Contacts',
        value: items.filter((item) => item.isPrimary).length.toLocaleString(),
        hint: 'Contacts synchronized into account summaries',
        icon: Star,
      },
      {
        label: 'At-Risk Coverage',
        value: items.filter((item) => item.isAtRisk).length.toLocaleString(),
        hint: 'Contacts tied to accounts needing attention',
        icon: AlertTriangle,
      },
      {
        label: 'Direct Channels',
        value: items.filter((item) => item.email || item.phone || item.mobile).length.toLocaleString(),
        hint: 'Visible contacts with an immediate outreach channel',
        icon: Mail,
      },
    ];
  }, [result]);

  const openCreateDialog = () => {
    setDialogMode('create');
    setContactForm(createEmptyContactForm(
      businessPartnerId !== 'all'
        ? businessPartnerId
        : selectedContact?.businessPartnerId || accountOptions[0]?.businessPartnerId || '',
    ));
    setDialogOpen(true);
  };

  const openEditDialog = (contact: CrmContactListItemDto) => {
    setSelectedContactId(contact.contactId);
    setDialogMode('edit');
    setContactForm(mapContactToForm(contact));
    setDialogOpen(true);
  };

  const submitContact = async () => {
    if (!contactForm.businessPartnerId) {
      toast.error('Select a CRM account before saving the contact.');
      return;
    }

    if (!contactForm.contactName.trim()) {
      toast.error('Contact name is required.');
      return;
    }

    try {
      setSaving(true);
      const payload = buildContactPayload(contactForm);

      if (dialogMode === 'create') {
        const created = await businessPartnerService.createPartnerContact(contactForm.businessPartnerId, payload);
        setSelectedContactId(created.id);
        toast.success('CRM contact added.');
      } else if (selectedContact) {
        await businessPartnerService.updatePartnerContact(selectedContact.businessPartnerId, selectedContact.contactId, payload);
        setSelectedContactId(selectedContact.contactId);
        toast.success('CRM contact updated.');
      }

      setDialogOpen(false);
      await loadContacts(page);
    } catch (error: unknown) {
      toast.error(getMessage(error, `Failed to ${dialogMode} CRM contact`));
    } finally {
      setSaving(false);
    }
  };

  const deleteContact = async () => {
    if (!selectedContact) {
      return;
    }

    try {
      setSaving(true);
      await businessPartnerService.deletePartnerContact(selectedContact.businessPartnerId, selectedContact.contactId);
      toast.success('CRM contact removed.');
      setDeleteDialogOpen(false);
      await loadContacts(page);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to delete CRM contact'));
    } finally {
      setSaving(false);
    }
  };

  const setPrimaryContact = async (contact: CrmContactListItemDto) => {
    try {
      setSaving(true);
      await businessPartnerService.setPrimaryPartnerContact(contact.businessPartnerId, contact.contactId);
      toast.success('Primary contact updated.');
      setSelectedContactId(contact.contactId);
      await loadContacts(page);
    } catch (error: unknown) {
      toast.error(getMessage(error, 'Failed to update primary contact'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">CRM Contacts</h1>
          <p className="text-muted-foreground">
            Relationship workspace for the BusinessPartner contacts already powering CRM accounts.
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/crm/accounts">Open Accounts</Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/crm/activities">Open Activities</Link>
          </Button>
          <Button variant="outline" onClick={() => void loadContacts(page)}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={openCreateDialog} disabled={accountOptionsLoading || accountOptions.length === 0}>
            <Plus className="mr-2 h-4 w-4" />
            Add Contact
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

      {(businessPartnerId !== 'all' || partnerType !== 'all' || department || primaryOnly || atRiskOnly) ? (
        <Card>
          <CardHeader>
            <CardTitle>Scoped View</CardTitle>
            <CardDescription>This workspace is focused on a narrower contact portfolio.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-2">
            {businessPartnerId !== 'all' ? <Badge variant="secondary">Account: {selectedAccountName || 'Scoped account'}</Badge> : null}
            {partnerType !== 'all' ? <Badge variant="secondary">Partner Type: {partnerType}</Badge> : null}
            {department ? <Badge variant="secondary">Department: {department}</Badge> : null}
            {primaryOnly ? <Badge variant="secondary">Primary only</Badge> : null}
            {atRiskOnly ? <Badge variant="secondary">At-risk only</Badge> : null}
            <Button asChild variant="link" className="px-0">
              <Link href="/crm/contacts">Clear scope</Link>
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search people, account context, channel availability, and risk profile.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 xl:grid-cols-[1.4fr_1fr_0.8fr_0.9fr]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-9"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search contact, account, title, or channel"
              />
            </div>

            <Select value={businessPartnerId} onValueChange={(value) => {
              setPage(1);
              setBusinessPartnerId(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Filter by account" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All CRM accounts</SelectItem>
                {accountOptions.map((account) => (
                  <SelectItem key={account.businessPartnerId} value={account.businessPartnerId}>
                    {account.partnerName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={partnerType} onValueChange={(value) => {
              setPage(1);
              setPartnerType(value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Partner type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All partner types</SelectItem>
                {PARTNER_TYPE_OPTIONS.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={department || 'all'} onValueChange={(value) => {
              setPage(1);
              setDepartment(value === 'all' ? '' : value);
            }}>
              <SelectTrigger>
                <SelectValue placeholder="Department" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All departments</SelectItem>
                {departmentOptions.map((option) => (
                  <SelectItem key={option} value={option}>
                    {option}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="flex items-center justify-between rounded-lg border px-4 py-2">
              <div>
                <div className="text-sm font-medium">Primary Contacts</div>
                <div className="text-xs text-muted-foreground">Focus on the contact mirrored into account rollups.</div>
              </div>
              <Switch checked={primaryOnly} onCheckedChange={(checked) => {
                setPage(1);
                setPrimaryOnly(checked);
              }} />
            </div>

            <div className="flex items-center justify-between rounded-lg border px-4 py-2">
              <div>
                <div className="text-sm font-medium">At-Risk Accounts</div>
                <div className="text-xs text-muted-foreground">Only contacts linked to accounts needing attention.</div>
              </div>
              <Switch checked={atRiskOnly} onCheckedChange={(checked) => {
                setPage(1);
                setAtRiskOnly(checked);
              }} />
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Contact Register</CardTitle>
            <CardDescription>{result ? `${result.totalCount} contacts matched` : 'Loading CRM contacts'}</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? <div className="py-16 text-center text-muted-foreground">Loading CRM contacts...</div> : null}
            {!loading && !result?.items.length ? (
              <div className="py-16 text-center text-muted-foreground">No CRM contacts matched the current filters.</div>
            ) : null}
            {!loading && result?.items.length ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Contact</TableHead>
                    <TableHead>Account</TableHead>
                    <TableHead>Channels</TableHead>
                    <TableHead>Health</TableHead>
                    <TableHead>Context</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {result.items.map((contact) => (
                    <TableRow
                      key={contact.contactId}
                      className={`cursor-pointer ${selectedContactId === contact.contactId ? 'bg-muted/40' : ''}`}
                      onClick={() => setSelectedContactId(contact.contactId)}
                    >
                      <TableCell>
                        <div className="flex flex-wrap items-center gap-2">
                          <div className="font-medium">{contact.contactName}</div>
                          {contact.isPrimary ? <Badge>Primary</Badge> : <Badge variant="outline">Contact</Badge>}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {contact.contactTitle || 'No title'}{contact.department ? ` | ${contact.department}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="font-medium">{contact.partnerName}</div>
                        <div className="text-xs text-muted-foreground">
                          {contact.partnerCode} | {contact.partnerType}
                          {contact.salesTerritory ? ` | ${contact.salesTerritory}` : ''}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{contact.email || 'No email'}</div>
                        <div className="text-xs text-muted-foreground">{contact.phone || contact.mobile || 'No phone'}</div>
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant={contact.isAtRisk ? 'destructive' : 'outline'}>{contact.healthCategory}</Badge>
                          <Badge variant="secondary">{formatScore(contact.healthScore)}</Badge>
                        </div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          {contact.hasOpenFollowUp ? 'Follow-up open' : 'No open follow-up'}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{contact.openOpportunityCount} opps | {contact.activeContractCount} contracts</div>
                        <div className="text-xs text-muted-foreground">
                          {formatMoney(contact.openOpportunityValue)} | {contact.activeProjectCount} projects
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : null}

            {result?.totalCount && result.totalCount > result.pageSize ? (
              <div className="mt-4 flex items-center justify-between">
                <div className="text-sm text-muted-foreground">
                  Page {result.page} of {Math.max(1, Math.ceil(result.totalCount / result.pageSize))}
                </div>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    disabled={page <= 1 || loading}
                    onClick={() => setPage((current) => Math.max(1, current - 1))}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    disabled={!result || page >= Math.ceil(result.totalCount / result.pageSize) || loading}
                    onClick={() => setPage((current) => current + 1)}
                  >
                    Next
                  </Button>
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Contact Preview</CardTitle>
            <CardDescription>
              {selectedContact ? `${selectedContact.contactName} | ${selectedContact.partnerName}` : 'Select a contact from the register'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {!selectedContact && !loading ? (
              <div className="py-16 text-center text-muted-foreground">Select a CRM contact to inspect its account context.</div>
            ) : null}

            {selectedContact ? (
              <>
                <div className="rounded-xl border bg-muted/30 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">Contact</div>
                      <div className="mt-2 text-2xl font-semibold">{selectedContact.contactName}</div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {selectedContact.contactTitle || 'No title'}
                        {selectedContact.department ? ` | ${selectedContact.department}` : ''}
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {selectedContact.isPrimary ? <Badge>Primary</Badge> : <Badge variant="outline">Supporting</Badge>}
                      <Badge variant={selectedContact.isAtRisk ? 'destructive' : 'secondary'}>
                        {selectedContact.healthCategory}
                      </Badge>
                    </div>
                  </div>
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border p-4">
                    <div className="mb-2 flex items-center gap-2 text-xs uppercase tracking-wide text-muted-foreground">
                      <Mail className="h-4 w-4" />
                      Email
                    </div>
                    <div className="font-medium">{selectedContact.email || 'No email provided'}</div>
                  </div>

                  <div className="rounded-lg border p-4">
                    <div className="mb-2 flex items-center gap-2 text-xs uppercase tracking-wide text-muted-foreground">
                      <Phone className="h-4 w-4" />
                      Phone
                    </div>
                    <div className="font-medium">{selectedContact.phone || selectedContact.mobile || 'No phone provided'}</div>
                  </div>
                </div>

                <div className="rounded-lg border p-4">
                  <div className="mb-3 flex items-center gap-2 font-medium">
                    <Building2 className="h-4 w-4 text-muted-foreground" />
                    Account Context
                  </div>
                  <div className="space-y-2 text-sm">
                    <div className="flex items-center justify-between gap-3">
                      <span>Account</span>
                      <span>{selectedContact.partnerName}</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Pipeline</span>
                      <span>{selectedContact.openOpportunityCount} opps | {formatMoney(selectedContact.openOpportunityValue)}</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Delivery</span>
                      <span>{selectedContact.activeProjectCount} projects | {selectedContact.activeContractCount} contracts</span>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <span>Next milestone</span>
                      <span>{formatDate(selectedContact.nextMilestoneDate)}</span>
                    </div>
                  </div>
                </div>

                <div className="rounded-lg border p-4">
                  <div className="text-xs uppercase tracking-wide text-muted-foreground">Source of truth</div>
                  <div className="mt-2 text-sm text-muted-foreground">
                    This contact is managed against the existing BusinessPartner record and surfaced in CRM without duplicating master data.
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button asChild>
                    <Link href={`/crm/accounts/${selectedContact.businessPartnerId}`}>
                      Open Account
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/opportunities?businessPartnerId=${selectedContact.businessPartnerId}`}>
                      Pipeline
                    </Link>
                  </Button>
                  <Button asChild variant="outline">
                    <Link href={`/crm/activities?businessPartnerId=${selectedContact.businessPartnerId}`}>
                      Activities
                    </Link>
                  </Button>
                </div>

                <div className="flex flex-wrap gap-2 border-t pt-4">
                  <Button variant="outline" onClick={() => openEditDialog(selectedContact)} disabled={saving}>
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit
                  </Button>
                  {!selectedContact.isPrimary ? (
                    <Button variant="outline" onClick={() => void setPrimaryContact(selectedContact)} disabled={saving}>
                      <Star className="mr-2 h-4 w-4" />
                      Set Primary
                    </Button>
                  ) : null}
                  <Button variant="destructive" onClick={() => setDeleteDialogOpen(true)} disabled={saving}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Delete
                  </Button>
                </div>
              </>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <ContactDialog
        open={dialogOpen}
        mode={dialogMode}
        form={contactForm}
        accounts={accountOptions}
        saving={saving}
        onOpenChange={setDialogOpen}
        onSubmit={() => void submitContact()}
        onChange={(field, value) => setContactForm((current) => ({ ...current, [field]: value }))}
      />

      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete CRM Contact</DialogTitle>
            <DialogDescription>
              This removes the selected BusinessPartner contact from the CRM account.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteDialogOpen(false)} disabled={saving}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={() => void deleteContact()} disabled={saving}>
              {saving ? 'Deleting...' : 'Delete Contact'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
