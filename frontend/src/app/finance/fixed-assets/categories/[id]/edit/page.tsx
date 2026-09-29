'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { DepreciationMethod, FixedAssetGlAccountOption, UpdateFixedAssetCategoryDto } from '@/types/fixed-assets';

export default function EditFixedAssetCategoryPage({ params }: { params: Promise<{ id: string }> }) {
  const NONE_VALUE = '__none__';
  const { id } = React.use(params);
  const router = useRouter();
  const { toast } = useToast();
  const [assetAccountOptions, setAssetAccountOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [accumulatedDepOptions, setAccumulatedDepOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [depreciationExpenseOptions, setDepreciationExpenseOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [gainOnDisposalOptions, setGainOnDisposalOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [lossOnDisposalOptions, setLossOnDisposalOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [revaluationSurplusOptions, setRevaluationSurplusOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [aucAccountOptions, setAucAccountOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [formData, setFormData] = useState<UpdateFixedAssetCategoryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    const loadCategory = async () => {
      try {
        setLoading(true);
        const [category, glAccounts] = await Promise.all([
          fixedAssetsDataService.getCategoryById(id),
          fixedAssetsDataService.getGlAccounts(),
        ]);
        setAssetAccountOptions(glAccounts.assetAccounts);
        setAccumulatedDepOptions(glAccounts.accumulatedDepreciationAccounts);
        setDepreciationExpenseOptions(glAccounts.depreciationExpenseAccounts);
        setGainOnDisposalOptions(glAccounts.gainOnDisposalAccounts);
        setLossOnDisposalOptions(glAccounts.lossOnDisposalAccounts);
        setRevaluationSurplusOptions(glAccounts.revaluationSurplusAccounts || []);
        setAucAccountOptions(glAccounts.aucAccounts || []);
        setFormData({
          name: category.name,
          code: category.code,
          description: category.description,
          requiresMaintenance: category.requiresMaintenance,
          defaultMethod: category.defaultMethod,
          defaultUsefulLifeMonths: category.defaultUsefulLifeMonths,
          defaultResidualValuePercent: category.defaultResidualValuePercent,
          defaultDiminishingBalanceRatePercent: category.defaultDiminishingBalanceRatePercent,
          defaultLifetimeProductionCapacity: category.defaultLifetimeProductionCapacity,
          assetAccountId: category.assetAccountId,
          accumulatedDepreciationAccountId: category.accumulatedDepreciationAccountId,
          depreciationExpenseAccountId: category.depreciationExpenseAccountId,
          gainOnDisposalAccountId: category.gainOnDisposalAccountId,
          lossOnDisposalAccountId: category.lossOnDisposalAccountId,
          revaluationSurplusAccountId: category.revaluationSurplusAccountId,
          aucAccountId: category.aucAccountId,
        });
      } catch (error) {
        console.error('Failed to load category:', error);
        toast({
          title: 'Error',
          description: 'Failed to load fixed asset category.',
          variant: 'destructive',
        });
      } finally {
        setLoading(false);
      }
    };

    loadCategory();
  }, [id, toast]);

  const handleSave = async () => {
    if (!formData) return;
    if (!formData.name || !formData.code) {
      toast({
        title: 'Validation error',
        description: 'Name and code are required.',
        variant: 'destructive',
      });
      return;
    }

    try {
      setSaving(true);
      await fixedAssetsDataService.updateCategory(id, {
        ...formData,
        gainOnDisposalAccountId: formData.gainOnDisposalAccountId || undefined,
        lossOnDisposalAccountId: formData.lossOnDisposalAccountId || undefined,
        revaluationSurplusAccountId: formData.revaluationSurplusAccountId || undefined,
        aucAccountId: formData.aucAccountId || undefined,
      });
      toast({
        title: 'Category updated',
        description: 'Fixed asset category saved successfully.',
      });
      router.push('/finance/fixed-assets/categories');
    } catch (error) {
      console.error('Failed to update category:', error);
      toast({
        title: 'Error',
        description: 'Failed to update fixed asset category.',
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
          <Link href="/finance/fixed-assets/categories">
            <Button variant="outline" size="icon">
              <ArrowLeft className="h-4 w-4" />
            </Button>
          </Link>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Edit Category</h1>
            <p className="text-muted-foreground">Update fixed asset category defaults and GL mappings.</p>
          </div>
        </div>
        <Button onClick={handleSave} disabled={saving}>
          <Save className="mr-2 h-4 w-4" />
          Save Changes
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Category Details</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name *</Label>
            <Input
              id="name"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="code">Code *</Label>
            <Input
              id="code"
              value={formData.code}
              onChange={(e) => setFormData({ ...formData, code: e.target.value })}
            />
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="description">Description</Label>
            <Input
              id="description"
              value={formData.description || ''}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
            />
          </div>
          <div className="flex items-start gap-3 rounded-md border p-4 md:col-span-2">
            <Checkbox
              id="requiresMaintenance"
              checked={formData.requiresMaintenance}
              onCheckedChange={(checked) => setFormData({ ...formData, requiresMaintenance: checked === true })}
            />
            <div className="space-y-1">
              <Label htmlFor="requiresMaintenance">Requires maintenance</Label>
              <p className="text-sm text-muted-foreground">
                Makes assets in this category available to Maintenance through the read-only integration feed.
              </p>
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="defaultMethod">Default Depreciation Method</Label>
            <Select
              value={formData.defaultMethod}
              onValueChange={(value) => setFormData({ ...formData, defaultMethod: value as DepreciationMethod })}
            >
              <SelectTrigger id="defaultMethod">
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
          {(formData.defaultMethod === 'DecliningBalance' ||
            formData.defaultMethod === 'DoubleDecliningBalance') && (
            <div className="space-y-2">
              <Label htmlFor="defaultDiminishingRate">Default Annual Rate (%)</Label>
              <Input
                id="defaultDiminishingRate"
                type="number"
                min={0}
                max={100}
                step="0.0001"
                value={formData.defaultDiminishingBalanceRatePercent}
                onChange={(e) => setFormData({
                  ...formData,
                  defaultDiminishingBalanceRatePercent: Number(e.target.value),
                })}
              />
            </div>
          )}
          {formData.defaultMethod === 'UnitsOfProduction' && (
            <div className="space-y-2">
              <Label htmlFor="defaultProductionCapacity">Default Lifetime Capacity</Label>
              <Input
                id="defaultProductionCapacity"
                type="number"
                min={0}
                step="0.0001"
                value={formData.defaultLifetimeProductionCapacity}
                onChange={(e) => setFormData({
                  ...formData,
                  defaultLifetimeProductionCapacity: Number(e.target.value),
                })}
              />
            </div>
          )}
          <div className="space-y-2">
            <Label htmlFor="defaultLife">Default Useful Life (months)</Label>
            <Input
              id="defaultLife"
              type="number"
              value={formData.defaultUsefulLifeMonths}
              onChange={(e) => setFormData({ ...formData, defaultUsefulLifeMonths: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="residualPercent">Residual Value %</Label>
            <Input
              id="residualPercent"
              type="number"
              value={formData.defaultResidualValuePercent}
              onChange={(e) => setFormData({ ...formData, defaultResidualValuePercent: Number(e.target.value) })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="assetAccountId">Asset Account</Label>
            <Select
              value={formData.assetAccountId}
              onValueChange={(value) => setFormData({ ...formData, assetAccountId: value })}
            >
              <SelectTrigger id="assetAccountId">
                <SelectValue placeholder="Select asset account" />
              </SelectTrigger>
              <SelectContent>
                {assetAccountOptions.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.accountNumber} - {account.accountName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="accumulatedAccountId">Accumulated Depreciation Account</Label>
            <Select
              value={formData.accumulatedDepreciationAccountId}
              onValueChange={(value) => setFormData({ ...formData, accumulatedDepreciationAccountId: value })}
            >
              <SelectTrigger id="accumulatedAccountId">
                <SelectValue placeholder="Select accumulated depreciation account" />
              </SelectTrigger>
              <SelectContent>
                {accumulatedDepOptions.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.accountNumber} - {account.accountName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="expenseAccountId">Depreciation Expense Account</Label>
            <Select
              value={formData.depreciationExpenseAccountId}
              onValueChange={(value) => setFormData({ ...formData, depreciationExpenseAccountId: value })}
            >
              <SelectTrigger id="expenseAccountId">
                <SelectValue placeholder="Select depreciation expense account" />
              </SelectTrigger>
              <SelectContent>
                {depreciationExpenseOptions.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.accountNumber} - {account.accountName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="gainAccountId">Gain on Disposal Account (optional)</Label>
            <Select
              value={formData.gainOnDisposalAccountId || NONE_VALUE}
              onValueChange={(value) =>
                setFormData({
                  ...formData,
                  gainOnDisposalAccountId: value === NONE_VALUE ? undefined : value,
                })
              }
            >
              <SelectTrigger id="gainAccountId">
                <SelectValue placeholder="Select gain on disposal account" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE_VALUE}>No account</SelectItem>
                {gainOnDisposalOptions.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.accountNumber} - {account.accountName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="lossAccountId">Loss on Disposal Account (optional)</Label>
            <Select
              value={formData.lossOnDisposalAccountId || NONE_VALUE}
              onValueChange={(value) =>
                setFormData({
                  ...formData,
                  lossOnDisposalAccountId: value === NONE_VALUE ? undefined : value,
                })
              }
            >
              <SelectTrigger id="lossAccountId">
                <SelectValue placeholder="Select loss on disposal account" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE_VALUE}>No account</SelectItem>
                {lossOnDisposalOptions.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.accountNumber} - {account.accountName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="revaluationSurplusAccountId">Revaluation Surplus Account (optional)</Label>
            <Select
              value={formData.revaluationSurplusAccountId || NONE_VALUE}
              onValueChange={(value) =>
                setFormData({
                  ...formData,
                  revaluationSurplusAccountId: value === NONE_VALUE ? undefined : value,
                })
              }
            >
              <SelectTrigger id="revaluationSurplusAccountId">
                <SelectValue placeholder="Select revaluation surplus account" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE_VALUE}>No account</SelectItem>
                {revaluationSurplusOptions.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.accountNumber} - {account.accountName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="aucAccountId">AUC / CIP Account (optional)</Label>
            <Select
              value={formData.aucAccountId || NONE_VALUE}
              onValueChange={(value) =>
                setFormData({
                  ...formData,
                  aucAccountId: value === NONE_VALUE ? undefined : value,
                })
              }
            >
              <SelectTrigger id="aucAccountId">
                <SelectValue placeholder="Select AUC account" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE_VALUE}>No account</SelectItem>
                {aucAccountOptions.map((account) => (
                  <SelectItem key={account.id} value={account.id}>
                    {account.accountNumber} - {account.accountName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
