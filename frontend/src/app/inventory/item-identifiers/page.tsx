'use client';

import { ChangeEvent, useEffect, useMemo, useRef, useState } from 'react';
import { AxiosError } from 'axios';
import { Download, Loader2, RefreshCw, Save, Search, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  inventoryManagementService,
  InventoryIdentifierMatchDto,
  InventoryItemDto,
  ItemUnitOfMeasureDto,
  UnitOfMeasureDto,
  UpdateInventoryItemIdentifiersDto,
  UpdateItemUnitIdentifierDto
} from '@/services/inventoryManagementService';

type IdentifierProblem = { detail?: string; title?: string; errors?: string[] };

const messageFrom = (error: unknown, fallback: string) => {
  const response = (error as AxiosError<IdentifierProblem>)?.response?.data;
  return response?.detail || response?.errors?.join(' ') || response?.title || fallback;
};

export default function InventoryItemIdentifiersPage() {
  const [items, setItems] = useState<InventoryItemDto[]>([]);
  const [unitDefinitions, setUnitDefinitions] = useState<UnitOfMeasureDto[]>([]);
  const [itemUnits, setItemUnits] = useState<ItemUnitOfMeasureDto[]>([]);
  const [selectedItem, setSelectedItem] = useState<InventoryItemDto | null>(null);
  const [draft, setDraft] = useState<UpdateInventoryItemIdentifiersDto>({});
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [savingItem, setSavingItem] = useState(false);
  const [savingUnitId, setSavingUnitId] = useState<string | null>(null);
  const [lookup, setLookup] = useState('');
  const [lookupResult, setLookupResult] = useState<InventoryIdentifierMatchDto | null>(null);
  const [lookupLoading, setLookupLoading] = useState(false);
  const [newUnitId, setNewUnitId] = useState('');
  const [newUnitBarcode, setNewUnitBarcode] = useState('');
  const [newUnitConversion, setNewUnitConversion] = useState('1');
  const fileInput = useRef<HTMLInputElement>(null);

  const loadPage = async () => {
    setLoading(true);
    try {
      const [loadedItems, loadedUnits] = await Promise.all([
        inventoryManagementService.getInventoryItems(),
        inventoryManagementService.getUnitsOfMeasure(true)
      ]);
      setItems(loadedItems);
      setUnitDefinitions(loadedUnits);
      if (selectedItem) {
        const refreshed = loadedItems.find(item => item.id === selectedItem.id) ?? null;
        setSelectedItem(refreshed);
        if (refreshed) setDraft(toDraft(refreshed));
      }
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to load item identifiers.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadPage();
  }, []);

  const filteredItems = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return items;
    return items.filter(item =>
      [item.itemCode, item.name, item.barcode, item.alternateBarcode, item.qrCode]
        .some(value => value?.toLowerCase().includes(term))
    );
  }, [items, search]);

  const selectItem = async (item: InventoryItemDto) => {
    setSelectedItem(item);
    setDraft(toDraft(item));
    setItemUnits([]);
    try {
      setItemUnits(await inventoryManagementService.getItemIdentifierUnits(item.id));
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to load item-unit identifiers.'));
    }
  };

  const saveItem = async () => {
    if (!selectedItem) return;
    setSavingItem(true);
    try {
      const updated = await inventoryManagementService.updateItemIdentifiers(selectedItem.id, draft);
      setItems(values => values.map(value => value.id === updated.id ? { ...value, ...updated } : value));
      setSelectedItem(current => current ? { ...current, ...updated } : current);
      setDraft(toDraft(updated));
      toast.success('Item identifiers saved.');
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to save item identifiers.'));
    } finally {
      setSavingItem(false);
    }
  };

  const saveUnit = async (row: ItemUnitOfMeasureDto) => {
    if (!selectedItem) return;
    setSavingUnitId(row.unitOfMeasureId);
    try {
      const saved = await inventoryManagementService.updateItemUnitIdentifier(
        selectedItem.id,
        row.unitOfMeasureId,
        unitPayload(row)
      );
      setItemUnits(values => values.map(value => value.unitOfMeasureId === saved.unitOfMeasureId ? saved : value));
      toast.success(`${saved.unitCode} identifier saved.`);
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to save the unit identifier.'));
    } finally {
      setSavingUnitId(null);
    }
  };

  const addUnit = async () => {
    if (!selectedItem || !newUnitId) return;
    const conversion = Number(newUnitConversion);
    if (!Number.isFinite(conversion) || conversion <= 0) {
      toast.error('Conversion to base must be greater than zero.');
      return;
    }
    setSavingUnitId(newUnitId);
    try {
      const saved = await inventoryManagementService.updateItemUnitIdentifier(selectedItem.id, newUnitId, {
        unitOfMeasureId: newUnitId,
        conversionToBase: conversion,
        isBaseUnit: false,
        isPurchaseUnit: false,
        isSalesUnit: false,
        isStockingUnit: false,
        barcode: newUnitBarcode || undefined
      });
      setItemUnits(values => [...values.filter(value => value.unitOfMeasureId !== saved.unitOfMeasureId), saved]);
      setNewUnitId('');
      setNewUnitBarcode('');
      setNewUnitConversion('1');
      toast.success(`${saved.unitCode} added.`);
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to add the unit identifier.'));
    } finally {
      setSavingUnitId(null);
    }
  };

  const resolve = async () => {
    if (!lookup.trim()) return;
    setLookupLoading(true);
    setLookupResult(null);
    try {
      setLookupResult(await inventoryManagementService.resolveItemIdentifier(lookup));
    } catch (error) {
      toast.error(messageFrom(error, 'Identifier was not found.'));
    } finally {
      setLookupLoading(false);
    }
  };

  const exportCsv = async () => {
    try {
      const blob = await inventoryManagementService.exportItemIdentifiers();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `inventory-item-identifiers-${new Date().toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to export identifiers.'));
    }
  };

  const importCsv = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    try {
      const result = await inventoryManagementService.importItemIdentifiers(file);
      toast.success(`Imported ${result.updatedItems} item and ${result.updatedUnits} unit identifier updates.`);
      await loadPage();
      if (selectedItem) setItemUnits(await inventoryManagementService.getItemIdentifierUnits(selectedItem.id));
    } catch (error) {
      toast.error(messageFrom(error, 'Unable to import identifiers.'));
    }
  };

  const updateUnitRow = (unitOfMeasureId: string, patch: Partial<ItemUnitOfMeasureDto>) => {
    setItemUnits(values => values.map(value => value.unitOfMeasureId === unitOfMeasureId ? { ...value, ...patch } : value));
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Item identifiers</h1>
          <p className="text-muted-foreground">Govern primary, alternate, QR, and unit barcodes from one tenant-safe workspace.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <input ref={fileInput} type="file" accept=".csv,text/csv" className="hidden" onChange={importCsv} />
          <Button variant="outline" onClick={() => fileInput.current?.click()}><Upload className="mr-2 h-4 w-4" />Import CSV</Button>
          <Button variant="outline" onClick={exportCsv}><Download className="mr-2 h-4 w-4" />Export CSV</Button>
          <Button variant="outline" onClick={() => void loadPage()} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
        </div>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/inventory">Inventory</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Item identifiers</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <Card>
        <CardHeader>
          <CardTitle>Authenticated lookup</CardTitle>
          <CardDescription>Resolve an identifier through the same shared service used by inventory integrations.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3 md:flex-row">
          <Input value={lookup} onChange={event => setLookup(event.target.value)} onKeyDown={event => event.key === 'Enter' && void resolve()} placeholder="Enter a barcode or QR value" />
          <Button onClick={resolve} disabled={lookupLoading || !lookup.trim()}>{lookupLoading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Search className="mr-2 h-4 w-4" />}Resolve</Button>
          {lookupResult && (
            <div className="flex min-w-fit items-center gap-2 rounded-md border px-3 py-2 text-sm">
              <Badge variant="secondary">{lookupResult.identifierKind}</Badge>
              <span className="font-medium">{lookupResult.itemCode}</span>
              <span className="text-muted-foreground">{lookupResult.itemName}{lookupResult.unitCode ? ` · ${lookupResult.unitCode}` : ''}</span>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.05fr)_minmax(420px,0.95fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Item register</CardTitle>
            <CardDescription>Select an item to maintain its identifier family.</CardDescription>
            <div className="relative pt-2"><Search className="absolute left-3 top-5 h-4 w-4 text-muted-foreground" /><Input className="pl-9" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search code, name, barcode, or QR" /></div>
          </CardHeader>
          <CardContent>
            <div className="max-h-[620px] overflow-auto rounded-md border">
              <Table>
                <TableHeader><TableRow><TableHead>Item</TableHead><TableHead>Primary</TableHead><TableHead>Alternate / QR</TableHead></TableRow></TableHeader>
                <TableBody>
                  {loading ? (
                    <TableRow><TableCell colSpan={3} className="h-24 text-center"><Loader2 className="mx-auto h-5 w-5 animate-spin" /></TableCell></TableRow>
                  ) : filteredItems.length === 0 ? (
                    <TableRow><TableCell colSpan={3} className="h-24 text-center text-muted-foreground">No items found.</TableCell></TableRow>
                  ) : filteredItems.map(item => (
                    <TableRow key={item.id} onClick={() => void selectItem(item)} className={`cursor-pointer ${selectedItem?.id === item.id ? 'bg-muted' : ''}`}>
                      <TableCell><div className="font-medium">{item.itemCode}</div><div className="text-xs text-muted-foreground">{item.name}</div></TableCell>
                      <TableCell className="font-mono text-xs">{item.barcode || '—'}</TableCell>
                      <TableCell className="font-mono text-xs"><div>{item.alternateBarcode || '—'}</div><div className="text-muted-foreground">{item.qrCode || '—'}</div></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card>
            <CardHeader><CardTitle>{selectedItem ? `${selectedItem.itemCode} identifiers` : 'Select an item'}</CardTitle><CardDescription>Blank values clear an identifier. Values are normalized before save.</CardDescription></CardHeader>
            <CardContent className="space-y-4">
              <IdentifierField label="Primary barcode" value={draft.barcode} disabled={!selectedItem} onChange={value => setDraft(current => ({ ...current, barcode: value }))} />
              <IdentifierField label="Alternate barcode" value={draft.alternateBarcode} disabled={!selectedItem} onChange={value => setDraft(current => ({ ...current, alternateBarcode: value }))} />
              <IdentifierField label="QR identifier" value={draft.qrCode} disabled={!selectedItem} onChange={value => setDraft(current => ({ ...current, qrCode: value }))} />
              <Button className="w-full" onClick={saveItem} disabled={!selectedItem || savingItem}>{savingItem ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Save item identifiers</Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle>Unit barcodes</CardTitle><CardDescription>Each packaging or stocking unit shares the same tenant-wide uniqueness rule.</CardDescription></CardHeader>
            <CardContent className="space-y-4">
              {selectedItem && itemUnits.map(row => (
                <div key={row.unitOfMeasureId} className="grid gap-2 rounded-md border p-3 md:grid-cols-[90px_1fr_90px_auto] md:items-end">
                  <div><Label>Unit</Label><div className="pt-2 text-sm font-medium">{row.unitCode}</div></div>
                  <div><Label>Barcode</Label><Input value={row.barcode || ''} onChange={event => updateUnitRow(row.unitOfMeasureId, { barcode: event.target.value })} /></div>
                  <div><Label>To base</Label><Input type="number" min="0.00000001" step="any" value={row.conversionFactor} onChange={event => updateUnitRow(row.unitOfMeasureId, { conversionFactor: Number(event.target.value) })} /></div>
                  <Button size="sm" onClick={() => void saveUnit(row)} disabled={savingUnitId === row.unitOfMeasureId}>{savingUnitId === row.unitOfMeasureId ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}</Button>
                </div>
              ))}

              {selectedItem && (
                <div className="grid gap-2 rounded-md border border-dashed p-3 md:grid-cols-[130px_1fr_90px_auto] md:items-end">
                  <div><Label>Add unit</Label><Select value={newUnitId} onValueChange={setNewUnitId}><SelectTrigger><SelectValue placeholder="Unit" /></SelectTrigger><SelectContent>{unitDefinitions.filter(unit => !itemUnits.some(row => row.unitOfMeasureId === unit.id)).map(unit => <SelectItem key={unit.id} value={unit.id}>{unit.code}</SelectItem>)}</SelectContent></Select></div>
                  <div><Label>Barcode</Label><Input value={newUnitBarcode} onChange={event => setNewUnitBarcode(event.target.value)} /></div>
                  <div><Label>To base</Label><Input type="number" min="0.00000001" step="any" value={newUnitConversion} onChange={event => setNewUnitConversion(event.target.value)} /></div>
                  <Button size="sm" onClick={addUnit} disabled={!newUnitId || savingUnitId === newUnitId}>{savingUnitId === newUnitId ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Add'}</Button>
                </div>
              )}
              {!selectedItem && <p className="py-8 text-center text-sm text-muted-foreground">Select an inventory item first.</p>}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function IdentifierField({ label, value, disabled, onChange }: { label: string; value?: string; disabled: boolean; onChange: (value: string) => void }) {
  return <div className="space-y-2"><Label>{label}</Label><Input value={value || ''} disabled={disabled} onChange={event => onChange(event.target.value)} /></div>;
}

const toDraft = (item: InventoryItemDto): UpdateInventoryItemIdentifiersDto => ({
  barcode: item.barcode || '',
  alternateBarcode: item.alternateBarcode || '',
  qrCode: item.qrCode || ''
});

const unitPayload = (row: ItemUnitOfMeasureDto): UpdateItemUnitIdentifierDto => ({
  unitOfMeasureId: row.unitOfMeasureId,
  conversionToBase: row.conversionFactor,
  isBaseUnit: row.isBaseUnit,
  isPurchaseUnit: row.isPurchaseUnit,
  isSalesUnit: row.isSalesUnit,
  isStockingUnit: row.isStockingUnit ?? false,
  barcode: row.barcode || undefined
});
