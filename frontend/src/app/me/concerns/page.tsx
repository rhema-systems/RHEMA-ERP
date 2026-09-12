'use client';

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Loader2, ShieldQuestion, Copy, Send, KeyRound, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { toast } from 'sonner';
import { employeeRelationsConcernPortalService } from '@/services/hr/employee-relations-portal.service';
import {
  CONCERN_CATEGORY_OPTIONS, CONCERN_STATUS_OPTIONS,
  type ConcernCategory, type ConcernReceipt, type EmployeeRelationsConcern,
} from '@/types/hr/employee-relations';

const MIN_STATEMENT = 20;

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const statusLabel = (v: string) => CONCERN_STATUS_OPTIONS.find((s) => s.value === v)?.label ?? v;

/**
 * Report a concern without giving your name, and follow it up afterwards.
 *
 * ⚠ **The retrieval code is shown exactly once and cannot be reissued.** It is hashed on the way
 * in, so HR cannot look it up either — which is what makes the channel anonymous rather than
 * merely discreet. The whole design of this screen follows from that: the code is presented as
 * something to copy and keep now, not as something to find again later, and there is deliberately
 * no "email it to me" affordance, which would attach an identity to the report.
 *
 * ⚠ **Rate limited to 5 requests a minute.** Do not add polling or automatic retries here.
 */
