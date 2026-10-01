export type OpportunityCurrencyOption = {
  code: string;
  name: string;
  isBaseCurrency?: boolean;
};

const normalizeCurrencyCode = (value?: string | null) =>
  value?.trim().toUpperCase() || '';

export const resolveOpportunityCurrency = (
  sourceCurrency: string | null | undefined,
  configuredCurrencies: OpportunityCurrencyOption[],
  fallbackCurrency = 'GHS'
) => {
  const sourceCode = normalizeCurrencyCode(sourceCurrency);
  if (sourceCode) return sourceCode;

  const baseCurrency = configuredCurrencies.find(
    (currency) => currency.isBaseCurrency
  );
  return (
    normalizeCurrencyCode(baseCurrency?.code) ||
    normalizeCurrencyCode(configuredCurrencies[0]?.code) ||
    normalizeCurrencyCode(fallbackCurrency)
  );
};

export const buildOpportunityCurrencyOptions = (
  configuredCurrencies: OpportunityCurrencyOption[],
  selectedCurrency?: string | null
) => {
  const unique = new Map<string, OpportunityCurrencyOption>();
  configuredCurrencies.forEach((currency) => {
    const code = normalizeCurrencyCode(currency.code);
    if (code && !unique.has(code)) {
      unique.set(code, { ...currency, code });
    }
  });

  const selectedCode = normalizeCurrencyCode(selectedCurrency);
  if (selectedCode && !unique.has(selectedCode)) {
    unique.set(selectedCode, {
      code: selectedCode,
      name: 'Linked property currency',
    });
  }

  return Array.from(unique.values());
};
