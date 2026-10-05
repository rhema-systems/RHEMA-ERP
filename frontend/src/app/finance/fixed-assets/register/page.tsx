'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, Building2, Edit, Filter, Loader2, Plus, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { DeleteFixedAssetDraftButton } from '@/components/finance/fixed-assets/DeleteFixedAssetDraftButton';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { FixedAsset, FixedAssetStatus } from '@/types/fixed-assets';

export default function FixedAssetRegisterPage() {
  const [assets, setAssets] = useState<FixedAsset[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | FixedAssetStatus>('all');

  useEffect(() => {
    const loadAssets = async () => {
      try {
        setLoading(true);
        const data = await fixedAssetsDataService.getAssets();
        setAssets(data);
      } catch (error) {
        console.error('Failed to load fixed assets:', error);
      } finally {
        setLoading(false);
      }
    };

    loadAssets();
  }, []);

  const filteredAssets = useMemo(() => {
    return assets.filter((asset) => {
      const matchesSearch =
        searchTerm === '' ||
        asset.assetCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
        asset.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        (asset.location || '').toLowerCase().includes(searchTerm.toLowerCase());

      const matchesStatus = statusFilter === 'all' || asset.status === statusFilter;

      return matchesSearch && matchesStatus;
    });
  }, [assets, searchTerm, statusFilter]);

  const formatMoney = (amount: number) => {
    return new Intl.NumberFormat('en-GH', {
      style: 'currency',
      currency: 'GHS',
    }).format(amount || 0);
  };

  const getStatusBadge = (status: FixedAssetStatus) => {
    const variants: Record<FixedAssetStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      Draft: 'outline',
      Active: 'default',
      FullyDepreciated: 'secondary',
      Disposed: 'destructive',
      HeldForSale: 'secondary',
      WrittenOff: 'destructive',
      UnderConstruction: 'outline',
      OnHold: 'secondary',
      Acquired: 'outline',
      Capitalized: 'default',
      PendingApproval: 'secondary',
      Rejected: 'destructive',
    };
    return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
  };

  const getLifecycleGuidance = (status: FixedAssetStatus) => {
    const guidance: Record<FixedAssetStatus, { label: string; action: string }> = {
      Draft: { label: 'Submit capitalization proposal', action: 'Continue setup' },
      PendingApproval: { label: 'Await independent approval', action: 'View approval' },
      Rejected: { label: 'Revise and resubmit proposal', action: 'Resolve rejection' },
      Acquired: { label: 'Post approved capitalization', action: 'Post capitalization' },
      Capitalized: { label: 'Activate the asset', action: 'Activate asset' },
      Active: { label: 'Asset is in service', action: 'Manage asset' },
      OnHold: { label: 'Review and resume lifecycle', action: 'Review hold' },
      UnderConstruction: { label: 'Complete construction/capitalization', action: 'Manage AUC' },
      HeldForSale: { label: 'Complete disposal workflow', action: 'Manage disposal' },
      FullyDepreciated: { label: 'Review retention or disposal', action: 'Review asset' },
      Disposed: { label: 'Lifecycle complete', action: 'View asset' },
      WrittenOff: { label: 'Lifecycle complete', action: 'View asset' },
    };

    return guidance[status];
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <Building2 className="h-8 w-8" />
            Fixed Assets Register
          </h1>
          <p className="text-muted-foreground">Track and manage fixed assets in your register.</p>
        </div>
        <div className="flex gap-2">
          <Link href="/finance/fixed-assets/categories">
            <Button variant="outline">Categories</Button>
          </Link>
          <Link href="/finance/fixed-assets/depreciation">
            <Button variant="outline">Run Depreciation</Button>
          </Link>
          <Link href="/finance/fixed-assets/import">
            <Button variant="outline">
              <Upload className="mr-2 h-4 w-4" />
              Import
            </Button>
          </Link>
          <Link href="/finance/fixed-assets/register/new">
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              New Asset
            </Button>
          </Link>
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
            <BreadcrumbPage>Fixed Assets</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Search</CardTitle>
          </CardHeader>
          <CardContent>
            <Input
              placeholder="Search by asset code or name..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Filter className="h-4 w-4" />
              Filters
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Select value={statusFilter} onValueChange={(value) => setStatusFilter(value as FixedAssetStatus | 'all')}>
              <SelectTrigger>
                <SelectValue placeholder="All Statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Acquired">Acquired</SelectItem>
                <SelectItem value="Capitalized">Capitalized</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="FullyDepreciated">Fully Depreciated</SelectItem>
                <SelectItem value="Disposed">Disposed</SelectItem>
                <SelectItem value="HeldForSale">Held For Sale</SelectItem>
                <SelectItem value="WrittenOff">Written Off</SelectItem>
                <SelectItem value="UnderConstruction">Under Construction</SelectItem>
                <SelectItem value="OnHold">On Hold</SelectItem>
              </SelectContent>
            </Select>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Assets</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Location</TableHead>
                <TableHead>Purchase Date</TableHead>
                <TableHead className="text-right">Net Book Value</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Next Step</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredAssets.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={9} className="text-center py-8 text-muted-foreground">
                    No fixed assets found.
                  </TableCell>
                </TableRow>
              ) : (
                filteredAssets.map((asset) => {
                  const lifecycle = getLifecycleGuidance(asset.status);
                  return (
                  <TableRow key={asset.id}>
                    <TableCell className="font-mono">{asset.assetCode}</TableCell>
                    <TableCell className="font-medium">{asset.name}</TableCell>
                    <TableCell>{asset.fixedAssetCategoryName || 'Unassigned'}</TableCell>
                    <TableCell>{asset.location || '-'}</TableCell>
                    <TableCell>
                      {new Date(asset.purchaseDate).toLocaleDateString('en-US', {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric',
                      })}
                    </TableCell>
                    <TableCell className="text-right">{formatMoney(asset.netBookValue)}</TableCell>
                    <TableCell>{getStatusBadge(asset.status)}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{lifecycle.label}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Link href={`/finance/fixed-assets/register/${asset.id}/edit`}>
                          <Button variant="outline" size="sm" aria-label={`${lifecycle.action}: ${asset.name}`}>
                            {asset.status === 'Active' || asset.status === 'Disposed' || asset.status === 'WrittenOff' ? (
                              <Edit className="mr-2 h-4 w-4" />
                            ) : (
                              <ArrowRight className="mr-2 h-4 w-4" />
                            )}
                            {lifecycle.action}
                          </Button>
                        </Link>
                        <DeleteFixedAssetDraftButton
                          asset={asset}
                          compact
                          onDeleted={() => setAssets(current => current.filter(candidate => candidate.id !== asset.id))}
                        />
                      </div>
                    </TableCell>
                  </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
