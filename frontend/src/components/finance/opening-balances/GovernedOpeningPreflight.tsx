import React from 'react';
import { AlertCircle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import type {
  GovernedOpeningBalanceOptions,
  SubledgerOpeningBalanceReadiness,
} from '@/types/finance';
import type { OpeningStockOptions } from '@/lib/finance/opening-balance-governance';

type PreflightBadgeVariant =
  | 'default'
  | 'secondary'
  | 'destructive'
  | 'outline';

interface PreflightStatus {
  label: string;
  detail: string;
  variant: PreflightBadgeVariant;
}

interface GovernedOpeningPreflightProps {
  subledgerReadiness?: SubledgerOpeningBalanceReadiness;
  subledgerReadinessLoading: boolean;
  subledgerReadinessError: boolean;
  financeOptions?: GovernedOpeningBalanceOptions;
  financeOptionsLoading: boolean;
  financeOptionsError: boolean;
  financeHeaderComplete: boolean;
  inventoryOptions?: OpeningStockOptions;
  inventoryOptionsLoading: boolean;
  inventoryOptionsError: boolean;
  inventoryAvailable: boolean;
}

function subledgerStatus(
  family: string,
  total: number | undefined,
  posted: number | undefined,
  loading: boolean,
  error: boolean
): PreflightStatus {
  if (loading) {
    return {
      label: 'Loading',
      detail: `${family} readiness is loading.`,
      variant: 'secondary',
    };
  }
  if (error) {
    return {
      label: 'Unavailable',
      detail: `Confirm ${family} readiness before a coordinated run.`,
      variant: 'destructive',
    };
  }
  if (total === undefined || posted === undefined) {
    return {
      label: 'Waiting',
      detail: `${family} readiness has not loaded.`,
      variant: 'secondary',
    };
  }
  if (total === 0) {
    return {
      label: 'Confirm scope',
      detail: `No ${family} opening evidence is recorded.`,
      variant: 'outline',
    };
  }
  if (posted >= total) {
    return {
      label: 'Ready',
      detail: `${posted}/${total} posted.`,
      variant: 'default',
    };
  }
  return {
    label: 'Incomplete',
    detail: `${posted}/${total} posted; stop if ${family} is required.`,
    variant: 'destructive',
  };
}

function financeStatus({
  financeOptions,
  loading,
  error,
  headerComplete,
}: {
  financeOptions?: GovernedOpeningBalanceOptions;
  loading: boolean;
  error: boolean;
  headerComplete: boolean;
}): PreflightStatus {
  if (!headerComplete) {
    return {
      label: 'Waiting',
      detail: 'Complete opening date, period and book.',
      variant: 'secondary',
    };
  }
  if (loading) {
    return {
      label: 'Loading',
      detail: 'Dated Finance options are loading.',
      variant: 'secondary',
    };
  }
  if (error) {
    return {
      label: 'Unavailable',
      detail: 'Dated Finance options could not be loaded.',
      variant: 'destructive',
    };
  }
  if (!financeOptions) {
    return {
      label: 'Waiting',
      detail: 'Dated Finance options have not loaded.',
      variant: 'secondary',
    };
  }
  if (financeOptions.blockers.length > 0) {
    return {
      label: 'Review',
      detail: `${financeOptions.blockers.length} source-specific blocker(s); confirm each required Finance source.`,
      variant: 'destructive',
    };
  }
  return {
    label: 'Ready',
    detail: 'Dated server options are loaded.',
    variant: 'default',
  };
}

function inventoryStatus({
  inventoryOptions,
  loading,
  error,
  available,
}: {
  inventoryOptions?: OpeningStockOptions;
  loading: boolean;
  error: boolean;
  available: boolean;
}): PreflightStatus {
  if (!available) {
    return {
      label: 'Confirm owner',
      detail: 'Ask the Inventory owner only when this source is in scope.',
      variant: 'outline',
    };
  }
  if (loading) {
    return {
      label: 'Loading',
      detail: 'Inventory readiness is loading.',
      variant: 'secondary',
    };
  }
  if (error) {
    return {
      label: 'Unavailable',
      detail: 'Stop if Inventory is required in this opening pack.',
      variant: 'destructive',
    };
  }
  if (!inventoryOptions) {
    return {
      label: 'Waiting',
      detail: 'Inventory readiness has not loaded.',
      variant: 'secondary',
    };
  }
  if (!inventoryOptions.isReady) {
    return {
      label: 'Blocked if in scope',
      detail: `${inventoryOptions.blockers.length} blocker(s) returned; stop if Inventory is required.`,
      variant: 'destructive',
    };
  }
  return {
    label: 'Ready if in scope',
    detail: 'Eligible Inventory evidence options are loaded.',
    variant: 'default',
  };
}

function PreflightItem({
  testId,
  title,
  status,
}: {
  testId: string;
  title: string;
  status: PreflightStatus;
}) {
  return (
    <div
      data-testid={testId}
      className="min-w-0 rounded-md border bg-background/60 p-3"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-sm font-medium">{title}</span>
        <Badge variant={status.variant}>{status.label}</Badge>
      </div>
      <p className="mt-2 text-xs text-muted-foreground">{status.detail}</p>
    </div>
  );
}

export function GovernedOpeningPreflight({
  subledgerReadiness,
  subledgerReadinessLoading,
  subledgerReadinessError,
  financeOptions,
  financeOptionsLoading,
  financeOptionsError,
  financeHeaderComplete,
  inventoryOptions,
  inventoryOptionsLoading,
  inventoryOptionsError,
  inventoryAvailable,
}: GovernedOpeningPreflightProps) {
  const items = [
    {
      testId: 'preflight-ap',
      title: 'AP opening',
      status: subledgerStatus(
        'AP',
        subledgerReadiness?.apOpeningInvoiceCount,
        subledgerReadiness?.postedApOpeningInvoiceCount,
        subledgerReadinessLoading,
        subledgerReadinessError
      ),
    },
    {
      testId: 'preflight-ar',
      title: 'AR opening',
      status: subledgerStatus(
        'AR',
        subledgerReadiness?.arOpeningInvoiceCount,
        subledgerReadiness?.postedArOpeningInvoiceCount,
        subledgerReadinessLoading,
        subledgerReadinessError
      ),
    },
    {
      testId: 'preflight-fixed-assets',
      title: 'Fixed assets',
      status: subledgerStatus(
        'Fixed assets',
        subledgerReadiness?.fixedAssetOpeningBookValueCount,
        subledgerReadiness?.postedFixedAssetOpeningBookValueCount,
        subledgerReadinessLoading,
        subledgerReadinessError
      ),
    },
    {
      testId: 'preflight-finance',
      title: 'Finance governed',
      status: financeStatus({
        financeOptions,
        loading: financeOptionsLoading,
        error: financeOptionsError,
        headerComplete: financeHeaderComplete,
      }),
    },
    {
      testId: 'preflight-inventory',
      title: 'Inventory opening',
      status: inventoryStatus({
        inventoryOptions,
        loading: inventoryOptionsLoading,
        error: inventoryOptionsError,
        available: inventoryAvailable,
      }),
    },
  ];

  return (
    <Alert>
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>Opening-pack preflight</AlertTitle>
      <AlertDescription className="space-y-3">
        <p>
          For a coordinated cutover, identify the required source families and
          resolve every required source before preparing the first governed
          source.
        </p>
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-5">
          {items.map((item) => (
            <PreflightItem key={item.testId} {...item} />
          ))}
        </div>
        <p className="text-xs">
          This summary is advisory, not a product-wide dependency. A source
          family excluded from this tenant&apos;s approved cutover does not
          block legitimate independent preparation.
        </p>
      </AlertDescription>
    </Alert>
  );
}
