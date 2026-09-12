'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  ClipboardCheck,
  FileWarning,
  HardHat,
  Megaphone,
  OctagonX,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { safetyPpeService } from '@/services/hr/safety-ppe.service';
import { safetyRiskAssessmentService } from '@/services/hr/safety-risk-assessment.service';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import { safetyStopWorkService } from '@/services/hr/safety-stop-work.service';

/**
 * My Safety — the personal safety hub (area 25 slice 8, spec #33).
 *
 * Built from nothing: the census found the pieces (PPE mine, stop-work mine, environmental mine)
 * but no hub. Every figure comes from the same self read its tile links to, and every read is
 * token-scoped. Reporting a concern is deliberately the loudest thing on the page.
 */
export default function MySafetyPage() {
  const { data: ppe = [] } = useQuery({
    queryKey: ['me', 'safety', 'ppe'],
    queryFn: () => safetyPpeService.getMyIssuances(),
  });
  const { data: acknowledgements = [] } = useQuery({
    queryKey: ['me', 'safety', 'risk-acknowledgements'],
    queryFn: () => safetyRiskAssessmentService.getMineForAcknowledgement(),
  });
  const { data: incidents = [] } = useQuery({
    queryKey: ['me', 'safety', 'incidents'],
    queryFn: () => safetyIncidentService.getMine(),
  });
  const { data: stopWork = [] } = useQuery({
    queryKey: ['me', 'safety', 'stop-work'],
    queryFn: () => safetyStopWorkService.getMine(),
  });

  const held = ppe.filter((i) => !i.isReturned);
  const expired = held.filter((i) => i.expiryDate && new Date(i.expiryDate) < new Date());
  const awaiting = acknowledgements.filter((a) => !a.acknowledgedByMe);
  const openStopWork = stopWork.filter((o) => o.status !== 'Cleared' && o.status !== 'Cancelled');

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Safety"
        description="Your protective equipment, the risk assessments that apply to you, and the concerns you have raised."
        backHref="/me"
      />

      <Link href="/me/safety/report">
        <Card className="border-destructive/50 hover:bg-destructive/5 transition-colors">
          <CardContent className="flex items-start gap-3 p-4">
            <Megaphone className="text-destructive mt-0.5 h-6 w-6 shrink-0" />
            <div>
              <p className="font-medium">Report a concern</p>
              <p className="text-muted-foreground text-sm">
                Incident, near miss, hazard, environmental incident — or stop dangerous work on the
                spot. Anyone can report; no role needed.
              </p>
            </div>
          </CardContent>
        </Card>
      </Link>

      <div className="grid gap-4 md:grid-cols-3">
        <Link href="/me/safety/risk-assessments">
          <Card className="hover:bg-muted/50 h-full transition-colors">
            <CardContent className="flex items-start gap-3 p-4">
              <ClipboardCheck className="text-muted-foreground mt-0.5 h-5 w-5 shrink-0" />
              <div>
                <p className="flex items-center gap-2 font-medium">
                  My Risk Assessments
                  {awaiting.length > 0 && <Badge>{awaiting.length} to acknowledge</Badge>}
                </p>
                <p className="text-muted-foreground text-sm">
                  {awaiting.length > 0
                    ? 'Read and sign the assessments that cover your work.'
                    : 'You are up to date with every active assessment.'}
                </p>
              </div>
            </CardContent>
          </Card>
        </Link>

        <Link href="/me/safety/ppe">
          <Card className="hover:bg-muted/50 h-full transition-colors">
            <CardContent className="flex items-start gap-3 p-4">
              <HardHat className="text-muted-foreground mt-0.5 h-5 w-5 shrink-0" />
              <div>
                <p className="flex items-center gap-2 font-medium">
                  My PPE
                  {expired.length > 0 && (
                    <Badge variant="destructive">{expired.length} expired</Badge>
                  )}
                </p>
                <p className="text-muted-foreground text-sm">
                  {held.length === 0
                    ? 'Nothing issued to you yet.'
                    : `${held.length} item${held.length === 1 ? '' : 's'} in your care.`}
                </p>
              </div>
            </CardContent>
          </Card>
        </Link>

        <Link href="/me/safety/reports">
          <Card className="hover:bg-muted/50 h-full transition-colors">
            <CardContent className="flex items-start gap-3 p-4">
              <FileWarning className="text-muted-foreground mt-0.5 h-5 w-5 shrink-0" />
              <div>
                <p className="flex items-center gap-2 font-medium">
                  My Reports
                  {openStopWork.length > 0 && (
                    <Badge variant="destructive">
                      <OctagonX className="mr-1 h-3 w-3" />
                      {openStopWork.length} stop-work open
                    </Badge>
                  )}
                </p>
                <p className="text-muted-foreground text-sm">
                  {incidents.length + stopWork.length === 0
                    ? 'Concerns you raise are tracked here.'
                    : 'Follow what happened to the concerns you raised.'}
                </p>
              </div>
            </CardContent>
          </Card>
        </Link>
      </div>
    </div>
  );
}
