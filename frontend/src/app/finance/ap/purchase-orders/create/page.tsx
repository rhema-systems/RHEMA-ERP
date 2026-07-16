'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { financePurchaseOrderService } from '@/services/financePurchaseOrderService';
import { businessPartnerService } from '@/services/businessPartnerService';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { financeService } from '@/services/finance.service';
import { taxDataService } from '@/services/finance/tax-data.service';
import { paymentTermService, type PaymentTermListDto } from '@/services/financeCommonService';
import { useToast } from '@/components/ui/use-toast';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Plus, Trash2, Check, ChevronsUpDown } from 'lucide-react';
import { cn } from "@/lib/utils";
import { useDocumentSequence } from '@/hooks/use-document-sequence';
import { FinanceDocumentTypes } from '@/types/document-numbering';

// Reusable Searchable Combobox Component
function SearchableSelect({ items, value, onValueChange, placeholder, displayKey, renderItem }: any) {
    const [open, setOpen] = useState(false);
    return (
        <Popover open={open} onOpenChange={setOpen}>
            <PopoverTrigger asChild>
                <Button
                    variant="outline"
                    role="combobox"
                    aria-expanded={open}
                    className="w-full justify-between font-normal text-left"
                >
                    <span className="truncate">
                        {value
                            ? items.find((item: any) => item.id === value)?.[displayKey]
                            : placeholder}
                    </span>
                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[400px] p-0" align="start">
                <Command>
                    <CommandInput placeholder={`Search ${placeholder.toLowerCase()}...`} />
                    <CommandList>
                        <CommandEmpty>No results found.</CommandEmpty>
                        <CommandGroup>
                            {items.map((item: any) => {
                                const code = item.accountCode || item.itemCode || item.partnerCode || '';
                                return (
                                    <CommandItem
                                        key={item.id}
                                        value={`${code} ${item[displayKey]}`}
                                        onSelect={() => {
                                            onValueChange(item.id);
                                            setOpen(false);
                                        }}
                                        className="flex items-center"
                                    >
                                        <Check
                                            className={cn(
                                                "mr-2 h-4 w-4 shrink-0",
                                                value === item.id ? "opacity-100" : "opacity-0"
                                            )}
                                        />
                                        {renderItem ? (
                                            renderItem(item)
                                        ) : code ? (
                                            <div className="flex flex-col py-1 overflow-hidden">
                                                <span className="font-medium text-foreground truncate">{item[displayKey]}</span>
                                                <span className="text-xs text-muted-foreground truncate">{code}</span>
                                            </div>
                                        ) : (
                                            <span className="truncate">{item[displayKey]}</span>
                                        )}
                                    </CommandItem>
                                );
                            })}
                        </CommandGroup>
                    </CommandList>
                </Command>
            </PopoverContent>
        </Popover>
    );
}

