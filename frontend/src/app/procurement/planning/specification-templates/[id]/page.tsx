'use client';

import { useParams } from 'next/navigation';

import { ProcurementSpecificationTemplateEditor } from '@/components/procurement/specifications/ProcurementSpecificationTemplateEditor';

export default function ProcurementSpecificationTemplateDetailPage() {
  const params = useParams<{ id: string }>();
  return <ProcurementSpecificationTemplateEditor id={params.id} />;
}
