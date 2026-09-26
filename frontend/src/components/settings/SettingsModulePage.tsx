'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { ChevronLeft, ChevronRight, Search, Settings } from 'lucide-react';

import { useAuth } from '../../hooks/use-auth';
import { useTenant } from '../../contexts/TenantContext';
import { Input } from '../ui/input';
import { settingsNavigationItems } from '../layout/sidebar';
import { buildSettingsSections, filterSettingsByAccess } from './AllSettingsPage';

interface SettingsModulePageProps {
  sectionKey: string;
  moduleKey: string;
}

export function SettingsModulePage({ sectionKey, moduleKey }: SettingsModulePageProps) {
  const router = useRouter();
  const { currentTenant } = useTenant();
  const { hasAnyRole, hasAnyPermission } = useAuth();
  const [query, setQuery] = useState('');

  const moduleCard = useMemo(() => {
    const allowedItems = filterSettingsByAccess(
      settingsNavigationItems,
      hasAnyRole,
      hasAnyPermission,
    );
    const section = buildSettingsSections(allowedItems)
      .find(candidate => candidate.key === sectionKey);
    return section?.cards.find(card => card.key === moduleKey);
  }, [hasAnyPermission, hasAnyRole, moduleKey, sectionKey]);

  const visibleLinks = useMemo(() => {
    if (!moduleCard) return [];
    const normalizedQuery = query.trim().toLowerCase();
    if (!normalizedQuery) return moduleCard.links;
    return moduleCard.links.filter(link =>
      `${link.title} ${link.searchPath}`.toLowerCase().includes(normalizedQuery),
    );
  }, [moduleCard, query]);

  /**
   * One band per nav group, in the order the navigation declares them. A module whose links are
   * ungrouped collapses to a single unheaded band, so this cannot make a flat module worse — but
   * a module with fifty settings in one undifferentiated grid (Human Resources) becomes legible.
   */
  const linkGroups = useMemo(() => {
    const groups: { title: string; links: typeof visibleLinks }[] = [];
    visibleLinks.forEach(link => {
      // trail is [module, ...groups, leaf]. A leaf with no group in between (trail length 2)
      // must not band under its own title, so it falls into the unheaded band.
      const title = link.trail.length > 2 ? link.trail[1] : '';
      const existing = groups.find(group => group.title === title);
      if (existing) {
        existing.links.push(link);
        return;
      }
      groups.push({ title, links: [link] });
    });
    return groups;
  }, [visibleLinks]);

  if (!moduleCard) {
    return (
      <main className="min-h-screen bg-slate-50 p-6 dark:bg-[#101010]">
        <div className="mx-auto max-w-3xl rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm dark:border-neutral-700 dark:bg-[#1b1b1b]">
          <Settings className="mx-auto mb-3 h-8 w-8 text-slate-400" />
          <h1 className="text-xl font-semibold text-slate-950 dark:text-white">
            Settings module unavailable
          </h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
            This module does not exist or you do not have access to any of its settings.
          </p>
          <button
            type="button"
            onClick={() => router.back()}
            className="mt-5 inline-flex items-center gap-2 rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 dark:border-neutral-600 dark:text-slate-200 dark:hover:bg-neutral-800"
          >
            <ChevronLeft className="h-4 w-4" />
            Back
          </button>
        </div>
      </main>
    );
  }

  const ModuleIcon = moduleCard.icon;

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#101010]">
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/95 px-5 py-3 backdrop-blur dark:border-neutral-700 dark:bg-[#1b1b1b]/95">
        <div className="mx-auto flex min-h-12 max-w-7xl items-center gap-4">
          <button
            type="button"
            onClick={() => router.back()}
            aria-label="Back to previous screen"
            className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-300 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 dark:border-neutral-600 dark:text-slate-200 dark:hover:bg-neutral-800"
          >
            <ChevronLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Back</span>
          </button>
          <div className="flex min-w-0 flex-1 items-center gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-blue-700 ring-1 ring-blue-100 dark:bg-blue-950/40 dark:text-blue-300 dark:ring-blue-900">
              <ModuleIcon className="h-5 w-5" />
            </span>
            <div className="min-w-0">
              <h1 className="truncate text-lg font-semibold text-slate-950 dark:text-white">
                {moduleCard.title} Settings
              </h1>
              <p className="truncate text-xs text-slate-500 dark:text-slate-400">
                {currentTenant?.name ?? 'Current organization'}
              </p>
            </div>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl space-y-4 p-4 lg:p-5">
        <section className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-neutral-700 dark:bg-[#1b1b1b]">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                {moduleCard.title}
              </h2>
              <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                Select the setting you want to configure.
              </p>
            </div>
            <div className="relative w-full sm:max-w-sm">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              <Input
                aria-label={`Search ${moduleCard.title} settings`}
                value={query}
                onChange={event => setQuery(event.target.value)}
                placeholder={`Search ${moduleCard.title.toLowerCase()} settings`}
                className="h-10 bg-white pl-9 shadow-sm dark:border-neutral-700 dark:bg-neutral-800"
              />
            </div>
          </div>

          {visibleLinks.length > 0 ? (
            <nav aria-label={`${moduleCard.title} settings`} className="mt-4 space-y-5">
              {linkGroups.map(group => (
                <div key={group.title || '__ungrouped'}>
                  {group.title && (
                    <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                      {group.title}
                    </h3>
                  )}
                  <div className="grid grid-cols-1 gap-2.5 md:grid-cols-2 xl:grid-cols-3">
                    {group.links.map(link => {
                      const LinkIcon = link.icon;
                      // The nav item's own one-liner if it has one; otherwise the ancestor path,
                      // minus the band heading already printed above. A leaf with neither — no
                      // description and no parent group — renders bare, which is the bug this
                      // caption was silently hiding.
                      const caption =
                        link.description ?? link.trail.slice(2, -1).join(' / ');
                      return (
                        <Link
                          key={link.href}
                          href={link.href}
                          className="group flex min-h-16 items-center gap-3 rounded-xl border border-slate-200 bg-slate-50/70 px-3 py-2 transition hover:border-blue-300 hover:bg-blue-50 dark:border-neutral-700 dark:bg-neutral-900 dark:hover:border-blue-800 dark:hover:bg-blue-950/30"
                        >
                          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white text-slate-600 shadow-sm ring-1 ring-slate-200 group-hover:text-blue-700 dark:bg-neutral-800 dark:text-slate-300 dark:ring-neutral-700 dark:group-hover:text-blue-300">
                            <LinkIcon className="h-4 w-4" />
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="block text-sm font-semibold text-slate-800 group-hover:text-blue-800 dark:text-slate-100 dark:group-hover:text-blue-200">
                              {link.title}
                            </span>
                            {caption && (
                              <span
                                title={caption}
                                className="mt-0.5 line-clamp-2 block text-xs text-slate-500 dark:text-slate-400"
                              >
                                {caption}
                              </span>
                            )}
                          </span>
                          <ChevronRight className="h-4 w-4 shrink-0 text-slate-400 group-hover:text-blue-600" />
                        </Link>
                      );
                    })}
                  </div>
                </div>
              ))}
            </nav>
          ) : (
            <div className="mt-5 rounded-xl border border-dashed border-slate-300 px-6 py-12 text-center dark:border-neutral-700">
              <Search className="mx-auto mb-3 h-6 w-6 text-slate-400" />
              <p className="font-medium text-slate-800 dark:text-slate-100">
                No {moduleCard.title.toLowerCase()} settings match “{query.trim()}”.
              </p>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
