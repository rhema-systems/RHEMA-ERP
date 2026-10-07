'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Sparkles } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { fiscalPeriodFor, useFinanceFiscalYears } from '@/components/hr/manpower/ManpowerBudgetFormFields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { policySettingsService } from '@/services/hr/policy-settings.service';

/**
 * "Use the position establishment to initiate the manpower recruitment budget" (round 2b, R4a).
 *
 * Choose the unit and the year; a Draft budget is created with one line per post in the unit and
 * the units under it, pre-filled from the planning baseline: posts authorised = in post + gap,
 * new posts = gap + exits due, the salary from the post's grade on the scale or the mean pay of
 * the people in it. The four money budgets are left for the holder to type. The page then opens
 * the draft. ⚠ One live budget per unit and year: a second attempt is refused naming the first.
 */
export function PlanBudgetFromEstablishmentDialog({
  open,
  onOpenChange,
  initialUnitId,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  initialUnitId?: string | null;
}) {
  const router = useRouter();
  const thisYear = new Date().getFullYear();
  const [unitId, setUnitId] = useState(initialUnitId ?? '');
  const [fiscalYear, setFiscalYear] = useState(thisYear + 1);
  const [periodStart, setPeriodStart] = useState(`${thisYear + 1}-01-01`);
  const [periodEnd, setPeriodEnd] = useState(`${thisYear + 1}-12-31`);
  const [includeUnestablished, setIncludeUnestablished] = useState(true);
  const [busy, setBusy] = useState(false);

  const { data: policy } = useQuery({
    queryKey: ['hr', 'policy-settings'],
    queryFn: () => policySettingsService.get(),
    staleTime: 5 * 60 * 1000,
  });
  const startMonth = policy?.fiscalYearStartMonth ?? 1;
  // Lane 4b (D-6): Finance's year of that number, or its sequence continued; the start month only with no Finance year.
  const financeYears = useFinanceFiscalYears();

  useEffect(() => {
    if (open) setUnitId(initialUnitId ?? '');
  }, [open, initialUnitId]);

  useEffect(() => {
    const p = fiscalPeriodFor(fiscalYear, startMonth, financeYears);
    setPeriodStart(p.start);
    setPeriodEnd(p.end);
  }, [fiscalYear, startMonth, financeYears]);

  const canGo = !!unitId && fiscalYear >= 2000 && fiscalYear <= 2100 && !!periodStart && !!periodEnd && periodEnd >= periodStart;

  const go = async () => {
    if (!canGo) return;
    setBusy(true);
    try {
      const created = await jobArchitectureService.createBudgetFromEstablishment({
        organizationUnitId: unitId,
        fiscalYear,
        periodStart,
        periodEnd,
        includeUnestablished,
      });
      toast.success(`${created.budgetNumber} drafted with ${created.budgetLines?.length ?? 0} line(s)`);
      onOpenChange(false);
      router.push(`/hr/manpower-budgets/${created.id}`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not draft the budget');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>Plan a budget from the establishment</DialogTitle>
          <DialogDescription>
            A draft manpower recruitment budget for the unit and the units under it, with one line
            per post: posts authorised, new posts from the gap and the exits due, and the salary
            from the scale. You fill in the money and refine the lines before submitting.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <OrganizationUnitPicker
            value={unitId}
            onChange={(id) => setUnitId(id)}
            unitLabel="Organisation unit *"
            idPrefix="plan-unit"
            showCode
          />
          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="plan-year">Fiscal year *</Label>
              <Input id="plan-year" type="number" min={2000} max={2100} value={fiscalYear} onChange={(e) => setFiscalYear(Number(e.target.value))} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="plan-start">Period start</Label>
              <Input id="plan-start" type="date" value={periodStart} onChange={(e) => setPeriodStart(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="plan-end">Period end</Label>
              <Input id="plan-end" type="date" value={periodEnd} onChange={(e) => setPeriodEnd(e.target.value)} />
            </div>
          </div>
          <div className="flex items-start gap-2">
            <Checkbox id="plan-unest" checked={includeUnestablished} onCheckedChange={(c) => setIncludeUnestablished(c === true)} />
            <div>
              <Label htmlFor="plan-unest">Include posts nobody has established</Label>
              <p className="text-xs text-muted-foreground">
                Their gap is unknown, so such a line plans only the exits due. The budget&apos;s
                approval is what establishes them.
              </p>
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={go} disabled={!canGo || busy}>
            {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Sparkles className="mr-2 h-4 w-4" />}
            Draft the budget
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
