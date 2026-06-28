'use client';

const MOBILE_SESSION_KEY = 'erp.mobile.session.v1';
const MOBILE_SESSION_DURATION_MS = 7 * 24 * 60 * 60 * 1000;

type MobileSession = {
  expiresAt: number;
};

function canUseLocalStorage() {
  return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
}

export function rememberMobileSession(now = Date.now()) {
  if (!canUseLocalStorage()) return;

  const session: MobileSession = {
    expiresAt: now + MOBILE_SESSION_DURATION_MS,
  };
  window.localStorage.setItem(MOBILE_SESSION_KEY, JSON.stringify(session));
}

export function clearMobileSession() {
  if (!canUseLocalStorage()) return;
  window.localStorage.removeItem(MOBILE_SESSION_KEY);
}

export function getMobileSessionExpiry(): number | null {
  if (!canUseLocalStorage()) return null;

  try {
    const session = JSON.parse(window.localStorage.getItem(MOBILE_SESSION_KEY) || 'null') as MobileSession | null;
    return typeof session?.expiresAt === 'number' ? session.expiresAt : null;
  } catch {
    clearMobileSession();
    return null;
  }
}

export function hasValidMobileSession(now = Date.now()) {
  const expiresAt = getMobileSessionExpiry();
  if (!expiresAt) return false;

  if (expiresAt <= now) {
    clearMobileSession();
    return false;
  }

  return true;
}
