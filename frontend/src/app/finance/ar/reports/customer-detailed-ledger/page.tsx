'use client';

import { useEffect, useState } from 'react';
import {
    DetailedLedgerReport,
    LedgerPartnerOption,
    PartnerDetailedLedgerReport,
} from '@/components/finance/PartnerDetailedLedgerReport';
import { arService } from '@/services/ar-service';
import { useAuth } from '@/hooks/use-auth';

export default function CustomerDetailedLedgerPage() {
    const { hasPermission } = useAuth();
    const [partners, setPartners] = useState<LedgerPartnerOption[]>([]);
    const [partnersLoading, setPartnersLoading] = useState(true);

    useEffect(() => {
        let isMounted = true;

        const loadCustomers = async () => {
            setPartnersLoading(true);
            try {
                const result = await arService.getCustomers({ page: 1, pageSize: 500, includeBalances: false });
                if (!isMounted) return;

                setPartners(
                    result.items
                        .map((customer) => ({
                            id: customer.id,
                            code: customer.customerCode,
                            name: customer.customerName,
                            currencyCode: customer.currencyCode,
                        }))
                        .sort((a, b) => a.name.localeCompare(b.name))
                );
            } catch (error) {
                console.error('Failed to load customer partners', error);
                if (isMounted) setPartners([]);
            } finally {
                if (isMounted) setPartnersLoading(false);
            }
        };

        loadCustomers();

        return () => {
            isMounted = false;
        };
    }, []);

    return (
        <PartnerDetailedLedgerReport
            title="Customer Detailed Ledger"
            description="Review opening balances, AR movements, and closing balances by customer."
            partnerLabel="Customer"
            partnerPluralLabel="Customers"
            currencyToggleLabel="Show transactions in customer currency where available"
            exportFilePrefix="customer-detailed-ledger"
            backHref="/finance/ar/reports"
            partners={partners}
            partnersLoading={partnersLoading}
            canExport={hasPermission('Finance.Reports.Export')}
            loadReport={async (params): Promise<DetailedLedgerReport> => {
                const report = await arService.getCustomerDetailedLedger({
                    fromDate: params.fromDate,
                    toDate: params.toDate,
                    businessPartnerIds: params.partnerIds,
                    showCustomerCurrency: params.showPartnerCurrency,
                });

                return {
                    fromDate: report.fromDate,
                    toDate: report.toDate,
                    currencyCode: report.currencyCode,
                    totalOpeningBalance: report.totalOpeningBalance,
                    totalDebits: report.totalDebits,
                    totalCredits: report.totalCredits,
                    totalClosingBalance: report.totalClosingBalance,
                    warnings: report.warnings ?? [],
                    accounts: report.customers.map((customer) => ({
                        id: customer.businessPartnerId,
                        code: customer.customerCode,
                        name: customer.customerName,
                        currencyCode: customer.currencyCode,
                        openingBalance: customer.openingBalance,
                        totalDebits: customer.totalDebits,
                        totalCredits: customer.totalCredits,
                        closingBalance: customer.closingBalance,
                        lines: customer.lines,
                    })),
                };
            }}
            downloadPdf={(params) => arService.downloadCustomerStatementPdf({
                fromDate: params.fromDate,
                toDate: params.toDate,
                businessPartnerIds: params.partnerIds,
                showCustomerCurrency: params.showPartnerCurrency,
            })}
            printPdf={(params) => arService.printCustomerStatement({
                fromDate: params.fromDate,
                toDate: params.toDate,
                businessPartnerIds: params.partnerIds,
                showCustomerCurrency: params.showPartnerCurrency,
            })}
        />
    );
}
