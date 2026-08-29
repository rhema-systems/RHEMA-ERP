import type {
  SupplierDebitNoteApplication,
  SupplierDebitNoteLineRequest,
} from '@/types/ap';
import type { TaxGroup } from '@/types/tax';

export function calculateSupplierDebitNoteLine(
  line: SupplierDebitNoteLineRequest,
  taxGroup?: TaxGroup
): SupplierDebitNoteLineRequest {
  const gross = roundMoney(
    Math.max(Number(line.quantity) || 0, 0) *
      Math.max(Number(line.unitPrice) || 0, 0)
  );
  const discountPercentage = Math.max(Number(line.discountPercentage) || 0, 0);
  const discountAmount = roundMoney((gross * discountPercentage) / 100);
  const net = roundMoney(gross - discountAmount);
  let cumulativeBase = net;
  let taxAmount = 0;

  for (const component of [...(taxGroup?.components ?? [])].sort(
    (a, b) => a.calculationOrder - b.calculationOrder
  )) {
    if (component.taxCategory === 'Withholding') continue;
    const taxableBasis =
      component.compoundBasis === 'Cumulative' ? cumulativeBase : net;
    const componentAmount = roundMoney(
      (taxableBasis * Number(component.taxRate)) / 100
    );
    taxAmount += componentAmount;
    if (
      component.compoundBasis === 'Cumulative' ||
      component.compoundBasis === 'BaseOnly'
    ) {
      cumulativeBase += componentAmount;
    }
  }

  taxAmount = roundMoney(taxAmount);
  return {
    ...line,
    taxRate: net > 0 ? roundRate((taxAmount / net) * 100) : 0,
    taxAmount,
    discountAmount,
    lineTotal: roundMoney(net + taxAmount),
  };
}

export function effectiveSupplierDebitNoteApplications(
  applications: SupplierDebitNoteApplication[] = []
): SupplierDebitNoteApplication[] {
  const reversedIds = new Set(
    applications
      .filter((item) => item.isReversal && item.originalApplicationId)
      .map((item) => item.originalApplicationId as string)
  );
  return applications.filter(
    (item) => !item.isReversal && !reversedIds.has(item.id)
  );
}

export function supplierDebitNoteApplicationLimit(
  invoiceBalanceAmount: number,
  debitNoteRemainingAmount: number
): number {
  return roundMoney(
    Math.max(Math.min(invoiceBalanceAmount, debitNoteRemainingAmount), 0)
  );
}

function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function roundRate(value: number): number {
  return Math.round((value + Number.EPSILON) * 1_000_000) / 1_000_000;
}