export default function MyConcernsPage() {
  const [category, setCategory] = useState<ConcernCategory>('Misconduct');
  const [subject, setSubject] = useState('');
  const [statement, setStatement] = useState('');
  const [receipt, setReceipt] = useState<ConcernReceipt | null>(null);

  const [trackNumber, setTrackNumber] = useState('');
  const [trackCode, setTrackCode] = useState('');
  const [tracked, setTracked] = useState<EmployeeRelationsConcern | null>(null);
  const [updateBody, setUpdateBody] = useState('');

  const report = useMutation({
    mutationFn: () => employeeRelationsConcernPortalService.report({
      category, subject: subject.trim(), statement: statement.trim(),
    }),
    onSuccess: (r) => {
      setReceipt(r);
      setSubject(''); setStatement('');
    },
    onError: (e: Error) => toast.error(e.message || 'Could not send it'),
  });

  const track = useMutation({
    mutationFn: () => employeeRelationsConcernPortalService.track({
      concernNumber: trackNumber.trim(), retrievalCode: trackCode.trim(),
    }),
    onSuccess: setTracked,
    // ⚠ The server's message on purpose. It is the SAME refusal whether the number or the code is
    // wrong, and rewording it to guess which would turn this form into a way of discovering that
    // somebody reported something.
    onError: (e: Error) => { setTracked(null); toast.error(e.message || 'Could not open it'); },
  });

  const addUpdate = useMutation({
    mutationFn: () => employeeRelationsConcernPortalService.addUpdate({
      concernNumber: trackNumber.trim(), retrievalCode: trackCode.trim(), body: updateBody.trim(),
    }),
    onSuccess: (c) => { setTracked(c); setUpdateBody(''); toast.success('Added to your report.'); },
    onError: (e: Error) => toast.error(e.message || 'Could not add it'),
  });

  const canReport = subject.trim().length > 0 && statement.trim().length >= MIN_STATEMENT;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Report a concern anonymously"
        description="For something you need to raise without your name attached. Nobody — including HR — can tell who made the report."
        backHref="/me"
      />

      <Tabs defaultValue="report">
        <TabsList>
          <TabsTrigger value="report">Make a report</TabsTrigger>
          <TabsTrigger value="track">Follow up on one</TabsTrigger>
        </TabsList>

        <TabsContent value="report" className="space-y-4">
          {receipt ? (
            <Card className="border-primary">
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <KeyRound className="h-4 w-4" />
                  Keep this code — it is shown only once
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <div>
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Reference</div>
                    <div className="font-mono text-lg">{receipt.concernNumber}</div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">Retrieval code</div>
                    <div className="font-mono text-lg tracking-wider">{receipt.retrievalCode}</div>
                  </div>
                </div>

                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    // Copy, not "send" — anywhere this could be sent would attach an identity to it.
                    navigator.clipboard?.writeText(`${receipt.concernNumber} / ${receipt.retrievalCode}`);
                    toast.success('Copied. Paste it somewhere safe now.');
                  }}
                >
                  <Copy className="mr-2 h-4 w-4" /> Copy both
                </Button>

                <div className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm dark:bg-amber-950/30">
                  <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
                  {/* The server's own wording, not a paraphrase: it is the notice, and softening it
                      would misdescribe what happens if the code is lost. */}
                  <span>{receipt.notice}</span>
                </div>

                <p className="text-xs text-muted-foreground">
                  Reported {fmtDateTime(receipt.reportedAt)}. Use the code on the
                  &ldquo;Follow up&rdquo; tab to read HR&apos;s reply and add anything further —
                  still without giving your name.
                </p>

                <Button variant="ghost" size="sm" onClick={() => setReceipt(null)}>
                  Make another report
                </Button>
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">What has happened?</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                  <ShieldQuestion className="mr-1 inline h-4 w-4" />
                  Nothing identifying you is recorded — not your name, not your staff number, not
                  your account. HR sees the report and can reply to it, but cannot tell who sent it.
                  <strong className="block pt-1 text-foreground">
                    So take care not to identify yourself in the text unless you mean to.
                  </strong>
                </div>

                <div className="space-y-2">
                  <Label>What kind of concern is it?</Label>
                  <Select value={category} onValueChange={(v) => setCategory(v as ConcernCategory)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {CONCERN_CATEGORY_OPTIONS.map((o) => (
                        <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="c-subject">In one line *</Label>
                  <Input
                    id="c-subject" value={subject} maxLength={300}
                    onChange={(e) => setSubject(e.target.value)}
                    placeholder="A short summary"
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="c-statement">What happened *</Label>
                  <Textarea
                    id="c-statement" rows={8} value={statement} maxLength={6000}
                    onChange={(e) => setStatement(e.target.value)}
                    placeholder="What happened, when, and where. The more specific you can be, the more HR can do about it."
                  />
                  <p className="text-xs text-muted-foreground">
                    At least {MIN_STATEMENT} characters. {statement.trim().length} so far.
                  </p>
                </div>

                <Button disabled={!canReport || report.isPending} onClick={() => report.mutate()}>
                  {report.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Send it
                </Button>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="track" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Open your report</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="t-number">Reference</Label>
                  <Input
                    id="t-number" value={trackNumber} className="font-mono"
                    onChange={(e) => setTrackNumber(e.target.value)}
                    placeholder="CON-2026-00001"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="t-code">Retrieval code</Label>
                  <Input
                    id="t-code" value={trackCode} className="font-mono"
                    onChange={(e) => setTrackCode(e.target.value)}
                  />
                </div>
              </div>
              <Button
                disabled={!trackNumber.trim() || !trackCode.trim() || track.isPending}
                onClick={() => track.mutate()}
              >
                {track.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Open
              </Button>
              <p className="text-xs text-muted-foreground">
                If you have lost the code there is no way to recover it — not by asking HR, who never
                had it. You would need to make a fresh report.
              </p>
            </CardContent>
          </Card>

          {tracked && (
            <>
              <Card>
                <CardHeader className="flex flex-row items-start justify-between">
                  <div>
                    <CardTitle className="text-base">{tracked.concernNumber} · {tracked.subject}</CardTitle>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {tracked.categoryName} · reported {fmtDateTime(tracked.reportedAt)}
                    </p>
                  </div>
                  <StatusBadge status={statusLabel(tracked.status)} />
                </CardHeader>
                <CardContent className="space-y-3">
                  <p className="whitespace-pre-wrap text-sm">{tracked.statement}</p>

                  {tracked.triagedAt && (
                    <div className="rounded-md border p-3 text-sm">
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">
                        What HR made of it · {fmtDateTime(tracked.triagedAt)}
                      </div>
                      <p className="mt-1 whitespace-pre-wrap">{tracked.triageNotes}</p>
                    </div>
                  )}

                  {tracked.closedAt && (
                    <div className="rounded-md border border-dashed p-3 text-sm">
                      <span className="font-medium">Closed {fmtDateTime(tracked.closedAt)}</span>
                      {tracked.closureReason ? ` — ${tracked.closureReason}` : ''}
                    </div>
                  )}

                  {tracked.convertedCaseNumber && (
                    <div className="rounded-md border p-3 text-sm">
                      {/*
                        The reference, and NOT a link. The case is about somebody, and the reporter
                        is not necessarily on it — a link that 403'd would be worse than none.
                      */}
                      This became employee-relations case{' '}
                      <span className="font-medium">{tracked.convertedCaseNumber}</span>. Only the
                      subject and your description were carried across — nothing from the messages
                      below.
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Messages</CardTitle>
                  <p className="mt-1 text-xs text-muted-foreground">
                    HR often needs one more detail. You can answer here and still not be named.
                  </p>
                </CardHeader>
                <CardContent className="space-y-3">
                  {tracked.updates.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Nothing yet.</p>
                  ) : (
                    tracked.updates.map((u) => (
                      <div
                        key={u.id}
                        className={`rounded-md border p-3 text-sm ${u.isFromReporter ? 'bg-muted/40' : ''}`}
                      >
                        <div className="text-xs text-muted-foreground">
                          {u.isFromReporter ? 'You' : `${u.authorName} (HR)`} · {fmtDateTime(u.postedAt)}
                        </div>
                        <p className="mt-1 whitespace-pre-wrap">{u.body}</p>
                      </div>
                    ))
                  )}

                  {tracked.status !== 'Closed' && tracked.status !== 'ConvertedToCase' && (
                    <div className="space-y-2 pt-2">
                      <Label htmlFor="t-update">Add something</Label>
                      <Textarea
                        id="t-update" rows={3} value={updateBody}
                        onChange={(e) => setUpdateBody(e.target.value)}
                      />
                      <Button
                        size="sm"
                        disabled={updateBody.trim().length < 5 || addUpdate.isPending}
                        onClick={() => addUpdate.mutate()}
                      >
                        {addUpdate.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                        <Send className="mr-2 h-4 w-4" /> Send
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>
            </>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
