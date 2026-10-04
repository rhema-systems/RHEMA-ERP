'use client';

export const SESSION_ACTIVITY_STORAGE_KEY = 'erp-session-last-activity';
const SESSION_EVENT_STORAGE_KEY = 'erp-session-event';
export const SESSION_TOKEN_REFRESHED_AT_STORAGE_KEY = 'erp-session-token-refreshed-at';
const REFRESH_CONTENDER_PREFIX = 'erp-session-refresh-contender:';
const CHANNEL_NAME = 'erp-auth-session';
const REFRESH_LOCK_NAME = 'erp-auth-token-refresh';
const REFRESH_LEASE_MS = 30_000;
const REFRESH_WAIT_LIMIT_MS = 35_000;
const REFRESH_CLAIM_SETTLE_MS = 50;

export type BrowserSessionEventType =
  | 'activity'
  | 'login'
  | 'logout'
  | 'session-expired'
  | 'token-refreshed';

export interface BrowserSessionEvent {
  id: string;
  sourceId: string;
  type: BrowserSessionEventType;
  occurredAt: number;
}

interface RefreshLease {
  ownerId: string;
  claimedAt: number;
  expiresAt: number;
}

type SessionEventListener = (event: BrowserSessionEvent) => void;

const parseTimestamp = (value: string | null): number | null => {
  if (!value) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
};

const createId = (): string => {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
};

const parseEvent = (value: unknown): BrowserSessionEvent | null => {
  if (!value || typeof value !== 'object') return null;
  const candidate = value as Partial<BrowserSessionEvent>;
  if (
    typeof candidate.id !== 'string' ||
    typeof candidate.sourceId !== 'string' ||
    typeof candidate.type !== 'string' ||
    typeof candidate.occurredAt !== 'number'
  ) {
    return null;
  }

  if (!['activity', 'login', 'logout', 'session-expired', 'token-refreshed'].includes(candidate.type)) {
    return null;
  }

  return candidate as BrowserSessionEvent;
};

const parseJson = <T,>(value: string | null): T | null => {
  if (!value) return null;
  try {
    return JSON.parse(value) as T;
  } catch {
    return null;
  }
};

export class BrowserSessionCoordinator {
  private readonly sourceId = createId();
  private readonly listeners = new Set<SessionEventListener>();
  private readonly seenEventIds = new Set<string>();
  private channel: BroadcastChannel | null = null;
  private initialized = false;
  private refreshPromise: Promise<unknown> | null = null;

  private readonly handleStorage = (event: StorageEvent): void => {
    if (event.key === SESSION_ACTIVITY_STORAGE_KEY) {
      const occurredAt = parseTimestamp(event.newValue);
      if (occurredAt) {
        this.deliver({
          id: `activity:${occurredAt}`,
          sourceId: 'storage',
          type: 'activity',
          occurredAt,
        });
      }
      return;
    }

    if (event.key === SESSION_EVENT_STORAGE_KEY) {
      const parsed = parseEvent(parseJson(event.newValue));
      if (parsed) this.deliver(parsed);
    }
  };

  private ensureInitialized(): void {
    if (this.initialized || typeof window === 'undefined') return;
    this.initialized = true;
    window.addEventListener('storage', this.handleStorage);

    if (typeof BroadcastChannel !== 'undefined') {
      this.channel = new BroadcastChannel(CHANNEL_NAME);
      this.channel.addEventListener('message', (message: MessageEvent<unknown>) => {
        const parsed = parseEvent(message.data);
        if (parsed) this.deliver(parsed);
      });
    }
  }

  private deliver(event: BrowserSessionEvent): void {
    if (this.seenEventIds.has(event.id)) return;
    this.seenEventIds.add(event.id);
    if (this.seenEventIds.size > 100) {
      const oldest = this.seenEventIds.values().next().value;
      if (oldest) this.seenEventIds.delete(oldest);
    }
    this.listeners.forEach(listener => listener(event));
  }

