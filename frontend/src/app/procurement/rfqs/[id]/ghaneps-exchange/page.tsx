'use client';

import { useParams } from 'next/navigation';

import { GhanepsExchangeWorkspace } from '@/components/procurement/ghaneps-exchange/GhanepsExchangeWorkspace';

export default function RfqGhanepsExchangePage() {
  const params = useParams();
  const sourceId = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');

  return (
    <GhanepsExchangeWorkspace
      sourceType="RequestForQuotation"
      sourceId={sourceId}
    />
  );
}
