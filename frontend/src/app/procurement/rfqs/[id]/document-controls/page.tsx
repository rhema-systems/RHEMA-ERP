'use client';

import { useParams } from 'next/navigation';

import { TenderDocumentRegisterWorkspace } from '@/components/procurement/tender-documents/TenderDocumentRegisterWorkspace';

export default function RfqDocumentControlsPage() {
  const params = useParams<{ id: string }>();
  return (
    <TenderDocumentRegisterWorkspace
      sourceType="RequestForQuotation"
      sourceId={params.id}
    />
  );
}
