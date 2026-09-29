'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Copy, Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  CopyOrientationProgramDialog,
  type ProgramCopySource,
} from '@/components/hr/orientation/CopyDialogs';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import {
  ORIENTATION_PROGRAM_STATUS_OPTIONS,
  ORIENTATION_PROGRAM_TYPE_OPTIONS,
  ORIENTATION_PRIORITY_OPTIONS,
} from '@/types/hr/orientation';
import type {
  OrientationProgramStatus,
  OrientationProgramType,
} from '@/types/hr/orientation';

const ALL = '__all__';

const PRIORITY_VARIANT: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Mandatory: 'destructive',
  Critical: 'destructive',
  High: 'secondary',
  Medium: 'outline',
  Low: 'outline',
};

const label = (options: { value: string; label: string }[], value: string) =>
  options.find((o) => o.value === value)?.label ?? value;

/**
 * The induction catalogue. Filtering is client-side over the full list rather than round-tripping
 * the per-status/per-type endpoints — the catalogue is tens of rows, not thousands, and a single
 * fetch keeps the filters instant and the counts consistent across them.
 */
export default function OrientationProgramsPage() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ALL);
  const [type, setType] = useState<string>(ALL);
  const [copying, setCopying] = useState<ProgramCopySource | null>(null);

  const { data: programs = [], isLoading } = useQuery({
    queryKey: ['hr', 'orientation-programs'],
    queryFn: () => orientationProgramService.getAll(),
  });

  const term = search.trim().toLowerCase();
  const filtered = programs.filter((p) => {
    if (status !== ALL && p.status !== (status as OrientationProgramStatus)) return false;
    if (type !== ALL && p.programType !== (type as OrientationProgramType)) return false;
    if (!term) return true;
    return (
      p.title.toLowerCase().includes(term) ||
      p.programCode.toLowerCase().includes(term) ||
      (p.categoryName ?? '').toLowerCase().includes(term)
    );
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation Programmes"
        description="The induction catalogue — each programme carries its modules, content, prerequisites and assessment."
        backHref="/administration/hr/orientation"
        actions={
          <Button asChild>
            <Link href="/administration/hr/orientation/programs/new">
              <Plus className="mr-2 h-4 w-4" />
              New programme
            </Link>
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <div className="relative min-w-[240px] flex-1">
            <Search className="text-muted-foreground absolute left-2.5 top-2.5 h-4 w-4" />
            <Input
              placeholder="Search by title, code or category…"
              className="pl-8"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>All statuses</SelectItem>
              {ORIENTATION_PROGRAM_STATUS_OPTIONS.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={type} onValueChange={setType}>
            <SelectTrigger className="w-[190px]">
              <SelectValue placeholder="Type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>All types</SelectItem>
              {ORIENTATION_PROGRAM_TYPE_OPTIONS.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <p className="text-muted-foreground p-6 text-sm">Loading programmes…</p>
          ) : filtered.length === 0 ? (
            <EmptyState
              title={programs.length === 0 ? 'No programmes yet' : 'No matching programmes'}
              description={
                programs.length === 0
                  ? 'Create an induction programme to start enrolling employees.'
                  : 'Try a different search or clear the filters.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead className="text-right">Modules</TableHead>
                  <TableHead className="text-right">Enrolled</TableHead>
                  <TableHead className="text-right">Completed</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[1%]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((p) => (
                  <TableRow key={p.id} className="cursor-pointer">
                    <TableCell className="font-mono text-xs">
                      <Link
                        href={`/administration/hr/orientation/programs/${p.id}`}
                        className="hover:underline"
                      >
                        {p.programCode}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <Link
                        href={`/administration/hr/orientation/programs/${p.id}`}
                        className="font-medium hover:underline"
                      >
                        {p.title}
                      </Link>
                      <div className="text-muted-foreground mt-0.5 flex gap-2 text-xs">
                        {p.requiresAssessment && <span>Assessed</span>}
                        {p.isCertificateIssued && <span>Certificated</span>}
                        {p.estimatedDurationMinutes ? (
                          <span>{p.estimatedDurationMinutes} min</span>
                        ) : null}
                      </div>
                    </TableCell>
                    <TableCell>{p.categoryName ?? '—'}</TableCell>
                    <TableCell>{label(ORIENTATION_PROGRAM_TYPE_OPTIONS, p.programType)}</TableCell>
                    <TableCell>
                      <Badge variant={PRIORITY_VARIANT[p.priority] ?? 'outline'}>
                        {label(ORIENTATION_PRIORITY_OPTIONS, p.priority)}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">{p.moduleCount}</TableCell>
                    <TableCell className="text-right">{p.enrollmentCount}</TableCell>
                    <TableCell className="text-right">
                      {p.completedCount}
                      {p.enrollmentCount > 0 && (
                        <span className="text-muted-foreground ml-1 text-xs">
                          ({Math.round((p.completedCount / p.enrollmentCount) * 100)}%)
                        </span>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={p.status} />
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() =>
                          setCopying({ id: p.id, title: p.title, programCode: p.programCode })
                        }
                      >
                        <Copy className="mr-1.5 h-3.5 w-3.5" />
                        Copy
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <CopyOrientationProgramDialog source={copying} onClose={() => setCopying(null)} />
    </div>
  );
}
