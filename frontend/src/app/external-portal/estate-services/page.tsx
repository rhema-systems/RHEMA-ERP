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
};

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
  const [form, setForm] = React.useState(initialForm);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [success, setSuccess] = React.useState<string | null>(null);

  const selectedType = React.useMemo(
    () => requestTypes.find((type) => type.code === form.requestType),
    [form.requestType, requestTypes]
  );

  const loadData = React.useCallback(async () => {
    const [types, submittedRequests] = await Promise.all([
      externalEstateServicesService.getRequestTypes(),
      externalEstateServicesService.getMyRequests(),
    ]);
    setRequestTypes(types);
    setRequests(submittedRequests);
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

  const updateForm = (field: keyof typeof initialForm, value: string) => {
    setForm((current) => ({ ...current, [field]: value }));
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
      const created = await externalEstateServicesService.createRequest({
        requestType: form.requestType,
        applicantName: form.applicantName.trim(),
        contact: form.contact.trim(),
        propertyReference: form.propertyReference.trim(),
        location: form.location.trim(),
        category: form.category.trim(),
        priority: form.priority,
        serviceImpact: form.serviceImpact,
        targetDate: form.targetDate || undefined,
        description: form.description.trim(),
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
                    onValueChange={(value) => updateForm('requestType', value)}
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
