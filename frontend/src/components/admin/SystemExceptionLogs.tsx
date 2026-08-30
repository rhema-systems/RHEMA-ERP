'use client';

import React, { useEffect, useMemo, useState } from 'react';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '../ui/card';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import { Input } from '../ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui/select';
import { ConfirmationDialog } from '../ui/confirmation-dialog';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../ui/dialog';
import { Textarea } from '../ui/textarea';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/table';
import { useToast } from '../ui/use-toast';
import {
  RefreshCw,
  Trash2,
  CheckCircle2,
  XCircle,
  Eye,
  Eraser,
  Copy,
} from 'lucide-react';

interface ExceptionLogListItem {
  id: string;
  level: string;
  fingerprint: string;
  occurrenceCount: number;
  logger?: string | null;
  shortMessage: string;
  requestMethod?: string | null;
  requestPath?: string | null;
  username?: string | null;
  traceId?: string | null;
  isResolved: boolean;
  createdAt: string;
  firstOccurredAt: string;
  lastOccurredAt: string;
}

interface ExceptionLogDetail {
  id: string;
  level: string;
  fingerprint: string;
  occurrenceCount: number;
  firstOccurredAt: string;
  lastOccurredAt: string;
  logger?: string | null;
  shortMessage: string;
  fullMessage?: string | null;
  exceptionType?: string | null;
  stackTrace?: string | null;
  traceId?: string | null;
  requestMethod?: string | null;
  requestPath?: string | null;
  queryString?: string | null;
  referrerUrl?: string | null;
  remoteIpAddress?: string | null;
  userAgent?: string | null;
  userId?: string | null;
  username?: string | null;
  isResolved: boolean;
  resolvedAt?: string | null;
  resolvedById?: string | null;
  resolutionNotes?: string | null;
  createdAt: string;
}

