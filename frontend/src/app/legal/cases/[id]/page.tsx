'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { procedureCaseService } from '@/services/procedure-case.service';

/** Resolve a search match through the authorized case owner before entering its procedure. */
export default function LegalSearchCasePage() {
  const params = useParams<{ id: string | string[] }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setError(null);
    void procedureCaseService.getCase(id).then(record => {
      if (!active) return;
      if (!record || record.module.toLowerCase() !== 'legal' || !record.entityType) {
        setError('Legal case was not found.');
        return;
      }
      router.replace(`/legal/${encodeURIComponent(record.entityType)}/cases/${encodeURIComponent(record.id)}`);
    }).catch(reason => {
      if (active) setError(reason instanceof Error ? reason.message : 'Unable to open this legal case.');
    });
    return () => { active = false; };
  }, [id, router]);

  return <div className="space-y-4 p-6">
    {error ? <p role="alert">{error}</p> : <p role="status">Opening legal case…</p>}
    <Link href="/legal" className="text-sm text-primary underline">Back to Legal</Link>
  </div>;
}
