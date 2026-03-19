'use client';

import { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
    ArrowLeft,
    Loader2,
    Plus,
    Trash2,
    Calendar as CalendarIcon,
    Check,
    ChevronsUpDown,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
    CardFooter,
} from '@/components/ui/card';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import {
    Command,
    CommandEmpty,
    CommandGroup,
    CommandInput,
    CommandList,
} from '@/components/ui/command';
import { Calendar } from '@/components/ui/calendar';
import { accountsPayableService } from '@/services/accountsPayableService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { businessPartnerService } from '@/services/businessPartnerService';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { format, addDays } from 'date-fns';
import { useQuery } from '@tanstack/react-query';

const lineItemSchema = z.object({
    lineItemType: z.enum(['Expense', 'Product', 'Inventory']).default('Expense'),
    glAccountId: z.string().optional(),
    inventoryItemId: z.string().optional(),
    warehouseId: z.string().optional(),
    purchaseOrderItemId: z.string().optional(),
    description: z.string().min(1, 'Description is required'),
    quantity: z.coerce.number().min(0.01, 'Quantity must be positive'),
    unitPrice: z.coerce.number().min(0, 'Unit price must be positive'),
    taxCode: z.string().optional(),
    discountPercentage: z.coerce.number().min(0).max(100).optional().default(0),
    unit: z.string().optional(),
});

const invoiceSchema = z.object({
    supplierId: z.string().min(1, 'Supplier is required'),
    supplierInvoiceNumber: z.string().optional(),
    purchaseOrderId: z.string().optional(),
    invoiceDate: z.date(),
    dueDate: z.date(),
    currencyCode: z.string().default('USD'),
    notes: z.string().optional(),
    reference: z.string().optional(),
    lineItems: z.array(lineItemSchema).min(1, 'At least one line item is required'),
});

type InvoiceFormValues = z.infer<typeof invoiceSchema>;

