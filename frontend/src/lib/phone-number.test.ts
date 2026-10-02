import { describe, expect, it } from 'vitest';
import {
  PHONE_COUNTRIES,
  getCountryPhoneLengthLabel,
  isValidPhoneNumberForCountry,
  toInternationalPhoneNumber,
  wouldExceedCountryPhoneLength,
} from './phone-number';

describe('country-aware phone numbers', () => {
  const ghana = PHONE_COUNTRIES.find((country) => country.code === 'GH')!;
  const unitedKingdom = PHONE_COUNTRIES.find(
    (country) => country.code === 'GB'
  )!;

  it('uses the selected country numbering plan rather than only the E.164 ceiling', () => {
    expect(getCountryPhoneLengthLabel(ghana)).toBe('8 or 9 digits');
    expect(toInternationalPhoneNumber('0201234567', ghana)).toBe(
      '+233201234567'
    );
    expect(isValidPhoneNumberForCountry('+233201234567', 'GH')).toBe(true);
    expect(isValidPhoneNumberForCountry('+23320123456', 'GH')).toBe(false);
    expect(wouldExceedCountryPhoneLength('02012345678', ghana)).toBe(true);
  });

  it('supports non-Ghana numbering plans from the shared country catalogue', () => {
    expect(toInternationalPhoneNumber('07123456789', unitedKingdom)).toBe(
      '+447123456789'
    );
    expect(isValidPhoneNumberForCountry('+447123456789', 'GB')).toBe(true);
    expect(isValidPhoneNumberForCountry('+447123456789', 'GH')).toBe(false);
  });
});
