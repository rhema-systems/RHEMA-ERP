'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Info, Loader2, Save } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { CurrencyPicker } from '@/components/hr/common/CurrencyPicker';
import { useToast } from '@/hooks/use-toast';
import { jobOfferService } from '@/services/hr/offers.service';
import type { UpdateJobOffer } from '@/types/hr/offers';

/**
 * Editing an offer's negotiated terms. Draft and Pending-Approval only — the server refuses outside
 * those statuses, and the role snapshot (position, grade, reporting line, employment type) is not on
 * this form at all: it is server-owned and cannot be sent, even to leave it unchanged.
 */
export default function EditJobOfferPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const { toast } = useToast();

  const { data: offer, isLoading } = useQuery({
    queryKey: ['hr', 'offers', id],
    queryFn: () => jobOfferService.getById(id),
    enabled: !!id,
  });

  const [form, setForm] = useState<{
    baseSalary: string;
    currencyCode: string;
    bonus: string;
    bonusTerms: string;
    commission: string;
    commissionStructure: string;
    proposedStartDate: string;
    expiryDate: string;
    contractDurationMonths: string;
    probationPeriodMonths: string;
    noticePeriodMonths: string;
    annualLeaveDays: string;
    weeklyHours: string;
    ndaRequired: boolean;
    isConditional: boolean;
    additionalTerms: string;
  } | null>(null);

  useEffect(() => {
    if (offer && !form) {
      setForm({
        baseSalary: offer.baseSalary?.toString() ?? '',
        currencyCode: offer.currencyCode ?? 'GHS',
        bonus: offer.bonus?.toString() ?? '',
        bonusTerms: offer.bonusTerms ?? '',
        commission: offer.commission?.toString() ?? '',
        commissionStructure: offer.commissionStructure ?? '',
        proposedStartDate: offer.proposedStartDate ?? '',
        expiryDate: offer.expiryDate?.slice(0, 10) ?? '',
        contractDurationMonths: offer.contractDurationMonths?.toString() ?? '',
        probationPeriodMonths: offer.probationPeriodMonths?.toString() ?? '',
        noticePeriodMonths: offer.noticePeriodMonths?.toString() ?? '',
        annualLeaveDays: offer.annualLeaveDays?.toString() ?? '',
        weeklyHours: offer.weeklyHours?.toString() ?? '',
        ndaRequired: offer.ndaRequired,
        isConditional: offer.isConditional,
        additionalTerms: offer.additionalTerms ?? '',
      });
    }
  }, [offer, form]);

  const save = useMutation({
    mutationFn: () => {
      if (!form) throw new Error('Nothing to save');
      const payload: UpdateJobOffer = {
        locationLevelId: offer?.locationLevelId ?? null,
        locationId: offer?.locationId ?? null,
        baseSalary: form.baseSalary ? Number(form.baseSalary) : null,
        currencyCode: form.currencyCode.trim() || null,
        bonus: form.bonus ? Number(form.bonus) : null,
        bonusTerms: form.bonusTerms.trim() || null,
        commission: form.commission ? Number(form.commission) : null,
        commissionStructure: form.commissionStructure.trim() || null,
        proposedStartDate: form.proposedStartDate || null,
        expiryDate: form.expiryDate || null,
        contractDurationMonths: form.contractDurationMonths ? Number(form.contractDurationMonths) : null,
        probationPeriodMonths: form.probationPeriodMonths ? Number(form.probationPeriodMonths) : null,
        noticePeriodMonths: form.noticePeriodMonths ? Number(form.noticePeriodMonths) : null,
        annualLeaveDays: form.annualLeaveDays ? Number(form.annualLeaveDays) : null,
        weeklyHours: form.weeklyHours ? Number(form.weeklyHours) : null,
        ndaRequired: form.ndaRequired,
        isConditional: form.isConditional,
        additionalTerms: form.additionalTerms.trim() || null,
      };
      return jobOfferService.update(id, payload);
    },
    onSuccess: () => {
      toast({ title: 'Offer updated' });
      router.push(`/hr/recruitment/offers/${id}`);
    },
    onError: (e: any) =>
      toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  if (isLoading || !form) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!offer) {
    return (
      <div className="p-6">
        <EmptyState title="Offer not found" description="It may have been removed." />
      </div>
    );
  }

  const editable = offer.offerStatus === 'Draft' || offer.offerStatus === 'PendingApproval';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`Edit ${offer.offerNumber}`}
        description={`${offer.candidateName} · ${offer.positionTitle}`}
        backHref={`/hr/recruitment/offers/${id}`}
        actions={
          <Button onClick={() => save.mutate()} disabled={!editable || save.isPending}>
            <Save className="mr-2 h-4 w-4" />
            {save.isPending ? 'Saving…' : 'Save'}
          </Button>
        }
      />

      {!editable && (
        <Alert variant="destructive">
          <Info className="h-4 w-4" />
          <AlertTitle>This offer can no longer be edited</AlertTitle>
          <AlertDescription>
            Only a Draft or Pending-Approval offer accepts changes here (currently{' '}
            {offer.offerStatusName}). Use Revise once it has been sent to change the terms after a
            counter-offer.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Compensation</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <div className="space-y-1.5">
            <Label htmlFor="baseSalary">Base salary</Label>
            <Input
              id="baseSalary"
              type="number"
              step="0.01"
              disabled={!editable}
              value={form.baseSalary}
              onChange={(e) => setForm({ ...form, baseSalary: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            {/* ⚠ Round 4, lane G3. The server now refuses a code Finance does not hold, so a
                free-text box here would offer the user a 422 they could not see coming. */}
            <Label htmlFor="currencyCode">Currency</Label>
            <CurrencyPicker
              id="currencyCode"
              value={form.currencyCode}
              onChange={(code) => setForm({ ...form, currencyCode: code })}
              disabled={!editable}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="weeklyHours">Weekly hours</Label>
            <Input
              id="weeklyHours"
              type="number"
              step="0.5"
              disabled={!editable}
              value={form.weeklyHours}
              onChange={(e) => setForm({ ...form, weeklyHours: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="bonus">Bonus</Label>
            <Input
              id="bonus"
              type="number"
              step="0.01"
              disabled={!editable}
              value={form.bonus}
              onChange={(e) => setForm({ ...form, bonus: e.target.value })}
            />
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Label htmlFor="bonusTerms">Bonus terms</Label>
            <Input
              id="bonusTerms"
              disabled={!editable}
              value={form.bonusTerms}
              onChange={(e) => setForm({ ...form, bonusTerms: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="commission">Commission</Label>
            <Input
              id="commission"
              type="number"
              step="0.01"
              disabled={!editable}
              value={form.commission}
              onChange={(e) => setForm({ ...form, commission: e.target.value })}
            />
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <Label htmlFor="commissionStructure">Commission structure</Label>
            <Input
              id="commissionStructure"
              disabled={!editable}
              value={form.commissionStructure}
              onChange={(e) => setForm({ ...form, commissionStructure: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Dates and terms</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <div className="space-y-1.5">
            <Label htmlFor="proposedStartDate">Proposed start date</Label>
            <Input
              id="proposedStartDate"
              type="date"
              disabled={!editable}
              value={form.proposedStartDate}
              onChange={(e) => setForm({ ...form, proposedStartDate: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="expiryDate">Offer expires</Label>
            <Input
              id="expiryDate"
              type="date"
              disabled={!editable}
              value={form.expiryDate}
              onChange={(e) => setForm({ ...form, expiryDate: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="contractDurationMonths">Contract length (months)</Label>
            <Input
              id="contractDurationMonths"
              type="number"
              min={0}
              disabled={!editable}
              value={form.contractDurationMonths}
              onChange={(e) => setForm({ ...form, contractDurationMonths: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="probationPeriodMonths">Probation (months)</Label>
            <Input
              id="probationPeriodMonths"
              type="number"
              min={0}
              disabled={!editable}
              value={form.probationPeriodMonths}
              onChange={(e) => setForm({ ...form, probationPeriodMonths: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="noticePeriodMonths">Notice period (months)</Label>
            <Input
              id="noticePeriodMonths"
              type="number"
              min={0}
              disabled={!editable}
              value={form.noticePeriodMonths}
              onChange={(e) => setForm({ ...form, noticePeriodMonths: e.target.value })}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="annualLeaveDays">Annual leave (days)</Label>
            <Input
              id="annualLeaveDays"
              type="number"
              min={0}
              disabled={!editable}
              value={form.annualLeaveDays}
              onChange={(e) => setForm({ ...form, annualLeaveDays: e.target.value })}
            />
          </div>

          <div className="flex items-center gap-2">
            <Checkbox
              id="ndaRequired"
              disabled={!editable}
              checked={form.ndaRequired}
              onCheckedChange={(c) => setForm({ ...form, ndaRequired: c === true })}
            />
            <Label htmlFor="ndaRequired" className="font-normal">
              NDA required
            </Label>
          </div>
          <div className="flex items-center gap-2">
            <Checkbox
              id="isConditional"
              disabled={!editable}
              checked={form.isConditional}
              onCheckedChange={(c) => setForm({ ...form, isConditional: c === true })}
            />
            <Label htmlFor="isConditional" className="font-normal">
              Conditional on pre-employment checks
            </Label>
          </div>

          <div className="space-y-1.5 md:col-span-3">
            <Label htmlFor="additionalTerms">Additional terms</Label>
            <Textarea
              id="additionalTerms"
              rows={4}
              disabled={!editable}
              value={form.additionalTerms}
              onChange={(e) => setForm({ ...form, additionalTerms: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => router.push(`/hr/recruitment/offers/${id}`)}>
          Cancel
        </Button>
        <Button onClick={() => save.mutate()} disabled={!editable || save.isPending}>
          <Save className="mr-2 h-4 w-4" />
          {save.isPending ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </div>
  );
}
