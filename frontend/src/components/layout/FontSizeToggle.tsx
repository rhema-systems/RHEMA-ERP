'use client';

import { Type } from 'lucide-react';
import { useFontSize, type FontSize } from '../../contexts/FontSizeContext';
import { cn } from '../../lib/utils';

const sizes: { value: FontSize; label: string; short: string }[] = [
  { value: 'small', label: 'Small', short: 'S' },
  { value: 'medium', label: 'Medium', short: 'M' },
  { value: 'large', label: 'Large', short: 'L' },
];

export function FontSizeToggle() {
  const { fontSize, setFontSize } = useFontSize();
  return (
    <div role="group" aria-label="Font size" className="flex shrink-0 items-center rounded-lg border border-slate-200 bg-white p-0.5 dark:border-neutral-700 dark:bg-neutral-900 print:hidden">
      <Type aria-hidden="true" className="mx-2 hidden h-4 w-4 text-slate-500 lg:block" />
      {sizes.map(({ value, label, short }) => (
        <button
          key={value}
          type="button"
          aria-label={`${label} font size`}
          aria-pressed={fontSize === value}
          title={`${label} font size`}
          onClick={() => setFontSize(value)}
          className={cn(
            'h-8 w-8 rounded-md text-xs font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2',
            fontSize === value
              ? 'bg-slate-900 text-white dark:bg-white dark:text-neutral-900'
              : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-neutral-800',
          )}
        >
          {short}
        </button>
      ))}
    </div>
  );
}
