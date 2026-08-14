'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, CalendarClock, Search } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyDocumentsService } from '@/services/hr/safety-documents.service';
import {
  SHE_DOCUMENT_CATEGORY_OPTIONS,
  SHE_DOCUMENT_STATUS_OPTIONS,
  type SheControlledDocumentCategory,
  type SheControlledDocumentStatus,
  type SheControlledDocumentSummary,
} from '@/types/hr/safety-documents';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function StatusBadge({ status }: { status: SheControlledDocumentStatus }) {
  switch (status) {
    case 'Active':
      return <Badge>Active</Badge>;
    case 'UnderReview':
      return <Badge variant="destructive">Under review</Badge>;
    case 'Archived':
      return <Badge variant="secondary">Archived</Badge>;
    default:
      return <Badge variant="outline">Draft</Badge>;
  }
}

/**
 * The SHE controlled document register (SRS §14): filing and retrieval
 * (FR-SHE-170) over policies, procedures, plans and reports, with version
 * control, approval and review cycles (FR-SHE-246) on the detail page.
 */
export default function SheDocumentsPage() {
  const [category, setCategory] = useState<SheControlledDocumentCategory | 'all'>('all');
  const [status, setStatus] = useState<SheControlledDocumentStatus | 'all'>('all');
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');

  const { data: documents = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-documents', 'list', category, status, term],
    queryFn: () =>
      safetyDocumentsService.getAll(
        category === 'all' ? undefined : category,
        status === 'all' ? undefined : status,
        term || undefined,
      ),
  });

  const { data: dueForReview = [] } = useQuery({
    queryKey: ['hr', 'safety-documents', 'due-for-review'],
    queryFn: () => safetyDocumentsService.getAll(undefined, undefined, undefined, 30),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="SHE Document Register"
        description="Controlled SHE documents — policies, procedures, plans and reports — with version history, approval and review cycles. Files are scanned and held in the central document repository."
        backHref="/hr/safety"
        actions={
          <Button asChild>
            <Link href="/hr/safety/documents/new">
              <Plus className="mr-2 h-4 w-4" />
              Register document
            </Link>
          </Button>
        }
      />

      {dueForReview.length > 0 && (
        <Card>
          <CardContent className="flex flex-wrap items-center gap-2 py-3">
            <span className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
              <CalendarClock className="h-4 w-4" />
              Review due within 30 days
            </span>
            {dueForReview.map((d) => (
              <Link key={d.id} href={`/hr/safety/documents/${d.id}`}>
                <Badge variant="outline" className="hover:bg-accent cursor-pointer">
                  {d.documentNumber} · {fmtDate(d.nextReviewDate)}
                </Badge>
              </Link>
            ))}
          </CardContent>
        </Card>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <form
          className="flex items-center gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            setTerm(search.trim());
          }}
        >
          <Input
            placeholder="Search number, title, keywords…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-64"
          />
          <Button type="submit" variant="outline" size="icon" aria-label="Search">
            <Search className="h-4 w-4" />
          </Button>
        </form>
        <Select
          value={category}
          onValueChange={(v) => setCategory(v as SheControlledDocumentCategory | 'all')}
        >
          <SelectTrigger className="w-56">
            <SelectValue placeholder="All categories" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All categories</SelectItem>
            {SHE_DOCUMENT_CATEGORY_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={status} onValueChange={(v) => setStatus(v as SheControlledDocumentStatus | 'all')}>
          <SelectTrigger className="w-44">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_DOCUMENT_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : documents.length === 0 ? (
            <EmptyState
              title="No documents"
              description="Register the first controlled SHE document to start the library."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Version</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Next review</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {documents.map((d: SheControlledDocumentSummary) => (
                  <TableRow key={d.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/safety/documents/${d.id}`} className="hover:underline">
                        <span className="font-mono">{d.documentNumber}</span>
                      </Link>
                    </TableCell>
                    <TableCell className="max-w-xs">
                      <span className="line-clamp-1">{d.title}</span>
                    </TableCell>
                    <TableCell>{d.categoryName}</TableCell>
                    <TableCell>{d.ownerName}</TableCell>
                    <TableCell className="font-mono">{d.currentVersionLabel ?? '—'}</TableCell>
                    <TableCell className="tabular-nums">{fmtDate(d.effectiveDate)}</TableCell>
                    <TableCell className="tabular-nums">{fmtDate(d.nextReviewDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={d.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
