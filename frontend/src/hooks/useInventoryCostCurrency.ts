'use client';

import { useEffect, useState } from 'react';
import { procurementCurrencyService } from '@/services/financeCommonService';

// Use the inventory/procurement reference projection, not Finance-only setup access.
export function useInventoryCostCurrency() {
  const [currency, setCurrency] = useState<string>();
  useEffect(() => {
    let active = true;
    procurementCurrencyService.getActive().then(currencies => {
      if (active) setCurrency(currencies.find(value => value.isBaseCurrency)?.code);
    }).catch(() => { if (active) setCurrency(undefined); });
    return () => { active = false; };
  }, []);
  return currency;
}
