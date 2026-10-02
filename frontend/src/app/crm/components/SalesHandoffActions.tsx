'use client';

import Link from 'next/link';
import { FileText, ShoppingCart } from 'lucide-react';

import { Button } from '@/components/ui/button';
import type {
  SalesSaleableItemDto,
  SalesSaleableSourceDto,
} from '@/services/salesSetupService';
import { buildSaleableSourceParams } from '@/app/sales/components/SaleableSourceQuickStart';

export interface LockedPropertyEnquirySalesOrderSource {
  propertyEnquiryId: string;
  assetType: string;
  source: SalesSaleableSourceDto;
  item: SalesSaleableItemDto;
}

export interface CrmSalesHandoffContext {
  businessPartnerId?: string;
  businessPartnerName?: string;
  leadId?: string;
  leadName?: string;
  opportunityId?: string;
  opportunityName?: string;
  quoteId?: string;
  quoteName?: string;
  currency?: string;
  estimatedValue?: number;
  propertyReference?: string;
  propertyType?: string;
  contextLabel?: string;
  lockedPropertyEnquirySalesOrderSource?: LockedPropertyEnquirySalesOrderSource;
}

interface SalesHandoffActionsProps {
  context: CrmSalesHandoffContext;
  size?: 'default' | 'sm';
  variant?: 'default' | 'outline';
  showUnavailableHint?: boolean;
  showSalesOrder?: boolean;
  showSalesAgreement?: boolean;
}

const appendIfPresent = (params: URLSearchParams, key: string, value?: string | number) => {
  if (value !== undefined && value !== null && String(value).trim()) {
    params.set(key, String(value));
  }
};

const buildSalesHref = (basePath: string, context: CrmSalesHandoffContext) => {
  const lockedSource = basePath === '/sales/orders/create'
    ? context.lockedPropertyEnquirySalesOrderSource
    : undefined;
  const params = new URLSearchParams(
    lockedSource
      ? buildSaleableSourceParams(
          lockedSource.item,
          {
            propertyEnquiryId: lockedSource.propertyEnquiryId,
            propertyEnquiryAssetType: lockedSource.assetType,
            saleableSourceLocked: 'true',
          },
          lockedSource.source,
        )
      : undefined,
  );

  appendIfPresent(params, 'customerId', context.businessPartnerId);
  appendIfPresent(params, 'customerName', context.businessPartnerName);
  appendIfPresent(params, 'leadId', context.leadId);
  appendIfPresent(params, 'leadName', context.leadName);
  appendIfPresent(params, 'opportunityId', context.opportunityId);
  appendIfPresent(params, 'opportunityName', context.opportunityName);
  appendIfPresent(params, 'quoteId', context.quoteId);
  appendIfPresent(params, 'quoteName', context.quoteName);
  appendIfPresent(params, 'currency', context.currency);
  appendIfPresent(params, 'estimatedValue', context.estimatedValue);
  appendIfPresent(params, 'propertyReference', context.propertyReference);
  appendIfPresent(params, 'propertyType', context.propertyType);
  appendIfPresent(params, 'crmContext', context.contextLabel);

  return `${basePath}?${params.toString()}`;
};

export function SalesHandoffActions({
  context,
  size = 'default',
  variant = 'outline',
  showUnavailableHint = true,
  showSalesOrder = true,
  showSalesAgreement = true,
}: SalesHandoffActionsProps) {
  if (!context.businessPartnerId) {
    return showUnavailableHint ? (
      <div className="rounded-md border border-dashed px-3 py-2 text-xs text-muted-foreground">
        Link this CRM record to a BusinessPartner account before starting a sales document.
      </div>
    ) : null;
  }

  return (
    <div className="flex flex-wrap gap-2">
      {showSalesOrder ? (
        <Button asChild size={size} variant={variant}>
          <Link href={buildSalesHref('/sales/orders/create', context)}>
            <ShoppingCart className="mr-2 h-4 w-4" />
            Sales Order
          </Link>
        </Button>
      ) : null}
      {showSalesAgreement ? (
        <Button asChild size={size} variant="outline">
          <Link href={buildSalesHref('/sales/agreements/create', context)}>
            <FileText className="mr-2 h-4 w-4" />
            Sales Agreement
          </Link>
        </Button>
      ) : null}
    </div>
  );
}
