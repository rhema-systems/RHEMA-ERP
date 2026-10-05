'use client';

import { Button } from '@/components/ui/button';
import type { PagedResult } from '@/types/hr/common';

/**
 * The page controls under a company-schedule register (lane 2g-1): the registers are paged on the server now, so the
 * count is of everything the filters find, not of what one page shows.
 */
export function RegisterPager<T>({
  data,
  noun,
  onPage,
}: {
  data?: PagedResult<T>;
  /** "events", "bookings". */
  noun: string;
  onPage: (page: number) => void;
}) {
  if (!data || data.totalCount === 0) return null;
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 pt-4">
      <p className="text-sm text-muted-foreground">
        {data.totalCount} {noun}
        {data.totalPages > 1 ? ` · page ${data.page} of ${data.totalPages}` : ''}
      </p>
      {data.totalPages > 1 && (
        <div className="flex gap-2">
          <Button variant="outline" size="sm" disabled={!data.hasPrevious} onClick={() => onPage(data.page - 1)}>
            Previous
          </Button>
          <Button variant="outline" size="sm" disabled={!data.hasNext} onClick={() => onPage(data.page + 1)}>
            Next
          </Button>
        </div>
      )}
    </div>
  );
}
