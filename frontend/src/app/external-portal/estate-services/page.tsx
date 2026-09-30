'use client';

import Link from 'next/link';
import React from 'react';
import { Eye, Loader2, Plus, Send } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  externalEstateServicesService,
  type ExternalEstateRequestType,
  type ExternalEstateRequestsPage,
  type ExternalEstateServiceRequest,
  type ExternalPropertyPortfolio,
} from '@/services/external-estate-services.service';
import {
  externalEstateListingsService,
  type ExternalCustomerProfile,
} from '@/services/external-estate-listings.service';

type PortalExtraField = {
  key: string;
  label: string;
  type?: 'text' | 'number' | 'date' | 'select' | 'textarea';
  options?: string[];
};

type EstateServiceFormState = {
  requestType: string;
  applicantName: string;
  contact: string;
  propertyReference: string;
  location: string;
  category: string;
  priority: string;
  serviceImpact: string;
  description: string;
  additionalValues: Record<string, string>;
};

const landUseOptions = [
  'Residential',
  'Commercial',
  'Institutional',
  'Industrial',
  'Agro Industrial',
  'Fuel Station',
  'Mixed Use',
  'Other',
];

// Customer-started Estate requests are routed into the internal procedure workspaces here.
const extraFieldsByRequestType: Record<string, PortalExtraField[]> = {
  maintenance: [
    { key: 'issueType', label: 'Maintenance issue type' },
    { key: 'preferredVisitDate', label: 'Preferred visit date', type: 'date' },
    {
      key: 'accessInstructions',
      label: 'Access instructions',
      type: 'textarea',
    },
  ],
  complaint: [
    { key: 'complaintCategory', label: 'Complaint category' },
    { key: 'incidentDate', label: 'Incident date', type: 'date' },
    { key: 'desiredResolution', label: 'Desired resolution', type: 'textarea' },
  ],
  searchApplication: [
    { key: 'searchPurpose', label: 'Search purpose' },
    { key: 'searchPeriod', label: 'Search period / scope' },
    { key: 'searchFeeReceipt', label: 'Search fee receipt' },
  ],
  changeAddress: [
    { key: 'oldAddress', label: 'Previous address' },
    { key: 'newAddress', label: 'New address' },
    { key: 'declarationReference', label: 'Statutory declaration reference' },
  ],
  certifiedTrueCopy: [
    {
      key: 'documentToCertify',
      label: 'Document to certify',
      type: 'select',
      options: [
        'Offer Letter',
        'Right of Entry',
        'Lease',
        'Rent Card',
        'Allocation Letter',
        'Other',
      ],
    },
    { key: 'originalDocumentReference', label: 'Original document reference' },
    { key: 'certificationFeeReceipt', label: 'Certification fee receipt' },
  ],
  jointOwnership: [
    { key: 'transferorName', label: 'Existing lessee / owner' },
    { key: 'transfereeName', label: 'Name to add' },
    { key: 'declarationReference', label: 'Statutory declaration reference' },
  ],
  transfer: [
    {
      key: 'transferProcessType',
      label: 'Transfer process type',
      type: 'select',
      options: [
        'Transfer of interest',
        'Portion transfer',
        'Assignment',
        'Rental transfer',
        'Rental-to-HOS conversion',
      ],
    },
    { key: 'housePlotShopNumber', label: 'House / plot / shop number' },
    { key: 'transferorName', label: 'Transferor' },
    { key: 'transfereeName', label: 'Transferee' },
    {
      key: 'newLesseeAddress',
      label: 'New lessee / transferee address',
      type: 'textarea',
    },
    {
      key: 'transferEffectiveDate',
      label: 'Transfer effective date',
      type: 'date',
    },
    {
      key: 'transferDeclarationReference',
      label: 'Transfer Declaration form reference',
    },
    {
      key: 'voluntaryVacationReference',
      label: 'Voluntary vacation evidence reference',
    },
    {
      key: 'hosFormReference',
      label: 'HOS form reference, if rental changes to HOS',
    },
    { key: 'houseType', label: 'House type, if HOS applies' },
    {
      key: 'purchaseAmount',
      label: 'Purchase amount / amount bought',
      type: 'number',
    },
    {
      key: 'considerationAmount',
      label: 'Consideration amount',
      type: 'number',
    },
  ],
  assignment: [
    { key: 'transferorName', label: 'Assignor' },
    { key: 'transfereeName', label: 'Assignee' },
    { key: 'draftDeedReference', label: 'Draft deed reference' },
  ],
  mortgageConsent: [
    {
      key: 'mortgageConsentType',
      label: 'Mortgage consent type',
      type: 'select',
      options: ['Consent to Mortgage', 'Mortgage in Principle'],
    },
    { key: 'mortgageeName', label: 'Mortgagee / financial institution' },
    { key: 'draftDeedReference', label: 'Draft mortgage deed reference' },
  ],
  leaseDocument: [
    {
      key: 'developmentStatus',
      label: 'Development status',
      type: 'select',
      options: [
        'Undeveloped',
        'Partially developed',
        'Substantially developed',
        'Completed',
      ],
    },
    { key: 'buildingPermitReference', label: 'Building permit reference' },
    { key: 'dateOfTenancy', label: 'Date of tenancy', type: 'date' },
  ],
  additionalLand: [
    { key: 'adjoiningPlotNumber', label: 'Adjoining plot number' },
    {
      key: 'additionalLandSizeAcres',
      label: 'Additional land size (acres)',
      type: 'number',
    },
    {
      key: 'recommendation',
      label: 'Reason for additional land',
      type: 'textarea',
    },
  ],
  changeOfUse: [
    {
      key: 'existingUse',
      label: 'Existing use',
      type: 'select',
      options: landUseOptions,
    },
    {
      key: 'newUse',
      label: 'Proposed use',
      type: 'select',
      options: landUseOptions,
    },
    { key: 'plotSizeAcres', label: 'Plot size (acres)', type: 'number' },
  ],
  leaseRenewal: [
    {
      key: 'existingLeaseExpiryDate',
      label: 'Existing lease expiry date',
      type: 'date',
    },
    { key: 'yearsToExpiry', label: 'Years to expiry', type: 'number' },
    {
      key: 'surrenderOptionStatus',
      label: 'Surrender option',
      type: 'select',
      options: ['Not required', 'Surrender requested'],
    },
    { key: 'developmentStatus', label: 'Development proposal / status' },
  ],
  landApplication: [
    {
      key: 'landUse',
      label: 'Intended land use',
      type: 'select',
      options: landUseOptions,
    },
    {
      key: 'plotSizeAcres',
      label: 'Preferred plot size (acres)',
      type: 'number',
    },
    {
      key: 'approvedFeeScheduleReference',
      label: 'Known approved fee / appendix reference, if any',
    },
  ],
  traditionalLand: [
    {
      key: 'landUse',
      label: 'Land use',
      type: 'select',
      options: landUseOptions,
    },
    { key: 'plotSizeAcres', label: 'Plot size (acres)', type: 'number' },
    { key: 'traditionalCouncil', label: 'Traditional Council / Stool' },
    {
      key: 'allocationLetterReference',
      label: 'Traditional Council allocation letter reference',
    },
    {
      key: 'sitePlanReference',
      label: 'Traditional Council site plan reference',
    },
  ],
  tenancyRecognition: [
    { key: 'declarationReference', label: 'Statutory declaration reference' },
    { key: 'dateOfTenancy', label: 'Date of tenancy', type: 'date' },
    { key: 'arrearsStatus', label: 'Rent / arrears position' },
  ],
  hosConversion: [
    { key: 'hosFormReference', label: 'House Ownership Scheme form reference' },
    {
      key: 'tenantNamesChangingToHos',
      label: 'Tenant names changing to HOS',
      type: 'textarea',
    },
    { key: 'houseType', label: 'House type' },
    { key: 'sellingPrice', label: 'Selling price', type: 'number' },
    {
      key: 'purchaseAmount',
      label: 'Purchase amount / amount bought',
      type: 'number',
    },
    { key: 'purchaseDate', label: 'Date property was purchased', type: 'date' },
    { key: 'dateOfTenancy', label: 'Date of tenancy', type: 'date' },
    { key: 'rentCardNumber', label: 'Rent card number' },
    { key: 'rentRegisterReference', label: 'Rent register reference' },
  ],
  regularisation: [
    {
      key: 'regularisationApproach',
      label: 'Regularisation approach',
      type: 'select',
      options: ['Direct approach', 'Indirect approach'],
    },
    { key: 'plotSizeAcres', label: 'Plot size (acres)', type: 'number' },
    {
      key: 'communityRegularised',
      label: 'Community / area being regularised',
    },
    { key: 'planLayoutStatus', label: 'Planning layout status' },
  ],
  rightOfEntry: [
    { key: 'allocationReference', label: 'Allocation reference' },
    { key: 'offerLetterReference', label: 'Offer Letter reference' },
    { key: 'acceptanceDate', label: 'Acceptance date', type: 'date' },
    {
      key: 'paymentConfirmationReference',
      label: 'Payment confirmation reference',
    },
  ],
};

