'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  Bell,
  Building2,
  ChevronRight,
  ClipboardCheck,
  Database,
  Search,
  Settings,
  Shield,
  Workflow,
  X,
} from 'lucide-react';

import { useAuth } from '../../hooks/use-auth';
import { useTenant } from '../../contexts/TenantContext';
import { cn } from '../../lib/utils';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { settingsNavigationItems, type NavItem } from '../layout/sidebar';
import { canAccessSettingsItem } from './settings-access';

export interface SettingsLink {
  title: string;
  href: string;
  icon: NavItem['icon'];
  trail: string[];
  searchPath: string;
  /** What the screen is for. Falls back to the ancestor path when a nav item does not carry one. */
  description?: string;
  group?: string;
}

export interface SettingsCard {
  key: string;
  title: string;
  icon: NavItem['icon'];
  links: SettingsLink[];
}

export interface SettingsSection {
  key: string;
  title: string;
  cards: SettingsCard[];
}

export function getSettingsModuleHref(sectionKey: string, cardKey: string) {
  return `/settings/${encodeURIComponent(sectionKey)}/${encodeURIComponent(cardKey)}`;
}

const moduleTitleAliases: Record<string, string> = {
  HR: 'Human Resources',
};

const moduleOrder = [
  'Finance',
  'Human Resources',
  'Safety (SHE)',
  'Procurement',
  'Inventory',
  'Sales',
  'Marketing',
  'Estate',
  'Projects',
  'Maintenance',
  'Fleet',
  'Helpdesk',
  'Legal',
];

const financeSettingsGroups = [
  {
    title: 'Core Accounting',
    hrefs: [
      '/administration/finance/settings',
      '/administration/finance/access-scopes',
      '/finance/accounts',
      '/administration/finance/account-segments',
      '/administration/finance/dimensions',
      '/administration/finance/account-generator',
    ],
  },
  {
    title: 'Fiscal & Close',
    hrefs: [
      '/administration/finance/fiscal-calendar',
      '/finance/fiscal-years',
      '/finance/fiscal-periods',
      '/administration/finance/close-templates',
    ],
  },
  {
    title: 'Banking',
    hrefs: ['/finance/cash/accounts'],
  },
  {
    title: 'Tax & Currency',
    hrefs: [
      '/administration/finance/tax',
      '/administration/finance/currencies',
    ],
  },
  {
    title: 'Payments & Documents',
    hrefs: [
      '/administration/finance/payment-terms',
      '/administration/finance/payment-methods',
      '/administration/finance/document-numbering',
    ],
  },
  {
    title: 'Fixed Assets',
    hrefs: ['/administration/finance/fixed-asset-categories'],
  },
  {
    title: 'Unit Accounting',
    hrefs: [
      '/finance/unit-accounts',
      '/administration/finance/unit-types',
      '/administration/finance/ratio-definitions',
    ],
  },
] as const;

function organizeFinanceLinks(links: SettingsLink[]): SettingsLink[] {
  const linksByHref = new Map(deduplicateLinks(links).map(link => [link.href, link]));
  const groupedHrefs = new Set<string>(financeSettingsGroups.flatMap(group => [...group.hrefs]));

  const grouped = financeSettingsGroups.flatMap(group =>
    group.hrefs.flatMap(href => {
      const link = linksByHref.get(href);
      return link ? [{ ...link, group: group.title }] : [];
    }),
  );

  const unclassified = Array.from(linksByHref.values())
    .filter(link => !groupedHrefs.has(link.href))
    .map(link => ({ ...link, group: 'Other Finance Settings' }));

  return [...grouped, ...unclassified];
}

function groupCardLinks(links: SettingsLink[]) {
  return links.reduce<Array<{ title: string; links: SettingsLink[] }>>((groups, link) => {
    const title = link.group ?? '';
    const existing = groups.find(group => group.title === title);
    if (existing) {
      existing.links.push(link);
    } else {
      groups.push({ title, links: [link] });
    }
    return groups;
  }, []);
}

function flattenSettingsLinks(item: NavItem, trail: string[] = []): SettingsLink[] {
  const nextTrail = [...trail, item.title];
  if (item.children?.length) {
    return item.children.flatMap(child => flattenSettingsLinks(child, nextTrail));
  }

  return [{
    title: item.title,
    href: item.href,
    icon: item.icon,
    trail: nextTrail,
    searchPath: [...nextTrail, item.description ?? ''].join(' '),
    description: item.description,
  }];
}

function deduplicateLinks(links: SettingsLink[]) {
  return Array.from(new Map(links.map(link => [link.href, link])).values());
}

function addCardSource(cards: Map<string, SettingsCard>, item: NavItem, titleOverride?: string) {
  const title = titleOverride ?? moduleTitleAliases[item.title] ?? item.title;
  const key = title.toLowerCase().replace(/[^a-z0-9]+/g, '-');
  const existing = cards.get(key);
  const links = flattenSettingsLinks(item);

  const mergedLinks = deduplicateLinks([...(existing?.links ?? []), ...links]);

  cards.set(key, {
    key,
    title,
    icon: existing?.icon ?? item.icon,
    links: title === 'Finance' ? organizeFinanceLinks(mergedLinks) : mergedLinks,
  });
}

