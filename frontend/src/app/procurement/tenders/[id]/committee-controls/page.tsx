'use client';

import { useParams } from 'next/navigation';

import { EvaluationCommitteeWorkspace } from '@/components/procurement/evaluation-committee/EvaluationCommitteeWorkspace';

export default function TenderEvaluationCommitteeControlsPage() {
  const params = useParams<{ id: string }>();
  return (
    <EvaluationCommitteeWorkspace
      sourceType="Tender"
      sourceId={params.id}
    />
  );
}
