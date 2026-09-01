'use client';

import React, { type Dispatch, type SetStateAction } from 'react';
import { Loader2, Plus, Search, Trash2, Users } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import type { BusinessPartnerDto } from '@/services/businessPartnerService';
import type {
  CreateProcurementPlanItemDto,
  CreateProcurementPlanItemSupplierDto,
  InventoryItemDto,
  MarketAnalysisDto,
  ProcurementBudgetAllocationDto,
} from '@/services/procurementPlanningService';

type UnitOfMeasureOption = {
  value: string;
  label: string;
};

type ProcurementPlanItemDialogBodyProps = {
  currency: string;
  form: CreateProcurementPlanItemDto;
  setForm: Dispatch<SetStateAction<CreateProcurementPlanItemDto>>;
  inventorySearchTerm: string;
  onInventorySearchTermChange: (value: string) => void;
  inventoryResults: InventoryItemDto[];
  loadingInventory: boolean;
  selectedInventoryItem?: InventoryItemDto | null;
  onSelectInventoryItem: (item: InventoryItemDto) => void;
  unitOfMeasureOptions: UnitOfMeasureOption[];
  loadingUnitsOfMeasure: boolean;
  marketAnalyses: MarketAnalysisDto[];
  loadingMarketAnalyses: boolean;
  onMarketAnalysisSelect: (value: string) => void;
  budgetCode?: string;
  budgetAllocations: ProcurementBudgetAllocationDto[];
  loadingBudgetAllocations: boolean;
  suppliers: BusinessPartnerDto[];
  supplierSearchTerm: string;
  onSupplierSearchTermChange: (value: string) => void;
  filteredSuppliers: BusinessPartnerDto[];
  loadingSuppliers: boolean;
  selectedItemSuppliers: CreateProcurementPlanItemSupplierDto[];
  onAddSupplier: (supplier: BusinessPartnerDto) => void;
  onUpdateSupplier: (supplierId: string, field: keyof CreateProcurementPlanItemSupplierDto, value: unknown) => void;
  onRemoveSupplier: (supplierId: string) => void;
  pendingPlanItems?: CreateProcurementPlanItemDto[];
  pendingPlanItemsTotal?: number;
  onAddCurrentItem?: () => void;
  onClearCurrentItem?: () => void;
  onRemovePendingPlanItem?: (index: number) => void;
};

const getItemEstimatedTotal = (item: Pick<CreateProcurementPlanItemDto, 'estimatedQuantity' | 'estimatedUnitPrice'>) =>
  (Number(item.estimatedQuantity) || 0) * (Number(item.estimatedUnitPrice) || 0);

const formatCurrency = (amount: number, currency: string) => {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount || 0);
  } catch {
    return `${currency} ${Number(amount || 0).toLocaleString()}`;
  }
};

