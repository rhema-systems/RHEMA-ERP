'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { LocationPicker } from '@/components/hr/common/LocationPicker';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { AUDIENCE_TARGETS, type HrAudienceTargetType } from '@/services/hr/announcements.service';

interface AudienceTargetPickerProps {
  targetType: HrAudienceTargetType;
  targetId: string | null;
  /** The name the server resolved for an already-saved target — the employee picker shows it. */
  initialLabel?: string | null;
  onTypeChange: (type: HrAudienceTargetType) => void;
  onTargetChange: (id: string | null) => void;
  /** Onboarding templates are chosen before the hire is an employee, so "one person" is refused there. */
  allowEmployee?: boolean;
  idPrefix?: string;
}

/**
 * The typed target picker round 4 lane I2 asked for: the "Target id" field was a free-text GUID box,
 * so a rule could name a unit that did not exist — or a position's id under "unit" — and silently
 * reach nobody. Each target type gets the picker the rest of HR already uses for that thing: the
 * level→unit and level→location cascades, a list of levels and positions, and the employee search.
 */
export function AudienceTargetPicker({
  targetType,
  targetId,
  initialLabel,
  onTypeChange,
  onTargetChange,
  allowEmployee = true,
  idPrefix = 'audience',
}: AudienceTargetPickerProps) {
  const levels = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
    staleTime: 5 * 60 * 1000,
    enabled: targetType === 'OrganizationLevel',
  });
  const positions = useQuery({
    queryKey: ['hr', 'employee-positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
    staleTime: 5 * 60 * 1000,
    enabled: targetType === 'Position',
  });

  const types = AUDIENCE_TARGETS.filter((t) => allowEmployee || t.value !== 'Employee');
  const spec = AUDIENCE_TARGETS.find((t) => t.value === targetType);

  return (
    <div className="space-y-3">
      <div className="space-y-1.5">
        <Label htmlFor={`${idPrefix}-type`}>Targets</Label>
        <Select
          value={targetType}
          onValueChange={(v) => {
            onTypeChange(v as HrAudienceTargetType);
            onTargetChange(null);
          }}
        >
          <SelectTrigger id={`${idPrefix}-type`}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {types.map((t) => (
              <SelectItem key={t.value} value={t.value}>
                {t.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {spec && <p className="text-muted-foreground text-xs">{spec.hint}</p>}
      </div>

      {targetType === 'OrganizationUnit' && (
        <OrganizationUnitPicker
          value={targetId ?? ''}
          onChange={(id) => onTargetChange(id || null)}
          idPrefix={`${idPrefix}-unit`}
          hint="Includes every unit beneath the one chosen."
        />
      )}

      {targetType === 'Location' && (
        <LocationPicker
          value={targetId ?? ''}
          onChange={(id) => onTargetChange(id || null)}
          idPrefix={`${idPrefix}-location`}
        />
      )}

      {targetType === 'OrganizationLevel' && (
        <div className="space-y-1.5">
          <Label htmlFor={`${idPrefix}-level`}>Organisation level</Label>
          <Select value={targetId ?? ''} onValueChange={(v) => onTargetChange(v || null)}>
            <SelectTrigger id={`${idPrefix}-level`}>
              <SelectValue placeholder={levels.isLoading ? 'Loading…' : 'Choose a level'} />
            </SelectTrigger>
            <SelectContent>
              {(levels.data ?? []).map((l) => (
                <SelectItem key={l.id} value={l.id}>
                  {l.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {targetType === 'Position' && (
        <div className="space-y-1.5">
          <Label htmlFor={`${idPrefix}-position`}>Position</Label>
          <Select value={targetId ?? ''} onValueChange={(v) => onTargetChange(v || null)}>
            <SelectTrigger id={`${idPrefix}-position`}>
              <SelectValue placeholder={positions.isLoading ? 'Loading…' : 'Choose a position'} />
            </SelectTrigger>
            <SelectContent>
              {(positions.data ?? []).map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  {p.title}
                  {p.code ? ` (${p.code})` : ''}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {targetType === 'Employee' && (
        <div className="space-y-1.5">
          <Label>Employee</Label>
          <EmployeePicker
            value={targetId}
            initialLabel={initialLabel}
            onChange={(id) => onTargetChange(id)}
          />
        </div>
      )}
    </div>
  );
}
