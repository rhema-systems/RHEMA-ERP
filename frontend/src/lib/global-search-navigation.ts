import {
  filterNavigationByAccess,
  navigationItems,
  type NavItem,
} from '@/components/layout/sidebar';

export interface GlobalSearchNavigationEntry {
  title: string;
  href: string;
  module: string;
  /** Ancestor labels, excluding the destination's own title. */
  breadcrumbs: string[];
}

const actionSegment = /^(new|create|edit|delete|approve|reject|submit|post|cancel|upload|import)$/i;

function destinationKey(href: string): string | null {
  // Search indexes destinations, not action forms, placeholders, or external links.
  if (!href.startsWith('/') || href.startsWith('//') || /[\[\]]/.test(href)) return null;
  const url = new URL(href, 'https://navigation.local');
  if (url.origin !== 'https://navigation.local') return null;
  if (url.pathname.split('/').some(segment => actionSegment.test(segment))) return null;
  if (['action', 'mode'].some(key => actionSegment.test(url.searchParams.get(key) ?? ''))) return null;
  url.searchParams.sort();
  const pathname = url.pathname.replace(/\/+$/, '') || '/';
  return `${pathname}${url.search}`;
}

/** Rebuild when the signed-in user's roles or permissions change. */
export function buildGlobalSearchNavigation(
  hasAnyRole: (roles: string[]) => boolean,
  hasAnyPermission: (permissions: string[]) => boolean,
  items: NavItem[] = navigationItems
): GlobalSearchNavigationEntry[] {
  const entries: GlobalSearchNavigationEntry[] = [];
  const seen = new Set<string>();
  const visit = (nodes: NavItem[], ancestors: string[]) => {
    for (const item of nodes) {
      if (item.children?.length) {
        visit(item.children, [...ancestors, item.title]);
        continue;
      }
      const key = destinationKey(item.href);
      if (!key || seen.has(key)) continue;
      seen.add(key);
      entries.push({
        title: item.title,
        href: item.href,
        module: ancestors[0] ?? item.title,
        breadcrumbs: [...ancestors],
      });
    }
  };

  // Use the same ancestor, role/permission AND/OR, and fallback rules as the sidebar.
  // Both operational and settings destinations remain searchable.
  visit(filterNavigationByAccess(items, hasAnyRole, hasAnyPermission), []);
  return entries;
}

const normalize = (text: string) => text
  .normalize('NFKD')
  .replace(/[\u0300-\u036f]/g, '')
  .toLocaleLowerCase()
  .replace(/[^\p{L}\p{N}]+/gu, ' ')
  .trim();

/** Synchronous local matching, suitable for every input change. */
export function searchGlobalSearchNavigation(
  entries: readonly GlobalSearchNavigationEntry[],
  query: string,
  limit = 8
): GlobalSearchNavigationEntry[] {
  const term = normalize(query.slice(0, 256));
  if (!term || !Number.isFinite(limit) || limit < 1) return [];
  const tokens = term.split(/\s+/);
  return entries
    .map((entry, index) => {
      const title = normalize(entry.title);
      const context = normalize([...entry.breadcrumbs, entry.title].join(' '));
      if (!tokens.every(token => context.includes(token))) return null;
      const rank = title === term ? 0
        : title.startsWith(term) ? 1
        : title.includes(term) ? 2
        : tokens.every(token => title.includes(token)) ? 3 : 4;
      return { entry, rank, index };
    })
    .filter(result => result !== null)
    .sort((a, b) => a.rank - b.rank || a.index - b.index)
    .slice(0, Math.floor(limit))
    .map(result => result.entry);
}
