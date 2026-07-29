'use client';

import React from 'react';
import { useParams } from 'next/navigation';

import { AwardReadinessWorkspace } from '@/components/procurement/award-readiness/AwardReadinessWorkspace';

export default function RfqAwardReadinessPage() {
  const params = useParams();
  const rfqId = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');

  return (
    <AwardReadinessWorkspace
      sourceType="RequestForQuotation"
      sourceId={rfqId}
    />
  );
}
