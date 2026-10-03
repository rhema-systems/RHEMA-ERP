'use client';

import { useCallback, useEffect, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

/**
 * The open tab of a travel page, kept in `?tab=` (travel final closure, lane 8, slice 8a): a notice links
 * straight to the tab where its reader acts — the traveller's Money, Before you go or Messages, the desk's
 * Finance, Comments or Attachments — and the back button returns to the tab before. A tab the page does not
 * know falls back to `fallback`. `replace`, not `push`: switching tabs is not a navigation worth a history
 * entry each (the employee profile's rule, round 3 lane T1).
 *
 * ⚠ `useSearchParams` wants a Suspense boundary above the page in Next 15 — the pages that use this wrap
 * their default export in one.
 */
export function useTabParam(tabs: readonly string[], fallback: string): [string, (next: string) => void] {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const asked = searchParams?.get('tab') ?? null;
  const requested = asked && tabs.includes(asked) ? asked : null;
  const [tab, setTab] = useState<string>(requested ?? fallback);

  useEffect(() => {
    // Only the URL drives this effect; a click writes the URL below.
    if (requested && requested !== tab) setTab(requested);
  }, [requested]);

  const change = useCallback(
    (next: string) => {
      setTab(next);
      const qs = new URLSearchParams(searchParams?.toString() ?? '');
      if (next === fallback) qs.delete('tab');
      else qs.set('tab', next);
      const query = qs.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [fallback, pathname, router, searchParams],
  );

  return [tab, change];
}
