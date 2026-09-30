'use client';

import Link from 'next/link';
import React from 'react';
import { Banknote, Building2, ExternalLink, FileText, MoreHorizontal, RefreshCw, Search } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  estateFacilitiesService,
  type FacilitiesPropertyOption,
  type FacilitiesProviderAssignment,
  type FacilitiesProviderAssignmentRequest,
  type FacilitiesProviderInvoice,
  type FacilitiesProviderOption,
  type FacilitiesProviderRate,
} from '@/services/estate-facilities.service';
import { FacilitiesDutyLookup } from './FacilitiesDutyLookup';
import { FacilitiesProviderRates } from './FacilitiesProviderRates';

const PAGE_SIZE = 20;
const todayInputValue = () => new Date().toISOString().slice(0, 10);
const billingFrequencies = ['OnDemand', 'Weekly', 'Monthly', 'Quarterly', 'SemiAnnual', 'Annual'];

const emptyAssignmentForm = (): FacilitiesProviderAssignmentRequest => ({
  estateManagedAssetId: '',
  contractId: null,
  providerRateId: null,
  serviceScope: '',
  serviceArea: '',
  assignmentStatus: 'Active',
  effectiveFrom: todayInputValue(),
  effectiveTo: '',
  schedulePattern: '',
  billingFrequency: 'Monthly',
  billingQuantity: 1,
  nextInvoiceDate: todayInputValue(),
  lastInvoiceDate: '',
  supervisorName: '',
  slaReference: '',
  notes: '',
});

export function expiringProviderContracts(provider: FacilitiesProviderOption, today = new Date()) {
  const current = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
  return provider.contracts.filter((contract) => {
    if (!contract.endDate) return false;
    const end = new Date(`${contract.endDate.slice(0, 10)}T00:00:00Z`);
    const days = Math.round((end.getTime() - current) / 86_400_000);
    return Number.isFinite(days) && days >= 0 && days <= 30;
  });
}

function supplierInvoiceHref(provider: FacilitiesProviderOption | null, assignment?: FacilitiesProviderAssignment) {
  if (!provider) return '/procurement/supplier-invoices/create';
  const params = new URLSearchParams({ businessPartnerId: provider.id });
  if (assignment?.providerRateCurrency) params.set('currencyCode', assignment.providerRateCurrency);
  if (assignment?.providerRate != null) params.set('unitPrice', String(assignment.providerRate));
  if (assignment?.billingQuantity != null) params.set('quantity', String(assignment.billingQuantity || 1));
  if (assignment) {
    const facility = [assignment.assetCode, assignment.assetName].filter(Boolean).join(' - ');
    const details = [assignment.serviceScope, assignment.serviceArea, facility].filter(Boolean).join(' | ');
    params.set('lineDescription', details || 'Facilities provider service');
    params.set('notes', `Facilities provider assignment${facility ? `: ${facility}` : ''}`);
  }
  return `/procurement/supplier-invoices/create?${params.toString()}`;
}

