'use client';

import { TabsList, TabsTrigger } from '@/components/ui/tabs';
import type { ProfileTabGroup } from '@/components/hr/employee/profileTabGroups';

interface ProfileTabNavProps {
  groups: ProfileTabGroup[];
  value: string;
  onChange: (tab: string) => void;
}

/**
 * The profile's navigation (round 3, lane T1): a grouped rail on wide screens, a grouped select
 * below that. Both read the same table and drive the same `Tabs` value, so the page has one source
 * of truth and no duplicated trigger lists.
 *
 * The rail is a real `TabsList` — arrow keys move between tabs, the active one is announced — laid
 * out vertically with a heading per group. Below `lg` a native `<select>` with `<optgroup>` does the
 * job: it is the one control every phone renders well, and it keeps the group names in front of the
 * user while they choose.
 */
export function ProfileTabNav({ groups, value, onChange }: ProfileTabNavProps) {
  return (
    <>
      {/* Wide: the rail. */}
      <TabsList
        aria-label="Profile sections"
        className="hidden h-auto w-56 shrink-0 flex-col items-stretch justify-start gap-4 rounded-md bg-transparent p-0 lg:flex"
        data-testid="profile-tab-rail"
      >
        {groups.map((group) => (
          <div key={group.key} className="space-y-1" data-testid={`profile-tab-group-${group.key}`}>
            <p className="px-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {group.label}
            </p>
            {group.tabs.map((tab) => (
              <TabsTrigger
                key={tab.key}
                value={tab.key}
                className="w-full justify-start rounded-md px-3 py-1.5 text-left data-[state=active]:bg-muted data-[state=active]:shadow-none"
              >
                {tab.label}
              </TabsTrigger>
            ))}
          </div>
        ))}
      </TabsList>

      {/* Narrow: the grouped select. */}
      <div className="lg:hidden">
        <label htmlFor="profile-tab-select" className="sr-only">
          Profile section
        </label>
        <select
          id="profile-tab-select"
          data-testid="profile-tab-select"
          className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
          value={value}
          onChange={(ev) => onChange(ev.target.value)}
        >
          {groups.map((group) => (
            <optgroup key={group.key} label={group.label}>
              {group.tabs.map((tab) => (
                <option key={tab.key} value={tab.key}>
                  {tab.label}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
      </div>
    </>
  );
}
