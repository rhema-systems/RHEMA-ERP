'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Check, ChevronsUpDown, Phone } from 'lucide-react';
import { cn } from '../../lib/utils';
import { Button } from './button';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from './command';
import { Input } from './input';
import { Popover, PopoverContent, PopoverTrigger } from './popover';
import {
  PHONE_COUNTRIES,
  findPhoneCountry,
  getCountryPhoneLengthLabel,
  getNationalPhoneDigits,
  isValidPhoneNumberForCountry,
  toInternationalPhoneNumber,
  wouldExceedCountryPhoneLength,
  type PhoneCountryOption,
} from '../../lib/phone-number';

interface PhoneInputProps {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  error?: boolean;
  disabled?: boolean;
  countrySelectLabel?: string;
  showCountryLengthHint?: boolean;
}

export default function PhoneInput({
  id,
  value,
  onChange,
  placeholder = 'Enter phone number',
  className,
  error,
  disabled = false,
  countrySelectLabel = 'Country calling code',
  showCountryLengthHint = false,
}: PhoneInputProps) {
  const [countryOpen, setCountryOpen] = useState(false);
  const [selectedCountry, setSelectedCountry] = useState<PhoneCountryOption>(
    () => findPhoneCountry(value)
  );

  useEffect(() => {
    if (!value) return;
    const detected = findPhoneCountry(value, selectedCountry);
    if (detected.code !== selectedCountry.code) setSelectedCountry(detected);
  }, [selectedCountry, value]);

  const phoneNumber = useMemo(
    () => getNationalPhoneDigits(value, selectedCountry),
    [selectedCountry, value]
  );
  const phoneLengthLabel = useMemo(
    () => getCountryPhoneLengthLabel(selectedCountry),
    [selectedCountry]
  );
  const isValid =
    Boolean(phoneNumber) &&
    isValidPhoneNumberForCountry(value, selectedCountry.code);

  const handleCountrySelect = (country: PhoneCountryOption) => {
    const existingNationalNumber = getNationalPhoneDigits(
      value,
      selectedCountry
    );
    setSelectedCountry(country);
    setCountryOpen(false);
    onChange(toInternationalPhoneNumber(existingNationalNumber, country));
  };

  const handlePhoneChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const digits = event.target.value.replace(/\D/g, '');
    if (wouldExceedCountryPhoneLength(digits, selectedCountry)) return;
    onChange(toInternationalPhoneNumber(digits, selectedCountry));
  };

  return (
    <div className={cn('space-y-1', className)}>
      <div className="flex min-w-0">
        <Popover open={countryOpen} onOpenChange={setCountryOpen}>
          <PopoverTrigger asChild>
            <Button
              type="button"
              variant="outline"
              role="combobox"
              aria-label={countrySelectLabel}
              aria-expanded={countryOpen}
              disabled={disabled}
              className="w-[7.5rem] shrink-0 justify-between rounded-r-none border-r-0 px-2 font-normal"
            >
              <span className="truncate">
                {selectedCountry.flag} {selectedCountry.dial}
              </span>
              <ChevronsUpDown className="ml-1 h-4 w-4 shrink-0 opacity-50" />
            </Button>
          </PopoverTrigger>
          <PopoverContent
            className="w-[min(22rem,calc(100vw-2rem))] p-0"
            align="start"
          >
            <Command>
              <CommandInput
                aria-label="Search phone country"
                placeholder="Search country or calling code..."
              />
              <CommandList>
                <CommandEmpty>No country found.</CommandEmpty>
                <CommandGroup>
                  {PHONE_COUNTRIES.map((country) => (
                    <CommandItem
                      key={country.code}
                      value={`${country.name} ${country.code} ${country.dial}`}
                      onSelect={() => handleCountrySelect(country)}
                    >
                      <Check
                        className={cn(
                          'mr-2 h-4 w-4 shrink-0',
                          country.code === selectedCountry.code
                            ? 'opacity-100'
                            : 'opacity-0'
                        )}
                      />
                      <span className="mr-2">{country.flag}</span>
                      <span className="min-w-0 flex-1 truncate">
                        {country.name}
                      </span>
                      <span className="ml-2 text-muted-foreground">
                        {country.dial}
                      </span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              </CommandList>
            </Command>
          </PopoverContent>
        </Popover>
        <div className="relative min-w-0 flex-1">
          <Input
            id={id}
            type="tel"
            inputMode="numeric"
            value={phoneNumber}
            onChange={handlePhoneChange}
            placeholder={placeholder}
            className={cn('rounded-l-none pr-10', error && 'border-red-500')}
            maxLength={18}
            disabled={disabled}
            aria-invalid={error || (Boolean(phoneNumber) && !isValid)}
          />
          <Phone className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        </div>
      </div>
      {showCountryLengthHint && (
        <p className="text-xs text-muted-foreground">
          {selectedCountry.name}: enter {phoneLengthLabel} after the calling
          code.
        </p>
      )}
    </div>
  );
}
