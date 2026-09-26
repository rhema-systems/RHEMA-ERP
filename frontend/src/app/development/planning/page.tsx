'use client';

import React from 'react';
import Link from 'next/link';
import {
  ArrowRight,
  LayoutDashboard,
  Loader2,
  Settings,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  planningProcedureService,
  type PlanningProcedure,
} from '@/services/planning-procedure.service';

export default function DevelopmentPlanningPage() {
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

      <div className="flex flex-wrap gap-2">
        <Button asChild variant="outline">
          <Link href="/estate/land-management">Estate Land Bank</Link>
        </Button>
        <Button asChild variant="outline">
          <Link href="/document-management?module=Planning">Document Mngt</Link>
        </Button>
        <Button asChild variant="outline">
          <Link href="/reports?module=planning">Planning Reports</Link>
        </Button>
        <Button asChild variant="outline">
          <Link href="/development/project-approvals">Approvals</Link>
        </Button>
      </div>

      <div className="space-y-2">
        <h2 className="text-lg font-semibold">Planning workspaces</h2>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Workspace</TableHead>
              <TableHead>Code</TableHead>
              <TableHead className="w-24 text-right">Open</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={3} className="py-10 text-center text-muted-foreground">
                  <span className="inline-flex items-center gap-2">
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Loading planning workspaces
                  </span>
                </TableCell>
              </TableRow>
            ) : loadError ? (
              <TableRow>
                <TableCell colSpan={3} className="py-10 text-center text-muted-foreground">{loadError}</TableCell>
              </TableRow>
            ) : procedures.map((procedure) => (
              <TableRow key={procedure.entityType}>
                <TableCell className="font-medium">{procedure.title}</TableCell>
                <TableCell className="text-muted-foreground">{procedure.entityType}</TableCell>
                <TableCell className="text-right">
                  <Button asChild variant="ghost" size="icon" title={`Open ${procedure.title}`}>
                    <Link href={`/development/planning/${encodeURIComponent(procedure.entityType)}`} aria-label={`Open ${procedure.title}`}>
                      <ArrowRight className="h-4 w-4" />
                    </Link>
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
