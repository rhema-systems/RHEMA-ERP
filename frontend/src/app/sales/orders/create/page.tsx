'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Separator } from '@/components/ui/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Popover, PopoverAnchor, PopoverContent } from '@/components/ui/popover';
import { Loader2, ShoppingCart, ArrowLeft, Plus, Trash2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type CreateSalesOrderDto, type CreateSalesOrderLineDto } from '@/services/salesOrderService';
import { salesAllocationService } from '@/services/salesAllocationService';
import { projectService } from '@/services/projectService';
import { apiService } from '@/services/api.service';
import {
  parseSaleableSourceContextFromParams,
  saleableItemToContext,
  SaleableSourceQuickStart,
  type SalesLinkedSourceContext,
} from '../../components/SaleableSourceQuickStart';
import {
  salesSetupService,
  type SalesSaleableItemDto,
  type SalesSaleableSourceDto,
} from '@/services/salesSetupService';

interface LineItem extends CreateSalesOrderLineDto {
  key: string;
}

interface CrmHandoffContext {
  contextLabel?: string;
  quoteId?: string;
  quoteName?: string;
  opportunityId?: string;
  opportunityName?: string;
  leadId?: string;
  leadName?: string;
  currency?: string;
  estimatedValue?: number;
}

const buildCrmReferenceText = (context: CrmHandoffContext | null) => {
  if (!context) {
    return '';
  }

  return [
    context.contextLabel ? `CRM Context: ${context.contextLabel}` : undefined,
    context.quoteName ? `Quote: ${context.quoteName}` : undefined,
    context.opportunityName ? `Opportunity: ${context.opportunityName}` : undefined,
    context.leadName ? `Lead: ${context.leadName}` : undefined,
  ].filter(Boolean).join(' | ');
};

