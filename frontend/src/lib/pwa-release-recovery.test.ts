import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const require = createRequire(import.meta.url);

describe('PWA release recovery contract', () => {
  it('forces service-worker checks past the HTTP cache', () => {
    const source = readFileSync(resolve(process.cwd(), 'src/lib/pwa.ts'), 'utf8');

    expect(source).toContain("updateViaCache: 'none'");
    expect(source).toContain('await registration.update()');
  });

  it('serves the service worker with no-store headers', async () => {
    const nextConfig = require(resolve(process.cwd(), 'next.config.js'));
    const headers = await nextConfig.headers();
    const serviceWorker = headers.find(
      (entry: { source: string }) => entry.source === '/sw.js'
    );

    expect(serviceWorker).toBeDefined();
    expect(serviceWorker.headers).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          key: 'Cache-Control',
          value: expect.stringContaining('no-store'),
        }),
        { key: 'Service-Worker-Allowed', value: '/' },
      ])
    );
  });
});