export default function SystemExceptionLogs() {
  const { toast } = useToast();

  const [items, setItems] = useState<ExceptionLogListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 100;
  const [level, setLevel] = useState<string>('all');
  const [resolved, setResolved] = useState<string>('all');
  const [search, setSearch] = useState<string>('');

  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState<ExceptionLogDetail | null>(null);

  const [resolveOpen, setResolveOpen] = useState(false);
  const [resolveNotes, setResolveNotes] = useState('');

  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);

  const [clearOpen, setClearOpen] = useState(false);

  const queryString = useMemo(() => {
    const params = new URLSearchParams();
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    if (level !== 'all') params.set('level', level);
    if (resolved !== 'all')
      params.set('resolved', resolved === 'resolved' ? 'true' : 'false');
    if (search.trim()) params.set('search', search.trim());
    return params.toString();
  }, [level, page, resolved, search]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const load = async () => {
    setLoading(true);
    try {
      const response = await fetch(
        `/api/admin/system-exception-logs?${queryString}`,
        {
          headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
        }
      );
      if (!response.ok) {
        toast({
          description: 'Failed to load exception logs',
          variant: 'destructive',
        });
        return;
      }
      const data = await response.json();
      setItems(data.items || []);
      setTotalCount(Number(data.totalCount) || 0);
    } catch {
      toast({
        description: 'Failed to load exception logs',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [queryString]);

  const openDetails = async (id: string) => {
    try {
      const response = await fetch(`/api/admin/system-exception-logs/${id}`, {
        headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
      });
      if (!response.ok) {
        toast({
          description: 'Failed to load details',
          variant: 'destructive',
        });
        return;
      }
      const data = await response.json();
      setSelected(data);
      setDetailOpen(true);
    } catch {
      toast({ description: 'Failed to load details', variant: 'destructive' });
    }
  };

  const requestDelete = (id: string) => {
    setDeleteTargetId(id);
    setDeleteOpen(true);
  };

  const deleteItem = async () => {
    if (!deleteTargetId) return;
    try {
      const response = await fetch(
        `/api/admin/system-exception-logs/${deleteTargetId}`,
        {
          method: 'DELETE',
          headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
        }
      );
      if (!response.ok) {
        toast({
          description: 'Failed to delete log entry',
          variant: 'destructive',
        });
        return;
      }
      toast({ description: 'Log entry deleted' });
      setDeleteTargetId(null);
      await load();
    } catch {
      toast({
        description: 'Failed to delete log entry',
        variant: 'destructive',
      });
    }
  };

  const resolveSelected = async () => {
    if (!selected) return;
    try {
      const response = await fetch(
        `/api/admin/system-exception-logs/${selected.id}/resolve`,
        {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${localStorage.getItem('token')}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ notes: resolveNotes }),
        }
      );
      if (!response.ok) {
        toast({
          description: 'Failed to resolve log entry',
          variant: 'destructive',
        });
        return false;
      }
      toast({ description: 'Marked as resolved' });
      setResolveNotes('');
      setResolveOpen(false);
      setDetailOpen(false);
      await load();
    } catch {
      toast({
        description: 'Failed to resolve log entry',
        variant: 'destructive',
      });
      return false;
    }
  };

  const clearAll = async () => {
    try {
      const response = await fetch(`/api/admin/system-exception-logs/clear`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
      });
      if (!response.ok) {
        toast({ description: 'Failed to clear logs', variant: 'destructive' });
        return false;
      }
      toast({ description: 'Logs cleared' });
      await load();
    } catch {
      toast({ description: 'Failed to clear logs', variant: 'destructive' });
      return false;
    }
  };

  const levelBadgeVariant = (lvl: string) => {
    const l = (lvl || '').toLowerCase();
    if (l === 'critical') return 'destructive';
    if (l === 'error') return 'secondary';
    if (l === 'warning') return 'outline';
    return 'default';
  };

  const compactPath = (method?: string | null, path?: string | null) => {
    const m = method ? method.toUpperCase() : '';
    const p = path || '';
    return [m, p].filter(Boolean).join(' ');
  };

  const friendlyDateTime = (value?: string | null) => {
    if (!value) return '-';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '-';
    return new Intl.DateTimeFormat(undefined, {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  };

  const copyFullMessage = async () => {
    if (!selected?.fullMessage) return;
    try {
      await navigator.clipboard.writeText(selected.fullMessage);
      toast({ description: 'Full message copied' });
    } catch {
      toast({
        description: 'Could not copy the full message',
        variant: 'destructive',
      });
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>System Exception Logs</CardTitle>
            <CardDescription>
              SQL-backed exception log viewer (Smartstore-style)
            </CardDescription>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={load}>
              <RefreshCw className="h-4 w-4 mr-2" />
              Refresh
            </Button>
            <Button variant="destructive" onClick={() => setClearOpen(true)}>
              <Eraser className="h-4 w-4 mr-2" />
              Clear
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
            <Input
              placeholder="Search message / logger / path / user / trace..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter') load();
              }}
            />
            <Select
              value={level}
              onValueChange={(value) => {
                setLevel(value);
                setPage(1);
              }}
            >
              <SelectTrigger>
                <SelectValue placeholder="Level" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Levels</SelectItem>
                <SelectItem value="Warning">Warning</SelectItem>
                <SelectItem value="Error">Error</SelectItem>
                <SelectItem value="Critical">Critical</SelectItem>
              </SelectContent>
            </Select>
            <Select
              value={resolved}
              onValueChange={(value) => {
                setResolved(value);
                setPage(1);
              }}
            >
              <SelectTrigger>
                <SelectValue placeholder="Resolved" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="unresolved">Unresolved</SelectItem>
                <SelectItem value="resolved">Resolved</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex items-center justify-end text-sm text-muted-foreground">
              {loading ? 'Loading...' : `${items.length} of ${totalCount} shown`}
            </div>
          </div>

          <div className="overflow-x-auto border rounded-lg">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Level</TableHead>
                  <TableHead>Short Message</TableHead>
                  <TableHead>Logger</TableHead>
                  <TableHead>User</TableHead>
                  <TableHead>Request</TableHead>
                  <TableHead>Last Seen</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="text-center py-10 text-muted-foreground"
                    >
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : items.length === 0 ? (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="text-center py-10 text-muted-foreground"
                    >
                      No logs found.
                    </TableCell>
                  </TableRow>
                ) : (
                  items.map((x) => (
                    <TableRow
                      key={x.id}
                      className={x.isResolved ? 'opacity-70' : ''}
                    >
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Badge variant={levelBadgeVariant(x.level)}>
                            {x.level}
                          </Badge>
                          {x.isResolved ? (
                            <CheckCircle2 className="h-4 w-4 text-green-600" />
                          ) : (
                            <XCircle className="h-4 w-4 text-muted-foreground" />
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="max-w-[520px]">
                        <div className="font-medium truncate">
                          {x.shortMessage}
                        </div>
                        <div className="text-xs text-muted-foreground truncate">
                          {x.traceId || '-'}
                        </div>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground max-w-[320px] truncate">
                        {x.logger || '-'}
                      </TableCell>
                      <TableCell className="text-sm">
                        {x.username || '-'}
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground max-w-[340px] truncate">
                        {compactPath(x.requestMethod, x.requestPath) || '-'}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {friendlyDateTime(x.lastOccurredAt || x.createdAt)}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => openDetails(x.id)}
                          >
                            <Eye className="h-4 w-4 mr-1" />
                            View
                          </Button>
                          <Button
                            variant="destructive"
                            size="sm"
                            onClick={() => requestDelete(x.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
          <div className="flex flex-col gap-2 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
            <div>
              Page {page} of {totalPages} · {totalCount} total records
            </div>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={loading || page <= 1}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={loading || page >= totalPages}
                onClick={() =>
                  setPage((current) => Math.min(totalPages, current + 1))
                }
              >
                Next
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete Log Entry?"
        description="This is a soft delete and will hide the log from the list."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          await deleteItem();
        }}
      />

      <ConfirmationDialog
        open={clearOpen}
        onOpenChange={setClearOpen}
        title="Clear Exception Logs?"
        description="This will soft-delete all exception logs for the current tenant."
        confirmText="Clear"
        variant="destructive"
        onConfirm={async () => {
          return await clearAll();
        }}
      />

      <Dialog open={detailOpen} onOpenChange={setDetailOpen}>
        <DialogContent className="w-[calc(100vw-2rem)] max-w-4xl max-h-[calc(100vh-2rem)] overflow-hidden">
          <DialogHeader>
            <DialogTitle>Exception Details</DialogTitle>
          </DialogHeader>
          {selected ? (
            <div className="min-w-0 max-h-[calc(100vh-8rem)] space-y-4 overflow-y-auto pr-1">
              <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
                <div>
                  <div className="text-xs text-muted-foreground">Level</div>
                  <Badge variant={levelBadgeVariant(selected.level)}>
                    {selected.level}
                  </Badge>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Created</div>
                  <div className="text-sm">
                    {friendlyDateTime(selected.createdAt)}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">User</div>
                  <div className="text-sm break-all">
                    {selected.username || '-'}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Trace</div>
                  <div className="text-sm font-mono break-all">
                    {selected.traceId || '-'}
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                <div>
                  <div className="text-xs text-muted-foreground">
                    First Seen
                  </div>
                  <div className="text-sm">
                    {friendlyDateTime(selected.firstOccurredAt)}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Last Seen</div>
                  <div className="text-sm">
                    {friendlyDateTime(selected.lastOccurredAt)}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">
                    Fingerprint
                  </div>
                  <div className="text-xs font-mono break-all">
                    {selected.fingerprint}
                  </div>
                </div>
              </div>

              <div>
                <div className="text-xs text-muted-foreground">
                  Short Message
                </div>
                <div className="text-sm font-medium break-words [overflow-wrap:anywhere]">
                  {selected.shortMessage}
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div>
                  <div className="text-xs text-muted-foreground">Logger</div>
                  <div className="text-sm font-mono break-all">
                    {selected.logger || '-'}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Request</div>
                  <div className="text-sm font-mono break-all">
                    {compactPath(
                      selected.requestMethod,
                      selected.requestPath
                    ) || '-'}
                    {selected.queryString ? `?${selected.queryString}` : ''}
                  </div>
                </div>
              </div>

              <div className="min-w-0 max-w-full">
                <div className="mb-1 flex items-center justify-between gap-2">
                  <div className="text-xs text-muted-foreground">
                    Full Message
                  </div>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-7 px-2 text-xs"
                    disabled={!selected.fullMessage}
                    onClick={copyFullMessage}
                  >
                    <Copy className="mr-1 h-3.5 w-3.5" />
                    Copy
                  </Button>
                </div>
                <pre className="max-w-full whitespace-pre-wrap break-words [overflow-wrap:anywhere] rounded-md bg-muted p-3 text-xs max-h-[260px] overflow-auto">
                  {selected.fullMessage || '-'}
                </pre>
              </div>

              <div className="flex items-center justify-between gap-2">
                <div className="text-sm text-muted-foreground">
                  {selected.isResolved ? 'Resolved' : 'Unresolved'}
                  {selected.resolutionNotes
                    ? ` • ${selected.resolutionNotes}`
                    : ''}
                </div>
                <div className="flex gap-2">
                  {!selected.isResolved && (
                    <Button
                      onClick={() => setResolveOpen(true)}
                      variant="default"
                    >
                      <CheckCircle2 className="h-4 w-4 mr-2" />
                      Mark Resolved
                    </Button>
                  )}
                </div>
              </div>
            </div>
          ) : (
            <div className="text-sm text-muted-foreground">
              No log selected.
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={resolveOpen} onOpenChange={setResolveOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Resolve Exception</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="text-sm text-muted-foreground">
              Add optional notes for what was done / how it was resolved.
            </div>
            <Textarea
              value={resolveNotes}
              onChange={(e) => setResolveNotes(e.target.value)}
              placeholder="Resolution notes (optional)"
            />
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setResolveOpen(false)}>
                Cancel
              </Button>
              <Button onClick={resolveSelected}>Resolve</Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
