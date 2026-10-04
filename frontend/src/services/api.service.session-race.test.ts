import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { browserSessionCoordinator } from './browser-session-coordinator';

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

  it('performs one refresh for simultaneous current-session requests', async () => {
    const refresh = deferred<Response>();
    fetchMock.mockResolvedValueOnce(response(401, { message: 'Expired' }));
    fetchMock.mockResolvedValueOnce(response(401, { message: 'Expired' }));
    fetchMock.mockReturnValueOnce(refresh.promise);
    fetchMock.mockResolvedValueOnce(response(200, { request: 1 }));
    fetchMock.mockResolvedValueOnce(response(200, { request: 2 }));

    const first = apiService.get('/protected?request=1');
    const second = apiService.get('/protected?request=2');
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
    refresh.resolve(response(200, {
      token: refreshedToken,
      refreshToken: 'old-refresh',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
    }, '/api/auth/refresh'));

    await expect(Promise.all([first, second])).resolves.toEqual([{ request: 1 }, { request: 2 }]);
    expect(fetchMock.mock.calls.filter(call => String(call[0]).includes('/auth/refresh'))).toHaveLength(1);
  });

  it('retries with a token refreshed by another tab without issuing a second refresh', async () => {
    const pending = deferred<Response>();
    fetchMock.mockReturnValueOnce(pending.promise);
    fetchMock.mockResolvedValueOnce(response(200, { success: true }));
    const request = apiService.get('/protected');

    replaceStoredSession();
    browserSessionCoordinator.publish('token-refreshed');
    pending.resolve(response(401, { message: 'Old token expired' }));

    await expect(request).resolves.toEqual({ success: true });
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(new Headers(fetchMock.mock.calls[1][1]?.headers).get('Authorization')).toBe(`Bearer ${newToken}`);
  });

  it('does not let a stale logout erase a replacement session', async () => {
    const logout = deferred<Response>();
    fetchMock.mockReturnValueOnce(logout.promise);
    const request = apiService.logout('old-refresh');
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));

    replaceStoredSession();
    logout.resolve(response(200, { success: true }, '/api/auth/logout'));
    await request;

    expect(localStorage.getItem('authToken')).toBe(newToken);
    expect(localStorage.getItem('refreshToken')).toBe('new-refresh');
  });

  it('presents governed Finance failures as an explanation with a support reference', async () => {
    fetchMock.mockResolvedValueOnce(response(400, {
      message: 'DELTA_POSTING_WINDOW_CLOSED: The accounting date is outside this Delta book posting window.',
    }, '/api/finance/journal-entries/entry/post'));

    await expect(apiService.post('/finance/journal-entries/entry/post')).rejects.toMatchObject({
      message: 'The accounting date is outside this Delta book posting window. Reference: DELTA_POSTING_WINDOW_CLOSED.',
      financeTitle: 'Finance action failed',
      status: 400,
    });
  });

  it.each(['/api/ap/invoices/1/post', '/api/ar/invoices/1/post'])(
    'normalizes Finance subledger feedback from %s',
    async (path) => {
      fetchMock.mockResolvedValueOnce(response(400, {
        message: 'ACCOUNTING_EVENT_APPROVAL_REQUIRED: Independent approval is required before posting.',
      }, path));

      await expect(apiService.post(path.replace('/api', ''))).rejects.toMatchObject({
        message: 'Independent approval is required before posting. Reference: ACCOUNTING_EVENT_APPROVAL_REQUIRED.',
        financeTitle: 'Approval required',
      });
    },
  );

  it('surfaces Finance field-validation details instead of the generic ASP.NET title', async () => {
    fetchMock.mockResolvedValueOnce(response(400, {
      title: 'One or more validation errors occurred.',
      errors: {
        liquidityAccountId: ['Select an active Cash Till or holding account.'],
        paymentMethodId: ['Payment method is required.'],
      },
    }, '/api/ar/payments'));

    await expect(apiService.post('/ar/payments')).rejects.toMatchObject({
      message: 'Liquidity Account ID: Select an active Cash Till or holding account. Payment Method ID: Payment method is required.',
      financeTitle: 'Finance action failed',
      status: 400,
    });
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
