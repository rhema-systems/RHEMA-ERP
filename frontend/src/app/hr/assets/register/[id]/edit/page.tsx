'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  AssetForm,
  assetToFormValues,
  EMPTY_ASSET_FORM,
  formValuesToPayload,
  type AssetFormValues,
} from '@/components/hr/assets/AssetForm';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';

/**
 * Editing a registered asset.
 *
 * ⚠ **The update is full-replace** (defect D-j): any field the payload omits is written as null,
 * and `id` must appear in the body as well as the route or the call answers `400 ID mismatch`. So
 * the form is loaded from the record and the whole record is sent back — never a diff. A screen
 * that posted only the changed fields here would silently erase the maintenance interval, the
 * insurance expiry and the rental rate of every asset it touched.
 */
export default function EditAssetPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [values, setValues] = useState<AssetFormValues>(EMPTY_ASSET_FORM);

  const { data: asset, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'register', id],
    queryFn: () => assetRegisterService.getAsset(id),
    enabled: Boolean(id),
  });

  useEffect(() => {
    if (asset) setValues(assetToFormValues(asset));
  }, [asset]);

  const save = useMutation({
    mutationFn: () =>
      assetRegisterService.updateAsset(id, { id, ...formValuesToPayload(values) }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({ title: 'Asset updated' });
      router.push(`/hr/assets/register/${id}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not save the asset', description: e.message, variant: 'destructive' }),
  });

  if (isLoading || !asset) {
    return (
      <div className="flex justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={asset.assetName}
        description={`${asset.assetNumber} · editing the register record`}
        backHref={`/hr/assets/register/${id}`}
      />
      <AssetForm
        values={values}
        onChange={setValues}
        onSubmit={() => save.mutate()}
        saving={save.isPending}
        submitLabel="Save changes"
        financeOwned={asset.isFinanceOwned}
        allowNextMaintenanceDate
      />
    </div>
  );
}
