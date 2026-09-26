'use client';

import { hasCustomerRole, hasSupplierRole, hasContractorRole } from '@/lib/business-partner-roles';
import type { BusinessPartnerReceivablesDefaults } from '@/services/businessPartnerService';
import { BusinessPartnerReceivablesFields } from '@/components/procurement/BusinessPartnerReceivablesFields';


import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Save } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  businessPartnerService,
  type BusinessPartnerDetailDto,
  type UpdateBusinessPartnerDto,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  paymentTermService,
  procurementCurrencyService,
  type PaymentTermListDto,
  type CurrencyListDto,
} from '@/services/financeCommonService';
import {
  priceListService,
  type PriceListDto,
} from '@/services/priceListService';
import {
  emptyBusinessPartnerPostingDefaults,
  PartnerAccountsFields,
  PartnerCatalogueNotice,
  PartnerOptionsFields,
  PartnerTaxDefaultsFields,
  useBusinessPartnerPostingCatalogues,
} from '@/components/procurement/BusinessPartnerPostingFields';

const emptyForm: UpdateBusinessPartnerDto = {
  partnerName: '',
  tradingName: '',
  registrationNumber: '',
  taxNumber: '',
  email: '',
  phone: '',
  website: '',
  physicalAddress: '',
  city: '',
  country: '',
  postalCode: '',
  notes: '',
  status: 'Active',
  isPreferred: false,
  currency: '',
  paymentTerms: '',
  paymentTermId: '',
  priceList: '',
  parentId: '',
};

