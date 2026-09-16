'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  DetailedLedgerReport,
  PartnerDetailedLedgerReport,
} from '@/components/finance/PartnerDetailedLedgerReport';
import { Button } from '@/components/ui/button';
import { useTenant } from '@/contexts/TenantContext';
import {
  apSupplierDetailedLedgerQueryKey,
  toApLedgerSupplierOptions,
} from '@/lib/finance/ap-supplier-detailed-ledger';
import { accountsPayableService } from '@/services/accountsPayableService';
import { useAuth } from '@/hooks/use-auth';

export default function SupplierDetailedLedgerPage() {
  const { hasPermission } = useAuth();
  const { currentTenantCode, isLoadingTenants } = useTenant();
  const {
    data: suppliers = [],
    isLoading: suppliersLoading,
    isError: suppliersFailed,
    error: suppliersError,
    refetch: refetchSuppliers,
  } = useQuery({
    queryKey: apSupplierDetailedLedgerQueryKey(currentTenantCode),
    queryFn: () => accountsPayableService.getInvoiceSupplierEntryOptions(),
    enabled: !isLoadingTenants && Boolean(currentTenantCode),
  });
  const partners = useMemo(
    () => toApLedgerSupplierOptions(suppliers),
    [suppliers]
  );
  const supplierLookupError =
    !isLoadingTenants && !currentTenantCode
      ? 'Choose a tenant before loading Finance suppliers.'
      : suppliersFailed
        ? suppliersError instanceof Error
          ? suppliersError.message
          : 'Finance suppliers could not be loaded.'
        : undefined;

  return (
    <>
      {supplierLookupError ? (
        <div
          role="alert"
          className="mx-auto mt-6 flex max-w-[1536px] items-center justify-between gap-4 rounded-md border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          <p>{supplierLookupError}</p>
          {suppliersFailed ? (
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => void refetchSuppliers()}
            >
              Retry supplier lookup
            </Button>
          ) : null}
        </div>
      ) : null}
      <PartnerDetailedLedgerReport
        key={currentTenantCode ?? 'missing-tenant'}
        title="Supplier Detailed Ledger"
        description="Review opening balances, AP movements, and closing balances by supplier."
        partnerLabel="Supplier"
        partnerPluralLabel="Suppliers"
        currencyToggleLabel="Show transactions in supplier currency where available"
        exportFilePrefix="supplier-detailed-ledger"
        backHref="/finance/ap/reports"
        partners={partners}
        partnersLoading={
          isLoadingTenants || suppliersLoading || !currentTenantCode
        }
        canExport={hasPermission('Finance.Reports.Export')}
        loadReport={async (params): Promise<DetailedLedgerReport> => {
          const report = await accountsPayableService.getSupplierDetailedLedger(
            {
              fromDate: params.fromDate,
              toDate: params.toDate,
              supplierIds: params.partnerIds,
              showSupplierCurrency: params.showPartnerCurrency,
            }
          );

          return {
            fromDate: report.fromDate,
            toDate: report.toDate,
            currencyCode: report.currencyCode,
            totalOpeningBalance: report.totalOpeningBalance,
            totalDebits: report.totalDebits,
            totalCredits: report.totalCredits,
            totalClosingBalance: report.totalClosingBalance,
            warnings: report.warnings ?? [],
            accounts: report.suppliers.map((supplier) => ({
              id: supplier.supplierId,
              code: supplier.supplierCode,
              name: supplier.supplierName,
              currencyCode: supplier.currencyCode,
              openingBalance: supplier.openingBalance,
              totalDebits: supplier.totalDebits,
              totalCredits: supplier.totalCredits,
              closingBalance: supplier.closingBalance,
              lines: supplier.lines,
            })),
          };
        }}
        downloadPdf={(params) => accountsPayableService.downloadSupplierStatementDocument({
          fromDate: params.fromDate,
          toDate: params.toDate,
          supplierIds: params.partnerIds,
          showSupplierCurrency: params.showPartnerCurrency,
          format: 'pdf',
        })}
        printPdf={(params) => accountsPayableService.printSupplierStatementDocument({
          fromDate: params.fromDate,
          toDate: params.toDate,
          supplierIds: params.partnerIds,
          showSupplierCurrency: params.showPartnerCurrency,
        })}
      />
    </>
  );
}
