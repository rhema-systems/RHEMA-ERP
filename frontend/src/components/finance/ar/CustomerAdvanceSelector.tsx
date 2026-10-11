'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  buildCustomerAdvanceApplicationUrl,
  isEligibleCustomerAdvance,
} from '@/lib/finance/ar-customer-advance';
import { formatCurrency } from '@/lib/utils';
import { arService } from '@/services/ar-service';

interface CustomerOption {
  id: string;
  customerName: string;
}

interface CustomerAdvanceSelectorProps {
  customers: CustomerOption[];
  selectedCustomerId: string;
  preselectedInvoiceId?: string | null;
  disabled?: boolean;
  onCustomerChange: (businessPartnerId: string) => void;
  onContinue: (url: string) => void;
}

export function CustomerAdvanceSelector({
  customers,
  selectedCustomerId,
  preselectedInvoiceId,
  disabled = false,
  onCustomerChange,
  onContinue,
}: CustomerAdvanceSelectorProps) {
  const [selectedAdvanceId, setSelectedAdvanceId] = useState('');
  const {
    data: payments,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['customer-advance-candidates', selectedCustomerId],
    queryFn: () =>
      arService.getPayments({
        businessPartnerId: selectedCustomerId,
        status: 'Posted',
        hasUnallocatedAmount: true,
        isCustomerAdvance: true,
        page: 1,
        pageSize: 100,
      }),
    enabled: Boolean(selectedCustomerId),
  });

  const advances = useMemo(
    () =>
      (payments?.items ?? []).filter((payment) =>
        isEligibleCustomerAdvance(payment, selectedCustomerId)
      ),
    [payments?.items, selectedCustomerId]
  );
  const selectedAdvance = advances.find(
    (payment) => payment.id === selectedAdvanceId
  );

  useEffect(() => {
    setSelectedAdvanceId('');
  }, [selectedCustomerId]);

  return (
    <Card className="max-w-3xl">
      <CardHeader>
        <CardTitle>Apply payment on account</CardTitle>
        <p className="text-sm text-muted-foreground">
          Select the exact posted customer-advance lot to apply. This does not
          record new cash.
        </p>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="space-y-2">
          <Label>Customer</Label>
          <Select
            value={selectedCustomerId}
            onValueChange={onCustomerChange}
            disabled={disabled}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select customer" />
            </SelectTrigger>
            <SelectContent>
              {customers.map((customer) => (
                <SelectItem key={customer.id} value={customer.id}>
                  {customer.customerName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-2">
          <Label>Available payment on account</Label>
          <Select
            value={selectedAdvanceId}
            onValueChange={setSelectedAdvanceId}
            disabled={
              disabled ||
              !selectedCustomerId ||
              isLoading ||
              advances.length === 0
            }
          >
            <SelectTrigger>
              <SelectValue
                placeholder={
                  isLoading
                    ? 'Loading available advances…'
                    : 'Select an advance lot'
                }
              />
            </SelectTrigger>
            <SelectContent>
              {advances.map((payment) => (
                <SelectItem key={payment.id} value={payment.id}>
                  {payment.paymentNumber} ·{' '}
                  {format(new Date(payment.paymentDate), 'dd MMM yyyy')} ·{' '}
                  {formatCurrency(
                    payment.unallocatedAmount,
                    payment.currencyCode
                  )}{' '}
                  available
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {isLoading && (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading posted advances…
            </p>
          )}
          {error && (
            <p className="text-sm text-destructive">
              Available customer advances could not be loaded. Refresh and try
              again.
            </p>
          )}
          {selectedCustomerId &&
            !isLoading &&
            !error &&
            advances.length === 0 && (
              <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
                This customer has no eligible posted, unapplied advance. Record
                a new receipt only if new money has actually been received.
              </p>
            )}
        </div>

        {selectedAdvance && (
          <div className="grid gap-3 rounded-md border bg-muted/30 p-4 text-sm sm:grid-cols-2">
            <div>
              <span className="text-muted-foreground">Receipt</span>
              <div className="font-medium">{selectedAdvance.paymentNumber}</div>
            </div>
            <div>
              <span className="text-muted-foreground">Receipt date</span>
              <div className="font-medium">
                {format(new Date(selectedAdvance.paymentDate), 'dd MMM yyyy')}
              </div>
            </div>
            <div>
              <span className="text-muted-foreground">Original amount</span>
              <div className="font-medium">
                {formatCurrency(
                  selectedAdvance.totalAmount,
                  selectedAdvance.currencyCode
                )}
              </div>
            </div>
            <div>
              <span className="text-muted-foreground">Available amount</span>
              <div className="font-medium text-emerald-700">
                {formatCurrency(
                  selectedAdvance.unallocatedAmount,
                  selectedAdvance.currencyCode
                )}
              </div>
            </div>
          </div>
        )}

        <div className="flex justify-end">
          <Button
            type="button"
            disabled={disabled || !selectedAdvance}
            onClick={() =>
              selectedAdvance &&
              onContinue(
                buildCustomerAdvanceApplicationUrl(
                  selectedAdvance.businessPartnerId,
                  selectedAdvance.id,
                  preselectedInvoiceId
                )
              )
            }
          >
            Continue to invoice allocation
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
