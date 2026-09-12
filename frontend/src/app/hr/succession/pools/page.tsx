'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, Sparkles, Users2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { talentPoolService, talentPoolTypeService } from '@/services/hr/succession.service';
import { TalentPoolFormDialog } from '@/components/hr/succession/TalentPoolFormDialog';
import type { TalentPoolSummary } from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function TalentPoolsPage() {
  const [creating, setCreating] = useState(false);
  const [typeFilter, setTypeFilter] = useState<string>('all');

  const { data: pools, isLoading } = useQuery({
    queryKey: ['talent-pools'],
    queryFn: () => talentPoolService.getAll(),
  });

  const { data: types } = useQuery({
    queryKey: ['talent-pool-types'],
    queryFn: () => talentPoolTypeService.getAll(),
  });

  const rows: TalentPoolSummary[] = (pools ?? []).filter(
    (p) => typeFilter === 'all' || p.poolTypeId === typeFilter,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Talent pools"
        description="Groups of people being grown for something — not tied to one post, unlike a succession plan."
        backHref="/hr/succession"
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New pool
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4">
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <Sparkles className="h-4 w-4" />
            {/* Members can appear without anyone using this screen — worth saying once, here. */}
            Approving a succession nomination on an appraisal adds the employee to the pool named in
            appraisal settings automatically.
          </p>
          <Select value={typeFilter} onValueChange={setTypeFilter}>
            <SelectTrigger className="w-56">
              <SelectValue placeholder="All pool types" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All pool types</SelectItem>
              {(types ?? []).map((t) => (
                <SelectItem key={t.id} value={t.id}>
                  {t.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Users2}
              title="No talent pools"
              description="A pool groups people worth developing — a leadership pipeline, the technical experts, the emergency bench."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Pool</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead className="text-right">Members</TableHead>
                  <TableHead className="text-right">Target</TableHead>
                  <TableHead>Valid</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>
                      <Link
                        href={`/hr/succession/pools/${p.id}`}
                        className="font-medium hover:underline"
                      >
                        {p.name}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        style={
                          p.poolTypeColor
                            ? { borderColor: p.poolTypeColor, color: p.poolTypeColor }
                            : undefined
                        }
                      >
                        {p.poolTypeName}
                      </Badge>
                    </TableCell>
                    <TableCell>{p.ownerName || '—'}</TableCell>
                    <TableCell className="text-right">{p.currentMemberCount}</TableCell>
                    <TableCell className="text-right text-muted-foreground">
                      {p.targetSize || '—'}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {p.validFrom || p.validTo
                        ? `${fmtDate(p.validFrom)} → ${fmtDate(p.validTo)}`
                        : 'Open-ended'}
                    </TableCell>
                    <TableCell>
                      <Badge variant={p.isActive ? 'default' : 'outline'}>
                        {p.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <TalentPoolFormDialog open={creating} onOpenChange={setCreating} />
    </div>
  );
}
