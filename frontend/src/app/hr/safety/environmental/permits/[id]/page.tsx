'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';
import type { SheEnvironmentalPermit } from '@/types/hr/safety-environment-compliance';

export default function EnvironmentalPermitDetailPage() {
  const params = useParams<{ id: string | string[] }>();
  const id = Array.isArray(params.id) ? params.id[0] : params.id;
  const [permit, setPermit] = useState<SheEnvironmentalPermit | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setPermit(null);
    setError(null);
    void safetyEnvironmentalComplianceService.getPermit(id).then(record => {
      if (!active) return;
      if (!record || record.id !== id) setError('Environmental permit was not found.');
      else setPermit(record);
    }).catch(reason => {
      if (active) setError(reason instanceof Error ? reason.message : 'Unable to load this environmental permit.');
    });
    return () => { active = false; };
  }, [id]);

  const date = (value: string) => new Date(value).toLocaleDateString();
  return <div className="space-y-6 p-6">
    <PageHeader title={permit?.permitName ?? 'Environmental permit'}
      description={permit?.registerNumber} backHref="/hr/safety/environmental/permits"
      actions={permit ? <Badge variant="outline">{permit.statusName}</Badge> : undefined} />
    {error ? <p role="alert" className="text-destructive">{error}</p> : !permit ?
      <p role="status">Loading environmental permit…</p> : <>
        <Card>
          <CardHeader><CardTitle>Permit details</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {[
                ['Type', permit.permitTypeName], ['Authority reference', permit.authorityReferenceNumber],
                ['Issuing body', permit.issuingBodyName], ['Responsible officer', permit.responsibleOfficerName],
                ['Location', permit.locationName], ['Issue date', date(permit.issueDate)],
                ['Expiry date', date(permit.expiryDate)], ['Current version', permit.currentVersionLabel],
              ].map(([label, value]) => <div key={label}>
                <dt className="text-sm text-muted-foreground">{label}</dt>
                <dd className="mt-1 break-words">{value || '—'}</dd>
              </div>)}
            </dl>
          </CardContent>
        </Card>
        {[
          ['Description', permit.description], ['Conditions', permit.conditions], ['Notes', permit.notes],
        ].filter(([, value]) => Boolean(value)).map(([label, value]) => <Card key={label}>
          <CardHeader><CardTitle>{label}</CardTitle></CardHeader>
          <CardContent className="whitespace-pre-wrap break-words">{value}</CardContent>
        </Card>)}
      </>}
    <Link href="/hr/safety/environmental/permits" className="text-sm text-primary underline">Back to permit register</Link>
  </div>;
}