  subscribe(listener: SessionEventListener): () => void {
    this.ensureInitialized();
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  publish(type: BrowserSessionEventType, occurredAt = Date.now()): void {
    if (typeof window === 'undefined') return;
    this.ensureInitialized();
    const event: BrowserSessionEvent = {
      id: createId(),
      sourceId: this.sourceId,
      type,
      occurredAt,
    };

    // The message deliberately contains no access token, refresh token, user data, or tenant data.
    localStorage.setItem(SESSION_EVENT_STORAGE_KEY, JSON.stringify(event));
    if (type === 'token-refreshed') {
      localStorage.setItem(SESSION_TOKEN_REFRESHED_AT_STORAGE_KEY, occurredAt.toString());
    }
    this.channel?.postMessage(event);
    this.deliver(event);
  }

  getLastActivity(fallback = Date.now()): number {
    if (typeof window === 'undefined') return fallback;
    return parseTimestamp(localStorage.getItem(SESSION_ACTIVITY_STORAGE_KEY)) ?? fallback;
  }

  recordActivity(occurredAt = Date.now()): number {
    if (typeof window === 'undefined') return occurredAt;
    const current = this.getLastActivity(0);
    const sharedActivity = Math.max(current, occurredAt);
    if (sharedActivity !== current) {
      localStorage.setItem(SESSION_ACTIVITY_STORAGE_KEY, sharedActivity.toString());
      this.publish('activity', sharedActivity);
    }
    return sharedActivity;
  }

  getLatestEvent(): BrowserSessionEvent | null {
    if (typeof window === 'undefined') return null;
    return parseEvent(parseJson(localStorage.getItem(SESSION_EVENT_STORAGE_KEY)));
  }

  wasTokenRefreshedAfter(timestamp: number): boolean {
    if (typeof window === 'undefined') return false;
    return (parseTimestamp(localStorage.getItem(SESSION_TOKEN_REFRESHED_AT_STORAGE_KEY)) ?? 0) >= timestamp;
  }

  getSessionVersion(): string {
    if (typeof window === 'undefined') return '';
    return [
      localStorage.getItem('authToken') || localStorage.getItem('token') || '',
      localStorage.getItem('refreshToken') || '',
      localStorage.getItem('tokenExpiry') || '',
    ].join('|');
  }

  async coordinateRefresh<T>(sessionVersion: string, refresh: () => Promise<T>): Promise<T | undefined> {
    if (this.refreshPromise) {
      return this.refreshPromise as Promise<T | undefined>;
    }

    const operation = this.coordinateRefreshAcrossTabs(sessionVersion, refresh);
    this.refreshPromise = operation;
    try {
      return await operation;
    } finally {
      if (this.refreshPromise === operation) this.refreshPromise = null;
    }
  }

  private async coordinateRefreshAcrossTabs<T>(sessionVersion: string, refresh: () => Promise<T>): Promise<T | undefined> {
    if (typeof window === 'undefined') return refresh();
    this.ensureInitialized();

    const lockManager = navigator.locks;
    if (lockManager?.request) {
      return lockManager.request(REFRESH_LOCK_NAME, { mode: 'exclusive' }, async () => {
        if (this.getSessionVersion() !== sessionVersion) return undefined;
        return refresh();
      });
    }

    return this.coordinateRefreshWithLease(sessionVersion, refresh);
  }

  private async coordinateRefreshWithLease<T>(sessionVersion: string, refresh: () => Promise<T>): Promise<T | undefined> {
    const waitStartedAt = Date.now();
    const contenderKey = `${REFRESH_CONTENDER_PREFIX}${this.sourceId}`;

    while (Date.now() - waitStartedAt < REFRESH_WAIT_LIMIT_MS) {
      if (this.getSessionVersion() !== sessionVersion) return undefined;

      const now = Date.now();
      this.getActiveRefreshContenders(now);
      if (!localStorage.getItem(contenderKey)) {
        const lease: RefreshLease = {
          ownerId: this.sourceId,
          claimedAt: now,
          expiresAt: now + REFRESH_LEASE_MS,
        };
        localStorage.setItem(contenderKey, JSON.stringify(lease));
      }

      // Each tab writes a separate contender key, so simultaneous claims cannot overwrite one
      // another. After a short contention window every tab deterministically chooses the oldest
      // live claim (then owner id), which sharply reduces dual ownership in browsers without
      // Web Locks. Expired claims recover a suspended or closed owner by timestamp.
      await new Promise<void>(resolve => setTimeout(resolve, REFRESH_CLAIM_SETTLE_MS));
      const winner = this.getActiveRefreshContenders(Date.now())
        .sort((left, right) => left.claimedAt - right.claimedAt || left.ownerId.localeCompare(right.ownerId))[0];
      if (winner?.ownerId === this.sourceId) {
        try {
          if (this.getSessionVersion() !== sessionVersion) return undefined;
          return await refresh();
        } finally {
          localStorage.removeItem(contenderKey);
        }
      }

      localStorage.removeItem(contenderKey);
      await this.waitForSessionSignal(250);
    }

    localStorage.removeItem(contenderKey);
    if (this.getSessionVersion() !== sessionVersion) return undefined;
    throw new Error('Timed out waiting for authentication refresh coordination');
  }

  private getActiveRefreshContenders(now: number): RefreshLease[] {
    const contenders: RefreshLease[] = [];
    for (let index = localStorage.length - 1; index >= 0; index -= 1) {
      const key = localStorage.key(index);
      if (!key?.startsWith(REFRESH_CONTENDER_PREFIX)) continue;
      const contender = parseJson<RefreshLease>(localStorage.getItem(key));
      if (!contender || contender.expiresAt <= now) {
        localStorage.removeItem(key);
        continue;
      }
      contenders.push(contender);
    }
    return contenders;
  }

  private waitForSessionSignal(timeoutMs: number): Promise<void> {
    return new Promise(resolve => {
      let settled = false;
      const finish = () => {
        if (settled) return;
        settled = true;
        unsubscribe();
        clearTimeout(timer);
        resolve();
      };
      const unsubscribe = this.subscribe(() => finish());
      const timer = setTimeout(finish, timeoutMs);
    });
  }

  destroy(): void {
    if (typeof window !== 'undefined' && this.initialized) {
      window.removeEventListener('storage', this.handleStorage);
    }
    this.channel?.close();
    this.channel = null;
    this.initialized = false;
    this.listeners.clear();
    this.seenEventIds.clear();
    this.refreshPromise = null;
  }
}

export const browserSessionCoordinator = new BrowserSessionCoordinator();