export function FacilitiesProviderRegister() {
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canManageRates = hasAnyRole(['admin', 'Admin', 'SystemAdmin', 'SuperAdmin', 'TenantAdmin', 'Estate Manager', 'Facilities Manager']);
  const [providers, setProviders] = React.useState<FacilitiesProviderOption[]>([]);
  const [search, setSearch] = React.useState('');
  const [expiringOnly, setExpiringOnly] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [invoiceProvider, setInvoiceProvider] = React.useState<FacilitiesProviderOption | null>(null);
  const [rateProvider, setRateProvider] = React.useState<FacilitiesProviderOption | null>(null);
  const [assignmentProvider, setAssignmentProvider] = React.useState<FacilitiesProviderOption | null>(null);
  const [invoices, setInvoices] = React.useState<FacilitiesProviderInvoice[]>([]);
  const [assignments, setAssignments] = React.useState<FacilitiesProviderAssignment[]>([]);
  const [assignmentRates, setAssignmentRates] = React.useState<FacilitiesProviderRate[]>([]);
  const [assignmentForm, setAssignmentForm] = React.useState<FacilitiesProviderAssignmentRequest>(emptyAssignmentForm);
  const [assignmentAssetLabel, setAssignmentAssetLabel] = React.useState('');
  const [invoiceLoading, setInvoiceLoading] = React.useState(false);
  const [invoiceError, setInvoiceError] = React.useState<string | null>(null);
  const [assignmentLoading, setAssignmentLoading] = React.useState(false);
  const [assignmentSaving, setAssignmentSaving] = React.useState(false);
  const [assignmentError, setAssignmentError] = React.useState<string | null>(null);

  const openInvoices = async (provider: FacilitiesProviderOption) => {
    setInvoiceProvider(provider);
    setInvoices([]);
    setInvoiceError(null);
    setInvoiceLoading(true);
    try {
      setInvoices(await estateFacilitiesService.getProviderInvoices(provider.id));
    } catch {
      setInvoiceError('Unable to load provider invoices.');
    } finally {
      setInvoiceLoading(false);
    }
  };

  const openAssignments = async (provider: FacilitiesProviderOption) => {
    setAssignmentProvider(provider);
    setAssignments([]);
    setAssignmentRates([]);
    setAssignmentForm({
      ...emptyAssignmentForm(),
      contractId: provider.contracts[0]?.id ?? null,
    });
    setAssignmentAssetLabel('');
    setAssignmentError(null);
    setAssignmentLoading(true);
    try {
      const [providerAssignments, providerRates] = await Promise.all([
        estateFacilitiesService.getProviderAssignments(provider.id),
        estateFacilitiesService.getProviderRates(provider.id),
      ]);
      setAssignments(providerAssignments);
      setAssignmentRates(providerRates);
    } catch {
      setAssignmentError('Unable to load provider assignments.');
    } finally {
      setAssignmentLoading(false);
    }
  };

  const saveAssignment = async () => {
    if (!assignmentProvider) return;
    setAssignmentSaving(true);
    setAssignmentError(null);
    try {
      await estateFacilitiesService.createProviderAssignment(assignmentProvider.id, {
        ...assignmentForm,
        contractId: assignmentForm.contractId || null,
        providerRateId: assignmentForm.providerRateId || null,
        effectiveTo: assignmentForm.effectiveTo || null,
        billingFrequency: assignmentForm.billingFrequency || null,
        billingQuantity: assignmentForm.billingQuantity || 1,
        nextInvoiceDate: assignmentForm.nextInvoiceDate || null,
        lastInvoiceDate: assignmentForm.lastInvoiceDate || null,
      });
      setAssignments(await estateFacilitiesService.getProviderAssignments(assignmentProvider.id));
      setAssignmentForm({
        ...emptyAssignmentForm(),
        contractId: assignmentProvider.contracts[0]?.id ?? null,
      });
      setAssignmentAssetLabel('');
    } catch (error) {
      setAssignmentError(error instanceof Error ? error.message : 'Unable to save provider assignment.');
    } finally {
      setAssignmentSaving(false);
    }
  };

  const loadProviders = React.useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setProviders(await estateFacilitiesService.getApprovedProviders());
    } catch {
      setError('Unable to load approved service providers.');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void loadProviders();
  }, [loadProviders]);

  const query = search.trim().toLowerCase();
  const filtered = providers.filter((provider) =>
    (!expiringOnly || expiringProviderContracts(provider).length > 0) &&
    [provider.partnerCode, provider.partnerName,
      ...(provider.categories ?? [])].some((value) => value?.toLowerCase().includes(query))
  );
  const expiringCount = providers.filter((provider) => expiringProviderContracts(provider).length > 0).length;
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const visible = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <section className="space-y-3" aria-label="Approved service providers">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">Approved service providers</h2>
        {expiringCount > 0 ? <Badge variant="destructive">{expiringCount} contracts nearing expiry</Badge> : null}
        <Button size="sm" variant="outline" onClick={() => void loadProviders()} disabled={loading} title="Refresh providers">
          <RefreshCw className="h-4 w-4" />
          <span className="sr-only">Refresh providers</span>
        </Button>
      </div>
      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input aria-label="Search providers" placeholder="Search provider or category" value={search}
          onChange={(event) => { setSearch(event.target.value); setPage(1); }} className="pl-9" />
      </div>
      <Button size="sm" variant={expiringOnly ? 'default' : 'outline'} aria-pressed={expiringOnly}
        onClick={() => { setExpiringOnly(!expiringOnly); setPage(1); }}>Expiring contracts</Button>
      {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
      <div className="overflow-x-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Provider</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Categories</TableHead>
              <TableHead>Active contracts</TableHead>
              <TableHead>Rating</TableHead>
              <TableHead>Contact</TableHead>
              <TableHead className="w-12"><span className="sr-only">Actions</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {loading ? (
              <TableRow><TableCell colSpan={7}>Loading providers...</TableCell></TableRow>
            ) : visible.length === 0 ? (
              <TableRow><TableCell colSpan={7}>No approved providers found.</TableCell></TableRow>
            ) : visible.map((provider) => (
              <TableRow key={provider.id}>
                <TableCell><div className="font-medium">{provider.partnerName}</div><div className="text-xs text-muted-foreground">{provider.partnerCode}</div></TableCell>
                <TableCell>{provider.partnerType}</TableCell>
                <TableCell>{provider.categories?.join(', ') || '-'}</TableCell>
                <TableCell>{provider.contracts.length ? provider.contracts.map((item) => (
                  <div key={item.id} className="flex flex-wrap items-center gap-1">
                    <span title={item.contractTitle}>{item.contractNumber}</span>
                    <span className="text-xs text-muted-foreground">{item.currency} {item.contractValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
                    {item.paymentTerms ? <span className="text-xs text-muted-foreground" title="Payment terms">{item.paymentTerms}</span> : null}
                    {item.endDate ? <span className="text-xs text-muted-foreground">ends {new Date(item.endDate).toLocaleDateString()}</span> : null}
                    {expiringProviderContracts(provider).some((contract) => contract.id === item.id)
                      ? <Badge variant="destructive">Soon</Badge> : null}
                  </div>
                )) : '-'}</TableCell>
                <TableCell>{provider.performanceRating == null ? '-' : provider.performanceRating.toFixed(1)}</TableCell>
                <TableCell>{provider.phone || provider.email || '-'}</TableCell>
                <TableCell className="text-right">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button size="icon" variant="ghost" title={`Actions for ${provider.partnerName}`}
                        aria-label={`Actions for ${provider.partnerName}`}>
                        <MoreHorizontal className="h-4 w-4" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end" className="w-48">
                      <DropdownMenuLabel>Actions</DropdownMenuLabel>
                      <DropdownMenuItem onClick={() => setRateProvider(provider)}>
                        <Banknote className="mr-2 h-4 w-4" />
                        Rates
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => void openAssignments(provider)}>
                        <Building2 className="mr-2 h-4 w-4" />
                        Assign facility
                      </DropdownMenuItem>
                      <DropdownMenuItem asChild>
                        <Link href={supplierInvoiceHref(provider)}>
                          <FileText className="mr-2 h-4 w-4" />
                          Raise supplier invoice
                        </Link>
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => void openInvoices(provider)}>
                        <FileText className="mr-2 h-4 w-4" />
                        View supplier invoices
                      </DropdownMenuItem>
                      {hasAnyPermission(['procurement.records.read']) ? (
                        <>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem asChild>
                            <Link href={`/procurement/business-partners/${provider.id}`}>
                              <ExternalLink className="mr-2 h-4 w-4" />
                              Details
                            </Link>
                          </DropdownMenuItem>
                        </>
                      ) : null}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {totalPages > 1 ? (
        <div className="flex items-center justify-end gap-2 text-sm">
          <Button size="sm" variant="outline" disabled={currentPage === 1} onClick={() => setPage(currentPage - 1)}>Previous</Button>
          <span>{currentPage} of {totalPages}</span>
          <Button size="sm" variant="outline" disabled={currentPage === totalPages} onClick={() => setPage(currentPage + 1)}>Next</Button>
        </div>
      ) : null}
      <Dialog open={invoiceProvider !== null} onOpenChange={(open) => { if (!open) setInvoiceProvider(null); }}>
        <DialogContent className="max-w-4xl" aria-describedby={undefined}>
          <DialogHeader><DialogTitle>{invoiceProvider?.partnerName} invoices</DialogTitle></DialogHeader>
          {invoiceError ? <p role="alert" className="text-sm text-destructive">{invoiceError}</p> : null}
          <div className="max-h-[60vh] overflow-auto rounded-md border">
            <Table>
              <TableHeader><TableRow>
                <TableHead>Invoice</TableHead><TableHead>Date</TableHead><TableHead>Purchase order</TableHead>
                <TableHead>Status</TableHead><TableHead className="text-right">Amount</TableHead>
                <TableHead className="text-right">Outstanding</TableHead>
              </TableRow></TableHeader>
              <TableBody>
                {invoiceLoading ? <TableRow><TableCell colSpan={6}>Loading invoices...</TableCell></TableRow>
                  : invoices.length === 0 ? <TableRow><TableCell colSpan={6}>No supplier invoices are linked to this provider.</TableCell></TableRow>
                    : invoices.map((invoice) => (
                      <TableRow key={invoice.id}>
                        <TableCell>{hasAnyPermission(['procurement.records.read']) ? <Link className="underline" href={`/procurement/supplier-invoices/${invoice.id}`}>{invoice.invoiceNumber}</Link> : invoice.invoiceNumber}</TableCell>
                        <TableCell>{new Date(invoice.invoiceDate).toLocaleDateString()}</TableCell>
                        <TableCell>{invoice.purchaseOrderId ? 'Linked' : 'Direct invoice'}</TableCell>
                        <TableCell>{typeof invoice.status === 'number' ? ['Unknown', 'Draft', 'Pending approval', 'Approved', 'Partially paid', 'Paid', 'Overdue', 'Voided', 'Rejected', 'On hold'][invoice.status] ?? 'Unknown' : invoice.status}</TableCell>
                        <TableCell className="text-right">{invoice.currencyCode} {invoice.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</TableCell>
                        <TableCell className="text-right">{invoice.currencyCode} {Math.max(0, invoice.totalAmount - invoice.paidAmount).toLocaleString(undefined, { minimumFractionDigits: 2 })}</TableCell>
                      </TableRow>
                    ))}
              </TableBody>
            </Table>
          </div>
        </DialogContent>
      </Dialog>
      <Dialog open={assignmentProvider !== null} onOpenChange={(open) => { if (!open) setAssignmentProvider(null); }}>
        <DialogContent className="max-w-5xl" aria-describedby={undefined}>
          <DialogHeader><DialogTitle>Assign {assignmentProvider?.partnerName} to facility</DialogTitle></DialogHeader>
          {assignmentError ? <p role="alert" className="text-sm text-destructive">{assignmentError}</p> : null}
          <div className="grid gap-4 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
            <div className="space-y-3 rounded-md border p-4">
              <div className="grid gap-2">
                <Label>Facility / property / site</Label>
                <FacilitiesDutyLookup<FacilitiesPropertyOption>
                  label="facility"
                  selectedLabel={assignmentAssetLabel}
                  search={(query) => estateFacilitiesService.searchDutyProperties(query)}
                  describe={(option) => ({
                    title: `${option.assetCode} - ${option.name}`,
                    detail: [option.location, option.projectCode, option.projectTitle].filter(Boolean).join(' | '),
                  })}
                  onSelect={(option) => {
                    setAssignmentForm((current) => ({ ...current, estateManagedAssetId: option.id }));
                    setAssignmentAssetLabel(`${option.assetCode} - ${option.name}`);
                  }}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="provider-assignment-contract">Contract</Label>
                <Select value={assignmentForm.contractId || 'none'} onValueChange={(value) => setAssignmentForm((current) => ({ ...current, contractId: value === 'none' ? null : value }))}>
                  <SelectTrigger id="provider-assignment-contract"><SelectValue placeholder="Select contract" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No contract selected</SelectItem>
                    {(assignmentProvider?.contracts ?? []).map((contract) => (
                      <SelectItem key={contract.id} value={contract.id}>{contract.contractNumber} - {contract.contractTitle}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="provider-assignment-rate">Provider rate</Label>
                <Select value={assignmentForm.providerRateId || 'none'} onValueChange={(value) => {
                  const selectedRate = assignmentRates.find((rate) => rate.id === value);
                  setAssignmentForm((current) => ({
                    ...current,
                    providerRateId: value === 'none' ? null : value,
                    contractId: selectedRate?.contractId ?? current.contractId,
                    serviceScope: current.serviceScope || selectedRate?.serviceName || '',
                  }));
                }}>
                  <SelectTrigger id="provider-assignment-rate"><SelectValue placeholder="Select service rate" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No rate selected</SelectItem>
                    {assignmentRates.map((rate) => (
                      <SelectItem key={rate.id} value={rate.id}>
                        {rate.serviceName} - {rate.currency} {rate.rate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })} / {rate.unitOfMeasure}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="provider-assignment-scope">Service scope</Label>
                <Input id="provider-assignment-scope" value={assignmentForm.serviceScope}
                  onChange={(event) => setAssignmentForm((current) => ({ ...current, serviceScope: event.target.value }))}
                  placeholder="Cleaning, security, landscaping, lift servicing..." />
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-area">Service area</Label>
                  <Input id="provider-assignment-area" value={assignmentForm.serviceArea || ''}
                    onChange={(event) => setAssignmentForm((current) => ({ ...current, serviceArea: event.target.value }))}
                    placeholder="Block A, compound, car park..." />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-status">Status</Label>
                  <Select value={assignmentForm.assignmentStatus} onValueChange={(value) => setAssignmentForm((current) => ({ ...current, assignmentStatus: value }))}>
                    <SelectTrigger id="provider-assignment-status"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Active">Active</SelectItem>
                      <SelectItem value="Suspended">Suspended</SelectItem>
                      <SelectItem value="Ended">Ended</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-from">Effective from</Label>
                  <Input id="provider-assignment-from" type="date" value={assignmentForm.effectiveFrom}
                    onChange={(event) => setAssignmentForm((current) => ({ ...current, effectiveFrom: event.target.value }))} />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-to">Effective to</Label>
                  <Input id="provider-assignment-to" type="date" value={assignmentForm.effectiveTo || ''}
                    onChange={(event) => setAssignmentForm((current) => ({ ...current, effectiveTo: event.target.value }))} />
                </div>
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-schedule">Schedule</Label>
                  <Input id="provider-assignment-schedule" value={assignmentForm.schedulePattern || ''}
                    onChange={(event) => setAssignmentForm((current) => ({ ...current, schedulePattern: event.target.value }))}
                    placeholder="Mon-Fri 07:00-17:00" />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-supervisor">Supervisor</Label>
                  <Input id="provider-assignment-supervisor" value={assignmentForm.supervisorName || ''}
                    onChange={(event) => setAssignmentForm((current) => ({ ...current, supervisorName: event.target.value }))} />
                </div>
              </div>
              <div className="grid gap-2 sm:grid-cols-3">
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-frequency">Billing frequency</Label>
                  <Select value={assignmentForm.billingFrequency || 'OnDemand'} onValueChange={(value) => setAssignmentForm((current) => ({ ...current, billingFrequency: value }))}>
                    <SelectTrigger id="provider-assignment-frequency"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {billingFrequencies.map((frequency) => <SelectItem key={frequency} value={frequency}>{frequency}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-quantity">Billing quantity</Label>
                  <Input id="provider-assignment-quantity" type="number" min="0.0001" step="0.0001"
                    value={assignmentForm.billingQuantity ?? 1}
                    onChange={(event) => setAssignmentForm((current) => ({ ...current, billingQuantity: Number(event.target.value) || 1 }))} />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="provider-assignment-next-invoice">Next invoice</Label>
                  <Input id="provider-assignment-next-invoice" type="date" value={assignmentForm.nextInvoiceDate || ''}
                    onChange={(event) => setAssignmentForm((current) => ({ ...current, nextInvoiceDate: event.target.value }))} />
                </div>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="provider-assignment-notes">Notes</Label>
                <Textarea id="provider-assignment-notes" value={assignmentForm.notes || ''}
                  onChange={(event) => setAssignmentForm((current) => ({ ...current, notes: event.target.value }))} />
              </div>
              <Button onClick={() => void saveAssignment()} disabled={assignmentSaving || !assignmentForm.estateManagedAssetId || !assignmentForm.serviceScope.trim()}>
                {assignmentSaving ? 'Saving...' : 'Assign provider'}
              </Button>
            </div>
            <div className="max-h-[70vh] overflow-auto rounded-md border">
              <Table>
                <TableHeader><TableRow>
                  <TableHead>Facility</TableHead><TableHead>Scope</TableHead><TableHead>Billing</TableHead><TableHead>Contract</TableHead><TableHead>Status</TableHead><TableHead>Dates</TableHead><TableHead className="w-12"><span className="sr-only">Invoice</span></TableHead>
                </TableRow></TableHeader>
                <TableBody>
                  {assignmentLoading ? <TableRow><TableCell colSpan={7}>Loading assignments...</TableCell></TableRow>
                    : assignments.length === 0 ? <TableRow><TableCell colSpan={7}>No facility assignments yet.</TableCell></TableRow>
                      : assignments.map((assignment) => (
                        <TableRow key={assignment.id}>
                          <TableCell><div className="font-medium">{assignment.assetCode || '-'}</div><div className="text-xs text-muted-foreground">{assignment.assetName || assignment.assetLocation || '-'}</div></TableCell>
                          <TableCell><div>{assignment.serviceScope}</div>{assignment.serviceArea ? <div className="text-xs text-muted-foreground">{assignment.serviceArea}</div> : null}</TableCell>
                          <TableCell>
                            <div>{assignment.billingFrequency || 'On demand'}</div>
                            <div className="text-xs text-muted-foreground">
                              {assignment.providerRate != null && assignment.providerRateCurrency
                                ? `${assignment.billingQuantity || 1} x ${assignment.providerRateCurrency} ${assignment.providerRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`
                                : 'No rate linked'}
                            </div>
                            {assignment.nextInvoiceDate ? <div className="text-xs text-muted-foreground">next {new Date(assignment.nextInvoiceDate).toLocaleDateString()}</div> : null}
                          </TableCell>
                          <TableCell>{assignment.contractNumber || '-'}</TableCell>
                          <TableCell><Badge variant={assignment.assignmentStatus === 'Active' ? 'default' : 'secondary'}>{assignment.assignmentStatus}</Badge></TableCell>
                          <TableCell><span className="text-xs">{new Date(assignment.effectiveFrom).toLocaleDateString()} {assignment.effectiveTo ? `- ${new Date(assignment.effectiveTo).toLocaleDateString()}` : 'onward'}</span></TableCell>
                          <TableCell className="text-right">
                            {assignment.providerRate ? (
                              <Button size="icon" variant="ghost" asChild title="Raise supplier invoice">
                                <Link href={supplierInvoiceHref(assignmentProvider, assignment)}>
                                  <FileText className="h-4 w-4" />
                                  <span className="sr-only">Raise supplier invoice</span>
                                </Link>
                              </Button>
                            ) : (
                              <Button size="icon" variant="ghost" disabled title="Link a provider rate first">
                                <FileText className="h-4 w-4" />
                                <span className="sr-only">Link a provider rate first</span>
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                </TableBody>
              </Table>
            </div>
          </div>
        </DialogContent>
      </Dialog>
      <FacilitiesProviderRates provider={rateProvider} canManage={canManageRates} onClose={() => setRateProvider(null)} />
    </section>
  );
}
