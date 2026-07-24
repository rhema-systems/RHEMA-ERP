'use client';

import { useParams } from 'next/navigation';

import { TenderDocumentTemplateEditor } from '@/components/procurement/tender-documents/TenderDocumentTemplateEditor';

export default function TenderDocumentTemplateDetailPage() {
  const params = useParams<{ id: string }>();
  return <TenderDocumentTemplateEditor id={params.id} />;
}
