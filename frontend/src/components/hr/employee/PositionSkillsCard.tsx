'use client';

/**
 * What the post asks of this person, against what they hold — the checklist the PDF asked for
 * (demo feedback round 2, lane C3b; register row S-4; plan § 6.4.4).
 *
 * The skills tab used to list every active skill in the catalogue and nothing else: it could tell
 * you what the person had recorded, never what their job needed. This leads the tab with the
 * position's requirement, marks each line held / below the level asked for / not held, and puts an
 * "Add" beside the gaps that opens the tab's own dialog pre-filled with the skill and the level.
 *
 * ⚠ The requirement is the position's EFFECTIVE one — its attached skill sets unioned with its
 * individual rows — so a skill required through a set is shown exactly like one listed
 * individually, and each line says which set asked for it.
 *
 * Renders nothing when the post asks for nothing: a strip that says "nothing to say" on every
 * profile is noise (the same rule as the certification compliance card).
 */

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { employeeService } from '@/services/hr/employee.service';
import type { EmployeeSkillRequirementLine } from '@/types/hr/named-sets';

const STATUS_STYLES: Record<EmployeeSkillRequirementLine['status'], string> = {
  Held: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  BelowLevel: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  Missing: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

const STATUS_LABEL: Record<EmployeeSkillRequirementLine['status'], string> = {
  Held: 'Held',
  BelowLevel: 'Below level',
  Missing: 'Not held',
};

/** "Site work core, Individual" — where the requirement came from, without repeating "Set:". */
function sourceText(line: EmployeeSkillRequirementLine): string {
  const names = line.sources.map((s) => (s.kind === 'Set' ? (s.setName ?? s.setCode ?? 'a set') : 'listed on the post'));
  return Array.from(new Set(names)).join(', ');
}

export function PositionSkillsCard({
  employeeId,
  onAddSkill,
}: {
  employeeId: string;
  /** Opens the skills tab's add dialog pre-filled. Omit to render the list without the affordance. */
  onAddSkill?: (line: EmployeeSkillRequirementLine) => void;
}) {
  const { data } = useQuery({
    queryKey: ['hr', 'employees', employeeId, 'skill-requirements'],
    queryFn: () => employeeService.getSkillRequirements(employeeId),
  });

  if (!data || data.lines.length === 0) return null;

  const ok = data.isCompliant;
  const gaps = data.requiredCount - data.requiredAtLevelCount;

  return (
    <Card className={ok ? undefined : 'border-amber-400'}>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          {ok ? (
            <CheckCircle2 className="h-4 w-4 text-emerald-600" />
          ) : (
            <AlertTriangle className="h-4 w-4 text-amber-600" />
          )}
          What this post asks for
        </CardTitle>
        <CardDescription>
          {data.positionTitle ? `${data.positionTitle} — ` : ''}
          {ok
            ? `every required skill is held at the level asked for (${data.requiredAtLevelCount} of ${data.requiredCount}).`
            : `${gaps} of ${data.requiredCount} required skill${data.requiredCount === 1 ? '' : 's'} still short.`}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-1.5">
        {data.lines.map((line) => (
          <div key={line.skillId} className="flex flex-wrap items-center gap-2 text-sm">
            <span className={`rounded px-1.5 py-0.5 text-xs ${STATUS_STYLES[line.status]}`}>
              {STATUS_LABEL[line.status]}
            </span>

            <span className="font-medium">{line.skillName}</span>

            <span className="text-xs text-muted-foreground">
              needs {line.requiredLevel}
              {line.held && line.heldLevel ? ` · holds ${line.heldLevel}` : ''}
              {line.isRequired ? '' : ' · preferred'}
            </span>

            {/* Which set asked for it — the whole point of naming sources on the effective read. */}
            <span className="text-xs text-muted-foreground">· {sourceText(line)}</span>

            {line.held && line.isVerified && (
              <Badge variant="outline" className="text-xs">
                Verified
              </Badge>
            )}

            {/* Recording is not gating: a skill needing a credential is allowed and flagged. */}
            {line.held && line.requiresCertification && !line.credentialSatisfied && (
              <Badge variant="secondary" className="text-xs">
                Credential missing
              </Badge>
            )}

            {!line.held && onAddSkill && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-6 px-2 text-xs"
                onClick={() => onAddSkill(line)}
              >
                <Plus className="mr-1 h-3 w-3" /> Record
              </Button>
            )}
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