export default function CreateVendorInvoicePage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedSupplierId = searchParams.get('supplierId');
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);

    // Supplier combobox state
    const [selectedSupplier, setSelectedSupplier] = useState<any>(null);
    const [supplierComboOpen, setSupplierComboOpen] = useState(false);
    const [supplierSearch, setSupplierSearch] = useState('');

    // GL Account combobox state
    const [glAccountOpenIndex, setGlAccountOpenIndex] = useState<number | null>(null);
    const [glAccountSearch, setGlAccountSearch] = useState('');

    // Inventory Item combobox state
    const [inventoryItemOpenIndex, setInventoryItemOpenIndex] = useState<number | null>(null);
    const [inventoryItemSearch, setInventoryItemSearch] = useState('');

    // Queries
    const { data: suppliersData, isLoading: suppliersLoading } = useQuery({
        queryKey: ['suppliers'],
        queryFn: () => businessPartnerService.getPartners({ partnerType: 'Supplier', pageSize: 100 }),
    });

    const { data: glAccountsData, isLoading: glAccountsLoading } = useQuery({
        queryKey: ['gl-accounts-active'],
        queryFn: async () => {
            const accounts = await financeDataService.getAccounts({ status: 'Active' });
            return { items: accounts.filter((account: any) => account.isActive !== false) };
        },
    });

    const { data: inventoryItemsData, isLoading: inventoryItemsLoading } = useQuery({
        queryKey: ['inventory-items-active'],
        queryFn: () => inventoryManagementService.getInventoryItems({ pageSize: 100, isActive: true } as any),
    });

    const { data: taxGroupsData } = useQuery({
        queryKey: ['tax-groups-active'],
        queryFn: async () => {
            try {
                const { apiService } = await import('@/services/api.service');
                return apiService.get<any[]>('/finance/tax/groups/active');
            } catch {
                return [];
            }
        },
    });

    const { data: warehousesData } = useQuery({
        queryKey: ['warehouses'],
        queryFn: () => inventoryManagementService.getWarehouses(),
    });

    // Filtering logic
    const filteredSuppliers = suppliersData?.items?.filter((supplier: any) => {
        if (!supplierSearch) return true;
        const search = supplierSearch.toLowerCase();
        return (
            supplier.name?.toLowerCase().includes(search) ||
            supplier.code?.toLowerCase().includes(search)
        );
    }) || [];

    const filteredGlAccounts = glAccountsData?.items?.filter((account: any) => {
        if (!glAccountSearch) return true;
        const search = glAccountSearch.toLowerCase();
        return (
            account.accountCode?.toLowerCase().includes(search) ||
            account.accountName?.toLowerCase().includes(search)
        );
    }) || [];

    const filteredInventoryItems = inventoryItemsData?.filter((item: any) => {
        if (!inventoryItemSearch) return true;
        const search = inventoryItemSearch.toLowerCase();
        return (
            item.itemCode?.toLowerCase().includes(search) ||
            item.name?.toLowerCase().includes(search)
        );
    }) || [];

    // Form setup
    const form = useForm<InvoiceFormValues>({
        // @ts-expect-error TODO: fix type
        resolver: zodResolver(invoiceSchema),
        defaultValues: {
            supplierId: preselectedSupplierId || '',
            supplierInvoiceNumber: '',
            invoiceDate: new Date(),
            dueDate: addDays(new Date(), 30),
            currencyCode: 'USD',
            notes: '',
            lineItems: [
                { lineItemType: 'Expense', description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxCode: '' }
            ],
        },
    });

    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: 'lineItems',
    });

    // Totals calculation
    const watchLineItems = form.watch('lineItems');
    const subtotal = watchLineItems.reduce((acc, item) => {
        const qty = Number(item.quantity) || 0;
        const price = Number(item.unitPrice) || 0;
        const discount = Number(item.discountPercentage) || 0;
        return acc + (qty * price * (1 - discount / 100));
    }, 0);
    const totalTax = 0; // Handled by backend, or we can mock computation
    const totalAmount = subtotal + totalTax;

    const onSupplierChange = (supplierId: string) => {
        form.setValue('supplierId', supplierId);
        if (!suppliersData?.items) return;

        const supplier = suppliersData.items.find(s => s.id === supplierId);
        if (supplier) {
            setSelectedSupplier(supplier);
            // Default dueDate based on terms could be set here
        }
    };

    useEffect(() => {
        if (preselectedSupplierId && suppliersData?.items) {
            onSupplierChange(preselectedSupplierId);
        }
    }, [preselectedSupplierId, suppliersData]);

    const getAccountDisplay = (accountId: string | undefined) => {
        if (!accountId) return null;
        const account = glAccountsData?.items?.find((a: any) => a.id === accountId);
        return account ? `${account.accountCode} - ${account.accountName}` : null;
    };

    const getInventoryItemDisplay = (itemId: string | undefined) => {
        if (!itemId) return null;
        const item = inventoryItemsData?.find((i: any) => i.id === itemId);
        return item ? `${item.itemCode} - ${item.name}` : null;
    };

    const onSubmit = async (data: InvoiceFormValues) => {
        setIsSubmitting(true);
        try {
            await accountsPayableService.createInvoice({
                ...data,
                invoiceDate: data.invoiceDate.toISOString(),
                dueDate: data.dueDate.toISOString(),
                lineItems: data.lineItems.map(item => ({
                    lineItemType: item.lineItemType,
                    glAccountId: item.glAccountId,
                    purchaseOrderItemId: item.purchaseOrderItemId,
                    description: item.description,
                    quantity: Number(item.quantity),
                    unitPrice: Number(item.unitPrice),
                    discountPercentage: Number(item.discountPercentage),
                    taxCode: item.taxCode,
                    unit: item.unit,
                    // Note: inventoryItemId and warehouseId will be passed in the DTO if backend is updated
                    ...(item.inventoryItemId ? { inventoryItemId: item.inventoryItemId } : {}),
                    ...(item.warehouseId ? { warehouseId: item.warehouseId } : {}),
                } as any))
            });

            toast({
                title: 'Success',
                description: 'Vendor invoice created successfully',
            });

            router.push('/finance/ap/invoices');
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to create vendor invoice',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1400px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Record Vendor Invoice</h1>
                    <p className="text-muted-foreground">
                        Enter a bill received from a supplier.
                    </p>
                </div>
            </div>

            <form onSubmit={form.handleSubmit(onSubmit as any)} className="space-y-8">
                <Card>
                    <CardHeader>
                        <CardTitle>Invoice Details</CardTitle>
                    </CardHeader>
                    <CardContent className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
                        <div className="space-y-2">
                            <Label htmlFor="supplier">Supplier</Label>
                            <Popover open={supplierComboOpen} onOpenChange={setSupplierComboOpen}>
                                <PopoverTrigger asChild>
                                    <Button
                                        variant="outline"
                                        role="combobox"
                                        aria-expanded={supplierComboOpen}
                                        className="w-full justify-between"
                                    >
                                        {selectedSupplier
                                            ? `${selectedSupplier.name} (${selectedSupplier.code})`
                                            : suppliersLoading
                                                ? "Loading suppliers..."
                                                : "Select a supplier..."}
                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-[400px] p-0" align="start">
                                    <Command shouldFilter={false}>
                                        <CommandInput
                                            placeholder="Search supplier..."
                                            value={supplierSearch}
                                            onValueChange={setSupplierSearch}
                                        />
                                        <CommandList>
                                            <CommandEmpty>
                                                {suppliersLoading ? "Loading..." : "No supplier found."}
                                            </CommandEmpty>
                                            <CommandGroup>
                                                {filteredSuppliers.map((supplier: any) => (
                                                    <div
                                                        key={supplier.id}
                                                        onClick={() => {
                                                            onSupplierChange(supplier.id);
                                                            setSupplierComboOpen(false);
                                                            setSupplierSearch('');
                                                        }}
                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                    >
                                                        <Check className={cn("mr-2 h-4 w-4", selectedSupplier?.id === supplier.id ? "opacity-100" : "opacity-0")} />
                                                        <div className="flex flex-col">
                                                            <span className="font-medium">{supplier.name}</span>
                                                            <span className="text-xs text-muted-foreground">{supplier.code}</span>
                                                        </div>
                                                    </div>
                                                ))}
                                            </CommandGroup>
                                        </CommandList>
                                    </Command>
                                </PopoverContent>
                            </Popover>
                            {form.formState.errors.supplierId && (
                                <p className="text-sm text-red-500">{form.formState.errors.supplierId.message}</p>
                            )}
                        </div>

                        <div className="space-y-2">
                            <Label>Supplier Invoice #</Label>
                            <Input {...form.register('supplierInvoiceNumber')} placeholder="INV-2026-001" />
                        </div>

                        <div className="space-y-2">
                            <Label>Invoice Date</Label>
                            <Controller
                                control={form.control}
                                name="invoiceDate"
                                render={({ field }) => (
                                    <Popover>
                                        <PopoverTrigger asChild>
                                            <Button variant="outline" className={cn("w-full justify-start text-left font-normal", !field.value && "text-muted-foreground")}>
                                                <CalendarIcon className="mr-2 h-4 w-4" />
                                                {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                            </Button>
                                        </PopoverTrigger>
                                        <PopoverContent className="w-auto p-0">
                                            <Calendar mode="single" selected={field.value} onSelect={field.onChange} />
                                        </PopoverContent>
                                    </Popover>
                                )}
                            />
                        </div>

                        <div className="space-y-2">
                            <Label>Due Date</Label>
                            <Controller
                                control={form.control}
                                name="dueDate"
                                render={({ field }) => (
                                    <Popover>
                                        <PopoverTrigger asChild>
                                            <Button variant="outline" className={cn("w-full justify-start text-left font-normal", !field.value && "text-muted-foreground")}>
                                                <CalendarIcon className="mr-2 h-4 w-4" />
                                                {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                            </Button>
                                        </PopoverTrigger>
                                        <PopoverContent className="w-auto p-0">
                                            <Calendar mode="single" selected={field.value} onSelect={field.onChange} />
                                        </PopoverContent>
                                    </Popover>
                                )}
                            />
                        </div>

                        <div className="space-y-2 lg:col-span-2">
                            <Label htmlFor="notes">Notes/Memo</Label>
                            <Textarea id="notes" placeholder="Reference number, payment instructions, etc." {...form.register('notes')} />
                        </div>
                    </CardContent>
                </Card>

                {/* Line Items Card */}
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Line Items</CardTitle>
                        <Button type="button" variant="outline" size="sm" onClick={() => append({ lineItemType: 'Expense' as const, description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxCode: '' })}>
                            <Plus className="mr-2 h-4 w-4" /> Add Item
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {fields.map((field, index) => {
                                const lineItemType = form.watch(`lineItems.${index}.lineItemType`);
                                return (
                                    <div key={field.id} className="grid grid-cols-12 gap-4 items-end border-b pb-4">
                                        <div className="col-span-2 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Type</Label>
                                            <Controller
                                                control={form.control}
                                                name={`lineItems.${index}.lineItemType`}
                                                render={({ field }) => (
                                                    <Select value={field.value} onValueChange={field.onChange}>
                                                        <SelectTrigger><SelectValue placeholder="Type" /></SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="Expense">GL Account / Expense</SelectItem>
                                                            <SelectItem value="Inventory">Inventory Item</SelectItem>
                                                            <SelectItem value="Product">Product / Other</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                )}
                                            />
                                        </div>

                                        {lineItemType === 'Expense' ? (
                                            <>
                                                <div className="col-span-3 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>GL Account</Label>
                                                    <Controller
                                                        control={form.control}
                                                        name={`lineItems.${index}.glAccountId`}
                                                        render={({ field }) => (
                                                            <Popover
                                                                open={glAccountOpenIndex === index}
                                                                onOpenChange={(open) => { setGlAccountOpenIndex(open ? index : null); if (!open) setGlAccountSearch(''); }}
                                                            >
                                                                <PopoverTrigger asChild>
                                                                    <Button variant="outline" role="combobox" className="w-full justify-between text-left font-medium line-clamp-1 h-10 px-3">
                                                                        <span className="truncate text-sm">
                                                                            {getAccountDisplay(field.value) || (glAccountsLoading ? "Loading..." : "Select account...")}
                                                                        </span>
                                                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                                    </Button>
                                                                </PopoverTrigger>
                                                                <PopoverContent className="w-[350px] p-0" align="start">
                                                                    <Command shouldFilter={false}>
                                                                        <CommandInput placeholder="Search code or name..." value={glAccountSearch} onValueChange={setGlAccountSearch} />
                                                                        <CommandList>
                                                                            <CommandEmpty>No account found.</CommandEmpty>
                                                                            <CommandGroup>
                                                                                {filteredGlAccounts.map((account: any) => (
                                                                                    <div
                                                                                        key={account.id}
                                                                                        onClick={() => {
                                                                                            field.onChange(account.id);
                                                                                            if (!form.getValues(`lineItems.${index}.description`)) {
                                                                                                form.setValue(`lineItems.${index}.description`, account.accountName);
                                                                                            }
                                                                                            setGlAccountOpenIndex(null);
                                                                                            setGlAccountSearch('');
                                                                                        }}
                                                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                                                    >
                                                                                        <div className="flex flex-col">
                                                                                            <span className="font-bold text-sm">{account.accountCode}</span>
                                                                                            <span className="text-xs text-muted-foreground">{account.accountName}</span>
                                                                                        </div>
                                                                                    </div>
                                                                                ))}
                                                                            </CommandGroup>
                                                                        </CommandList>
                                                                    </Command>
                                                                </PopoverContent>
                                                            </Popover>
                                                        )}
                                                    />
                                                </div>
                                                <div className="col-span-3 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>Description (Notes)</Label>
                                                    <Input {...form.register(`lineItems.${index}.description` as const)} placeholder="Notes" />
                                                </div>
                                            </>
                                        ) : lineItemType === 'Inventory' ? (
                                            <>
                                                <div className="col-span-3 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>Inventory Item</Label>
                                                    <Controller
                                                        control={form.control}
                                                        name={`lineItems.${index}.inventoryItemId`}
                                                        render={({ field }) => (
                                                            <Popover
                                                                open={inventoryItemOpenIndex === index}
                                                                onOpenChange={(open) => { setInventoryItemOpenIndex(open ? index : null); if (!open) setInventoryItemSearch(''); }}
                                                            >
                                                                <PopoverTrigger asChild>
                                                                    <Button variant="outline" role="combobox" className="w-full justify-between text-left font-medium line-clamp-1 h-10 px-3">
                                                                        <span className="truncate text-sm">
                                                                            {getInventoryItemDisplay(field.value) || (inventoryItemsLoading ? "Loading..." : "Select item...")}
                                                                        </span>
                                                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                                    </Button>
                                                                </PopoverTrigger>
                                                                <PopoverContent className="w-[350px] p-0" align="start">
                                                                    <Command shouldFilter={false}>
                                                                        <CommandInput placeholder="Search item code or name..." value={inventoryItemSearch} onValueChange={setInventoryItemSearch} />
                                                                        <CommandList>
                                                                            <CommandEmpty>No inventory items found.</CommandEmpty>
                                                                            <CommandGroup>
                                                                                {filteredInventoryItems.map((item: any) => (
                                                                                    <div
                                                                                        key={item.id}
                                                                                        onClick={() => {
                                                                                            field.onChange(item.id);
                                                                                            form.setValue(`lineItems.${index}.description`, item.name);
                                                                                            if (item.averageCost > 0) form.setValue(`lineItems.${index}.unitPrice`, item.averageCost);
                                                                                            setInventoryItemOpenIndex(null);
                                                                                            setInventoryItemSearch('');
                                                                                        }}
                                                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                                                    >
                                                                                        <div className="flex flex-col w-full">
                                                                                            <span className="font-bold text-sm">{item.itemCode}</span>
                                                                                            <div className="flex justify-between items-center w-full">
                                                                                                <span className="text-xs text-muted-foreground mr-2">{item.name}</span>
                                                                                                <span className="text-xs badge bg-muted px-1 rounded">{formatCurrency(item.averageCost || 0)}</span>
                                                                                            </div>
                                                                                        </div>
                                                                                    </div>
                                                                                ))}
                                                                            </CommandGroup>
                                                                        </CommandList>
                                                                    </Command>
                                                                </PopoverContent>
                                                            </Popover>
                                                        )}
                                                    />
                                                </div>
                                                <div className="col-span-3 space-y-2">
                                                    <Label className={index !== 0 ? 'sr-only' : ''}>Dest. Warehouse</Label>
                                                    <Controller
                                                        control={form.control}
                                                        name={`lineItems.${index}.warehouseId`}
                                                        render={({ field }) => (
                                                            <Select value={field.value} onValueChange={field.onChange}>
                                                                <SelectTrigger><SelectValue placeholder="Warehouse..." /></SelectTrigger>
                                                                <SelectContent>
                                                                    {warehousesData?.map((wh: any) => (
                                                                        <SelectItem key={wh.id} value={wh.id}>{wh.name}</SelectItem>
                                                                    ))}
                                                                </SelectContent>
                                                            </Select>
                                                        )}
                                                    />
                                                </div>
                                            </>
                                        ) : (
                                            <div className="col-span-6 space-y-2">
                                                <Label className={index !== 0 ? 'sr-only' : ''}>Description</Label>
                                                <Input {...form.register(`lineItems.${index}.description` as const)} placeholder="Item description" />
                                            </div>
                                        )}


                                        <div className="col-span-1 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Qty</Label>
                                            <Input type="number" step="1" {...form.register(`lineItems.${index}.quantity` as const)} className="text-center" />
                                        </div>
                                        <div className="col-span-2 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Price</Label>
                                            <Input type="number" step="0.01" {...form.register(`lineItems.${index}.unitPrice` as const)} className="text-right" />
                                        </div>
                                        <div className="col-span-1 flex items-end justify-center">
                                            <Button type="button" variant="ghost" size="icon" onClick={() => remove(index)} disabled={fields.length === 1} className="h-10">
                                                <Trash2 className="h-4 w-4 text-red-500" />
                                            </Button>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>

                        <div className="mt-8 flex justify-end">
                            <div className="w-1/3 space-y-2 text-right">
                                <div className="flex justify-between font-bold text-lg">
                                    <span>Total:</span>
                                    <span>{formatCurrency(totalAmount)}</span>
                                </div>
                            </div>
                        </div>
                    </CardContent>
                    <CardFooter className="flex justify-end space-x-2 bg-muted/50 p-4">
                        <Button variant="outline" type="button" onClick={() => router.back()}>Cancel</Button>
                        <Button type="submit" disabled={isSubmitting}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Record Invoice
                        </Button>
                    </CardFooter>
                </Card>
            </form>
        </div>
    );
}
