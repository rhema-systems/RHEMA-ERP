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
    AlertCircle,
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
    CardDescription,
    CardFooter,
    CardHeader,
    CardTitle,
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
import { arService } from '@/services/ar-service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { useToast } from '@/components/ui/use-toast';
import { formatCurrency, cn } from '@/lib/utils';
import { format, addDays } from 'date-fns';
import { useQuery } from '@tanstack/react-query';

const lineItemSchema = z.object({
    lineItemType: z.enum(['Product', 'GLAccount']).default('Product'),
    productId: z.string().optional(),
    glAccountId: z.string().optional(),
    description: z.string().min(1, 'Description is required'),
    quantity: z.coerce.number().min(0.01, 'Quantity must be positive'),
    unitPrice: z.coerce.number().min(0, 'Unit price must be positive'),
    discountPercentage: z.coerce.number().min(0).max(100).optional().default(0),
    taxCode: z.string().optional(),
});

const invoiceSchema = z.object({
    customerId: z.string().min(1, 'Customer is required'),
    invoiceDate: z.date(),
    dueDate: z.date(),
    currencyCode: z.string().default('USD'),
    notes: z.string().optional(),
    lineItems: z.array(lineItemSchema).min(1, 'At least one line item is required'),
});

type InvoiceFormValues = z.infer<typeof invoiceSchema>;

