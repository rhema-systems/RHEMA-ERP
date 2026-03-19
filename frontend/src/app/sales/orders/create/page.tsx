'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Separator } from '@/components/ui/separator';
import { ShoppingCart, ArrowLeft, Plus, Trash2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type CreateSalesOrderDto, type CreateSalesOrderLineDto } from '@/services/salesOrderService';
import { apiService } from '@/services/api.service';

interface LineItem extends CreateSalesOrderLineDto {
  key: string;
}

export default function CreateSalesOrderPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);

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
