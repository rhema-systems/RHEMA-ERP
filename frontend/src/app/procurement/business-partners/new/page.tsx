'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Separator } from '@/components/ui/separator';
import {
  ArrowLeft,
  Building2,
  Truck,
  HardHat,
  Users,
  Save,
  Loader2,
  MapPin,
  Phone,
  Mail,
  Globe,
  CreditCard,
  DollarSign,
  Percent,
  ShoppingCart,
  Calendar,
  Award,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  businessPartnerService,
  type CreateBusinessPartnerDto,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  paymentTermService,
  procurementCurrencyService,
} from '@/services/financeCommonService';
import type {
  PaymentTermListDto,
  CurrencyListDto,
} from '@/services/financeCommonService';
import {
  priceListService,
  PriceListDto,
  PriceListType,
} from '@/services/priceListService';
import {
  emptyBusinessPartnerPostingDefaults,
  PartnerAccountsFields,
  PartnerCatalogueNotice,
  PartnerOptionsFields,
  PartnerTaxDefaultsFields,
  useBusinessPartnerPostingCatalogues,
} from '@/components/procurement/BusinessPartnerPostingFields';

type PartnerType = 'Supplier' | 'Contractor' | 'Customer' | 'Both' | '';

interface FormData {
  // Common fields
  partnerType: PartnerType;
  partnerName: string;
  tradingName: string;
  registrationNumber: string;
  taxNumber: string;
  email: string;
  phone: string;
  website: string;
  physicalAddress: string;
  city: string;
  country: string;
  postalCode: string;
  notes: string;
  parentId: string;
  // Banking
  bankName: string;
  bankAccountNumber: string;
  bankBranchCode: string;
  // Customer-specific fields
  customerType: string;
  creditLimit: string;
  paymentTerms: string;
  paymentTermId: string;
  currency: string;
  defaultDiscount: string;
  priceList: string;
  salesTerritory: string;
  isTaxExempt: boolean;
  taxExemptionNumber: string;
  taxExemptionExpiry: string;
  preferredShippingMethod: string;
  deliveryInstructions: string;
  customerSince: string;
  loyaltyTier: string;
}

const initialFormData: FormData = {
  partnerType: '',
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
  parentId: '',
  bankName: '',
  bankAccountNumber: '',
  bankBranchCode: '',
  customerType: '',
  creditLimit: '',
  paymentTerms: '',
  paymentTermId: '',
  currency: '',
  defaultDiscount: '',
  priceList: '',
  salesTerritory: '',
  isTaxExempt: false,
  taxExemptionNumber: '',
  taxExemptionExpiry: '',
  preferredShippingMethod: '',
  deliveryInstructions: '',
  customerSince: '',
  loyaltyTier: '',
};

const partnerTypeOptions = [
  {
    value: 'Supplier',
    label: 'Supplier',
    icon: Truck,
    description: 'Vendors who supply goods and materials',
  },
  {
    value: 'Contractor',
    label: 'Contractor',
    icon: HardHat,
    description: 'Service providers and contractors',
  },
  {
    value: 'Customer',
    label: 'Customer',
    icon: Users,
    description: 'Customers/Debtors for sales transactions',
  },
  {
    value: 'Both',
    label: 'Supplier & Contractor',
    icon: Building2,
    description: 'Partners who are both suppliers and contractors',
  },
];

const customerTypeOptions = [
  { value: 'Retail', label: 'Retail' },
  { value: 'Wholesale', label: 'Wholesale' },
  { value: 'Corporate', label: 'Corporate' },
  { value: 'Government', label: 'Government' },
  { value: 'Non-Profit', label: 'Non-Profit' },
];

const paymentTermsOptions = [
  { value: 'COD', label: 'Cash on Delivery (COD)' },
  { value: 'Net7', label: 'Net 7 Days' },
  { value: 'Net15', label: 'Net 15 Days' },
  { value: 'Net30', label: 'Net 30 Days' },
  { value: 'Net45', label: 'Net 45 Days' },
  { value: 'Net60', label: 'Net 60 Days' },
  { value: 'Net90', label: 'Net 90 Days' },
  { value: 'Prepaid', label: 'Prepaid' },
];

const loyaltyTierOptions = [
  { value: 'Bronze', label: 'Bronze' },
  { value: 'Silver', label: 'Silver' },
  { value: 'Gold', label: 'Gold' },
  { value: 'Platinum', label: 'Platinum' },
];

