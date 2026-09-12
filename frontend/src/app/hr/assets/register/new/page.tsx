'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  AssetForm,
  EMPTY_ASSET_FORM,
  formValuesToPayload,
  type AssetFormValues,
} from '@/components/hr/assets/AssetForm';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';
import type { CreateCompanyAssetRequest } from '@/types/hr/assets';

/**
 * Registering an asset.
 *
 * ⚠ `nextMaintenanceDate` is stripped from the payload here — the create DTO has no such field, so
 * sending it would bind to nothing, answer 201, and leave the column null. That is not theoretical:
 * slice 11's harness did exactly this and its "due for service" fixture landed on no list at all.
 * A newly registered asset that needs servicing is therefore correctly *unscheduled* until somebody
 * dates it, which is the whole reason the unscheduled watchlist exists.
 */
export default function NewAssetPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [values, setValues] = useState<AssetFormValues>(EMPTY_ASSET_FORM);

  const create = useMutation({
    mutationFn: () => {
      const { nextMaintenanceDate, lastMaintenanceDate, ...rest } = formValuesToPayload(values);
      void nextMaintenanceDate;
      void lastMaintenanceDate;
      return assetRegisterService.createAsset(rest as CreateCompanyAssetRequest);
    },
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({ title: 'Asset registered', description: created.assetNumber });
      router.push(`/hr/assets/register/${created.id}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not register the asset', description: e.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Register an asset"
        description="Add something the organisation owns to the register."
        backHref="/hr/assets/register"
      />
      <AssetForm
        values={values}
        onChange={setValues}
        onSubmit={() => create.mutate()}
        saving={create.isPending}
        submitLabel="Register asset"
      />
    </div>
  );
}
