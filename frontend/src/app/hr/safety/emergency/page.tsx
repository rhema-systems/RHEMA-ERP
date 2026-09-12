'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Plus, Siren } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { safetyEmergencyService } from '@/services/hr/safety-emergency.service';
import { SHE_EMERGENCY_TYPE_OPTIONS } from '@/types/hr/safety-equipment';
import type { EmergencyPlanSummary } from '@/types/hr/safety-equipment';

/**
 * Emergency preparedness (FRD §10): the plan register with review tracking, plus the standing
 * strips for upcoming drills and expiring response-team certificates. Review due dates,
 * certificate expiries and drill dates also raise automatic reminders (slice-13 engine);
 * these strips stay the work queues.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const typeLabel = (v: string) =>
  SHE_EMERGENCY_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

function PlanTable({ items, emptyText }: { items: EmergencyPlanSummary[]; emptyText: string }) {
  if (items.length === 0) {
    return <EmptyState title="Nothing here" description={emptyText} icon={Siren} />;
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Number</TableHead>
              <TableHead>Plan</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Location</TableHead>
              <TableHead>Owner</TableHead>
              <TableHead>Next review</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((p) => {
              const reviewOverdue = new Date(p.nextReviewDate) < new Date();
              return (
                <TableRow key={p.id}>
                  <TableCell>
                    <Link
                      href={`/hr/safety/emergency/${p.id}`}
                      className="font-mono text-primary hover:underline"
                    >
                      {p.planNumber}
                    </Link>
                  </TableCell>
                  <TableCell className="font-medium">{p.planName}</TableCell>
                  <TableCell>{typeLabel(p.type)}</TableCell>
                  <TableCell>{p.locationName ?? 'All sites'}</TableCell>
                  <TableCell>{p.planOwnerName}</TableCell>
                  <TableCell>
                    {reviewOverdue && p.isActive ? (
                      <Badge variant="destructive">Overdue {fmtDate(p.nextReviewDate)}</Badge>
                    ) : (
                      <span className="text-sm">{fmtDate(p.nextReviewDate)}</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={p.isActive ? 'Active' : 'Inactive'} />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function EmergencyPlansPage() {
  const [tab, setTab] = useState('all');

  const { data: all = [] } = useQuery({
    queryKey: ['hr', 'safety-emergency', 'plans'],
    queryFn: () => safetyEmergencyService.getPlans(),
  });
  const { data: dueForReview = [] } = useQuery({
    queryKey: ['hr', 'safety-emergency', 'plans', 'due-for-review'],
    queryFn: () => safetyEmergencyService.getPlansDueForReview(30),
  });
  const { data: upcomingDrills = [] } = useQuery({
    queryKey: ['hr', 'safety-emergency', 'drills', 'upcoming'],
    queryFn: () => safetyEmergencyService.getUpcomingDrills(30),
  });
  const { data: expiringCerts = [] } = useQuery({
    queryKey: ['hr', 'safety-emergency', 'team', 'expiring-certs'],
    queryFn: () => safetyEmergencyService.getExpiringTeamCertificates(30),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Emergency Preparedness"
        description="Emergency plans with assembly points, contact trees, drills and response teams. Plan reviews, team certificates and upcoming drills raise automatic reminders; these views stay the work queues."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/emergency/new">
              <Plus className="mr-2 h-4 w-4" /> New plan
            </Link>
          </Button>
        }
      />

      {upcomingDrills.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Upcoming drills ({upcomingDrills.length})</CardTitle>
            <CardDescription>Scheduled within the next 30 days.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {upcomingDrills.map((d) => (
              <Badge key={d.id} variant="secondary">
                {d.drillNumber} · {d.drillName} · {fmtDate(d.nextDrillScheduledDate)}
              </Badge>
            ))}
          </CardContent>
        </Card>
      )}

      {expiringCerts.length > 0 && (
        <Card className="border-destructive/50">
          <CardHeader>
            <CardTitle className="text-base text-destructive">
              Response-team certificates expiring ({expiringCerts.length})
            </CardTitle>
            <CardDescription>
              Within 30 days — arrange refresher training and recertification.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {expiringCerts.map((t) => (
              <Badge key={t.id} variant="outline" className="border-destructive/50">
                {t.employeeName} · {t.role}
                {t.planName ? ` · ${t.planName}` : ''} · {fmtDate(t.certificateExpiryDate)}
              </Badge>
            ))}
          </CardContent>
        </Card>
      )}

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="all">All plans ({all.length})</TabsTrigger>
          <TabsTrigger value="due-review">Due for review ({dueForReview.length})</TabsTrigger>
        </TabsList>
        <TabsContent value="all" className="mt-4">
          <PlanTable items={all} emptyText="No emergency plans yet. Every site needs at least a fire plan." />
        </TabsContent>
        <TabsContent value="due-review" className="mt-4">
          <PlanTable
            items={dueForReview}
            emptyText="No active plan is due for review in the next 30 days."
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
