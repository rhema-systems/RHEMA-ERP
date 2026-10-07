'use client';

import { TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

/**
 * Says a diary is incomplete (company-schedule lane 5b, R4-10A.3). A source the server could not read used to vanish
 * silently — and in a diary a missing source reads as free time somebody does not have. The server now names each
 * source that failed (`incompleteSources`: "leave", "training", "meetings and events"…); this says which, and not to
 * read their absence as free.
 */
export function IncompleteDiaryBanner({ sources }: { sources?: string[] | null }) {
  if (!sources?.length) return null;
  const list =
    sources.length === 1 ? sources[0] : `${sources.slice(0, -1).join(', ')} and ${sources[sources.length - 1]}`;
  return (
    <Alert variant="destructive">
      <TriangleAlert className="h-4 w-4" />
      <AlertTitle>This schedule is incomplete</AlertTitle>
      <AlertDescription>
        The {list} could not be read just now, so none of it is shown. A gap here is not free time — reload the page to
        try again.
      </AlertDescription>
    </Alert>
  );
}
