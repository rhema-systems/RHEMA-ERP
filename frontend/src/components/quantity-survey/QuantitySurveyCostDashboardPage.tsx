'use client';

import React, { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, BarChart3, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { projectService } from '@/services/projectService';
import { ReportModuleNavigator } from '@/components/reports/ReportModuleNavigator';
import { quantitySurveyCatalogue } from '@/components/reports/StatutoryReportCataloguePage';

const ALL = 'all';

export function QuantitySurveyCostDashboardPage() {
  const { hasPermission, hasAnyRole } = useAuth();
  const canRead =
    hasAnyRole(['SuperAdmin', 'TenantAdmin']) ||
    hasPermission('quantity-survey.reports.read');
  const [projectId, setProjectId] = useState('');
  const [sectionCode, setSectionCode] = useState(ALL);
  const [costCode, setCostCode] = useState(ALL);

  const projectsQuery = useQuery({
    queryKey: ['projects', 'quantity-survey-cost-dashboard'],
    queryFn: () => projectService.getProjects({ page: 1, pageSize: 500 }),
    enabled: canRead,
    staleTime: 5 * 60 * 1000,
  });
  const dashboardQuery = useQuery({
    queryKey: ['quantity-survey-cost-dashboard', projectId],
    queryFn: () => projectService.getQuantitySurveyCostDashboard(projectId),
    enabled: canRead && !!projectId,
  });
  const dashboard = dashboardQuery.data;

  const sections = useMemo(
    () =>
      Array.from(
        new Map(
          (dashboard?.lines ?? [])
            .filter((line) => line.sectionCode || line.sectionName)
            .map((line) => [
              line.sectionCode ?? line.sectionName!,
              {
                value: line.sectionCode ?? line.sectionName!,
                label: [line.sectionCode, line.sectionName]
                  .filter(Boolean)
                  .join(' · '),
              },
            ])
        ).values()
      ),
    [dashboard]
  );
  const costCodes = useMemo(
    () =>
      Array.from(
        new Map(
          (dashboard?.lines ?? [])
            .filter((line) => line.costCode || line.costCodeName)
            .filter(
              (line) =>
                sectionCode === ALL ||
                (line.sectionCode ?? line.sectionName) === sectionCode
            )
            .map((line) => [
              line.costCode ?? line.costCodeName!,
              {
                value: line.costCode ?? line.costCodeName!,
                label: [line.costCode, line.costCodeName]
                  .filter(Boolean)
                  .join(' · '),
              },
            ])
        ).values()
      ),
    [dashboard, sectionCode]
  );
  const visibleLines = useMemo(
    () =>
      (dashboard?.lines ?? []).filter(
        (line) =>
          (sectionCode === ALL ||
            (line.sectionCode ?? line.sectionName) === sectionCode) &&
          (costCode === ALL ||
            (line.costCode ?? line.costCodeName) === costCode)
      ),
    [costCode, dashboard, sectionCode]
  );

  const money = (value: number) =>
    new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: dashboard?.currencyCode || 'GHS',
      maximumFractionDigits: 2,
    }).format(value || 0);
  const metrics = dashboard
    ? ([
        ['Approved budget', dashboard.approvedBudget],
        ['Commitments', dashboard.committedValue],
        ['Certified value', dashboard.certifiedValue],
        ['Actual cost', dashboard.actualCost],
        ['Approved variations', dashboard.approvedVariationValue],
        ['Forecast', dashboard.forecastCost],
        ['Final projected cost', dashboard.finalProjectedCost],
        ['Cost to complete', dashboard.costToComplete],
        ['Budget variance', dashboard.budgetVariance],
      ] as const)
    : [];

  if (!canRead)
    return (
      <Card className="border-amber-200">
        <CardContent className="py-6 text-sm">
          Quantity Survey report permission is required.
        </CardContent>
      </Card>
    );

  return (
    <div className="space-y-3">
      <div className="flex min-h-9 items-center gap-2">
        <ReportModuleNavigator
          moduleName="Quantity Survey"
          modulePath="/reports/quantity-survey"
          activeReportCode="dashboard"
          items={[
            {
              code: 'dashboard',
              title: 'QS Cost Dashboard',
              group: 'Cost control',
              icon: BarChart3,
              available: true,
            },
            ...quantitySurveyCatalogue.map((item) => ({
              ...item,
              available: true,
            })),
          ]}
        />
        <h1 className="truncate text-xl font-semibold tracking-tight">
          QS Cost Dashboard
        </h1>
      </div>

      <Card>
        <CardContent className="p-2">
          <div className="flex flex-wrap items-end gap-2">
            <div className="min-w-72 flex-1 space-y-1">
              <Label className="text-xs">Project</Label>
              <Select
                value={projectId}
                onValueChange={(value) => {
                  setProjectId(value);
                  setSectionCode(ALL);
                  setCostCode(ALL);
                }}
              >
                <SelectTrigger className="h-9" aria-label="Project">
                  <SelectValue placeholder="Select assigned project" />
                </SelectTrigger>
                <SelectContent>
                  {(projectsQuery.data?.items ?? []).map((project) => (
                    <SelectItem key={project.id} value={project.id}>
                      {project.projectCode} · {project.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-56 flex-1 space-y-1">
              <Label className="text-xs">Section</Label>
              <Select
                value={sectionCode}
                onValueChange={(value) => {
                  setSectionCode(value);
                  setCostCode(ALL);
                }}
                disabled={!dashboard}
              >
                <SelectTrigger className="h-9" aria-label="Section">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All project sections</SelectItem>
                  {sections.map((item) => (
                    <SelectItem key={item.value} value={item.value}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-56 flex-1 space-y-1">
              <Label className="text-xs">Cost code</Label>
              <Select
                value={costCode}
                onValueChange={setCostCode}
                disabled={!dashboard}
              >
                <SelectTrigger className="h-9" aria-label="Cost code">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All project cost codes</SelectItem>
                  {costCodes.map((item) => (
                    <SelectItem key={item.value} value={item.value}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button
              className="h-9"
              variant="outline"
              onClick={() => void dashboardQuery.refetch()}
              disabled={!projectId || dashboardQuery.isFetching}
            >
              <RefreshCw
                className={`mr-2 h-4 w-4 ${dashboardQuery.isFetching ? 'animate-spin' : ''}`}
              />
              Refresh
            </Button>
          </div>
        </CardContent>
      </Card>

      {dashboardQuery.isError && (
        <Card className="border-red-200">
          <CardContent className="py-5 text-sm text-red-700">
            {dashboardQuery.error instanceof Error
              ? dashboardQuery.error.message
              : 'The dashboard could not be loaded.'}
          </CardContent>
        </Card>
      )}
      {!projectId && (
        <Card>
          <CardContent className="py-8 text-center text-sm text-muted-foreground">
            Select an assigned project to load its QS cost position.
          </CardContent>
        </Card>
      )}

      {dashboard && (
        <>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
            {metrics.map(([label, value]) => (
              <Card key={label}>
                <CardContent className="p-3">
                  <div className="text-xs text-muted-foreground">{label}</div>
                  <div className="mt-1 text-base font-semibold tabular-nums">
                    {money(value)}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
          <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
            <span>
              {dashboard.projectCode} · {dashboard.projectTitle} ·{' '}
              <Badge variant="outline">{dashboard.projectStatus}</Badge>
            </span>
            <span>{dashboard.forecastBasis}</span>
          </div>
          {dashboard.warnings.length > 0 && (
            <div className="space-y-1 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-100">
              {dashboard.warnings.map((warning) => (
                <div key={warning} className="flex gap-2">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>{warning}</span>
                </div>
              ))}
            </div>
          )}
          <Card>
            <CardContent className="p-0">
              <div className="flex items-center justify-between border-b px-3 py-2">
                <h2 className="text-sm font-semibold">
                  BoQ cost-line drilldown
                </h2>
                <span className="text-xs text-muted-foreground">
                  {visibleLines.length} line(s)
                </span>
              </div>
              <div className="overflow-x-auto">
                <Table className="min-w-[1320px]">
                  <TableHeader>
                    <TableRow>
                      <TableHead>Package / line</TableHead>
                      <TableHead>Section</TableHead>
                      <TableHead>Cost code</TableHead>
                      <TableHead className="min-w-72">Description</TableHead>
                      <TableHead className="text-right">Quantity</TableHead>
                      <TableHead className="text-right">Budget</TableHead>
                      <TableHead className="text-right">Committed</TableHead>
                      <TableHead className="text-right">Actual</TableHead>
                      <TableHead className="text-right">Forecast</TableHead>
                      <TableHead className="text-right">Variance</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {visibleLines.map((line) => (
                      <TableRow key={line.boqItemId}>
                        <TableCell>
                          <div className="font-medium">
                            {line.packageCode ?? line.packageName ?? 'Project'}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {line.lineNumber ?? line.itemCode ?? 'Line'}
                          </div>
                        </TableCell>
                        <TableCell>
                          {[line.sectionCode, line.sectionName]
                            .filter(Boolean)
                            .join(' · ') || 'Unclassified'}
                        </TableCell>
                        <TableCell>
                          {[line.costCode, line.costCodeName]
                            .filter(Boolean)
                            .join(' · ') || 'Unclassified'}
                        </TableCell>
                        <TableCell>{line.description}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {line.quantity} {line.unitOfMeasure ?? ''}
                        </TableCell>
                        {[
                          line.budgetAmount,
                          line.committedAmount,
                          line.actualAmount,
                          line.forecastAmount,
                          line.forecastVarianceAmount,
                        ].map((value, index) => (
                          <TableCell
                            key={index}
                            className="text-right tabular-nums"
                          >
                            {money(value)}
                          </TableCell>
                        ))}
                      </TableRow>
                    ))}
                    {visibleLines.length === 0 && (
                      <TableRow>
                        <TableCell
                          colSpan={10}
                          className="h-24 text-center text-muted-foreground"
                        >
                          No cost lines match the selected project filters.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
