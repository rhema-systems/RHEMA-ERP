'use client';

import { useParams } from 'next/navigation';

import { PropertyRequestsView } from '../page';

export default function PropertyRequestDetailPage() {
  const params = useParams<{ id: string }>();

  return <PropertyRequestsView selectedRequestId={params.id} />;
}
