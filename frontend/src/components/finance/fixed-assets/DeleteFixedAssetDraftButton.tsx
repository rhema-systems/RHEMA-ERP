'use client';

import React, { useState } from 'react';
import { Loader2, Trash2 } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { FixedAsset } from '@/types/fixed-assets';

type DeleteFixedAssetDraftButtonProps = {
  asset: Pick<FixedAsset, 'id' | 'assetCode' | 'name' | 'status'>;
  onDeleted: () => void | Promise<void>;
  compact?: boolean;
};

export function DeleteFixedAssetDraftButton({ asset, onDeleted, compact = false }: DeleteFixedAssetDraftButtonProps) {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  if (asset.status !== 'Draft' || !hasPermission('Finance.FixedAssets.Manage')) return null;

  const handleDelete = async () => {
    try {
      setDeleting(true);
      await fixedAssetsDataService.deleteAsset(asset.id);
      toast({ title: 'Draft asset deleted', description: `${asset.assetCode} can now be corrected and re-imported.` });
      setOpen(false);
      await onDeleted();
    } catch (error: unknown) {
      toast({
        title: 'Draft asset was not deleted',
        description: error instanceof Error ? error.message : 'The draft may now have approval or posting evidence. Refresh the register and retry.',
        variant: 'destructive',
      });
    } finally {
      setDeleting(false);
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={(nextOpen) => !deleting && setOpen(nextOpen)}>
      <AlertDialogTrigger asChild>
        <Button variant="destructive" size={compact ? 'icon' : 'default'} aria-label={`Delete draft asset: ${asset.name}`}>
          <Trash2 className={compact ? 'h-4 w-4' : 'mr-2 h-4 w-4'} />
          {!compact && 'Delete draft'}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete draft asset {asset.assetCode}?</AlertDialogTitle>
          <AlertDialogDescription>
            This permanently removes the unposted draft and its imported opening register evidence.
            Use this when the spreadsheet values are wrong and you need to re-import the same asset code.
            Posted or approval-controlled assets cannot be deleted.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleting}>Keep draft</AlertDialogCancel>
          <AlertDialogAction
            onClick={(event) => { event.preventDefault(); void handleDelete(); }}
            disabled={deleting}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {deleting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Delete draft
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
