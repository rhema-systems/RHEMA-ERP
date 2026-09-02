'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Loader2, Save, Play, Pause, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import { FixedAssetCapitalizationReversalPanel } from '@/components/finance/FixedAssetCapitalizationReversalPanel';
import { FixedAssetCapitalizationApprovalPanel } from '@/components/finance/FixedAssetCapitalizationApprovalPanel';
import { SourceDocumentDimensionEvidence } from '@/components/finance/dimensions/source-document-dimension-panel';
import { AssetLocationCombobox } from '@/components/finance/fixed-assets/AssetLocationCombobox';
import type {
  DepreciationConvention,
  DepreciationMethod,
  FixedAsset,
  FixedAssetCategory,
  FixedAssetLocationOption,
  FixedAssetStatus,
  UpdateFixedAssetDto,
} from '@/types/fixed-assets';

const toDateInput = (value?: string) => (value ? value.slice(0, 10) : '');

export default function EditFixedAssetPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = React.use(params);
  const router = useRouter();
  const { toast } = useToast();
  const [categories, setCategories] = useState<FixedAssetCategory[]>([]);
  const [locationOptions, setLocationOptions] = useState<FixedAssetLocationOption[]>([]);
  const [asset, setAsset] = useState<FixedAsset | null>(null);
  const [formData, setFormData] = useState<UpdateFixedAssetDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const loadData = async () => {
      try {
        setLoading(true);
        const [asset, categoryData, locationData] = await Promise.all([
          fixedAssetsDataService.getAssetById(id),
          fixedAssetsDataService.getCategories(),
          fixedAssetsDataService.getLocationOptions(),
        ]);

        setCategories(categoryData);
        setLocationOptions(locationData);
        setAsset(asset);
        setFormData({
          assetCode: asset.assetCode,
          name: asset.name,
          description: asset.description,
          location: asset.location,
          fixedAssetCategoryId: asset.fixedAssetCategoryId,
          purchaseDate: toDateInput(asset.purchaseDate),
          placedInServiceDate: toDateInput(asset.placedInServiceDate),
          purchasePrice: asset.purchasePrice,
          installationCost: asset.installationCost,
          taxAmount: asset.taxAmount,
          acquisitionCost: asset.acquisitionCost,
          depreciationMethod: asset.depreciationMethod,
          depreciationConvention: asset.depreciationConvention,
          usefulLifeMonths: asset.usefulLifeMonths,
          residualValue: asset.residualValue,
          diminishingBalanceRatePercent: asset.diminishingBalanceRatePercent,
          lifetimeProductionCapacity: asset.lifetimeProductionCapacity,
          status: asset.status,
          disposalDate: toDateInput(asset.disposalDate),
          maintenanceAssetId: asset.maintenanceAssetId,
          serialNumber: asset.serialNumber,
        });
      } catch (error) {
        console.error('Failed to load asset:', error);
        toast({
          title: 'Error',
          description: 'Failed to load fixed asset.',
          variant: 'destructive',
        });
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, [id, toast]);

  const handleSave = async () => {
    if (!formData) return;
    if (!formData.assetCode || !formData.name || !formData.fixedAssetCategoryId) {
      toast({
        title: 'Validation error',
        description: 'Asset code, name, and category are required.',
        variant: 'destructive',
      });
      return;
    }

    try {
      setSaving(true);
      await fixedAssetsDataService.updateAsset(id, {
        ...formData,
        placedInServiceDate: formData.placedInServiceDate || undefined,
        location: formData.location || undefined,
        maintenanceAssetId: formData.maintenanceAssetId || undefined,
        serialNumber: formData.serialNumber || undefined,
        disposalDate: formData.disposalDate || undefined,
      });
      toast({
        title: 'Asset updated',
        description: 'Fixed asset saved successfully.',
      });
      router.push('/finance/fixed-assets/register');
    } catch (error) {
      console.error('Failed to update asset:', error);
      toast({
        title: 'Error',
        description: 'Failed to update fixed asset.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    };
  };

  const refreshAccountingState = async () => {
    const refreshed = await fixedAssetsDataService.getAssetById(id);
    setAsset(refreshed);
    // A posted reversal changes only accounting-owned fields. Preserve any unsaved descriptive
    // edits while refreshing the status and cost evidence shown elsewhere on this workspace.
    setFormData(current => current ? {
      ...current,
      status: refreshed.status,
      purchasePrice: refreshed.purchasePrice,
      installationCost: refreshed.installationCost,
      taxAmount: refreshed.taxAmount,
      acquisitionCost: refreshed.acquisitionCost,
    } : current);
  };

  const handleActivate = async () => {
    try {
      await fixedAssetsDataService.activateAsset(id, formData?.placedInServiceDate || undefined);
      toast({ title: 'Activated', description: 'Asset is now active and eligible for depreciation.' });
      router.refresh();
    } catch (error: unknown) {
      toast({ title: 'Error', description: error instanceof Error ? error.message : 'Failed to activate.', variant: 'destructive' });
    }
  };

  const handleHold = async () => {
    const reason = prompt('Reason for putting on hold:');
    if (reason === null) return;
    try {
      await fixedAssetsDataService.holdAsset(id, reason);
      toast({ title: 'On Hold', description: 'Asset is now on hold. Depreciation is suspended.' });
      router.refresh();
    } catch (error: unknown) {
      toast({ title: 'Error', description: error instanceof Error ? error.message : 'Failed.', variant: 'destructive' });
    }
  };

  const handleResume = async () => {
    try {
      await fixedAssetsDataService.resumeAsset(id);
      toast({ title: 'Resumed', description: 'Asset is active again. Depreciation will resume.' });
      router.refresh();
    } catch (error: unknown) {
      toast({ title: 'Error', description: error instanceof Error ? error.message : 'Failed.', variant: 'destructive' });
    }
  };

  if (loading || !formData) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const accountingLocked = !!asset?.postingEventId && !asset.capitalizationReversalPostingEventId;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link href="/finance/fixed-assets/register">
            <Button variant="outline" size="icon">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Edit Fixed Asset</h1>
            <p className="text-muted-foreground">Update asset details and depreciation settings.</p>
          </div>
        </div>
        <Button onClick={handleSave} disabled={saving || asset?.status === 'PendingApproval' || (!!asset?.capitalizationApprovalApprovedAt && !asset?.capitalizationApprovalInvalidatedAt)}>
          <Save className="mr-2 h-4 w-4" />
          Save Changes
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Asset Details</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="assetCode">Asset Code *</Label>
            <Input
              id="assetCode"
              value={formData.assetCode}
              onChange={(e) => setFormData({ ...formData, assetCode: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="name">Name *</Label>
            <Input
              id="name"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="category">Category *</Label>
            <Select
              value={formData.fixedAssetCategoryId}
              onValueChange={(value) => setFormData({ ...formData, fixedAssetCategoryId: value })}
              disabled={accountingLocked}
            >
              <SelectTrigger id="category">
                <SelectValue placeholder="Select category" />
              </SelectTrigger>
              <SelectContent>
                {categories.map((category) => (
                  <SelectItem key={category.id} value={category.id}>
                    {category.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="purchaseDate">Purchase Date</Label>
            <Input
              id="purchaseDate"
              type="date"
              value={formData.purchaseDate}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, purchaseDate: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="serviceDate">Placed In Service</Label>
            <Input
              id="serviceDate"
              type="date"
              value={formData.placedInServiceDate || ''}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, placedInServiceDate: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="location">Location</Label>
            <AssetLocationCombobox
              id="location"
              options={locationOptions}
              value={locationOptions.find(option => option.displayName === formData.location)?.id}
              placeholder={formData.location ? `Legacy: ${formData.location}` : undefined}
              onValueChange={(location) => setFormData({ ...formData, location: location?.displayName })}
            />
            {formData.location && !locationOptions.some(option => option.displayName === formData.location) && (
              <p className="text-xs text-amber-700">
                This legacy value is not linked to an active HR/Payroll location. Select a configured location to reconcile it.
              </p>
            )}
          </div>
          <div className="space-y-2">
            <Label htmlFor="serialNumber">Serial Number</Label>
            <Input
              id="serialNumber"
              value={formData.serialNumber || ''}
              onChange={(e) => setFormData({ ...formData, serialNumber: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="status">Status</Label>
            <Select
              value={formData.status}
              onValueChange={(value) => setFormData({ ...formData, status: value as FixedAssetStatus })}
              disabled
            >
              <SelectTrigger id="status">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="FullyDepreciated">Fully Depreciated</SelectItem>
                <SelectItem value="Disposed">Disposed</SelectItem>
                <SelectItem value="HeldForSale">Held For Sale</SelectItem>
                <SelectItem value="WrittenOff">Written Off</SelectItem>
                <SelectItem value="UnderConstruction">Under Construction</SelectItem>
                <SelectItem value="OnHold">On Hold</SelectItem>
                <SelectItem value="Acquired">Acquired</SelectItem>
                <SelectItem value="Capitalized">Capitalized</SelectItem>
                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
              </SelectContent>
            </Select>
            {/* Status is accounting-owned. Lifecycle commands and approval workflows change it;
                allowing a normal edit form to do so would disguise the required audit trail. */}
            <p className="text-xs text-muted-foreground">Status changes through lifecycle and approval actions.</p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="disposalDate">Disposal Date</Label>
            <Input
              id="disposalDate"
              type="date"
              value={formData.disposalDate || ''}
              onChange={(e) => setFormData({ ...formData, disposalDate: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Financial Details</CardTitle>
          {accountingLocked && <CardDescription>Posted accounting values are locked. Use the controlled capitalization correction below.</CardDescription>}
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="purchasePrice">Purchase Price</Label>
            <Input
              id="purchasePrice"
              type="number"
              value={formData.purchasePrice}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, purchasePrice: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="installationCost">Installation Cost</Label>
            <Input
              id="installationCost"
              type="number"
              value={formData.installationCost}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, installationCost: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="taxAmount">Tax Amount</Label>
            <Input
              id="taxAmount"
              type="number"
              value={formData.taxAmount}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, taxAmount: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="acquisitionCost">Acquisition Cost</Label>
            <Input
              id="acquisitionCost"
              type="number"
              value={formData.acquisitionCost ?? ''}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, acquisitionCost: e.target.value === '' ? undefined : Number(e.target.value) })}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Depreciation Settings</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="method">Depreciation Method</Label>
            <Select
              value={formData.depreciationMethod}
              onValueChange={(value) => setFormData({ ...formData, depreciationMethod: value as DepreciationMethod })}
              disabled={accountingLocked}
            >
              <SelectTrigger id="method">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="StraightLine">Straight Line</SelectItem>
                <SelectItem value="DecliningBalance">Declining Balance</SelectItem>
                <SelectItem value="DoubleDecliningBalance">Double Declining Balance</SelectItem>
                <SelectItem value="UnitsOfProduction">Units of Production</SelectItem>
              </SelectContent>
            </Select>
          </div>
          {(formData.depreciationMethod === 'DecliningBalance' ||
            formData.depreciationMethod === 'DoubleDecliningBalance') && (
            <div className="space-y-2">
              <Label htmlFor="diminishingRate">Annual Diminishing-Balance Rate (%)</Label>
              <Input
                id="diminishingRate"
                type="number"
                min={0}
                max={100}
                step="0.0001"
                value={formData.diminishingBalanceRatePercent}
                disabled={accountingLocked}
                onChange={(e) => setFormData({ ...formData, diminishingBalanceRatePercent: Number(e.target.value) })}
              />
              <p className="text-xs text-muted-foreground">
                Double-declining may use 0 for the useful-life-derived accelerated rate.
              </p>
            </div>
          )}
          {formData.depreciationMethod === 'UnitsOfProduction' && (
            <div className="space-y-2">
              <Label htmlFor="productionCapacity">Approved Lifetime Production Capacity</Label>
              <Input
                id="productionCapacity"
                type="number"
                min={0}
                step="0.0001"
                value={formData.lifetimeProductionCapacity}
                disabled={accountingLocked}
                onChange={(e) => setFormData({ ...formData, lifetimeProductionCapacity: Number(e.target.value) })}
              />
              <p className="text-xs text-muted-foreground">
                Capitalized assumptions are locked and must not be destructively edited.
              </p>
            </div>
          )}
          <div className="space-y-2">
            <Label htmlFor="convention">Depreciation Convention</Label>
            <Select
              value={formData.depreciationConvention}
              onValueChange={(value) => setFormData({ ...formData, depreciationConvention: value as DepreciationConvention })}
              disabled={accountingLocked}
            >
              <SelectTrigger id="convention">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="FullMonth">Full Month</SelectItem>
                <SelectItem value="MidMonth">Mid Month</SelectItem>
                <SelectItem value="HalfYear">Half Year</SelectItem>
                <SelectItem value="ActualDays">Actual Days</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="usefulLife">Useful Life (Months)</Label>
            <Input
              id="usefulLife"
              type="number"
              value={formData.usefulLifeMonths}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, usefulLifeMonths: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="residualValue">Residual Value</Label>
            <Input
              id="residualValue"
              type="number"
              value={formData.residualValue}
              disabled={accountingLocked}
              onChange={(e) => setFormData({ ...formData, residualValue: Number(e.target.value) })}
            />
          </div>
        </CardContent>
      </Card>

      {asset && (
        <SourceDocumentDimensionEvidence evidence={asset.financeDimensions} />
      )}

      {asset && (
        <FixedAssetCapitalizationApprovalPanel
          asset={asset}
          category={categories.find(category => category.id === asset.fixedAssetCategoryId)}
          onChanged={refreshAccountingState}
        />
      )}

      {asset && (
        <FixedAssetCapitalizationReversalPanel
          asset={asset}
          onChanged={refreshAccountingState}
        />
      )}

      {/* Lifecycle Actions */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Lifecycle Actions</CardTitle>
          <CardDescription>Current status: <Badge variant="outline">{formData.status}</Badge></CardDescription>
        </CardHeader>
        <CardContent className="flex gap-3">
          {formData.status === 'Capitalized' && (
            <Button onClick={handleActivate} className="bg-emerald-600 hover:bg-emerald-700">
              <Play className="h-4 w-4 mr-2" />Activate Asset
            </Button>
          )}
          {formData.status === 'Active' && (
            <Button variant="outline" onClick={handleHold}>
              <Pause className="h-4 w-4 mr-2" />Put on Hold
            </Button>
          )}
          {formData.status === 'OnHold' && (
            <Button onClick={handleResume} className="bg-blue-600 hover:bg-blue-700">
              <RotateCcw className="h-4 w-4 mr-2" />Resume Asset
            </Button>
          )}
          {(formData.status !== 'Capitalized' && formData.status !== 'Disposed' && formData.status !== 'WrittenOff') && formData.status !== 'OnHold' && formData.status !== 'Active' && (
            <p className="text-sm text-muted-foreground">No lifecycle actions available for the current status.</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
