'use client';

/**
 * Area 25 slice 1 — the HR unlinked-users queue.
 *
 * Every user account with no employee link, each carrying the exact-match candidates the
 * LDAP auto-link would have used (same rules, same backend service). One-click accepts a
 * suggestion; "Link all exact matches" bulk-accepts every row whose rule matched exactly
 * one employee; ambiguous candidates are shown flagged so a human decides — the auto-link
 * never does. Manual search covers the rows with no suggestion at all.
 */

import { useCallback, useEffect, useMemo, useState } from 'react';
import NextLink from 'next/link';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Link2, Search, UserCheck, AlertTriangle, RefreshCw } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import { apiService } from '@/services/api.service';
import { employeeService } from '@/services/hr/employee.service';
import type { Employee } from '@/types/hr/employee';

interface LinkSuggestion {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  employeeEmail?: string | null;
  matchedBy: 'Email' | 'EmployeeNumber';
  isExact: boolean;
}

interface UnlinkedUserRow {
  userId: string;
  userName: string;
  fullName: string;
  email: string;
  authenticationProvider: string;
  isActive: boolean;
  lastLoginDate?: string | null;
  suggestions: LinkSuggestion[];
}

interface BulkLinkResponse {
  linked: number;
  failed: number;
  results: { userId: string; employeeId: string; success: boolean; message: string }[];
}

