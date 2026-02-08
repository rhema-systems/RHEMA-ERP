'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Switch } from '@/components/ui/switch';
import { 
  DollarSign, TrendingUp, TrendingDown, Package, BarChart3, 
  PieChart, Calculator, RefreshCw, Download
} from 'lucide-react';
import { 
  inventoryManagementService, 
  InventoryItemDto, WarehouseDto, WarehouseItemDto
} from '@/services/inventoryManagementService';

export default function InventoryValuationPage() {
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [inTransitByItemId, setInTransitByItemId] = useState<Record<string, number>>({});
  const [inTransitByWarehouseItem, setInTransitByWarehouseItem] = useState<Record<string, Record<string, number>>>({});
  const [selectedWarehouseItemsByItemId, setSelectedWarehouseItemsByItemId] = useState<Record<string, WarehouseItemDto>>({});
  const [loading, setLoading] = useState(true);
  const [selectedWarehouse, setSelectedWarehouse] = useState('all');
  const [valuationMethod, setValuationMethod] = useState('average');
  const [includeInTransit, setIncludeInTransit] = useState(true);

  const valuationMethodOptions = [
    { value: 'average', label: 'Weighted Average (AVCO)' },
    { value: 'fifo', label: 'FIFO (First In, First Out)' },
    { value: 'lifo', label: 'LIFO (Last In, First Out)' },
    { value: 'standard', label: 'Standard Cost' },
    { value: 'specific', label: 'Specific Identification' },
    { value: 'last', label: 'Last Purchase Cost' },
  ] as const;

  const normalizeApiData = <T,>(payload: any): T => {
    if (payload && typeof payload === 'object' && 'data' in payload) {
      return payload.data as T;
    }
    return payload as T;
  };

  const fetchData = async () => {
    try {
      setLoading(true);
      const [itemDataRaw, warehouseDataRaw, inTransitRaw] = await Promise.all([
        inventoryManagementService.getInventoryItems(),
        inventoryManagementService.getWarehouses(),
        inventoryManagementService.getInTransitTransfers()
      ]);

      const itemData = normalizeApiData<InventoryItemDto[]>(itemDataRaw) || [];
      const warehouseData = normalizeApiData<WarehouseDto[]>(warehouseDataRaw) || [];
      const inTransitTransfers = normalizeApiData<any[]>(inTransitRaw) || [];

      const inTransitQuantities: Record<string, number> = {};
      const inTransitQuantitiesByWarehouse: Record<string, Record<string, number>> = {};
      for (const transfer of inTransitTransfers) {
        const transferId = transfer?.id || transfer?.transferId;
        if (!transferId) continue;

        try {
          const detailRaw = await inventoryManagementService.getInventoryTransferById(transferId);
          const detail = normalizeApiData<any>(detailRaw);
          const transferItems = detail?.items || [];
          const sourceWarehouseId = transfer?.sourceWarehouseId || detail?.sourceWarehouseId;
          const destinationWarehouseId = transfer?.destinationWarehouseId || detail?.destinationWarehouseId;
          const relatedWarehouseIds = Array.from(
            new Set([sourceWarehouseId, destinationWarehouseId].filter((id): id is string => !!id))
          );

          for (const transferItem of transferItems) {
            const itemId = transferItem?.inventoryItemId;
            if (!itemId) continue;

            const shipped = Number(transferItem?.shippedQuantity || 0);
            const received = Number(transferItem?.receivedQuantity || 0);
            const inTransitQty = Math.max(0, shipped - received);

            if (inTransitQty > 0) {
              inTransitQuantities[itemId] = (inTransitQuantities[itemId] || 0) + inTransitQty;

              for (const warehouseId of relatedWarehouseIds) {
                if (!inTransitQuantitiesByWarehouse[warehouseId]) {
                  inTransitQuantitiesByWarehouse[warehouseId] = {};
                }
                inTransitQuantitiesByWarehouse[warehouseId][itemId] =
                  (inTransitQuantitiesByWarehouse[warehouseId][itemId] || 0) + inTransitQty;
              }
            }
          }
        } catch (transferDetailError) {
          console.warn('Unable to load transfer detail for valuation in-transit calculation:', transferDetailError);
        }
      }

      setItems(itemData);
      setWarehouses(warehouseData);
      setInTransitByItemId(inTransitQuantities);
      setInTransitByWarehouseItem(inTransitQuantitiesByWarehouse);
    } catch (err) {
      console.error('Error fetching data:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchData(); }, []);

  useEffect(() => {
    const fetchWarehouseScopedItems = async () => {
      if (selectedWarehouse === 'all') {
        setSelectedWarehouseItemsByItemId({});
        return;
      }

      try {
        const warehouseItemsRaw = await inventoryManagementService.getWarehouseItems(selectedWarehouse);
        const warehouseItems = normalizeApiData<WarehouseItemDto[]>(warehouseItemsRaw) || [];
        const byItemId: Record<string, WarehouseItemDto> = {};
        for (const warehouseItem of warehouseItems) {
          if (warehouseItem.inventoryItemId) {
            byItemId[warehouseItem.inventoryItemId] = warehouseItem;
          }
        }
        setSelectedWarehouseItemsByItemId(byItemId);
      } catch (warehouseItemsError) {
        console.warn('Unable to load warehouse-scoped inventory items for valuation:', warehouseItemsError);
        setSelectedWarehouseItemsByItemId({});
      }
    };

    fetchWarehouseScopedItems();
  }, [selectedWarehouse]);

  const valuationItems = useMemo(() => {
    if (selectedWarehouse === 'all') {
      return items;
    }

    return items
      .filter((item) => !!selectedWarehouseItemsByItemId[item.id])
      .map((item) => {
        const warehouseItem = selectedWarehouseItemsByItemId[item.id];
        if (!warehouseItem) return item;

        return {
          ...item,
          currentStock: Number(warehouseItem.currentStock || 0),
          availableStock: Number(warehouseItem.availableStock || 0),
          allocatedStock: Number(warehouseItem.allocatedStock || 0),
          reorderLevel: Number(warehouseItem.reorderLevel || item.reorderLevel || 0),
          averageCost: Number(warehouseItem.averageCost || item.averageCost || 0),
        };
      });
  }, [items, selectedWarehouse, selectedWarehouseItemsByItemId]);

  const activeInTransitByItemId = useMemo(() => {
    if (selectedWarehouse === 'all') {
      return inTransitByItemId;
    }
    return inTransitByWarehouseItem[selectedWarehouse] || {};
  }, [selectedWarehouse, inTransitByItemId, inTransitByWarehouseItem]);

  // Calculate valuation metrics
  const getEffectiveQuantity = (item: InventoryItemDto) => {
    const onHandQty = Number(item.currentStock || 0);
    const inTransitQty = Number(activeInTransitByItemId[item.id] || 0);
    return includeInTransit ? onHandQty + inTransitQty : onHandQty;
  };

  const getUnitValueByMethod = (item: InventoryItemDto) => {
    switch (valuationMethod) {
      case 'standard': return Number(item.standardCost || 0);
      case 'fifo': return Number(item.lastPurchaseCost || item.averageCost || 0);
      case 'lifo': return Number(item.currentCost || item.averageCost || item.lastPurchaseCost || 0);
      case 'specific': return Number(item.currentCost || item.lastPurchaseCost || item.averageCost || 0);
      case 'last': return Number(item.lastPurchaseCost || 0);
      case 'average':
      default:
        return Number(item.averageCost || 0);
    }
  };

  const calculateValuation = (item: InventoryItemDto) => {
    const qty = getEffectiveQuantity(item);
    return qty * getUnitValueByMethod(item);
  };

  const totalValue = valuationItems.reduce((sum, item) => sum + calculateValuation(item), 0);
  const totalItems = valuationItems.reduce((sum, item) => sum + getEffectiveQuantity(item), 0);
  const totalInTransitUnits = valuationItems.reduce((sum, item) => sum + Number(activeInTransitByItemId[item.id] || 0), 0);
  const totalInTransitValue = valuationItems.reduce(
    (sum, item) => sum + (Number(activeInTransitByItemId[item.id] || 0) * getUnitValueByMethod(item)),
    0
  );
  const avgCostPerUnit = totalItems > 0 ? totalValue / totalItems : 0;
  const lowStockValue = valuationItems
    .filter(i => i.availableStock <= i.reorderLevel)
    .reduce((sum, i) => sum + calculateValuation(i), 0);

  // Group by category for pie chart data
  const categoryValues = valuationItems.reduce((acc, item) => {
    const cat = item.categoryName || 'Uncategorized';
    acc[cat] = (acc[cat] || 0) + calculateValuation(item);
    return acc;
  }, {} as Record<string, number>);

  const topCategories = Object.entries(categoryValues)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 5);

  // Top valued items
  const topValuedItems = [...valuationItems]
    .sort((a, b) => calculateValuation(b) - calculateValuation(a))
    .slice(0, 10);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Inventory Valuation</h1>
          <p className="text-muted-foreground">Analyze inventory value, costs, and trends</p>
          <p className="text-xs text-muted-foreground mt-1">
            Scope:{' '}
            {selectedWarehouse === 'all'
              ? 'All Warehouses'
              : warehouses.find((warehouse) => warehouse.id === selectedWarehouse)?.name || 'Selected Warehouse'}
            {' '}+ In-Transit {includeInTransit ? 'Included' : 'Excluded'}
          </p>
        </div>
        <div className="flex items-center space-x-2">
          <Button variant="outline" onClick={fetchData}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          <Button variant="outline"><Download className="h-4 w-4 mr-2" />Export</Button>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Valuation</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Valuation Method Selector */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center"><Calculator className="h-4 w-4 mr-2" />Valuation Settings</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">Valuation Method</label>
                <Select value={valuationMethod} onValueChange={setValuationMethod}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {valuationMethodOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Warehouse</label>
              <Select value={selectedWarehouse} onValueChange={setSelectedWarehouse}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Warehouses</SelectItem>
                  {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Include In-Transit Quantity</label>
              <div className="h-10 px-3 border rounded-md flex items-center justify-between">
                <span className="text-sm text-muted-foreground">
                  {includeInTransit ? 'Enabled' : 'Disabled'}
                </span>
                <Switch checked={includeInTransit} onCheckedChange={setIncludeInTransit} />
              </div>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">In-Transit Units</label>
              <div className="h-10 px-3 border rounded-md flex items-center justify-between bg-muted/30">
                <span className="text-sm text-muted-foreground">Current in-transit quantity</span>
                <span className="text-sm font-semibold">{totalInTransitUnits.toLocaleString()}</span>
              </div>
              <p className="text-xs text-muted-foreground">
                {selectedWarehouse === 'all'
                  ? 'Across all warehouses'
                  : 'For transfers involving the selected warehouse'}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Summary Stats */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">${totalValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                <p className="text-sm text-muted-foreground">Total Inventory Value</p>
              </div>
              <DollarSign className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{totalItems.toLocaleString()}</p>
                <p className="text-sm text-muted-foreground">Total Units</p>
              </div>
              <Package className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">${avgCostPerUnit.toFixed(2)}</p>
                <p className="text-sm text-muted-foreground">Avg Cost/Unit</p>
              </div>
              <BarChart3 className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-orange-600">${lowStockValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                <p className="text-sm text-muted-foreground">Low Stock Value</p>
              </div>
              <TrendingDown className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-indigo-600">${totalInTransitValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                <p className="text-sm text-muted-foreground">In-Transit Value</p>
              </div>
              <TrendingUp className="h-8 w-8 text-indigo-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="items" className="w-full">
        <TabsList>
          <TabsTrigger value="items">Top Valued Items</TabsTrigger>
          <TabsTrigger value="categories">By Category</TabsTrigger>
        </TabsList>

        <TabsContent value="items">
          <Card>
            <CardHeader>
              <CardTitle>Top 10 Valued Items</CardTitle>
              <CardDescription>Items with highest inventory value</CardDescription>
            </CardHeader>
            <CardContent>
              {loading ? (
                <div className="text-center py-8 text-muted-foreground">Loading...</div>
              ) : (
                <div className="space-y-3">
                  {topValuedItems.map((item, idx) => {
                    const value = calculateValuation(item);
                    const percentage = totalValue > 0 ? (value / totalValue) * 100 : 0;
                    return (
                      <div key={item.id} className="border rounded-lg p-4">
                        <div className="flex items-center justify-between">
                          <div className="flex items-center space-x-4">
                            <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center text-blue-600 font-bold">
                              {idx + 1}
                            </div>
                            <div>
                              <div className="flex items-center space-x-2">
                                <h3 className="font-semibold">{item.name}</h3>
                                <Badge variant="outline">{item.itemCode}</Badge>
                              </div>
                              <p className="text-sm text-muted-foreground">
                                {item.categoryName || 'Uncategorized'} • Stock: {item.currentStock} units
                                {Number(activeInTransitByItemId[item.id] || 0) > 0 && (
                                  <> • In-Transit: {Number(activeInTransitByItemId[item.id] || 0)} units</>
                                )}
                              </p>
                            </div>
                          </div>
                          <div className="text-right">
                            <div className="text-lg font-bold">${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
                            <div className="text-sm text-muted-foreground">{percentage.toFixed(1)}% of total</div>
                          </div>
                        </div>
                        <div className="mt-2 h-2 bg-gray-100 rounded-full overflow-hidden">
                          <div className="h-full bg-blue-500 rounded-full" style={{ width: `${percentage}%` }} />
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="categories">
          <Card>
            <CardHeader>
              <CardTitle>Value by Category</CardTitle>
              <CardDescription>Inventory value distribution across categories</CardDescription>
            </CardHeader>
            <CardContent>
              {loading ? (
                <div className="text-center py-8 text-muted-foreground">Loading...</div>
              ) : (
                <div className="space-y-4">
                  {topCategories.map(([category, value], idx) => {
                    const percentage = totalValue > 0 ? (value / totalValue) * 100 : 0;
                    const colors = ['bg-blue-500', 'bg-green-500', 'bg-purple-500', 'bg-orange-500', 'bg-pink-500'];
                    return (
                      <div key={category} className="space-y-2">
                        <div className="flex items-center justify-between">
                          <div className="flex items-center space-x-2">
                            <div className={`w-3 h-3 rounded-full ${colors[idx % colors.length]}`} />
                            <span className="font-medium">{category}</span>
                          </div>
                          <div className="text-right">
                            <span className="font-bold">${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                            <span className="text-muted-foreground ml-2">({percentage.toFixed(1)}%)</span>
                          </div>
                        </div>
                        <div className="h-3 bg-gray-100 rounded-full overflow-hidden">
                          <div className={`h-full ${colors[idx % colors.length]} rounded-full`} style={{ width: `${percentage}%` }} />
                        </div>
                      </div>
                    );
                  })}
                  {Object.keys(categoryValues).length > 5 && (
                    <div className="text-center text-sm text-muted-foreground pt-2">
                      + {Object.keys(categoryValues).length - 5} more categories
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Cost Comparison */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center"><TrendingUp className="h-4 w-4 mr-2" />Cost Comparison</CardTitle>
          <CardDescription>Compare different costing methods</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="border rounded-lg p-4 text-center">
              <p className="text-sm text-muted-foreground mb-2">Standard Cost Valuation</p>
              <p className="text-2xl font-bold">
                ${valuationItems.reduce((sum, i) => sum + i.currentStock * i.standardCost, 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </p>
            </div>
            <div className="border rounded-lg p-4 text-center">
              <p className="text-sm text-muted-foreground mb-2">Average Cost Valuation</p>
              <p className="text-2xl font-bold">
                ${valuationItems.reduce((sum, i) => sum + i.currentStock * i.averageCost, 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </p>
            </div>
            <div className="border rounded-lg p-4 text-center">
              <p className="text-sm text-muted-foreground mb-2">Last Purchase Cost Valuation</p>
              <p className="text-2xl font-bold">
                ${valuationItems.reduce((sum, i) => sum + i.currentStock * i.lastPurchaseCost, 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
