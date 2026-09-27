'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { FixedAsset } from '@/types/fixed-assets';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export default function FixedAssetDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [asset, setAsset] = useState<FixedAsset | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setAsset(null);
    setError('');
    setLoading(true);
    void fixedAssetsDataService.getAssetById(encodeURIComponent(id)).then(result => {
      if (active) setAsset(result);
    }).catch((cause: unknown) => {
      if (!active) return;
      const problem = cause && typeof cause === 'object' ? cause as { detail?: string; code?: string; message?: string } : {};
      setError([problem.detail || problem.message || 'Unable to load this asset.', problem.code].filter(Boolean).join(' '));
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [id]);

  return (
    <main className="space-y-4 p-6">
      <Button variant="outline" asChild><Link href="/finance/fixed-assets/register">Back to asset register</Link></Button>
      {loading ? <p role="status">Loading asset…</p> : error ? <p role="alert" className="text-destructive">{error}</p> : asset ? (
        <Card>
          <CardHeader><CardTitle>{asset.assetCode} · {asset.name}</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid gap-4 sm:grid-cols-2">
              {[
                ['Status', asset.status],
                ['Category', asset.fixedAssetCategoryName || 'Unassigned'],
                ['Location', asset.location || '—'],
                ['Purchase date', asset.purchaseDate?.slice(0, 10) || '—'],
                ['Description', asset.description || '—'],
              ].map(([label, value]) => (
                <div key={label}><dt className="text-sm text-muted-foreground">{label}</dt><dd className="whitespace-pre-wrap">{value}</dd></div>
              ))}
            </dl>
          </CardContent>
        </Card>
      ) : <p role="alert">Asset not found.</p>}
    </main>
  );
}
