'use client';

import { useQuery } from '@tanstack/react-query';
import { Sparkles } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { performanceLinkService } from '@/services/hr/performance-links.service';
import type { DevelopmentSkillSuggestion } from '@/types/hr/performance-links';

interface DevelopmentSkillSuggestionsProps {
  /** Omit to ask for the signed-in employee's own suggestions. */
  employeeId?: string | null;
  cycleId?: string | null;
  /** Wired up on screens that can turn a suggestion into an objective. */
  onUse?: (suggestion: DevelopmentSkillSuggestion) => void;
}

/**
 * Competencies this employee's own goals say they need to develop, in one cycle.
 *
 * These are not generic recommendations: each one is a skill somebody ticked as "development
 * needed" on a real goal, and the goals that asked for it are listed underneath. That provenance is
 * the point — it is the difference between a development plan assembled from evidence and one
 * assembled from a dropdown.
 *
 * Empty is the normal state until goals start carrying required skills, so the empty copy says
 * where they come from rather than implying something is broken.
 */
export function DevelopmentSkillSuggestions({
  employeeId,
  cycleId,
  onUse,
}: DevelopmentSkillSuggestionsProps) {
  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'skill-suggestions', employeeId ?? 'me', cycleId],
    queryFn: () =>
      employeeId
        ? performanceLinkService.getSkillSuggestions(employeeId, cycleId ?? '')
        : performanceLinkService.getMySkillSuggestions(cycleId ?? ''),
    enabled: !!cycleId,
  });

  if (!cycleId) return null;
  if (isLoading) return <Skeleton className="h-32 w-full" />;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Sparkles className="h-4 w-4" />
          Suggested from this year&apos;s goals
        </CardTitle>
      </CardHeader>
      <CardContent>
        {(data ?? []).length === 0 ? (
          <EmptyState
            icon={Sparkles}
            title="No suggestions yet"
            description="Suggestions appear once a goal records a competency as needing development."
          />
        ) : (
          <div className="space-y-3">
            {(data ?? []).map((s) => (
              <div
                key={s.competencyId}
                className="flex flex-wrap items-start justify-between gap-3 rounded-lg border p-3"
              >
                <div className="space-y-1">
                  <p className="font-medium">{s.competencyName ?? 'Unnamed competency'}</p>
                  <div className="flex flex-wrap gap-1">
                    {/* The provenance: which goals asked for this skill. */}
                    {s.fromGoals.map((g) => (
                      <Badge key={g} variant="outline" className="font-normal">
                        {g}
                      </Badge>
                    ))}
                  </div>
                </div>
                {onUse && (
                  <Button variant="outline" size="sm" onClick={() => onUse(s)}>
                    Add as objective
                  </Button>
                )}
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
