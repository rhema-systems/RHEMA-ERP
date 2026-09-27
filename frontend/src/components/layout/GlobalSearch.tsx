'use client';

import { useEffect, useId, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { Loader2, Search, X } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { useTenant } from '@/contexts/TenantContext';
import { Input } from '@/components/ui/input';
import { buildGlobalSearchNavigation, searchGlobalSearchNavigation } from '@/lib/global-search-navigation';
import { accessibleSearchSources, searchGlobalRecords, type GlobalSearchResult } from '@/lib/global-search';
import { GLOBAL_SEARCH_RECORD_SOURCES } from '@/lib/global-search-record-sources';
import { GLOBAL_SEARCH_PROCUREMENT_SOURCES } from '@/lib/global-search-procurement-sources';
import { GLOBAL_SEARCH_INVENTORY_SOURCES } from '@/lib/global-search-inventory-sources';
import { GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES } from '@/lib/global-search-estate-legal-sources';
import { GLOBAL_SEARCH_CIVIL_QS_SOURCES } from '@/lib/global-search-civil-qs-sources';
import { GLOBAL_SEARCH_OPERATIONS_SOURCES } from '@/lib/global-search-operations-sources';
import { getGlobalSearchAdministrationSources } from '@/lib/global-search-administration-sources';

const sources = [...GLOBAL_SEARCH_RECORD_SOURCES, ...GLOBAL_SEARCH_PROCUREMENT_SOURCES, ...GLOBAL_SEARCH_INVENTORY_SOURCES, ...GLOBAL_SEARCH_ESTATE_LEGAL_SOURCES, ...GLOBAL_SEARCH_CIVIL_QS_SOURCES, ...GLOBAL_SEARCH_OPERATIONS_SOURCES];

export function GlobalSearch() {
  const { user, hasAnyRole, hasAnyPermission } = useAuth();
  const { currentTenantCode } = useTenant();
  // Remount on identity, tenant or grant changes; old requests/results never remain visible.
  const session = JSON.stringify([user?.id, currentTenantCode, user?.roles, user?.permissions]);
  return <SearchInput key={session} enabled={!!user} hasAnyRole={hasAnyRole} hasAnyPermission={hasAnyPermission} />;
}

function SearchInput({ enabled, hasAnyRole, hasAnyPermission }: {
  enabled: boolean; hasAnyRole: (roles: string[]) => boolean; hasAnyPermission: (permissions: string[]) => boolean;
}) {
  const [query, setQuery] = useState('');
  const [module, setModule] = useState('All modules');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [records, setRecords] = useState<GlobalSearchResult[]>([]);
  const [unavailable, setUnavailable] = useState(0);
  const [activeId, setActiveId] = useState<string | null>(null);
  const root = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const listId = useId();
  const term = query.trim();
  // Permissions are snapshotted for this keyed session; unstable auth callback identity
  // must not start another search on each progressive response.
  const [navigation] = useState(() => buildGlobalSearchNavigation(hasAnyRole, hasAnyPermission));
  const [allowed] = useState(() => accessibleSearchSources(
    [...sources, ...getGlobalSearchAdministrationSources(hasAnyRole)], navigation, hasAnyPermission));
  const modules = useMemo(() => [...new Set([...navigation.map(item => item.module), ...allowed.map(item => item.module)])].sort(), [navigation, allowed]);
  const visibleNavigation = navigation.filter(item => module === 'All modules' || item.module === module);
  const pageResults: GlobalSearchResult[] = term.length >= 2 ? searchGlobalSearchNavigation(visibleNavigation, term, 8).map(item => ({
    id: `page:${item.href}`, title: item.title, href: item.href, module: item.module,
    kind: 'Page', subtitle: item.breadcrumbs.join(' › '),
  })) : [];
  const results = [...records.slice().sort((a, b) => {
    const q = term.toLowerCase();
    return Number(b.title.toLowerCase().startsWith(q)) - Number(a.title.toLowerCase().startsWith(q)) || a.title.localeCompare(b.title);
  }), ...pageResults].slice(0, 80);
  const active = results.findIndex(result => result.id === activeId);

  useEffect(() => {
    setRecords([]); setUnavailable(0); setActiveId(null);
    if (!enabled || !open || term.length < 2) { setBusy(false); return; }
    const controller = new AbortController();
    setBusy(true);
    const timer = setTimeout(() => {
      void searchGlobalRecords(allowed.filter(source => module === 'All modules' || source.module === module), term, controller.signal,
        (next, failed) => { setRecords(next); setUnavailable(failed); })
        .finally(() => { if (!controller.signal.aborted) setBusy(false); });
    }, 350);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [term, module, enabled, open, allowed]);

  useEffect(() => {
    const outside = (event: MouseEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    const shortcut = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault(); input.current?.focus(); setOpen(true);
      }
    };
    document.addEventListener('mousedown', outside);
    document.addEventListener('keydown', shortcut);
    return () => { document.removeEventListener('mousedown', outside); document.removeEventListener('keydown', shortcut); };
  }, []);

  useEffect(() => {
    if (open && active >= 0) document.getElementById(`${listId}-${active}`)?.scrollIntoView?.({ block: 'nearest' });
  }, [active, open, listId]);

  return <div ref={root} className="relative w-full max-w-2xl" onBlur={event => {
    if (event.relatedTarget && !event.currentTarget.contains(event.relatedTarget)) setOpen(false);
  }}>
    <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
    <Input ref={input} role="combobox" aria-label="Search across the app" aria-autocomplete="list"
      aria-expanded={open} aria-controls={open ? listId : undefined}
      aria-activedescendant={open && active >= 0 ? `${listId}-${active}` : undefined}
      placeholder="Search records, modules and pages…" maxLength={100} value={query}
      className="bg-slate-50/70 pl-10 pr-16 dark:bg-neutral-800/80"
      onFocus={() => setOpen(true)} onClick={() => setOpen(true)} onChange={event => { setQuery(event.target.value); setRecords([]); setActiveId(null); setOpen(true); }}
      onKeyDown={event => {
        if (event.key === 'Escape') { setOpen(false); setActiveId(null); }
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
          event.preventDefault(); setOpen(true);
          setActiveId(previous => {
            if (!results.length) return null;
            const previousIndex = results.findIndex(result => result.id === previous);
            const next = previousIndex < 0 ? (event.key === 'ArrowDown' ? 0 : results.length - 1)
              : (previousIndex + (event.key === 'ArrowDown' ? 1 : -1) + results.length) % results.length;
            return results[next].id;
          });
        }
        if (event.key === 'Enter' && open && results.length) {
          event.preventDefault(); document.getElementById(`${listId}-${Math.max(active, 0)}`)?.click();
        }
      }} />
    <div className="absolute right-2 top-1/2 flex -translate-y-1/2 items-center gap-1">
      {busy && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" aria-label="Searching" />}
      {query ? <button type="button" aria-label="Clear search" className="rounded p-1 hover:bg-muted" onClick={() => { setQuery(''); input.current?.focus(); }}><X className="h-4 w-4" /></button>
        : <kbd className="hidden text-xs text-muted-foreground lg:block">Ctrl K</kbd>}
    </div>
    {open && <div className="absolute left-0 right-0 top-full z-[80] mt-2 overflow-hidden rounded-xl border bg-popover text-popover-foreground shadow-xl sm:min-w-[min(42rem,calc(100vw-4rem))]">
      <div className="flex items-center justify-between gap-3 border-b px-4 py-3">
        <span className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Global search</span>
        <select aria-label="Search module" value={module} onChange={event => { setModule(event.target.value); setRecords([]); }} className="max-w-[60%] rounded-md border bg-background px-2 py-1 text-xs">
          <option>All modules</option>{modules.map(name => <option key={name}>{name}</option>)}
        </select>
      </div>
      <div id={listId} role="listbox" aria-label="Search results" className="max-h-[min(65vh,36rem)] overflow-y-auto p-2">
        {results.map((result, index) => <Link key={result.id} id={`${listId}-${index}`} role="option" aria-selected={active === index}
          href={result.href} prefetch={false} onClick={() => setOpen(false)} onMouseEnter={() => setActiveId(result.id)}
          className={`flex items-center gap-3 rounded-lg px-3 py-3 ${active === index ? 'bg-accent' : 'hover:bg-accent/60'}`}>
          <span className="rounded-lg bg-blue-50 p-2 text-blue-600 dark:bg-blue-950"><Search className="h-4 w-4" /></span>
          <span className="min-w-0 flex-1"><span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="truncate text-sm font-medium">{result.title}</span>
            <span className="rounded-full bg-muted px-2 py-0.5 text-[0.65rem] uppercase tracking-wide text-muted-foreground">{result.kind}</span>
          </span><span className="mt-1 block truncate text-xs text-muted-foreground">{result.subtitle || result.module}</span></span>
          {result.status && <span className="shrink-0 rounded-full border px-2 py-1 text-xs text-muted-foreground">{result.status}</span>}
        </Link>)}
      </div>
      <div role="status" className="border-t px-4 py-2 text-xs text-muted-foreground">
        {term.length < 2 ? 'Type at least 2 characters to search.' : busy ? `Searching… ${results.length} results` : results.length ? `${results.length} results · ↑ ↓ to navigate · Enter to open` : 'No matching records or pages.'}
        {!!unavailable && <span className="ml-2">Some record searches are unavailable. Try again.</span>}
      </div>
    </div>}
  </div>;
}
