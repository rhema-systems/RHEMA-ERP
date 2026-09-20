'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  ArrowUpDown, ArrowUp, ArrowDown, Package, RefreshCw,
  Download, Calendar, Filter, TrendingUp, TrendingDown, Search
} from 'lucide-react';
import {
  inventoryManagementService,
  StockMovementDto, WarehouseDto, InventoryItemDto
} from '@/services/inventoryManagementService';
import { MovementDetailDialog } from '@/components/inventory/MovementDetailDialog';
import { format } from 'date-fns';
import { INVENTORY_MOVEMENT_REPORT_PATH } from '@/lib/inventory-report-navigation';
import { formatInventoryMoney } from '@/lib/inventory-currency';

// Movement type definitions with isInbound to determine direction
const MovementTypes: { value: string; label: string; color: string; icon: typeof ArrowDown; isInbound: boolean | null }[] = [
  // Inbound movements (stock increases / green)
  { value: 'Receipt', label: 'Receipt', color: 'bg-green-100 text-green-800', icon: ArrowDown, isInbound: true },
  { value: 'Return', label: 'Return', color: 'bg-purple-100 text-purple-800', icon: ArrowDown, isInbound: true },
  { value: 'Adjustment+', label: 'Adjustment (+)', color: 'bg-green-100 text-green-800', icon: ArrowDown, isInbound: true },
  { value: 'Transfer-In', label: 'Transfer In', color: 'bg-blue-100 text-blue-800', icon: ArrowDown, isInbound: true },
  { value: 'Production', label: 'Production', color: 'bg-green-100 text-green-800', icon: ArrowDown, isInbound: true },
  // Outbound movements (stock decreases / red)
  { value: 'Issue', label: 'Issue', color: 'bg-red-100 text-red-800', icon: ArrowUp, isInbound: false },
  { value: 'Sale', label: 'Sale', color: 'bg-red-100 text-red-800', icon: ArrowUp, isInbound: false },
  { value: 'Adjustment-', label: 'Adjustment (-)', color: 'bg-red-100 text-red-800', icon: ArrowUp, isInbound: false },
  { value: 'Transfer-Out', label: 'Transfer Out', color: 'bg-blue-100 text-blue-800', icon: ArrowUp, isInbound: false },
  { value: 'Consumption', label: 'Consumption', color: 'bg-red-100 text-red-800', icon: ArrowUp, isInbound: false },
  { value: 'Waste', label: 'Waste', color: 'bg-gray-100 text-gray-800', icon: ArrowUp, isInbound: false },
  { value: 'Scrap', label: 'Scrap', color: 'bg-gray-100 text-gray-800', icon: ArrowUp, isInbound: false },
  // Neutral/Transfer movements
  { value: 'Transfer', label: 'Transfer', color: 'bg-blue-100 text-blue-800', icon: ArrowUpDown, isInbound: null },
  { value: 'Adjustment', label: 'Adjustment', color: 'bg-yellow-100 text-yellow-800', icon: ArrowUpDown, isInbound: null },
  { value: 'Allocation', label: 'Allocation', color: 'bg-orange-100 text-orange-800', icon: ArrowUpDown, isInbound: null },
  { value: 'Allocation-Release', label: 'Allocation Release', color: 'bg-orange-100 text-orange-800', icon: ArrowUpDown, isInbound: null },
];

// Inbound movement types for filtering/stats
const INBOUND_TYPES = ['Receipt', 'Return', 'Adjustment+', 'Transfer-In', 'Production'];
const OUTBOUND_TYPES = ['Issue', 'Sale', 'Adjustment-', 'Transfer-Out', 'Consumption', 'Waste', 'Scrap'];

