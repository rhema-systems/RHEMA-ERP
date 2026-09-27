import { z } from 'zod';

const moduleSchema = z.enum(['AR', 'AP']);
const purposeSchema = z.enum(['StandardAdjustment', 'FinanceCharge', 'Writeoff', 'OverpaymentWriteoff']);

export const adjustmentSchema = z.object({
    module: moduleSchema,
    purpose: purposeSchema,
    businessPartnerId: z.string().min(1, 'Business Partner is required'),
    businessPartnerRoleId: z.string().optional(),
    adjustmentDate: z.string().min(1, 'Adjustment date is required'),
    dueDate: z.string().optional(),
    adjustmentType: z.enum(['Debit', 'Credit']),
    amount: z.number().min(0.01, 'Amount must be greater than zero'),
    currencyCode: z.string().min(3, 'Currency is required').max(3, 'Use a 3-letter currency code'),
    exchangeRate: z.number().min(0.000001, 'Exchange rate must be greater than zero'),
    contraAccountId: z.string(),
    reference: z.string().max(100).optional(),
    reason: z.string().min(1, 'Reason is required').max(500, 'Reason cannot exceed 500 characters'),
    notes: z.string().max(2000).optional(),
}).superRefine((value, ctx) => {
    if (!value.contraAccountId.trim()) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['contraAccountId'], message: 'Contra account is required' });
    }
});

export type AdjustmentFormValues = z.infer<typeof adjustmentSchema>;
