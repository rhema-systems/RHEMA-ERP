import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  BrowserSessionCoordinator,
  SESSION_ACTIVITY_STORAGE_KEY,
  type BrowserSessionEvent,
} from './browser-session-coordinator';

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(complete => { resolve = complete; });
  return { promise, resolve };
};

describe('BrowserSessionCoordinator', () => {
  const coordinators: BrowserSessionCoordinator[] = [];

  const createCoordinator = () => {
    const coordinator = new BrowserSessionCoordinator();
    coordinators.push(coordinator);
    return coordinator;
  };

  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    coordinators.splice(0).forEach(coordinator => coordinator.destroy());
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('shares only a monotonic activity timestamp and emits no credential data', () => {
    const coordinator = createCoordinator();
    const received: BrowserSessionEvent[] = [];
    coordinator.subscribe(event => received.push(event));
    localStorage.setItem('authToken', 'access-secret');
    localStorage.setItem('refreshToken', 'refresh-secret');

    expect(coordinator.recordActivity(2_000)).toBe(2_000);
    expect(coordinator.recordActivity(1_000)).toBe(2_000);
    coordinator.publish('token-refreshed', 3_000);
    coordinator.publish('activity', 4_000);

    expect(localStorage.getItem(SESSION_ACTIVITY_STORAGE_KEY)).toBe('2000');
    expect(received.filter(event => event.type === 'activity')).toHaveLength(2);
    expect(coordinator.wasTokenRefreshedAfter(2_500)).toBe(true);
    expect(JSON.stringify(coordinator.getLatestEvent())).not.toContain('access-secret');
    expect(JSON.stringify(coordinator.getLatestEvent())).not.toContain('refresh-secret');
  });

  it('uses the storage-event fallback to wake another tab', () => {
    const sender = createCoordinator();
    const receiver = createCoordinator();
    const received = vi.fn();
    receiver.subscribe(received);

    sender.publish('logout', 4_000);
    const eventValue = localStorage.getItem('erp-session-event');
    window.dispatchEvent(new StorageEvent('storage', {
      key: 'erp-session-event',
      newValue: eventValue,
    }));

    expect(received).toHaveBeenCalledWith(expect.objectContaining({ type: 'logout', occurredAt: 4_000 }));
  });

  it('deduplicates simultaneous refresh attempts in the same tab', async () => {
    const coordinator = createCoordinator();
    localStorage.setItem('authToken', 'header.token.signature');
    localStorage.setItem('refreshToken', 'refresh-token');
    const pending = deferred<string>();
    const refresh = vi.fn(() => pending.promise);
    const sessionVersion = coordinator.getSessionVersion();

    const first = coordinator.coordinateRefresh(sessionVersion, refresh);
    const second = coordinator.coordinateRefresh(sessionVersion, refresh);
    await vi.waitFor(() => expect(refresh).toHaveBeenCalledTimes(1));
    pending.resolve('refreshed');

    await expect(first).resolves.toBe('refreshed');
    await expect(second).resolves.toBe('refreshed');
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('elects one refresh owner across tabs when Web Locks are unavailable', async () => {
    const firstCoordinator = createCoordinator();
    const secondCoordinator = createCoordinator();
    localStorage.setItem('authToken', 'header.token.signature');
    localStorage.setItem('refreshToken', 'refresh-token');
    const sessionVersion = firstCoordinator.getSessionVersion();
    const refresh = vi.fn(async () => {
      localStorage.setItem('authToken', 'header.refreshed.signature');
      firstCoordinator.publish('token-refreshed');
      return 'owner-result';
    });

    const [first, second] = await Promise.all([
      firstCoordinator.coordinateRefresh(sessionVersion, refresh),
      secondCoordinator.coordinateRefresh(sessionVersion, refresh),
    ]);

    expect(refresh).toHaveBeenCalledTimes(1);
    expect([first, second].filter(result => result === 'owner-result')).toHaveLength(1);
    expect([first, second].filter(result => result === undefined)).toHaveLength(1);
  });
});
