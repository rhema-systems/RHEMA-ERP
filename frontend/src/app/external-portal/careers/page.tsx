'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Briefcase, FileSignature, Loader2, MailWarning, Printer, UserRound } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatMoney } from '@/lib/hr/attendance-format';
import { candidateService } from '@/services/hr/careers.service';
import type { CandidateApplicationSummary, CandidateOffer, OfferResponseChoice } from '@/types/hr/careers';

/**
 * The candidate's home: every application they have made, its live status, and — when one
 * arrives — the offer, viewable and answerable in place.
 */
export default function CandidateHomePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [withdrawing, setWithdrawing] = useState<CandidateApplicationSummary | null>(null);
  const [withdrawReason, setWithdrawReason] = useState('');
  const [offerFor, setOfferFor] = useState<CandidateApplicationSummary | null>(null);
  const [offerResponse, setOfferResponse] = useState<OfferResponseChoice>('Accepted');
  const [offerNotes, setOfferNotes] = useState('');

  const dashboard = useQuery({
    queryKey: ['candidate', 'dashboard'],
    queryFn: () => candidateService.getDashboard(),
  });

  const offer = useQuery({
    queryKey: ['candidate', 'offer', offerFor?.applicationId],
    queryFn: () => candidateService.getOffer(offerFor?.applicationId ?? ''),
    enabled: !!offerFor,
    retry: false,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['candidate'] });

  const withdraw = useMutation({
    mutationFn: () => candidateService.withdraw(withdrawing?.applicationId ?? '', withdrawReason.trim() || null),
    onSuccess: async () => {
      await refresh();
      setWithdrawing(null);
      setWithdrawReason('');
      toast({ title: 'Application withdrawn' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not withdraw', description: e?.message, variant: 'destructive' }),
  });

  const respond = useMutation({
    mutationFn: () =>
      candidateService.respondToOffer(
        offerFor?.applicationId ?? '',
        offerResponse,
        offerNotes.trim() || null,
        offerResponse === 'Declined' ? offerNotes.trim() || null : null,
      ),
    onSuccess: async () => {
      await refresh();
      await queryClient.invalidateQueries({ queryKey: ['candidate', 'offer'] });
      toast({ title: 'Your response has been recorded' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not record your response', description: e?.message, variant: 'destructive' }),
  });

  const printLetter = async () => {
    if (!offerFor) return;
    try {
      const letter = await candidateService.getOfferLetter(offerFor.applicationId);
      const w = window.open('', '_blank');
      if (w) {
        w.document.write(letter.htmlBody);
        w.document.close();
        w.print();
      }
    } catch (e: any) {
      toast({ title: 'Could not load the letter', description: e?.message, variant: 'destructive' });
    }
  };

  if (dashboard.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const d = dashboard.data;
  const profile = d?.profile;
  const applications = d?.applications ?? [];
  const o: CandidateOffer | undefined = offer.data ?? undefined;
  const offerOpen = !!o && ['Extended', 'Negotiating'].includes(o.offerStatus);

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">
            {profile?.firstName ? `Welcome, ${profile.firstName}` : 'My applications'}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {d?.totalApplications ?? 0} application{(d?.totalApplications ?? 0) === 1 ? '' : 's'} ·{' '}
            {d?.activeApplications ?? 0} active · {d?.shortlistedCount ?? 0} shortlisted
          </p>
        </div>
        <Button asChild>
          <Link href="/careers">
            <Briefcase className="mr-2 h-4 w-4" />
            Browse open positions
          </Link>
        </Button>
      </div>

      {profile && !profile.candidateId && (
        <Card className="border-primary/40">
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
            <div className="flex items-center gap-3">
              <UserRound className="h-5 w-5 text-primary" />
              <div>
                <div className="font-medium">Complete your candidate profile</div>
                <div className="text-sm text-muted-foreground">
                  Applications carry your profile — you can&apos;t apply without one.
                </div>
              </div>
            </div>
            <Button asChild variant="outline">
              <Link href="/external-portal/careers/profile">Complete profile</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      {profile && !profile.isEmailVerified && (
        <Card>
          <CardContent className="flex flex-wrap items-center justify-between gap-3 py-4">
            <div className="flex items-center gap-3">
              <MailWarning className="h-5 w-5 text-amber-600" />
              <div className="text-sm">
                <span className="font-medium">Confirm your email address.</span>{' '}
                <span className="text-muted-foreground">
                  It links any existing candidate record with your address to this account.
                </span>
              </div>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={async () => {
                try {
                  const r = await candidateService.sendEmailConfirmation();
                  toast({ title: r.message });
                } catch (e: any) {
                  toast({ title: 'Could not send', description: e?.message, variant: 'destructive' });
                }
              }}
            >
              Send confirmation link
            </Button>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">My applications</CardTitle>
          <CardDescription>Newest first. An offer opens from its row when one arrives.</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {applications.length === 0 ? (
            <div className="py-12 text-center text-muted-foreground">
              <Briefcase className="mx-auto mb-3 h-8 w-8" />
              Nothing yet — find a role and apply.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Role</TableHead>
                  <TableHead className="w-32">Applied</TableHead>
                  <TableHead className="w-40">Status</TableHead>
                  <TableHead className="w-52 text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {applications.map((a) => (
                  <TableRow key={a.applicationId}>
                    <TableCell>
                      <div className="font-medium">{a.jobTitle}</div>
                      <div className="text-xs text-muted-foreground">
                        {a.applicationNumber}
                        {a.departmentName ? ` · ${a.departmentName}` : ''}
                        {a.locationName ? ` · ${a.locationName}` : ''}
                      </div>
                    </TableCell>
                    <TableCell className="text-sm">{formatDate(a.applicationDate)}</TableCell>
                    <TableCell>
                      <Badge variant={a.status === 'Rejected' || a.status === 'Withdrawn' ? 'secondary' : 'default'}>
                        {a.statusLabel}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-2">
                        {a.status === 'OfferExtended' && (
                          <Button
                            size="sm"
                            onClick={() => {
                              setOfferFor(a);
                              setOfferNotes('');
                              setOfferResponse('Accepted');
                            }}
                          >
                            <FileSignature className="mr-1.5 h-4 w-4" />
                            View offer
                          </Button>
                        )}
                        {a.canWithdraw && (
                          <Button variant="outline" size="sm" onClick={() => setWithdrawing(a)}>
                            Withdraw
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* withdraw */}
      <Dialog
        open={!!withdrawing}
        onOpenChange={(open) => {
          if (!open) {
            setWithdrawing(null);
            setWithdrawReason('');
          }
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Withdraw this application?</DialogTitle>
            <DialogDescription>
              {withdrawing?.jobTitle} — withdrawing cannot be undone, though you can apply again
              while the vacancy is open.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="withdraw-reason">Reason (optional)</Label>
            <Textarea
              id="withdraw-reason"
              value={withdrawReason}
              maxLength={1000}
              onChange={(e) => setWithdrawReason(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setWithdrawing(null)}>
              Keep it
            </Button>
            <Button variant="destructive" disabled={withdraw.isPending} onClick={() => withdraw.mutate()}>
              {withdraw.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* offer */}
      <Dialog open={!!offerFor} onOpenChange={(open) => !open && setOfferFor(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Offer — {offerFor?.jobTitle}</DialogTitle>
            <DialogDescription>
              {o?.offerNumber}
              {o?.expiryDate ? ` · respond by ${formatDate(o.expiryDate)}` : ''}
            </DialogDescription>
          </DialogHeader>
          {offer.isLoading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : offer.isError || !o ? (
            <p className="text-sm text-muted-foreground">No offer is available for this application.</p>
          ) : (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm sm:grid-cols-3">
                <div>
                  <div className="text-xs text-muted-foreground">Position</div>
                  {o.positionTitle ?? '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Department</div>
                  {o.departmentName ?? '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Location</div>
                  {o.locationName ?? '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Base salary</div>
                  {o.baseSalary != null ? formatMoney(o.baseSalary, o.currencyCode ?? 'GHS') : '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Start date</div>
                  {o.proposedStartDate ? formatDate(o.proposedStartDate) : '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Probation</div>
                  {o.probationPeriodMonths != null ? `${o.probationPeriodMonths} months` : '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Annual leave</div>
                  {o.annualLeaveDays != null ? `${o.annualLeaveDays} days` : '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Weekly hours</div>
                  {o.weeklyHours ?? '—'}
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">Status</div>
                  <Badge variant="secondary">{o.offerStatus}</Badge>
                </div>
              </div>

              {o.benefits && (
                <div className="text-sm">
                  <div className="text-xs text-muted-foreground">Benefits</div>
                  <p className="whitespace-pre-wrap">{o.benefits}</p>
                </div>
              )}
              {o.additionalTerms && (
                <div className="text-sm">
                  <div className="text-xs text-muted-foreground">Additional terms</div>
                  <p className="whitespace-pre-wrap">{o.additionalTerms}</p>
                </div>
              )}

              <Button variant="outline" size="sm" onClick={printLetter}>
                <Printer className="mr-1.5 h-4 w-4" />
                View / print the formal letter
              </Button>

              {offerOpen ? (
                <div className="space-y-3 rounded-lg border p-4">
                  <div className="grid gap-3 sm:grid-cols-2">
                    <div className="space-y-2">
                      <Label>Your response</Label>
                      <Select value={offerResponse} onValueChange={(v) => setOfferResponse(v as OfferResponseChoice)}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Accepted">Accept the offer</SelectItem>
                          <SelectItem value="Negotiating">Discuss the terms</SelectItem>
                          <SelectItem value="Declined">Decline</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="offer-notes">
                      {offerResponse === 'Declined' ? 'Why are you declining?' : 'Notes (optional)'}
                    </Label>
                    <Textarea
                      id="offer-notes"
                      value={offerNotes}
                      maxLength={4000}
                      onChange={(e) => setOfferNotes(e.target.value)}
                    />
                  </div>
                  <Button disabled={respond.isPending} onClick={() => respond.mutate()}>
                    {respond.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Send response
                  </Button>
                </div>
              ) : (
                (o.acceptedDate || o.declinedDate) && (
                  <p className="text-sm text-muted-foreground">
                    You responded on {formatDate(o.acceptedDate ?? o.declinedDate)}.
                    {o.candidateResponseNotes ? ` “${o.candidateResponseNotes}”` : ''}
                  </p>
                )
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
