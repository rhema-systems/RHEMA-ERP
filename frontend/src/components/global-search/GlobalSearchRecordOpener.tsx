'use client';

import React, { Suspense, useEffect, useRef } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { toast } from 'sonner';

type Props<T> = { load: (id: string) => Promise<T>; onOpen: (record: T) => void | Promise<void> };

function RecordOpener<T>({ load, onOpen }: Props<T>) {
  const params = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const recordId = params.get('recordId');
  const handlers = useRef({ load, onOpen });
  handlers.current = { load, onOpen };
  const navigation = useRef({ params, pathname, router });
  navigation.current = { params, pathname, router };

  useEffect(() => {
    if (!recordId) return;
    let active = true;
    const open = async () => {
      try {
        const record = await handlers.current.load(recordId);
        if (active) await handlers.current.onOpen(record);
      } catch (error) {
        if (active) toast.error(error instanceof Error ? error.message : 'Unable to open this record.');
      } finally {
        if (active) {
          // Consume the link so selecting the same record again can reopen its dialog.
          const latest = navigation.current;
          const remaining = new URLSearchParams(latest.params.toString());
          remaining.delete('recordId');
          latest.router.replace(`${latest.pathname}${remaining.size ? `?${remaining}` : ''}`, { scroll: false });
        }
      }
    };
    void open();
    return () => { active = false; };
  // Hook object identity and callback changes do not represent a new selection.
  }, [recordId, pathname]);
  return null;
}

export function GlobalSearchRecordOpener<T>(props: Props<T>) {
  return <Suspense fallback={null}><RecordOpener {...props} /></Suspense>;
}
