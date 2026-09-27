import { z } from 'zod';

// API optional links are serialized as null; normalize them to omitted form values.
const optionalText = z.string().nullish().transform(value => value ?? undefined).optional();

const lineItemSchema = z.object({
  sourceLineId: z.string().uuid(),
  lineItemType: z
    .enum([
      'Expense',
      'Service',
      'Product',
      'Inventory',
      'Freight',
      'Miscellaneous',
      'FinanceCharge',
    ])
    .default('Expense'),
  glAccountId: optionalText,
  budgetEntryId: optionalText,
  inventoryItemId: optionalText,
  warehouseId: optionalText,
  purchaseOrderItemId: optionalText,
  description: z.string().min(1, 'Description is required'),
  quantity: z.coerce.number().min(0.0001, 'Quantity must be positive'),
  unitPrice: z.coerce.number().min(0, 'Unit price must be positive'),
  taxGroupId: optionalText,
  taxTreatment: z.coerce.number().min(1).max(5).optional(),
  discountPercentage: z.coerce.number().min(0).max(100).optional().default(0),
  unit: optionalText,
});

export const supplierInvoiceSchema = z
  .object({
    apAccountId: optionalText,
    expenseAccountId: optionalText,
    supplierId: z.string().min(1, 'Supplier is required'),
    supplierInvoiceNumber: optionalText,
    purchaseOrderId: optionalText,
    acceptedSupplyKind: z
      .enum([
        'GoodsReceiptInspection',
        'ServiceCompletion',
        'WorksPaymentCertificate',
        'GoodsReceiptConsolidation',
      ])
      .nullish()
      .transform(value => value ?? undefined)
      .optional(),
    acceptedSupplySourceId: optionalText,
    invoiceDate: z.date(),
    dueDate: z.date(),
    paymentTermId: optionalText,
    currencyCode: z.string().default('GHS'),
    exchangeRate: z.coerce.number().min(0.0001).optional().default(1.0),
    exchangeRateId: optionalText,
    exchangeRateDate: z.date().optional(),
    exchangeRateSource: optionalText.default('Daily'),
    notes: optionalText,
    reference: optionalText,
    taxGroupId: optionalText,
    withholdingTaxId: optionalText.default('none'),
    withholdingTaxRate: z.coerce.number().min(0).max(100).optional().default(0),
    isOpeningBalance: z.boolean().default(false),
    lineItems: z
      .array(lineItemSchema)
      .min(1, 'At least one line item is required'),
  })
  .refine(({ invoiceDate, dueDate }) => dueDate >= invoiceDate, {
    message: 'Due date cannot be earlier than the invoice date',
    path: ['dueDate'],
  });

export type SupplierInvoiceFormValues = z.infer<typeof supplierInvoiceSchema>;
