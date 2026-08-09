'use client';

import React from 'react';
import { ClipboardList, Loader2, Send, Wrench } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  externalEstateServicesService,
  type ExternalEstateRequestType,
  type ExternalEstateServiceRequest,
} from '@/services/external-estate-services.service';

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
  targetDate: string;
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
    { key: 'accessInstructions', label: 'Access instructions', type: 'textarea' },
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
      options: ['Offer Letter', 'Right of Entry', 'Lease', 'Rent Card', 'Allocation Letter', 'Other'],
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
    { key: 'transferorName', label: 'Transferor' },
    { key: 'transfereeName', label: 'Transferee' },
    { key: 'considerationAmount', label: 'Consideration amount', type: 'number' },
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
      options: ['Undeveloped', 'Partially developed', 'Substantially developed', 'Completed'],
    },
    { key: 'buildingPermitReference', label: 'Building permit reference' },
    { key: 'dateOfTenancy', label: 'Date of tenancy', type: 'date' },
  ],
  additionalLand: [
    { key: 'adjoiningPlotNumber', label: 'Adjoining plot number' },
    { key: 'additionalLandSizeAcres', label: 'Additional land size (acres)', type: 'number' },
    { key: 'recommendation', label: 'Reason for additional land', type: 'textarea' },
  ],
  changeOfUse: [
    { key: 'existingUse', label: 'Existing use', type: 'select', options: landUseOptions },
    { key: 'newUse', label: 'Proposed use', type: 'select', options: landUseOptions },
    { key: 'plotSizeAcres', label: 'Plot size (acres)', type: 'number' },
  ],
  leaseRenewal: [
    { key: 'existingLeaseExpiryDate', label: 'Existing lease expiry date', type: 'date' },
    { key: 'yearsToExpiry', label: 'Years to expiry', type: 'number' },
    { key: 'developmentStatus', label: 'Development proposal / status' },
  ],
  landApplication: [
    { key: 'landUse', label: 'Intended land use', type: 'select', options: landUseOptions },
    { key: 'plotSizeAcres', label: 'Preferred plot size (acres)', type: 'number' },
  ],
  traditionalLand: [
    { key: 'landUse', label: 'Land use', type: 'select', options: landUseOptions },
    { key: 'plotSizeAcres', label: 'Plot size (acres)', type: 'number' },
    { key: 'traditionalCouncil', label: 'Traditional Council / Stool' },
  ],
  tenancyRecognition: [
    { key: 'declarationReference', label: 'Statutory declaration reference' },
    { key: 'dateOfTenancy', label: 'Date of tenancy', type: 'date' },
    { key: 'arrearsStatus', label: 'Rent / arrears position' },
  ],
  hosConversion: [
    { key: 'houseType', label: 'House type' },
    { key: 'sellingPrice', label: 'Selling price', type: 'number' },
    { key: 'dateOfTenancy', label: 'Date of tenancy', type: 'date' },
  ],
  regularisation: [
    {
      key: 'regularisationApproach',
      label: 'Regularisation approach',
      type: 'select',
      options: ['Direct approach', 'Indirect approach'],
    },
    { key: 'plotSizeAcres', label: 'Plot size (acres)', type: 'number' },
    { key: 'planLayoutStatus', label: 'Planning layout status' },
  ],
  rightOfEntry: [
    { key: 'allocationReference', label: 'Allocation reference' },
    { key: 'offerLetterReference', label: 'Offer Letter reference' },
    { key: 'acceptanceDate', label: 'Acceptance date', type: 'date' },
    { key: 'paymentConfirmationReference', label: 'Payment confirmation reference' },
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
  targetDate: '',
  description: '',
  additionalValues: {},
} satisfies EstateServiceFormState;

function formatDate(value?: string | null) {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'Not recorded' : date.toLocaleDateString();
}

