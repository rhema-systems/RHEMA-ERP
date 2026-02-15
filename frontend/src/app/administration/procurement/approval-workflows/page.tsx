'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ArrowRight, Workflow } from 'lucide-react';

export default function ApprovalWorkflowsRedirectPage() {
  const router = useRouter();

  useEffect(() => {
    // Keep the Procurement menu link, but centralize workflow administration in one place.
    router.replace('/administration/workflow');
  }, [router]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Approval Workflows</h1>
        <p className="text-muted-foreground">
          Redirecting to Workflow Administration...
        </p>
      </div>

      <Card>
        <CardContent className="pt-6">
          <div className="flex items-start gap-3">
            <Workflow className="h-5 w-5 text-muted-foreground mt-0.5" />
            <div className="space-y-2">
              <p className="text-sm">
                Approval workflows are managed centrally under <strong>Administration → Workflow</strong> so all modules (Procurement, Maintenance, etc.) use the same workflow engine.
              </p>
              <Button onClick={() => router.push('/administration/workflow')} variant="outline">
                Open Workflow Administration
                <ArrowRight className="h-4 w-4 ml-2" />
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