export default function EditBusinessPartnerPage() {
  const params = useParams();
  const router = useRouter();
  const id = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState('details');
  const [partner, setPartner] = useState<BusinessPartnerDetailDto | null>(null);
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermListDto[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [priceLists, setPriceLists] = useState<PriceListDto[]>([]);
  const [allPartners, setAllPartners] = useState<BusinessPartnerDto[]>([]);
  const [formData, setFormData] = useState<UpdateBusinessPartnerDto>(emptyForm);
  const [creditLimit, setCreditLimit] = useState('');
  const [receivablesDefaults, setReceivablesDefaults] = useState<BusinessPartnerReceivablesDefaults>({ defaultArAccountId: null });
  const [postingDefaults, setPostingDefaults] = useState(
    emptyBusinessPartnerPostingDefaults
  );
  const catalogues = useBusinessPartnerPostingCatalogues(partner?.partnerType);

  useEffect(() => {
    let current = true;
    setLoading(true);
    void Promise.all([
      businessPartnerService.getPartnerById(id),
      paymentTermService.getActive().catch(() => []),
      procurementCurrencyService.getActive().catch(() => []),
      businessPartnerService.getAllPartnersForDropdown().catch(() => []),
      priceListService.getActivePriceLists().catch(() => []),
    ])
      .then(([data, terms, currencyData, partners, lists]) => {
        if (!current) return;
        setPartner(data);
        setPaymentTerms(terms);
        setCurrencies(currencyData);
        setAllPartners(partners.filter((candidate) => candidate.id !== id));
        setPriceLists(lists);
        setFormData({
          partnerType: data.partnerType,
          partnerName: data.partnerName || data.companyName || '',
          tradingName: data.tradingName || '',
          registrationNumber: data.registrationNumber || '',
          taxNumber: data.taxNumber || '',
          email: data.email || '',
          phone: data.phone || '',
          website: data.website || '',
          physicalAddress: data.physicalAddress || '',
          city: data.city || '',
          country: data.country || '',
          postalCode: data.physicalPostalCode || '',
          notes: data.notes || '',
          status: data.status || 'Active',
          isPreferred: data.isPreferred,
          currency: data.currency || '',
          paymentTerms: data.paymentTerms || '',
          paymentTermId: data.paymentTermId || '',
          priceList: data.priceList || '',
          parentId: data.parentId || '',
        });
        setReceivablesDefaults(data.receivablesDefaults ?? { defaultArAccountId: null });
        setCreditLimit(
          data.creditLimit == null ? '' : String(data.creditLimit)
        );
        setPostingDefaults({
          ...emptyBusinessPartnerPostingDefaults(),
          ...data.postingDefaults,
        });
      })
      .catch((error) => {
        if (current)
          toast.error(
            error instanceof Error
              ? error.message
              : 'Failed to load business partner'
          );
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
    };
  }, [id]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!formData.partnerName.trim()) {
      setActiveTab('details');
      toast.error('Enter the company name.');
      return;
    }
    if (
      creditLimit !== '' &&
      (!Number.isFinite(Number(creditLimit)) || Number(creditLimit) < 0)
    ) {
      setActiveTab('options');
      toast.error('Credit Limit must be zero or greater.');
      return;
    }
    try {
      setSaving(true);
      await businessPartnerService.updatePartner(id, {
        ...formData,
        creditLimit: creditLimit === '' ? null : Number(creditLimit),
        postingDefaults,
        receivablesDefaults: hasCustomerRole(formData.partnerType) ? receivablesDefaults : undefined,
      });
      toast.success('Business partner updated successfully');
      router.push(`/procurement/business-partners/${id}`);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to update business partner'
      );
    } finally {
      setSaving(false);
    }
  };

  const textField = (
    field: keyof UpdateBusinessPartnerDto,
    label: string,
    type = 'text'
  ) => (
    <div className="space-y-1.5" key={field}>
      <Label htmlFor={field}>{label}</Label>
      <Input
        id={field}
        type={type}
        value={String(formData[field] ?? '')}
        disabled={saving}
        onChange={(event) =>
          setFormData((previous) => ({
            ...previous,
            [field]: event.target.value,
          }))
        }
      />
    </div>
  );

  if (loading)
    return (
      <div
        role="status"
        className="flex h-64 items-center justify-center text-muted-foreground"
      >
        Loading business partner...
      </div>
    );
  if (!partner)
    return (
      <div className="space-y-4 py-12 text-center">
        <p>Business partner could not be loaded.</p>
        <Button variant="outline" onClick={() => router.back()}>
          Back
        </Button>
      </div>
    );

  const hasPayables = hasSupplierRole(formData.partnerType) || hasContractorRole(formData.partnerType);

  return (
    <div className="mx-auto w-full max-w-5xl space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            size="icon"
            aria-label="Back"
            onClick={() => router.back()}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-semibold">Edit Business Partner</h1>
            <p className="text-sm text-muted-foreground">
              {partner.partnerCode} - {partner.partnerName}
            </p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            onClick={() => router.back()}
            disabled={saving}
          >
            Cancel
          </Button>
          <Button type="submit" form="business-partner-edit" disabled={saving}>
            <Save className="mr-2 h-4 w-4" />
            {saving ? 'Saving...' : 'Save'}
          </Button>
        </div>
      </div>
      <PartnerCatalogueNotice unavailable={catalogues.unavailable} />
      <form id="business-partner-edit" onSubmit={handleSubmit}>
        <Card>
          <CardContent className="p-4">
            <Tabs value={activeTab} onValueChange={setActiveTab}>
              <TabsList className={`grid h-auto w-full ${hasCustomerRole(formData.partnerType) && hasPayables ? 'grid-cols-5' : 'grid-cols-4'}`}>
                <TabsTrigger value="details">Details</TabsTrigger>
                <TabsTrigger value="contact">Contact</TabsTrigger>
                <TabsTrigger value="options">Options</TabsTrigger>
                {hasPayables && <TabsTrigger value="accounts">Accounts Payable</TabsTrigger>}
                {hasCustomerRole(formData.partnerType) && <TabsTrigger value="receivables">Accounts Receivable</TabsTrigger>}
              </TabsList>
              <div className="h-[min(620px,calc(100vh-250px))] min-h-80 overflow-y-auto px-1">
                <TabsContent value="receivables" className="py-4"><BusinessPartnerReceivablesFields value={receivablesDefaults} onChange={setReceivablesDefaults} accounts={catalogues.accounts} disabled={saving || catalogues.loading} /></TabsContent>
                <TabsContent value="details" className="space-y-5 py-3">
                  <div className="space-y-2">
                    <Label htmlFor="partner-role">Partner roles</Label>
                    <Select value={formData.partnerType} disabled={saving} onValueChange={(partnerType) => setFormData(previous => ({ ...previous, partnerType }))}>
                      <SelectTrigger id="partner-role"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value={partner.partnerType}>{partner.partnerType === 'CustomerAndSupplier' ? 'Supplier & Customer' : partner.partnerType}</SelectItem>
                        {['Supplier', 'Vendor', 'Manufacturer', 'Customer'].includes(partner.partnerType) && <SelectItem value="CustomerAndSupplier">Supplier & Customer</SelectItem>}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                    {textField('partnerName', 'Company Name *')}
                    {textField('tradingName', 'Trading Name')}
                    {textField('registrationNumber', 'Registration Number')}
                    <div className="space-y-1.5">
                      <Label htmlFor="status">Status</Label>
                      <Select
                        value={formData.status}
                        disabled={saving}
                        onValueChange={(status) =>
                          setFormData((previous) => ({ ...previous, status }))
                        }
                      >
                        <SelectTrigger id="status">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {partner.status && !['Active', 'Inactive', 'Suspended'].includes(partner.status) && (
                            <SelectItem value={partner.status}>{partner.status}</SelectItem>
                          )}
                          {['Active', 'Inactive', 'Suspended'].map((status) => (
                            <SelectItem key={status} value={status}>
                              {status}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1.5 md:col-span-2">
                      <Label htmlFor="parentId">Parent Business Partner</Label>
                      <Select
                        value={formData.parentId || '__none__'}
                        disabled={saving}
                        onValueChange={(parentId) =>
                          setFormData((previous) => ({
                            ...previous,
                            parentId: parentId === '__none__' ? '' : parentId,
                          }))
                        }
                      >
                        <SelectTrigger id="parentId">
                          <SelectValue placeholder="None" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">None</SelectItem>
                          {formData.parentId &&
                            !allPartners.some(
                              (candidate) => candidate.id === formData.parentId
                            ) && (
                              <SelectItem value={formData.parentId}>
                                {partner.parentName ||
                                  'Saved partner (unavailable)'}
                              </SelectItem>
                            )}
                          {allPartners.map((candidate) => (
                            <SelectItem key={candidate.id} value={candidate.id}>
                              {candidate.partnerCode} - {candidate.partnerName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                  <PartnerTaxDefaultsFields
                    partnerType={formData.partnerType}
                    value={postingDefaults}
                    onChange={setPostingDefaults}
                    taxGroups={catalogues.taxGroups}
                    withholdingTaxes={catalogues.withholdingTaxes}
                    disabled={saving}
                  />
                  <div className="space-y-1.5">
                    <Label htmlFor="notes">Notes</Label>
                    <Textarea
                      id="notes"
                      rows={3}
                      disabled={saving}
                      value={formData.notes}
                      onChange={(event) =>
                        setFormData((previous) => ({
                          ...previous,
                          notes: event.target.value,
                        }))
                      }
                    />
                  </div>
                </TabsContent>
                <TabsContent value="contact" className="space-y-4 py-3">
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                    {textField('email', 'Email', 'email')}
                    {textField('phone', 'Phone')}
                    {textField('website', 'Website')}
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="physicalAddress">Physical Address</Label>
                    <Textarea
                      id="physicalAddress"
                      rows={3}
                      disabled={saving}
                      value={formData.physicalAddress}
                      onChange={(event) =>
                        setFormData((previous) => ({
                          ...previous,
                          physicalAddress: event.target.value,
                        }))
                      }
                    />
                  </div>
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                    {textField('city', 'City')}
                    {textField('country', 'Country')}
                    {textField('postalCode', 'Postal Code')}
                  </div>
                </TabsContent>
                <TabsContent value="options" className="space-y-4 py-3">
                  <PartnerOptionsFields
                    value={postingDefaults}
                    onChange={setPostingDefaults}
                    options={{
                      paymentTermId: formData.paymentTermId || '',
                      taxNumber: formData.taxNumber || '',
                      creditLimit,
                    }}
                    onOptionsChange={(patch) => {
                      if (patch.creditLimit !== undefined)
                        setCreditLimit(patch.creditLimit);
                      setFormData((previous) => ({
                        ...previous,
                        ...(patch.paymentTermId !== undefined
                          ? { paymentTermId: patch.paymentTermId }
                          : {}),
                        ...(patch.taxNumber !== undefined
                          ? { taxNumber: patch.taxNumber }
                          : {}),
                      }));
                    }}
                    paymentTerms={paymentTerms}
                    partnerType={partner.partnerType}
                    bankAccounts={catalogues.bankAccounts}
                    disabled={saving}
                  />
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                    <div className="space-y-1.5">
                      <Label htmlFor="currency">Currency</Label>
                      <Select
                        value={formData.currency || ''}
                        disabled={saving}
                        onValueChange={(currency) =>
                          setFormData((previous) => ({ ...previous, currency }))
                        }
                      >
                        <SelectTrigger id="currency">
                          <SelectValue placeholder="Select currency" />
                        </SelectTrigger>
                        <SelectContent>
                          {formData.currency &&
                            !currencies.some(
                              (currency) => currency.code === formData.currency
                            ) && (
                              <SelectItem value={formData.currency}>
                                {formData.currency}
                              </SelectItem>
                            )}
                          {currencies.map((currency) => (
                            <SelectItem key={currency.id} value={currency.code}>
                              {currency.code} - {currency.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1.5">
                      <Label htmlFor="priceList">Price List</Label>
                      <Select
                        value={formData.priceList || '__none__'}
                        disabled={saving}
                        onValueChange={(priceList) =>
                          setFormData((previous) => ({
                            ...previous,
                            priceList:
                              priceList === '__none__' ? '' : priceList,
                          }))
                        }
                      >
                        <SelectTrigger id="priceList">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">None</SelectItem>
                          {formData.priceList &&
                            !priceLists.some(
                              (list) => list.id === formData.priceList
                            ) && (
                              <SelectItem value={formData.priceList}>
                                Saved price list (unavailable)
                              </SelectItem>
                            )}
                          {priceLists.map((list) => (
                            <SelectItem key={list.id} value={list.id}>
                              {list.priceListCode} - {list.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                </TabsContent>
                {hasPayables && <TabsContent value="accounts" className="py-3">
                  <PartnerAccountsFields
                    value={postingDefaults}
                    onChange={setPostingDefaults}
                    accounts={catalogues.accounts}
                    bankAccounts={catalogues.bankAccounts}
                    disabled={saving}
                  />
                </TabsContent>}
              </div>
            </Tabs>
          </CardContent>
        </Card>
      </form>
    </div>
  );
}
