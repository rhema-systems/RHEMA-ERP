'use client';

import Link from 'next/link';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';

interface PageHeaderProps {
  title: string;
  description?: string;
  /** Optional back-link target (renders an arrow button to the left of the title). */
  backHref?: string;
  /** Right-aligned actions (e.g. a "New" button). */
  actions?: React.ReactNode;
}

/**
 * Standard page header for HR screens: optional back link, title + description,
 * and a right-aligned action slot. Matches the finance/AR header layout.
 */
export function PageHeader({ title, description, backHref, actions }: PageHeaderProps) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div className="flex items-start gap-3">
        {backHref && (
          <Button variant="ghost" size="icon" asChild className="mt-1">
            <Link href={backHref} aria-label="Back">
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
        )}
        <div>
          <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
          {description && <p className="text-muted-foreground mt-2">{description}</p>}
        </div>
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}