export default function NewInvoicePage() {
    const router = useRouter();
    const searchParams = useSearchParams();
    const preselectedCustomerId = searchParams.get('customerId');
    const { toast } = useToast();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [selectedCustomer, setSelectedCustomer] = useState<any>(null);
    const [customerComboOpen, setCustomerComboOpen] = useState(false);
    const [customerSearch, setCustomerSearch] = useState('');
    const [glAccountOpenIndex, setGlAccountOpenIndex] = useState<number | null>(null);
    const [glAccountSearch, setGlAccountSearch] = useState('');

    // Fetch customers for the dropdown
    const { data: customersData, isLoading: customersLoading } = useQuery({
        queryKey: ['customers-list'],
        queryFn: () => arService.getCustomers({ pageSize: 100 }),
    });

    // Fetch active GL accounts for line item selection using financeDataService
    const { data: glAccountsData, isLoading: glAccountsLoading } = useQuery({
        queryKey: ['gl-accounts-active'],
        queryFn: async () => {
            const accounts = await financeDataService.getAccounts({ status: 'Active' });
            return {
                items: accounts.filter((account: any) => account.isActive !== false)
            };
        },
    });

    // Fetch active tax groups for line item tax selection
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

    // Filter customers based on search
    const filteredCustomers = customersData?.items?.filter((customer: any) => {
        if (!customerSearch) return true;
        const search = customerSearch.toLowerCase();
        return (
            customer.customerName?.toLowerCase().includes(search) ||
            customer.customerCode?.toLowerCase().includes(search) ||
            customer.email?.toLowerCase().includes(search)
        );
    }) || [];

    // Filter GL accounts based on search
    const filteredGlAccounts = glAccountsData?.items?.filter((account: any) => {
        if (!glAccountSearch) return true;
        const search = glAccountSearch.toLowerCase();
        return (
            account.accountCode?.toLowerCase().includes(search) ||
            account.accountName?.toLowerCase().includes(search) ||
            account.accountNumber?.toLowerCase().includes(search)
        );
    }) || [];

    // Helper to get account display name by ID
    const getAccountDisplay = (accountId: string | undefined) => {
        if (!accountId) return null;
        const account = glAccountsData?.items?.find((a: any) => a.id === accountId);
        return account ? `${account.accountCode} - ${account.accountName}` : null;
    };


    const form = useForm<InvoiceFormValues>({
        resolver: zodResolver(invoiceSchema),
        defaultValues: {
            customerId: preselectedCustomerId || '',
            invoiceDate: new Date(),
            dueDate: addDays(new Date(), 30),
            currencyCode: 'USD',
            notes: '',
            lineItems: [
                { lineItemType: 'Product' as const, description: 'Service / Product', quantity: 1, unitPrice: 0, discountPercentage: 0, taxCode: '' }
            ],
        },
    });

    const { fields, append, remove } = useFieldArray({
        control: form.control,
        name: 'lineItems',
    });

    // Calculate totals
    const watchLineItems = form.watch('lineItems');
    const subtotal = watchLineItems.reduce((acc, item) => {
        const qty = Number(item.quantity) || 0;
        const price = Number(item.unitPrice) || 0;
        const discount = Number(item.discountPercentage) || 0;
        const lineTotal = qty * price * (1 - discount / 100);
        return acc + lineTotal;
    }, 0);

    // Tax will be calculated by backend based on tax code
    const totalTax = 0;

    const totalAmount = subtotal + totalTax;

    // Update due date when customer is selected (based on payment terms)
    const onCustomerChange = (customerId: string) => {
        form.setValue('customerId', customerId);
        if (!customersData?.items) return;

        const customer = customersData.items.find(c => c.id === customerId);
        if (customer) {
            setSelectedCustomer(customer);
            const terms = customer.paymentTermsDays || 30;
            const invoiceDate = form.getValues('invoiceDate');
            form.setValue('dueDate', addDays(invoiceDate, terms));
        }
    };

    // Set initial selected customer if preselected
    useEffect(() => {
        if (preselectedCustomerId && customersData?.items) {
            onCustomerChange(preselectedCustomerId);
        }
    }, [preselectedCustomerId, customersData]);


    const onSubmit = async (data: InvoiceFormValues) => {
        setIsSubmitting(true);
        try {
            await arService.createInvoice({
                ...data,
                invoiceDate: data.invoiceDate.toISOString(),
                dueDate: data.dueDate.toISOString(),
                lineItems: data.lineItems.map(item => ({
                    lineItemType: item.lineItemType,
                    productId: item.productId,
                    glAccountId: item.glAccountId,
                    description: item.description,
                    quantity: Number(item.quantity),
                    unitPrice: Number(item.unitPrice),
                    discountPercentage: Number(item.discountPercentage),
                    taxCode: item.taxCode
                }))
            });

            toast({
                title: 'Success',
                description: 'Invoice created successfully',
            });

            router.push('/finance/ar/invoices');
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to create invoice',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="space-y-8 p-8 max-w-[1200px] mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">New Invoice</h1>
                    <p className="text-muted-foreground">
                        Create a new invoice for a customer.
                    </p>
                </div>
            </div>

            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8">
                <Card>
                    <CardHeader>
                        <CardTitle>Invoice Details</CardTitle>
                    </CardHeader>
                    <CardContent className="grid gap-6 md:grid-cols-2">
                        <div className="space-y-2">
                            <Label htmlFor="customer">Customer</Label>
                            <Popover open={customerComboOpen} onOpenChange={setCustomerComboOpen}>
                                <PopoverTrigger asChild>
                                    <Button
                                        variant="outline"
                                        role="combobox"
                                        aria-expanded={customerComboOpen}
                                        className="w-full justify-between"
                                    >
                                        {selectedCustomer
                                            ? `${selectedCustomer.customerName} (${selectedCustomer.customerCode})`
                                            : customersLoading
                                            ? "Loading customers..."
                                            : "Select a customer..."}
                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-[400px] p-0" align="start">
                                    <Command shouldFilter={false}>
                                        <CommandInput
                                            placeholder="Search customer by name, code, or email..."
                                            value={customerSearch}
                                            onValueChange={setCustomerSearch}
                                        />
                                        <CommandList>
                                            <CommandEmpty>
                                                {customersLoading ? "Loading..." : "No customer found."}
                                            </CommandEmpty>
                                            <CommandGroup>
                                                {filteredCustomers.map((customer: any) => {
                                                    const handleSelect = () => {
                                                        onCustomerChange(customer.id);
                                                        setCustomerComboOpen(false);
                                                        setCustomerSearch('');
                                                    };
                                                    return (
                                                        <div
                                                            key={customer.id}
                                                            onClick={handleSelect}
                                                            className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                        >
                                                            <Check
                                                                className={cn(
                                                                    "mr-2 h-4 w-4",
                                                                    selectedCustomer?.id === customer.id
                                                                        ? "opacity-100"
                                                                        : "opacity-0"
                                                                )}
                                                            />
                                                            <div className="flex flex-col">
                                                                <span className="font-medium">{customer.customerName}</span>
                                                                <span className="text-xs text-muted-foreground">
                                                                    {customer.customerCode} {customer.email && `- ${customer.email}`}
                                                                </span>
                                                            </div>
                                                        </div>
                                                    );
                                                })}
                                            </CommandGroup>
                                        </CommandList>
                                    </Command>
                                </PopoverContent>
                            </Popover>
                            {form.formState.errors.customerId && (
                                <p className="text-sm text-red-500">{form.formState.errors.customerId.message}</p>
                            )}
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label>Invoice Date</Label>
                                <Controller
                                    control={form.control}
                                    name="invoiceDate"
                                    render={({ field }) => (
                                        <Popover>
                                            <PopoverTrigger asChild>
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        "w-full justify-start text-left font-normal",
                                                        !field.value && "text-muted-foreground"
                                                    )}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-auto p-0">
                                                <Calendar
                                                    mode="single"
                                                    selected={field.value}
                                                    onSelect={field.onChange}
                                                    initialFocus
                                                />
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
                                                <Button
                                                    variant="outline"
                                                    className={cn(
                                                        "w-full justify-start text-left font-normal",
                                                        !field.value && "text-muted-foreground"
                                                    )}
                                                >
                                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                                    {field.value ? format(field.value, "PPP") : <span>Pick a date</span>}
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-auto p-0">
                                                <Calendar
                                                    mode="single"
                                                    selected={field.value}
                                                    onSelect={field.onChange}
                                                    initialFocus
                                                />
                                            </PopoverContent>
                                        </Popover>
                                    )}
                                />
                            </div>
                        </div>

                        <div className="md:col-span-2">
                            <Label htmlFor="notes">Notes/Memo</Label>
                            <Textarea
                                id="notes"
                                placeholder="Reference number, payment instructions, etc."
                                {...form.register('notes')}
                            />
                        </div>
                    </CardContent>
                </Card>

                {/* Line Items Card */}
                <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                        <CardTitle>Line Items</CardTitle>
                        <Button type="button" variant="outline" size="sm" onClick={() => append({ lineItemType: 'Product' as const, description: '', quantity: 1, unitPrice: 0, discountPercentage: 0, taxCode: '' })}>
                            <Plus className="mr-2 h-4 w-4" /> Add Item
                        </Button>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            {fields.map((field, index) => {
                                const lineItemType = form.watch(`lineItems.${index}.lineItemType`);
                                return (
                                    <div key={field.id} className="grid grid-cols-12 gap-4 items-end border-b pb-4">
                                        {/* Type Selector */}
                                        <div className="col-span-2 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Type</Label>
                                            <Controller
                                                control={form.control}
                                                name={`lineItems.${index}.lineItemType`}
                                                render={({ field }) => (
                                                    <Select
                                                        value={field.value}
                                                        onValueChange={field.onChange}
                                                    >
                                                        <SelectTrigger>
                                                            <SelectValue placeholder="Type" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="Product">Product</SelectItem>
                                                            <SelectItem value="GLAccount">GL Account</SelectItem>
                                                        </SelectContent>
                                                    </Select>
                                                )}
                                            />
                                        </div>

                                        {/* Conditional: GL Account or Description */}
                                        {lineItemType === 'GLAccount' ? (
                                            <div className="col-span-3 space-y-2">
                                                <Label className={index !== 0 ? 'sr-only' : ''}>GL Account</Label>
                                                <Controller
                                                    control={form.control}
                                                    name={`lineItems.${index}.glAccountId`}
                                                    render={({ field }) => (
                                                        <Popover
                                                            open={glAccountOpenIndex === index}
                                                            onOpenChange={(open) => {
                                                                setGlAccountOpenIndex(open ? index : null);
                                                                if (!open) setGlAccountSearch('');
                                                            }}
                                                        >
                                                            <PopoverTrigger asChild>
                                                                <Button
                                                                    variant="outline"
                                                                    role="combobox"
                                                                    className="w-full justify-between text-left font-medium"
                                                                >
                                                                    <span className="truncate font-semibold">
                                                                        {getAccountDisplay(field.value) ||
                                                                            (glAccountsLoading ? "Loading..." : "Select account...")}
                                                                    </span>
                                                                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                                </Button>
                                                            </PopoverTrigger>
                                                            <PopoverContent className="w-[350px] p-0" align="start">
                                                                <Command shouldFilter={false}>
                                                                    <CommandInput
                                                                        placeholder="Search by code or name..."
                                                                        value={glAccountSearch}
                                                                        onValueChange={setGlAccountSearch}
                                                                    />
                                                                    <CommandList>
                                                                        <CommandEmpty>
                                                                            {glAccountsLoading ? "Loading..." : "No account found."}
                                                                        </CommandEmpty>
                                                                        <CommandGroup>
                                                                            {filteredGlAccounts.map((account: any) => {
                                                                                const handleSelect = () => {
                                                                                    field.onChange(account.id);
                                                                                    // Auto-fill description with account name
                                                                                    form.setValue(
                                                                                        `lineItems.${index}.description`,
                                                                                        account.accountName
                                                                                    );
                                                                                    setGlAccountOpenIndex(null);
                                                                                    setGlAccountSearch('');
                                                                                };
                                                                                return (
                                                                                    <div
                                                                                        key={account.id}
                                                                                        onClick={handleSelect}
                                                                                        className="relative flex cursor-pointer select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground"
                                                                                    >
                                                                                        <Check
                                                                                            className={cn(
                                                                                                "mr-2 h-4 w-4",
                                                                                                field.value === account.id
                                                                                                    ? "opacity-100"
                                                                                                    : "opacity-0"
                                                                                            )}
                                                                                        />
                                                                                        <div className="flex flex-col">
                                                                                            <span className="font-bold text-sm">
                                                                                                {account.accountCode}
                                                                                            </span>
                                                                                            <span className="text-xs text-muted-foreground truncate font-medium">
                                                                                                {account.accountName}
                                                                                            </span>
                                                                                        </div>
                                                                                    </div>
                                                                                );
                                                                            })}
                                                                        </CommandGroup>
                                                                    </CommandList>
                                                                </Command>
                                                            </PopoverContent>
                                                        </Popover>
                                                    )}
                                                />
                                            </div>
                                        ) : (
                                            <div className="col-span-3 space-y-2">
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
                                        <div className="col-span-1 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Disc %</Label>
                                            <Input type="number" step="0.5" {...form.register(`lineItems.${index}.discountPercentage` as const)} className="text-center" />
                                        </div>
                                        <div className="col-span-2 space-y-2">
                                            <Label className={index !== 0 ? 'sr-only' : ''}>Tax Code</Label>
                                            <Controller
                                                control={form.control}
                                                name={`lineItems.${index}.taxCode`}
                                                render={({ field }) => (
                                                    <Select
                                                        value={field.value}
                                                        onValueChange={field.onChange}
                                                    >
                                                        <SelectTrigger>
                                                            <SelectValue placeholder="None" />
                                                        </SelectTrigger>
                                                        <SelectContent>
                                                            <SelectItem value="none">None</SelectItem>
                                                            {taxGroupsData?.map((group: any) => (
                                                                <SelectItem key={group.code} value={group.code}>
                                                                    {group.code}
                                                                </SelectItem>
                                                            ))}
                                                        </SelectContent>
                                                    </Select>
                                                )}
                                            />
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

                        {/* Totals Section */}
                        <div className="mt-8 flex justify-end">
                            <div className="w-1/3 space-y-2 text-right">
                                <div className="flex justify-between">
                                    <span className="text-muted-foreground">Subtotal:</span>
                                    <span className="font-medium">{formatCurrency(subtotal)}</span>
                                </div>
                                <div className="flex justify-between">
                                    <span className="text-muted-foreground">Tax:</span>
                                    <span className="font-medium">{formatCurrency(totalTax)}</span>
                                </div>
                                <div className="flex justify-between text-lg font-bold border-t pt-2 mt-2">
                                    <span>Total:</span>
                                    <span>{formatCurrency(totalAmount)}</span>
                                </div>
                            </div>
                        </div>

                    </CardContent>
                    <CardFooter className="flex justify-end space-x-2 bg-muted/50 p-4">
                        <Button variant="outline" type="button" onClick={() => router.back()}>
                            Cancel
                        </Button>
                        <Button type="submit" disabled={isSubmitting}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Create Invoice
                        </Button>
                    </CardFooter>
                </Card>
            </form>
        </div>
    );
}