export default function CreatePurchaseOrderPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [submitting, setSubmitting] = useState(false);

    const [vendors, setVendors] = useState<any[]>([]);
    const [inventoryItems, setInventoryItems] = useState<any[]>([]);
    const [warehouses, setWarehouses] = useState<any[]>([]);
    const [glAccounts, setGlAccounts] = useState<any[]>([]);
    const [taxGroups, setTaxGroups] = useState<any[]>([]);
    const [paymentTerms, setPaymentTerms] = useState<PaymentTermListDto[]>([]);

    const [vendorId, setVendorId] = useState('');
    const [orderNumber, setOrderNumber] = useState('');
    const orderSequence = useDocumentSequence('Finance', FinanceDocumentTypes.FinancePurchaseOrder);
    const [orderDate, setOrderDate] = useState(new Date().toISOString().split('T')[0]);
    const [currencyCode, setCurrencyCode] = useState('GHS');
    const [exchangeRate, setExchangeRate] = useState<number>(1.0);
    const [exchangeRateDate, setExchangeRateDate] = useState(new Date().toISOString().split('T')[0]);
    const [exchangeRateSource, setExchangeRateSource] = useState('Daily');
    const [taxGroupId, setTaxGroupId] = useState('');
    const [paymentTermId, setPaymentTermId] = useState('');

    const [lines, setLines] = useState<any[]>([]);

    useEffect(() => {
        setExchangeRateDate(orderDate);
    }, [orderDate]);

    useEffect(() => {
        const loadData = async () => {
            try {
                const loadSupplierPaymentTerms = async () => {
                    const supplierTerms = await paymentTermService.getByApplicableTo('Supplier');
                    if (supplierTerms.length > 0) {
                        return supplierTerms;
                    }

                    const activeTerms = await paymentTermService.getActive();
                    return activeTerms.filter(term => {
                        const applicableTo = (term.applicableTo || 'All').toLowerCase();
                        return applicableTo === 'all' || applicableTo === 'supplier' || applicableTo === 'vendor';
                    });
                };

                const [v, i, w, a, tg, pt] = await Promise.all([
                    businessPartnerService.getActivePartners(),
                    inventoryManagementService.getInventoryItems(),
                    inventoryManagementService.getWarehouses(true),
                    financeService.getAllAccounts(),
                    taxDataService.getActiveTaxGroups('Purchases'),
                    loadSupplierPaymentTerms()
                ]);

                // Map, filter out Customers, and deduplicate vendors
                const mappedVendors = (v || [])
                    .filter(ven => ven.partnerType !== 'Customer')
                    .map(ven => ({
                        ...ven, 
                        displayName: (ven as any).name || ven.companyName || ven.partnerName || 'Unknown'
                    }));
                // Sort to prefer vendors that have a currency populated so they are kept during deduplication
                mappedVendors.sort((a, b) => {
                    const aHasCurr = a.currency && a.currency.trim() !== '' ? 1 : 0;
                    const bHasCurr = b.currency && b.currency.trim() !== '' ? 1 : 0;
                    return bHasCurr - aHasCurr;
                });
                // Remove duplicates by ID (in case of backend join duplication) and then by displayName to clean up UI
                const uniqueVendors = mappedVendors.filter((vendor, index, self) =>
                    index === self.findIndex((t) => t.id === vendor.id || t.displayName === vendor.displayName)
                );
                
                setVendors(uniqueVendors);
                setTaxGroups(tg || []);
                setPaymentTerms(pt || []);
                const defaultPaymentTerm = (pt || []).find(term => term.isDefault);
                if (defaultPaymentTerm) {
                    setPaymentTermId(defaultPaymentTerm.id);
                }
                
                setInventoryItems((i || []).map(item => ({
                    ...item,
                    displayName: item.name || (item as any).itemName || item.itemCode || 'Unknown'
                })));
                
                setWarehouses((w || []).map(wh => ({
                    ...wh,
                    displayName: wh.name || (wh as any).warehouseName || 'Unknown'
                })));
                
                const rawAccounts = (a as any)?.data || a || [];
                // Sort accounts by accountCode (Chart of Accounts standard)
                const sortedAccounts = [...rawAccounts].sort((x: any, y: any) => {
                    const codeX = (x.accountCode || '').toString();
                    const codeY = (y.accountCode || '').toString();
                    return codeX.localeCompare(codeY, undefined, { numeric: true, sensitivity: 'base' });
                });
                setGlAccounts(sortedAccounts.map((acc: any) => ({
                    ...acc,
                    displayName: acc.name || acc.accountName || acc.accountCode || 'Unknown'
                })));

            } catch (error) {
                console.error("Failed to load dropdown data", error);
                toast({ title: 'Warning', description: 'Could not load some dropdown data', variant: 'destructive' });
            }
        };
        loadData();
    }, []);

    const handleVendorChange = async (val: string) => {
        setVendorId(val);
        const selectedVendor = vendors.find(v => v.id === val);
        if (selectedVendor?.paymentTermId) {
            setPaymentTermId(selectedVendor.paymentTermId);
        }
        if (selectedVendor && selectedVendor.currency) {
            const currency = selectedVendor.currency;
            setCurrencyCode(currency);
            if (currency === 'GHS') {
                setExchangeRate(1.0);
                setExchangeRateSource('Daily');
            } else {
                try {
                    const rateObj = await financeService.getCurrentExchangeRate(currency);
                    const rawRate = rateObj?.rate || (rateObj as any)?.currentExchangeRate || 1.0;
                    const finalRate = rawRate < 1 ? Number((1 / rawRate).toFixed(4)) : rawRate;
                    setExchangeRate(finalRate);
                    setExchangeRateSource('Daily');
                } catch (err) {
                    console.error("Failed to fetch exchange rate for vendor currency", err);
                    setExchangeRate(1.0);
                    setExchangeRateSource('Custom');
                }
            }
        }
    };

    const handleCurrencyChange = async (val: string) => {
        setCurrencyCode(val);
        if (val === 'GHS') {
            setExchangeRate(1.0);
            setExchangeRateSource('Daily');
        } else {
            try {
                const rateObj = await financeService.getCurrentExchangeRate(val);
                const rawRate = rateObj?.rate || (rateObj as any)?.currentExchangeRate || 1.0;
                const finalRate = rawRate < 1 ? Number((1 / rawRate).toFixed(4)) : rawRate;
                setExchangeRate(finalRate);
                setExchangeRateSource('Daily');
            } catch (err) {
                console.error("Failed to fetch exchange rate for currency", err);
                setExchangeRate(1.0);
                setExchangeRateSource('Custom');
            }
        }
    };

    const handleAddLine = (lineType: number) => {
        setLines([...lines, {
            id: Math.random().toString(),
            lineType,
            inventoryItemId: '',
            glAccountId: '',
            warehouseId: '',
            description: '',
            orderedQuantity: 1,
            unitPrice: 0,
            lineTotal: 0,
            taxGroupId: '' // Inherit
        }]);
    };

    const handleRemoveLine = (index: number) => {
        setLines(lines.filter((_, i) => i !== index));
    };

    const handleLineChange = (index: number, field: string, value: any) => {
        const newLines = [...lines];
        newLines[index][field] = value;
        
        if (field === 'orderedQuantity' || field === 'unitPrice') {
            newLines[index].lineTotal = Number(newLines[index].orderedQuantity) * Number(newLines[index].unitPrice);
        }
        
        // Auto-fill description if item/account selected
        if (field === 'inventoryItemId') {
            const item = inventoryItems.find(x => x.id === value);
            if (item) newLines[index].description = item.displayName;
        }
        if (field === 'glAccountId') {
            const acc = glAccounts.find(x => x.id === value);
            if (acc) newLines[index].description = acc.displayName;
        }

        setLines(newLines);
    };

    // Calculate interactive tax previews
    const totalAmount = lines.reduce((acc, line) => acc + (line.lineTotal || 0), 0);

    const getTaxBreakdown = () => {
        let totalTaxAmount = 0;
        const breakdowns: { [taxCode: string]: { name: string; rate: number; amount: number } } = {};

        lines.forEach(line => {
            const lineSubtotal = Number(line.lineTotal) || 0;
            const activeGroupId = line.taxGroupId || taxGroupId;
            const activeGroup = taxGroups.find(tg => tg.id === activeGroupId);
            
            if (activeGroup && activeGroup.components) {
                let cumulativeBase = lineSubtotal;
                const sortedComponents = [...activeGroup.components].sort((a, b) => a.calculationOrder - b.calculationOrder);
                
                sortedComponents.forEach(comp => {
                    if (comp.taxCategory === 'Withholding') return; // WHT is separate
                    
                    let taxableBasis = lineSubtotal;
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

        return {
            totalTaxAmount,
            taxList: Object.entries(breakdowns).map(([code, data]) => ({ code, ...data }))
        };
    };

    const taxEstimate = getTaxBreakdown();
    const totalEstimatedTax = taxEstimate.totalTaxAmount;
    const grandTotal = totalAmount + totalEstimatedTax;

    const handleCreate = async () => {
        if (!vendorId) {
            toast({ title: 'Validation Error', description: 'Vendor is required', variant: 'destructive' });
            return;
        }
        if (lines.length === 0) {
            toast({ title: 'Validation Error', description: 'At least one line item is required', variant: 'destructive' });
            return;
        }

        setSubmitting(true);
        try {
            const newPo = {
                vendorId: vendorId,
                orderNumber: orderSequence.allowManualEntry && orderNumber.trim() ? orderNumber.trim() : undefined,
                orderDate: new Date(orderDate).toISOString(),
                paymentTermId: paymentTermId || null,
                currencyCode: currencyCode,
                exchangeRate: Number(exchangeRate) || 1.0,
                taxGroupId: taxGroupId === 'none' ? null : (taxGroupId || null),
                totalAmount: grandTotal,
                items: lines.map(l => ({
                    lineType: l.lineType,
                    inventoryItemId: (l.lineType === 1 && l.inventoryItemId) ? l.inventoryItemId : null,
                    glAccountId: (l.lineType === 2 && l.glAccountId) ? l.glAccountId : null,
                    warehouseId: (l.lineType === 1 && l.warehouseId) ? l.warehouseId : null,
                    description: l.description,
                    orderedQuantity: Number(l.orderedQuantity),
                    unitPrice: Number(l.unitPrice),
                    lineTotal: Number(l.lineTotal),
                    taxGroupId: l.taxGroupId === 'none' ? null : (l.taxGroupId || null)
                }))
            };
            const result = await financePurchaseOrderService.createPurchaseOrder(newPo);
            toast({ title: 'Success', description: 'PO Created successfully' });
            router.push(`/finance/ap/purchase-orders/${result.id}`);
        } catch (error: any) {
            toast({ title: 'Error', description: error.message || 'Failed to create PO', variant: 'destructive' });
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="space-y-6 p-8 max-w-[1600px] mx-auto">
            <div className="flex justify-between items-center">
                <h1 className="text-3xl font-bold tracking-tight">Create Finance PO</h1>
                <Button variant="outline" onClick={() => router.back()}>Back</Button>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Purchase Order Header</CardTitle>
                </CardHeader>
                <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Order Number</label>
                        <Input
                            value={orderSequence.allowManualEntry ? orderNumber : orderSequence.sampleNumber}
                            onChange={(e) => setOrderNumber(e.target.value)}
                            disabled={!orderSequence.allowManualEntry || orderSequence.loading}
                            placeholder={orderSequence.allowManualEntry ? `Auto: ${orderSequence.sampleNumber}` : undefined}
                            className="font-mono"
                        />
                        <p className="text-xs text-muted-foreground">Assigned by the configured Finance Purchase Order sequence when saved.</p>
                    </div>
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Order Date</label>
                        <Input type="date" value={orderDate} onChange={(e) => setOrderDate(e.target.value)} />
                    </div>
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Vendor / Supplier</label>
                        <SearchableSelect 
                            items={vendors} 
                            value={vendorId} 
                            onValueChange={handleVendorChange} 
                            placeholder="Select Vendor"
                            displayKey="displayName"
                        />
                    </div>
                    <div className="space-y-2">
                        <label className="text-sm font-medium">Payment Terms</label>
                        <Select value={paymentTermId || 'none'} onValueChange={(value) => setPaymentTermId(value === 'none' ? '' : value)}>
                            <SelectTrigger>
                                <SelectValue placeholder="Select Payment Terms" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="none">Vendor Default / None</SelectItem>
                                {paymentTerms.map(term => (
                                    <SelectItem key={term.id} value={term.id}>
                                        {term.name} ({term.code})
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                        <p className="text-xs text-muted-foreground">Controls invoice due date and early-payment discount terms.</p>
                    </div>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4 md:col-span-2">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Currency</label>
                            <Select value={currencyCode} onValueChange={handleCurrencyChange}>
                                <SelectTrigger>
                                    <SelectValue placeholder="Select Currency" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="GHS">GHS - Ghana Cedi</SelectItem>
                                    <SelectItem value="USD">USD - US Dollar</SelectItem>
                                    <SelectItem value="EUR">EUR - Euro</SelectItem>
                                    <SelectItem value="GBP">GBP - British Pound</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                        {currencyCode !== 'GHS' && (
                            <div className="space-y-2">
                                <label className="text-sm font-medium text-amber-600 font-semibold">Exchange Rate to Base Currency</label>
                                <Input 
                                    type="number" 
                                    step="0.0001" 
                                    min="0.0001"
                                    value={exchangeRate} 
                                    onChange={(e) => {
                                        setExchangeRate(Number(e.target.value) || 1.0);
                                        setExchangeRateSource('Custom');
                                    }} 
                                />
                                <span className="text-[11px] text-muted-foreground block mt-1">1 {currencyCode} = {exchangeRate} GHS</span>
                            </div>
                        )}
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Default Tax Group (For new lines)</label>
                            <Select value={taxGroupId} onValueChange={setTaxGroupId}>
                                <SelectTrigger>
                                    <SelectValue placeholder="No Tax (Zero/Exempt)" />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="none">No Tax (Zero/Exempt)</SelectItem>
                                    {taxGroups.map(tg => (
                                        <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                            <span className="text-[11px] text-muted-foreground block mt-1">Optional. Pre-populates new lines; can be overridden on each line.</span>
                        </div>
                    </div>

                    {currencyCode !== 'GHS' && (
                        <div className="border p-4 rounded-lg bg-muted/20 md:col-span-2 space-y-4">
                            <div className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Advanced FX Details</div>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <label className="text-xs font-medium">Exchange Rate Date</label>
                                    <Input type="date" value={exchangeRateDate} onChange={(e) => setExchangeRateDate(e.target.value)} />
                                </div>
                                <div className="space-y-2">
                                    <label className="text-xs font-medium">Exchange Rate Source</label>
                                    <Select value={exchangeRateSource} onValueChange={setExchangeRateSource}>
                                        <SelectTrigger className="h-10 text-xs">
                                            <SelectValue placeholder="Select FX Source" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Daily">Daily</SelectItem>
                                            <SelectItem value="Spot">Spot</SelectItem>
                                            <SelectItem value="Official">Official</SelectItem>
                                            <SelectItem value="Market">Market</SelectItem>
                                            <SelectItem value="Custom">Custom</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                            </div>
                        </div>
                    )}
                </CardContent>
            </Card>

            <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                    <CardTitle>PO Lines</CardTitle>
                    <div className="flex space-x-2">
                        <Button size="sm" variant="outline" onClick={() => handleAddLine(1)}>
                            <Plus className="w-4 h-4 mr-2" /> Add Inventory Line
                        </Button>
                        <Button size="sm" variant="outline" onClick={() => handleAddLine(2)}>
                            <Plus className="w-4 h-4 mr-2" /> Add GL Line
                        </Button>
                    </div>
                </CardHeader>
                <CardContent className="space-y-4">
                    {lines.map((line, index) => (
                        <div key={line.id} className="grid grid-cols-12 gap-3 items-end border p-4 rounded-md">
                            <div className="col-span-12 md:col-span-3">
                                <label className="text-sm font-medium mb-2 block">{line.lineType === 1 ? 'Inventory Item' : 'GL Account'}</label>
                                {line.lineType === 1 ? (
                                    <SearchableSelect 
                                        items={inventoryItems}
                                        value={line.inventoryItemId}
                                        onValueChange={(val: any) => handleLineChange(index, 'inventoryItemId', val)}
                                        placeholder="Select Item"
                                        displayKey="displayName"
                                    />
                                ) : (
                                    <SearchableSelect 
                                        items={glAccounts}
                                        value={line.glAccountId}
                                        onValueChange={(val: any) => handleLineChange(index, 'glAccountId', val)}
                                        placeholder="Select Account"
                                        displayKey="displayName"
                                    />
                                )}
                            </div>
                            
                            {line.lineType === 1 && (
                                <div className="col-span-12 md:col-span-1">
                                    <label className="text-sm font-medium mb-2 block">Whse</label>
                                    <Select value={line.warehouseId} onValueChange={(val) => handleLineChange(index, 'warehouseId', val)}>
                                        <SelectTrigger><SelectValue placeholder="Whse" /></SelectTrigger>
                                        <SelectContent>
                                            {warehouses.map(w => (
                                                <SelectItem key={w.id} value={w.id}>{w.displayName}</SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                </div>
                            )}

                            <div className={`col-span-12 ${line.lineType === 1 ? 'md:col-span-2' : 'md:col-span-3'}`}>
                                <label className="text-sm font-medium mb-2 block">Description</label>
                                <Input value={line.description} onChange={(e) => handleLineChange(index, 'description', e.target.value)} />
                            </div>

                            <div className="col-span-4 md:col-span-1">
                                <label className="text-sm font-medium mb-2 block">Qty</label>
                                <Input type="number" min="1" value={line.orderedQuantity} onChange={(e) => handleLineChange(index, 'orderedQuantity', e.target.value)} />
                            </div>

                            <div className="col-span-4 md:col-span-1">
                                <label className="text-sm font-medium mb-2 block">Price</label>
                                <Input type="number" min="0" step="0.01" value={line.unitPrice} onChange={(e) => handleLineChange(index, 'unitPrice', e.target.value)} />
                            </div>

                            <div className="col-span-6 md:col-span-2">
                                <label className="text-sm font-medium mb-2 block text-amber-600 font-semibold">Tax Group</label>
                                <Select 
                                    value={line.taxGroupId || 'inherit'} 
                                    onValueChange={(val) => handleLineChange(index, 'taxGroupId', val === 'inherit' ? '' : val)}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Inherit Default" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="inherit">
                                            {taxGroupId && taxGroupId !== 'none'
                                                ? `Inherited: ${taxGroups.find(t => t.id === taxGroupId)?.name || ''}`
                                                : 'Inherited: Zero-rated / Exempt'}
                                        </SelectItem>
                                        <SelectItem value="none">Zero-rated / Exempt</SelectItem>
                                        {taxGroups.map(tg => (
                                            <SelectItem key={tg.id} value={tg.id}>{tg.name}</SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                            </div>

                            <div className="col-span-6 md:col-span-1">
                                <label className="text-sm font-medium mb-2 block">Total</label>
                                <Input value={line.lineTotal?.toFixed(2) || '0.00'} disabled />
                            </div>
                            
                            <div className="col-span-12 md:col-span-1 flex justify-end">
                                <Button variant="destructive" size="icon" onClick={() => handleRemoveLine(index)}>
                                    <Trash2 className="w-4 h-4" />
                                </Button>
                            </div>
                        </div>
                    ))}
                    {lines.length === 0 && (
                        <div className="text-center p-8 text-muted-foreground border border-dashed rounded-md">
                            No lines added. Click "Add Inventory Line" or "Add GL Line" above.
                        </div>
                    )}
                    
                    {/* Tax Preview Breakdown Cards */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-6 border-t mt-4">
                        <div>
                            {taxEstimate.taxList.length > 0 && (
                                <div className="p-4 bg-muted/40 rounded-lg space-y-2 border">
                                    <div className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-2">Estimated Tax Breakdowns</div>
                                    <div className="space-y-1">
                                        {taxEstimate.taxList.map(t => (
                                            <div key={t.code} className="flex justify-between text-sm">
                                                <span>{t.name} ({t.rate}%)</span>
                                                <span className="font-medium">{currencyCode} {t.amount.toFixed(2)}</span>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            )}
                        </div>
                        <div className="flex flex-col items-end space-y-2">
                            <div className="flex justify-between w-64 text-sm">
                                <span className="text-muted-foreground">Subtotal (Net):</span>
                                <span className="font-medium">{currencyCode} {totalAmount.toFixed(2)}</span>
                            </div>
                            <div className="flex justify-between w-64 text-sm">
                                <span className="text-muted-foreground">Est. Tax:</span>
                                <span className="font-medium text-amber-600">+{currencyCode} {totalEstimatedTax.toFixed(2)}</span>
                            </div>
                            <div className="flex justify-between w-64 text-xl font-bold border-t pt-2 mt-2">
                                <span>Grand Total:</span>
                                <span className="text-primary">{currencyCode} {grandTotal.toFixed(2)}</span>
                            </div>
                        </div>
                    </div>
                </CardContent>
            </Card>

            <div className="flex justify-end space-x-4">
                <Button variant="outline" onClick={() => router.back()}>Cancel</Button>
                <Button onClick={handleCreate} disabled={submitting}>
                    {submitting ? 'Creating...' : 'Create Purchase Order'}
                </Button>
            </div>
        </div>
    );
}
