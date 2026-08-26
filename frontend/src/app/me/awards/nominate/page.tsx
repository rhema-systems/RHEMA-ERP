'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { AlertTriangle, Info, Loader2, Search, Send, UserPlus } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { awardsService } from '@/services/hr/awards.service';
import type { AwardCycleSummary } from '@/types/hr/awards';

/**
 * Put a colleague forward for an award (AWD-02, AWD-03, AWD-09).
 *
 * ⚠ **Behind no HR permission.** Nominating is an act every employee performs; entitlement is read
 * off the record rather than granted. See `AwardsMeController`.
 *
 * ⚠ **The candidate picker searches server-side.** The qualified set can be most of the workforce —
 * measured at 5,579 employees — so this must not fetch a list and filter it in the browser. The
 * endpoint is paged and searchable for exactly this screen.
 *
 * ⚠ **Only the eligible are offered, and never the reasons anyone else failed.** The awards desk
 * sees those; an employee choosing somebody to nominate has no business reading why a colleague did
 * not qualify.
 *
 * Area 25 slice 9: re-homed from /hr/awards/me/nominate (D3). The award-type read now rides the
 * NEW self arm `awards/me/types/{id}` — this page used to call the desk `GET Awards/types/{id}`,
 * which needs HR.Awards.Read, so a plain employee 403'd the moment they picked a cycle (measured
 * live in probe-slice9).
 */
