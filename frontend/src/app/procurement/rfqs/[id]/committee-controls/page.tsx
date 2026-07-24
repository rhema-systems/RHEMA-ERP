'use client';

import { useParams } from 'next/navigation';

import { EvaluationCommitteeWorkspace } from '@/components/procurement/evaluation-committee/EvaluationCommitteeWorkspace';

export default function RfqEvaluationCommitteeControlsPage() {
  const params = useParams<{ id: string }>();
  return (
    <EvaluationCommitteeWorkspace
      sourceType="RequestForQuotation"
      sourceId={params.id}
    />
  );
}
