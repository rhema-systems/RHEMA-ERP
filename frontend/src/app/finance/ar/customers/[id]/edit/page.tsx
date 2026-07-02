'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowLeft, Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
    CardFooter
} from '@/components/ui/card';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue
} from '@/components/ui/select';
import { arService } from '@/services/ar-service';
import { paymentTermService, type PaymentTermListDto } from '@/services/financeCommonService';
import { useToast } from '@/components/ui/use-toast';
import { CustomerUpdateRequest } from '@/types/ar';
import { Skeleton } from '@/components/ui/skeleton';

const customerSchema = z.object({
    customerCode: z.string().min(1, 'Customer code is required'), // Read-only typically
    customerName: z.string().min(1, 'Customer name is required'),
    email: z.string().email('Invalid email address').optional().or(z.literal('')),
    phone: z.string().optional(),
    address: z.string().optional(),
    city: z.string().optional(),
    country: z.string().optional(),
    creditLimit: z.coerce.number().min(0, 'Credit limit must be positive'),
    paymentTermsDays: z.coerce.number().min(0, 'Payment terms must be positive'),
    paymentTermId: z.string().optional(),
    priceGroup: z.string().optional(),
    currencyCode: z.string().min(3, 'Currency is required'),
});

type CustomerFormValues = z.infer<typeof customerSchema>;

