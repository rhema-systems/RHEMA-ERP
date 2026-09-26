'use client';

import React from 'react';
import Link from 'next/link';
import { ArrowRight, FileText, Loader2, RefreshCw, Settings } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  planningProcedureService,
  type PlanningProcedure,
} from '@/services/planning-procedure.service';
import { procedureCaseService, type ProcedureCaseSummary } from '@/services/procedure-case.service';

export default function PlanningDashboardPage() {
  const [procedures, setProcedures] = React.useState<PlanningProcedure[]>([]);
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);

  const load = React.useCallback(async () => {
    setIsLoading(true);
    const [procedureResult, caseResult] = await Promise.allSettled([
      planningProcedureService.getProcedures(),
      procedureCaseService.listModuleCases('Planning'),
    ]);

    setProcedures(procedureResult.status === 'fulfilled' ? procedureResult.value : []);

    setCases(caseResult.status === 'fulfilled' ? caseResult.value : []);
    setIsLoading(false);
  }, []);

  React.useEffect(() => {
    void load();
  }, [load]);

  const stats = React.useMemo(() => {
    const openCases = cases.filter((item) => item.status !== 'Completed').length;
    const completedCases = cases.filter((item) => item.status === 'Completed').length;
    const workflowCases = cases.filter((item) => item.usesConfiguredWorkflow).length;
    const stageCount = procedures.reduce((sum, procedure) => sum + procedure.stageCount, 0);
    const uniqueStages = new Set(cases.map((item) => item.currentStageName).filter(Boolean)).size;

    return {
      procedureCount: procedures.length,
      stageCount,
      openCases,
      completedCases,
      workflowCases,
      uniqueStages,
    };
  }, [cases, procedures]);

  const stageQueue = React.useMemo(() => {
    const counts = new Map<string, number>();
    cases
      .filter((item) => item.status !== 'Completed')
      .forEach((item) => counts.set(item.currentStageName, (counts.get(item.currentStageName) ?? 0) + 1));

    return [...counts.entries()]
      .map(([stage, count]) => ({ stage, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 6);
  }, [cases]);

  const recentCases = cases.slice(0, 8);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-2">
          <Badge variant="outline">Project Management</Badge>
          <h1 className="text-3xl font-bold tracking-tight">Planning Dashboard</h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button asChild variant="outline">
            <Link href="/administration/workflow?q=Planning">
              <Settings className="mr-2 h-4 w-4" />
              Workflow setup
            </Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
        <Card><CardHeader className="pb-2"><CardDescription>Procedures</CardDescription><CardTitle>{stats.procedureCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Stages</CardDescription><CardTitle>{stats.stageCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Open Cases</CardDescription><CardTitle>{stats.openCases}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Completed</CardDescription><CardTitle>{stats.completedCases}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Workflow Cases</CardDescription><CardTitle>{stats.workflowCases}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Active Stages</CardDescription><CardTitle>{stats.uniqueStages}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Stage Queue</CardTitle>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/planning">Workspaces</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {isLoading ? (
              <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading planning queue
              </div>
            ) : null}
            {!isLoading && stageQueue.length === 0 ? (
              <div className="py-8 text-center text-sm text-muted-foreground">No open planning stages.</div>
            ) : null}
            {stageQueue.map((item) => (
              <div key={item.stage} className="flex items-center justify-between gap-3 rounded-md border p-3">
                <div className="font-medium">{item.stage}</div>
                <Badge>{item.count}</Badge>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Recent Cases</CardTitle>
              <Button asChild variant="outline" size="sm">
                <Link href="/document-management?module=Planning">
                  <FileText className="mr-2 h-4 w-4" />
                  Documents
                </Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {isLoading ? (
              <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading planning cases
              </div>
            ) : null}
            {!isLoading && recentCases.length === 0 ? (
              <div className="py-8 text-center text-sm text-muted-foreground">No planning cases opened yet.</div>
            ) : null}
            {recentCases.map((item) => (
              <div key={item.id} className="rounded-md border p-3">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.referenceNumber || item.title}</div>
                    <div className="mt-1 text-sm text-muted-foreground">{item.currentStageName}</div>
                  </div>
                  <Badge variant={item.status === 'Completed' ? 'outline' : 'secondary'}>{item.status}</Badge>
                </div>
                <div className="mt-3">
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/development/planning/${encodeURIComponent(item.entityType)}`}>
                      Open workspace
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