const initialForm = {
  requestType: '',
  applicantName: '',
  contact: '',
  propertyReference: '',
  location: '',
  category: '',
  priority: 'Normal',
  serviceImpact: 'Tenant affected',
  description: '',
  additionalValues: {},
} satisfies EstateServiceFormState;

const REQUEST_PAGE_SIZE = 10;

function formatDate(value?: string | null) {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? 'Not recorded'
    : date.toLocaleDateString();
}

function propertyReference(
  property: ExternalPropertyPortfolio['properties'][number]
) {
  return property.projectUnitCode || property.assetCode || property.name;
}

function propertyLocation(
  property: ExternalPropertyPortfolio['properties'][number]
) {
  return [property.location, property.town, property.district]
    .filter(Boolean)
    .join(', ');
}

function customerContact(customer?: ExternalCustomerProfile | null) {
  return [customer?.primaryEmail, customer?.primaryPhone]
    .filter(Boolean)
    .join(' / ');
}

export default function ExternalEstateServicesPage() {
  const [requestTypes, setRequestTypes] = React.useState<
    ExternalEstateRequestType[]
  >([]);
  const [requests, setRequests] = React.useState<
    ExternalEstateServiceRequest[]
  >([]);
  const [requestPage, setRequestPage] = React.useState(1);
  const [requestTotalCount, setRequestTotalCount] = React.useState(0);
  const [isRequestPageLoading, setIsRequestPageLoading] = React.useState(false);
  const [properties, setProperties] = React.useState<
    ExternalPropertyPortfolio['properties']
  >([]);
  const [customers, setCustomers] = React.useState<ExternalCustomerProfile[]>(
    []
  );
  const [form, setForm] = React.useState<EstateServiceFormState>(initialForm);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [isCreateOpen, setIsCreateOpen] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const selectedType = React.useMemo(
    () => requestTypes.find((type) => type.code === form.requestType),
    [form.requestType, requestTypes]
  );
  const extraFields = React.useMemo(
    () => extraFieldsByRequestType[form.requestType] ?? [],
    [form.requestType]
  );
  const totalRequestPages = Math.max(
    1,
    Math.ceil(requestTotalCount / REQUEST_PAGE_SIZE)
  );

  const applyRequestsPage = React.useCallback(
    (pageResult: ExternalEstateRequestsPage) => {
      setRequests(pageResult.items);
      setRequestPage(pageResult.page);
      setRequestTotalCount(pageResult.totalCount);
    },
    []
  );

  const loadRequestsPage = React.useCallback(
    async (pageNumber: number) => {
      setIsRequestPageLoading(true);
      try {
        const pageResult =
          await externalEstateServicesService.getMyRequestsPage({
            page: pageNumber,
            pageSize: REQUEST_PAGE_SIZE,
            source: 'estateServices',
          });
        applyRequestsPage(pageResult);
      } finally {
        setIsRequestPageLoading(false);
      }
    },
    [applyRequestsPage]
  );

  const loadData = React.useCallback(async () => {
    const [types, submittedRequests, propertyPortfolio, customerProfiles] =
      await Promise.all([
        externalEstateServicesService.getRequestTypes(),
        externalEstateServicesService.getMyRequestsPage({
          page: 1,
          pageSize: REQUEST_PAGE_SIZE,
          source: 'estateServices',
        }),
        externalEstateServicesService.getMyProperties(),
        externalEstateListingsService.getCustomerProfiles(),
      ]);
    const ownedProperties = propertyPortfolio.properties ?? [];
    const selectedProperty =
      ownedProperties.length === 1 ? ownedProperties[0] : undefined;
    const selectedCustomer =
      (selectedProperty?.customerBusinessPartnerId
        ? customerProfiles.find(
            (customer) =>
              customer.id === selectedProperty.customerBusinessPartnerId
          )
        : undefined) ??
      (customerProfiles.length === 1 ? customerProfiles[0] : undefined);
    setRequestTypes(types);
    setProperties(ownedProperties);
    setCustomers(customerProfiles);
    applyRequestsPage(submittedRequests);
    setForm((current) => ({
      ...current,
      requestType: current.requestType || types[0]?.code || '',
      applicantName:
        current.applicantName || selectedCustomer?.partnerName || '',
      contact: current.contact || customerContact(selectedCustomer),
      propertyReference:
        current.propertyReference && ownedProperties.some(
          (property) => propertyReference(property) === current.propertyReference
        )
          ? current.propertyReference
          : selectedProperty ? propertyReference(selectedProperty) : '',
      location:
        current.location ||
        (selectedProperty ? propertyLocation(selectedProperty) : ''),
    }));
  }, [applyRequestsPage]);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      setError(null);
      try {
        await loadData();
      } catch {
        if (mounted) {
          setError('Could not load Estate service requests.');
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, [loadData]);

  const updateForm = (
    field: Exclude<keyof EstateServiceFormState, 'additionalValues'>,
    value: string
  ) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const updateRequestType = (value: string) => {
    const nextType = requestTypes.find((type) => type.code === value);
    setForm((current) => ({
      ...current,
      requestType: value,
      category: nextType?.category || current.category,
      additionalValues: {},
    }));
  };

  const updatePropertyReference = (value: string) => {
    const selectedProperty = properties.find(
      (property) => propertyReference(property) === value
    );
    const selectedCustomer = selectedProperty?.customerBusinessPartnerId
      ? customers.find(
          (customer) =>
            customer.id === selectedProperty.customerBusinessPartnerId
        )
      : customers.length === 1
        ? customers[0]
        : undefined;
    setForm((current) => ({
      ...current,
      applicantName: selectedCustomer?.partnerName || current.applicantName,
      contact: customerContact(selectedCustomer) || current.contact,
      propertyReference: value,
      location: selectedProperty
        ? propertyLocation(selectedProperty)
        : current.location,
    }));
  };

  const updateAdditionalValue = (field: string, value: string) => {
    setForm((current) => ({
      ...current,
      additionalValues: {
        ...current.additionalValues,
        [field]: value,
      },
    }));
  };

  const renderExtraField = (field: PortalExtraField) => {
    const value = form.additionalValues[field.key] ?? '';

    if (field.type === 'select' && field.options?.length) {
      return (
        <div key={field.key} className="space-y-2">
          <Label>{field.label}</Label>
          <Select
            value={value || undefined}
            onValueChange={(nextValue) =>
              updateAdditionalValue(field.key, nextValue)
            }
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {field.options.map((option) => (
                <SelectItem key={option} value={option}>
                  {option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      );
    }

    if (field.type === 'textarea') {
      return (
        <div key={field.key} className="space-y-2 md:col-span-2">
          <Label>{field.label}</Label>
          <Textarea
            value={value}
            onChange={(event) =>
              updateAdditionalValue(field.key, event.target.value)
            }
          />
        </div>
      );
    }

    return (
      <div key={field.key} className="space-y-2">
        <Label>{field.label}</Label>
        <Input
          type={
            field.type === 'number'
              ? 'number'
              : field.type === 'date'
                ? 'date'
                : 'text'
          }
          step={field.type === 'number' ? '0.01' : undefined}
          value={value}
          onChange={(event) =>
            updateAdditionalValue(field.key, event.target.value)
          }
        />
      </div>
    );
  };

  const submitRequest = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (!form.requestType || !form.description.trim()) {
      toast.error('Request type and description are required.');
      return;
    }

    if (properties.length > 0 && !form.propertyReference.trim()) {
      toast.error('Select the property, unit, or plot for this request.');
      return;
    }

    if (form.propertyReference.trim() && !properties.some(
      (property) => propertyReference(property) === form.propertyReference
    )) {
      toast.error('Select a property linked to your account.');
      return;
    }

    setIsSaving(true);
    try {
      const additionalValues = Object.fromEntries(
        extraFields
          .map((field) => [
            field.key,
            form.additionalValues[field.key]?.trim() || null,
          ])
          .filter(([, value]) => value)
      );
      const created = await externalEstateServicesService.createRequest({
        requestType: form.requestType,
        applicantName: form.applicantName.trim(),
        contact: form.contact.trim(),
        propertyReference: form.propertyReference.trim(),
        location: form.location.trim(),
        category: form.category.trim() || selectedType?.category || '',
        priority: form.priority,
        serviceImpact: form.serviceImpact,
        description: form.description.trim(),
        additionalValues,
      });
      toast.success(
        `Request ${created.referenceNumber || created.title} submitted.`
      );
      try {
        await loadRequestsPage(1);
      } catch {
        setRequests((current) => [created, ...current]);
        setRequestPage(1);
        setRequestTotalCount((current) => current + 1);
      }
      setForm((current) => ({
        ...initialForm,
        requestType: current.requestType,
        applicantName: current.applicantName,
        contact: current.contact,
        propertyReference: current.propertyReference,
        location: current.location,
      }));
      setIsCreateOpen(false);
    } catch {
      toast.error('Could not submit the Estate service request.');
    } finally {
      setIsSaving(false);
    }
  };

  const changeRequestPage = async (nextPage: number) => {
    if (
      nextPage < 1 ||
      nextPage > totalRequestPages ||
      nextPage === requestPage ||
      isRequestPageLoading
    ) {
      return;
    }

    setError(null);
    try {
      await loadRequestsPage(nextPage);
    } catch {
      setError('Could not load Estate service requests.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">
            Estate Services
          </h1>
        </div>
        <div className="flex items-center gap-3">
          <Badge variant="outline" className="w-fit">
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-3 w-3 animate-spin" />
                Loading
              </>
            ) : (
              `${requestTotalCount} request${requestTotalCount === 1 ? '' : 's'}`
            )}
          </Badge>
          <Button
            type="button"
            disabled={isLoading}
            onClick={() => setIsCreateOpen(true)}
          >
            <Plus className="mr-2 h-4 w-4" />
            Add New
          </Button>
        </div>
      </div>

      {error ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}
      <Dialog
        open={isCreateOpen}
        onOpenChange={(open) => !isSaving && setIsCreateOpen(open)}
      >
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>New Estate Service Request</DialogTitle>
            <DialogDescription className="sr-only">Provide the request details for Estate Services.</DialogDescription>
          </DialogHeader>
          <form className="space-y-4" onSubmit={submitRequest}>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2">
                <Label>Request type</Label>
                <Select
                  value={form.requestType}
                  onValueChange={updateRequestType}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select request type" />
                  </SelectTrigger>
                  <SelectContent>
                    {requestTypes.map((type) => (
                      <SelectItem key={type.code} value={type.code}>
                        {type.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Category</Label>
                <Input
                  value={form.category}
                  placeholder={selectedType?.category || 'Category'}
                  onChange={(event) =>
                    updateForm('category', event.target.value)
                  }
                />
              </div>
              <div className="space-y-2">
                <Label>Name</Label>
                <Input value={form.applicantName} readOnly />
              </div>
              <div className="space-y-2">
                <Label>Contact</Label>
                <Input value={form.contact} readOnly />
              </div>
              <div className="space-y-2">
                <Label htmlFor="estate-service-property">Property / unit / plot</Label>
                <Select
                  value={form.propertyReference || undefined}
                  onValueChange={updatePropertyReference}
                  disabled={properties.length === 0}
                >
                  <SelectTrigger id="estate-service-property">
                    <SelectValue placeholder={properties.length === 0 ? 'No linked properties' : 'Select your property'} />
                  </SelectTrigger>
                  <SelectContent>
                    {properties.map((property) => {
                      const reference = propertyReference(property);
                      return (
                        <SelectItem key={property.id} value={reference}>
                          {reference} · {property.name}
                        </SelectItem>
                      );
                    })}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Location</Label>
                <Input value={form.location} readOnly />
              </div>
              <div className="space-y-2">
                <Label>Urgency</Label>
                <Select
                  value={form.priority}
                  onValueChange={(value) => updateForm('priority', value)}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {['Low', 'Normal', 'High', 'Urgent'].map((priority) => (
                      <SelectItem key={priority} value={priority}>
                        {priority}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {extraFields.length > 0 ? (
              <div className="grid gap-4 md:grid-cols-2">
                {extraFields.map((field) => renderExtraField(field))}
              </div>
            ) : null}
            <div className="space-y-2">
              <Label>Service impact</Label>
              <Select
                value={form.serviceImpact}
                onValueChange={(value) => updateForm('serviceImpact', value)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {[
                    'No service impact',
                    'Tenant affected',
                    'Common area affected',
                    'Safety risk',
                    'Access restricted',
                    'Utility outage',
                    'Unit block required',
                  ].map((impact) => (
                    <SelectItem key={impact} value={impact}>
                      {impact}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="estate-service-description">Description</Label>
              <Textarea
                id="estate-service-description"
                className="min-h-[140px]"
                value={form.description}
                onChange={(event) =>
                  updateForm('description', event.target.value)
                }
              />
            </div>
            <div className="flex justify-end gap-2 border-t pt-4">
              <Button
                type="button"
                variant="outline"
                disabled={isSaving}
                onClick={() => setIsCreateOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={isSaving}>
                {isSaving ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Send className="mr-2 h-4 w-4" />
                )}
                Submit request
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      <section className="space-y-3">
        <h2 className="text-base font-semibold text-slate-900">My Requests</h2>
        <div className="overflow-x-auto rounded-md border">
          <Table className="min-w-[840px]">
            <TableHeader>
              <TableRow>
                <TableHead>Reference</TableHead>
                <TableHead>Service</TableHead>
                <TableHead>Property / unit / plot</TableHead>
                <TableHead>Current stage</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Submitted</TableHead>
                <TableHead className="text-right">Action</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading || isRequestPageLoading ? (
                <TableRow>
                  <TableCell
                    colSpan={7}
                    className="py-10 text-center text-slate-500"
                  >
                    <Loader2 className="mr-2 inline h-4 w-4 animate-spin" />
                    Loading requests
                  </TableCell>
                </TableRow>
              ) : requests.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={7}
                    className="py-10 text-center text-slate-500"
                  >
                    No Estate service requests submitted yet.
                  </TableCell>
                </TableRow>
              ) : (
                requests.map((request) => (
                  <TableRow key={request.id}>
                    <TableCell className="font-medium">
                      {request.referenceNumber || 'Pending'}
                    </TableCell>
                    <TableCell>{request.title}</TableCell>
                    <TableCell>
                      {request.fieldValues?.propertyReference || 'Not recorded'}
                    </TableCell>
                    <TableCell>
                      <div>{request.currentStageName || 'Not started'}</div>
                      <div className="text-xs text-muted-foreground">
                        {request.currentAssignedRole || 'Awaiting assignment'}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="secondary">{request.status}</Badge>
                    </TableCell>
                    <TableCell>{formatDate(request.createdAt)}</TableCell>
                    <TableCell className="text-right">
                      <Button asChild type="button" size="sm" variant="outline">
                        <Link
                          href={`/external-portal/estate-services/${request.id}`}
                        >
                          <Eye className="mr-2 h-4 w-4" />
                          Open
                        </Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
        {requestTotalCount > REQUEST_PAGE_SIZE ? (
          <div className="flex items-center justify-between text-xs text-slate-600">
            <span>
              Page {requestPage} of {totalRequestPages}
            </span>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={requestPage <= 1 || isRequestPageLoading}
                onClick={() => void changeRequestPage(requestPage - 1)}
              >
                Previous
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={
                  requestPage >= totalRequestPages || isRequestPageLoading
                }
                onClick={() => void changeRequestPage(requestPage + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        ) : null}
      </section>
    </div>
  );
}
