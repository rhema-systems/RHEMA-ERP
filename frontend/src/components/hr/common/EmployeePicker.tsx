'use client';

import { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Search, X } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useDebounce } from '@/hooks/use-debounce';
import { staffDirectoryService } from '@/services/hr/staff-directory.service';

interface EmployeePickerProps {
  value: string | null;
  /** Label to show for an already-selected employee (e.g. on edit). */
  initialLabel?: string | null;
  onChange: (employeeId: string | null, label: string | null) => void;
  placeholder?: string;
  disabled?: boolean;
}

/**
 * Lightweight searchable employee selector backed by GET /employee-portal/directory/lookup.
 * Used until a shared combobox primitive / full Employees UI exists.
 *
 * ⚠ Not `POST /hr/Employees/paged`: since master's global search (2026-09-27) that needs
 * HR.Employee.Read, which the SHE, manager and other roles that fill HR forms do not hold. The
 * lookup is the lean directory card — name, number, post — open to any internal user.
 */
export function EmployeePicker({
  value,
  initialLabel,
  onChange,
  placeholder = 'Search employees…',
  disabled,
}: EmployeePickerProps) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [selectedLabel, setSelectedLabel] = useState<string | null>(initialLabel ?? null);
  const containerRef = useRef<HTMLDivElement>(null);
  const debouncedQuery = useDebounce(query, 400);

  useEffect(() => {
    setSelectedLabel(initialLabel ?? null);
  }, [initialLabel]);

  // Close the results panel when clicking outside.
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const { data, isFetching } = useQuery({
    queryKey: ['hr', 'employee-lookup', debouncedQuery],
    queryFn: () => staffDirectoryService.lookup(debouncedQuery, 10),
    enabled: open && debouncedQuery.trim().length >= 2,
  });

  const results = data?.items ?? [];

  const select = (id: string, label: string) => {
    setSelectedLabel(label);
    setQuery('');
    setOpen(false);
    onChange(id, label);
  };

  const clear = () => {
    setSelectedLabel(null);
    setQuery('');
    onChange(null, null);
  };

  if (value && selectedLabel) {
    return (
      <div className="flex items-center justify-between rounded-md border px-3 py-2">
        <span className="text-sm">{selectedLabel}</span>
        {!disabled && (
          <Button type="button" variant="ghost" size="icon" className="h-6 w-6" onClick={clear}>
            <X className="h-4 w-4" />
          </Button>
        )}
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input
          className="pl-8"
          placeholder={placeholder}
          value={query}
          disabled={disabled}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
        />
      </div>
      {open && debouncedQuery.trim().length >= 2 && (
        <div className="absolute z-50 mt-1 w-full rounded-md border bg-popover shadow-md">
          {isFetching ? (
            <div className="flex items-center gap-2 p-3 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> Searching…
            </div>
          ) : results.length === 0 ? (
            <div className="p-3 text-sm text-muted-foreground">No employees found.</div>
          ) : (
            <ul className="max-h-60 overflow-auto py-1">
              {results.map((emp) => {
                const label = emp.displayName || emp.fullName;
                return (
                  <li key={emp.id}>
                    <button
                      type="button"
                      className="flex w-full flex-col items-start px-3 py-2 text-left text-sm hover:bg-accent"
                      onClick={() => select(emp.id, label)}
                    >
                      <span className="font-medium">{label}</span>
                      <span className="text-xs text-muted-foreground">
                        {emp.employeeNumber}
                        {emp.positionTitle ? ` · ${emp.positionTitle}` : ''}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