function platformCardKey(item: NavItem) {
  const value = `${item.title} ${item.href}`.toLowerCase();
  if (value.includes('workflow')) return 'workflow';
  if (
    value.includes('security') ||
    value.includes('identity-management') ||
    value.includes('user management') ||
    value.includes('role management') ||
    value.includes('user-employee') ||
    value.includes('retention')
  ) return 'security';
  if (value.includes('tenant') || value.includes('field labels')) return 'tenant';
  if (value.includes('audit') || value.includes('system log') || value.includes('message queue')) return 'audit';
  if (
    value.includes('notification') ||
    value.includes('email') ||
    value.includes('sms') ||
    value.includes('file upload')
  ) return 'communications';
  if (value.includes('report') || value.includes('data source')) return 'reporting';
  return 'administration';
}

const platformCardDefinitions: Record<string, Omit<SettingsCard, 'links'>> = {
  workflow: { key: 'workflow', title: 'Workflow & Automation', icon: Workflow },
  security: { key: 'security', title: 'Security & Identity', icon: Shield },
  tenant: { key: 'tenant', title: 'Tenant & Organization', icon: Building2 },
  audit: { key: 'audit', title: 'Audit & Monitoring', icon: ClipboardCheck },
  communications: { key: 'communications', title: 'Notifications & Communications', icon: Bell },
  reporting: { key: 'reporting', title: 'Reporting & Data', icon: Database },
  administration: { key: 'administration', title: 'General Administration', icon: Settings },
};

export function filterSettingsByAccess(
  items: NavItem[],
  hasAnyRole: (roles: string[]) => boolean,
  hasAnyPermission: (permissions: string[]) => boolean,
): NavItem[] {
  return items.reduce<NavItem[]>((allowed, item) => {
    const children = item.children
      ? filterSettingsByAccess(item.children, hasAnyRole, hasAnyPermission)
      : undefined;

    // Navigation parents are structural on the Settings catalogue. A user who
    // can access one exact descendant may traverse its parent categories, but
    // siblings are still removed by their own leaf-level access checks.
    if (item.children && (!children || children.length === 0)) return allowed;

    if (!item.children && !canAccessSettingsItem(item, { hasAnyRole, hasAnyPermission })) {
      return allowed;
    }

    allowed.push({ ...item, children });
    return allowed;
  }, []);
}

export function buildSettingsSections(items: NavItem[]): SettingsSection[] {
  const moduleCards = new Map<string, SettingsCard>();
  const platformLinks = new Map<string, SettingsLink[]>();
  const administration = items.find(item => item.title === 'Administration');

  items
    .filter(item => item.title !== 'Administration' && item.title !== 'Notifications')
    .forEach(item => addCardSource(moduleCards, item));

  const administrationModules = new Set([
    'Finance',
    'HR',
    // The SHE settings tree is its own module card, beside Human Resources,
    // not a bucket of "General Administration" links.
    'Safety (SHE)',
    'Procurement',
    'Inventory',
    'Sales',
    'Marketing',
    'Estate',
    'Projects',
    'Maintenance',
    'Fleet',
    'Helpdesk',
  ]);

  administration?.children?.forEach(item => {
    if (administrationModules.has(item.title)) {
      addCardSource(moduleCards, item);
      return;
    }

    if (item.title === 'System' && item.children?.length) {
      item.children.forEach(child => {
        const key = platformCardKey(child);
        platformLinks.set(key, [...(platformLinks.get(key) ?? []), ...flattenSettingsLinks(child)]);
      });
      return;
    }

    const key = platformCardKey(item);
    platformLinks.set(key, [...(platformLinks.get(key) ?? []), ...flattenSettingsLinks(item)]);
  });

  const notifications = items.find(item => item.title === 'Notifications');
  if (notifications) {
    platformLinks.set('communications', [
      ...(platformLinks.get('communications') ?? []),
      ...flattenSettingsLinks(notifications),
    ]);
  }

  const orderedModuleCards = Array.from(moduleCards.values()).sort((left, right) => {
    const leftIndex = moduleOrder.indexOf(left.title);
    const rightIndex = moduleOrder.indexOf(right.title);
    return (leftIndex < 0 ? Number.MAX_SAFE_INTEGER : leftIndex) -
      (rightIndex < 0 ? Number.MAX_SAFE_INTEGER : rightIndex);
  });

  const platformCards = Object.keys(platformCardDefinitions)
    .map(key => ({
      ...platformCardDefinitions[key],
      links: deduplicateLinks(platformLinks.get(key) ?? []),
    }))
    .filter(card => card.links.length > 0);

  return [
    { key: 'modules', title: 'Module Settings', cards: orderedModuleCards },
    { key: 'administration', title: 'Administration & Governance', cards: platformCards },
  ].filter(section => section.cards.length > 0);
}

