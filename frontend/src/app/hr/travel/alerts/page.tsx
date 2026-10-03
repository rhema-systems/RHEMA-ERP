'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus, Trash2, TriangleAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';
import type {
  StaffTravelAlertSummary,
  TravelAlertSeverity,
  TravelAlertType,
} from '@/types/hr/travel-compliance';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
/** `datetime-local` wants no zone; the API takes a DateTime. */
const toDateInput = (v?: string | null) => (v ? String(v).slice(0, 10) : '');

const ALERT_TYPES: TravelAlertType[] = [
  'Security', 'HealthOutbreak', 'Weather', 'PoliticalUnrest',
  'TransportDisruption', 'NaturalDisaster',
];

const SEVERITIES: TravelAlertSeverity[] = ['Info', 'Warning', 'Critical', 'Emergency'];

const SEVERITY_TONE: Record<TravelAlertSeverity, string> = {
  Info: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200',
  Warning: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  Critical: 'bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-200',
  Emergency: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

interface FormState {
  alertType: TravelAlertType;
  severity: TravelAlertSeverity;
  countryId: string;
  city: string;
  title: string;
  body: string;
  source: string;
  effectiveFrom: string;
  effectiveTo: string;
  isActive: boolean;
}

const BLANK: FormState = {
  alertType: 'Security',
  severity: 'Warning',
  countryId: '',
  city: '',
  title: '',
  body: '',
  source: '',
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
  isActive: true,
};

/**
 * Destination alerts — what travellers are told about where they are going.
 *
 * ⚠ **This feed had no maintenance screen at all.** `createAlert`, `updateAlert` and `deleteAlert`
 * have existed in the client throughout and nothing called any of them, so the alerts a traveller
 * sees on their trip could only ever have come from a seed or a database edit. The coverage
 * instrument counted them wired because the client methods exist.
 *
 * ⚠ **An alert dispatches for real**, unlike the policy rules next door: `SendAlertAsync`
 * publishes an event carrying the title, severity, country, trip reference, route and dates to
 * the traveller. That is why this ships with authoring and the rules register does not.
 *
 * ⚠ **The list reads are summaries and carry no `body`.** The edit dialog therefore fetches the
 * alert by id rather than binding the row — the same reason the travel-request alert strip was
 * showing a severity with no text until the per-destination read was widened.
 */
export default function TravelAlertsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { canWrite, canAdmin: canDelete } = useTravelAccess();

  const { data: alerts, isLoading, isError, error } = useQuery({
    queryKey: ['travel-alerts', 'active'],
    queryFn: () => travelComplianceService.getActiveAlerts(),
  });

  const { data: countries } = useQuery({
    queryKey: ['countries'],
    queryFn: () => countryService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<FormState>(BLANK);
  const [pendingDelete, setPendingDelete] = useState<StaffTravelAlertSummary | null>(null);

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const fail = (title: string) => (e: Error) =>
    toast({
      title,
      description: e?.message || 'Please try again.',
      variant: 'destructive',
    });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['travel-alerts'] });

  const payload = () => ({
    alertType: form.alertType,
    severity: form.severity,
    countryId: form.countryId,
    city: form.city.trim() || null,
    title: form.title.trim(),
    body: form.body.trim() || null,
    source: form.source.trim() || null,
    effectiveFrom: new Date(`${form.effectiveFrom}T00:00:00`).toISOString(),
    effectiveTo: form.effectiveTo
      ? new Date(`${form.effectiveTo}T23:59:59`).toISOString()
      : null,
    isActive: form.isActive,
  });

  const save = useMutation({
    mutationFn: () =>
      editingId
        ? travelComplianceService.updateAlert({ ...payload(), id: editingId })
        : travelComplianceService.createAlert(payload()),
    onSuccess: async () => {
      await refresh();
      toast({ title: editingId ? 'Alert updated' : 'Alert raised' });
      setOpen(false);
    },
    onError: fail('Could not save the alert'),
  });

  const remove = useMutation({
    mutationFn: (a: StaffTravelAlertSummary) => travelComplianceService.deleteAlert(a.id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Alert removed' });
      setPendingDelete(null);
    },
    onError: fail('Could not remove the alert'),
  });

  const openCreate = () => {
    setForm(BLANK);
    setEditingId(null);
    setOpen(true);
  };

  /**
   * ⚠ Fetches by id. The list row is a summary with no `body`, `source`, `countryId` or
   * `effectiveTo`, so an edit form seeded from it would blank four fields on save.
   */
  const openEdit = async (row: StaffTravelAlertSummary) => {
    try {
      const full = await travelComplianceService.getAlert(row.id);
      setForm({
        alertType: full.alertType,
        severity: full.severity,
        countryId: full.countryId,
        city: full.city ?? '',
        title: full.title,
        body: full.body ?? '',
        source: full.source ?? '',
        effectiveFrom: toDateInput(full.effectiveFrom),
        effectiveTo: toDateInput(full.effectiveTo),
        isActive: full.isActive,
      });
      setEditingId(full.id);
      setOpen(true);
    } catch (e) {
      fail('Could not open the alert')(e as Error);
    }
  };

  const rows = alerts ?? [];
  const valid = form.title.trim().length > 0 && !!form.countryId && !!form.effectiveFrom;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Destination alerts"
        description="What travellers are told about security, health and disruption where they are going."
        backHref="/hr/travel"
        actions={
          canWrite ? (
            <Button onClick={openCreate}>
              <Plus className="mr-2 h-4 w-4" />
              Raise an alert
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError && !alerts ? (
            <div className="p-4">
              <TravelQueryError error={error} what="the destination alerts" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={TriangleAlert}
              title="No active alerts"
              description="Nothing is in force for any destination. A trip to a country with no alert shows nothing on its compliance tab."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Alert</TableHead>
                  <TableHead>Where</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>In force from</TableHead>
                  <TableHead className="w-24 text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>
                      <div className="font-medium">{a.title}</div>
                      <div className="text-xs text-muted-foreground">
                        {spaced(a.alertTypeName || a.alertType)}
                      </div>
                    </TableCell>
                    <TableCell>
                      {a.countryName ?? '—'}
                      {a.city && (
                        <span className="text-muted-foreground"> · {a.city}</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge className={SEVERITY_TONE[a.severity] ?? ''}>
                        {spaced(a.severityName || a.severity)}
                      </Badge>
                    </TableCell>
                    <TableCell>{fmtDate(a.effectiveFrom)}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        {canWrite && (
                          <Button variant="ghost" size="sm" onClick={() => openEdit(a)}>
                            <Pencil className="h-4 w-4" />
                            <span className="sr-only">Edit</span>
                          </Button>
                        )}
                        {canDelete && (
                          <Button
                            variant="ghost"
                            size="sm"
                            className="text-red-600"
                            onClick={() => setPendingDelete(a)}
                          >
                            <Trash2 className="h-4 w-4" />
                            <span className="sr-only">Remove</span>
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

      <p className="text-xs text-muted-foreground">
        An alert appears automatically on the travel desk&apos;s view of any trip to that country
        while it is in force. A traveller sees it only once it is sent to them — from the
        trip&apos;s Compliance tab — which emails them and puts it on their own travel page to
        confirm they have read it. Raising an alert here sends nothing by itself.
      </p>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit alert' : 'Raise a destination alert'}</DialogTitle>
            <DialogDescription>
              The travel desk sees it on every trip to this destination while it is in force.
              {editingId
                ? ' Once it has reached a trip it is not deleted — untick Active to stand it down.'
                : ' Raised active, it goes at once to the traveller of every approved or under-way trip to the destination in its dates (the city, when one is named); later trips are sent it from their Compliance tab.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="al-title">
                Title<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Input
                id="al-title"
                maxLength={200}
                value={form.title}
                onChange={(e) => set('title', e.target.value)}
                placeholder="Civil unrest in the capital"
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="al-type">Type</Label>
                <Select
                  value={form.alertType}
                  onValueChange={(v) => set('alertType', v as TravelAlertType)}
                >
                  <SelectTrigger id="al-type"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {ALERT_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>{spaced(t)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="al-severity">Severity</Label>
                <Select
                  value={form.severity}
                  onValueChange={(v) => set('severity', v as TravelAlertSeverity)}
                >
                  <SelectTrigger id="al-severity"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {SEVERITIES.map((sv) => (
                      <SelectItem key={sv} value={sv}>{sv}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="al-country">
                  Country<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Select value={form.countryId} onValueChange={(v) => set('countryId', v)}>
                  <SelectTrigger id="al-country">
                    <SelectValue placeholder="Pick a country" />
                  </SelectTrigger>
                  <SelectContent>
                    {(countries ?? []).map((c) => (
                      <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="al-city">City</Label>
                <Input
                  id="al-city"
                  maxLength={100}
                  value={form.city}
                  onChange={(e) => set('city', e.target.value)}
                  placeholder="Optional — the whole country if left blank"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="al-body">What travellers need to know</Label>
              <Textarea
                id="al-body"
                rows={4}
                value={form.body}
                onChange={(e) => set('body', e.target.value)}
                placeholder="What is happening, where, and what a traveller should do about it"
              />
              <p className="text-xs text-muted-foreground">
                This is the alert. A title and a severity with no text tells a traveller nothing
                they can act on.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="al-source">Source</Label>
              <Input
                id="al-source"
                maxLength={200}
                value={form.source}
                onChange={(e) => set('source', e.target.value)}
                placeholder="Who says so — an embassy, a security provider, the news"
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="al-from">
                  In force from<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input
                  id="al-from"
                  type="date"
                  value={form.effectiveFrom}
                  onChange={(e) => set('effectiveFrom', e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="al-to">Until</Label>
                <Input
                  id="al-to"
                  type="date"
                  value={form.effectiveTo}
                  onChange={(e) => set('effectiveTo', e.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  Leave blank while it is open-ended. An alert stops showing on trips once this
                  passes.
                </p>
              </div>
            </div>

            <div className="flex items-start gap-2 rounded-md border p-3">
              <Checkbox
                id="al-active"
                checked={form.isActive}
                onCheckedChange={(v) => set('isActive', v === true)}
              />
              <div className="space-y-1">
                <Label htmlFor="al-active" className="cursor-pointer">Active</Label>
                <p className="text-xs text-muted-foreground">
                  An inactive alert shows on no trip, whatever its dates say.
                </p>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={save.isPending}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate()} disabled={!valid || save.isPending}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editingId ? 'Save' : 'Raise it'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(o) => !o && setPendingDelete(null)}
        title="Remove this alert?"
        description={`"${pendingDelete?.title ?? ''}" will stop appearing on trips to that destination. Notifications already sent about it are not withdrawn.`}
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          if (pendingDelete) await remove.mutateAsync(pendingDelete);
        }}
      />
    </div>
  );
}
