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
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandList } from '@/components/ui/command';
import { ShoppingCart, ArrowLeft, Plus, Trash2, Save, Building2, ChevronsUpDown, Check, Percent, DollarSign } from 'lucide-react';
import { toast } from 'sonner';
import { salesOrderService, type CreateSalesOrderDto, type CreateSalesOrderLineDto } from '@/services/salesOrderService';
import { projectService, type ProjectReleasedUnitSalesLookupDto } from '@/services/projectService';
import { arService } from '@/services/ar-service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { financeService } from '@/services/finance.service';
import { cn } from '@/lib/utils';

interface LineItem {
  key: string;
  itemId?: string;
  itemCode?: string;
  itemName: string;
  description?: string;
  quantity: number;
  unitOfMeasure?: string;
  unitPrice: number;
  discountPercentage: number;
  taxGroupId?: string;
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

  // Form states
  const [businessPartnerId, setBusinessPartnerId] = useState('');
  const [customerSearch, setCustomerSearch] = useState('');
  const [selectedCustomer, setSelectedCustomer] = useState<any>(null);
  const [customerComboOpen, setCustomerComboOpen] = useState(false);
  const [customers, setCustomers] = useState<any[]>([]);
  const [taxGroups, setTaxGroups] = useState<any[]>([]);
  const [currency, setCurrency] = useState('GHS');
  const [exchangeRate, setExchangeRate] = useState(1.0);
  const [taxGroupId, setTaxGroupId] = useState('');

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
    { key: crypto.randomUUID(), itemName: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxGroupId: '' },
  ]);

  useEffect(() => {
    loadCustomers();
    loadTaxGroups();
  }, []);

  useEffect(() => {
    if (typeof window === 'undefined') return;

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
      setSelectedCustomer({ id: customerId, customerName: customerName, companyName: customerName });
    }

    if (propertyReferenceParam) {
      setPropertyReference(propertyReferenceParam);
    }

    if (orderTypeParam) {
      setOrderType(orderTypeParam);
    }
  }, []);

  useEffect(() => {
    if (linkedProjectContext) return;

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

  const loadCustomers = async () => {
    try {
      const result = await arService.getCustomers({ pageSize: 100 });
      setCustomers(result.items || []);
    } catch (error) {
      console.error('Failed to load customers:', error);
    }
  };

  const loadTaxGroups = async () => {
    try {
      const groups = await taxDataService.getTaxGroups({ isActive: true, applicability: 'Sales' });
      setTaxGroups(groups || []);
    } catch (error) {
      console.error('Failed to load tax groups:', error);
    }
  };

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
      ? { id: unit.customerBusinessPartnerId, customerName: unit.customerBusinessPartnerName, companyName: unit.customerBusinessPartnerName }
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
        discountPercentage: first.discountPercentage || 0,
      };

      if (shouldReplaceFirst) {
        return [{ ...updatedFirst, key: first.key }];
      }

      return current.map((line, index) => index === 0 ? { ...line, ...updatedFirst, key: line.key } : line);
    });
  };

  const selectCustomer = async (customer: any) => {
    setBusinessPartnerId(customer.id);
    setSelectedCustomer(customer);
    setCustomerSearch(customer.customerName || customer.companyName || customer.name || '');
    setCustomerComboOpen(false);
    if (customer.billingAddress) setBillingAddress(customer.billingAddress);
    if (customer.shippingAddress) setShippingAddress(customer.shippingAddress);
    if (customer.paymentTerms) setPaymentTerms(customer.paymentTerms);

    const custCurrency = customer.currencyCode || 'GHS';
    setCurrency(custCurrency);
    const isForeign = custCurrency !== 'GHS';
    setExchangeRate(isForeign ? exchangeRate : 1.0);

    if (isForeign) {
      try {
        const rateObj = await financeService.getCurrentExchangeRate(custCurrency);
        const rawRate = rateObj?.rate || rateObj?.currentExchangeRate || 1.0;
        const finalRate = rawRate < 1 ? Number((1 / rawRate).toFixed(4)) : rawRate;
        setExchangeRate(finalRate);
      } catch (err) {
        console.error("Failed to fetch exchange rate for customer currency", err);
      }
    }
  };

  const addLine = () => {
    setLines([...lines, { key: crypto.randomUUID(), itemName: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxGroupId: '' }]);
  };

  const removeLine = (key: string) => {
    if (lines.length <= 1) { toast.error('At least one line item is required'); return; }
    setLines(lines.filter(l => l.key !== key));
  };

  const updateLine = (key: string, field: keyof LineItem, value: any) => {
    setLines(lines.map(l => l.key === key ? { ...l, [field]: value } : l));
  };

  const getTotals = () => {
    let subtotal = 0;
    let totalDiscount = 0;
    let totalTaxAmount = 0;
    const breakdowns: { [taxCode: string]: { name: string; rate: number; amount: number } } = {};

    lines.forEach((item) => {
      const qty = Number(item.quantity) || 0;
      const price = Number(item.unitPrice) || 0;
      const discPercent = Number(item.discountPercentage) || 0;

      const gross = qty * price;
      const discount = gross * (discPercent / 100);
      const netTaxable = gross - discount;

      subtotal += gross;
      totalDiscount += discount;

      const activeGroupId = item.taxGroupId || taxGroupId;
      const activeGroup = taxGroups?.find(tg => tg.id === activeGroupId);

      if (activeGroup && activeGroup.components) {
        let cumulativeBase = netTaxable;
        const sortedComponents = [...activeGroup.components].sort((a, b) => a.calculationOrder - b.calculationOrder);

        sortedComponents.forEach(comp => {
          if (comp.taxCategory === 'Withholding') return;

          let taxableBasis = netTaxable;
          if (comp.compoundBasis === 'Cumulative') {
            taxableBasis = cumulativeBase;
          }

          const taxAmt = taxableBasis * (Number(comp.taxRate) / 100);
          totalTaxAmount += taxAmt;

          if (comp.compoundBasis === 'Cumulative' || comp.compoundBasis === 'BaseOnly') {
            cumulativeBase += taxAmt;
          }

          if (breakdowns[comp.taxCode]) {
            breakdowns[comp.taxCode].amount += taxAmt;
          } else {
            breakdowns[comp.taxCode] = {
              name: comp.taxName,
              rate: comp.taxRate,
              amount: taxAmt
            };
          }
        });
      }
    });

    const grandTotal = (subtotal - totalDiscount) + totalTaxAmount;
    const rate = Number(exchangeRate) || 1.0;
    const baseGrandTotal = grandTotal * rate;

    return {
      subtotal,
      totalDiscount,
      totalTaxAmount,
      grandTotal,
      baseGrandTotal,
      taxList: Object.entries(breakdowns).map(([code, data]) => ({ code, ...data }))
    };
  };

  const totals = getTotals();

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
      propertyType: propertyType ? (propertyType as any) : undefined,
      shippingAddress: shippingAddress || undefined,
      billingAddress: billingAddress || undefined,
      notes: notes || undefined,
      internalNotes: internalNotes || undefined,
      currency,
      exchangeRate,
      taxGroupId: taxGroupId === 'none' ? undefined : (taxGroupId || undefined),
      lines: lines.map(l => ({
        itemName: l.itemName,
        itemCode: l.itemCode || undefined,
        itemId: l.itemId || undefined,
        description: l.description || undefined,
        quantity: l.quantity,
        unitOfMeasure: l.unitOfMeasure || 'EA',
        unitPrice: l.unitPrice,
        discountPercentage: l.discountPercentage || 0,
        taxGroupId: l.taxGroupId === 'none' ? undefined : (l.taxGroupId || undefined),
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

  const filteredCustomers = customers.filter(c => {
    if (!customerSearch) return true;
    const search = customerSearch.toLowerCase();
    return c.customerName?.toLowerCase().includes(search) || c.customerCode?.toLowerCase().includes(search);
  });

  return (
    <div className="container mx-auto py-8 px-4 space-y-8 animate-in fade-in duration-500">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-6">
        <div className="flex items-center gap-4">
          <Button variant="outline" size="icon" onClick={() => router.push('/sales/orders')} className="rounded-xl">
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-extrabold tracking-tight flex items-center gap-3 text-slate-900">
              <ShoppingCart className="h-10 w-10 text-teal-600 animate-pulse" /> Establish Sales Order
            </h1>
            <p className="text-slate-500 mt-2 text-base">Harden commercial documents with searchable customers, terms, and locked exchange rates.</p>
          </div>
        </div>
        <Button className="bg-teal-600 hover:bg-teal-700 text-white font-bold py-6 px-6 rounded-xl shadow-lg hover:shadow-teal-100 transition-all duration-300 transform hover:-translate-y-0.5" onClick={handleSave} disabled={saving}>
          <Save className="h-5 w-5 mr-2" /> {saving ? 'Securing Sales Order...' : 'Establish Order'}
        </Button>
      </div>

      {linkedProjectContext && (
        <Card className="border-teal-200 bg-teal-50/20 rounded-2xl shadow-sm">
          <CardContent className="pt-6">
            <div className="flex items-center gap-2 text-sm font-black text-teal-800">
              <Building2 className="h-4 w-4" />
              Creating Order For Project-Linked Unit
            </div>
            <div className="mt-2 text-sm text-slate-700">
              <p className="font-extrabold">
                {linkedProjectContext.projectCode}
                {linkedProjectContext.projectTitle ? ` • ${linkedProjectContext.projectTitle}` : ''}
              </p>
              <p className="font-semibold text-slate-500">
                {linkedProjectContext.projectUnitCode || linkedProjectContext.projectUnitName || 'Linked project unit'}
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {!linkedProjectContext && (
        <Card className="border-dashed border-teal-200 rounded-2xl shadow-sm bg-slate-50/20">
          <CardHeader>
            <CardTitle className="text-base font-extrabold text-slate-800">Start From Released Project Unit</CardTitle>
            <CardDescription>Pick a released unit to prefill customer and property context before drafting the order.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_220px]">
              <Input
                placeholder="Search by project code, unit, or customer..."
                value={releasedUnitsSearch}
                onChange={(event) => setReleasedUnitsSearch(event.target.value)}
                className="rounded-xl border-slate-200 bg-white"
              />
              <Select value={selectedReleasedUnitId || 'none'} onValueChange={(value) => setSelectedReleasedUnitId(value === 'none' ? '' : value)}>
                <SelectTrigger className="rounded-xl border-slate-200 bg-white"><SelectValue placeholder="Select released unit" /></SelectTrigger>
                <SelectContent className="rounded-xl font-semibold">
                  <SelectItem value="none">Select released unit</SelectItem>
                  {releasedUnits.map((unit) => (
                    <SelectItem key={unit.projectUnitId} value={unit.projectUnitId}>
                      {unit.projectCode} - {unit.projectUnitCode || unit.projectUnitName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {releasedUnitsLoading && <p className="text-xs font-semibold text-slate-400">Loading released units...</p>}
            {selectedReleasedUnit && (
              <div className="rounded-xl border bg-slate-50 p-4 text-xs text-slate-600 font-semibold space-y-1">
                <div className="font-extrabold text-slate-800">
                  {selectedReleasedUnit.projectCode} • {selectedReleasedUnit.projectUnitCode || selectedReleasedUnit.projectUnitName}
                </div>
                <div>Customer: {selectedReleasedUnit.customerBusinessPartnerName || 'No customer assigned'}</div>
                <div>Price: {selectedReleasedUnit.currency} {selectedReleasedUnit.basePrice?.toLocaleString(undefined, { minimumFractionDigits: 2 })}</div>
              </div>
            )}
            <div className="flex justify-end">
              <Button variant="outline" disabled={!selectedReleasedUnit} onClick={() => selectedReleasedUnit && applyReleasedUnit(selectedReleasedUnit)} className="rounded-xl font-bold border-slate-200">
                Use Released Unit Context
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Customer Selection */}
      <Card className="border-slate-100 rounded-2xl shadow-sm">
        <CardHeader><CardTitle className="text-lg font-extrabold text-slate-800">Customer & Currency Defaults</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="space-y-2 flex flex-col justify-end">
            <Label className="font-bold text-slate-700">Customer (Searchable Debtors) *</Label>
            <Popover open={customerComboOpen} onOpenChange={setCustomerComboOpen}>
              <PopoverTrigger asChild>
                <Button variant="outline" role="combobox" aria-expanded={customerComboOpen} className="w-full justify-between rounded-xl border-slate-200 py-6 font-semibold text-slate-700 hover:bg-slate-50 text-left bg-white">
                  {selectedCustomer ? `${selectedCustomer.customerName || selectedCustomer.companyName} (${selectedCustomer.customerCode || selectedCustomer.partnerCode || selectedCustomer.code || 'Code'})` : "Select an active customer..."}
                  <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-[380px] p-0 rounded-2xl shadow-xl border-slate-100" align="start">
                <Command shouldFilter={false}>
                  <CommandInput placeholder="Search active debtors by name..." value={customerSearch} onValueChange={setCustomerSearch} className="font-semibold text-sm" />
                  <CommandList>
                    <CommandEmpty className="py-6 text-center text-slate-500 text-sm font-medium">No debtors matching search.</CommandEmpty>
                    <CommandGroup className="p-2">
                      {filteredCustomers.map((customer) => (
                        <div key={customer.id} onClick={() => selectCustomer(customer)} className="relative flex cursor-pointer select-none items-center rounded-xl px-3 py-2 text-sm text-slate-700 outline-none hover:bg-slate-50 font-semibold transition-colors">
                          <Check className={cn("mr-2 h-4 w-4 text-teal-600", selectedCustomer?.id === customer.id ? "opacity-100" : "opacity-0")} />
                          <div className="flex flex-col">
                            <span>{customer.customerName}</span>
                            <span className="text-xs text-slate-400 font-normal">{customer.customerCode} | default: {customer.currencyCode || 'GHS'}</span>
                          </div>
                        </div>
                      ))}
                    </CommandGroup>
                  </CommandList>
                </Command>
              </PopoverContent>
            </Popover>
          </div>

          <div className="grid grid-cols-2 gap-4 bg-slate-50/50 p-4 rounded-2xl border border-slate-100">
            <div className="space-y-1">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Currency</span>
              <p className="text-lg font-black text-slate-800 mt-1">{currency || 'GHS'}</p>
            </div>
            {currency !== 'GHS' && (
              <div className="space-y-1.5 col-span-2 sm:col-span-1">
                <Label className="text-xs font-bold text-amber-600 flex items-center gap-0.5"><DollarSign className="h-3.5 w-3.5" /> Exchange Rate</Label>
                <Input type="number" step="0.0001" value={exchangeRate} onChange={(e) => setExchangeRate(Number(e.target.value) || 1.0)} className="rounded-xl border-slate-200 bg-white font-bold" />
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Order Details */}
      <Card className="border-slate-100 rounded-2xl shadow-sm">
        <CardHeader><CardTitle className="text-lg font-extrabold text-slate-800">Order Details</CardTitle></CardHeader>
        <CardContent className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Order Type</Label>
              <Select value={orderType} onValueChange={setOrderType}>
                <SelectTrigger className="rounded-xl border-slate-200 bg-white font-semibold text-slate-700 py-5"><SelectValue /></SelectTrigger>
                <SelectContent className="rounded-xl font-semibold">
                  <SelectItem value="Standard">Standard</SelectItem>
                  <SelectItem value="Rush">Rush</SelectItem>
                  <SelectItem value="Blanket">Blanket</SelectItem>
                  <SelectItem value="Return">Return</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Priority</Label>
              <Select value={priority} onValueChange={setPriority}>
                <SelectTrigger className="rounded-xl border-slate-200 bg-white font-semibold text-slate-700 py-5"><SelectValue /></SelectTrigger>
                <SelectContent className="rounded-xl font-semibold">
                  <SelectItem value="Low">Low</SelectItem>
                  <SelectItem value="Normal">Normal</SelectItem>
                  <SelectItem value="High">High</SelectItem>
                  <SelectItem value="Urgent">Urgent</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Expected Delivery Date</Label>
              <Input type="date" value={expectedDeliveryDate} onChange={(e) => setExpectedDeliveryDate(e.target.value)} className="rounded-xl border-slate-200 py-5 bg-white font-semibold text-slate-700" />
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Payment Terms</Label>
              <Input placeholder="e.g. Net 30" value={paymentTerms} onChange={(e) => setPaymentTerms(e.target.value)} className="rounded-xl border-slate-200 py-5 bg-white font-semibold" />
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Customer PO #</Label>
              <Input placeholder="Customer PO reference" value={customerPoNumber} onChange={(e) => setCustomerPoNumber(e.target.value)} className="rounded-xl border-slate-200 py-5 bg-white font-semibold" />
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Default Tax Group</Label>
              <Select value={taxGroupId || 'none'} onValueChange={(val) => setTaxGroupId(val === 'none' ? '' : val)}>
                <SelectTrigger className="rounded-xl border-slate-200 bg-white font-bold text-slate-700 py-5">
                  <SelectValue placeholder="No Tax (Zero/Exempt)" />
                </SelectTrigger>
                <SelectContent className="rounded-xl font-semibold">
                  <SelectItem value="none">No Tax (Zero/Exempt)</SelectItem>
                  {taxGroups.map((tg) => (
                    <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Property Reference</Label>
              <Input placeholder="Property reference (TDC)" value={propertyReference} onChange={(e) => setPropertyReference(e.target.value)} className="rounded-xl border-slate-200 py-5 bg-white font-semibold" />
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Property Type</Label>
              <Select value={propertyType || 'none'} onValueChange={(val) => setPropertyType(val === 'none' ? '' : val)}>
                <SelectTrigger className="rounded-xl border-slate-200 bg-white font-semibold text-slate-700 py-5"><SelectValue placeholder="Select property type..." /></SelectTrigger>
                <SelectContent className="rounded-xl font-semibold">
                  <SelectItem value="none">None</SelectItem>
                  <SelectItem value="ServicedPlot">Serviced Plot</SelectItem>
                  <SelectItem value="ResidentialProperty">Residential Property</SelectItem>
                  <SelectItem value="CommercialProperty">Commercial Property</SelectItem>
                  <SelectItem value="LightIndustrial">Light Industrial</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Shipping Address</Label>
              <Textarea placeholder="Shipping address" value={shippingAddress} onChange={(e) => setShippingAddress(e.target.value)} rows={3} className="rounded-xl border-slate-200 bg-white" />
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Billing Address</Label>
              <Textarea placeholder="Billing address" value={billingAddress} onChange={(e) => setBillingAddress(e.target.value)} rows={3} className="rounded-xl border-slate-200 bg-white" />
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Notes (Customer facing)</Label>
              <Textarea placeholder="Notes visible to customer..." value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} className="rounded-xl border-slate-200 bg-white" />
            </div>
            <div className="space-y-2">
              <Label className="font-bold text-slate-700">Internal Notes</Label>
              <Textarea placeholder="Internal staff notes..." value={internalNotes} onChange={(e) => setInternalNotes(e.target.value)} rows={2} className="rounded-xl border-slate-200 bg-white" />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Order Lines */}
      <Card className="border-slate-100 rounded-2xl shadow-sm">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-lg font-extrabold text-slate-800">Order Lines</CardTitle>
            <Button variant="outline" size="sm" onClick={addLine} className="border-teal-200 text-teal-700 hover:bg-teal-50 rounded-xl font-bold">
              <Plus className="h-4 w-4 mr-1" /> Add Order Line
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-3">
            {lines.map((line, idx) => (
              <div key={line.key} className="grid grid-cols-12 gap-3 items-end border border-slate-100 rounded-2xl p-4 bg-slate-50/30 hover:bg-slate-50/50 transition-colors">
                <div className="col-span-12 sm:col-span-4 space-y-1.5">
                  <Label className="text-xs font-bold text-slate-500">Item Name / Description *</Label>
                  <Input value={line.itemName} onChange={(e) => updateLine(line.key, 'itemName', e.target.value)} placeholder="Service or product name" className="rounded-xl border-slate-200 bg-white" />
                </div>

                <div className="col-span-4 sm:col-span-2 space-y-1.5">
                  <Label className="text-xs font-bold text-slate-500">Qty</Label>
                  <Input type="number" min={1} value={line.quantity} onChange={(e) => updateLine(line.key, 'quantity', parseFloat(e.target.value) || 0)} className="rounded-xl border-slate-200 bg-white font-bold" />
                </div>

                <div className="col-span-4 sm:col-span-2 space-y-1.5">
                  <Label className="text-xs font-bold text-slate-500">Unit Price</Label>
                  <Input type="number" min={0} step={0.01} value={line.unitPrice} onChange={(e) => updateLine(line.key, 'unitPrice', parseFloat(e.target.value) || 0)} className="rounded-xl border-slate-200 bg-white font-bold" />
                </div>

                <div className="col-span-4 sm:col-span-2 space-y-1.5">
                  <Label className="text-xs font-bold text-amber-600 flex items-center gap-0.5"><Percent className="h-3 w-3" /> Disc Allowed %</Label>
                  <Input type="number" min={0} max={100} value={line.discountPercentage} onChange={(e) => updateLine(line.key, 'discountPercentage', parseFloat(e.target.value) || 0)} className="rounded-xl border-slate-200 bg-white font-bold" />
                </div>

                <div className="col-span-6 sm:col-span-2 space-y-1.5">
                  <Label className="text-xs font-bold text-slate-500">Tax Group</Label>
                  <Select value={line.taxGroupId || 'none'} onValueChange={(val) => updateLine(line.key, 'taxGroupId', val === 'none' ? '' : val)}>
                    <SelectTrigger className="rounded-xl border-slate-200 bg-white font-semibold text-xs py-5">
                      <SelectValue placeholder="Inherit Header" />
                    </SelectTrigger>
                    <SelectContent className="rounded-xl font-semibold">
                      <SelectItem value="none">Inherit / None</SelectItem>
                      {taxGroups.map((tg) => (
                        <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="col-span-12 text-right font-extrabold text-sm text-slate-700 py-3 pr-2 border-t mt-2 flex justify-between items-center">
                  <div className="text-xs text-slate-400">
                    Gross: {currency} {(line.quantity * line.unitPrice).toLocaleString(undefined, { minimumFractionDigits: 2 })}
                  </div>
                  <div className="flex items-center gap-3">
                    <span>Line Net (Pre-tax): {currency} {((line.quantity * line.unitPrice) * (1 - (line.discountPercentage || 0)/100)).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                    {lines.length > 1 && (
                      <Button variant="ghost" size="icon" onClick={() => removeLine(line.key)} className="text-rose-500 hover:text-rose-600 hover:bg-rose-50 rounded-xl h-8 w-8">
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>

          <Separator className="my-6" />

          {/* Premium totals card matching Quotes */}
          <div className="backdrop-blur-md bg-white/40 border border-slate-200/50 p-6 rounded-2xl shadow-xl space-y-4 relative overflow-hidden">
            <div className="absolute top-0 right-0 bg-gradient-to-bl from-teal-500/10 to-transparent w-36 h-36 rounded-full blur-2xl" />
            <CardDescription className="font-extrabold text-slate-600 uppercase tracking-wider text-xs flex items-center gap-2">Estimated Sales Order Financial Summary</CardDescription>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm border-b pb-4">
              <div className="space-y-1">
                <span className="text-slate-400 font-medium">Subtotal Gross:</span>
                <p className="font-bold text-slate-700">{currency} {totals.subtotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
              </div>
              <div className="space-y-1">
                <span className="text-rose-500 font-medium">Discount Allowed:</span>
                <p className="font-bold text-rose-600">-{currency} {totals.totalDiscount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
              </div>
              <div className="space-y-1">
                <span className="text-slate-400 font-medium">Estimated Taxes:</span>
                <p className="font-bold text-slate-700">{currency} {totals.totalTaxAmount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
              </div>
              <div className="space-y-1">
                <span className="text-slate-400 font-medium">Exchange Rate:</span>
                <p className="font-bold text-slate-700">{currency} 1.00 = ₵{exchangeRate}</p>
              </div>
            </div>

            {totals.taxList.length > 0 && (
              <div className="text-xs text-slate-500 space-y-1.5 bg-slate-50/50 p-3 rounded-xl border border-slate-100">
                <span className="font-bold uppercase tracking-wider text-[10px] text-slate-400 block mb-1">Estimated Tax Breakdown:</span>
                {totals.taxList.map((tax) => (
                  <div key={tax.code} className="flex justify-between font-semibold">
                    <span>{tax.name} ({tax.rate}%):</span>
                    <span>{currency} {tax.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                  </div>
                ))}
              </div>
            )}

            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pt-2">
              <div className="space-y-0.5">
                <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Grand Total (Foreign):</span>
                <p className="text-2xl font-black text-slate-900">{currency} {totals.grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
              </div>
              {currency !== 'GHS' && (
                <div className="bg-gradient-to-r from-teal-50 to-emerald-50 border border-teal-100 p-4 rounded-xl space-y-0.5 text-right sm:min-w-[200px]">
                  <span className="text-[10px] font-black text-teal-800 uppercase tracking-wider">Equivalent Base Currency (GHS):</span>
                  <p className="text-xl font-extrabold text-teal-700">₵{totals.baseGrandTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
                </div>
              )}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
