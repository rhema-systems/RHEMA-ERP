'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  BadgeCheck,
  ClipboardCheck,
  ClipboardList,
  FileCheck2,
  FilePenLine,
  FileText,
  Loader2,
  Map,
  MapPin,
  MessageSquare,
  RefreshCw,
  Search,
  Users,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  planningFallbackProcedures,
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
  const [procedures, setProcedures] = React.useState<PlanningProcedure[]>(planningFallbackProcedures);
  const [isLoading, setIsLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedures = async () => {
      try {
        const data = await planningProcedureService.getProcedures();
        if (mounted && data.length > 0) {
          setProcedures(data);
        }
      } catch {
        if (mounted) {
          setProcedures(planningFallbackProcedures);
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

  const openWorkspace = (entityType: string) => {
    router.push(`/development/planning/${encodeURIComponent(entityType)}`);
  };

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-2">
            <Badge variant="outline" className="w-fit">
              Development planning
            </Badge>
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                Planning Procedures
              </h1>
              <p className="mt-2 max-w-3xl text-sm leading-6 text-muted-foreground">
                SOP workspaces for land use vetting, change of use, planning schemes, site plans, searches, permit conformity, regularization, inspections, and committee reporting.
              </p>
            </div>
          </div>
          <Button variant="outline" onClick={() => router.push('/administration/workflow?q=Planning')}>
            Workflow setup
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {isLoading ? (
            <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
              <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading planning procedures
              </CardContent>
            </Card>
          ) : null}
          {!isLoading && procedures.map((procedure) => {
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
                  <p className="text-sm leading-6 text-muted-foreground">{procedure.summary}</p>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{procedure.entityType}</Badge>
                    <Badge variant="outline">SOP workspace</Badge>
                  </div>
                  <Button variant="outline" className="w-full justify-between" onClick={() => openWorkspace(procedure.entityType)}>
                    Open workspace
                    <ArrowRight className="h-4 w-4" />
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </div>
      </div>
    </div>
  );
}
