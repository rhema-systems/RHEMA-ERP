const LOGIN_ROUTE = '/login';
const TENANT_SELECT_ROUTE = '/tenant-select';

type SearchParamsLike = {
  get(name: string): string | null;
};

const isReservedAuthRoute = (value: string) =>
  value === LOGIN_ROUTE ||
  value.startsWith(`${LOGIN_ROUTE}?`) ||
  value.startsWith(`${LOGIN_ROUTE}#`) ||
  value === TENANT_SELECT_ROUTE ||
  value.startsWith(`${TENANT_SELECT_ROUTE}?`) ||
  value.startsWith(`${TENANT_SELECT_ROUTE}#`);

export const normalizeRedirectTarget = (value?: string | null): string | null => {
  if (!value) {
    return null;
  }

  const trimmed = value.trim();
  if (!trimmed || !trimmed.startsWith('/') || trimmed.startsWith('//')) {
    return null;
  }

  if (isReservedAuthRoute(trimmed)) {
    return null;
  }

  return trimmed;
};

export const getCurrentRelativeUrl = (): string | null => {
  if (typeof window === 'undefined') {
    return null;
  }

  const { pathname, search, hash } = window.location;
  return normalizeRedirectTarget(`${pathname}${search}${hash}`);
};

const appendRedirectQuery = (basePath: string, redirectTarget?: string | null): string => {
  const safeRedirectTarget = normalizeRedirectTarget(redirectTarget);
  if (!safeRedirectTarget) {
    return basePath;
  }

  return `${basePath}?redirect=${encodeURIComponent(safeRedirectTarget)}`;
};

export const buildLoginRedirectUrl = (redirectTarget?: string | null): string =>
  appendRedirectQuery(LOGIN_ROUTE, redirectTarget);

export const buildTenantSelectRedirectUrl = (redirectTarget?: string | null): string =>
  appendRedirectQuery(TENANT_SELECT_ROUTE, redirectTarget);

export const getRedirectTargetFromSearchParams = (searchParams?: SearchParamsLike | null): string | null =>
  normalizeRedirectTarget(searchParams?.get('redirect') ?? null);

export const getRedirectTargetFromCurrentLocation = (): string | null => {
  if (typeof window === 'undefined') {
    return null;
  }

  return getRedirectTargetFromSearchParams(new URLSearchParams(window.location.search));
};

export const resolveRedirectTarget = (redirectTarget: string | null | undefined, fallbackPath: string): string =>
  normalizeRedirectTarget(redirectTarget) ?? fallbackPath;