export default function ExternalEstateServicesPage() {
  const [requestTypes, setRequestTypes] = React.useState<
    ExternalEstateRequestType[]
  >([]);
  const [requests, setRequests] = React.useState<ExternalEstateServiceRequest[]>(
    []
  );
  const [form, setForm] = React.useState<EstateServiceFormState>(initialForm);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [success, setSuccess] = React.useState<string | null>(null);

  const selectedType = React.useMemo(
    () => requestTypes.find((type) => type.code === form.requestType),
    [form.requestType, requestTypes]
  );
  const extraFields = React.useMemo(
    () => extraFieldsByRequestType[form.requestType] ?? [],
    [form.requestType]
  );

  const loadData = React.useCallback(async () => {
    const [types, submittedRequests] = await Promise.all([
      externalEstateServicesService.getRequestTypes(),
      externalEstateServicesService.getMyRequests(),
    ]);
    setRequestTypes(types);
    setRequests(
      submittedRequests.filter(
        (request) =>
          request.sourceDepartment !== 'External Portal - Estate Listings'
      )
    );
    setForm((current) => ({
      ...current,
      requestType: current.requestType || types[0]?.code || '',
    }));
  }, []);

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
          onChange={(event) => updateAdditionalValue(field.key, event.target.value)}
        />
      </div>
    );
  };

  const submitRequest = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setSuccess(null);

    if (!form.requestType || !form.description.trim()) {
      setError('Request type and description are required.');
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
        targetDate: form.targetDate || undefined,
        description: form.description.trim(),
        additionalValues,
      });
      setRequests((current) => [created, ...current]);
      setSuccess(`Request ${created.referenceNumber || created.title} submitted.`);
      setForm((current) => ({
        ...initialForm,
        requestType: current.requestType,
      }));
    } catch {
      setError('Could not submit the Estate service request.');
    } finally {
      setIsSaving(false);
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
        {isLoading ? (
          <Badge variant="outline" className="w-fit">
            <Loader2 className="mr-2 h-3 w-3 animate-spin" />
            Loading
          </Badge>
        ) : (
          <Badge variant="outline" className="w-fit">
            {requests.length} request{requests.length === 1 ? '' : 's'}
          </Badge>
        )}
      </div>

      {error ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}
      {success ? (
        <div className="rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700">
          {success}
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_420px]">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Wrench className="h-5 w-5 text-blue-600" />
              New Request
            </CardTitle>
          </CardHeader>
          <CardContent>
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
                    onChange={(event) => updateForm('category', event.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Name</Label>
                  <Input
                    value={form.applicantName}
                    onChange={(event) =>
                      updateForm('applicantName', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Contact</Label>
                  <Input
                    value={form.contact}
                    onChange={(event) => updateForm('contact', event.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Property / unit / plot</Label>
                  <Input
                    value={form.propertyReference}
                    onChange={(event) =>
                      updateForm('propertyReference', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label>Location</Label>
                  <Input
                    value={form.location}
                    onChange={(event) => updateForm('location', event.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Priority</Label>
                  <Select
                    value={form.priority}
                    onValueChange={(value) => updateForm('priority', value)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {['Low', 'Normal', 'High', 'Emergency'].map((priority) => (
                        <SelectItem key={priority} value={priority}>
                          {priority}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Target date</Label>
                  <Input
                    type="date"
                    value={form.targetDate}
                    onChange={(event) =>
                      updateForm('targetDate', event.target.value)
                    }
                  />
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
                <Label>Description</Label>
                <Textarea
                  className="min-h-[140px]"
                  value={form.description}
                  onChange={(event) =>
                    updateForm('description', event.target.value)
                  }
                />
              </div>
              <Button className="w-full gap-2" disabled={isSaving}>
                {isSaving ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Send className="h-4 w-4" />
                )}
                Submit request
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ClipboardList className="h-5 w-5 text-blue-600" />
              My Requests
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {requests.length === 0 && !isLoading ? (
              <div className="rounded-md border border-dashed p-6 text-center text-sm text-slate-500">
                No Estate service requests submitted yet.
              </div>
            ) : null}
            {requests.map((request) => (
              <div key={request.id} className="rounded-md border p-3 text-sm">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <div className="font-medium text-slate-900">
                      {request.referenceNumber || request.title}
                    </div>
                    <div className="mt-1 text-xs text-slate-500">
                      {request.title}
                    </div>
                  </div>
                  <Badge variant="secondary">{request.status}</Badge>
                </div>
                <div className="mt-3 grid gap-2 text-xs text-slate-600">
                  <div>{request.currentStageName}</div>
                  <div>{request.currentAssignedRole || 'Awaiting assignment'}</div>
                  <div>{formatDate(request.createdAt)}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
