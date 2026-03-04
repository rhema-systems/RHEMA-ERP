'use client';

import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';

export default function ExternalPortalSupportHomePage() {
  const router = useRouter();

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Support</CardTitle>
          <CardDescription>Submit and track enquiries, complaints, helpdesk tickets, and service requests.</CardDescription>
        </CardHeader>
        <CardContent className="flex items-center gap-3">
          <Button onClick={() => router.push('/support/tickets')}>My Tickets</Button>
          <Button variant="outline" onClick={() => router.push('/support/tickets/new')}>
            Create Ticket
          </Button>
          <Button variant="outline" onClick={() => router.push('/support/requests')}>
            My Service Requests
          </Button>
          <Button variant="outline" onClick={() => router.push('/support/requests/new')}>
            New Service Request
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
