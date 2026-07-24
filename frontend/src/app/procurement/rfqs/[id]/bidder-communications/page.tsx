'use client';

import { useParams } from 'next/navigation';

import { BidderCommunicationWorkspace } from '@/components/procurement/bidder-communications/BidderCommunicationWorkspace';

export default function RfqBidderCommunicationsPage() {
  const params = useParams();
  const sourceId = Array.isArray(params?.id) ? params.id[0] : (params?.id ?? '');

  return (
    <BidderCommunicationWorkspace
      sourceType="RequestForQuotation"
      sourceId={sourceId}
    />
  );
}