export default function UnlinkedUsersQueuePage() {
  const { toast } = useToast();
  const [rows, setRows] = useState<UnlinkedUserRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [filter, setFilter] = useState('');

  // manual-search dialog state
  const [manualFor, setManualFor] = useState<UnlinkedUserRow | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [searchResults, setSearchResults] = useState<Employee[]>([]);
  const [searching, setSearching] = useState(false);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setRows(await apiService.get<UnlinkedUserRow[]>('/useremployeelink/unlinked-users'));
    } catch {
      toast({ title: 'Error', description: 'Could not load the unlinked-users queue', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    load();
  }, [load]);

  const visibleRows = useMemo(() => {
    const needle = filter.trim().toLowerCase();
    if (!needle) return rows;
    return rows.filter(
      (r) =>
        r.userName.toLowerCase().includes(needle) ||
        r.fullName.toLowerCase().includes(needle) ||
        r.email.toLowerCase().includes(needle),
    );
  }, [rows, filter]);

  // The bulk set: rows whose rules produced exactly one exact candidate — what auto-link would do.
  const exactRows = useMemo(
    () =>
      rows
        .map((r) => ({ row: r, exact: r.suggestions.filter((s) => s.isExact) }))
        .filter((x) => x.exact.length === 1),
    [rows],
  );

  const linkOne = async (row: UnlinkedUserRow, employeeId: string, employeeName: string) => {
    try {
      setBusy(true);
      await apiService.post('/useremployeelink/link-user-to-employee', { userId: row.userId, employeeId });
      toast({ title: 'Linked', description: `${row.userName} → ${employeeName}`, variant: 'success' });
      setManualFor(null);
      await load();
    } catch {
      toast({ title: 'Error', description: `Could not link ${row.userName}`, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const linkAllExact = async () => {
    if (exactRows.length === 0) return;
    try {
      setBusy(true);
      const result = await apiService.post<BulkLinkResponse>('/useremployeelink/bulk-link', {
        links: exactRows.map((x) => ({ userId: x.row.userId, employeeId: x.exact[0].employeeId })),
      });
      toast({
        title: 'Bulk link finished',
        description: `${result.linked} linked, ${result.failed} failed`,
        variant: result.failed === 0 ? 'success' : 'destructive',
      });
      await load();
    } catch {
      toast({ title: 'Error', description: 'Bulk link failed', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  const runSearch = async () => {
    const term = searchTerm.trim();
    if (!term) return;
    try {
      setSearching(true);
      const page = await employeeService.searchPaged({ searchTerm: term }, 1, 20);
      setSearchResults(page.items ?? []);
    } catch {
      toast({ title: 'Error', description: 'Employee search failed', variant: 'destructive' });
    } finally {
      setSearching(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Unlinked Users</h1>
          <p className="text-muted-foreground">
            User accounts with no employee record — the self-service portal is dead for them until linked
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={load} disabled={loading || busy}>
            <RefreshCw className="mr-1 h-4 w-4" /> Refresh
          </Button>
          <Button size="sm" onClick={linkAllExact} disabled={busy || exactRows.length === 0}>
            <UserCheck className="mr-1 h-4 w-4" />
            Link all exact matches{exactRows.length > 0 ? ` (${exactRows.length})` : ''}
          </Button>
          <Button variant="outline" size="sm" asChild>
            <NextLink href="/administration/user-employee-links">All links</NextLink>
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle className="flex items-center gap-2 text-lg">
              <Link2 className="h-5 w-5" />
              Queue{!loading && ` — ${rows.length} user${rows.length === 1 ? '' : 's'}`}
            </CardTitle>
            <div className="w-64">
              <Input
                placeholder="Filter by name, username, email…"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading…</div>
          ) : visibleRows.length === 0 ? (
            <div className="py-10 text-center text-muted-foreground">
              {rows.length === 0
                ? 'Every user account is linked to an employee record. Nothing to do here.'
                : 'No rows match the filter.'}
            </div>
          ) : (
            <div className="space-y-3">
              {visibleRows.map((row) => (
                <div key={row.userId} className="rounded-lg border p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <h4 className="font-medium">{row.fullName || row.userName}</h4>
                        <Badge variant="outline">@{row.userName}</Badge>
                        <Badge variant={row.authenticationProvider === 'LDAP' ? 'secondary' : 'outline'}>
                          {row.authenticationProvider}
                        </Badge>
                        {!row.isActive && <Badge variant="destructive">Inactive</Badge>}
                      </div>
                      <p className="text-sm text-muted-foreground">{row.email}</p>
                      {row.lastLoginDate && (
                        <p className="text-xs text-muted-foreground">
                          Last login {new Date(row.lastLoginDate).toLocaleString()}
                        </p>
                      )}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={busy}
                      onClick={() => {
                        setManualFor(row);
                        setSearchTerm('');
                        setSearchResults([]);
                      }}
                    >
                      <Search className="mr-1 h-4 w-4" /> Find employee…
                    </Button>
                  </div>

                  {row.suggestions.length > 0 && (
                    <div className="mt-3 space-y-2">
                      {row.suggestions.map((s) => (
                        <div
                          key={`${row.userId}-${s.employeeId}-${s.matchedBy}`}
                          className="flex flex-wrap items-center justify-between gap-2 rounded bg-muted p-2"
                        >
                          <div className="flex items-center gap-2 text-sm">
                            {!s.isExact && <AlertTriangle className="h-4 w-4 text-amber-500" />}
                            <span className="font-medium">{s.employeeName}</span>
                            <span className="text-muted-foreground">({s.employeeNumber})</span>
                            <Badge variant="outline">
                              matched by {s.matchedBy === 'Email' ? 'email' : 'employee number'}
                            </Badge>
                            {!s.isExact && (
                              <span className="text-xs text-amber-600">
                                ambiguous — this rule matched more than one employee
                              </span>
                            )}
                          </div>
                          <Button
                            size="sm"
                            variant={s.isExact ? 'default' : 'outline'}
                            disabled={busy}
                            onClick={() => linkOne(row, s.employeeId, s.employeeName)}
                          >
                            <Link2 className="mr-1 h-4 w-4" /> Link
                          </Button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={manualFor !== null} onOpenChange={(open) => !open && setManualFor(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Link {manualFor?.userName} to an employee</DialogTitle>
            <DialogDescription>
              Search by name or employee number. Linking grants this account the employee&apos;s
              self-service surface — check you have the right person.
            </DialogDescription>
          </DialogHeader>
          <div className="flex gap-2">
            <Input
              placeholder="Name or employee number…"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && runSearch()}
            />
            <Button onClick={runSearch} disabled={searching || !searchTerm.trim()}>
              {searching ? 'Searching…' : 'Search'}
            </Button>
          </div>
          <div className="max-h-72 space-y-2 overflow-y-auto">
            {searchResults.map((e) => (
              <div
                key={e.id}
                className="flex items-center justify-between rounded border p-2 text-sm"
              >
                <div>
                  <span className="font-medium">
                    {e.fullName || `${e.firstName} ${e.lastName}`}
                  </span>{' '}
                  <span className="text-muted-foreground">({e.employeeNumber})</span>
                </div>
                <Button
                  size="sm"
                  disabled={busy}
                  onClick={() =>
                    manualFor &&
                    linkOne(manualFor, e.id, e.fullName || `${e.firstName} ${e.lastName}`)
                  }
                >
                  Link
                </Button>
              </div>
            ))}
            {!searching && searchResults.length === 0 && searchTerm.trim() && (
              <p className="py-2 text-center text-sm text-muted-foreground">
                No results yet — run the search.
              </p>
            )}
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
