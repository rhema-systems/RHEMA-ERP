'use client';

import React from 'react';
import { useRouter } from 'next/navigation';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export default function FleetComplianceTemplatesMovedPage() {
  const router = useRouter();

  React.useEffect(() => {
    router.replace('/administration/maintenance/fleet-compliance-templates');
  }, [router]);

  return (
    <div className="p-6">
      <Card>
        <CardHeader>
          <CardTitle>Compliance Templates</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="text-sm text-muted-foreground">
            This page moved to the Administration section.
          </div>
          <Button variant="outline" onClick={() => router.push('/administration/maintenance/fleet-compliance-templates')}>
            Go to Compliance Templates
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

