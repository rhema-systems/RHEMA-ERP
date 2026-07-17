'use client';

import Link from 'next/link';
import {
  BookTemplate,
  FileText,
  GitBranch,
  ShieldCheck,
  Workflow,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

const setupCards = [
  {
    title: 'Integration Contract',
    href: '/administration/document-management/integration-contract',
    icon: GitBranch,
  },
  {
    title: 'Metadata Templates',
    href: '/administration/document-management/metadata-templates',
    icon: BookTemplate,
  },
  {
    title: 'Document Templates',
    href: '/administration/document-management/document-templates',
    icon: FileText,
  },
  {
    title: 'Access & Retention',
    href: '/administration/document-management/access-retention',
    icon: ShieldCheck,
  },
  {
    title: 'Workflow Setup',
    href: '/administration/workflow?q=Document%20Management',
    icon: Workflow,
  },
];

export default function AdministrationDocumentManagementPage() {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Administration setup
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Document Management Setup
            </h1>
          </div>
        </div>
        <Button asChild variant="outline">
          <Link href="/document-management">
            <FileText className="mr-2 h-4 w-4" />
            Open Central DMS
          </Link>
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {setupCards.map((card) => {
          const Icon = card.icon;
          return (
            <Card
              key={card.title}
              className="border-border bg-card text-card-foreground"
            >
              <CardHeader>
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
                  <Icon className="h-5 w-5 text-primary" />
                </div>
                <CardTitle className="text-base">{card.title}</CardTitle>
              </CardHeader>
              <CardContent>
                <Button asChild variant="outline" className="w-full">
                  <Link href={card.href}>Open setup</Link>
                </Button>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
