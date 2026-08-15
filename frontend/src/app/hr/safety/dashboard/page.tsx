'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  Loader2,
  AlertTriangle,
  Ambulance,
  Search,
  ClipboardX,
  Eye,
  Flame,
  FileWarning,
  CalendarClock,
  ClipboardList,
  ClipboardCheck,
  FileBadge,
  PauseCircle,
  Wrench,
  BadgeAlert,
  PowerOff,
  PackageMinus,
  Leaf,
  Scale,
  Sprout,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { safetyDashboardService } from '@/services/hr/safety-dashboard.service';

/**
 * The SHE overview. Every tile is a LIVE count computed from the registers server-side — unlike
 * the performance snapshots, nothing here is hand-reported. The groups mirror the registers the
 * later slices ship, so each tile eventually links to a filtered list.
 */
export default function SafetyDashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'safety-dashboard'],
    queryFn: () => safetyDashboardService.getDashboard(),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="p-6">
        <EmptyState
          title="Could not load the dashboard"
          description="Please try again in a moment."
        />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="SHE Dashboard"
        description="Live counts across the safety registers — what is open, due or overdue right now."
        backHref="/hr/safety"
        actions={
          <Button asChild variant="outline">
            <Link href="/hr/safety/performance">Performance snapshots</Link>
          </Button>
        }
      />

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Incidents</h2>
        <MetricTiles
          tiles={[
            {
              label: 'Open incidents',
              value: data.openIncidents,
              icon: AlertTriangle,
              tone: data.openIncidents > 0 ? 'warning' : 'default',
            },
            {
              label: 'Lost-time injuries',
              value: data.lostTimeInjuries,
              icon: Ambulance,
              tone: data.lostTimeInjuries > 0 ? 'danger' : 'default',
            },
            {
              label: 'Awaiting investigation',
              value: data.incidentsRequiringInvestigation,
              icon: Search,
            },
            {
              label: 'Overdue corrective actions',
              value: data.overdueCorrectiveActions,
              icon: ClipboardX,
              tone: data.overdueCorrectiveActions > 0 ? 'danger' : 'default',
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Hazards & risk</h2>
        <MetricTiles
          tiles={[
            { label: 'Hazards due for review', value: data.hazardsDueForReview, icon: Eye },
            {
              label: 'High-risk hazards',
              value: data.highRiskHazards,
              icon: Flame,
              tone: data.highRiskHazards > 0 ? 'danger' : 'default',
            },
            {
              label: 'Risk assessments expiring',
              value: data.riskAssessmentsExpiring,
              icon: FileWarning,
              tone: data.riskAssessmentsExpiring > 0 ? 'warning' : 'default',
            },
            {
              label: 'RAs due for review',
              value: data.riskAssessmentsDueForReview,
              icon: CalendarClock,
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Inspections & permits</h2>
        <MetricTiles
          tiles={[
            { label: 'Inspections due', value: data.inspectionsDue, icon: ClipboardList },
            {
              label: 'Open inspection findings',
              value: data.openInspectionFindings,
              icon: ClipboardCheck,
              tone: data.openInspectionFindings > 0 ? 'warning' : 'default',
            },
            { label: 'Active permits', value: data.activePermits, icon: FileBadge },
            {
              label: 'Permits expiring',
              value: data.expiringPermits,
              icon: CalendarClock,
              tone: data.expiringPermits > 0 ? 'warning' : 'default',
            },
            {
              label: 'Suspended permits',
              value: data.suspendedPermits,
              icon: PauseCircle,
              tone: data.suspendedPermits > 0 ? 'warning' : 'default',
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Equipment & PPE</h2>
        <MetricTiles
          tiles={[
            {
              label: 'Equipment due for inspection',
              value: data.equipmentDueForInspection,
              icon: Wrench,
            },
            {
              label: 'Certifications expiring',
              value: data.equipmentExpiringCertification,
              icon: BadgeAlert,
              tone: data.equipmentExpiringCertification > 0 ? 'warning' : 'default',
            },
            {
              label: 'Out of service',
              value: data.equipmentOutOfService,
              icon: PowerOff,
            },
            {
              label: 'PPE below reorder level',
              value: data.ppeBelowReorder,
              icon: PackageMinus,
              tone: data.ppeBelowReorder > 0 ? 'warning' : 'default',
            },
          ]}
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Environment</h2>
        <MetricTiles
          tiles={[
            {
              // FR-ENV-019 — expired environmental permits show red and escalate to management.
              label: 'Expired environmental permits',
              value: data.expiredEnvironmentalPermits,
              icon: FileWarning,
              tone: data.expiredEnvironmentalPermits > 0 ? 'danger' : 'default',
            },
            {
              label: 'Permit renewals due (90 days)',
              value: data.environmentalPermitsExpiringSoon,
              icon: CalendarClock,
              tone: data.environmentalPermitsExpiringSoon > 0 ? 'warning' : 'default',
            },
            {
              label: 'Open environmental incidents',
              value: data.openEnvironmentalIncidents,
              icon: Leaf,
              tone: data.openEnvironmentalIncidents > 0 ? 'warning' : 'default',
            },
            {
              label: 'Monitoring cycles due (30 days)',
              value: data.monitoringSchedulesDue,
              icon: ClipboardList,
            },
            {
              label: 'Open regulatory updates',
              value: data.regulatoryUpdatesOpen,
              icon: Scale,
            },
            {
              label: 'Active sustainability initiatives',
              value: data.sustainabilityInitiativesActive,
              icon: Sprout,
            },
          ]}
        />
      </div>
    </div>
  );
}
