'use client';

/**
 * The employee's own leave calendar — the third entry point of closure plan slice E1.
 *
 * Scope `Mine` resolves from the token, so there is no employee picker and nothing to authorize
 * beyond having a linked employee record — the portal rule for every self-service surface.
 */

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ArrowLeft } from 'lucide-react';
import { LeaveCalendar } from '@/components/hr/leave/LeaveCalendar';

export default function MyLeaveCalendarPage() {
  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="-ml-2 mb-2">
          <Link href="/me/leave">
            <ArrowLeft className="mr-1 h-4 w-4" /> My Leave
          </Link>
        </Button>
        <h1 className="text-2xl font-bold tracking-tight">My leave calendar</h1>
      </div>

      <LeaveCalendar
        scope="Mine"
        title="My year"
        description="Your leave, with public holidays underneath."
        hrefFor={(e) => `/me/leave/${e.id}`}
      />
    </div>
  );
}
