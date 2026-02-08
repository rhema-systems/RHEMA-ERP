'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { RefreshCw, Search } from 'lucide-react';
import { inventoryManagementService, BinStockDto, PagedResult, WarehouseDto, WarehouseLocationDto } from '@/services/inventoryManagementService';
import { format } from 'date-fns';

export default function BinStockPage() {
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [locations, setLocations] = useState<WarehouseLocationDto[]>([]);
  const [result, setResult] = useState<PagedResult<BinStockDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [warehouseId, setWarehouseId] = useState<string>('all');
  const [locationId, setLocationId] = useState<string>('all');
  const [search, setSearch] = useState('');
  const [includeZero, setIncludeZero] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const selectedWarehouseId = useMemo(() => (warehouseId !== 'all' ? warehouseId : undefined), [warehouseId]);
  const selectedLocationId = useMemo(() => (locationId !== 'all' ? locationId : undefined), [locationId]);

  const loadWarehouses = async () => {
    const data = await inventoryManagementService.getWarehouses(true);
    setWarehouses(data);
  };

  const loadLocations = async (warehouse?: string) => {
    const data = await inventoryManagementService.getWarehouseLocations(warehouse);
    setLocations(data);
  };

  const loadStock = async () => {
    try {
      setLoading(true);
      setError(null);

      const data = await inventoryManagementService.getBinStock({
        warehouseId: selectedWarehouseId,
        locationId: selectedLocationId,
        search: search || undefined,
        includeZero,
        page,
        pageSize
      });

      setResult(data);
    } catch (err: any) {
      console.error('Error loading bin stock:', err);
      setError('Failed to load bin stock');
      setResult(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadWarehouses().catch(console.error);
    loadLocations(undefined).catch(console.error);
  }, []);

  useEffect(() => {
    // Reset dependent filter + paging when warehouse changes
    setLocationId('all');
    setPage(1);
    loadLocations(selectedWarehouseId).catch(console.error);
  }, [selectedWarehouseId]);

  useEffect(() => {
    loadStock();
  }, [selectedWarehouseId, selectedLocationId, search, includeZero, page]);

  const rows = result?.items ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Bin Stock</h1>
          <p className="text-muted-foreground">Live bin balances by warehouse location</p>
        </div>
        <Button variant="outline" onClick={loadStock} disabled={loading}>
          <RefreshCw className="h-4 w-4 mr-2" />
          Refresh
        </Button>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Bin Stock</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Filter by warehouse, bin, or item</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-12 md:items-end">
            <div className="md:col-span-3">
              <label className="text-sm font-medium">Warehouse</label>
              <Select value={warehouseId} onValueChange={setWarehouseId}>
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="All warehouses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All warehouses</SelectItem>
                  {warehouses.map(w => (
                    <SelectItem key={w.id} value={w.id}>{w.code ? `${w.code} - ${w.name}` : w.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="md:col-span-3">
              <label className="text-sm font-medium">Bin</label>
              <Select value={locationId} onValueChange={setLocationId}>
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="All bins" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All bins</SelectItem>
                  {locations.map(l => (
                    <SelectItem key={l.id} value={l.id}>{l.locationCode}{l.name ? ` - ${l.name}` : ''}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="md:col-span-4">
              <label className="text-sm font-medium">Search</label>
              <div className="relative mt-1">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={e => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder="Item code/name, warehouse, or bin code"
                />
              </div>
            </div>

            <div className="md:col-span-2">
              <label className="text-sm font-medium">Options</label>
              <div className="mt-2 flex items-center gap-2">
                <Checkbox
                  id="include-zero"
                  checked={includeZero}
                  onCheckedChange={(v) => {
                    setIncludeZero(Boolean(v));
                    setPage(1);
                  }}
                />
                <label htmlFor="include-zero" className="text-sm text-muted-foreground">
                  Include zero
                </label>
              </div>
            </div>
          </div>

          {error && (
            <div className="mt-4 text-sm text-red-600">{error}</div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Bin Balances</CardTitle>
          <CardDescription>
            {loading ? 'Loading…' : `${result?.totalCount ?? 0} record(s)`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Warehouse</TableHead>
                  <TableHead>Bin</TableHead>
                  <TableHead>Item</TableHead>
                  <TableHead className="text-right">Qty</TableHead>
                  <TableHead className="text-right">Allocated</TableHead>
                  <TableHead className="text-right">Available</TableHead>
                  <TableHead className="text-right">Avg Cost</TableHead>
                  <TableHead>Last Move</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {!loading && rows.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={8} className="text-center text-muted-foreground py-8">
                      No records found
                    </TableCell>
                  </TableRow>
                )}
                {rows.map(r => (
                  <TableRow key={`${r.locationId}:${r.inventoryItemId}`}>
                    <TableCell className="whitespace-nowrap">{r.warehouseCode ? `${r.warehouseCode} - ${r.warehouseName}` : r.warehouseName}</TableCell>
                    <TableCell className="whitespace-nowrap">{r.locationCode}</TableCell>
                    <TableCell>
                      <div className="font-medium">{r.itemCode}</div>
                      <div className="text-xs text-muted-foreground">{r.itemName}</div>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{r.quantity.toLocaleString()}</TableCell>
                    <TableCell className="text-right tabular-nums">{r.allocatedQuantity.toLocaleString()}</TableCell>
                    <TableCell className="text-right tabular-nums">{r.availableQuantity.toLocaleString()}</TableCell>
                    <TableCell className="text-right tabular-nums">{(r.averageCost ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {r.lastMovementDate ? format(new Date(r.lastMovementDate), 'yyyy-MM-dd HH:mm') : '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="mt-4 flex items-center justify-between">
            <div className="text-sm text-muted-foreground">
              Page {result?.page ?? page} of {result?.totalPages ?? 1}
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={loading || !(result?.hasPrevious ?? page > 1)}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(p => p + 1)}
                disabled={loading || !(result?.hasNext ?? false)}
              >
                Next
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

