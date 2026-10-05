'use client';

import { RotateCcw, FilePenLine } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { StaffTravelRequest } from '@/types/hr/travel';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '');

/**
 * Why a request is back with its requester — sent back by an approver, or a change asked for after
 * approval (travel final closure, lane 1: decisions D-6 and D-9). Both stay on the record after the
 * request moves on, as its history; the one that put it where it is now is shown first.
 */
export function TravelLifecycleNotes({ request: r }: { request: StaffTravelRequest }) {
  const notes = [
    r.returnReason && {
      key: 'returned',
      at: r.returnedAt,
      icon: <RotateCcw className="h-4 w-4" />,
      title: 'Sent back for revision',
      body: r.returnReason,
      by: r.returnedByName,
    },
    r.changeReason && {
      key: 'change',
      at: r.changeRequestedAt,
      icon: <FilePenLine className="h-4 w-4" />,
      title: 'Change asked for after approval',
      body: r.changeReason,
      by: r.changeRequestedByName,
    },
  ]
    .filter((n): n is Exclude<typeof n, '' | null | undefined> => !!n)
    .sort((a, b) => Date.parse(b.at ?? '') - Date.parse(a.at ?? ''));

  if (notes.length === 0) return null;

  return (
    <>
      {notes.map((n) => (
        <Card key={n.key}>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base">
              {n.icon}
              {n.title}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm whitespace-pre-wrap">{n.body}</p>
            <p className="mt-2 text-xs text-muted-foreground">
              {[n.by, fmtDateTime(n.at)].filter(Boolean).join(' · ')}
            </p>
          </CardContent>
        </Card>
      ))}
    </>
  );
}
