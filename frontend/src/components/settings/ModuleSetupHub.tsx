'use client';

import Link from 'next/link';
import type { ComponentType } from 'react';
import { ArrowRight } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export interface ModuleSetupLink {
  title: string;
  href: string;
  icon: ComponentType<{ className?: string }>;
  description: string;
}

interface ModuleSetupHubProps {
  moduleName: string;
  title: string;
  description: string;
  links: ModuleSetupLink[];
  workspaceHref: string;
  workspaceLabel: string;
}

export function ModuleSetupHub({
  moduleName,
  title,
  description,
  links,
  workspaceHref,
  workspaceLabel,
}: ModuleSetupHubProps) {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 border-b pb-5 lg:flex-row lg:items-end lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline">Administration / {moduleName}</Badge>
          <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
            {title}
          </h1>
          <p className="max-w-3xl text-sm text-muted-foreground">
            {description}
          </p>
        </div>
        <Button asChild variant="outline">
          <Link href={workspaceHref}>
            {workspaceLabel}
            <ArrowRight className="ml-2 h-4 w-4" />
          </Link>
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {links.map((item) => {
          const Icon = item.icon;
          return (
            <Card
              key={item.href}
              className="border-border bg-card text-card-foreground"
            >
              <CardHeader className="pb-3">
                <div className="mb-2 flex h-9 w-9 items-center justify-center rounded-md border bg-muted">
                  <Icon className="h-4 w-4 text-primary" />
                </div>
                <CardTitle className="text-base">{item.title}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <p className="min-h-10 text-sm text-muted-foreground">
                  {item.description}
                </p>
                <Button
                  asChild
                  variant="outline"
                  className="w-full justify-between"
                >
                  <Link href={item.href}>
                    Open
                    <ArrowRight className="h-4 w-4" />
                  </Link>
                </Button>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
