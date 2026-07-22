'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Loader2, MapPin, Package, RefreshCw } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

interface LocationLookup {
  id: string;
  name: string;
  code?: string | null;
  isActive?: boolean;
}

interface AssetLookup {
  id: string;
  assetNumber: string;
  name: string;
  categoryName?: string | null;
  category?: string | null;
  status?: string | null;
  location?: string | null;
  currentProjectId?: string | null;
  currentSiteLocationId?: string | null;
  currentSiteLocationName?: string | null;
}

const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

const authHeaders = () => {
  const token = localStorage.getItem('authToken') || localStorage.getItem('token');
  return {
    Authorization: token ? `Bearer ${token}` : '',
    'Content-Type': 'application/json',
  };
};

const mapAsset = (asset: any): AssetLookup => ({
  id: asset.id || asset.Id,
  assetNumber: asset.assetNumber || asset.AssetNumber || 'N/A',
  name: asset.name || asset.Name || 'Unnamed asset',
  categoryName: asset.categoryName || asset.CategoryName || asset.assetCategory?.name || asset.category || null,
  category: asset.category || asset.Category || asset.categoryName || asset.CategoryName || null,
  status: asset.status || asset.Status || null,
  location: asset.location || asset.Location || null,
  currentProjectId: asset.currentProjectId || asset.CurrentProjectId || null,
  currentSiteLocationId: asset.currentSiteLocationId || asset.CurrentSiteLocationId || null,
  currentSiteLocationName: asset.currentSiteLocationName || asset.CurrentSiteLocationName || null,
});