const cardAccentClasses = [
  'from-emerald-50 to-white text-emerald-700 dark:from-emerald-950/40 dark:to-slate-900 dark:text-emerald-300',
  'from-orange-50 to-white text-orange-700 dark:from-orange-950/40 dark:to-slate-900 dark:text-orange-300',
  'from-blue-50 to-white text-blue-700 dark:from-blue-950/40 dark:to-slate-900 dark:text-blue-300',
  'from-violet-50 to-white text-violet-700 dark:from-violet-950/40 dark:to-slate-900 dark:text-violet-300',
];

export function AllSettingsPage() {
  const router = useRouter();
  const { currentTenant } = useTenant();
  const { hasAnyRole, hasAnyPermission } = useAuth();
  const [query, setQuery] = useState('');

  const sections = useMemo(() => {
    const allowedItems = filterSettingsByAccess(settingsNavigationItems, hasAnyRole, hasAnyPermission);
    return buildSettingsSections(allowedItems);
  }, [hasAnyPermission, hasAnyRole]);

  const visibleSections = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    if (!normalizedQuery) return sections;

    return sections
      .map(section => ({
        ...section,
        cards: section.cards.filter(card =>
          `${section.title} ${card.title} ${card.links.map(link => `${link.title} ${link.searchPath}`).join(' ')}`
            .toLowerCase()
            .includes(normalizedQuery)),
      }))
      .filter(section => section.cards.length > 0);
  }, [query, sections]);

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#101010]">
      <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/95 px-5 py-3 backdrop-blur dark:border-neutral-700 dark:bg-[#1b1b1b]/95">
        <div className="grid min-h-12 grid-cols-[minmax(220px,1fr)_minmax(280px,520px)_minmax(220px,1fr)] items-center gap-5">
          <div className="flex min-w-0 items-center gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-blue-200 bg-blue-50 text-blue-600 dark:border-blue-900 dark:bg-blue-950/40 dark:text-blue-300">
              <Settings className="h-5 w-5" />
            </div>
            <div className="min-w-0">
              <h1 className="truncate text-lg font-semibold text-slate-950 dark:text-white">All Settings</h1>
              <p className="truncate text-xs text-slate-500 dark:text-slate-400">
                {currentTenant?.name ?? 'Current organization'}
              </p>
            </div>
          </div>

          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <Input
              aria-label="Search settings"
              autoFocus
              value={query}
              onChange={event => setQuery(event.target.value)}
              placeholder="Search modules and settings"
              className="h-10 bg-white pl-9 shadow-sm dark:border-neutral-700 dark:bg-neutral-800"
            />
          </div>

          <div className="flex justify-end">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.back()}
              className="gap-2"
            >
              Close Settings
              <X className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        </div>
      </header>

      <main className="space-y-4 p-4 lg:p-5">
        {visibleSections.map(section => (
          <section
            key={section.key}
            aria-labelledby={`${section.key}-settings-heading`}
            className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-neutral-700 dark:bg-[#1b1b1b]"
          >
            <h2
              id={`${section.key}-settings-heading`}
              className="mb-3 text-base font-semibold text-slate-800 dark:text-slate-100"
            >
              {section.title}
            </h2>
            <div
              data-testid={`${section.key}-settings-grid`}
              className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4"
            >
              {section.cards.map((card, cardIndex) => {
                const CardIcon = card.icon;
                return (
                  <article
                    key={card.key}
                    className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md dark:border-neutral-700 dark:bg-neutral-900 dark:hover:border-blue-700"
                  >
                    <Link
                      href={getSettingsModuleHref(section.key, card.key)}
                      aria-label={`Open ${card.title} settings`}
                      className={cn(
                        'flex min-h-20 w-full items-center gap-3 bg-gradient-to-br px-4 py-3 text-left transition-colors',
                        cardAccentClasses[cardIndex % cardAccentClasses.length],
                      )}
                    >
                      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white/80 shadow-sm ring-1 ring-black/5 dark:bg-black/20 dark:ring-white/10">
                        <CardIcon className="h-4 w-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <h3 className="text-base font-semibold">{card.title}</h3>
                        <span className="mt-0.5 block text-xs font-medium opacity-75">
                          {card.links.length} {card.links.length === 1 ? 'setting' : 'settings'}
                        </span>
                      </span>
                      <ChevronRight className="h-4 w-4 shrink-0 opacity-60" />
                    </Link>
                  </article>
                );
              })}
            </div>
          </section>
        ))}

        {visibleSections.length === 0 && (
          <div className="rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-16 text-center dark:border-neutral-700 dark:bg-[#1b1b1b]">
            <Search className="mx-auto mb-3 h-6 w-6 text-slate-400" />
            <p className="font-medium text-slate-800 dark:text-slate-100">
              {query.trim()
                ? `No settings match “${query.trim()}”.`
                : 'No settings are available for your current roles and permissions.'}
            </p>
          </div>
        )}
      </main>
    </div>
  );
}
