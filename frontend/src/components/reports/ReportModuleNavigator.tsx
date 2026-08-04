'use client';

import React, { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { BarChart3, Menu, Search, X, type LucideIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

export interface ReportModuleNavigationItem {
  code: string;
  title: string;
  group: string;
  icon: LucideIcon;
  available: boolean;
}

interface ReportModuleNavigatorProps {
  moduleName: string;
  modulePath: string;
  activeReportCode: string;
  items: ReportModuleNavigationItem[];
}

/** Shared companion drawer for any module report detail page. Module teams supply only their catalogue metadata. */
export function ReportModuleNavigator({
  moduleName,
  modulePath,
  activeReportCode,
  items,
}: ReportModuleNavigatorProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const panelRef = useRef<HTMLElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const moduleLabel = moduleName.toLowerCase();
  const normalizedQuery = query.trim().toLocaleLowerCase();
  const visibleItems = normalizedQuery
    ? items.filter(item => `${item.title} ${item.group}`.toLocaleLowerCase().includes(normalizedQuery))
    : items;
  const groups = Array.from(new Set(visibleItems.map(item => item.group)));

  useEffect(() => {
    if (!open) return;

    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (!panelRef.current?.contains(target) && !triggerRef.current?.contains(target)) setOpen(false);
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  return (
    <>
      <Button
        ref={triggerRef}
        variant="outline"
        size="icon"
        className="h-9 w-9 shrink-0"
        aria-label={`${open ? 'Close' : 'Open'} ${moduleLabel} report navigator`}
        aria-expanded={open}
        aria-controls={`${moduleLabel}-report-navigator`}
        onClick={() => setOpen(value => !value)}
      >
        <Menu className="h-4 w-4" />
      </Button>

      {open && (
        <aside
          ref={panelRef}
          id={`${moduleLabel}-report-navigator`}
          className="fixed bottom-0 left-16 top-16 z-40 w-72 border-r bg-background shadow-xl animate-in slide-in-from-left-2 duration-200"
          aria-label={`${moduleName} report navigator`}
        >
          <div className="flex h-full flex-col">
            <div className="flex h-14 items-center justify-between border-b px-4">
              <div>
                <h2 className="text-lg font-semibold">Reports</h2>
                <p className="text-xs text-muted-foreground">{moduleName}</p>
              </div>
              <Button variant="ghost" size="icon" className="h-8 w-8 text-red-600 hover:bg-red-50 hover:text-red-700 dark:hover:bg-red-950/30" aria-label="Close report navigator" onClick={() => setOpen(false)}>
                <X className="h-4 w-4" />
              </Button>
            </div>

            <div className="border-b p-3">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={query}
                  onChange={event => setQuery(event.target.value)}
                  className="h-9 pl-9"
                  placeholder="Search reports"
                  aria-label="Search reports"
                />
              </div>
            </div>

            <nav className="flex-1 overflow-y-auto p-3" aria-label={`${moduleName} report navigation`}>
              <Link
                href={modulePath}
                onClick={() => setOpen(false)}
                className="mb-4 flex items-center gap-3 rounded-md border px-3 py-2 text-sm font-medium transition-colors hover:bg-muted"
              >
                <BarChart3 className="h-4 w-4" />
                All {moduleLabel} reports
              </Link>

              <div className="space-y-4">
                {groups.map(group => (
                  <section key={group} aria-labelledby={`report-navigator-${group.replaceAll(' ', '-').toLowerCase()}`}>
                    <h3
                      id={`report-navigator-${group.replaceAll(' ', '-').toLowerCase()}`}
                      className="mb-1 px-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                    >
                      {group}
                    </h3>
                    <div className="space-y-1">
                      {visibleItems.filter(item => item.group === group).map(item => {
                        const Icon = item.icon;
                        const isActive = item.code === activeReportCode;

                        if (!item.available) {
                          return (
                            <div
                              key={item.code}
                              className="flex items-center gap-3 rounded-md px-2 py-2 text-sm text-muted-foreground opacity-50"
                              aria-disabled="true"
                            >
                              <Icon className="h-4 w-4 shrink-0" />
                              <span className="min-w-0 flex-1 truncate">{item.title}</span>
                            </div>
                          );
                        }

                        return (
                          <Link
                            key={item.code}
                            href={`${modulePath}/${item.code}`}
                            onClick={() => setOpen(false)}
                            aria-current={isActive ? 'page' : undefined}
                            className={cn(
                              'flex items-center gap-3 rounded-md px-2 py-2 text-sm transition-colors hover:bg-muted',
                              isActive && 'bg-blue-50 font-medium text-blue-700 dark:bg-blue-950/40 dark:text-blue-300',
                            )}
                          >
                            <Icon className="h-4 w-4 shrink-0" />
                            <span className="min-w-0 flex-1 truncate">{item.title}</span>
                            {isActive && <Badge variant="secondary" className="text-[10px]">Current</Badge>}
                          </Link>
                        );
                      })}
                    </div>
                  </section>
                ))}
                {visibleItems.length === 0 && (
                  <div className="px-2 py-6 text-center text-sm text-muted-foreground">No matching reports.</div>
                )}
              </div>
            </nav>
          </div>
        </aside>
      )}
    </>
  );
}