export default function StockMovementsPage() {
  const [movements, setMovements] = useState<StockMovementDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [availableTypes, setAvailableTypes] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [warehouseFilter, setWarehouseFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [transferNumberSearch, setTransferNumberSearch] = useState('');

  // Dialog state for viewing movement details
  const [selectedMovement, setSelectedMovement] = useState<StockMovementDto | null>(null);
  const [detailDialogOpen, setDetailDialogOpen] = useState(false);

  // Load reference data once on mount
  useEffect(() => {
    const loadReferenceData = async () => {
      try {
        const [warehouseData, itemData, typesData] = await Promise.all([
          inventoryManagementService.getWarehouses(),
          inventoryManagementService.getInventoryItems(),
          inventoryManagementService.getStockMovementTypes()
        ]);
        setWarehouses(warehouseData);
        setItems(itemData);
        setAvailableTypes(typesData);
      } catch (err) {
        console.error('Error loading reference data:', err);
      }
    };
    loadReferenceData();
  }, []);

  const fetchMovements = async () => {
    try {
      setLoading(true);
      setError(null);
      const params = {
        warehouseId: warehouseFilter !== 'all' ? warehouseFilter : undefined,
        movementType: typeFilter !== 'all' ? typeFilter : undefined,
        startDate: startDate || undefined,
        endDate: endDate || undefined,
        referenceNumber: transferNumberSearch || undefined,
        limit: 100
      };
      console.log('Fetching movements with params:', params);
      const movementData = await inventoryManagementService.getStockMovements(params);
      console.log('Received movements:', movementData?.length ?? 0);
      setMovements(movementData);
    } catch (err: any) {
      console.error('Error fetching movements:', err);
      setError('Failed to load stock movements');
      setMovements([]);
    } finally {
      setLoading(false);
    }
  };

  const handleSearchByTransferNumber = () => {
    fetchMovements();
  };

  const handleMovementClick = (movement: StockMovementDto) => {
    setSelectedMovement(movement);
    setDetailDialogOpen(true);
  };

  useEffect(() => { fetchMovements(); }, [warehouseFilter, typeFilter, startDate, endDate]);

  const getMovementType = (type: string) => {
    const found = MovementTypes.find(t => t.value === type);
    if (found) return found;

    // Fallback: try to determine direction from type name
    const isLikelyInbound = type.toLowerCase().includes('in') ||
                            type.toLowerCase().includes('receipt') ||
                            type.toLowerCase().includes('return') ||
                            type.includes('+');
    const isLikelyOutbound = type.toLowerCase().includes('out') ||
                             type.toLowerCase().includes('issue') ||
                             type.toLowerCase().includes('sale') ||
                             type.toLowerCase().includes('consumption') ||
                             type.includes('-');

    if (isLikelyInbound) {
      return { value: type, label: type, color: 'bg-green-100 text-green-800', icon: ArrowDown, isInbound: true };
    } else if (isLikelyOutbound) {
      return { value: type, label: type, color: 'bg-red-100 text-red-800', icon: ArrowUp, isInbound: false };
    }
    // Default fallback for unknown types
    return { value: type, label: type, color: 'bg-gray-100 text-gray-800', icon: ArrowUpDown, isInbound: null };
  };

  // Calculate summary stats - inbound vs outbound based on movement type
  // Use Math.abs() since quantities may be stored as negative for outbound
  const totalInbound = movements
    .filter(m => INBOUND_TYPES.includes(m.movementType) || getMovementType(m.movementType).isInbound === true)
    .reduce((sum, m) => sum + Math.abs(m.quantity), 0);
  const totalOutbound = movements
    .filter(m => OUTBOUND_TYPES.includes(m.movementType) || getMovementType(m.movementType).isInbound === false)
    .reduce((sum, m) => sum + Math.abs(m.quantity), 0);
  const totalValue = movements.reduce((sum, m) => sum + Math.abs(m.totalCost || 0), 0);
  // Values arrive with their valuation currency; do not guess a currency or sum mixed currencies.
  const money = (value: number, code?: string | null) => code ? formatInventoryMoney(value, code) : '—';
  const currencyCode = movements[0]?.currencyCode;
  const sameCurrency = currencyCode && movements.every(m => m.currencyCode === currencyCode);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Stock Movements</h1>
          <p className="text-muted-foreground">Track all inventory transactions and movements</p>
        </div>
        <div className="flex items-center space-x-2">
          <Button variant="outline" onClick={fetchMovements}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
          <Button variant="outline" asChild>
            <Link href={INVENTORY_MOVEMENT_REPORT_PATH}>
              <Download className="h-4 w-4 mr-2" />Open report export
            </Link>
          </Button>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Stock Movements</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Summary Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{movements.length}</p><p className="text-sm text-muted-foreground">Total Movements</p></div>
            <ArrowUpDown className="h-8 w-8 text-blue-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-green-600">+{totalInbound.toLocaleString()}</p><p className="text-sm text-muted-foreground">Inbound (Qty)</p></div>
            <TrendingUp className="h-8 w-8 text-green-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold text-red-600">{totalOutbound > 0 ? '-' : ''}{totalOutbound.toLocaleString()}</p><p className="text-sm text-muted-foreground">Outbound (Qty)</p></div>
            <TrendingDown className="h-8 w-8 text-red-500" />
          </div>
        </CardContent></Card>
        <Card><CardContent className="pt-6">
          <div className="flex items-center justify-between">
            <div><p className="text-2xl font-bold">{sameCurrency ? money(totalValue, currencyCode) : '—'}</p><p className="text-sm text-muted-foreground">Total Value</p></div>
            <Package className="h-8 w-8 text-purple-500" />
          </div>
        </CardContent></Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle className="flex items-center"><Filter className="h-4 w-4 mr-2" />Filters</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
            {/* Transfer Number Search */}
            <div className="flex items-center space-x-2">
              <div className="relative flex-1">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Transfer number..."
                  className="pl-8"
                  value={transferNumberSearch}
                  onChange={(e) => setTransferNumberSearch(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && handleSearchByTransferNumber()}
                />
              </div>
              <Button size="sm" variant="outline" onClick={handleSearchByTransferNumber}>
                <Search className="h-4 w-4" />
              </Button>
            </div>
            <Select value={warehouseFilter} onValueChange={setWarehouseFilter}>
              <SelectTrigger><SelectValue placeholder="Warehouse" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Warehouses</SelectItem>
                {warehouses.map(w => <SelectItem key={w.id} value={w.id}>{w.name}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={typeFilter} onValueChange={setTypeFilter}>
              <SelectTrigger><SelectValue placeholder="Movement Type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                {/* Show actual types from database */}
                {availableTypes.map(t => {
                  const typeInfo = getMovementType(t);
                  return <SelectItem key={t} value={t}>{typeInfo.label}</SelectItem>;
                })}
              </SelectContent>
            </Select>
            <div className="flex items-center space-x-2">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              <Input type="date" placeholder="Start Date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div className="flex items-center space-x-2">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              <Input type="date" placeholder="End Date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Movements List */}
      <Card>
        <CardHeader>
          <CardTitle>Movement History</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${movements.length} movement(s) found`}</CardDescription>
          {!loading && movements.some(m => !m.currencyCode) && <p className="text-sm text-amber-700">Valuation currency unavailable. Refresh to load monetary values.</p>}
          {!loading && movements.length > 0 && movements.every(m => m.currencyCode) && !sameCurrency && <p className="text-sm text-muted-foreground">Values use different currencies; no combined total is shown.</p>}
        </CardHeader>
        <CardContent>
          {loading && <div className="text-center py-8 text-muted-foreground">Loading...</div>}
          {error && <div className="text-center py-8 text-red-600">{error}</div>}
          {!loading && !error && (
            <div className="space-y-3">
              {movements.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">No stock movements found.</div>
              ) : (
                movements.map((movement) => {
                  const typeInfo = getMovementType(movement.movementType);
                  const IconComponent = typeInfo.icon;
                  const isInbound = typeInfo.isInbound === true;
                  const isOutbound = typeInfo.isInbound === false;
                  return (
                    <div
                      key={movement.id}
                      className="border rounded-lg p-4 hover:bg-muted/50 transition-colors cursor-pointer"
                      onClick={() => handleMovementClick(movement)}
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-4">
                          <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${
                            isInbound ? 'bg-green-100' :
                            isOutbound ? 'bg-red-100' :
                            'bg-blue-100'
                          }`}>
                            <IconComponent className={`h-5 w-5 ${
                              isInbound ? 'text-green-600' :
                              isOutbound ? 'text-red-600' :
                              'text-blue-600'
                            }`} />
                          </div>
                          <div>
                            <div className="flex items-center space-x-2">
                              <h3 className="font-semibold">{movement.movementNumber}</h3>
                              <Badge className={typeInfo.color}>{typeInfo.label}</Badge>
                            </div>
                            <p className="text-sm text-muted-foreground">
                              {movement.itemName || movement.itemCode || 'Unknown Item'}
                            </p>
                            <p className="text-sm text-muted-foreground">
                              {movement.sourceLocationName && `From: ${movement.sourceLocationName}`}
                              {movement.sourceLocationName && movement.destinationLocationName && ' → '}
                              {movement.destinationLocationName && `To: ${movement.destinationLocationName}`}
                            </p>
                          </div>
                        </div>
                        <div className="text-right">
                          <div className={`text-lg font-bold ${
                            isInbound ? 'text-green-600' :
                            isOutbound ? 'text-red-600' :
                            ''
                          }`}>
                            {isInbound ? '+' : isOutbound ? '-' : ''}
                            {Math.abs(movement.quantity).toLocaleString()}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            {money(movement.totalCost || 0, movement.currencyCode)}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {movement.movementDate ? format(new Date(movement.movementDate), 'MMM dd, yyyy HH:mm') : '-'}
                          </div>
                        </div>
                      </div>
                      {movement.referenceNumber && (
                        <div className="mt-2 text-sm text-muted-foreground">
                          Reference: {movement.referenceType} - {movement.referenceNumber}
                        </div>
                      )}
                      {movement.notes && (
                        <div className="mt-1 text-sm text-muted-foreground">
                          Notes: {movement.notes}
                        </div>
                      )}
                    </div>
                  );
                })
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Movement Detail Dialog */}
      <MovementDetailDialog
        open={detailDialogOpen}
        onOpenChange={setDetailDialogOpen}
        movement={selectedMovement}
        currencyCode={selectedMovement?.currencyCode ?? null}
      />
    </div>
  );
}
