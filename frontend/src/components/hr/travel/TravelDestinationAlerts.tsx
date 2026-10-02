'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Info } from 'lucide-react';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelAlertSummary } from '@/types/hr/travel-compliance';

const SEVERE = new Set(['Critical', 'Emergency']);

/**
 * The destination alerts in force over a trip (travel final closure, lane 5, T-45).
 *
 * An alert never reached the trip it warned about — nothing read its severity. This reads the approver's door
 * (`requests/{id}/destination-alerts`), so whoever decides the trip sees what the desk sees, travel permission or not.
 * A **Critical or Emergency** alert is drawn as a warning; lesser ones as one quiet line. It warns and does not refuse:
 * whether such an alert should block approval or booking is TDC's question, recorded for lane 10.
 */
export function TravelDestinationAlerts({ requestId, context }: { requestId: string; context: 'approve' | 'book' }) {
  const { data } = useQuery({
    queryKey: ['travel-destination-alerts', requestId],
    queryFn: () => travelService.getDestinationAlerts(requestId),
  });
  const alerts: StaffTravelAlertSummary[] = data ?? [];
  if (alerts.length === 0) return null;

  const severe = alerts.filter((a) => SEVERE.has(a.severityName));
  const others = alerts.filter((a) => !SEVERE.has(a.severityName));
  const where = (a: StaffTravelAlertSummary) => [a.city, a.countryName].filter(Boolean).join(', ');

  return (
    <div className="space-y-2">
      {severe.length > 0 && (
        <div className="flex items-start gap-3 rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
          <div className="space-y-1">
            <p className="font-medium">
              {severe.length === 1 ? 'A serious alert is in force' : `${severe.length} serious alerts are in force`} for
              this destination during the trip
            </p>
            <ul className="space-y-0.5">
              {severe.map((a) => (
                <li key={a.id}>
                  <span className="font-medium">{a.severityName}</span> · {a.title}
                  {where(a) ? <span className="text-muted-foreground"> — {where(a)}</span> : null}
                </li>
              ))}
            </ul>
            <p className="text-muted-foreground">
              {context === 'approve'
                ? 'Weigh it before approving — the system warns but does not refuse.'
                : 'Weigh it before booking — the system warns but does not refuse.'}
            </p>
          </div>
        </div>
      )}
      {others.length > 0 && (
        <p className="flex items-start gap-2 text-xs text-muted-foreground">
          <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <span>
            Also in force: {others.map((a) => `${a.title} (${a.severityName.toLowerCase()})`).join('; ')}.
          </span>
        </p>
      )}
    </div>
  );
}
