'use client';

import { useQuery } from '@tanstack/react-query';
import { leaveEncashmentService } from '@/services/hr/leave.service';

/**
 * Whether leave may be cashed in while still employed (round 5, lane L1).
 *
 * The company switch decides whether the portal shows its encashment screen at all: off — TDC's
 * setting, FR-HR-046 and the Labour Act's s.31 — unused annual leave is paid only in the final
 * settlement, and a screen that offers the route would only lead to a refusal.
 *
 * ⚠ Until the answer arrives it reports `allowed: false`, so a link never flashes up and vanishes.
 * `loaded` tells a page that must say something either way whether it knows yet.
 */
export function useEncashmentAvailability() {
  const { data, isSuccess } = useQuery({
    queryKey: ['hr', 'leave-encashments', 'availability'],
    queryFn: () => leaveEncashmentService.getAvailability(),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  return {
    allowed: data?.inServiceAllowed ?? false,
    explanation: data?.explanation ?? '',
    loaded: isSuccess,
  };
}