export function ProcurementPlanItemDialogBody({
  currency,
  form,
  setForm,
  inventorySearchTerm,
  onInventorySearchTermChange,
  inventoryResults,
  loadingInventory,
  selectedInventoryItem,
  onSelectInventoryItem,
  unitOfMeasureOptions,
  loadingUnitsOfMeasure,
  marketAnalyses,
  loadingMarketAnalyses,
  onMarketAnalysisSelect,
  budgetCode,
  budgetAllocations,
  loadingBudgetAllocations,
  suppliers,
  supplierSearchTerm,
  onSupplierSearchTermChange,
  filteredSuppliers,
  loadingSuppliers,
  selectedItemSuppliers,
  onAddSupplier,
  onUpdateSupplier,
  onRemoveSupplier,
  pendingPlanItems = [],
  pendingPlanItemsTotal = 0,
  onAddCurrentItem,
  onClearCurrentItem,
  onRemovePendingPlanItem,
}: ProcurementPlanItemDialogBodyProps) {
  const selectedInventoryLabel = selectedInventoryItem
    ? `${selectedInventoryItem.itemCode} - ${selectedInventoryItem.name}`
    : '';
  const showInventoryResults =
    inventorySearchTerm.trim().length >= 2 && inventorySearchTerm.trim() !== selectedInventoryLabel;
  const showSupplierResults = supplierSearchTerm.trim().length >= 2;
  const showBatchQueue = Boolean(onAddCurrentItem && onRemovePendingPlanItem);
  const selectedBudgetAllocation = budgetAllocations.find(
    (allocation) => allocation.id === form.procurementBudgetAllocationId,
  );
  const gridClass = showBatchQueue
    ? 'grid grid-cols-1 gap-4 xl:grid-cols-[minmax(460px,1.15fr)_minmax(330px,0.8fr)_minmax(280px,0.65fr)]'
    : 'grid grid-cols-1 gap-4 xl:grid-cols-[minmax(520px,1.2fr)_minmax(360px,0.8fr)]';

  const updateForm = <K extends keyof CreateProcurementPlanItemDto>(field: K, value: CreateProcurementPlanItemDto[K]) => {
    setForm((current) => ({ ...current, [field]: value }));
  };

  const handleSelectInventoryItem = (item: InventoryItemDto) => {
    onSelectInventoryItem(item);
    onInventorySearchTermChange(`${item.itemCode} - ${item.name}`);
  };

  const handleBudgetAllocationSelect = (allocationId: string) => {
    const allocation = budgetAllocations.find((item) => item.id === allocationId);
    if (!allocation) return;

    setForm((current) => ({
      ...current,
      procurementBudgetId: allocation.procurementBudgetId,
      procurementBudgetAllocationId: allocation.id,
      budgetCategoryName: allocation.categoryName,
      approvedBudgetAmount: current.approvedBudgetAmount ?? getItemEstimatedTotal(current),
    }));
  };

  return (
    <div className="min-h-0 flex-1 overflow-y-auto pr-1">
      <div className={gridClass}>
        <div className="space-y-3">
          <div className="relative space-y-1">
            <Label htmlFor="inventoryItemSearch">Product Search</Label>
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="inventoryItemSearch"
                className="h-9 pl-9"
                value={inventorySearchTerm}
                onChange={(event) => onInventorySearchTermChange(event.target.value)}
                placeholder="Search item code, name, or category"
              />
            </div>
            {showInventoryResults && (
              <div className="absolute left-0 right-0 top-full z-50 mt-1 max-h-64 overflow-y-auto rounded-md border bg-background shadow-lg">
                {loadingInventory ? (
                  <div className="flex items-center justify-center py-6">
                    <Loader2 className="h-5 w-5 animate-spin" />
                  </div>
                ) : inventoryResults.length === 0 ? (
                  <div className="px-3 py-4 text-center text-sm text-muted-foreground">No items found</div>
                ) : (
                  <div className="divide-y">
                    {inventoryResults.slice(0, 12).map((item) => (
                      <button
                        key={item.id}
                        type="button"
                        className={`block w-full px-3 py-2 text-left hover:bg-muted ${
                          selectedInventoryItem?.id === item.id ? 'bg-muted' : ''
                        }`}
                        onMouseDown={(event) => {
                          event.preventDefault();
                          handleSelectInventoryItem(item);
                        }}
                      >
                        <div className="truncate text-sm font-medium">{item.name}</div>
                        <div className="truncate text-xs text-muted-foreground">
                          {item.itemCode} | {item.categoryName || 'No Category'} | {item.unitOfMeasure || 'No UOM'}
                        </div>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          <div className="grid gap-3 md:grid-cols-[minmax(220px,1fr)_150px_130px]">
            <div className="space-y-1">
              <Label htmlFor="itemDescription">Item Description *</Label>
              <Input
                id="itemDescription"
                className="h-9"
                value={form.itemDescription}
                onChange={(event) => updateForm('itemDescription', event.target.value)}
                placeholder="Enter item description"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="unitOfMeasure">UOM *</Label>
              <Select
                value={form.unitOfMeasure || undefined}
                onValueChange={(value) => updateForm('unitOfMeasure', value)}
                disabled={loadingUnitsOfMeasure && unitOfMeasureOptions.length === 0}
              >
                <SelectTrigger id="unitOfMeasure" className="h-9">
                  <SelectValue placeholder={loadingUnitsOfMeasure ? 'Loading...' : 'Select UOM'} />
                </SelectTrigger>
                <SelectContent>
                  {unitOfMeasureOptions.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="estimatedQuantity">Quantity *</Label>
              <Input
                id="estimatedQuantity"
                type="number"
                min={1}
                className="h-9"
                value={form.estimatedQuantity}
                onChange={(event) => updateForm('estimatedQuantity', parseFloat(event.target.value) || 0)}
              />
            </div>
          </div>

          <details className="rounded-md border px-3 py-2">
            <summary className="cursor-pointer text-sm font-medium">More Details</summary>
            <div className="mt-3 grid gap-3 md:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="estimatedUnitPrice">Unit Cost ({currency})</Label>
                <Input
                  id="estimatedUnitPrice"
                  type="number"
                  min={0}
                  step={0.01}
                  className="h-9"
                  value={form.estimatedUnitPrice ?? 0}
                  onChange={(event) => updateForm('estimatedUnitPrice', parseFloat(event.target.value) || 0)}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="priority">Priority</Label>
                <Select value={form.priority || 'Medium'} onValueChange={(value) => updateForm('priority', value)}>
                  <SelectTrigger id="priority" className="h-9">
                    <SelectValue placeholder="Select priority" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Low">Low</SelectItem>
                    <SelectItem value="Medium">Medium</SelectItem>
                    <SelectItem value="High">High</SelectItem>
                    <SelectItem value="Critical">Critical</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="requiredDate">Required By</Label>
                <Input
                  id="requiredDate"
                  type="date"
                  className="h-9"
                  value={form.requiredDate || ''}
                  onChange={(event) => updateForm('requiredDate', event.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="plannedQuarter">Quarter</Label>
                <Select value={form.plannedQuarter || ''} onValueChange={(value) => updateForm('plannedQuarter', value)}>
                  <SelectTrigger id="plannedQuarter" className="h-9">
                    <SelectValue placeholder="Select quarter" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Q1">Q1</SelectItem>
                    <SelectItem value="Q2">Q2</SelectItem>
                    <SelectItem value="Q3">Q3</SelectItem>
                    <SelectItem value="Q4">Q4</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="procurementMethod">Method</Label>
                <Select value={form.procurementMethod || 'DirectPurchase'} onValueChange={(value) => updateForm('procurementMethod', value)}>
                  <SelectTrigger id="procurementMethod" className="h-9">
                    <SelectValue placeholder="Select method" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="DirectPurchase">Direct Purchase</SelectItem>
                    <SelectItem value="RFQ">RFQ</SelectItem>
                    <SelectItem value="Tender">Tender</SelectItem>
                    <SelectItem value="Framework">Framework</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label>Market Analysis</Label>
                <Select value={form.marketAnalysisId || 'none'} onValueChange={onMarketAnalysisSelect}>
                  <SelectTrigger className="h-9">
                    <SelectValue placeholder={loadingMarketAnalyses ? 'Loading...' : 'Select analysis'} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No linked analysis</SelectItem>
                    {marketAnalyses.map((analysis) => (
                      <SelectItem key={analysis.id} value={analysis.id}>
                        {analysis.title} - {formatCurrency(analysis.currentMarketPrice || 0, analysis.currency || currency)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="procurementBudgetAllocationId">Budget Allocation</Label>
                <Select
                  value={form.procurementBudgetAllocationId || undefined}
                  onValueChange={handleBudgetAllocationSelect}
                  disabled={loadingBudgetAllocations || budgetAllocations.length === 0}
                >
                  <SelectTrigger id="procurementBudgetAllocationId" className="h-9">
                    <SelectValue
                      placeholder={
                        loadingBudgetAllocations
                          ? 'Loading allocations...'
                          : budgetAllocations.length === 0
                            ? 'No allocations configured'
                            : 'Select budget allocation'
                      }
                    />
                  </SelectTrigger>
                  <SelectContent>
                    {budgetAllocations.map((allocation) => (
                      <SelectItem key={allocation.id} value={allocation.id}>
                        {allocation.categoryName} - {formatCurrency(allocation.remainingAmount, currency)} remaining
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {budgetCode
                    ? budgetAllocations.length > 0
                      ? `Controlled by ${budgetCode}`
                      : `${budgetCode} has no category allocations configured`
                    : 'Link an approved budget to the plan before selecting an allocation'}
                </p>
              </div>
              <div className="space-y-1">
                <Label htmlFor="approvedBudgetAmount">Item Budget Amount</Label>
                <Input
                  id="approvedBudgetAmount"
                  type="number"
                  min={0}
                  max={selectedBudgetAllocation?.remainingAmount}
                  step={0.01}
                  className="h-9"
                  value={form.approvedBudgetAmount ?? getItemEstimatedTotal(form)}
                  onChange={(event) => updateForm('approvedBudgetAmount', event.target.value ? parseFloat(event.target.value) || 0 : undefined)}
                />
                <p className="text-xs text-muted-foreground">
                  Defaults to the item total and becomes approved with the plan.
                </p>
              </div>
              {selectedBudgetAllocation && (
                <div className="grid grid-cols-3 gap-2 rounded-md border bg-muted/40 p-2 text-xs md:col-span-2">
                  <div>
                    <span className="block text-muted-foreground">Allocated</span>
                    <span className="font-medium">{formatCurrency(selectedBudgetAllocation.allocatedAmount, currency)}</span>
                  </div>
                  <div>
                    <span className="block text-muted-foreground">Utilized</span>
                    <span className="font-medium">{formatCurrency(selectedBudgetAllocation.utilizedAmount, currency)}</span>
                  </div>
                  <div>
                    <span className="block text-muted-foreground">Remaining</span>
                    <span className="font-medium">{formatCurrency(selectedBudgetAllocation.remainingAmount, currency)}</span>
                  </div>
                </div>
              )}
              <div className="space-y-1 md:col-span-2">
                <Label htmlFor="specifications">Specifications</Label>
                <Textarea
                  id="specifications"
                  rows={2}
                  className="min-h-[64px]"
                  value={form.specifications || ''}
                  onChange={(event) => updateForm('specifications', event.target.value)}
                />
              </div>
              <div className="space-y-1 md:col-span-2">
                <Label htmlFor="justification">Justification</Label>
                <Textarea
                  id="justification"
                  rows={2}
                  className="min-h-[64px]"
                  value={form.justification || ''}
                  onChange={(event) => updateForm('justification', event.target.value)}
                />
              </div>
            </div>
          </details>

          <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/40 px-3 py-2">
            <div>
              <p className="text-xs text-muted-foreground">Current item cost</p>
              <p className="font-semibold">{formatCurrency(getItemEstimatedTotal(form), currency)}</p>
            </div>
            {onAddCurrentItem && onClearCurrentItem && (
              <div className="flex gap-2">
                <Button type="button" variant="outline" size="sm" onClick={onClearCurrentItem}>
                  Clear
                </Button>
                <Button type="button" size="sm" onClick={onAddCurrentItem}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add to List
                </Button>
              </div>
            )}
          </div>
        </div>

        <div className="space-y-3">
          <div className="space-y-2 rounded-md border p-3">
            <div className="flex items-center justify-between gap-2">
              <h4 className="flex items-center gap-2 font-medium">
                <Users className="h-4 w-4" />
                Suppliers
              </h4>
              <Badge variant="secondary">{selectedItemSuppliers.length}</Badge>
            </div>
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Search suppliers"
                value={supplierSearchTerm}
                onChange={(event) => onSupplierSearchTermChange(event.target.value)}
                className="h-9 pl-9"
              />
              {showSupplierResults && (
                <div className="absolute left-0 right-0 top-full z-50 mt-1 max-h-48 overflow-y-auto rounded-md border bg-background shadow-lg">
                  {loadingSuppliers ? (
                    <div className="flex items-center justify-center py-5">
                      <Loader2 className="h-5 w-5 animate-spin" />
                    </div>
                  ) : filteredSuppliers.length === 0 ? (
                    <div className="px-3 py-4 text-center text-sm text-muted-foreground">No suppliers found</div>
                  ) : (
                    <div className="divide-y">
                      {filteredSuppliers.slice(0, 12).map((supplier) => (
                        <button
                          key={supplier.id}
                          type="button"
                          className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left hover:bg-muted"
                          onMouseDown={(event) => {
                            event.preventDefault();
                            onAddSupplier(supplier);
                            onSupplierSearchTermChange('');
                          }}
                        >
                          <span className="min-w-0">
                            <span className="block truncate text-sm font-medium">{supplier.partnerName}</span>
                            <span className="block truncate text-xs text-muted-foreground">{supplier.partnerCode}</span>
                          </span>
                          <Plus className="h-4 w-4 shrink-0 text-green-600" />
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
            <div className="max-h-[320px] overflow-y-auto rounded-md border">
              {selectedItemSuppliers.length === 0 ? (
                <div className="flex h-24 items-center justify-center px-4 text-center text-sm text-muted-foreground">
                  No suppliers selected
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="text-xs">Supplier</TableHead>
                      <TableHead className="w-[70px] text-xs">Preferred</TableHead>
                      <TableHead className="w-[90px] text-xs">Quote</TableHead>
                      <TableHead className="w-[42px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {selectedItemSuppliers.map((itemSupplier) => {
                      const supplier = suppliers.find((entry) => entry.id === itemSupplier.supplierId);
                      return (
                        <TableRow key={itemSupplier.supplierId}>
                          <TableCell className="py-1.5">
                            <div className="max-w-[150px] truncate text-xs font-medium">{supplier?.partnerName || 'Unknown'}</div>
                            <div className="max-w-[150px] truncate text-xs text-muted-foreground">{supplier?.partnerCode}</div>
                          </TableCell>
                          <TableCell className="py-1.5">
                            <input
                              type="checkbox"
                              checked={itemSupplier.isPreferred || false}
                              onChange={(event) => onUpdateSupplier(itemSupplier.supplierId, 'isPreferred', event.target.checked)}
                              className="h-4 w-4"
                            />
                          </TableCell>
                          <TableCell className="py-1.5">
                            <Input
                              type="number"
                              min={0}
                              step={0.01}
                              value={itemSupplier.quotedUnitPrice || ''}
                              onChange={(event) => onUpdateSupplier(itemSupplier.supplierId, 'quotedUnitPrice', parseFloat(event.target.value) || undefined)}
                              className="h-7 w-[78px] text-xs"
                            />
                          </TableCell>
                          <TableCell className="py-1.5">
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              className="h-7 w-7"
                              onClick={() => onRemoveSupplier(itemSupplier.supplierId)}
                            >
                              <Trash2 className="h-3.5 w-3.5 text-red-500" />
                            </Button>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              )}
            </div>
          </div>
        </div>

        {showBatchQueue && (
          <div className="space-y-3">
            <div className="space-y-2 rounded-md border p-3">
              <div className="flex items-center justify-between">
                <h4 className="font-medium">Items to Save</h4>
                <Badge variant="secondary">{pendingPlanItems.length}</Badge>
              </div>
              <div className="max-h-[260px] overflow-y-auto rounded-md border">
                {pendingPlanItems.length === 0 ? (
                  <div className="flex h-24 items-center justify-center px-4 text-center text-sm text-muted-foreground">
                    No queued items
                  </div>
                ) : (
                  <div className="divide-y">
                    {pendingPlanItems.map((item, index) => (
                      <div key={`${item.itemDescription}-${index}`} className="flex items-start gap-2 p-2.5">
                        <div className="min-w-0 flex-1">
                          <div className="truncate text-sm font-medium">{item.itemDescription}</div>
                          <div className="text-xs text-muted-foreground">
                            {item.estimatedQuantity} {item.unitOfMeasure} | {formatCurrency(getItemEstimatedTotal(item), currency)}
                          </div>
                          {item.itemSuppliers?.length ? (
                            <div className="text-xs text-muted-foreground">{item.itemSuppliers.length} supplier(s)</div>
                          ) : null}
                        </div>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="h-7 w-7 shrink-0"
                          onClick={() => onRemovePendingPlanItem?.(index)}
                        >
                          <Trash2 className="h-3.5 w-3.5 text-red-500" />
                        </Button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Batch total</span>
                <span className="font-semibold">{formatCurrency(pendingPlanItemsTotal, currency)}</span>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
