'use client';

import { useEffect, useState } from 'react';
import {
    DetailedLedgerReport,
    LedgerPartnerOption,
    PartnerDetailedLedgerReport,
} from '@/components/finance/PartnerDetailedLedgerReport';
import { accountsPayableService } from '@/services/accountsPayableService';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';

const supplierPartnerTypes = new Set(['supplier', 'contractor', 'both']);

const isSupplierPartner = (partner: BusinessPartnerDto) =>
    supplierPartnerTypes.has((partner.partnerType ?? '').toLowerCase());

export default function SupplierDetailedLedgerPage() {
    const [partners, setPartners] = useState<LedgerPartnerOption[]>([]);
    const [partnersLoading, setPartnersLoading] = useState(true);

    useEffect(() => {
        let isMounted = true;

        const loadPartners = async () => {
            setPartnersLoading(true);
            try {
                const allPartners = await businessPartnerService.getAllPartnersForDropdown();
                if (!isMounted) return;

                setPartners(
                    allPartners
                        .filter(isSupplierPartner)
                        .map((partner) => ({
                            id: partner.id,
                            code: partner.partnerCode,
                            name: partner.partnerName,
                            currencyCode: partner.currency,
                        }))
                        .sort((a, b) => a.name.localeCompare(b.name))
                );
            } catch (error) {
                console.error('Failed to load supplier partners', error);
                if (isMounted) setPartners([]);
            } finally {
                if (isMounted) setPartnersLoading(false);
            }
        };

        loadPartners();

        return () => {
            isMounted = false;
        };
    }, []);

    return (
        <PartnerDetailedLedgerReport
            title="Supplier Detailed Ledger"
            description="Review opening balances, AP movements, and closing balances by supplier."
            partnerLabel="Supplier"
            partnerPluralLabel="Suppliers"
            currencyToggleLabel="Show transactions in supplier currency where available"
            exportFilePrefix="supplier-detailed-ledger"
            backHref="/finance/ap/reports"
            partners={partners}
            partnersLoading={partnersLoading}
            loadReport={async (params): Promise<DetailedLedgerReport> => {
                const report = await accountsPayableService.getSupplierDetailedLedger({
                    fromDate: params.fromDate,
                    toDate: params.toDate,
                    supplierIds: params.partnerIds,
                    showSupplierCurrency: params.showPartnerCurrency,
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
                    accounts: report.suppliers.map((supplier) => ({
                        id: supplier.businessPartnerId ?? supplier.supplierId,
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
        />
    );
}
