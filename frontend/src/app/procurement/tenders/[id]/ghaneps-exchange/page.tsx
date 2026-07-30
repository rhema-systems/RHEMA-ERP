'use client';

import { useParams, useSearchParams } from 'next/navigation';

import { GhanepsExchangeWorkspace } from '@/components/procurement/ghaneps-exchange/GhanepsExchangeWorkspace';

export default function TenderGhanepsExchangePage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const sourceId = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');
  const sourceType =
    searchParams.get('sourceType') === 'ExceptionalSourcing'
      ? 'ExceptionalSourcing'
      : 'Tender';

  return (
    <GhanepsExchangeWorkspace
      sourceType={sourceType}
      sourceId={sourceId}
    />
  );
}
