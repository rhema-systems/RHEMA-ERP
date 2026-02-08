'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ArrowLeft, Save, Building2 } from 'lucide-react';
import { toast } from 'sonner';
import { businessPartnerService, BusinessPartnerDetailDto, UpdateBusinessPartnerDto, BusinessPartnerDto } from '@/services/businessPartnerService';
import { paymentTermService, currencyService } from '@/services/financeCommonService';
import type { PaymentTermListDto, CurrencyListDto } from '@/services/financeCommonService';
import { priceListService, PriceListDto, PriceListType } from '@/services/priceListService';

export default function EditBusinessPartnerPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [partner, setPartner] = useState<BusinessPartnerDetailDto | null>(null);
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermListDto[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [priceLists, setPriceLists] = useState<PriceListDto[]>([]);
  const [allPartners, setAllPartners] = useState<BusinessPartnerDto[]>([]);
  const [formData, setFormData] = useState<UpdateBusinessPartnerDto>({
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
    priceList: '',
    parentId: '',
  });

  useEffect(() => {
    loadData();
  }, [id]);

  const loadData = async () => {
    try {
      setLoading(true);
      console.log('Business Partner Edit Page: Starting to load data for id:', id);
      
      // Load price lists separately to debug
      console.log('Business Partner Edit Page: Calling priceListService.getActivePriceLists()...');
      let priceListsData: PriceListDto[] = [];
      try {
        priceListsData = await priceListService.getActivePriceLists();
        console.log('Business Partner Edit Page: Price lists loaded:', priceListsData);
      } catch (priceListError) {
        console.error('Business Partner Edit Page: Error loading price lists:', priceListError);
      }
      
      // Load partner data and reference data in parallel
      const [partnerData, termsData, currenciesData, partnersData] = await Promise.all([
        businessPartnerService.getPartnerById(id),
        paymentTermService.getActive().catch((err) => { console.error('Error loading payment terms:', err); return []; }),
        currencyService.getActive().catch((err) => { console.error('Error loading currencies:', err); return []; }),
        businessPartnerService.getAllPartnersForDropdown().catch((err) => { console.error('Error loading partners:', err); return []; })
      ]);
      
      console.log('Business Partner Edit Page: Other data loaded - terms:', termsData?.length, 'currencies:', currenciesData?.length, 'partners:', partnersData?.length);
      
      setPartner(partnerData);
      setPaymentTerms(termsData || []);
      setCurrencies(currenciesData || []);
      // Show all active price lists
      setPriceLists(priceListsData || []);
      // Filter out the current partner from the list (can't be its own parent)
      setAllPartners((partnersData || []).filter(p => p.id !== id));
      
      // Populate form data
      setFormData({
        partnerName: partnerData.partnerName || partnerData.companyName || '',
        tradingName: partnerData.tradingName || '',
        registrationNumber: partnerData.registrationNumber || '',
        taxNumber: partnerData.taxNumber || '',
        email: partnerData.email || '',
        phone: partnerData.phone || '',
        website: partnerData.website || '',
        physicalAddress: partnerData.physicalAddress || '',
        city: partnerData.city || '',
        country: partnerData.country || '',
        postalCode: partnerData.physicalPostalCode || '',
        notes: partnerData.notes || '',
        status: partnerData.status || 'Active',
        isPreferred: partnerData.isPreferred || false,
        currency: partnerData.currency || '',
        paymentTerms: partnerData.paymentTerms || '',
        priceList: partnerData.priceList || '',
        parentId: partnerData.parentId || '',
      });
    } catch (error) {
      console.error('Error loading business partner:', error);
      toast.error('Failed to load business partner details');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    try {
      setSaving(true);
      await businessPartnerService.updatePartner(id, formData);
      toast.success('Business partner updated successfully');
      router.push(`/procurement/business-partners/${id}`);
    } catch (error) {
      console.error('Error updating business partner:', error);
      toast.error('Failed to update business partner');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading business partner...</p>
        </div>
      </div>
    );
  }

  if (!partner) {
    return (
      <div className="text-center py-12">
        <p className="text-gray-600">Business partner not found</p>
        <Button onClick={() => router.push('/procurement/business-partners')} className="mt-4">
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back to List
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.back()}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Edit Business Partner</h1>
            <p className="text-gray-600 mt-1">{partner.partnerCode} - {partner.partnerName}</p>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => router.back()}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={saving}>
            <Save className="w-4 h-4 mr-2" />
            {saving ? 'Saving...' : 'Save Changes'}
          </Button>
        </div>
      </div>

      {/* Form */}
      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Basic Information */}
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label htmlFor="partnerName">Company Name *</Label>
                <Input
                  id="partnerName"
                  value={formData.partnerName}
                  onChange={(e) => setFormData({ ...formData, partnerName: e.target.value })}
                  required
                />
              </div>
              <div>
                <Label htmlFor="tradingName">Trading Name</Label>
                <Input
                  id="tradingName"
                  value={formData.tradingName}
                  onChange={(e) => setFormData({ ...formData, tradingName: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="status">Status</Label>
                <Select value={formData.status} onValueChange={(value) => setFormData({ ...formData, status: value })}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Active">Active</SelectItem>
                    <SelectItem value="Inactive">Inactive</SelectItem>
                    <SelectItem value="Suspended">Suspended</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="registrationNumber">Registration Number</Label>
                <Input
                  id="registrationNumber"
                  value={formData.registrationNumber}
                  onChange={(e) => setFormData({ ...formData, registrationNumber: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="taxNumber">Tax Number</Label>
                <Input
                  id="taxNumber"
                  value={formData.taxNumber}
                  onChange={(e) => setFormData({ ...formData, taxNumber: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="parentId" className="flex items-center gap-2">
                  <Building2 className="w-4 h-4" />
                  Parent Business Partner
                </Label>
                <Select value={formData.parentId || '__none__'} onValueChange={(value) => setFormData({ ...formData, parentId: value === '__none__' ? '' : value })}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select parent (optional)" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">None (Top Level)</SelectItem>
                    {allPartners.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.partnerCode} - {p.partnerName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Contact Information */}
        <Card>
          <CardHeader>
            <CardTitle>Contact Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="phone">Phone</Label>
                <Input
                  id="phone"
                  value={formData.phone}
                  onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="website">Website</Label>
                <Input
                  id="website"
                  value={formData.website}
                  onChange={(e) => setFormData({ ...formData, website: e.target.value })}
                />
              </div>
            </div>
            <div>
              <Label htmlFor="physicalAddress">Physical Address</Label>
              <Textarea
                id="physicalAddress"
                value={formData.physicalAddress}
                onChange={(e) => setFormData({ ...formData, physicalAddress: e.target.value })}
                rows={3}
              />
            </div>
            <div className="grid grid-cols-3 gap-4">
              <div>
                <Label htmlFor="city">City</Label>
                <Input
                  id="city"
                  value={formData.city}
                  onChange={(e) => setFormData({ ...formData, city: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="country">Country</Label>
                <Input
                  id="country"
                  value={formData.country}
                  onChange={(e) => setFormData({ ...formData, country: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="postalCode">Postal Code</Label>
                <Input
                  id="postalCode"
                  value={formData.postalCode}
                  onChange={(e) => setFormData({ ...formData, postalCode: e.target.value })}
                />
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Additional Information */}
        <Card>
          <CardHeader>
            <CardTitle>Additional Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label htmlFor="currency">Currency</Label>
                <Select value={formData.currency || ''} onValueChange={(value) => setFormData({ ...formData, currency: value })}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select currency" />
                  </SelectTrigger>
                  <SelectContent>
                    {currencies.length > 0 ? (
                      currencies.map((curr) => (
                        <SelectItem key={curr.id} value={curr.id}>
                          {curr.code} - {curr.name} ({curr.symbol})
                        </SelectItem>
                      ))
                    ) : (
                      <>
                        <SelectItem value="USD">USD - US Dollar</SelectItem>
                        <SelectItem value="EUR">EUR - Euro</SelectItem>
                        <SelectItem value="GBP">GBP - British Pound</SelectItem>
                        <SelectItem value="ZAR">ZAR - South African Rand</SelectItem>
                      </>
                    )}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="paymentTerms">Payment Terms</Label>
                <Select value={formData.paymentTerms || ''} onValueChange={(value) => setFormData({ ...formData, paymentTerms: value })}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select payment terms" />
                  </SelectTrigger>
                  <SelectContent>
                    {paymentTerms.length > 0 ? (
                      paymentTerms.map((term) => (
                        <SelectItem key={term.id} value={term.id}>
                          {term.code} - {term.name}
                        </SelectItem>
                      ))
                    ) : (
                      <>
                        <SelectItem value="COD">Cash on Delivery</SelectItem>
                        <SelectItem value="Net30">Net 30 Days</SelectItem>
                        <SelectItem value="Net60">Net 60 Days</SelectItem>
                      </>
                    )}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="priceList">Price List</Label>
                <Select value={formData.priceList || ''} onValueChange={(value) => setFormData({ ...formData, priceList: value })}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select price list" />
                  </SelectTrigger>
                  <SelectContent>
                    {priceLists.length > 0 ? (
                      priceLists.map((pl) => (
                        <SelectItem key={pl.id} value={pl.id}>
                          {pl.priceListCode} - {pl.name}
                        </SelectItem>
                      ))
                    ) : (
                      <SelectItem value="__no_price_lists__" disabled>No price lists available</SelectItem>
                    )}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label htmlFor="notes">Notes</Label>
              <Textarea
                id="notes"
                value={formData.notes}
                onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                rows={4}
              />
            </div>
          </CardContent>
        </Card>
      </form>
    </div>
  );
}


