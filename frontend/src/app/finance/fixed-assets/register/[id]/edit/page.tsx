'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type {
  DepreciationConvention,
  DepreciationMethod,
  FixedAssetCategory,
  FixedAssetStatus,
  UpdateFixedAssetDto,
} from '@/types/fixed-assets';

const toDateInput = (value?: string) => (value ? value.slice(0, 10) : '');

export default function EditFixedAssetPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = React.use(params);
  const router = useRouter();
  const { toast } = useToast();
  const [categories, setCategories] = useState<FixedAssetCategory[]>([]);
  const [formData, setFormData] = useState<UpdateFixedAssetDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const loadData = async () => {
      try {
        setLoading(true);
        const [asset, categoryData] = await Promise.all([
          fixedAssetsDataService.getAssetById(id),
          fixedAssetsDataService.getCategories(),
        ]);

        setCategories(categoryData);
        setFormData({
          assetCode: asset.assetCode,
          name: asset.name,
          description: asset.description,
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
    }
  };

  if (loading || !formData) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

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
        <Button onClick={handleSave} disabled={saving}>
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
              onChange={(e) => setFormData({ ...formData, purchaseDate: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="serviceDate">Placed In Service</Label>
            <Input
              id="serviceDate"
              type="date"
              value={formData.placedInServiceDate || ''}
              onChange={(e) => setFormData({ ...formData, placedInServiceDate: e.target.value })}
            />
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
              </SelectContent>
            </Select>
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
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="purchasePrice">Purchase Price</Label>
            <Input
              id="purchasePrice"
              type="number"
              value={formData.purchasePrice}
              onChange={(e) => setFormData({ ...formData, purchasePrice: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="installationCost">Installation Cost</Label>
            <Input
              id="installationCost"
              type="number"
              value={formData.installationCost}
              onChange={(e) => setFormData({ ...formData, installationCost: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="taxAmount">Tax Amount</Label>
            <Input
              id="taxAmount"
              type="number"
              value={formData.taxAmount}
              onChange={(e) => setFormData({ ...formData, taxAmount: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="acquisitionCost">Acquisition Cost</Label>
            <Input
              id="acquisitionCost"
              type="number"
              value={formData.acquisitionCost ?? ''}
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
            >
              <SelectTrigger id="method">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="StraightLine">Straight Line</SelectItem>
                <SelectItem value="DecliningBalance">Declining Balance</SelectItem>
                <SelectItem value="DoubleDecliningBalance">Double Declining Balance</SelectItem>
                <SelectItem value="SumOfYearsDigits">Sum of Years Digits</SelectItem>
                <SelectItem value="UnitsOfProduction">Units of Production</SelectItem>
                <SelectItem value="None">None</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="convention">Depreciation Convention</Label>
            <Select
              value={formData.depreciationConvention}
              onValueChange={(value) => setFormData({ ...formData, depreciationConvention: value as DepreciationConvention })}
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
              onChange={(e) => setFormData({ ...formData, usefulLifeMonths: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="residualValue">Residual Value</Label>
            <Input
              id="residualValue"
              type="number"
              value={formData.residualValue}
              onChange={(e) => setFormData({ ...formData, residualValue: Number(e.target.value) })}
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
