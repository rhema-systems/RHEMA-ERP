'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Separator } from '@/components/ui/separator';
import { ShoppingCart, ArrowLeft, Plus, Trash2, Save, Building2 } from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type CreateSalesOrderDto, type CreateSalesOrderLineDto } from '@/services/salesOrderService';
import { projectService, type ProjectReleasedUnitSalesLookupDto } from '@/services/projectService';
import { apiService } from '@/services/api.service';

interface LineItem extends CreateSalesOrderLineDto {
  key: string;
}

interface LinkedProjectContext {
  projectId: string;
  projectCode?: string;
  projectTitle?: string;
  projectUnitId?: string;
  projectUnitCode?: string;
  projectUnitName?: string;
}

export default function CreateSalesOrderPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [linkedProjectContext, setLinkedProjectContext] = useState<LinkedProjectContext | null>(null);
  const [releasedUnits, setReleasedUnits] = useState<ProjectReleasedUnitSalesLookupDto[]>([]);
  const [releasedUnitsLoading, setReleasedUnitsLoading] = useState(false);
  const [releasedUnitsSearch, setReleasedUnitsSearch] = useState('');
  const [selectedReleasedUnitId, setSelectedReleasedUnitId] = useState('');

  // Form state
  const [businessPartnerId, setBusinessPartnerId] = useState('');
  const [customerSearch, setCustomerSearch] = useState('');
  const [customerResults, setCustomerResults] = useState<any[]>([]);
  const [selectedCustomer, setSelectedCustomer] = useState<any>(null);
  const [orderType, setOrderType] = useState('Standard');
  const [priority, setPriority] = useState('Normal');
  const [expectedDeliveryDate, setExpectedDeliveryDate] = useState('');
  const [paymentTerms, setPaymentTerms] = useState('');
  const [customerPoNumber, setCustomerPoNumber] = useState('');
  const [propertyReference, setPropertyReference] = useState('');
  const [propertyType, setPropertyType] = useState('');
  const [shippingAddress, setShippingAddress] = useState('');
  const [billingAddress, setBillingAddress] = useState('');
  const [notes, setNotes] = useState('');
  const [internalNotes, setInternalNotes] = useState('');

  // Order lines
  const [lines, setLines] = useState<LineItem[]>([
    { key: crypto.randomUUID(), itemName: '', quantity: 1, unitPrice: 0, discountPercent: 0, taxPercent: 0 },
  ]);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const projectId = params.get('projectId');
    const projectCode = params.get('projectCode') || undefined;
    const projectTitle = params.get('projectTitle') || undefined;
    const projectUnitId = params.get('projectUnitId') || undefined;
    const projectUnitCode = params.get('projectUnitCode') || undefined;
    const projectUnitName = params.get('projectUnitName') || undefined;
    const customerId = params.get('customerId');
    const customerName = params.get('customerName');
    const propertyReferenceParam = params.get('propertyReference');
    const orderTypeParam = params.get('orderType');

    if (projectId) {
      setLinkedProjectContext({
        projectId,
        projectCode,
        projectTitle,
        projectUnitId,
        projectUnitCode,
        projectUnitName,
      });
    }

    if (customerId) {
      setBusinessPartnerId(customerId);
    }

    if (customerName) {
      setCustomerSearch(customerName);
      setSelectedCustomer({ id: customerId, companyName: customerName, name: customerName });
    }

    if (propertyReferenceParam) {
      setPropertyReference(propertyReferenceParam);
    }

    if (orderTypeParam) {
      setOrderType(orderTypeParam);
    }
  }, []);

  useEffect(() => {
    if (linkedProjectContext) {
      return;
    }

    const timer = setTimeout(async () => {
      try {
        setReleasedUnitsLoading(true);
        const items = await projectService.getReleasedProjectUnitsForSales(releasedUnitsSearch, 50);
        setReleasedUnits(items.filter((item) => item.canCreateSalesOrder));
      } catch {
        setReleasedUnits([]);
      } finally {
        setReleasedUnitsLoading(false);
      }
    }, 250);

    return () => clearTimeout(timer);
  }, [linkedProjectContext, releasedUnitsSearch]);

  const selectedReleasedUnit = releasedUnits.find((item) => item.projectUnitId === selectedReleasedUnitId);

  const applyReleasedUnit = (unit: ProjectReleasedUnitSalesLookupDto) => {
    setLinkedProjectContext({
      projectId: unit.projectId,
      projectCode: unit.projectCode,
      projectTitle: unit.projectTitle,
      projectUnitId: unit.projectUnitId,
      projectUnitCode: unit.projectUnitCode,
      projectUnitName: unit.projectUnitName,
    });
    setBusinessPartnerId(unit.customerBusinessPartnerId || '');
    setCustomerSearch(unit.customerBusinessPartnerName || '');
    setSelectedCustomer(unit.customerBusinessPartnerId && unit.customerBusinessPartnerName
      ? { id: unit.customerBusinessPartnerId, companyName: unit.customerBusinessPartnerName, name: unit.customerBusinessPartnerName }
      : null);
    setPropertyReference(unit.propertyReference || '');
    setPropertyType(unit.suggestedPropertyType || '');
    setOrderType(unit.suggestedOrderType || 'PropertySale');
    setLines((current) => {
      const first = current[0];
      const shouldReplaceFirst = current.length === 1
        && !first.itemName
        && !first.description
        && first.unitPrice === 0;

      const updatedFirst: LineItem = {
        ...(shouldReplaceFirst ? first : current[0]),
        description: unit.suggestedSalesOrderLineDescription || first.description,
        itemName: unit.projectUnitCode || unit.projectUnitName,
        quantity: 1,
        unitPrice: unit.basePrice ?? first.unitPrice,
        unitOfMeasure: unit.areaSquareMeters ? 'Unit' : (first.unitOfMeasure || 'Lot'),
      };

      if (shouldReplaceFirst) {
        return [{ ...updatedFirst, key: first.key }];
      }

      return current.map((line, index) => index === 0 ? { ...line, ...updatedFirst, key: line.key } : line);
    });
  };

  const searchCustomers = async (query: string) => {
    setCustomerSearch(query);
    if (query.length < 2) { setCustomerResults([]); return; }
    try {
      const results = await apiService.get<any>(`/procurement/business-partners?search=${encodeURIComponent(query)}&pageSize=10`);
      setCustomerResults(results.items || results || []);
    } catch {
      setCustomerResults([]);
    }
  };

  const selectCustomer = (customer: any) => {
    setBusinessPartnerId(customer.id);
    setSelectedCustomer(customer);
    setCustomerSearch(customer.companyName || customer.name || '');
    setCustomerResults([]);
    if (customer.billingAddress) setBillingAddress(customer.billingAddress);
    if (customer.shippingAddress) setShippingAddress(customer.shippingAddress);
    if (customer.paymentTerms) setPaymentTerms(customer.paymentTerms);
  };

  const addLine = () => {
    setLines([...lines, { key: crypto.randomUUID(), itemName: '', quantity: 1, unitPrice: 0, discountPercent: 0, taxPercent: 0 }]);
  };

  const removeLine = (key: string) => {
    if (lines.length <= 1) { toast.error('At least one line item is required'); return; }
    setLines(lines.filter(l => l.key !== key));
  };

  const updateLine = (key: string, field: keyof LineItem, value: any) => {
    setLines(lines.map(l => l.key === key ? { ...l, [field]: value } : l));
  };

  const calcLineTotal = (line: LineItem) => {
    const base = line.quantity * line.unitPrice;
    const discounted = base * (1 - (line.discountPercent || 0) / 100);
    const taxed = discounted * (1 + (line.taxPercent || 0) / 100);
    return taxed;
  };

  const subtotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const totalDiscount = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice * (l.discountPercent || 0) / 100, 0);
  const totalTax = lines.reduce((sum, l) => {
    const base = l.quantity * l.unitPrice;
    const discounted = base * (1 - (l.discountPercent || 0) / 100);
    return sum + discounted * (l.taxPercent || 0) / 100;
  }, 0);
  const grandTotal = subtotal - totalDiscount + totalTax;

  const handleSave = async () => {
    if (!businessPartnerId) { toast.error('Please select a customer'); return; }
    if (lines.some(l => !l.itemName)) { toast.error('All lines must have an item name'); return; }
    if (lines.some(l => l.quantity <= 0)) { toast.error('Quantities must be greater than 0'); return; }

    const dto: CreateSalesOrderDto = {
      businessPartnerId,
      orderType,
      priority,
      expectedDeliveryDate: expectedDeliveryDate || undefined,
      paymentTerms: paymentTerms || undefined,
      customerPoNumber: customerPoNumber || undefined,
      propertyReference: propertyReference || undefined,
      propertyType: propertyType || undefined,
      shippingAddress: shippingAddress || undefined,
      billingAddress: billingAddress || undefined,
      notes: notes || undefined,
      internalNotes: internalNotes || undefined,
      lines: lines.map(l => ({
        itemName: l.itemName,
        itemCode: l.itemCode || undefined,
        itemId: l.itemId || undefined,
        description: l.description || undefined,
        quantity: l.quantity,
        unitOfMeasure: l.unitOfMeasure || 'EA',
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent || 0,
        taxPercent: l.taxPercent || 0,
      })),
    };

    try {
      setSaving(true);
      const result = await salesOrderService.createSalesOrder(dto);
      if (linkedProjectContext?.projectUnitId) {
        try {
          await projectService.linkSalesOrderToProjectUnit(linkedProjectContext.projectUnitId, result.id);
        } catch (linkError: any) {
          toast.warning(linkError?.message || 'Sales order created, but the project unit could not be linked automatically.');
        }
      }
      toast.success(`Sales Order ${result.orderNumber} created`);
      router.push(`/sales/orders/${result.id}`);
    } catch (error: any) {
      toast.error(error.message || 'Failed to create sales order');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/sales/orders')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <ShoppingCart className="h-6 w-6 text-blue-600" />
              New Sales Order
            </h1>
            <p className="text-gray-500">Create a new sales order</p>
          </div>
        </div>
        <Button onClick={handleSave} disabled={saving}>
          <Save className="h-4 w-4 mr-2" />{saving ? 'Saving...' : 'Save Order'}
        </Button>
      </div>

      {linkedProjectContext && (
        <Card className="border-emerald-200 bg-emerald-50/40">
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm font-medium text-emerald-700">
              <Building2 className="h-4 w-4" />
              Creating Order For Project-Linked Unit
            </div>
            <div className="mt-2 text-sm text-slate-700">
              <p className="font-semibold">
                {linkedProjectContext.projectCode}
                {linkedProjectContext.projectTitle ? ` • ${linkedProjectContext.projectTitle}` : ''}
              </p>
              <p>
                {linkedProjectContext.projectUnitCode || linkedProjectContext.projectUnitName || 'Linked project unit'}
                {linkedProjectContext.projectUnitCode && linkedProjectContext.projectUnitName
                  ? ` • ${linkedProjectContext.projectUnitName}`
                  : ''}
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {!linkedProjectContext && (
        <Card className="border-dashed border-emerald-200">
          <CardHeader>
            <CardTitle className="text-base">Start From Released Project Unit</CardTitle>
            <CardDescription>Pick a released unit to prefill customer and property context before drafting the order.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_220px]">
              <Input
                placeholder="Search by project code, title, unit, or customer..."
                value={releasedUnitsSearch}
                onChange={(event) => setReleasedUnitsSearch(event.target.value)}
              />
              <Select value={selectedReleasedUnitId || 'none'} onValueChange={(value) => setSelectedReleasedUnitId(value === 'none' ? '' : value)}>
                <SelectTrigger><SelectValue placeholder="Select released unit" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Select released unit</SelectItem>
                  {releasedUnits.map((unit) => (
                    <SelectItem key={unit.projectUnitId} value={unit.projectUnitId}>
                      {unit.projectCode} - {unit.projectUnitCode || unit.projectUnitName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {releasedUnitsLoading ? (
              <p className="text-sm text-muted-foreground">Loading released units...</p>
            ) : null}
            {!releasedUnitsLoading && releasedUnits.length === 0 ? (
              <p className="text-sm text-muted-foreground">No released project units are currently ready for sales-order handoff.</p>
            ) : null}
            {selectedReleasedUnit ? (
              <div className="rounded-lg border bg-slate-50 p-4 text-sm text-slate-700">
                <div className="font-semibold text-slate-900">
                  {selectedReleasedUnit.projectCode}
                  {selectedReleasedUnit.projectTitle ? ` • ${selectedReleasedUnit.projectTitle}` : ''}
                </div>
                <div className="mt-1">
                  {selectedReleasedUnit.projectUnitCode || selectedReleasedUnit.projectUnitName}
                  {selectedReleasedUnit.projectUnitCode && selectedReleasedUnit.projectUnitName
                    ? ` • ${selectedReleasedUnit.projectUnitName}`
                    : ''}
                </div>
                <div className="mt-1 text-xs text-slate-500">
                  {selectedReleasedUnit.customerBusinessPartnerName || 'No customer assigned'}
                  {selectedReleasedUnit.basePrice != null ? ` • ${selectedReleasedUnit.currency} ${selectedReleasedUnit.basePrice.toLocaleString()}` : ''}
                  {selectedReleasedUnit.suggestedOrderType ? ` • ${selectedReleasedUnit.suggestedOrderType}` : ''}
                </div>
              </div>
            ) : null}
            <div className="flex justify-end">
              <Button
                variant="outline"
                disabled={!selectedReleasedUnit || !selectedReleasedUnit.canCreateSalesOrder}
                onClick={() => selectedReleasedUnit && applyReleasedUnit(selectedReleasedUnit)}
              >
                Use Released Unit
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Customer Selection */}
      <Card>
        <CardHeader><CardTitle>Customer</CardTitle></CardHeader>
        <CardContent>
          <div className="space-y-3">
            <div className="relative">
              <Label>Search Customer / Business Partner</Label>
              <Input
                placeholder="Type to search by name or code..."
                value={customerSearch}
                onChange={(e) => searchCustomers(e.target.value)}
              />
              {customerResults.length > 0 && (
                <div className="absolute z-50 w-full mt-1 bg-white border rounded-md shadow-lg max-h-48 overflow-y-auto">
                  {customerResults.map((c: any) => (
                    <button
                      key={c.id}
                      className="w-full text-left px-4 py-2 hover:bg-gray-100 border-b last:border-0"
                      onClick={() => selectCustomer(c)}
                    >
                      <p className="font-medium">{c.companyName || c.name}</p>
                      <p className="text-xs text-gray-500">{c.partnerCode || c.code} • {c.email || ''}</p>
                    </button>
                  ))}
                </div>
              )}
            </div>
            {selectedCustomer && (
              <div className="p-3 bg-blue-50 border border-blue-200 rounded-lg">
                <p className="font-medium">{selectedCustomer.companyName || selectedCustomer.name}</p>
                <p className="text-sm text-gray-600">{selectedCustomer.partnerCode || selectedCustomer.code}</p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Order Details */}
      <Card>
        <CardHeader><CardTitle>Order Details</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <Label>Order Type</Label>
              <Select value={orderType} onValueChange={setOrderType}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Standard">Standard</SelectItem>
                  <SelectItem value="Rush">Rush</SelectItem>
                  <SelectItem value="Blanket">Blanket</SelectItem>
                  <SelectItem value="Return">Return</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Priority</Label>
              <Select value={priority} onValueChange={setPriority}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Normal">Normal</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Urgent">Urgent</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Expected Delivery Date</Label>
              <Input type="date" value={expectedDeliveryDate} onChange={(e) => setExpectedDeliveryDate(e.target.value)} />
            </div>
            <div>
              <Label>Payment Terms</Label>
              <Input placeholder="e.g., Net 30" value={paymentTerms} onChange={(e) => setPaymentTerms(e.target.value)} />
            </div>
            <div>
              <Label>Customer PO #</Label>
              <Input placeholder="Customer PO reference" value={customerPoNumber} onChange={(e) => setCustomerPoNumber(e.target.value)} />
            </div>
            <div>
              <Label>Property Reference</Label>
              <Input placeholder="Property reference (TDC)" value={propertyReference} onChange={(e) => setPropertyReference(e.target.value)} />
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
            <div>
              <Label>Shipping Address</Label>
              <Textarea placeholder="Shipping address" value={shippingAddress} onChange={(e) => setShippingAddress(e.target.value)} rows={3} />
            </div>
            <div>
              <Label>Billing Address</Label>
              <Textarea placeholder="Billing address" value={billingAddress} onChange={(e) => setBillingAddress(e.target.value)} rows={3} />
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
            <div>
              <Label>Notes</Label>
              <Textarea placeholder="Notes visible to customer" value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} />
            </div>
            <div>
              <Label>Internal Notes</Label>
              <Textarea placeholder="Internal notes (not visible to customer)" value={internalNotes} onChange={(e) => setInternalNotes(e.target.value)} rows={2} />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Order Lines */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Order Lines</CardTitle>
            <Button variant="outline" size="sm" onClick={addLine}><Plus className="h-4 w-4 mr-2" />Add Line</Button>
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Item Name</TableHead>
                <TableHead>Item Code</TableHead>
                <TableHead className="w-24">Qty</TableHead>
                <TableHead className="w-20">UoM</TableHead>
                <TableHead className="w-28">Unit Price</TableHead>
                <TableHead className="w-20">Disc %</TableHead>
                <TableHead className="w-20">Tax %</TableHead>
                <TableHead className="text-right w-28">Total</TableHead>
                <TableHead className="w-10"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {lines.map((line) => (
                <TableRow key={line.key}>
                  <TableCell>
                    <Input placeholder="Item name" value={line.itemName} onChange={(e) => updateLine(line.key, 'itemName', e.target.value)} />
                  </TableCell>
                  <TableCell>
                    <Input placeholder="Code" value={line.itemCode || ''} onChange={(e) => updateLine(line.key, 'itemCode', e.target.value)} />
                  </TableCell>
                  <TableCell>
                    <Input type="number" min={1} value={line.quantity} onChange={(e) => updateLine(line.key, 'quantity', parseFloat(e.target.value) || 0)} />
                  </TableCell>
                  <TableCell>
                    <Input placeholder="EA" value={line.unitOfMeasure || ''} onChange={(e) => updateLine(line.key, 'unitOfMeasure', e.target.value)} />
                  </TableCell>
                  <TableCell>
                    <Input type="number" min={0} step={0.01} value={line.unitPrice} onChange={(e) => updateLine(line.key, 'unitPrice', parseFloat(e.target.value) || 0)} />
                  </TableCell>
                  <TableCell>
                    <Input type="number" min={0} max={100} value={line.discountPercent || 0} onChange={(e) => updateLine(line.key, 'discountPercent', parseFloat(e.target.value) || 0)} />
                  </TableCell>
                  <TableCell>
                    <Input type="number" min={0} max={100} value={line.taxPercent || 0} onChange={(e) => updateLine(line.key, 'taxPercent', parseFloat(e.target.value) || 0)} />
                  </TableCell>
                  <TableCell className="text-right font-semibold">
                    GHS {calcLineTotal(line).toLocaleString(undefined, { minimumFractionDigits: 2 })}
                  </TableCell>
                  <TableCell>
                    <Button variant="ghost" size="icon" onClick={() => removeLine(line.key)}>
                      <Trash2 className="h-4 w-4 text-red-500" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Separator className="my-4" />
          <div className="flex justify-end">
            <div className="w-64 space-y-2">
              <div className="flex justify-between text-sm"><span className="text-gray-500">Subtotal</span><span>GHS {subtotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span></div>
              <div className="flex justify-between text-sm"><span className="text-gray-500">Discount</span><span className="text-red-500">-GHS {totalDiscount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span></div>
              <div className="flex justify-between text-sm"><span className="text-gray-500">Tax</span><span>GHS {totalTax.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span></div>
              <Separator />
              <div className="flex justify-between font-bold text-lg"><span>Total</span><span className="text-blue-600">GHS {grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span></div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
