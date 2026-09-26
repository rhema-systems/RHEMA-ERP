'use client';

import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

interface OrganizationScopeFieldsProps {
  levelId: string;
  unitId: string;
  onLevelChange: (value: string) => void;
  onUnitChange: (value: string) => void;
  /** Copy under the pair, when the caller wants to explain what the scope does. */
  hint?: string;
  disabled?: boolean;
}

/**
 * The level → unit narrowing pair used wherever an HR record is scoped to part of the org.
 *
 * Since demo feedback round 2 (lane B3) this is a thin wrapper over `OrganizationUnitPicker` —
 * the one cascade every unit choice in HR goes through — with the two things a scope FILTER
 * needs that a required picker does not: "any level" is a state (`allowNoLevel`), and a level
 * with no unit is remembered on edit (`initialLevelId`). The contract to callers is unchanged:
 * two ids, two setters, blank meaning "no narrowing"; choosing a unit is the narrower statement
 * and is what the server acts on when both are set.
 */
export function OrganizationScopeFields({
  levelId,
  unitId,
  onLevelChange,
  onUnitChange,
  hint,
  disabled,
}: OrganizationScopeFieldsProps) {
  return (
    <OrganizationUnitPicker
      value={unitId}
      onChange={(id) => onUnitChange(id)}
      onLevelChange={(id) => {
        onLevelChange(id);
        // A unit from the old level would contradict the new one (the picker clears its own
        // value too; this keeps the caller's state in step even when no unit was chosen).
        if (unitId) onUnitChange('');
      }}
      initialLevelId={levelId}
      allowNoLevel="Any level"
      allowNone="Any unit"
      levelLabel="Organization level"
      unitLabel="Organization unit"
      levelPlaceholder="Any level"
      unitPlaceholder="Any unit"
      hint={hint}
      disabled={disabled}
      idPrefix="organization-scope"
    />
  );
}
