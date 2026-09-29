'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { Edit, Plus, RefreshCw, Settings } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { useToast } from '@/components/ui/use-toast';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { CreateFixedAssetCategoryDto, DepreciationMethod, FixedAssetCategory, FixedAssetGlAccountOption } from '@/types/fixed-assets';

export default function FixedAssetCategoriesPage() {
  const NONE_VALUE = '__none__';
  const { toast } = useToast();
  const [categories, setCategories] = useState<FixedAssetCategory[]>([]);
  const [assetAccountOptions, setAssetAccountOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [accumulatedDepOptions, setAccumulatedDepOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [depreciationExpenseOptions, setDepreciationExpenseOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [gainOnDisposalOptions, setGainOnDisposalOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [lossOnDisposalOptions, setLossOnDisposalOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [revaluationSurplusOptions, setRevaluationSurplusOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [aucAccountOptions, setAucAccountOptions] = useState<FixedAssetGlAccountOption[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [formData, setFormData] = useState<CreateFixedAssetCategoryDto>({
    name: '',
    code: '',
    description: '',
    requiresMaintenance: false,
    defaultMethod: 'StraightLine',
    defaultUsefulLifeMonths: 36,
    defaultResidualValuePercent: 0,
    defaultDiminishingBalanceRatePercent: 0,
    defaultLifetimeProductionCapacity: 0,
    assetAccountId: '',
    accumulatedDepreciationAccountId: '',
    depreciationExpenseAccountId: '',
    gainOnDisposalAccountId: '',
    lossOnDisposalAccountId: '',
    revaluationSurplusAccountId: '',
    aucAccountId: '',
  });

  const loadCategories = async () => {
    try {
      setIsLoading(true);
      const [categoryData, glAccounts] = await Promise.all([
        fixedAssetsDataService.getCategories(),
        fixedAssetsDataService.getGlAccounts(),
      ]);
      setCategories(categoryData);
      setAssetAccountOptions(glAccounts.assetAccounts);
      setAccumulatedDepOptions(glAccounts.accumulatedDepreciationAccounts);
      setDepreciationExpenseOptions(glAccounts.depreciationExpenseAccounts);
      setGainOnDisposalOptions(glAccounts.gainOnDisposalAccounts);
      setLossOnDisposalOptions(glAccounts.lossOnDisposalAccounts);
      setRevaluationSurplusOptions(glAccounts.revaluationSurplusAccounts || []);
      setAucAccountOptions(glAccounts.aucAccounts || []);
    } catch (error) {
      console.error('Failed to load categories:', error);
      toast({
        title: 'Error',
        description: 'Failed to load fixed asset categories.',
        variant: 'destructive',
      });
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadCategories();
  }, []);

  const handleCreate = async () => {
    if (!formData.name || !formData.code) {
      toast({
        title: 'Validation error',
        description: 'Name and code are required.',
        variant: 'destructive',
      });
      return;
    }

    try {
      await fixedAssetsDataService.createCategory({
        ...formData,
        gainOnDisposalAccountId: formData.gainOnDisposalAccountId || undefined,
        lossOnDisposalAccountId: formData.lossOnDisposalAccountId || undefined,
        revaluationSurplusAccountId: formData.revaluationSurplusAccountId || undefined,
        aucAccountId: formData.aucAccountId || undefined,
      });
      toast({
        title: 'Category created',
        description: 'Fixed asset category saved.',
      });
      setIsCreateOpen(false);
      setFormData({
        name: '',
        code: '',
        description: '',
        requiresMaintenance: false,
        defaultMethod: 'StraightLine',
        defaultUsefulLifeMonths: 36,
        defaultResidualValuePercent: 0,
        defaultDiminishingBalanceRatePercent: 0,
        defaultLifetimeProductionCapacity: 0,
        assetAccountId: '',
        accumulatedDepreciationAccountId: '',
        depreciationExpenseAccountId: '',
        gainOnDisposalAccountId: '',
        lossOnDisposalAccountId: '',
        revaluationSurplusAccountId: '',
        aucAccountId: '',
      });
      loadCategories();
    } catch (error) {
      console.error('Failed to create category:', error);
      toast({
        title: 'Error',
        description: 'Failed to create fixed asset category.',
        variant: 'destructive',
      });
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <Settings className="h-8 w-8" />
            Fixed Asset Categories
          </h1>
          <p className="text-muted-foreground">Configure asset categories and GL mappings.</p>
        </div>
        <div className="flex gap-2">
          <Link href="/finance/fixed-assets/register">
            <Button variant="outline">Back to Register</Button>
          </Link>
          <Button variant="outline" size="icon" onClick={loadCategories} disabled={isLoading}>
            <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
          </Button>
          <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                New Category
              </Button>
            </DialogTrigger>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>Create Fixed Asset Category</DialogTitle>
                <DialogDescription>
                  Define default depreciation settings and GL accounts for this category.
                </DialogDescription>
              </DialogHeader>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
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
                      <SelectValue placeholder="Select depreciation method" />
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
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setIsCreateOpen(false)}>
                  Cancel
                </Button>
                <Button onClick={handleCreate}>Create Category</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Fixed Asset Categories</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Categories</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Method</TableHead>
                <TableHead>Useful Life</TableHead>
                <TableHead>Residual %</TableHead>
                <TableHead>Maintenance</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {categories.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                    No categories found.
                  </TableCell>
                </TableRow>
              ) : (
                categories.map((category) => (
                  <TableRow key={category.id}>
                    <TableCell className="font-mono">{category.code}</TableCell>
                    <TableCell className="font-medium">{category.name}</TableCell>
                    <TableCell>{category.defaultMethod}</TableCell>
                    <TableCell>{category.defaultUsefulLifeMonths} months</TableCell>
                    <TableCell>{category.defaultResidualValuePercent}%</TableCell>
                    <TableCell>{category.requiresMaintenance ? 'Required' : 'Not required'}</TableCell>
                    <TableCell className="text-right">
                      <Link href={`/finance/fixed-assets/categories/${category.id}/edit`}>
                        <Button variant="ghost" size="icon" aria-label="Edit category">
                          <Edit className="h-4 w-4" />
                        </Button>
                      </Link>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
