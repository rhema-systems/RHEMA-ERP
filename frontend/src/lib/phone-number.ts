import {
  getCountryCallingCode,
  isSupportedCountry,
  parsePhoneNumberFromString,
  validatePhoneNumberLength,
  type CountryCode,
} from 'libphonenumber-js/max';
import { COUNTRIES } from '@/lib/countries';

export interface PhoneCountryOption {
  code: CountryCode;
  name: string;
  flag: string;
  dial: string;
}

export const PHONE_COUNTRIES: PhoneCountryOption[] = COUNTRIES.flatMap(
  (country) => {
    if (!isSupportedCountry(country.code)) return [];
    const code = country.code as CountryCode;
    return [
      {
        code,
        name: country.name,
        flag: country.flag,
        dial: `+${getCountryCallingCode(code)}`,
      },
    ];
  }
).sort((left, right) => left.name.localeCompare(right.name));

export const DEFAULT_PHONE_COUNTRY =
  PHONE_COUNTRIES.find((country) => country.code === 'GH') ??
  PHONE_COUNTRIES[0];

export function findPhoneCountry(
  value: string,
  fallback: PhoneCountryOption = DEFAULT_PHONE_COUNTRY
): PhoneCountryOption {
  const parsed = parsePhoneNumberFromString(value);
  if (parsed?.country) {
    return (
      PHONE_COUNTRIES.find((country) => country.code === parsed.country) ??
      fallback
    );
  }

  const compact = value.replace(/[^\d+]/g, '');
  return (
    PHONE_COUNTRIES.filter((country) => compact.startsWith(country.dial)).sort(
      (left, right) => right.dial.length - left.dial.length
    )[0] ?? fallback
  );
}

export function getNationalPhoneDigits(
  value: string,
  country: PhoneCountryOption
): string {
  const parsed = parsePhoneNumberFromString(value);
  if (parsed && parsed.countryCallingCode === country.dial.slice(1)) {
    return parsed.nationalNumber;
  }

  const compact = value.replace(/[^\d+]/g, '');
  const withoutCallingCode = compact.startsWith(country.dial)
    ? compact.slice(country.dial.length)
    : compact;
  return withoutCallingCode.replace(/\D/g, '');
}

export function toInternationalPhoneNumber(
  nationalInput: string,
  country: PhoneCountryOption
): string {
  const digits = nationalInput.replace(/\D/g, '');
  if (!digits) return country.dial;

  const parsed = parsePhoneNumberFromString(digits, country.code);
  return parsed?.number ?? `${country.dial}${digits}`;
}

export function wouldExceedCountryPhoneLength(
  nationalInput: string,
  country: PhoneCountryOption
): boolean {
  return (
    validatePhoneNumberLength(
      nationalInput.replace(/\D/g, ''),
      country.code
    ) === 'TOO_LONG'
  );
}

export function isValidPhoneNumberForCountry(
  value: string,
  countryCode?: CountryCode
): boolean {
  const parsed = parsePhoneNumberFromString(value);
  if (!parsed?.isValid()) return false;
  return !countryCode || parsed.country === countryCode;
}

export function getCountryPhoneLengthLabel(
  country: PhoneCountryOption
): string {
  const possibleLengths: number[] = [];
  for (let length = 4; length <= 15; length += 1) {
    if (
      validatePhoneNumberLength('2'.repeat(length), country.code) === undefined
    ) {
      possibleLengths.push(length);
    }
  }

  if (possibleLengths.length === 0) return 'a valid number of digits';
  if (possibleLengths.length === 1) return `${possibleLengths[0]} digits`;
  return `${possibleLengths.join(' or ')} digits`;
}
