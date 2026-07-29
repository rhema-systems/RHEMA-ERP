'use client';

import { useParams } from 'next/navigation';

import { TenderDocumentRegisterWorkspace } from '@/components/procurement/tender-documents/TenderDocumentRegisterWorkspace';

export default function TenderDocumentControlsPage() {
  const params = useParams<{ id: string }>();
  return (
    <TenderDocumentRegisterWorkspace
      sourceType="Tender"
      sourceId={params.id}
    />
  );
}
