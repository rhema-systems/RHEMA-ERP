'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  Bell,
  Building2,
  ChevronDown,
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
  searchPath: string;
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

const moduleTitleAliases: Record<string, string> = {
  HR: 'Human Resources',
};

const moduleOrder = [
  'Finance',
  'Human Resources',
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

function flattenSettingsLinks(item: NavItem, trail: string[] = []): SettingsLink[] {
  const nextTrail = [...trail, item.title];
  if (item.children?.length) {
    return item.children.flatMap(child => flattenSettingsLinks(child, nextTrail));
  }

  return [{
    title: item.title,
    href: item.href,
    icon: item.icon,
    searchPath: nextTrail.join(' '),
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

  cards.set(key, {
    key,
    title,
    icon: existing?.icon ?? item.icon,
    links: deduplicateLinks([...(existing?.links ?? []), ...links]),
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
  const [collapsedCards, setCollapsedCards] = useState<Set<string>>(new Set());

  const toggleCard = (cardKey: string) => {
    setCollapsedCards(current => {
      const next = new Set(current);
      if (next.has(cardKey)) {
        next.delete(cardKey);
      } else {
        next.add(cardKey);
      }
      return next;
    });
  };

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
        cards: section.cards
          .map(card => {
            if (`${section.title} ${card.title}`.toLowerCase().includes(normalizedQuery)) return card;
            return {
              ...card,
              links: card.links.filter(link =>
                `${link.title} ${link.searchPath}`.toLowerCase().includes(normalizedQuery)),
            };
          })
          .filter(card => card.links.length > 0),
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
              placeholder="Search settings"
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

      <main className="space-y-6 p-5 lg:p-7">
        {visibleSections.map(section => (
          <section
            key={section.key}
            aria-labelledby={`${section.key}-settings-heading`}
            className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-sm dark:border-neutral-700 dark:bg-[#1b1b1b]"
          >
            <h2
              id={`${section.key}-settings-heading`}
              className="mb-4 text-lg font-medium text-slate-800 dark:text-slate-100"
            >
              {section.title}
            </h2>
            <div
              data-testid={`${section.key}-settings-masonry`}
              className="columns-1 gap-4 md:columns-2 xl:columns-4 2xl:columns-5"
            >
              {section.cards.map((card, cardIndex) => {
                const CardIcon = card.icon;
                const accordionKey = `${section.key}:${card.key}`;
                const isExpanded = query.trim().length > 0 || !collapsedCards.has(accordionKey);
                const contentId = `${accordionKey}-content`;
                return (
                  <article
                    key={card.key}
                    className="mb-4 inline-block w-full break-inside-avoid overflow-hidden rounded-xl border border-slate-200 bg-white align-top shadow-sm dark:border-neutral-700 dark:bg-neutral-900"
                  >
                    <button
                      type="button"
                      aria-expanded={isExpanded}
                      aria-controls={contentId}
                      aria-label={`${isExpanded ? 'Collapse' : 'Expand'} ${card.title} settings`}
                      onClick={() => toggleCard(accordionKey)}
                      className={cn(
                      'flex w-full items-center gap-2 bg-gradient-to-r px-4 py-3 text-left text-sm font-semibold transition-colors',
                      cardAccentClasses[cardIndex % cardAccentClasses.length],
                    )}>
                      <CardIcon className="h-4 w-4 shrink-0" />
                      <h3 className="flex-1">{card.title}</h3>
                      <ChevronDown className={cn('h-4 w-4 transition-transform', isExpanded && 'rotate-180')} />
                    </button>
                    {isExpanded && (
                      <nav id={contentId} aria-label={`${card.title} settings`} className="space-y-0.5 p-2">
                        {card.links.map(link => (
                          <Link
                            key={link.href}
                            href={link.href}
                            className="block rounded-lg px-3 py-2 text-sm text-slate-700 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-slate-300 dark:hover:bg-blue-950/40 dark:hover:text-blue-300"
                          >
                            {link.title}
                          </Link>
                        ))}
                      </nav>
                    )}
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
