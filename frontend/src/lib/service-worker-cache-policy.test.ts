import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runInNewContext } from 'node:vm';
import { describe, expect, it, vi } from 'vitest';

type FetchEvent = {
  request: Request;
  respondWith: (response: Promise<Response>) => void;
};

function loadServiceWorker() {
  const listeners = new Map<string, (event: FetchEvent) => void>();
  const cache = {
    match: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  };
  const caches = {
    keys: vi.fn().mockResolvedValue([]),
    delete: vi.fn(),
    open: vi.fn().mockResolvedValue(cache),
  };
  const fetch = vi.fn().mockResolvedValue(
    new Response(JSON.stringify({ registrationId: 'registration-a' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })
  );
  const workerSource = readFileSync(
    resolve(process.cwd(), 'public/sw.js'),
    'utf8'
  );

  runInNewContext(workerSource, {
    Request,
    Response,
    URL,
    caches,
    console: {
      error: vi.fn(),
      log: vi.fn(),
      warn: vi.fn(),
    },
    fetch,
    indexedDB: { open: vi.fn() },
    self: {
      addEventListener: (type: string, handler: (event: FetchEvent) => void) =>
        listeners.set(type, handler),
      clients: { claim: vi.fn() },
      location: { hostname: 'erp.company.com' },
      registration: { showNotification: vi.fn() },
      skipWaiting: vi.fn(),
    },
  });

  return {
    cache,
    caches,
    fetch,
    fetchListener: listeners.get('fetch')!,
  };
}

describe('service-worker authenticated API cache policy', () => {
  it('keeps bearer-authenticated API responses network-only', async () => {
    const worker = loadServiceWorker();
    const respondWith = vi.fn();
    const request = new Request(
      'https://erp.company.com/api/procurement/supplier-applicant-access/portal',
      {
        headers: { Authorization: 'Bearer restricted-applicant-token' },
      }
    );

    worker.fetchListener({ request, respondWith });
    expect(respondWith).toHaveBeenCalledOnce();

    await respondWith.mock.calls[0][0];

    expect(worker.fetch).toHaveBeenCalledWith(request);
    expect(worker.caches.open).not.toHaveBeenCalled();
    expect(worker.cache.match).not.toHaveBeenCalled();
    expect(worker.cache.put).not.toHaveBeenCalled();
  });

  it('does not poison the static cache with a failed deployment-time response', async () => {
    const worker = loadServiceWorker();
    worker.fetch.mockResolvedValueOnce(
      new Response('temporarily unavailable', { status: 404 })
    );
    const respondWith = vi.fn();
    const request = new Request(
      'https://erp.company.com/_next/static/chunks/app/release.js'
    );

    worker.fetchListener({ request, respondWith });
    const response = await respondWith.mock.calls[0][0];

    expect(response.status).toBe(404);
    expect(worker.cache.put).not.toHaveBeenCalled();
  });

  it('removes a previously cached failed static response and retries the network', async () => {
    const worker = loadServiceWorker();
    worker.cache.match.mockResolvedValueOnce(
      new Response('cached failure', { status: 404 })
    );
    const respondWith = vi.fn();
    const request = new Request(
      'https://erp.company.com/_next/static/chunks/app/recovered.js'
    );

    worker.fetchListener({ request, respondWith });
    const response = await respondWith.mock.calls[0][0];

    expect(response.ok).toBe(true);
    expect(worker.cache.delete).toHaveBeenCalledWith(request);
    expect(worker.fetch).toHaveBeenCalledWith(request);
    expect(worker.cache.put).toHaveBeenCalledOnce();
  });
});
