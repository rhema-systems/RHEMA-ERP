'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  IdentificationTypeForm,
  type IdentificationTypeFormValues,
} from '@/components/hr/lookup/IdentificationTypeForm';
import { identificationTypeService } from '@/services/hr/lookup.service';
import { countryService } from '@/services/hr/country.service';

export default function EditIdentificationTypePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);
  const [savingModules, setSavingModules] = useState(false);
  const [selectedModuleIds, setSelectedModuleIds] = useState<string[]>([]);

  const { data: type, isLoading, isError } = useQuery({
    queryKey: ['hr', 'identification-types', id],
    queryFn: () => identificationTypeService.getById(id),
    enabled: !!id,
  });

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const moduleQuery = useQuery({
    queryKey: ['hr', 'identification-types', id, 'modules'],
    queryFn: () => identificationTypeService.getModuleAvailability(id),
    enabled: !!id && !!type,
  });

  useEffect(() => {
    if (moduleQuery.data) {
      setSelectedModuleIds(
        moduleQuery.data.modules.filter((module) => module.isSelected).map((module) => module.tenantModuleId),
      );
    }
  }, [moduleQuery.data]);

  const savedModuleIds = useMemo(
    () =>
      moduleQuery.data?.modules
        .filter((module) => module.isSelected)
        .map((module) => module.tenantModuleId)
        .sort()
        .join('|') ?? '',
    [moduleQuery.data],
  );
  const moduleSelectionChanged = [...selectedModuleIds].sort().join('|') !== savedModuleIds;

  const handleSubmit = async (values: IdentificationTypeFormValues) => {
    setSubmitting(true);
    try {
      await identificationTypeService.update(id, {
        id,
        name: values.name,
        code: values.code || null,
        description: values.description || null,
        issuingAuthorityName: values.issuingAuthorityName,
        issuingCountryId: values.issuingCountryId || null,
        hasExpiryDate: values.hasExpiryDate,
        // Blank stays null, and a type that does not expire cannot carry a warning time at all —
        // the sweep would ignore it, so storing one would be a setting that only looks like a
        // feature. Both are the same instruction to the server: raise nothing.
        expiryNotificationLeadDays:
          values.hasExpiryDate && values.expiryNotificationLeadDays
            ? Number(values.expiryNotificationLeadDays)
            : null,
        isActive: values.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'identification-types'] });
      toast({ title: 'Success', description: 'Identification type updated.' });
      router.push('/administration/hr/identification-types');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update identification type.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const saveModuleAvailability = async () => {
    setSavingModules(true);
    try {
      const saved = await identificationTypeService.updateModuleAvailability(id, selectedModuleIds);
      queryClient.setQueryData(['hr', 'identification-types', id, 'modules'], saved);
      toast({ title: 'Module availability saved', description: 'The selected modules can now use this identification type.' });
    } catch (error: any) {
      toast({
        title: 'Could not save module availability',
        description: error?.message || 'Please review the selection and try again.',
        variant: 'destructive',
      });
    } finally {
      setSavingModules(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-4xl mx-auto">
      <PageHeader
        title="Edit Identification Type"
        description={type ? type.name : 'Update this identification type.'}
        backHref="/administration/hr/identification-types"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !type ? (
        <EmptyState
          title="Identification type not found"
          description="This type may have been deleted."
        />
      ) : (
        <>
        <IdentificationTypeForm
          defaultValues={{
            name: type.name,
            code: type.code ?? '',
            issuingAuthorityName: type.issuingAuthorityName,
            issuingCountryId: type.issuingCountryId ?? '',
            description: type.description ?? '',
            hasExpiryDate: type.hasExpiryDate,
            expiryNotificationLeadDays:
              type.expiryNotificationLeadDays == null
                ? ''
                : String(type.expiryNotificationLeadDays),
            isActive: type.isActive,
          }}
          countries={countries ?? []}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push('/administration/hr/identification-types')}
        />
        <Card>
          <CardHeader>
            <CardTitle>Module availability</CardTitle>
            <CardDescription>
              Choose where this identification type is offered. This selection is saved separately from the identification type details above.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {moduleQuery.isLoading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading tenant modules…
              </div>
            ) : moduleQuery.isError ? (
              <p className="text-sm text-destructive">
                Module availability could not be loaded. Employee Admin permission is required to manage it.
              </p>
            ) : moduleQuery.data?.modules.length ? (
              <div className="grid gap-3 sm:grid-cols-2">
                {moduleQuery.data.modules.map((module) => {
                  const checked = selectedModuleIds.includes(module.tenantModuleId);
                  return (
                    <div key={module.tenantModuleId} className="flex items-start gap-3 rounded-lg border p-3">
                      <Checkbox
                        id={`module-${module.tenantModuleId}`}
                        checked={checked}
                        onCheckedChange={(next) =>
                          setSelectedModuleIds((current) =>
                            next === true
                              ? [...new Set([...current, module.tenantModuleId])]
                              : current.filter((moduleId) => moduleId !== module.tenantModuleId),
                          )
                        }
                      />
                      <Label htmlFor={`module-${module.tenantModuleId}`} className="flex-1 cursor-pointer">
                        <span className="block font-medium">{module.moduleName}</span>
                        <span className="text-xs font-normal text-muted-foreground">{module.status}</span>
                      </Label>
                    </div>
                  );
                })}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">No tenant modules are configured.</p>
            )}

            <div className="flex justify-end border-t pt-4">
              <Button
                type="button"
                onClick={saveModuleAvailability}
                disabled={savingModules || moduleQuery.isLoading || moduleQuery.isError || !moduleSelectionChanged}
              >
                {savingModules && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save module availability
              </Button>
            </div>
          </CardContent>
        </Card>
        </>
      )}
    </div>
  );
}
