/**
 * ISO 4217 Currency Data
 * Curated list of ~50 most commonly used currencies
 * Source: ISO 4217 Standard
 */

export interface ISO4217Currency {
    code: string;
    name: string;
    symbol: string;
    decimalPlaces: number;
}

export const ISO_4217_CURRENCIES: ISO4217Currency[] = [
    // Major World Currencies
    { code: 'USD', name: 'US Dollar', symbol: '$', decimalPlaces: 2 },
    { code: 'EUR', name: 'Euro', symbol: '€', decimalPlaces: 2 },
    { code: 'GBP', name: 'British Pound', symbol: '£', decimalPlaces: 2 },
    { code: 'JPY', name: 'Japanese Yen', symbol: '¥', decimalPlaces: 0 },
    { code: 'CHF', name: 'Swiss Franc', symbol: 'CHF', decimalPlaces: 2 },
    { code: 'CAD', name: 'Canadian Dollar', symbol: 'C$', decimalPlaces: 2 },
    { code: 'AUD', name: 'Australian Dollar', symbol: 'A$', decimalPlaces: 2 },
    { code: 'NZD', name: 'New Zealand Dollar', symbol: 'NZ$', decimalPlaces: 2 },
    { code: 'CNY', name: 'Chinese Yuan', symbol: '¥', decimalPlaces: 2 },
    { code: 'HKD', name: 'Hong Kong Dollar', symbol: 'HK$', decimalPlaces: 2 },
    { code: 'SGD', name: 'Singapore Dollar', symbol: 'S$', decimalPlaces: 2 },

    // African Currencies
    { code: 'GHS', name: 'Ghana Cedi', symbol: '₵', decimalPlaces: 2 },
    { code: 'NGN', name: 'Nigerian Naira', symbol: '₦', decimalPlaces: 2 },
    { code: 'ZAR', name: 'South African Rand', symbol: 'R', decimalPlaces: 2 },
    { code: 'KES', name: 'Kenyan Shilling', symbol: 'KSh', decimalPlaces: 2 },
    { code: 'UGX', name: 'Ugandan Shilling', symbol: 'USh', decimalPlaces: 0 },
    { code: 'TZS', name: 'Tanzanian Shilling', symbol: 'TSh', decimalPlaces: 2 },
    { code: 'EGP', name: 'Egyptian Pound', symbol: 'E£', decimalPlaces: 2 },
    { code: 'MAD', name: 'Moroccan Dirham', symbol: 'MAD', decimalPlaces: 2 },
    { code: 'XOF', name: 'West African CFA Franc', symbol: 'CFA', decimalPlaces: 0 },
    { code: 'XAF', name: 'Central African CFA Franc', symbol: 'FCFA', decimalPlaces: 0 },
    { code: 'RWF', name: 'Rwandan Franc', symbol: 'FRw', decimalPlaces: 0 },
    { code: 'ETB', name: 'Ethiopian Birr', symbol: 'Br', decimalPlaces: 2 },

    // Asian Currencies
    { code: 'INR', name: 'Indian Rupee', symbol: '₹', decimalPlaces: 2 },
    { code: 'KRW', name: 'South Korean Won', symbol: '₩', decimalPlaces: 0 },
    { code: 'MYR', name: 'Malaysian Ringgit', symbol: 'RM', decimalPlaces: 2 },
    { code: 'THB', name: 'Thai Baht', symbol: '฿', decimalPlaces: 2 },
    { code: 'IDR', name: 'Indonesian Rupiah', symbol: 'Rp', decimalPlaces: 2 },
    { code: 'PHP', name: 'Philippine Peso', symbol: '₱', decimalPlaces: 2 },
    { code: 'VND', name: 'Vietnamese Dong', symbol: '₫', decimalPlaces: 0 },
    { code: 'PKR', name: 'Pakistani Rupee', symbol: '₨', decimalPlaces: 2 },
    { code: 'BDT', name: 'Bangladeshi Taka', symbol: '৳', decimalPlaces: 2 },
    { code: 'LKR', name: 'Sri Lankan Rupee', symbol: 'Rs', decimalPlaces: 2 },

    // Middle Eastern Currencies
    { code: 'AED', name: 'UAE Dirham', symbol: 'د.إ', decimalPlaces: 2 },
    { code: 'SAR', name: 'Saudi Riyal', symbol: '﷼', decimalPlaces: 2 },
    { code: 'QAR', name: 'Qatari Riyal', symbol: '﷼', decimalPlaces: 2 },
    { code: 'KWD', name: 'Kuwaiti Dinar', symbol: 'KD', decimalPlaces: 3 },
    { code: 'BHD', name: 'Bahraini Dinar', symbol: 'BD', decimalPlaces: 3 },
    { code: 'OMR', name: 'Omani Rial', symbol: '﷼', decimalPlaces: 3 },
    { code: 'ILS', name: 'Israeli Shekel', symbol: '₪', decimalPlaces: 2 },
    { code: 'TRY', name: 'Turkish Lira', symbol: '₺', decimalPlaces: 2 },

    // European Currencies (non-Euro)
    { code: 'SEK', name: 'Swedish Krona', symbol: 'kr', decimalPlaces: 2 },
    { code: 'NOK', name: 'Norwegian Krone', symbol: 'kr', decimalPlaces: 2 },
    { code: 'DKK', name: 'Danish Krone', symbol: 'kr', decimalPlaces: 2 },
    { code: 'PLN', name: 'Polish Zloty', symbol: 'zł', decimalPlaces: 2 },
    { code: 'CZK', name: 'Czech Koruna', symbol: 'Kč', decimalPlaces: 2 },
    { code: 'HUF', name: 'Hungarian Forint', symbol: 'Ft', decimalPlaces: 2 },
    { code: 'RON', name: 'Romanian Leu', symbol: 'lei', decimalPlaces: 2 },
    { code: 'RUB', name: 'Russian Ruble', symbol: '₽', decimalPlaces: 2 },

    // Americas (excluding USD/CAD)
    { code: 'MXN', name: 'Mexican Peso', symbol: 'Mex$', decimalPlaces: 2 },
    { code: 'BRL', name: 'Brazilian Real', symbol: 'R$', decimalPlaces: 2 },
    { code: 'ARS', name: 'Argentine Peso', symbol: 'AR$', decimalPlaces: 2 },
    { code: 'CLP', name: 'Chilean Peso', symbol: 'CLP$', decimalPlaces: 0 },
    { code: 'COP', name: 'Colombian Peso', symbol: 'COL$', decimalPlaces: 2 },
    { code: 'PEN', name: 'Peruvian Sol', symbol: 'S/', decimalPlaces: 2 },
];

/**
 * Find a currency by its ISO code
 */
export function getCurrencyByCode(code: string): ISO4217Currency | undefined {
    return ISO_4217_CURRENCIES.find(c => c.code === code.toUpperCase());
}

/**
 * Search currencies by code or name
 */
export function searchCurrencies(query: string): ISO4217Currency[] {
    const lowerQuery = query.toLowerCase();
    return ISO_4217_CURRENCIES.filter(
        c => c.code.toLowerCase().includes(lowerQuery) ||
            c.name.toLowerCase().includes(lowerQuery)
    );
}