export default function CreateSalesOrderPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [linkedSourceContext, setLinkedSourceContext] = useState<SalesLinkedSourceContext | null>(null);
  const [lineSearchSource, setLineSearchSource] = useState<SalesSaleableSourceDto | null>(null);
  const [crmHandoffContext, setCrmHandoffContext] = useState<CrmHandoffContext | null>(null);

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
  const [currency, setCurrency] = useState('GHS');
  const [notes, setNotes] = useState('');
  const [internalNotes, setInternalNotes] = useState('');

  // Order lines
  const [lines, setLines] = useState<LineItem[]>([
    { key: crypto.randomUUID(), itemName: '', quantity: 1, unitPrice: 0, discountPercent: 0, taxPercent: 0 },
  ]);
  const [activeLinePicker, setActiveLinePicker] = useState<string | null>(null);
  const [lineItemSearch, setLineItemSearch] = useState<Record<string, string>>({});
  const [lineItemResults, setLineItemResults] = useState<Record<string, SalesSaleableItemDto[]>>({});
  const [lineItemLoading, setLineItemLoading] = useState<Record<string, boolean>>({});

  const activeLineSearchSourceId = linkedSourceContext?.sourceId || lineSearchSource?.id;
  const activeLineSearchSourceName =
    linkedSourceContext?.sourceDisplayName
    || lineSearchSource?.displayName
    || linkedSourceContext?.sourceCode
    || lineSearchSource?.code;

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const customerId = params.get('customerId');
    const customerName = params.get('customerName');
    const propertyReferenceParam = params.get('propertyReference');
    const orderTypeParam = params.get('orderType');
    const sourceContext = parseSaleableSourceContextFromParams(params);
    const crmContext: CrmHandoffContext = {
      contextLabel: params.get('crmContext') || undefined,
      quoteId: params.get('quoteId') || undefined,
      quoteName: params.get('quoteName') || undefined,
      opportunityId: params.get('opportunityId') || undefined,
      opportunityName: params.get('opportunityName') || undefined,
      leadId: params.get('leadId') || undefined,
      leadName: params.get('leadName') || undefined,
      currency: params.get('currency') || undefined,
      estimatedValue: params.get('estimatedValue') ? Number(params.get('estimatedValue')) : undefined,
    };
    const hasCrmContext = Object.values(crmContext).some((value) => value !== undefined && value !== null && value !== '');

    if (sourceContext) {
      setLinkedSourceContext(sourceContext);
      if (sourceContext.itemName || sourceContext.projectUnitName) {
        setLines((current) => {
          const first = current[0];
          const itemName = sourceContext.itemName || sourceContext.projectUnitName || '';
          const itemCode = sourceContext.itemCode || sourceContext.projectUnitCode || undefined;
          const updatedFirst: LineItem = {
            ...first,
            itemName,
            itemCode,
            productCode: itemCode,
            description: sourceContext.propertyReference ? `${itemName} - ${sourceContext.propertyReference}` : itemName,
            inventoryItemId: sourceContext.inventoryItemId,
            warehouseId: sourceContext.warehouseId,
            locationId: sourceContext.locationId,
            quantity: 1,
            unitPrice: sourceContext.estimatedValue ?? first.unitPrice,
            unitOfMeasure: sourceContext.unitOfMeasure || (sourceContext.areaSquareMeters ? 'Unit' : (first.unitOfMeasure || 'EA')),
            unit: sourceContext.unitOfMeasure || first.unit,
          };

          return current.map((line, index) => index === 0 ? { ...updatedFirst, key: line.key } : line);
        });
      }
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

    if (sourceContext?.currency) {
      // Currency is accepted by the backend create DTO even though it is not edited directly on this compact form.
      setCurrency(sourceContext.currency);
    }

    if (hasCrmContext) {
      setCrmHandoffContext(crmContext);
      if (crmContext.currency && !sourceContext?.currency) {
        setCurrency(crmContext.currency);
      }
      setInternalNotes((current) => current || buildCrmReferenceText(crmContext));
    }
  }, []);

  const applySaleableItem = (item: SalesSaleableItemDto, source: SalesSaleableSourceDto) => {
    const context = saleableItemToContext(item, source);
    setLinkedSourceContext(context);
    setLineSearchSource(source);
    setCurrency(item.currency || source.defaultCurrency || 'GHS');
    setBusinessPartnerId(item.customerId || '');
    setCustomerSearch(item.customerName || '');
    setSelectedCustomer(item.customerId && item.customerName
      ? { id: item.customerId, companyName: item.customerName, name: item.customerName }
      : null);
    setPropertyReference(item.propertyReference || '');
    setPropertyType(item.itemType || '');
    setOrderType(item.suggestedOrderType || 'PropertySale');
    setLines((current) => {
      const first = current[0];
      const shouldReplaceFirst = current.length === 1
        && !first.itemName
        && !first.description
        && first.unitPrice === 0;

      const updatedFirst: LineItem = {
        ...(shouldReplaceFirst ? first : current[0]),
        description: item.propertyReference ? `${item.itemName} - ${item.propertyReference}` : item.itemName,
        inventoryItemId: item.inventoryItemId || first.inventoryItemId,
        warehouseId: item.warehouseId || first.warehouseId,
        locationId: item.locationId || first.locationId,
        itemCode: item.itemCode || first.itemCode,
        productCode: item.itemCode || first.productCode,
        itemName: item.itemName,
        quantity: 1,
        unitPrice: item.estimatedValue ?? first.unitPrice,
        unitOfMeasure: item.unitOfMeasure || (item.areaSquareMeters ? 'Unit' : (first.unitOfMeasure || 'Lot')),
        unit: item.unitOfMeasure || first.unit,
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

  const updateLineFields = (key: string, changes: Partial<LineItem>) => {
    setLines((current) => current.map((line) => line.key === key ? { ...line, ...changes } : line));
  };

  const searchSaleableLineItems = useCallback(async (lineKey: string, query: string) => {
    if (!activeLineSearchSourceId) {
      return;
    }

    try {
      setLineItemLoading((current) => ({ ...current, [lineKey]: true }));
      const results = await salesSetupService.searchSaleableItems(
        activeLineSearchSourceId,
        query.trim() || undefined,
        25,
      );
      setLineItemResults((current) => ({
        ...current,
        [lineKey]: results.filter((item) => item.canCreateSalesOrder),
      }));
    } catch {
      setLineItemResults((current) => ({ ...current, [lineKey]: [] }));
    } finally {
      setLineItemLoading((current) => ({ ...current, [lineKey]: false }));
    }
  }, [activeLineSearchSourceId]);

  useEffect(() => {
    if (!activeLinePicker || !activeLineSearchSourceId) {
      return;
    }

    const query = lineItemSearch[activeLinePicker] ?? lines.find((line) => line.key === activeLinePicker)?.itemName ?? '';
    const timer = window.setTimeout(() => {
      void searchSaleableLineItems(activeLinePicker, query);
    }, 250);

    return () => window.clearTimeout(timer);
  }, [activeLinePicker, activeLineSearchSourceId, lineItemSearch, lines, searchSaleableLineItems]);

  const handleLineItemSearchChange = (line: LineItem, value: string) => {
    setActiveLinePicker(line.key);
    setLineItemSearch((current) => ({ ...current, [line.key]: value }));
    updateLineFields(line.key, {
      itemName: value,
      description: value || undefined,
      itemId: undefined,
      productId: undefined,
      inventoryItemId: undefined,
      warehouseId: undefined,
      locationId: undefined,
      itemCode: undefined,
      productCode: undefined,
    });
  };

  const selectSaleableLineItem = (lineKey: string, item: SalesSaleableItemDto) => {
    updateLineFields(lineKey, {
      itemName: item.itemName,
      description: item.propertyReference ? `${item.itemName} - ${item.propertyReference}` : item.itemName,
      inventoryItemId: item.inventoryItemId || undefined,
      warehouseId: item.warehouseId || undefined,
      locationId: item.locationId || undefined,
      itemCode: item.itemCode || undefined,
      productCode: item.itemCode || undefined,
      quantity: 1,
      unitPrice: item.estimatedValue ?? 0,
      unitOfMeasure: item.unitOfMeasure || (item.areaSquareMeters ? 'Unit' : 'EA'),
      unit: item.unitOfMeasure || undefined,
    });
    if (item.currency) {
      setCurrency(item.currency);
    }
    setLineItemSearch((current) => ({ ...current, [lineKey]: item.itemName }));
    setLineItemResults((current) => ({ ...current, [lineKey]: [] }));
    setActiveLinePicker(null);
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
      // Preserve currency in the Sales payload so downstream Finance posting can use the correct document currency.
      currency,
      customerPoNumber: customerPoNumber || undefined,
      propertyReference: propertyReference || undefined,
      propertyType: propertyType || undefined,
      shippingAddress: shippingAddress || undefined,
      billingAddress: billingAddress || undefined,
      notes: notes || undefined,
      internalNotes: internalNotes || undefined,
      quoteId: crmHandoffContext?.quoteId,
      opportunityId: crmHandoffContext?.opportunityId,
      lines: lines.map(l => ({
        itemName: l.itemName,
        itemCode: l.itemCode || undefined,
        itemId: l.itemId || undefined,
        productId: l.productId || undefined,
        inventoryItemId: l.inventoryItemId || undefined,
        productCode: l.productCode || l.itemCode || undefined,
        warehouseId: l.warehouseId || undefined,
        locationId: l.locationId || undefined,
        description: l.description || undefined,
        quantity: l.quantity,
        unitOfMeasure: l.unitOfMeasure || 'EA',
        unit: l.unit || l.unitOfMeasure || 'EA',
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent || 0,
        taxPercent: l.taxPercent || 0,
      })),
    };

    try {
      setSaving(true);
      if (linkedSourceContext?.sourceId && linkedSourceContext.sourceItemId && linkedSourceContext.shouldCreateSalesAllocation !== false) {
        const activeCheck = await salesAllocationService.hasActiveAllocation(
          linkedSourceContext.sourceId,
          linkedSourceContext.sourceItemId,
        );
        if (activeCheck.hasActiveAllocation) {
          toast.error('This saleable item already has an active reservation or allocation.');
          return;
        }
      }

      const result = await salesOrderService.createSalesOrder(dto);
      if (linkedSourceContext?.projectUnitId) {
        try {
          await projectService.linkSalesOrderToProjectUnit(linkedSourceContext.projectUnitId, result.id);
        } catch (linkError: any) {
          toast.warning(linkError?.message || 'Sales order created, but the project unit could not be linked automatically.');
        }
      }
      if (linkedSourceContext?.sourceId && linkedSourceContext.sourceItemId && linkedSourceContext.shouldCreateSalesAllocation !== false) {
        try {
          await salesAllocationService.createAllocation({
            saleableSourceId: linkedSourceContext.sourceId,
            sourceItemId: linkedSourceContext.sourceItemId,
            sourceItemCode: linkedSourceContext.itemCode || linkedSourceContext.projectUnitCode,
            sourceItemName: linkedSourceContext.itemName || linkedSourceContext.projectUnitName || propertyReference || 'Saleable item',
            sourceItemType: linkedSourceContext.itemType || propertyType || undefined,
            businessPartnerId,
            customerName: selectedCustomer?.companyName || selectedCustomer?.name || customerSearch || linkedSourceContext.customerName,
            salesOrderId: result.id,
            allocationType: orderType === 'Lease' ? 'Lease' : 'Reservation',
            status: 'Reserved',
            estimatedValue: linkedSourceContext.estimatedValue,
            agreedValue: result.totalAmount || grandTotal,
            currency,
            notes: `Reserved from Sales Order ${result.orderNumber || result.id}`,
          });
        } catch (allocationError: any) {
          toast.warning(allocationError?.message || 'Sales order created, but the saleable item reservation could not be recorded.');
        }
      }
      toast.success(`Sales Order ${result.orderNumber || result.id} created`);
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

      <SaleableSourceQuickStart
        mode="order"
        linkedContext={linkedSourceContext}
        onSourceSelected={setLineSearchSource}
        onUseOrder={applySaleableItem}
      />

      {crmHandoffContext ? (
        <Card className="border-blue-200 bg-blue-50/60">
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-3 text-sm">
            <div>
              <div className="font-medium text-blue-900">Started from CRM</div>
              <div className="text-blue-700">
                {buildCrmReferenceText(crmHandoffContext) || 'CRM context will be carried into this order.'}
              </div>
            </div>
            {crmHandoffContext.estimatedValue ? (
              <div className="font-medium text-blue-900">
                {(crmHandoffContext.currency || currency)} {crmHandoffContext.estimatedValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </div>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

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

      <Tabs defaultValue="details" className="space-y-4">
        <TabsList className="grid w-full max-w-md grid-cols-2">
          <TabsTrigger value="details">Order Details</TabsTrigger>
          <TabsTrigger value="lines">Order Lines</TabsTrigger>
        </TabsList>

        <TabsContent value="details" className="mt-0">
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
        </TabsContent>

        <TabsContent value="lines" className="mt-0 min-h-[420px]">
          {/* Order Lines */}
          <Card className="overflow-visible">
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle>Order Lines</CardTitle>
                <Button variant="outline" size="sm" onClick={addLine}><Plus className="h-4 w-4 mr-2" />Add Line</Button>
              </div>
            </CardHeader>
            <CardContent className="overflow-visible pb-10">
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
                    <Popover
                      open={Boolean(activeLineSearchSourceId && activeLinePicker === line.key)}
                      onOpenChange={(open) => {
                        if (!open && activeLinePicker === line.key) {
                          setActiveLinePicker(null);
                        }
                      }}
                    >
                      <PopoverAnchor asChild>
                        <Input
                          className="min-w-64"
                          placeholder={activeLineSearchSourceId
                            ? `Search ${activeLineSearchSourceName || 'source'} items`
                            : 'Item name'}
                          value={activeLinePicker === line.key ? (lineItemSearch[line.key] ?? line.itemName) : line.itemName}
                          onFocus={() => {
                            setActiveLinePicker(line.key);
                            setLineItemSearch((current) => ({ ...current, [line.key]: current[line.key] ?? line.itemName }));
                            if (activeLineSearchSourceId) {
                              void searchSaleableLineItems(line.key, line.itemName);
                            }
                          }}
                          onChange={(e) => {
                            if (activeLineSearchSourceId) {
                              handleLineItemSearchChange(line, e.target.value);
                            } else {
                              updateLine(line.key, 'itemName', e.target.value);
                            }
                          }}
                        />
                      </PopoverAnchor>
                      {activeLineSearchSourceId ? (
                        <PopoverContent
                          align="start"
                          side="bottom"
                          sideOffset={6}
                          className="z-[80] max-h-80 w-[var(--radix-popper-anchor-width)] min-w-80 overflow-y-auto p-0"
                          onOpenAutoFocus={(event) => event.preventDefault()}
                        >
                          {lineItemLoading[line.key] ? (
                            <div className="flex items-center gap-2 px-3 py-3 text-sm text-muted-foreground">
                              <Loader2 className="h-4 w-4 animate-spin" />
                              Searching items...
                            </div>
                          ) : (lineItemResults[line.key] || []).length > 0 ? (
                            <div className="divide-y">
                              {lineItemResults[line.key].map((item) => (
                                <button
                                  key={item.sourceItemId}
                                  type="button"
                                  className="w-full px-3 py-2 text-left text-sm hover:bg-muted"
                                  onMouseDown={(event) => event.preventDefault()}
                                  onClick={() => selectSaleableLineItem(line.key, item)}
                                >
                                  <div className="font-medium">{item.itemName}</div>
                                  <div className="mt-0.5 text-xs text-muted-foreground">
                                    {[item.itemCode, item.warehouseName, item.locationName]
                                      .filter(Boolean)
                                      .join(' - ')}
                                  </div>
                                  <div className="mt-1 flex flex-wrap gap-1 text-xs text-muted-foreground">
                                    {item.availableQuantity !== undefined ? (
                                      <span>
                                        Available: {item.availableQuantity.toLocaleString()} {item.unitOfMeasure || ''}
                                      </span>
                                    ) : null}
                                    {item.estimatedValue !== undefined ? (
                                      <span>
                                        {item.currency || currency} {item.estimatedValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                                      </span>
                                    ) : null}
                                  </div>
                                </button>
                              ))}
                            </div>
                          ) : (
                            <div className="px-3 py-3 text-sm text-muted-foreground">
                              No source items found.
                            </div>
                          )}
                        </PopoverContent>
                      ) : null}
                    </Popover>
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
        </TabsContent>
      </Tabs>
    </div>
  );
}
