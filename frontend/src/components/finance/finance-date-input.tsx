'use client';

import React from 'react';
import { Input, type InputProps } from '@/components/ui/input';

type FinanceDateInputProps = Omit<
  InputProps,
  'type' | 'value' | 'defaultValue' | 'onChange' | 'onInput' | 'min' | 'max'
> & {
  value?: Date | null;
  onChange: (value: Date | undefined) => void;
  min?: Date | null;
  max?: Date | null;
};

const pad = (value: number) => String(value).padStart(2, '0');

export function formatFinanceDateInput(value?: Date | null): string {
  if (!value || Number.isNaN(value.getTime())) return '';

  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
}

export function parseFinanceDateInput(value: string): Date | undefined {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return undefined;

  const [, yearText, monthText, dayText] = match;
  const year = Number(yearText);
  const month = Number(monthText);
  const day = Number(dayText);

  // Construct the date from local calendar parts so a timezone conversion cannot move it
  // into the preceding or following accounting day.
  const result = new Date(year, month - 1, day);
  if (
    result.getFullYear() !== year ||
    result.getMonth() !== month - 1 ||
    result.getDate() !== day
  ) {
    return undefined;
  }

  return result;
}

export const FinanceDateInput = React.forwardRef<HTMLInputElement, FinanceDateInputProps>(function FinanceDateInput({
  value,
  onChange,
  min,
  max,
  ...props
}, ref) {
  const renderedValue = formatFinanceDateInput(value);
  const emittedValueRef = React.useRef(renderedValue);

  React.useEffect(() => {
    emittedValueRef.current = renderedValue;
  }, [renderedValue]);

  const handleDateInput = (
    event: React.FormEvent<HTMLInputElement> | React.ChangeEvent<HTMLInputElement>
  ) => {
    const nextValue = event.currentTarget.value;
    if (nextValue === emittedValueRef.current) return;

    emittedValueRef.current = nextValue;
    onChange(parseFinanceDateInput(nextValue));
  };

  return (
    <Input
      ref={ref}
      {...props}
      type="date"
      value={renderedValue}
      min={formatFinanceDateInput(min)}
      max={formatFinanceDateInput(max)}
      // Chromium date controls and browser automation do not always emit the same event.
      // Handle both, while deduplicating the usual input-then-change sequence.
      onInput={handleDateInput}
      onChange={handleDateInput}
    />
  );
});
