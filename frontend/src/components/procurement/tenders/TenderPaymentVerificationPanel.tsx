'use client';

import React, { useState } from 'react';
import { format } from 'date-fns';
import { AlertCircle, CheckCircle2, RefreshCw, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import * as tenderBidService from '@/services/tenderBidService';
import type { TenderPaymentDto } from '@/services/tenderBidService';

export const TENDER_PAYMENT_VERIFY_PERMISSION =
  'procurement.tender.payment.verify';

type PaymentDecision = {
  payment: TenderPaymentDto;
  isApproved: boolean;
};

interface TenderPaymentVerificationPanelProps {
  bidId: string;
  payments: TenderPaymentDto[];
  canVerifyPayment: boolean;
  onRefresh: (updatedPayment: TenderPaymentDto) => Promise<void>;
}

const paymentStatusPresentation = (status: string) => {
  const normalized = status.trim().toLowerCase();
  if (normalized === 'pending') {
    return {
      label: 'Pending verification',
      className: 'border-amber-200 bg-amber-100 text-amber-800',
    };
  }
  if (normalized === 'verified') {
    return {
      label: 'Verified',
      className: 'border-green-200 bg-green-100 text-green-800',
    };
  }
  if (normalized === 'rejected') {
    return {
      label: 'Rejected',
      className: 'border-red-200 bg-red-100 text-red-800',
    };
  }
  return {
    label: status || 'Unknown',
    className: 'border-gray-200 bg-gray-100 text-gray-800',
  };
};

const formatPaymentDate = (dateString?: string) => {
  if (!dateString) return 'Not recorded';
  try {
    return format(new Date(dateString), 'PPP p');
  } catch {
    return dateString;
  }
};

const formatPaymentAmount = (amount: number, currency: string) => {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: currency || 'GHS',
      currencyDisplay: 'code',
    }).format(amount);
  } catch {
    return `${currency || 'GHS'} ${amount.toLocaleString(undefined, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;
  }
};

export function TenderPaymentVerificationPanel({
  bidId,
  payments,
  canVerifyPayment,
  onRefresh,
}: TenderPaymentVerificationPanelProps) {
  const [decision, setDecision] = useState<PaymentDecision | null>(null);
  const [notes, setNotes] = useState('');
  const [decisionError, setDecisionError] = useState<string | null>(null);
  const [verifying, setVerifying] = useState(false);
  const isFinanceRecovery = Boolean(
    decision?.isApproved &&
      decision.payment.status.trim().toLowerCase() === 'verified' &&
      !decision.payment.journalEntryId
  );

  const openDecision = (payment: TenderPaymentDto, isApproved: boolean) => {
    setDecision({ payment, isApproved });
    setNotes('');
    setDecisionError(null);
  };

  const closeDecision = () => {
    if (verifying) return;
    setDecision(null);
    setNotes('');
    setDecisionError(null);
  };

  const confirmDecision = async () => {
    if (!decision) return false;

    const trimmedNotes = notes.trim();
    if (!decision.isApproved && !trimmedNotes) {
      setDecisionError('A rejection reason is required.');
      return false;
    }

    try {
      setVerifying(true);
      setDecisionError(null);
      const updatedPayment = await tenderBidService.verifyBidPayment(
        bidId,
        decision.payment.id,
        {
          isApproved: decision.isApproved,
          notes: trimmedNotes || undefined,
        }
      );

      let refreshFailed = false;
      try {
        await onRefresh(updatedPayment);
      } catch (refreshError) {
        refreshFailed = true;
        console.error('Error refreshing tender fee payments:', refreshError);
      }

      const action = isFinanceRecovery
        ? 'posted to Finance'
        : decision.isApproved
          ? 'approved'
          : 'rejected';
      toast.success(`Tender fee payment ${action}.`);
      if (refreshFailed) {
        toast.error(
          'The decision was saved, but the payment list could not be refreshed. Reload this page to confirm the latest status.'
        );
      }
      setDecision(null);
      setNotes('');
      setDecisionError(null);
      return true;
    } catch (error) {
      console.error('Error verifying tender fee payment:', error);
      const message =
        error instanceof Error
          ? error.message
          : 'Failed to update the tender fee payment.';
      setDecisionError(message);
      toast.error(message);
      return false;
    } finally {
      setVerifying(false);
    }
  };

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>Tender Fee Payments</CardTitle>
          <CardDescription>
            Review the supplier&apos;s recorded tender fee payments and their
            verification status.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {payments.length === 0 ? (
            <div className="rounded-lg border border-dashed py-10 text-center text-sm text-gray-500">
              No tender fee payments have been recorded for this bid.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Payment reference</TableHead>
                    <TableHead>Amount</TableHead>
                    <TableHead>Method</TableHead>
                    <TableHead>Payment date</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Finance posting</TableHead>
                    {canVerifyPayment && (
                      <TableHead className="text-right">Actions</TableHead>
                    )}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {payments.map((payment) => {
                    const status = paymentStatusPresentation(payment.status);
                    const isPending =
                      payment.status.trim().toLowerCase() === 'pending';
                    const isVerifiedWithoutPosting =
                      payment.status.trim().toLowerCase() === 'verified' &&
                      !payment.journalEntryId;
                    return (
                      <TableRow key={payment.id}>
                        <TableCell>
                          <p className="font-medium">
                            {payment.paymentReference || 'Not provided'}
                          </p>
                          {payment.transactionId && (
                            <p className="text-xs text-gray-500">
                              Transaction: {payment.transactionId}
                            </p>
                          )}
                        </TableCell>
                        <TableCell className="font-medium">
                          {formatPaymentAmount(
                            payment.amount,
                            payment.currency
                          )}
                        </TableCell>
                        <TableCell>{payment.paymentMethod || '—'}</TableCell>
                        <TableCell>
                          {formatPaymentDate(payment.paymentDate)}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline" className={status.className}>
                            {status.label}
                          </Badge>
                          {payment.verifiedDate && (
                            <p className="mt-1 text-xs text-gray-500">
                              {payment.verifiedByName
                                ? `By ${payment.verifiedByName} · `
                                : ''}
                              {formatPaymentDate(payment.verifiedDate)}
                            </p>
                          )}
                        </TableCell>
                        <TableCell>
                          {payment.journalEntryId ? (
                            <div className="space-y-1">
                              <Badge
                                variant="outline"
                                className="border-green-200 bg-green-100 text-green-800"
                              >
                                Posted
                              </Badge>
                              <a
                                href={`/finance/journal-entries/${payment.journalEntryId}`}
                                className="block text-xs font-medium text-primary hover:underline"
                              >
                                View journal entry
                              </a>
                              {payment.postedAtUtc && (
                                <p className="text-xs text-gray-500">
                                  {formatPaymentDate(payment.postedAtUtc)}
                                </p>
                              )}
                            </div>
                          ) : payment.status.trim().toLowerCase() ===
                            'verified' ? (
                            <Badge
                              variant="outline"
                              className="border-amber-200 bg-amber-100 text-amber-800"
                            >
                              Posting pending
                            </Badge>
                          ) : (
                            <span className="text-gray-400">—</span>
                          )}
                        </TableCell>
                        {canVerifyPayment && (
                          <TableCell className="text-right">
                            {isPending ? (
                              <div className="flex justify-end gap-2">
                                <Button
                                  size="sm"
                                  onClick={() => openDecision(payment, true)}
                                  aria-label={`Approve payment ${payment.paymentReference}`}
                                >
                                  <CheckCircle2 className="mr-2 h-4 w-4" />
                                  Approve
                                </Button>
                                <Button
                                  size="sm"
                                  variant="destructive"
                                  onClick={() => openDecision(payment, false)}
                                  aria-label={`Reject payment ${payment.paymentReference}`}
                                >
                                  <XCircle className="mr-2 h-4 w-4" />
                                  Reject
                                </Button>
                              </div>
                            ) : isVerifiedWithoutPosting ? (
                              <Button
                                size="sm"
                                onClick={() => openDecision(payment, true)}
                                aria-label={`Post payment ${payment.paymentReference} to Finance`}
                              >
                                <RefreshCw className="mr-2 h-4 w-4" />
                                Post to Finance
                              </Button>
                            ) : (
                              <span className="text-sm text-gray-500">
                                Decision complete
                              </span>
                            )}
                          </TableCell>
                        )}
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={Boolean(decision)}
        onOpenChange={(open) => {
          if (!open) closeDecision();
        }}
        title={
          isFinanceRecovery
            ? 'Post tender fee payment to Finance'
            : decision?.isApproved
              ? 'Approve tender fee payment'
              : 'Reject tender fee payment'
        }
        description={
          decision
            ? `${isFinanceRecovery ? 'Create the missing Finance journal for' : decision.isApproved ? 'Approve' : 'Reject'} payment ${decision.payment.paymentReference || 'without a reference'} for ${formatPaymentAmount(decision.payment.amount, decision.payment.currency)}.`
            : undefined
        }
        confirmText={
          isFinanceRecovery
            ? 'Post to Finance'
            : decision?.isApproved
              ? 'Approve payment'
              : 'Reject payment'
        }
        cancelText="Cancel"
        variant={decision?.isApproved ? 'default' : 'destructive'}
        onConfirm={confirmDecision}
        isLoading={verifying}
      >
        <div className="space-y-3">
          <div className="space-y-2">
            <Label htmlFor="payment-decision-notes">
              Decision notes{decision?.isApproved ? ' (optional)' : ''}
            </Label>
            <Textarea
              id="payment-decision-notes"
              value={notes}
              onChange={(event) => {
                setNotes(event.target.value);
                if (decisionError) setDecisionError(null);
              }}
              placeholder={
                isFinanceRecovery
                  ? 'Add recovery notes for the audit record'
                  : decision?.isApproved
                    ? 'Add verification notes for the audit record'
                    : 'Enter the reason this payment is being rejected'
              }
              disabled={verifying}
            />
            {!decision?.isApproved && (
              <p className="text-xs text-gray-500">
                Required when rejecting a payment.
              </p>
            )}
          </div>
          {decisionError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Payment decision not completed</AlertTitle>
              <AlertDescription>{decisionError}</AlertDescription>
            </Alert>
          )}
        </div>
      </ConfirmationDialog>
    </>
  );
}
