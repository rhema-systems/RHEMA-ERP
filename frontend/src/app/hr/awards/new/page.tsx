'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { AlertTriangle, Info, Loader2, Trophy } from 'lucide-react';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { awardsService } from '@/services/hr/awards.service';

/**
 * Confer an award outright, with no nomination behind it (AWD-07).
 *
 * ⚠ **Only a `ManagementDirect` award may be conferred this way**, and the API refuses anything
 * else. That is not a formality: allowing it elsewhere would let somebody hand out the prize while
 * the ballot was still open. The picker therefore offers only awards whose nominations come from
 * management, and says why the others are absent.
 *
 * ⚠ **A levelled award refuses to be conferred at no level.** Before slice 8 the level was simply
 * dropped, which made a Gold and a Bronze award indistinguishable on every read afterwards.
 */
export default function ConferAwardPage() {
  const router = useRouter();

  const [awardTypeId, setAwardTypeId] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [levelId, setLevelId] = useState('');
  const [awardDate, setAwardDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [citation, setCitation] = useState('');

  const { data: types, isLoading } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
  });

  const all = types ?? [];
  const direct = all.filter((t) => t.isActive && t.nominationSource === 'ManagementDirect');
  const chosen = direct.find((t) => t.id === awardTypeId);

  const { data: levels } = useQuery({
    queryKey: ['award-levels', awardTypeId],
    queryFn: () => awardsService.getLevels(awardTypeId),
    enabled: Boolean(awardTypeId) && Boolean(chosen?.hasLevels),
  });

  const confer = useMutation({
    mutationFn: () =>
      awardsService.conferDirectly({
        employeeId,
        awardTypeId,
        awardLevelId: levelId || null,
        awardDate: new Date(awardDate).toISOString().slice(0, 19),
        reason: reason.trim(),
        citation: citation.trim() || null,
        monetaryAmount: amount === '' ? null : Number(amount),
      }),
    onSuccess: (award) => {
      toast.success(`Award ${award.awardNumber} conferred.`);
      router.push(`/hr/awards/${award.id}`);
    },
    onError: (e: any) =>
      toast.error(e?.body?.detail || e?.body?.message || e?.message || 'The conferral was refused.'),
  });

  const ready = useMemo(() => {
    if (!awardTypeId || !employeeId || !awardDate || reason.trim().length === 0) return false;
    return chosen?.hasLevels ? levelId.length > 0 : true;
  }, [awardTypeId, employeeId, awardDate, reason, chosen, levelId]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Confer an award"
        description="Hand out an award directly, without a nomination behind it."
        backHref="/hr/awards"
      />

      {isLoading ? (
        <div className="flex items-center justify-center p-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : direct.length === 0 ? (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            No award is set up for direct management selection. Only awards whose nominations come
            from management can be conferred this way — the rest go through nomination and either a
            vote or a committee.
          </AlertDescription>
        </Alert>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Which award, and to whom</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  {direct.length} of {all.filter((t) => t.isActive).length} active awards can be
                  conferred directly. The others are decided by a vote or by a committee, and handing
                  one out here would pre-empt that.
                </AlertDescription>
              </Alert>

              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>Award</Label>
                  <Select
                    value={awardTypeId}
                    onValueChange={(v) => { setAwardTypeId(v); setLevelId(''); }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Choose an award" />
                    </SelectTrigger>
                    <SelectContent>
                      {direct.map((t) => (
                        <SelectItem key={t.id} value={t.id}>
                          {t.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label>Employee</Label>
                  <EmployeePicker
                    value={employeeId || null}
                    onChange={(id) => setEmployeeId(id ?? '')}
                  />
                </div>

                {chosen?.hasLevels && (
                  <div className="space-y-2">
                    <Label>Level</Label>
                    <Select value={levelId} onValueChange={setLevelId}>
                      <SelectTrigger>
                        <SelectValue placeholder="Which level?" />
                      </SelectTrigger>
                      <SelectContent>
                        {(levels ?? [])
                          .filter((l) => l.isActive)
                          .map((l) => (
                            <SelectItem key={l.id} value={l.id}>
                              {l.name}
                            </SelectItem>
                          ))}
                      </SelectContent>
                    </Select>
                    <p className="text-xs text-muted-foreground">
                      This award has levels, so it cannot be conferred without naming one.
                    </p>
                  </div>
                )}

                <div className="space-y-2">
                  <Label htmlFor="awardDate">Award date</Label>
                  <Input
                    id="awardDate"
                    type="date"
                    value={awardDate}
                    onChange={(e) => setAwardDate(e.target.value)}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="amount">Amount</Label>
                  <Input
                    id="amount"
                    type="number"
                    min={0}
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    placeholder="Leave blank for none"
                  />
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Why</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="reason">Reason</Label>
                <Textarea
                  id="reason"
                  rows={4}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Why this award is being given"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="citation">Citation (optional)</Label>
                <Textarea
                  id="citation"
                  rows={3}
                  value={citation}
                  onChange={(e) => setCitation(e.target.value)}
                  placeholder="The wording for the certificate"
                />
              </div>

              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => router.push('/hr/awards')}>
                  Cancel
                </Button>
                <Button disabled={!ready || confer.isPending} onClick={() => confer.mutate()}>
                  {confer.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Trophy className="mr-2 h-4 w-4" />
                  )}
                  Confer
                </Button>
              </div>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
