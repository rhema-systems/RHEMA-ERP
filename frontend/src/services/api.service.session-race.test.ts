import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';

const oldToken = 'header.old-session.signature';
const newToken = 'header.new-session.signature';
const refreshedToken = 'header.refreshed-session.signature';

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((complete) => { resolve = complete; });
  return { promise, resolve };
};

const response = (status: number, body: unknown, path = '/api/protected') => ({
  ok: status >= 200 && status < 300,
  status,
  statusText: status === 401 ? 'Unauthorized' : 'OK',
  url: `http://localhost${path}`,
  headers: new Headers({ 'Content-Type': 'application/json' }),
  text: async () => JSON.stringify(body),
  json: async () => body,
  blob: async () => new Blob(['result']),
}) as Response;

// Write directly to storage to reproduce a login in a different browser tab.
const replaceStoredSession = () => {
  localStorage.setItem('authToken', newToken);
  localStorage.setItem('token', newToken);
  localStorage.setItem('refreshToken', 'new-refresh');
};

describe('API session changes during outstanding requests', () => {
  const fetchMock = vi.fn<typeof fetch>();
  const blacklisted = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    apiService.setToken(oldToken);
    localStorage.setItem('refreshToken', 'old-refresh');
    vi.stubGlobal('fetch', fetchMock);
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    window.addEventListener('session-blacklisted', blacklisted);
  });

  afterEach(() => {
    window.removeEventListener('session-blacklisted', blacklisted);
    localStorage.clear();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it.each(['json', 'blob'])('does not refresh or blacklist a new login for a stale %s 401', async (kind) => {
    const pending = deferred<Response>();
    fetchMock.mockReturnValueOnce(pending.promise);
    const request = kind === 'json' ? apiService.get('/protected') : apiService.downloadBlob('/protected');
    const rejected = expect(request).rejects.toMatchObject({ status: 401 });
    replaceStoredSession();
    pending.resolve(response(401, { message: 'Expired old session' }));
    await rejected;
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem('authToken')).toBe(newToken);
    expect(localStorage.getItem('refreshToken')).toBe('new-refresh');
    expect(blacklisted).not.toHaveBeenCalled();
  });

  it.each([200, 401])('preserves a newer login if an old refresh finishes with HTTP %s', async (status) => {
    const refresh = deferred<Response>();
    fetchMock.mockResolvedValueOnce(response(401, { message: 'Expired old session' }));
    fetchMock.mockReturnValueOnce(refresh.promise);
    const request = apiService.get('/protected');
    const rejected = expect(request).rejects.toMatchObject({ status: 401 });
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    replaceStoredSession();
    refresh.resolve(response(status, status === 200
      ? { token: refreshedToken, refreshToken: 'stale-refresh' }
      : { message: 'Refresh rejected' }, '/api/auth/refresh'));
    await rejected;
    expect(localStorage.getItem('authToken')).toBe(newToken);
    expect(localStorage.getItem('refreshToken')).toBe('new-refresh');
    expect(blacklisted).not.toHaveBeenCalled();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('still refreshes and retries a request from the current session', async () => {
    fetchMock.mockResolvedValueOnce(response(401, { message: 'Expired' }));
    fetchMock.mockResolvedValueOnce(response(200, { token: refreshedToken, refreshToken: 'fresh-refresh' }, '/api/auth/refresh'));
    fetchMock.mockResolvedValueOnce(response(200, { success: true }));
    await expect(apiService.get('/protected')).resolves.toEqual({ success: true });
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(new Headers(fetchMock.mock.calls[2][1]?.headers).get('Authorization')).toBe(`Bearer ${refreshedToken}`);
    expect(blacklisted).not.toHaveBeenCalled();
  });

  it('does not restore a logged-out session when its refresh finishes later', async () => {
    const refresh = deferred<Response>();
    fetchMock.mockReturnValueOnce(refresh.promise);
    const request = apiService.refreshToken();
    const rejected = expect(request).rejects.toThrow('The session changed');
    apiService.clearToken();
    refresh.resolve(response(200, { token: refreshedToken, refreshToken: 'stale-refresh' }, '/api/auth/refresh'));
    await rejected;
    expect(localStorage.getItem('authToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(blacklisted).not.toHaveBeenCalled();
  });

  it.each(['json', 'blob'])('still clears and blacklists an expired current %s session when refresh fails', async (kind) => {
    fetchMock.mockResolvedValueOnce(response(401, { message: 'Expired' }));
    fetchMock.mockResolvedValueOnce(response(401, { message: 'Refresh rejected' }, '/api/auth/refresh'));
    const request = kind === 'json' ? apiService.get('/protected') : apiService.downloadBlob('/protected');
    await expect(request).rejects.toMatchObject({ status: 401 });
    expect(localStorage.getItem('authToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(blacklisted).toHaveBeenCalledTimes(1);
  });
});
