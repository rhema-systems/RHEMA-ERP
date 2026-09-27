import { apiService } from '@/services/api.service';

export interface GlobalSearchRecordSource {
  id: string;
  module: string;
  label: string;
  route: string;
  endpoint: string;
  searchParam: string;
  params?: Record<string, string | number | boolean>;
  method?: 'GET' | 'POST';
  searchIn?: 'query' | 'body';
  body?: Record<string, string | number | boolean>;
  itemsPath?: string;
  idField: string;
  titleFields: string[];
  subtitleFields?: string[];
  statusField?: string;
  detailPath?: string;
  permissions?: string[];
  maxResults?: number;
}

export interface GlobalSearchResult {
  id: string;
  title: string;
  subtitle: string;
  module: string;
  kind: string;
  href: string;
  status?: string;
}

/** Preserve restricted navigation views (for example customers-only or procurement AP).
 * A matching pathname alone must not broaden those register filters. */
export function accessibleSearchSources(sources: GlobalSearchRecordSource[], navigation: { href: string }[],
  hasAnyPermission: (permissions: string[]) => boolean): GlobalSearchRecordSource[] {
  return sources.flatMap(source => {
    if (source.permissions?.length && !hasAnyPermission(source.permissions)) return [];
    const sourceUrl = new URL(source.route, 'https://app.local');
    const matches = navigation.map(entry => new URL(entry.href, 'https://app.local'))
      .filter(url => url.pathname === sourceUrl.pathname);
    const unrestricted = matches.find(url => !url.search);
    const views = unrestricted ? [unrestricted] : matches;
    return views.flatMap(url => {
      const filters = Object.fromEntries(url.searchParams);
      const sourceFilters = { ...Object.fromEntries(sourceUrl.searchParams), ...source.params };
      if (Object.entries(filters).some(([key, value]) => key in sourceFilters && String(sourceFilters[key]) !== value)) return [];
      return [{ ...source, params: { ...sourceFilters, ...filters } }];
    }).filter((item, index, all) => all.findIndex(other => JSON.stringify(other.params) === JSON.stringify(item.params)) === index);
  });
}

function read(value: unknown, path: string): unknown {
  return path.split('.').filter(Boolean).reduce<unknown>((item, key) =>
    item !== null && typeof item === 'object' ? (item as Record<string, unknown>)[key] : undefined, value);
}

function text(value: unknown): string {
  return typeof value === 'string' || typeof value === 'number' ? String(value).trim() : '';
}

export function extractSearchRecords(source: GlobalSearchRecordSource, response: unknown): GlobalSearchResult[] {
  const rows = read(response, source.itemsPath ?? 'items');
  if (!Array.isArray(rows)) throw new Error('Unexpected search response');
  return rows.slice(0, source.maxResults ?? 5).flatMap(row => {
    const id = text(read(row, source.idField));
    const title = source.titleFields.map(field => text(read(row, field))).filter(Boolean).join(' · ');
    if (!id || !title) return [];
    const href = source.detailPath?.replace(':id', encodeURIComponent(id)) ?? source.route;
    if (!href.startsWith('/') || href.startsWith('//')) return [];
    return [{ id: `${source.id}:${id}`, title, href, module: source.module, kind: source.label,
      subtitle: (source.subtitleFields ?? []).map(field => text(read(row, field))).filter(Boolean).join(' · '),
      status: source.statusField ? text(read(row, source.statusField)) || undefined : undefined }];
  });
}

export function searchRecordRequest(source: GlobalSearchRecordSource, term: string) {
  const params = new URLSearchParams(Object.entries(source.params ?? {}).map(([key, value]) => [key, String(value)]));
  const body = source.searchIn === 'body' ? { ...source.body, [source.searchParam]: term } : source.body;
  if (source.searchIn !== 'body') params.set(source.searchParam, term);
  return { endpoint: `${source.endpoint}?${params}`, options: {
    method: source.method ?? 'GET', ...(body ? { body: JSON.stringify(body) } : {}),
  } };
}

/** Uses each module's authenticated endpoint, retaining its tenant and row-level scope.
 * No shared result cache: results must never cross user or tenant boundaries. */
export async function searchGlobalRecords(
  sources: GlobalSearchRecordSource[], term: string, signal: AbortSignal,
  onProgress: (results: GlobalSearchResult[], unavailable: number) => void,
): Promise<void> {
  if (term.trim().length < 2 || term.length > 100) return;
  let cursor = 0;
  let unavailable = 0;
  const results = new Map<string, GlobalSearchResult>();
  const worker = async () => {
    while (!signal.aborted && cursor < sources.length) {
      const source = sources[cursor++];
      const request = searchRecordRequest(source, term.trim());
      const controller = new AbortController();
      const abort = () => controller.abort();
      signal.addEventListener('abort', abort, { once: true });
      const timeout = setTimeout(abort, 10000);
      try {
        const response = await apiService.silentRequest<unknown>(request.endpoint, { ...request.options, signal: controller.signal });
        if (signal.aborted) return;
        for (const result of extractSearchRecords(source, response)) results.set(result.id, result);
      } catch (error) {
        if (signal.aborted) return;
        const status = error && typeof error === 'object' ? (error as { status?: number }).status : undefined;
        // Denied sources expose neither their records nor a denial message to the user.
        if (status !== 403 && status !== 401) {
          unavailable++;
          if (process.env.NODE_ENV === 'development') {
            const reason = error instanceof Error && error.message === 'Unexpected search response'
              ? 'response-contract' : status ?? (error instanceof Error ? error.name : 'network');
            console.warn('[global-search] Source unavailable:', source.id, reason);
          }
        }
      } finally {
        clearTimeout(timeout);
        signal.removeEventListener('abort', abort);
      }
      if (!signal.aborted) onProgress([...results.values()], unavailable);
    }
  };
  await Promise.all(Array.from({ length: Math.min(4, sources.length) }, worker));
}
