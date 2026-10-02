'use client';

import React from 'react';
import { LayoutDashboard, Sparkles } from 'lucide-react';
import { useInterfaceStyle, type InterfaceStyle } from '../../contexts/InterfaceStyleContext';
import { cn } from '../../lib/utils';

const choices: Array<{ value: InterfaceStyle; label: string; icon: typeof LayoutDashboard }> = [
  { value: 'executive', label: 'Executive interface', icon: LayoutDashboard },
  { value: 'immersive', label: 'Immersive interface', icon: Sparkles },
];

export function InterfaceStyleToggle() {
  const { interfaceStyle, setInterfaceStyle } = useInterfaceStyle();

  return (
    <div
      role="group"
      aria-label="Interface style"
      className="hidden h-9 items-center rounded-xl border border-slate-200/70 bg-slate-50/80 p-0.5 dark:border-neutral-700 dark:bg-neutral-800/80 md:flex"
    >
      {choices.map(({ value, label, icon: Icon }) => {
        const selected = interfaceStyle === value;
        return (
          <button
            key={value}
            type="button"
            aria-label={label}
            aria-pressed={selected}
            title={label}
            onClick={() => setInterfaceStyle(value)}
            className={cn(
              'flex h-7 w-8 items-center justify-center rounded-lg transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              selected
                ? 'bg-white text-blue-700 shadow-sm dark:bg-neutral-700 dark:text-blue-300'
                : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white',
            )}
          >
            <Icon className="h-3.5 w-3.5" aria-hidden="true" />
          </button>
        );
      })}
    </div>
  );
}
