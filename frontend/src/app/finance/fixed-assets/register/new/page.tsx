'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowLeft, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { CreateFixedAssetDto, DepreciationConvention, DepreciationMethod, FixedAssetCategory } from '@/types/fixed-assets';

export default function NewFixedAssetPage() {
  const { toast } = useToast();
  const [categories, setCategories] = useState<FixedAssetCategory[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [formData, setFormData] = useState<CreateFixedAssetDto>({
    assetCode: '',
    name: '',
    description: '',
    fixedAssetCategoryId: '',
    purchaseDate: new Date().toISOString().slice(0, 10),
    placedInServiceDate: '',
    purchasePrice: 0,
    installationCost: 0,
    taxAmount: 0,
    acquisitionCost: undefined,
    depreciationMethod: 'StraightLine',
    depreciationConvention: 'FullMonth',
    usefulLifeMonths: 36,
    residualValue: 0,
    maintenanceAssetId: '',
    serialNumber: '',
  });

  useEffect(() => {
    const loadCategories = async () => {
      try {
        const data = await fixedAssetsDataService.getCategories();
        setCategories(data);
      } catch (error) {
        console.error('Failed to load categories:', error);
      }
    };

    loadCategories();
  }, []);

  const handleSave = async () => {
    if (!formData.assetCode || !formData.name || !formData.fixedAssetCategoryId) {
      toast({
        title: 'Validation error',
        description: 'Asset code, name, and category are required.',
        variant: 'destructive',
      });
      return;
    }

    try {
      setIsSaving(true);
      await fixedAssetsDataService.createAsset({
        ...formData,
        placedInServiceDate: formData.placedInServiceDate || undefined,
        maintenanceAssetId: formData.maintenanceAssetId || undefined,
        serialNumber: formData.serialNumber || undefined,
      });
      toast({
        title: 'Fixed asset created',
        description: 'Asset saved successfully.',
      });
      window.location.href = '/finance/fixed-assets/register';
    } catch (error) {
      console.error('Failed to create asset:', error);
      toast({
        title: 'Error',
        description: 'Failed to create fixed asset.',
        variant: 'destructive',
      });
    } finally {
      setIsSaving(false);
    }
  };

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
            <h1 className="text-3xl font-bold tracking-tight">New Fixed Asset</h1>
            <p className="text-muted-foreground">Create a fixed asset and set depreciation details.</p>
          </div>
        </div>
        <Button onClick={handleSave} disabled={isSaving}>
          <Save className="mr-2 h-4 w-4" />
          Save Asset
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
            <Label htmlFor="acquisitionCost">Acquisition Cost (optional)</Label>
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
