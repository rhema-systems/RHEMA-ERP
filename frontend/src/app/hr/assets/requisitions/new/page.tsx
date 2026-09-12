'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardList, Loader2, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';
import { ASSET_REQUISITION_PRIORITIES } from '@/types/hr/assets';

const SELF = '__self__';

/**
 * Raising a requisition from the register — AST-6b's HR half, area 16 slice 12b.
 *
 * ⚠ The employee portal already lets somebody request equipment for themselves, and for a direct
 * report. Its beneficiary picker renders **only when the caller has direct reports** — deliberately,
 * because offering the field to everybody would be an invitation to a 403. The consequence nobody
 * noticed: an HR officer who manages nobody could not raise a request for anyone at all, although
 * the API has always allowed "line manager **or** HR". This screen is that missing half.
 *
 * ⚠ **Urgent is 1 and Low is 4** — the numbers run against reading order, and the create payload
 * takes the number. A picker built in reading order would file every emergency as an afterthought.
 * The options come from `ASSET_REQUISITION_PRIORITIES`.
 */
export default function NewAssetRequisitionPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [form, setForm] = useState({
    assetTypeId: '',
    beneficiaryEmployeeId: SELF,
    description: '',
    quantity: '1',
    priority: 'Medium',
    justification: '',
    requiredByDate: '',
  });
  const [error, setError] = useState<string | null>(null);

  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'assets', 'types'],
    queryFn: () => assetRegisterService.getTypes(),
  });

  /**
   * ⚠ `sendNow` is a mutation VARIABLE, not component state.
   *
   * The obvious shape — `setSendNow(false); submit();` — reads the old value: a `useState` setter
   * does not apply before the click handler finishes, so the "save as a draft" button would submit
   * the request anyway. Threading it through `mutate()` is the version that cannot drift.
   */
  const create = useMutation({
    mutationFn: async (sendNow: boolean) => {
      const created = await assetRegisterService.createRequisition({
        assetTypeId: form.assetTypeId,
        beneficiaryEmployeeId:
          form.beneficiaryEmployeeId === SELF ? null : form.beneficiaryEmployeeId,
        description: form.description.trim(),
        quantity: Number(form.quantity) || 1,
        priority: ASSET_REQUISITION_PRIORITIES.find((p) => p.label === form.priority)?.value ?? 3,
        justification: form.justification.trim(),
        // ⚠ A full DateTime on this payload, unlike the asset's own DateOnly fields.
        requiredByDate: form.requiredByDate ? `${form.requiredByDate}T00:00:00` : null,
      });
      // A requisition is born a Draft and nothing is decided until it is submitted. Leaving it as
      // one is a legitimate choice — saving a request nobody ever sends is not.
      if (sendNow) await assetRegisterService.submitRequisition(created.id);
      return created;
    },
    onSuccess: (created, sendNow) => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({
        title: sendNow ? 'Request raised and sent' : 'Request saved as a draft',
        description: created.requisitionNumber,
      });
      router.push(`/hr/assets/requisitions/${created.id}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not raise the request', description: e.message, variant: 'destructive' }),
  });

  const submit = (sendNow: boolean) => {
    if (!form.assetTypeId) return setError('Choose what kind of asset is being asked for.');
    if (!form.description.trim()) return setError('Say what is wanted.');
    if (!form.justification.trim()) return setError('Say why it is needed.');
    setError(null);
    create.mutate(sendNow);
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Raise a request"
        description="Ask for equipment, for yourself or on somebody else's behalf."
        backHref="/hr/assets/requisitions"
      />

      <Card>
        <CardHeader><CardTitle className="text-base">The request</CardTitle></CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>What kind of asset *</Label>
            <Select
              value={form.assetTypeId}
              onValueChange={(v) => setForm((f) => ({ ...f, assetTypeId: v }))}
            >
              <SelectTrigger><SelectValue placeholder="Choose a type" /></SelectTrigger>
              <SelectContent>
                {types.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>How many</Label>
            <Input
              type="number"
              min={1}
              value={form.quantity}
              onChange={(e) => setForm((f) => ({ ...f, quantity: e.target.value }))}
            />
          </div>

          <div className="space-y-2 sm:col-span-2">
            <Label>Who is it for?</Label>
            <div className="flex flex-wrap items-center gap-3">
              <Button
                type="button"
                size="sm"
                variant={form.beneficiaryEmployeeId === SELF ? 'default' : 'outline'}
                onClick={() => setForm((f) => ({ ...f, beneficiaryEmployeeId: SELF }))}
              >
                Me
              </Button>
              <Button
                type="button"
                size="sm"
                variant={form.beneficiaryEmployeeId !== SELF ? 'default' : 'outline'}
                onClick={() => setForm((f) => ({ ...f, beneficiaryEmployeeId: '' }))}
              >
                Somebody else
              </Button>
            </div>
            {form.beneficiaryEmployeeId !== SELF && (
              <div className="pt-2">
                <EmployeePicker
                  value={form.beneficiaryEmployeeId || null}
                  onChange={(v) => setForm((f) => ({ ...f, beneficiaryEmployeeId: v ?? '' }))}
                />
                <p className="mt-1 text-xs text-muted-foreground">
                  Naming somebody else needs the HR role, or being their recorded line manager. You
                  are recorded as having asked either way.
                </p>
              </div>
            )}
          </div>

          <div className="space-y-2">
            <Label>Priority</Label>
            <Select
              value={form.priority}
              onValueChange={(v) => setForm((f) => ({ ...f, priority: v }))}
            >
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {ASSET_REQUISITION_PRIORITIES.map((p) => (
                  <SelectItem key={p.label} value={p.label}>{p.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Needed by</Label>
            <Input
              type="date"
              value={form.requiredByDate}
              onChange={(e) => setForm((f) => ({ ...f, requiredByDate: e.target.value }))}
            />
          </div>

          <div className="space-y-2 sm:col-span-2">
            <Label>What is wanted *</Label>
            <Textarea
              rows={2}
              value={form.description}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
            />
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label>Why it is needed *</Label>
            <Textarea
              rows={3}
              value={form.justification}
              onChange={(e) => setForm((f) => ({ ...f, justification: e.target.value }))}
            />
          </div>
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex flex-wrap items-center justify-end gap-3">
        <Button
          variant="outline"
          disabled={create.isPending}
          onClick={() => submit(false)}
        >
          Save as a draft
        </Button>
        <Button
          disabled={create.isPending}
          onClick={() => submit(true)}
        >
          {create.isPending
            ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            : <Send className="mr-2 h-4 w-4" />}
          Raise and send for approval
        </Button>
      </div>
    </div>
  );
}