export default function EditCustomerPage() {
    const router = useRouter();
    const params = useParams();
    const id = params.id as string;
    const { toast } = useToast();
    const queryClient = useQueryClient();
    const [isSubmitting, setIsSubmitting] = useState(false);

    // In a real app, this would come from a useTenant() hook or similar global state
    const [baseCurrencySymbol, setBaseCurrencySymbol] = useState('GHS');
    const { data: paymentTerms = [], isLoading: paymentTermsLoading } = useQuery({
        queryKey: ['payment-terms', 'Customer'],
        queryFn: () => paymentTermService.getByApplicableTo('Customer'),
    });

    const { data: customer, isLoading } = useQuery({
        queryKey: ['customer', id],
        queryFn: () => arService.getCustomer(id),
    });

    const form = useForm<CustomerFormValues>({
        resolver: zodResolver(customerSchema) as any,
        defaultValues: {
            customerCode: '',
            customerName: '',
            email: '',
            phone: '',
            address: '',
            city: '',
            country: '',
            creditLimit: 0,
            paymentTermsDays: 30,
            paymentTermId: 'none',
            priceGroup: 'Standard',
            currencyCode: 'GHS'
        },
    });

    const formatPaymentTerm = (term: PaymentTermListDto) => {
        const discountText = term.discountPercent && term.discountDays
            ? `, ${term.discountPercent}% if paid in ${term.discountDays} days`
            : '';
        return `${term.code} - ${term.name} (${term.dueDays} days${discountText})`;
    };

    const handlePaymentTermChange = (value: string) => {
        form.setValue('paymentTermId', value);
        const selectedTerm = paymentTerms.find(term => term.id === value);
        if (selectedTerm) {
            form.setValue('paymentTermsDays', selectedTerm.dueDays);
        }
    };

    // Reset form when customer data loads
    useEffect(() => {
        if (customer) {
            form.reset({
                customerCode: customer.customerCode,
                customerName: customer.customerName,
                email: customer.email || '',
                phone: customer.phone || '',
                address: customer.address || '',
                city: customer.city || '',
                country: customer.country || '',
                creditLimit: customer.creditLimit,
                paymentTermsDays: customer.paymentTermsDays,
                paymentTermId: customer.paymentTermId || 'none',
                priceGroup: customer.priceGroup || 'Standard',
                currencyCode: customer.currencyCode
            });
        }
    }, [customer, form]);

    const onSubmit = async (data: CustomerFormValues) => {
        if (!customer) return;
        setIsSubmitting(true);
        try {
            const payload: CustomerUpdateRequest = {
                id: customer.id,
                ...data,
                creditLimit: Number(data.creditLimit),
                paymentTermsDays: Number(data.paymentTermsDays),
                paymentTermId: data.paymentTermId === 'none' ? null : data.paymentTermId,
                isActive: customer.isActive // Preserve existing status
            };

            await arService.updateCustomer(id, payload);

            // Invalidate queries
            await queryClient.invalidateQueries({ queryKey: ['customer', id] });
            await queryClient.invalidateQueries({ queryKey: ['customers'] });

            toast({
                title: 'Success',
                description: 'Customer updated successfully',
            });

            router.push(`/finance/ar/customers/${id}`);
        } catch (error: any) {
            toast({
                title: 'Error',
                description: error.message || 'Failed to update customer',
                variant: 'destructive',
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    if (isLoading) {
        return <div className="p-8 space-y-4 max-w-2xl mx-auto">
            <Skeleton className="h-10 w-1/3" />
            <Card>
                <CardHeader>
                    <Skeleton className="h-6 w-1/4" />
                    <Skeleton className="h-4 w-1/2" />
                </CardHeader>
                <CardContent className="space-y-4">
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                    <Skeleton className="h-10 w-full" />
                </CardContent>
            </Card>
        </div>;
    }

    if (!customer) {
        return <div className="p-8 text-center">Customer not found</div>;
    }

    return (
        <div className="space-y-8 p-8 max-w-2xl mx-auto">
            <div className="flex items-center space-x-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ArrowLeft className="h-4 w-4" />
                </Button>
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">Edit Customer</h1>
                    <p className="text-muted-foreground">
                        Update customer information.
                    </p>
                </div>
            </div>

            <Card>
                <form onSubmit={form.handleSubmit(onSubmit)}>
                    <CardHeader>
                        <CardTitle>Customer Details</CardTitle>
                        <CardDescription>
                            Update the information for {customer.customerName}.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="customerCode">Customer Code</Label>
                                <Input
                                    id="customerCode"
                                    {...form.register('customerCode')}
                                    disabled // Customer Code usually shouldn't be changed
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="customerName">Customer Name</Label>
                                <Input
                                    id="customerName"
                                    {...form.register('customerName')}
                                />
                                {form.formState.errors.customerName && (
                                    <p className="text-sm text-red-500">{form.formState.errors.customerName.message}</p>
                                )}
                            </div>
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="currencyCode">Currency</Label>
                                <Select
                                    onValueChange={(value) => form.setValue('currencyCode', value)}
                                    value={form.watch('currencyCode')}
                                    disabled // Changing currency might be restricted if transactions exist
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select currency" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="GHS">GHS - Ghana Cedi</SelectItem>
                                        <SelectItem value="USD">USD - US Dollar</SelectItem>
                                        <SelectItem value="EUR">EUR - Euro</SelectItem>
                                        <SelectItem value="GBP">GBP - British Pound</SelectItem>
                                    </SelectContent>
                                </Select>
                                <p className="text-xs text-muted-foreground">Currency cannot be changed once transactions exist.</p>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="creditLimit">Credit Limit ({baseCurrencySymbol})</Label>
                                <div className="relative">
                                    <span className="absolute left-3 top-2.5 text-gray-500">{baseCurrencySymbol}</span>
                                    <Input
                                        id="creditLimit"
                                        type="number"
                                        className="pl-7"
                                        {...form.register('creditLimit')}
                                    />
                                </div>
                                <p className="text-xs text-muted-foreground">Tracked in Base Currency.</p>
                                {form.formState.errors.creditLimit && (
                                    <p className="text-sm text-red-500">{form.formState.errors.creditLimit.message}</p>
                                )}
                            </div>
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="email">Email</Label>
                                <Input
                                    id="email"
                                    type="email"
                                    {...form.register('email')}
                                />
                                {form.formState.errors.email && (
                                    <p className="text-sm text-red-500">{form.formState.errors.email.message}</p>
                                )}
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="phone">Phone</Label>
                                <Input
                                    id="phone"
                                    {...form.register('phone')}
                                />
                            </div>
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="address">Address</Label>
                            <Input
                                id="address"
                                {...form.register('address')}
                            />
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="city">City</Label>
                                <Input
                                    id="city"
                                    {...form.register('city')}
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="country">Country</Label>
                                <Input
                                    id="country"
                                    {...form.register('country')}
                                />
                            </div>
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="paymentTermId">Payment Term</Label>
                                <Select
                                    onValueChange={handlePaymentTermChange}
                                    value={form.watch('paymentTermId') || 'none'}
                                >
                                    <SelectTrigger id="paymentTermId">
                                        <SelectValue placeholder={paymentTermsLoading ? 'Loading terms...' : 'Select payment term'} />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="none">Manual / No configured term</SelectItem>
                                        {paymentTerms.map(term => (
                                            <SelectItem key={term.id} value={term.id}>
                                                {formatPaymentTerm(term)}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                                <Label htmlFor="paymentTermsDays" className="text-xs text-muted-foreground">Due Days</Label>
                                <Input
                                    id="paymentTermsDays"
                                    type="number"
                                    {...form.register('paymentTermsDays')}
                                />
                                <p className="text-xs text-muted-foreground">The selected finance term controls default due-date behavior for AR invoices.</p>
                                {form.formState.errors.paymentTermsDays && (
                                    <p className="text-sm text-red-500">{form.formState.errors.paymentTermsDays.message}</p>
                                )}
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="priceGroup">Price Group</Label>
                                <Select
                                    onValueChange={(value) => form.setValue('priceGroup', value)}
                                    value={form.watch('priceGroup')}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select price group" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Standard">Standard</SelectItem>
                                        <SelectItem value="Wholesale">Wholesale</SelectItem>
                                        <SelectItem value="VIP">VIP</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>

                    </CardContent>
                    <CardFooter className="flex justify-end space-x-2">
                        <Button variant="outline" type="button" onClick={() => router.back()}>
                            Cancel
                        </Button>
                        <Button type="submit" disabled={isSubmitting}>
                            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Save Changes
                        </Button>
                    </CardFooter>
                </form>
            </Card>
        </div>
    );
}
