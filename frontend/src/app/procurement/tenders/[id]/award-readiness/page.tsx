'use client';

import React from 'react';
import { useParams, useSearchParams } from 'next/navigation';

import { AwardReadinessWorkspace } from '@/components/procurement/award-readiness/AwardReadinessWorkspace';

export default function TenderAwardReadinessPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const tenderId = Array.isArray(params?.id)
    ? params.id[0]
    : (params?.id ?? '');
  const sourceType =
    searchParams.get('sourceType') === 'ExceptionalSourcing'
      ? 'ExceptionalSourcing'
      : 'Tender';

  return (
    <AwardReadinessWorkspace
      sourceType={sourceType}
      sourceId={tenderId}
    />
  );
}
