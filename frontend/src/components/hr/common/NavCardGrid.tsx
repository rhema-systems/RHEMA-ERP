'use client';

import Link from 'next/link';
import { ArrowRight, type LucideIcon } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export interface NavCardItem {
  title: string;
  description: string;
  href: string;
  icon: LucideIcon;
}

/**
 * Card grid used by the HR index pages to link on to their children. Nav parents in the
 * sidebar are clickable, so every group needs a landing page rather than a 404.
 */
export function NavCardGrid({ items }: { items: NavCardItem[] }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {items.map(({ title, description, href, icon: Icon }) => (
        <Link key={href} href={href}>
          <Card className="h-full transition-colors hover:bg-muted/50">
            <CardHeader className="pb-2">
              <div className="flex items-center gap-2">
                <Icon className="h-5 w-5 text-muted-foreground" />
                <CardTitle className="text-base">{title}</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="flex items-end justify-between gap-2">
              <p className="text-sm text-muted-foreground">{description}</p>
              <ArrowRight className="h-4 w-4 shrink-0 text-muted-foreground" />
            </CardContent>
          </Card>
        </Link>
      ))}
    </div>
  );
}
