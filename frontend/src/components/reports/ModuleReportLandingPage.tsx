import React from 'react';
import Link from 'next/link';
import { ArrowRight } from 'lucide-react';

import type { ModuleReportLandingDefinition } from './module-report-landings';

export function ModuleReportLandingPage({ definition }: { definition: ModuleReportLandingDefinition }) {
  const ModuleIcon = definition.icon;

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <span className="rounded-lg bg-blue-50 p-2 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300">
          <ModuleIcon className="h-5 w-5" />
        </span>
        <h1 className="text-xl font-semibold tracking-tight">{definition.title}</h1>
      </div>

      <div className="grid gap-3 lg:grid-cols-2">
        {definition.groups.map(group => (
          <section key={group.title} className="overflow-hidden rounded-lg border bg-card">
            <h2 className="border-b px-3 py-2 text-sm font-semibold">{group.title}</h2>
            <div className="divide-y">
              {group.items.map(item => {
                const ItemIcon = item.icon;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    className="group flex items-center gap-3 px-3 py-3 text-sm transition-colors hover:bg-muted"
                  >
                    <ItemIcon className="h-4 w-4 shrink-0 text-muted-foreground group-hover:text-foreground" />
                    <span className="min-w-0 flex-1 font-medium">{item.title}</span>
                    <ArrowRight className="h-4 w-4 shrink-0 text-muted-foreground" />
                  </Link>
                );
              })}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
