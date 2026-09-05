'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Search, UserPlus, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useDebounce } from '@/hooks/use-debounce';
import { externalAssociateService } from '@/services/hr/interviews.service';

export interface PanelSelection {
  id: string;
  name: string;
}

/**
 * Builds a panel from two different populations: employees, and external associates who have no ERP
 * login at all.
 *
 * The split is not cosmetic — an external panelist confirms their assignment by emailed token and
 * never signs in, so HR records their scores on their behalf. Keeping the two lists visibly
 * separate is what makes that difference legible before the interview rather than after it.
 */
export function PanelMemberPicker({
  employees,
  externals,
  onEmployeesChange,
  onExternalsChange,
  disabled,
}: {
  employees: PanelSelection[];
  externals: PanelSelection[];
  onEmployeesChange: (next: PanelSelection[]) => void;
  onExternalsChange: (next: PanelSelection[]) => void;
  disabled?: boolean;
}) {
  const [externalQuery, setExternalQuery] = useState('');
  const debounced = useDebounce(externalQuery, 400);

  const associateResults = useQuery({
    queryKey: ['hr', 'external-associates', 'search', debounced],
    queryFn: () => externalAssociateService.search(debounced),
    enabled: debounced.trim().length >= 2,
  });

  const addEmployee = (id: string | null, label: string | null) => {
    if (!id || employees.some((e) => e.id === id)) return;
    onEmployeesChange([...employees, { id, name: label ?? 'Panelist' }]);
  };

  return (
    <div className="space-y-5">
      <div className="space-y-2">
        <Label>Internal panel</Label>
        <EmployeePicker
          value={null}
          initialLabel={null}
          disabled={disabled}
          placeholder="Search employees to add…"
          onChange={addEmployee}
        />
        {employees.length > 0 && (
          <div className="flex flex-wrap gap-2 pt-1">
            {employees.map((member) => (
              <Badge key={member.id} variant="secondary" className="gap-1.5 py-1 pl-2.5 pr-1">
                {member.name}
                <button
                  type="button"
                  aria-label={`Remove ${member.name}`}
                  disabled={disabled}
                  onClick={() => onEmployeesChange(employees.filter((e) => e.id !== member.id))}
                  className="rounded-sm hover:bg-muted"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </Badge>
            ))}
          </div>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="externalSearch">External panel</Label>
        <p className="text-sm text-muted-foreground">
          Associates outside the organisation. They confirm by emailed link and never sign in, so HR
          records their scores for them.
        </p>
        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            id="externalSearch"
            className="pl-8"
            placeholder="Search external associates…"
            value={externalQuery}
            disabled={disabled}
            onChange={(e) => setExternalQuery(e.target.value)}
          />
          {associateResults.isFetching && (
            <Loader2 className="absolute right-2.5 top-2.5 h-4 w-4 animate-spin text-muted-foreground" />
          )}
        </div>

        {debounced.trim().length >= 2 && (associateResults.data ?? []).length > 0 && (
          <div className="max-h-44 overflow-y-auto rounded-md border">
            {(associateResults.data ?? []).map((assoc) => {
              const already = externals.some((e) => e.id === assoc.id);
              return (
                <button
                  key={assoc.id}
                  type="button"
                  disabled={already || disabled}
                  onClick={() => {
                    onExternalsChange([...externals, { id: assoc.id, name: assoc.fullName }]);
                    setExternalQuery('');
                  }}
                  className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-muted disabled:opacity-50"
                >
                  <span>
                    <span className="font-medium">{assoc.fullName}</span>
                    {assoc.companyName && (
                      <span className="ml-2 text-muted-foreground">{assoc.companyName}</span>
                    )}
                  </span>
                  {already ? (
                    <span className="text-xs text-muted-foreground">Added</span>
                  ) : (
                    <UserPlus className="h-4 w-4 text-muted-foreground" />
                  )}
                </button>
              );
            })}
          </div>
        )}

        {externals.length > 0 && (
          <div className="flex flex-wrap gap-2 pt-1">
            {externals.map((member) => (
              <Badge key={member.id} variant="outline" className="gap-1.5 py-1 pl-2.5 pr-1">
                {member.name}
                <button
                  type="button"
                  aria-label={`Remove ${member.name}`}
                  disabled={disabled}
                  onClick={() => onExternalsChange(externals.filter((e) => e.id !== member.id))}
                  className="rounded-sm hover:bg-muted"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </Badge>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
