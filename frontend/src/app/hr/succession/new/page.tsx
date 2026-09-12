'use client';

import { PageHeader } from '@/components/hr/common/PageHeader';
import { SuccessionPlanForm } from '@/components/hr/succession/SuccessionPlanForm';

export default function NewSuccessionPlanPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="New succession plan"
        description="Created as a draft. It becomes the position's active version only once it is approved — which supersedes whatever plan the post had before."
        backHref="/hr/succession"
      />
      <SuccessionPlanForm />
    </div>
  );
}
