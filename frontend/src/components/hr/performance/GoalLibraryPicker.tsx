'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Library, Loader2, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useDebounce } from '@/hooks/use-debounce';
import { goalLibraryService } from '@/services/hr/goals.service';
import type { GoalLibrarySelectorItem } from '@/types/hr/goals';

/**
 * Picks a goal library template to seed a new employee goal.
 *
 * Choosing one copies its title, description and success criteria into the form and records
 * the template id on the goal, which is what the library's usage figures count. The copy is
 * a snapshot — editing the template later does not reach goals already created from it, and
 * the goal stays editable afterwards.
 *
 * Scope filters are passed through so the list can be narrowed to the employee's own level,
 * unit or position; templates with no scope ("Global") always appear.
 */
export function GoalLibraryPicker({
  open,
  onOpenChange,
  onSelect,
  organizationLevelId,
  organizationUnitId,
  positionId,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSelect: (item: GoalLibrarySelectorItem) => void;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  positionId?: string | null;
}) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebounce(search, 350);

  const { data, isLoading } = useQuery({
    queryKey: [
      'hr',
      'goal-library',
      'selector',
      debouncedSearch,
      organizationLevelId,
      organizationUnitId,
      positionId,
      page,
    ],
    queryFn: () =>
      goalLibraryService.getSelector({
        search: debouncedSearch || undefined,
        activeOnly: true,
        organizationLevelId,
        organizationUnitId,
        positionId,
        page,
        pageSize: 8,
      }),
    enabled: open,
  });

  const items = data?.items ?? [];

  const choose = (item: GoalLibrarySelectorItem) => {
    onSelect(item);
    onOpenChange(false);
    setSearch('');
    setPage(1);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[680px]">
        <DialogHeader>
          <DialogTitle>Choose from the goal library</DialogTitle>
          <DialogDescription>
            The template’s wording is copied into the goal and stays editable. Targets, weight
            and dates are still yours to set.
          </DialogDescription>
        </DialogHeader>

        <div className="relative">
          <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder="Search titles and descriptions…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div className="max-h-[46vh] space-y-2 overflow-y-auto">
          {isLoading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={Library}
              title="No templates match"
              description={
                search
                  ? 'Try a different search, or write the goal from scratch.'
                  : 'The goal library is empty. Add templates under Administration → HR → Performance.'
              }
            />
          ) : (
            items.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => choose(item)}
                className="flex w-full flex-col items-start gap-1 rounded-md border p-3 text-left transition-colors hover:bg-accent"
              >
                <div className="flex w-full items-start justify-between gap-3">
                  <span className="font-medium">{item.title}</span>
                  <Badge variant="outline" className="shrink-0">
                    {item.scopeSummary}
                  </Badge>
                </div>
                {item.description && (
                  <span className="line-clamp-2 text-sm text-muted-foreground">
                    {item.description}
                  </span>
                )}
              </button>
            ))
          )}
        </div>

        <DialogFooter className="sm:justify-between">
          <span className="text-sm text-muted-foreground">
            {data ? `${data.totalCount} template${data.totalCount === 1 ? '' : 's'}` : ''}
          </span>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={!data?.hasPrevious}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={!data?.hasNext}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