export default function MaintenanceSitesPage() {
  const { toast } = useToast();
  const [locations, setLocations] = useState<LocationLookup[]>([]);
  const [selectedLocationId, setSelectedLocationId] = useState('');
  const [siteAssets, setSiteAssets] = useState<AssetLookup[]>([]);
  const [allAssets, setAllAssets] = useState<AssetLookup[]>([]);
  const [selectedAssetId, setSelectedAssetId] = useState('');
  const [reason, setReason] = useState('Site assignment');
  const [loading, setLoading] = useState(true);
  const [assetsLoading, setAssetsLoading] = useState(false);
  const [moving, setMoving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedLocation = useMemo(
    () => locations.find((location) => location.id === selectedLocationId) || null,
    [locations, selectedLocationId]
  );

  const selectedAsset = useMemo(
    () => allAssets.find((asset) => asset.id === selectedAssetId) || null,
    [allAssets, selectedAssetId]
  );

  const loadLocations = async () => {
    setError(null);
    const response = await fetch(`${API_URL}/Location/summary`, { headers: authHeaders() });
    if (!response.ok) {
      throw new Error(`Site lookup returned HTTP ${response.status}`);
    }

    const payload = await response.json();
    const items: LocationLookup[] = Array.isArray(payload)
      ? payload
      : Array.isArray(payload?.items)
        ? payload.items
        : Array.isArray(payload?.data)
          ? payload.data
          : [];

    const activeSites = items.filter((item) => item.isActive !== false);
    setLocations(activeSites);
    setSelectedLocationId((current) => current || activeSites[0]?.id || '');
  };

  const loadAllAssets = async () => {
    const response = await fetch(`${API_URL}/maintenance/assets?page=1&pageSize=100`, { headers: authHeaders() });
    if (!response.ok) {
      throw new Error(await response.text());
    }

    const payload = await response.json();
    const items = payload.items || payload.data || payload || [];
    setAllAssets(items.map(mapAsset));
  };

  const loadSiteAssets = async (locationId: string) => {
    if (!locationId) {
      setSiteAssets([]);
      return;
    }

    setAssetsLoading(true);
    try {
      const response = await fetch(`${API_URL}/maintenance/assets?page=1&pageSize=100&siteLocationId=${encodeURIComponent(locationId)}`, { headers: authHeaders() });
      if (!response.ok) {
        throw new Error(await response.text());
      }

      const payload = await response.json();
      const items = payload.items || payload.data || payload || [];
      setSiteAssets(items.map(mapAsset));
    } finally {
      setAssetsLoading(false);
    }
  };

  const refresh = async () => {
    setLoading(true);
    try {
      await Promise.all([loadLocations(), loadAllAssets()]);
    } catch (refreshError) {
      setError(refreshError instanceof Error ? refreshError.message : 'Sites could not be loaded.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
  }, []);

  useEffect(() => {
    void loadSiteAssets(selectedLocationId).catch((loadError) => {
      setError(loadError instanceof Error ? loadError.message : 'Assets could not be loaded for this site.');
    });
  }, [selectedLocationId]);

  const assignAssetToSite = async () => {
    if (!selectedLocation || !selectedAsset || !reason.trim()) {
      return;
    }

    setMoving(true);
    try {
      const response = await fetch(`${API_URL}/maintenance/assets/${selectedAsset.id}/move`, {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({
          projectId: selectedAsset.currentProjectId || null,
          siteLocationId: selectedLocation.id,
          location: selectedLocation.name,
          reason: reason.trim(),
          notes: `Assigned from Maintenance Sites page to ${selectedLocation.name}`,
          effectiveDate: new Date().toISOString(),
        }),
      });

      if (!response.ok) {
        throw new Error(await response.text());
      }

      toast({
        title: 'Asset assigned to site',
        description: `${selectedAsset.assetNumber} · ${selectedAsset.name} was moved to ${selectedLocation.name}.`,
      });

      setSelectedAssetId('');
      await Promise.all([loadAllAssets(), loadSiteAssets(selectedLocation.id)]);
    } catch (moveError) {
      toast({
        title: 'Asset assignment failed',
        description: moveError instanceof Error ? moveError.message : 'The asset could not be moved to the selected site.',
        variant: 'destructive',
      });
    } finally {
      setMoving(false);
    }
  };

  const assignableAssets = allAssets
    .filter((asset) => asset.id && asset.currentSiteLocationId !== selectedLocationId)
    .sort((a, b) => `${a.assetNumber} ${a.name}`.localeCompare(`${b.assetNumber} ${b.name}`));

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Maintenance Sites</h1>
          <p className="text-muted-foreground">
            View operational sites, assigned assets, and move assets between locations.
          </p>
        </div>
        <Button variant="outline" onClick={() => void refresh()} disabled={loading}>
          {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
          Refresh
        </Button>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Sites</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {error && (
        <Card className="border-destructive/40 bg-destructive/5">
          <CardContent className="pt-6 text-sm text-destructive">{error}</CardContent>
        </Card>
      )}

      <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><MapPin className="h-5 w-5" /> Sites</CardTitle>
            <CardDescription>Select a site to review its assigned assets.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Loading sites...
              </div>
            ) : locations.length === 0 ? (
              <p className="text-sm text-muted-foreground">No active HR locations were found.</p>
            ) : (
              locations.map((location) => (
                <button
                  key={location.id}
                  type="button"
                  onClick={() => setSelectedLocationId(location.id)}
                  className={`w-full rounded-lg border p-3 text-left transition ${
                    selectedLocationId === location.id ? 'border-primary bg-primary/5' : 'hover:bg-muted'
                  }`}
                >
                  <div className="font-medium">{location.name}</div>
                  {location.code && <div className="text-xs text-muted-foreground">{location.code}</div>}
                </button>
              ))
            )}
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center justify-between gap-3">
                <span>{selectedLocation?.name || 'Select a site'}</span>
                <Badge variant="outline">{siteAssets.length} assets</Badge>
              </CardTitle>
              <CardDescription>
                Assets shown here are currently assigned to the selected HR Location.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-4 md:grid-cols-[1fr_220px_auto]">
                <div className="space-y-2">
                  <Label>Asset to move into this site</Label>
                  <Select value={selectedAssetId} onValueChange={setSelectedAssetId} disabled={!selectedLocation}>
                    <SelectTrigger><SelectValue placeholder="Select asset" /></SelectTrigger>
                    <SelectContent>
                      {assignableAssets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.assetNumber} · {asset.name}
                        </SelectItem>
                      ))}
                      {assignableAssets.length === 0 && (
                        <SelectItem value="no-assets" disabled>No unassigned/other-site assets available</SelectItem>
                      )}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Reason</Label>
                  <Input value={reason} onChange={(event) => setReason(event.target.value)} />
                </div>
                <div className="flex items-end">
                  <Button onClick={() => void assignAssetToSite()} disabled={!selectedLocation || !selectedAsset || !reason.trim() || moving}>
                    {moving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Assign Asset
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2"><Package className="h-5 w-5" /> Assets at Site</CardTitle>
            </CardHeader>
            <CardContent>
              {assetsLoading ? (
                <div className="flex items-center gap-2 py-8 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" /> Loading assets...
                </div>
              ) : siteAssets.length === 0 ? (
                <p className="py-8 text-center text-sm text-muted-foreground">No assets are currently assigned to this site.</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Asset</TableHead>
                      <TableHead>Category</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Location Detail</TableHead>
                      <TableHead className="text-right">Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {siteAssets.map((asset) => (
                      <TableRow key={asset.id}>
                        <TableCell>
                          <div className="font-medium">{asset.name}</div>
                          <div className="text-xs text-muted-foreground">{asset.assetNumber}</div>
                        </TableCell>
                        <TableCell>{asset.categoryName || asset.category || 'Uncategorized'}</TableCell>
                        <TableCell><Badge variant="outline">{asset.status || 'Unknown'}</Badge></TableCell>
                        <TableCell>{asset.location || asset.currentSiteLocationName || selectedLocation?.name}</TableCell>
                        <TableCell className="text-right">
                          <Button asChild size="sm" variant="outline">
                            <Link href={`/maintenance/assets?id=${asset.id}`}>Open Asset</Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