export default function NominatePage() {
  const router = useRouter();

  const [cycleId, setCycleId] = useState('');
  const [nomineeId, setNomineeId] = useState('');
  const [search, setSearch] = useState('');
  const [debounced, setDebounced] = useState('');
  const [teamName, setTeamName] = useState('');
  const [justification, setJustification] = useState('');
  const [submitNow, setSubmitNow] = useState(true);

  // Search server-side, on a pause rather than on every keystroke.
  useEffect(() => {
    const t = setTimeout(() => setDebounced(search.trim()), 300);
    return () => clearTimeout(t);
  }, [search]);

  const { data: cycles, isLoading: loadingCycles } = useQuery({
    queryKey: ['me', 'awards', 'open-cycles'],
    queryFn: () => awardsService.getMyOpenCycles(),
  });

  const openCycles: AwardCycleSummary[] = cycles ?? [];
  const cycle = openCycles.find((c) => c.id === cycleId);

  // The award type behind the chosen cycle decides whether this is a team nomination and whether
  // the employee may put their own name forward. The SELF arm — the desk read is HR-gated.
  const { data: awardType } = useQuery({
    queryKey: ['me', 'awards', 'type', cycle?.awardTypeId],
    queryFn: () => awardsService.getMyType(cycle?.awardTypeId as string),
    enabled: Boolean(cycle?.awardTypeId),
  });

  const { data: candidates, isFetching: searching } = useQuery({
    queryKey: ['me', 'awards', 'candidates', cycle?.awardTypeId, debounced],
    queryFn: () =>
      awardsService.getCandidates(cycle?.awardTypeId as string, {
        search: debounced || undefined,
        pageSize: 20,
      }),
    enabled: Boolean(cycle?.awardTypeId) && !awardType?.isTeamAward,
  });

  const create = useMutation({
    mutationFn: async () => {
      if (!cycle) throw new Error('Choose an award first.');
      const nomination = await awardsService.createNomination({
        awardTypeId: cycle.awardTypeId,
        awardCycleId: cycle.id,
        nomineeId: awardType?.isTeamAward ? null : nomineeId,
        teamName: awardType?.isTeamAward ? teamName.trim() : null,
        year: cycle.year,
        justification: justification.trim(),
      });
      // Submitting is a second act, and deliberately so: a draft can be edited, a submitted
      // nomination cannot. The checkbox makes that choice explicit rather than implied.
      if (submitNow) await awardsService.submitNomination(nomination.id);
      return nomination;
    },
    onSuccess: (nomination) => {
      toast.success(
        submitNow
          ? `Nomination ${nomination.nominationNumber} submitted.`
          : `Nomination ${nomination.nominationNumber} saved as a draft.`,
      );
      router.push(`/me/awards/nominations/${nomination.id}`);
    },
    onError: (e: any) =>
      toast.error(e?.body?.detail || e?.body?.message || e?.message || 'The nomination was refused.'),
  });

  const rows = candidates?.items ?? [];
  const chosen = rows.find((c) => c.employeeId === nomineeId);

  const ready = useMemo(() => {
    if (!cycle || justification.trim().length === 0) return false;
    return awardType?.isTeamAward ? teamName.trim().length > 0 : nomineeId.length > 0;
  }, [cycle, justification, awardType, teamName, nomineeId]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Nominate a colleague"
        description="Put someone forward for an award that is open for nominations."
        backHref="/me/awards"
      />

      {loadingCycles ? (
        <div className="flex items-center justify-center p-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : openCycles.length === 0 ? (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            Nothing is open for nomination at the moment. Awards appear here while their nomination
            window is open.
          </AlertDescription>
        </Alert>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Which award?</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label>Award</Label>
                <Select
                  value={cycleId}
                  onValueChange={(v) => {
                    setCycleId(v);
                    setNomineeId('');
                    setSearch('');
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Choose an award that is open for nomination" />
                  </SelectTrigger>
                  <SelectContent>
                    {openCycles.map((c) => (
                      <SelectItem key={c.id} value={c.id}>
                        {c.awardTypeName} — {c.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {awardType && !awardType.allowSelfNomination && (
                <p className="text-xs text-muted-foreground">
                  This award does not accept self-nomination. Your own name will not appear below.
                </p>
              )}
            </CardContent>
          </Card>

          {cycle && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">
                  {awardType?.isTeamAward ? 'Which team?' : 'Who are you nominating?'}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {awardType?.isTeamAward ? (
                  <div className="space-y-2">
                    <Label htmlFor="teamName">Team name</Label>
                    <Input
                      id="teamName"
                      value={teamName}
                      onChange={(e) => setTeamName(e.target.value)}
                      placeholder="The team being nominated"
                    />
                    <p className="text-xs text-muted-foreground">
                      This is a team award, so it is nominated by name. Individual members are added
                      to the nomination afterwards by the awards desk.
                    </p>
                  </div>
                ) : (
                  <>
                    <div className="relative">
                      <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                      <Input
                        className="pl-9"
                        placeholder="Search by name or employee number"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                      />
                    </div>

                    {searching ? (
                      <div className="flex items-center justify-center p-6">
                        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                      </div>
                    ) : rows.length === 0 ? (
                      <p className="p-4 text-sm text-muted-foreground">
                        {debounced
                          ? 'Nobody eligible matches that search.'
                          : 'Nobody qualifies for this award yet.'}
                      </p>
                    ) : (
                      <div className="max-h-72 space-y-1 overflow-y-auto rounded-md border p-1">
                        {rows.map((c) => (
                          <button
                            type="button"
                            key={c.employeeId}
                            onClick={() => setNomineeId(c.employeeId)}
                            className={`flex w-full items-center justify-between rounded px-3 py-2 text-left text-sm hover:bg-muted ${
                              c.employeeId === nomineeId ? 'bg-muted font-medium' : ''
                            }`}
                          >
                            <span>{c.employeeName}</span>
                            <span className="text-xs text-muted-foreground">{c.employeeNumber}</span>
                          </button>
                        ))}
                      </div>
                    )}

                    {/* The total is the eligible set, not the page — so a reader can tell that
                        searching narrowed a large list rather than that few people qualify. */}
                    {candidates && (
                      <p className="text-xs text-muted-foreground">
                        Showing {rows.length} of {candidates.totalCount} eligible colleagues
                        {debounced ? ' matching that search' : ''}.
                      </p>
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          )}

          {cycle && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Why?</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="justification">Justification</Label>
                  <Textarea
                    id="justification"
                    rows={5}
                    value={justification}
                    onChange={(e) => setJustification(e.target.value)}
                    placeholder="What has this colleague done that deserves the award?"
                  />
                  <p className="text-xs text-muted-foreground">
                    This is what the committee or the voters read. It is required.
                  </p>
                </div>

                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={submitNow}
                    onChange={(e) => setSubmitNow(e.target.checked)}
                  />
                  Submit it now
                </label>
                {!submitNow && (
                  <Alert>
                    <AlertTriangle className="h-4 w-4" />
                    <AlertDescription>
                      A draft is not seen by anyone until you submit it. You can edit it until then;
                      once submitted it cannot be changed or withdrawn.
                    </AlertDescription>
                  </Alert>
                )}

                <div className="flex justify-end gap-2">
                  <Button variant="outline" onClick={() => router.push('/me/awards')}>
                    Cancel
                  </Button>
                  <Button disabled={!ready || create.isPending} onClick={() => create.mutate()}>
                    {create.isPending ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : submitNow ? (
                      <Send className="mr-2 h-4 w-4" />
                    ) : (
                      <UserPlus className="mr-2 h-4 w-4" />
                    )}
                    {submitNow ? 'Nominate' : 'Save draft'}
                  </Button>
                </div>

                {chosen && !awardType?.isTeamAward && (
                  <p className="text-right text-xs text-muted-foreground">
                    Nominating {chosen.employeeName} for {cycle.awardTypeName}.
                  </p>
                )}
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
