'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { formatCurrencyAmount, normalizeCurrencyCode } from '@/lib/currency';
import { currencyService, type CurrencyDetailDto } from '@/services/financeCommonService';

interface MaintenanceCurrencyState {
  code: string;
  name: string;
  decimalPlaces: number;
}

const DEFAULT_MAINTENANCE_CURRENCY: MaintenanceCurrencyState = {
  code: 'GHS',
  name: 'Ghana Cedi',
  decimalPlaces: 2,
};

function resolveCurrencyCode(currency?: CurrencyDetailDto | null): string {
  return normalizeCurrencyCode(currency?.code, DEFAULT_MAINTENANCE_CURRENCY.code) || DEFAULT_MAINTENANCE_CURRENCY.code;
}

export function useMaintenanceCurrency() {
  const [currency, setCurrency] = useState<MaintenanceCurrencyState>(DEFAULT_MAINTENANCE_CURRENCY);

  useEffect(() => {
    let cancelled = false;

    currencyService.getBaseCurrency()
      .then((baseCurrency) => {
        if (cancelled || !baseCurrency) {
          return;
        }

        setCurrency({
          code: resolveCurrencyCode(baseCurrency),
          name: baseCurrency.name || DEFAULT_MAINTENANCE_CURRENCY.name,
          decimalPlaces: typeof baseCurrency.decimalPlaces === 'number'
            ? baseCurrency.decimalPlaces
            : DEFAULT_MAINTENANCE_CURRENCY.decimalPlaces,
        });
      })
      .catch(() => {
        if (!cancelled) {
          setCurrency(DEFAULT_MAINTENANCE_CURRENCY);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const formatMoney = useCallback((value?: number | null, maximumFractionDigits?: number) => {
    const amount = typeof value === 'number' && Number.isFinite(value) ? value : 0;
    return formatCurrencyAmount(
      amount,
      currency.code,
      maximumFractionDigits ?? currency.decimalPlaces,
    );
  }, [currency.code, currency.decimalPlaces]);

  return useMemo(() => ({
    currencyCode: currency.code,
    currencyName: currency.name,
    currencyDecimalPlaces: currency.decimalPlaces,
    amountLabel: `Amount (${currency.code})`,
    costLabel: `Cost (${currency.code})`,
    assetValueLabel: `Asset Value (${currency.code})`,
    mileageRateLabel: `Mileage Rate (${currency.code}/mile)`,
    formatMoney,
  }), [currency.code, currency.name, currency.decimalPlaces, formatMoney]);
}
