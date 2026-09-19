'use client';

import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';

export default function EstateProcedureCasePage() {
  const router = useRouter();
  const params = useParams<{
    entityType?: string | string[];
    caseId?: string | string[];
  }>();
  const entityTypeValue = Array.isArray(params?.entityType)
    ? params.entityType[0]
    : params?.entityType;
  const caseIdValue = Array.isArray(params?.caseId)
    ? params.caseId[0]
    : params?.caseId;
  const entityType = entityTypeValue ? decodeURIComponent(entityTypeValue) : '';
  const caseId = caseIdValue ? decodeURIComponent(caseIdValue) : '';

  return (
    <div className="space-y-4">
      <Button
        variant="ghost"
        className="w-fit gap-2 px-0"
        onClick={() => {
          if (window.history.length > 1) {
            router.back();
            return;
          }

          router.push(`/estate/${encodeURIComponent(entityType)}`);
        }}
      >
        <ArrowLeft className="h-4 w-4" />
        Back to cases
      </Button>
      <ProcedureCaseWorkspace
        module="Estate"
        entityType={entityType}
        defaultTitle={entityType}
        caseId={caseId}
        caseBasePath={`/estate/${encodeURIComponent(entityType)}`}
        detailOnly
      />
    </div>
  );
}
