'use client';

import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, GraduationCap, Loader2, MinusCircle, TrendingDown } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
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
import { jobArchitectureService } from '@/services/hr/job-architecture.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * An employee's own competency profile and gaps.
 *
 * ⚠ **This screen reads `me/profile` and `me/gaps`, not the by-employee routes.** The client `User`
 * object carries roles, tenants and permissions but NO employee link, so a browser has no id to put
 * in `employee/{employeeId}` on the caller's own behalf. The self tier could not arrive as a relaxed
 * permission; it had to arrive as endpoints that take the employee from the token.
 *
 * ⚠ **And this deliberately differs from succession, which has no self tier at all.** A readiness
 * rating is an assessment filed *about* someone. A competency gap is the list of what their own role
 * requires and where they stand against it — the thing they need in order to close it.
 */
export default function MyCompetenciesPage() {
  const { data: profile, isLoading: loadingProfile } = useQuery({
    queryKey: ['competencies', 'me', 'profile'],
    queryFn: () => jobArchitectureService.getMyProfile(),
  });

  const { data: gaps, isLoading: loadingGaps } = useQuery({
    queryKey: ['competencies', 'me', 'gaps'],
    queryFn: () => jobArchitectureService.getMyGaps(),
  });

  if (loadingProfile || loadingGaps) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="My competencies"
        description={
          profile?.positionTitle
            ? `What ${profile.positionTitle} requires, and where you stand against it.`
            : 'What your position requires, and where you stand against it.'
        }
      />

      {gaps && gaps.totalCompetencies > 0 && (
        <div className="grid gap-4 sm:grid-cols-3">
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <CheckCircle2 className="h-4 w-4" />
                At or above
              </div>
              <div className="mt-2 text-2xl font-semibold text-emerald-700">{gaps.metOrExceeded}</div>
              <Progress
                className="mt-3"
                value={gaps.totalCompetencies ? (gaps.metOrExceeded / gaps.totalCompetencies) * 100 : 0}
              />
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <TrendingDown className="h-4 w-4" />
                Below requirement
              </div>
              <div className="mt-2 text-2xl font-semibold text-amber-700">{gaps.hasGap}</div>
              <div className="mt-1 text-xs text-muted-foreground">worth a conversation about training</div>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="pt-6">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <MinusCircle className="h-4 w-4" />
                Not yet assessed
              </div>
              <div className="mt-2 text-2xl font-semibold">{gaps.notAssessed}</div>
              <div className="mt-1 text-xs text-muted-foreground">nobody has looked at these yet</div>
            </CardContent>
          </Card>
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle>What your position requires</CardTitle>
        </CardHeader>
        <CardContent>
          {!gaps || gaps.competencyGaps.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="No competency requirements set"
              description="Your position has no competency requirements recorded yet."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Competency</TableHead>
                  <TableHead className="text-right">Required</TableHead>
                  <TableHead className="text-right">You</TableHead>
                  <TableHead>Standing</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {gaps.competencyGaps.map((g) => (
                  <TableRow key={g.competencyId}>
                    <TableCell>
                      <div className="font-medium">{g.competencyName}</div>
                      <div className="font-mono text-xs text-muted-foreground">{g.competencyCode}</div>
                    </TableCell>
                    <TableCell className="text-right">
                      {g.requiredLevel} / {g.proficiencyScaleMax}
                    </TableCell>
                    <TableCell className="text-right">
                      {/* ⚠ Null means never assessed — not zero. Rendering it as 0 would tell someone
                          they scored the lowest possible mark on something nobody has looked at. */}
                      {g.currentLevel == null ? '—' : `${g.currentLevel} / ${g.proficiencyScaleMax}`}
                    </TableCell>
                    <TableCell>
                      {g.gapStatus == null ? (
                        <Badge variant="outline" className="text-muted-foreground">
                          Not assessed
                        </Badge>
                      ) : g.gapStatus === 'Gap' ? (
                        <Badge className="bg-amber-100 text-amber-800">Below</Badge>
                      ) : g.gapStatus === 'Exceeded' ? (
                        <Badge className="bg-emerald-100 text-emerald-800">Exceeds</Badge>
                      ) : (
                        <Badge className="bg-emerald-100 text-emerald-800">Meets</Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Your assessments</CardTitle>
        </CardHeader>
        <CardContent>
          {!profile || profile.competencies.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="No assessments recorded"
              description="Assessments appear here once your manager or HR records them."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Competency</TableHead>
                  <TableHead className="text-right">Level</TableHead>
                  <TableHead>Assessed</TableHead>
                  <TableHead>By</TableHead>
                  <TableHead>Method</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {profile.competencies.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-medium">{c.competencyName}</TableCell>
                    <TableCell className="text-right">
                      {c.currentProficiencyLevel} / {c.proficiencyScaleMax}
                    </TableCell>
                    <TableCell>{fmtDate(c.assessmentDate)}</TableCell>
                    <TableCell>{c.assessedByName || '—'}</TableCell>
                    <TableCell className="text-muted-foreground">{c.assessmentMethod || '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
