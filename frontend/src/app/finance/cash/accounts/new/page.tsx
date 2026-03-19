'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { format } from 'date-fns';
import { Loader2, ArrowLeft, Check, ChevronsUpDown } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { cn } from '@/lib/utils'; // Assuming this utility exists

import { cashManagementDataService } from '@/services/finance/cash-management-data.service';
import { financeDataService } from '@/services/finance/finance-data.service';
import { BankAccountType, CreateBankAccountDto } from '@/types/cash-management';
import { Account, Currency } from '@/types/finance';

const formSchema = z.object({
    bankName: z.string().min(2, 'Bank name is required'),
    accountName: z.string().min(2, 'Account name is required'),
    accountNumber: z.string().min(2, 'Account number is required'),
    bankBranch: z.string().optional(),
    accountType: z.nativeEnum(BankAccountType),
    currency: z.string().min(1, 'Currency is required'),
    glAccountId: z.string().optional(),
    openingBalance: z.coerce.number().default(0),
    openingDate: z.string().min(1, 'Opening date is required'),
    notes: z.string().optional(),
});

export default function NewBankAccountPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [submitting, setSubmitting] = useState(false);
    const [glAccounts, setGlAccounts] = useState<Account[]>([]);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [openGlSelect, setOpenGlSelect] = useState(false);
    const [glAccountSearch, setGlAccountSearch] = useState('');

    const form = useForm<z.infer<typeof formSchema>>({
        resolver: zodResolver(formSchema),
        defaultValues: {
            bankName: '',
            accountName: '',
            accountNumber: '',
            bankBranch: '',
            accountType: BankAccountType.Checking,
            currency: '',
            glAccountId: '',
            openingBalance: 0,
            openingDate: format(new Date(), 'yyyy-MM-dd'),
            notes: '',
        },
    });

    useEffect(() => {
        const loadData = async () => {
            try {
                // Fetch Asset accounts for the GL selector
                const accountsData = await financeDataService.getAccounts({ accountType: 'Asset', status: 'Active' });
                // Filter for Cash and Cash Equivalents
                const cashAccounts = accountsData.filter(a =>
                    a.accountSubCategory === 'Cash and Cash Equivalents' ||
                    a.accountCategory === 'Cash and Cash Equivalents' ||
                    a.accountName.toLowerCase().includes('cash') ||
                    a.accountName.toLowerCase().includes('bank')
                );
                setGlAccounts(cashAccounts.length > 0 ? cashAccounts : accountsData); // Fallback to all assets if no specific cash accounts found, or just show filtered? User asked for filtering. Let's prioritize filtered but maybe keep assets if empty logic is risky. User explicitly asked "let's filter", so showing only cash accounts is safer.

                if (cashAccounts.length > 0) {
                    setGlAccounts(cashAccounts);
                } else {
                    // If no specific "Cash" category found, fallback to Assets but sorted by code
                    setGlAccounts(accountsData.sort((a, b) => a.accountCode.localeCompare(b.accountCode)));
                }

                // Fetch Currencies
                const currenciesData = await financeDataService.getCurrencies({ isActive: true });
                setCurrencies(currenciesData);

                // Set default currency if possible
                const baseCurrency = currenciesData.find(c => c.isBaseCurrency);
                if (baseCurrency) {
                    form.setValue('currency', baseCurrency.currencyCode);
                }
            } catch (error) {
                console.error('Failed to load dependency data:', error);
                toast({
                    title: 'Error',
                    description: 'Failed to load accounts or currencies.',
                    variant: 'destructive',
                });
            }
        };

        loadData();
    }, [form, toast]);

    // Filter GL accounts based on search
    const filteredGlAccounts = glAccounts.filter((account) => {
        if (!glAccountSearch) return true;
        const search = glAccountSearch.toLowerCase();
        return (
            account.accountCode?.toLowerCase().includes(search) ||
            account.accountName?.toLowerCase().includes(search)
        );
    });

    const onSubmit = async (values: z.infer<typeof formSchema>) => {
        setSubmitting(true);
        try {
            await cashManagementDataService.createBankAccount(values as CreateBankAccountDto);
            toast({
                title: 'Success',
                description: 'Bank account created successfully.',
            });
            router.push('/finance/cash/accounts');
        } catch (error) {
            console.error('Error creating bank account:', error);
            toast({
                title: 'Error',
                description: 'Failed to create bank account. Please try again.',
                variant: 'destructive',
            });
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="p-8 max-w-2xl mx-auto space-y-6">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold">New Bank Account</h1>
                    <p className="text-muted-foreground">Add a new bank account and link it to the General Ledger.</p>
                </div>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>Account Details</CardTitle>
                    <CardDescription>Enter the details for the new bank account.</CardDescription>
                </CardHeader>
                <CardContent>
                    <Form {...form}>
                        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">

                            <div className="grid grid-cols-2 gap-4">
                                <FormField
                                    control={form.control}
                                    name="bankName"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Bank Name</FormLabel>
                                            <FormControl>
                                                <Input placeholder="e.g. Chase Bank" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />

                                <FormField
                                    control={form.control}
                                    name="bankBranch"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Branch (Optional)</FormLabel>
                                            <FormControl>
                                                <Input placeholder="e.g. Downtown Branch" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>

                            <div className="grid grid-cols-2 gap-4">
                                <FormField
                                    control={form.control}
                                    name="accountName"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Account Name</FormLabel>
                                            <FormControl>
                                                <Input placeholder="e.g. Operating Account" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />

                                <FormField
                                    control={form.control}
                                    name="accountNumber"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Account Number</FormLabel>
                                            <FormControl>
                                                <Input placeholder="e.g. 1234567890" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>

                            <div className="grid grid-cols-2 gap-4">
                                <FormField
                                    control={form.control}
                                    name="accountType"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Account Type</FormLabel>
                                            <Select onValueChange={field.onChange} defaultValue={field.value}>
                                                <FormControl>
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select account type" />
                                                    </SelectTrigger>
                                                </FormControl>
                                                <SelectContent>
                                                    {Object.values(BankAccountType).map((type) => (
                                                        <SelectItem key={type} value={type}>
                                                            {type}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />

                                <FormField
                                    control={form.control}
                                    name="currency"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Currency</FormLabel>
                                            <Select onValueChange={field.onChange} defaultValue={field.value}>
                                                <FormControl>
                                                    <SelectTrigger>
                                                        <SelectValue placeholder="Select currency" />
                                                    </SelectTrigger>
                                                </FormControl>
                                                <SelectContent>
                                                    {currencies.map((c) => (
                                                        <SelectItem key={c.id} value={c.currencyCode}>
                                                            {c.currencyCode} - {c.currencyName}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>

                            <FormField
                                control={form.control}
                                name="glAccountId"
                                render={({ field }) => (
                                    <FormItem className="flex flex-col">
                                        <FormLabel>Linked GL Account (Asset)</FormLabel>
                                        <Popover open={openGlSelect} onOpenChange={setOpenGlSelect}>
                                            <PopoverTrigger asChild>
                                                <FormControl>
                                                    <Button
                                                        variant="outline"
                                                        role="combobox"
                                                        className={cn(
                                                            "w-full justify-between",
                                                            !field.value && "text-muted-foreground"
                                                        )}
                                                    >
                                                        {field.value
                                                            ? glAccounts.find(
                                                                (account) => account.id === field.value
                                                            )?.accountName + ` (${glAccounts.find((a) => a.id === field.value)?.accountCode})`
                                                            : "Select GL account"}
                                                        <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                    </Button>
                                                </FormControl>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-[500px] p-0">
                                                <Command shouldFilter={false}>
                                                    <CommandInput
                                                        placeholder="Search GL account..."
                                                        value={glAccountSearch}
                                                        onValueChange={setGlAccountSearch}
                                                    />
                                                    <CommandList>
                                                        <CommandEmpty>No GL account found.</CommandEmpty>
                                                        <CommandGroup>
                                                            {filteredGlAccounts.map((account) => {
                                                                const handleSelect = () => {
                                                                    form.setValue("glAccountId", account.id);

                                                                    // Auto-fill account details if empty
                                                                    const currentName = form.getValues('accountName');
                                                                    if (!currentName) {
                                                                        form.setValue('accountName', account.accountName);
                                                                    }

                                                                    // Also try to match currency if possible
                                                                    if (account.currencyCode) {
                                                                        form.setValue('currency', account.currencyCode);
                                                                    }

                                                                    setOpenGlSelect(false);
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
                                                                                account.id === field.value
                                                                                    ? "opacity-100"
                                                                                    : "opacity-0"
                                                                            )}
                                                                        />
                                                                        <span className="font-bold mr-2">{account.accountCode}</span>
                                                                        {account.accountName}
                                                                    </div>
                                                                );
                                                            })}
                                                        </CommandGroup>
                                                    </CommandList>
                                                </Command>
                                            </PopoverContent>
                                        </Popover>
                                        <FormDescription>
                                            Link this bank account to a General Ledger asset account.
                                        </FormDescription>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <div className="grid grid-cols-2 gap-4">
                                <FormField
                                    control={form.control}
                                    name="openingDate"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Opening Date</FormLabel>
                                            <FormControl>
                                                <Input type="date" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                                <FormField
                                    control={form.control}
                                    name="openingBalance"
                                    render={({ field }) => (
                                        <FormItem>
                                            <FormLabel>Opening Balance</FormLabel>
                                            <FormControl>
                                                <Input type="number" step="0.01" {...field} />
                                            </FormControl>
                                            <FormMessage />
                                        </FormItem>
                                    )}
                                />
                            </div>

                            <FormField
                                control={form.control}
                                name="notes"
                                render={({ field }) => (
                                    <FormItem>
                                        <FormLabel>Notes</FormLabel>
                                        <FormControl>
                                            <Textarea placeholder="Additional notes..." {...field} />
                                        </FormControl>
                                        <FormMessage />
                                    </FormItem>
                                )}
                            />

                            <div className="flex justify-end gap-4">
                                <Button type="button" variant="outline" onClick={() => router.back()}>
                                    Cancel
                                </Button>
                                <Button type="submit" disabled={submitting}>
                                    {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                                    Create Bank Account
                                </Button>
                            </div>
                        </form>
                    </Form>
                </CardContent>
            </Card>
        </div>
    );
}
