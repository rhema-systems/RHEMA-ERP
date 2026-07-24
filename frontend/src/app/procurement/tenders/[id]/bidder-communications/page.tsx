'use client';

import { useParams, useSearchParams } from 'next/navigation';

import { BidderCommunicationWorkspace } from '@/components/procurement/bidder-communications/BidderCommunicationWorkspace';

export default function TenderBidderCommunicationsPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const sourceId = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');
  const sourceType =
    searchParams.get('sourceType') === 'ExceptionalSourcing'
      ? 'ExceptionalSourcing'
      : 'Tender';

  return (
    <BidderCommunicationWorkspace
      sourceType={sourceType}
      sourceId={sourceId}
    />
  );
}