const shippingMethodOptions = [
  { value: 'Standard', label: 'Standard Shipping' },
  { value: 'Express', label: 'Express Shipping' },
  { value: 'Overnight', label: 'Overnight Delivery' },
  { value: 'Pickup', label: 'Customer Pickup' },
  { value: 'Freight', label: 'Freight/Trucking' },
];

export default function NewBusinessPartnerPage() {
  const router = useRouter();
  const [formData, setFormData] = useState<FormData>(initialFormData);
  const [saving, setSaving] = useState(false);
  const [activeTab, setActiveTab] = useState('basic');
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermListDto[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [priceLists, setPriceLists] = useState<PriceListDto[]>([]);
  const [allPartners, setAllPartners] = useState<BusinessPartnerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [postingDefaults, setPostingDefaults] = useState(
    emptyBusinessPartnerPostingDefaults
  );
  const catalogues = useBusinessPartnerPostingCatalogues(formData.partnerType);

  // Load payment terms, currencies, price lists, and partners from API
  useEffect(() => {
    const loadData = async () => {
      try {
        // Load price lists separately to debug
        let priceListsData: PriceListDto[] = [];
        try {
          priceListsData = await priceListService.getActivePriceLists();
        } catch (priceListError) {
          console.error(
            'Business Partner New Page: Error loading price lists:',
            priceListError
          );
        }

        const [terms, currs, partnersData] = await Promise.all([
          paymentTermService.getActive().catch(() => []),
          procurementCurrencyService.getActive().catch(() => []),
          businessPartnerService.getAllPartnersForDropdown().catch(() => []),
        ]);

        setPaymentTerms(terms || []);
        setCurrencies(currs || []);
        // Show all active price lists
        setPriceLists(priceListsData || []);
        setAllPartners(partnersData || []);
      } catch (error) {
        console.error('Error loading reference data:', error);
        toast.error('Failed to load reference data');
        // Set empty arrays to prevent errors
        setPaymentTerms([]);
        setCurrencies([]);
        setPriceLists([]);
        setAllPartners([]);
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, []);

  const handleInputChange = (
    field: keyof FormData,
    value: string | boolean
  ) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async () => {
    // Validation
    if (!formData.partnerType) {
      toast.error('Please select a partner type');
      return;
    }
    if (!formData.partnerName.trim()) {
      toast.error('Please enter a partner name');
      return;
    }

    try {
      setSaving(true);

      const createData: CreateBusinessPartnerDto = {
        partnerType: formData.partnerType,
        partnerName: formData.partnerName,
        tradingName: formData.tradingName || undefined,
        registrationNumber: formData.registrationNumber || undefined,
        taxNumber: formData.taxNumber || undefined,
        email: formData.email || undefined,
        phone: formData.phone || undefined,
        website: formData.website || undefined,
        physicalAddress: formData.physicalAddress || undefined,
        city: formData.city || undefined,
        country: formData.country || undefined,
        postalCode: formData.postalCode || undefined,
        bankName: formData.bankName || undefined,
        bankAccountNumber: formData.bankAccountNumber || undefined,
        bankBranchCode: formData.bankBranchCode || undefined,
        notes: formData.notes || undefined,
        parentId: formData.parentId || undefined,
        currency: formData.currency || undefined,
        // Procurement/Finance boundary: submit the structured FK; the backend owns the legacy descriptor dual-write.
        paymentTermId: formData.paymentTermId || undefined,
        creditLimit:
          formData.creditLimit === ''
            ? undefined
            : Number(formData.creditLimit),
        postingDefaults,
      };

      // Add customer-specific fields if partner type is Customer
      if (formData.partnerType === 'Customer') {
        createData.customerType = formData.customerType || undefined;
        createData.defaultDiscount = formData.defaultDiscount
          ? parseFloat(formData.defaultDiscount)
          : undefined;
        createData.priceList = formData.priceList || undefined;
        createData.salesTerritory = formData.salesTerritory || undefined;
        createData.isTaxExempt = formData.isTaxExempt;
        createData.taxExemptionNumber =
          formData.taxExemptionNumber || undefined;
        createData.taxExemptionExpiry =
          formData.taxExemptionExpiry || undefined;
        createData.preferredShippingMethod =
          formData.preferredShippingMethod || undefined;
        createData.deliveryInstructions =
          formData.deliveryInstructions || undefined;
        createData.customerSince = formData.customerSince || undefined;
        createData.loyaltyTier = formData.loyaltyTier || undefined;
      }

      const result = await businessPartnerService.createPartner(createData);
      toast.success('Business partner created successfully');
      router.push(`/procurement/business-partners/${result.id}`);
    } catch (error: any) {
      console.error('Error creating business partner:', error);
      toast.error(error.message || 'Failed to create business partner');
    } finally {
      setSaving(false);
    }
  };

  const isCustomer = formData.partnerType === 'Customer';

  return (
    <div className="mx-auto w-full max-w-5xl space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="outline"
            onClick={() => router.push('/procurement/business-partners')}
          >
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Add Business Partner</h1>
            <p className="text-gray-600 mt-1">
              Create a new supplier, contractor, or customer
            </p>
          </div>
        </div>
        <Button
          onClick={handleSubmit}
          disabled={saving || !formData.partnerType}
        >
          {saving ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="w-4 h-4 mr-2" />
              Save Partner
            </>
          )}
        </Button>
      </div>

      {/* Partner Type Selection */}
      <Card>
        <CardHeader>
          <CardTitle>Select Partner Type</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {partnerTypeOptions.map((option) => {
              const Icon = option.icon;
              const isSelected = formData.partnerType === option.value;
              return (
                <button
                  key={option.value}
                  type="button"
                  aria-pressed={isSelected}
                  disabled={saving}
                  onClick={() =>
                    handleInputChange(
                      'partnerType',
                      option.value as PartnerType
                    )
                  }
                  className={`
                    cursor-pointer rounded-lg border-2 p-3 transition-all
                    ${
                      isSelected
                        ? 'border-blue-500 bg-blue-50 shadow-md'
                        : 'border-gray-200 hover:border-gray-300 hover:bg-gray-50'
                    }
                  `}
                >
                  <div className="flex items-center gap-2">
                    <div
                      className={`
                      p-2 rounded-full
                      ${isSelected ? 'bg-blue-500 text-white' : 'bg-gray-100 text-gray-600'}
                    `}
                    >
                      <Icon className="w-4 h-4" />
                    </div>
                    <h3
                      className={`font-semibold ${isSelected ? 'text-blue-700' : 'text-gray-900'}`}
                    >
                      {option.label}
                    </h3>
                  </div>
                </button>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Form Tabs */}
      <PartnerCatalogueNotice unavailable={catalogues.unavailable} />
      {formData.partnerType && (
        <Card>
          <CardContent className="pt-6">
            <Tabs value={activeTab} onValueChange={setActiveTab}>
              <TabsList
                className={
                  isCustomer
                    ? 'grid w-full grid-cols-6'
                    : 'grid w-full grid-cols-5'
                }
              >
                <TabsTrigger value="basic">Details</TabsTrigger>
                <TabsTrigger value="contact">Contact</TabsTrigger>
                <TabsTrigger value="banking">Banking</TabsTrigger>
                <TabsTrigger value="options">Options</TabsTrigger>
                <TabsTrigger value="accounts">Accounts</TabsTrigger>
                {isCustomer && (
                  <TabsTrigger value="customer">Customer Details</TabsTrigger>
                )}
              </TabsList>

              <div className="h-[min(620px,calc(100vh-350px))] min-h-80 overflow-y-auto px-1">
                {/* Basic Information Tab */}
                <TabsContent value="basic" className="space-y-6 mt-6">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                      <Label htmlFor="partnerName">Partner Name *</Label>
                      <Input
                        id="partnerName"
                        value={formData.partnerName}
                        onChange={(e) =>
                          handleInputChange('partnerName', e.target.value)
                        }
                        placeholder="Enter company/partner name"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="tradingName">Trading Name</Label>
                      <Input
                        id="tradingName"
                        value={formData.tradingName}
                        onChange={(e) =>
                          handleInputChange('tradingName', e.target.value)
                        }
                        placeholder="Enter trading name (if different)"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="registrationNumber">
                        Registration Number
                      </Label>
                      <Input
                        id="registrationNumber"
                        value={formData.registrationNumber}
                        onChange={(e) =>
                          handleInputChange(
                            'registrationNumber',
                            e.target.value
                          )
                        }
                        placeholder="Business registration number"
                      />
                    </div>
                    <div className="space-y-2 md:col-span-2">
                      <Label
                        htmlFor="parentId"
                        className="flex items-center gap-2"
                      >
                        <Building2 className="w-4 h-4" />
                        Parent Business Partner
                      </Label>
                      <Select
                        value={formData.parentId || '__none__'}
                        onValueChange={(value) =>
                          handleInputChange(
                            'parentId',
                            value === '__none__' ? '' : value
                          )
                        }
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select parent business partner (optional)" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">
                            None (Top Level)
                          </SelectItem>
                          {(allPartners || []).map((partner) => (
                            <SelectItem key={partner.id} value={partner.id}>
                              {partner.partnerCode} - {partner.partnerName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <p className="text-sm text-muted-foreground">
                        Select a parent to create a hierarchy of business
                        partners
                      </p>
                    </div>
                  </div>

                  <PartnerTaxDefaultsFields
                    value={postingDefaults}
                    onChange={setPostingDefaults}
                    taxGroups={catalogues.taxGroups}
                    withholdingTaxes={catalogues.withholdingTaxes}
                    disabled={saving}
                  />

                  <Separator />

                  <div className="space-y-2">
                    <Label htmlFor="notes">Notes</Label>
                    <Textarea
                      id="notes"
                      value={formData.notes}
                      onChange={(e) =>
                        handleInputChange('notes', e.target.value)
                      }
                      placeholder="Additional notes about this partner..."
                      rows={4}
                    />
                  </div>
                </TabsContent>

                {/* Contact & Address Tab */}
                <TabsContent value="contact" className="space-y-6 mt-6">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                      <Label
                        htmlFor="email"
                        className="flex items-center gap-2"
                      >
                        <Mail className="w-4 h-4" />
                        Email
                      </Label>
                      <Input
                        id="email"
                        type="email"
                        value={formData.email}
                        onChange={(e) =>
                          handleInputChange('email', e.target.value)
                        }
                        placeholder="email@company.com"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label
                        htmlFor="phone"
                        className="flex items-center gap-2"
                      >
                        <Phone className="w-4 h-4" />
                        Phone
                      </Label>
                      <Input
                        id="phone"
                        value={formData.phone}
                        onChange={(e) =>
                          handleInputChange('phone', e.target.value)
                        }
                        placeholder="+1 234 567 8900"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label
                        htmlFor="website"
                        className="flex items-center gap-2"
                      >
                        <Globe className="w-4 h-4" />
                        Website
                      </Label>
                      <Input
                        id="website"
                        value={formData.website}
                        onChange={(e) =>
                          handleInputChange('website', e.target.value)
                        }
                        placeholder="https://www.company.com"
                      />
                    </div>
                  </div>

                  <Separator />

                  <h3 className="text-lg font-semibold flex items-center gap-2">
                    <MapPin className="w-5 h-5" />
                    Physical Address
                  </h3>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="md:col-span-2 space-y-2">
                      <Label htmlFor="physicalAddress">Street Address</Label>
                      <Input
                        id="physicalAddress"
                        value={formData.physicalAddress}
                        onChange={(e) =>
                          handleInputChange('physicalAddress', e.target.value)
                        }
                        placeholder="123 Main Street"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="city">City</Label>
                      <Input
                        id="city"
                        value={formData.city}
                        onChange={(e) =>
                          handleInputChange('city', e.target.value)
                        }
                        placeholder="City"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="postalCode">Postal Code</Label>
                      <Input
                        id="postalCode"
                        value={formData.postalCode}
                        onChange={(e) =>
                          handleInputChange('postalCode', e.target.value)
                        }
                        placeholder="12345"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="country">Country</Label>
                      <Input
                        id="country"
                        value={formData.country}
                        onChange={(e) =>
                          handleInputChange('country', e.target.value)
                        }
                        placeholder="Country"
                      />
                    </div>
                  </div>
                </TabsContent>

                {/* Banking Tab */}
                <TabsContent value="banking" className="space-y-6 mt-6">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                      <Label htmlFor="currency">Default Currency</Label>
                      <Select
                        value={formData.currency}
                        onValueChange={(value) =>
                          handleInputChange('currency', value)
                        }
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select currency" />
                        </SelectTrigger>
                        <SelectContent>
                          {(currencies || []).map((curr) => (
                            <SelectItem key={curr.id} value={curr.code}>
                              {curr.code} - {curr.name} ({curr.symbol})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label
                        htmlFor="bankName"
                        className="flex items-center gap-2"
                      >
                        <CreditCard className="w-4 h-4" />
                        Bank Name
                      </Label>
                      <Input
                        id="bankName"
                        value={formData.bankName}
                        onChange={(e) =>
                          handleInputChange('bankName', e.target.value)
                        }
                        placeholder="Bank name"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="bankBranchCode">Branch Code</Label>
                      <Input
                        id="bankBranchCode"
                        value={formData.bankBranchCode}
                        onChange={(e) =>
                          handleInputChange('bankBranchCode', e.target.value)
                        }
                        placeholder="Branch code"
                      />
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="bankAccountNumber">Account Number</Label>
                      <Input
                        id="bankAccountNumber"
                        value={formData.bankAccountNumber}
                        onChange={(e) =>
                          handleInputChange('bankAccountNumber', e.target.value)
                        }
                        placeholder="Account number"
                      />
                    </div>
                  </div>
                </TabsContent>

                <TabsContent value="options" className="py-4">
                  <PartnerOptionsFields
                    value={postingDefaults}
                    onChange={setPostingDefaults}
                    options={{
                      paymentTermId: formData.paymentTermId,
                      taxNumber: formData.taxNumber,
                      creditLimit: formData.creditLimit,
                    }}
                    onOptionsChange={(patch) =>
                      setFormData((previous) => ({ ...previous, ...patch }))
                    }
                    paymentTerms={paymentTerms}
                    partnerType={formData.partnerType}
                    bankAccounts={catalogues.bankAccounts}
                    disabled={saving}
                  />
                </TabsContent>
                <TabsContent value="accounts" className="py-4">
                  <PartnerAccountsFields
                    value={postingDefaults}
                    onChange={setPostingDefaults}
                    accounts={catalogues.accounts}
                    bankAccounts={catalogues.bankAccounts}
                    disabled={saving}
                  />
                </TabsContent>

                {/* Customer Details Tab */}
                {isCustomer && (
                  <TabsContent value="customer" className="space-y-6 mt-6">
                    {/* Customer Classification */}
                    <div>
                      <h3 className="text-lg font-semibold flex items-center gap-2 mb-4">
                        <Users className="w-5 h-5" />
                        Customer Classification
                      </h3>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div className="space-y-2">
                          <Label htmlFor="customerType">Customer Type</Label>
                          <Select
                            value={formData.customerType}
                            onValueChange={(value) =>
                              handleInputChange('customerType', value)
                            }
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select customer type" />
                            </SelectTrigger>
                            <SelectContent>
                              {customerTypeOptions.map((option) => (
                                <SelectItem
                                  key={option.value}
                                  value={option.value}
                                >
                                  {option.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="salesTerritory">
                            Sales Territory
                          </Label>
                          <Input
                            id="salesTerritory"
                            value={formData.salesTerritory}
                            onChange={(e) =>
                              handleInputChange(
                                'salesTerritory',
                                e.target.value
                              )
                            }
                            placeholder="e.g., North Region, West Coast"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label
                            htmlFor="customerSince"
                            className="flex items-center gap-2"
                          >
                            <Calendar className="w-4 h-4" />
                            Customer Since
                          </Label>
                          <Input
                            id="customerSince"
                            type="date"
                            value={formData.customerSince}
                            onChange={(e) =>
                              handleInputChange('customerSince', e.target.value)
                            }
                          />
                        </div>
                        <div className="space-y-2">
                          <Label
                            htmlFor="loyaltyTier"
                            className="flex items-center gap-2"
                          >
                            <Award className="w-4 h-4" />
                            Loyalty Tier
                          </Label>
                          <Select
                            value={formData.loyaltyTier}
                            onValueChange={(value) =>
                              handleInputChange('loyaltyTier', value)
                            }
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select loyalty tier" />
                            </SelectTrigger>
                            <SelectContent>
                              {loyaltyTierOptions.map((option) => (
                                <SelectItem
                                  key={option.value}
                                  value={option.value}
                                >
                                  {option.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>
                    </div>

                    <Separator />

                    {/* Credit & Payment Terms */}
                    <div>
                      <h3 className="text-lg font-semibold flex items-center gap-2 mb-4">
                        <DollarSign className="w-5 h-5" />
                        Credit & Payment Terms
                      </h3>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div className="space-y-2">
                          <Label
                            htmlFor="defaultDiscount"
                            className="flex items-center gap-2"
                          >
                            <Percent className="w-4 h-4" />
                            Default Discount (%)
                          </Label>
                          <Input
                            id="defaultDiscount"
                            type="number"
                            step="0.01"
                            min="0"
                            max="100"
                            value={formData.defaultDiscount}
                            onChange={(e) =>
                              handleInputChange(
                                'defaultDiscount',
                                e.target.value
                              )
                            }
                            placeholder="0.00"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="priceList">Price List</Label>
                          <Select
                            value={formData.priceList}
                            onValueChange={(value) =>
                              handleInputChange('priceList', value)
                            }
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select price list" />
                            </SelectTrigger>
                            <SelectContent>
                              {(priceLists || []).map((pl) => (
                                <SelectItem key={pl.id} value={pl.id}>
                                  {pl.priceListCode} - {pl.name}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>
                    </div>

                    <Separator />

                    {/* Tax Exemption */}
                    <div>
                      <h3 className="text-lg font-semibold mb-4">
                        Tax Exemption
                      </h3>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div className="flex items-center space-x-2">
                          <Switch
                            id="isTaxExempt"
                            checked={formData.isTaxExempt}
                            onCheckedChange={(checked) =>
                              handleInputChange('isTaxExempt', checked)
                            }
                          />
                          <Label htmlFor="isTaxExempt">Tax Exempt</Label>
                        </div>
                        {formData.isTaxExempt && (
                          <>
                            <div className="space-y-2">
                              <Label htmlFor="taxExemptionNumber">
                                Tax Exemption Number
                              </Label>
                              <Input
                                id="taxExemptionNumber"
                                value={formData.taxExemptionNumber}
                                onChange={(e) =>
                                  handleInputChange(
                                    'taxExemptionNumber',
                                    e.target.value
                                  )
                                }
                                placeholder="Exemption certificate number"
                              />
                            </div>
                            <div className="space-y-2">
                              <Label htmlFor="taxExemptionExpiry">
                                Exemption Expiry Date
                              </Label>
                              <Input
                                id="taxExemptionExpiry"
                                type="date"
                                value={formData.taxExemptionExpiry}
                                onChange={(e) =>
                                  handleInputChange(
                                    'taxExemptionExpiry',
                                    e.target.value
                                  )
                                }
                              />
                            </div>
                          </>
                        )}
                      </div>
                    </div>

                    <Separator />

                    {/* Shipping & Delivery */}
                    <div>
                      <h3 className="text-lg font-semibold flex items-center gap-2 mb-4">
                        <ShoppingCart className="w-5 h-5" />
                        Shipping & Delivery
                      </h3>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <div className="space-y-2">
                          <Label htmlFor="preferredShippingMethod">
                            Preferred Shipping Method
                          </Label>
                          <Select
                            value={formData.preferredShippingMethod}
                            onValueChange={(value) =>
                              handleInputChange(
                                'preferredShippingMethod',
                                value
                              )
                            }
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select shipping method" />
                            </SelectTrigger>
                            <SelectContent>
                              {shippingMethodOptions.map((option) => (
                                <SelectItem
                                  key={option.value}
                                  value={option.value}
                                >
                                  {option.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div className="md:col-span-2 space-y-2">
                          <Label htmlFor="deliveryInstructions">
                            Delivery Instructions
                          </Label>
                          <Textarea
                            id="deliveryInstructions"
                            value={formData.deliveryInstructions}
                            onChange={(e) =>
                              handleInputChange(
                                'deliveryInstructions',
                                e.target.value
                              )
                            }
                            placeholder="Special delivery instructions, gate codes, contact person, etc."
                            rows={3}
                          />
                        </div>
                      </div>
                    </div>
                  </TabsContent>
                )}
              </div>
            </Tabs>
          </CardContent>
        </Card>
      )}

      {/* Action Buttons */}
      {formData.partnerType && (
        <div className="flex justify-end gap-4">
          <Button
            variant="outline"
            onClick={() => router.push('/procurement/business-partners')}
          >
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={saving}>
            {saving ? (
              <>
                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                Saving...
              </>
            ) : (
              <>
                <Save className="w-4 h-4 mr-2" />
                Save Business Partner
              </>
            )}
          </Button>
        </div>
      )}
    </div>
  );
}
