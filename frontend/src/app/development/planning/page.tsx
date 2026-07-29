'use client';

import React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  BadgeCheck,
  ClipboardCheck,
  ClipboardList,
  FileCheck2,
  FilePenLine,
  FileText,
  LayoutDashboard,
  Loader2,
  Map,
  MapPin,
  MessageSquare,
  RefreshCw,
  Search,
  Settings,
  Users,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  planningProcedureService,
  type PlanningProcedure,
} from '@/services/planning-procedure.service';

const procedureIcons: Record<string, React.ComponentType<{ className?: string }>> = {
  BadgeCheck,
  ClipboardCheck,
  ClipboardList,
  FileCheck2,
  FilePenLine,
  FileText,
  Map,
  MapPin,
  MessageSquare,
  RefreshCw,
  Search,
  Users,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  blue: 'text-blue-700 dark:text-blue-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  indigo: 'text-indigo-700 dark:text-indigo-300',
  orange: 'text-orange-700 dark:text-orange-300',
  purple: 'text-purple-700 dark:text-purple-300',
  rose: 'text-rose-700 dark:text-rose-300',
  sky: 'text-sky-700 dark:text-sky-300',
  slate: 'text-slate-700 dark:text-slate-200',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
};

export default function DevelopmentPlanningPage() {
  const router = useRouter();
  const [procedures, setProcedures] = React.useState<PlanningProcedure[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedures = async () => {
      try {
        const data = await planningProcedureService.getProcedures();
        if (mounted) {
          setProcedures(data);
          setLoadError(data.length === 0 ? 'No planning procedures were returned by the API.' : null);
        }
      } catch {
        if (mounted) {
          setProcedures([]);
          setLoadError('Unable to load planning procedures from the API.');
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadProcedures();

    return () => {
      mounted = false;
    };
  }, []);

  const totalStages = procedures.reduce((sum, procedure) => sum + procedure.stageCount, 0);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Project Management
          </Badge>
          <h1 className="text-3xl font-bold tracking-tight">Planning</h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/development/planning/dashboard">
              <LayoutDashboard className="mr-2 h-4 w-4" />
              Dashboard
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/administration/workflow?q=Planning">
              <Settings className="mr-2 h-4 w-4" />
              Workflow setup
            </Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Procedures</CardDescription>
            <CardTitle>{procedures.length}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Stages</CardDescription>
            <CardTitle>{totalStages}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Source</CardDescription>
            <CardTitle className="text-base">Project Planning</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>DMS</CardDescription>
            <CardTitle className="text-base">Enabled</CardTitle>
          </CardHeader>
        </Card>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button asChild variant="outline">
          <Link href="/estate/land-management">Estate Land Bank</Link>
        </Button>
        <Button asChild variant="outline">
          <Link href="/document-management?module=Planning">Document Mngt</Link>
        </Button>
        <Button asChild variant="outline">
          <Link href="/development/project-approvals">Approvals</Link>
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {!isLoading && loadError ? (
          <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
            <CardContent className="py-12 text-center text-sm text-muted-foreground">
              {loadError}
            </CardContent>
          </Card>
        ) : null}
        {isLoading ? (
          <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
            <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading planning procedures
            </CardContent>
          </Card>
        ) : null}
        {!isLoading &&
          procedures.map((procedure) => {
            const Icon = procedureIcons[procedure.icon] || ClipboardList;
            const accent = accentClasses[procedure.accent] || accentClasses.slate;

            return (
              <Card key={procedure.entityType} className="border-border bg-card text-card-foreground">
                <CardHeader className="space-y-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                      <Icon className={`h-5 w-5 ${accent}`} />
                    </div>
                    <Badge variant="secondary">{procedure.stageCount} stages</Badge>
                  </div>
                  <div>
                    <CardTitle className="text-base leading-6">{procedure.title}</CardTitle>
                    <CardDescription className="mt-1">{procedure.source}</CardDescription>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{procedure.entityType}</Badge>
                    <Badge variant="outline">Live cases</Badge>
                  </div>
                  <Button
                    variant="outline"
                    className="w-full justify-between"
                    onClick={() => router.push(`/development/planning/${encodeURIComponent(procedure.entityType)}`)}
                  >
                    Open workspace
                    <ArrowRight className="h-4 w-4" />
                  </Button>
                </CardContent>
              </Card>
            );
          })}
      </div>
    </div>
  );
}
