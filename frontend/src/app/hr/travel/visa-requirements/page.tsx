'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ExternalLink, Globe2, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { countryService } from '@/services/hr/country.service';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';
import {
  VISA_REQUIREMENT_TYPES,
  VISA_REQUIREMENT_TYPE_LABELS,
} from '@/types/hr/travel-compliance';
import type {
  StaffTravelVisaRequirement,
  VisaRequirementType,
} from '@/types/hr/travel-compliance';

/**
 * What a passport needs to enter a destination — the reference table the travel desk answers from.
 *
 * ⚠ **Built 2026-09-01 (lane 5b). It had a client and no screen**, so eleven fields on its two
 * DTOs were unreachable and its TypeScript interface had drifted into fiction — six of nine
 * declared fields existed on neither the entity nor either DTO. Instrument 01 counted the family
 * wired throughout, because it matches the frontend SERVICE layer and a client method with no
 * screen caller looks the same as one with ten.
 *
 * ⚠ **The API has no list-everything route**, and the screen is shaped around that rather than
 * pretending otherwise: `GET visa-requirements` takes both country ids and answers about ONE pair,
 * and the only real list is by destination. So the register is read a destination at a time, which
 * is also how the travel desk asks the question.
 */

const blank = () => ({
  passportCountryId: '',
  visaRequirementType: 'EmbassyVisa' as VisaRequirementType,
  visaCategory: '',
  maxStayDays: '',
  processingDays: '',
  officialSourceUrl: '',
  lastVerifiedAt: '',
  notes: '',
});

/** Types that describe a way IN. Prohibited is the one that does not. */
const badgeVariant = (t: VisaRequirementType) =>
  t === 'Prohibited' ? 'destructive' : t === 'VisaFree' ? 'secondary' : 'outline';

const num = (v: string) => (v.trim() === '' ? null : Number(v));

export default function VisaRequirementsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  const canWrite = hasAnyPermission(['HR.Travel.Write', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES);
  // ⚠ DELETE is gated on TravelAdminPolicy while POST and PUT take TravelWritePolicy — a tier
  // apart, exactly as the shortlisting criteria are. Offering Remove to a writer produces a 403
  // and a toast that explains nothing.
  const canDelete = hasAnyPermission(['HR.Travel.Admin']);

  const [destinationId, setDestinationId] = useState<string>('');
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<StaffTravelVisaRequirement | null>(null);
  const [form, setForm] = useState(blank);

  const countries = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const requirements = useQuery({
    queryKey: ['hr', 'visa-requirements', destinationId],
    queryFn: () => travelComplianceService.getVisaRequirementsForDestination(destinationId),
    enabled: !!destinationId,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'visa-requirements', destinationId] });

  const countryList = countries.data ?? [];
  const destination = countryList.find((c) => c.id === destinationId);
  const rows = requirements.data ?? [];

  // A passport already covered for this destination cannot be added twice — the server refuses it,
  // and there is no reason to offer the option and then explain the refusal.
  const taken = useMemo(
    () => new Set(rows.filter((r) => r.id !== editing?.id).map((r) => r.passportCountryId)),
    [rows, editing],
  );

  const save = useMutation({
    mutationFn: () =>
      editing
        ? travelComplianceService.updateVisaRequirement({
            id: editing.id,
            visaRequirementType: form.visaRequirementType,
            visaCategory: form.visaCategory.trim() || null,
            maxStayDays: num(form.maxStayDays),
            processingDays: num(form.processingDays),
            officialSourceUrl: form.officialSourceUrl.trim() || null,
            lastVerifiedAt: form.lastVerifiedAt || null,
            notes: form.notes.trim() || null,
          })
        : travelComplianceService.createVisaRequirement({
            passportCountryId: form.passportCountryId,
            destinationCountryId: destinationId,
            visaRequirementType: form.visaRequirementType,
            visaCategory: form.visaCategory.trim() || null,
            maxStayDays: num(form.maxStayDays),
            processingDays: num(form.processingDays),
            officialSourceUrl: form.officialSourceUrl.trim() || null,
            lastVerifiedAt: form.lastVerifiedAt || null,
            notes: form.notes.trim() || null,
          }),
    onSuccess: async () => {
      // ⚠ Refetch rather than rendering the write response: create and update map an entity whose
      // country navigations are not loaded, so both names come back null.
      await refresh();
      setOpen(false);
      setEditing(null);
      setForm(blank());
      toast({ title: editing ? 'Requirement updated' : 'Requirement added' });
    },
    onError: (e: any) =>
      toast({
        title: editing ? 'Could not update it' : 'Could not add it',
        description: e?.message,
        variant: 'destructive',
      }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => travelComplianceService.deleteVisaRequirement(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Requirement removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  const startAdd = () => {
    setEditing(null);
    setForm(blank());
    setOpen(true);
  };

  const startEdit = (r: StaffTravelVisaRequirement) => {
    setEditing(r);
    setForm({
      passportCountryId: r.passportCountryId,
      visaRequirementType: r.visaRequirementType,
      visaCategory: r.visaCategory ?? '',
      maxStayDays: r.maxStayDays?.toString() ?? '',
      processingDays: r.processingDays?.toString() ?? '',
      officialSourceUrl: r.officialSourceUrl ?? '',
      lastVerifiedAt: r.lastVerifiedAt?.slice(0, 10) ?? '',
      notes: r.notes ?? '',
    });
    setOpen(true);
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Visa requirements"
        description="What each passport needs to enter a destination. The travel desk answers from this, so an entry that is wrong is worse than one that is missing."
        backHref="/hr/travel"
        actions={
          canWrite && destinationId ? (
            <Button size="sm" onClick={startAdd}>
              <Plus className="mr-2 h-4 w-4" /> Add a passport
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Destination</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <Select value={destinationId} onValueChange={setDestinationId}>
            <SelectTrigger className="max-w-md">
              <SelectValue placeholder={countries.isLoading ? 'Loading countries…' : 'Choose a destination country'} />
            </SelectTrigger>
            <SelectContent>
              {countryList.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-xs text-muted-foreground">
            The register is read one destination at a time — the API answers by destination, and so
            does the travel desk.
          </p>
        </CardContent>
      </Card>

      {!destinationId ? (
        <Card>
          <CardContent className="py-10">
            <EmptyState
              icon={Globe2}
              title="Choose a destination"
              description="Pick the country being travelled to, and every passport recorded against it appears here."
            />
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">
              Travelling to {destination?.name ?? '—'}
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {requirements.isLoading ? (
              <div className="flex items-center justify-center py-10">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : requirements.isError && !requirements.data ? (
              <div className="p-4">
                <TravelQueryError error={requirements.error} what="the visa requirements" />
              </div>
            ) : rows.length === 0 ? (
              <div className="py-8">
                <EmptyState
                  icon={Globe2}
                  title="Nothing recorded for this destination"
                  description="No passport has a requirement here yet. Until one is added, the travel desk has no answer to give."
                />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Passport</TableHead>
                    <TableHead>Requirement</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead className="text-right">Max stay</TableHead>
                    <TableHead className="text-right">Processing</TableHead>
                    <TableHead>Last checked</TableHead>
                    {canWrite && <TableHead className="w-20" />}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell className="font-medium">
                        {r.passportCountryName ?? '—'}
                        {r.notes && (
                          <span className="block text-sm font-normal text-muted-foreground">
                            {r.notes}
                          </span>
                        )}
                      </TableCell>
                      <TableCell>
                        <Badge variant={badgeVariant(r.visaRequirementType)}>
                          {VISA_REQUIREMENT_TYPE_LABELS[r.visaRequirementType] ?? r.visaRequirementTypeName}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {r.visaCategory || '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.maxStayDays != null ? `${r.maxStayDays} days` : '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {r.processingDays != null ? `${r.processingDays} days` : '—'}
                      </TableCell>
                      <TableCell className="text-sm">
                        {r.lastVerifiedAt ? (
                          <span className="text-muted-foreground">{r.lastVerifiedAt.slice(0, 10)}</span>
                        ) : (
                          <span className="text-amber-600">Never</span>
                        )}
                        {r.officialSourceUrl && (
                          <a
                            href={r.officialSourceUrl}
                            target="_blank"
                            rel="noreferrer"
                            className="ml-2 inline-flex items-center text-primary hover:underline"
                          >
                            source <ExternalLink className="ml-0.5 h-3 w-3" />
                          </a>
                        )}
                      </TableCell>
                      {canWrite && (
                        <TableCell className="whitespace-nowrap">
                          <Button variant="ghost" size="icon" onClick={() => startEdit(r)}>
                            <Pencil className="h-4 w-4" />
                          </Button>
                          {canDelete && (
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => remove.mutate(r.id)}
                              disabled={remove.isPending}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) setEditing(null); }}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {editing
                ? `Edit — ${editing.passportCountryName ?? 'passport'} to ${destination?.name ?? ''}`
                : `Add a passport for ${destination?.name ?? ''}`}
            </DialogTitle>
            <DialogDescription>
              What a holder of this passport needs in order to enter {destination?.name ?? 'the destination'}.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2">
            {editing ? (
              // ⚠ The update DTO carries no country fields — a requirement cannot be moved onto
              // another pair. Showing the passport as a fixed label rather than a disabled picker
              // is the honest rendering of that.
              <Alert>
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  The country pair cannot be changed. To correct it, remove this entry and add the
                  right one.
                </AlertDescription>
              </Alert>
            ) : (
              <div className="space-y-1.5">
                <Label htmlFor="passportCountryId">Passport country</Label>
                <Select
                  value={form.passportCountryId}
                  onValueChange={(v) => setForm({ ...form, passportCountryId: v })}
                >
                  <SelectTrigger id="passportCountryId">
                    <SelectValue placeholder="Choose the passport held" />
                  </SelectTrigger>
                  <SelectContent>
                    {countryList
                      // Already recorded for this destination — the server refuses a second, so
                      // do not offer it and then explain the refusal.
                      .filter((c) => !taken.has(c.id))
                      .map((c) => (
                        <SelectItem key={c.id} value={c.id}>
                          {c.name}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="visaRequirementType">What is required</Label>
              <Select
                value={form.visaRequirementType}
                onValueChange={(v) => setForm({ ...form, visaRequirementType: v as VisaRequirementType })}
              >
                <SelectTrigger id="visaRequirementType">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {VISA_REQUIREMENT_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {VISA_REQUIREMENT_TYPE_LABELS[t]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="visaCategory">Category</Label>
              <Input
                id="visaCategory"
                value={form.visaCategory}
                onChange={(e) => setForm({ ...form, visaCategory: e.target.value })}
                placeholder="e.g. Standard visitor"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="maxStayDays">Maximum stay (days)</Label>
                <Input
                  id="maxStayDays"
                  type="number"
                  min={0}
                  value={form.maxStayDays}
                  onChange={(e) => setForm({ ...form, maxStayDays: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="processingDays">Processing (days)</Label>
                <Input
                  id="processingDays"
                  type="number"
                  min={0}
                  value={form.processingDays}
                  onChange={(e) => setForm({ ...form, processingDays: e.target.value })}
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="officialSourceUrl">Official source</Label>
              <Input
                id="officialSourceUrl"
                value={form.officialSourceUrl}
                onChange={(e) => setForm({ ...form, officialSourceUrl: e.target.value })}
                placeholder="https://…"
              />
              <p className="text-xs text-muted-foreground">
                Where this was read from. Visa rules change without notice, so the next person to
                check needs to know what you checked.
              </p>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="lastVerifiedAt">Last checked against that source</Label>
              <Input
                id="lastVerifiedAt"
                type="date"
                value={form.lastVerifiedAt}
                onChange={(e) => setForm({ ...form, lastVerifiedAt: e.target.value })}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="notes">Notes</Label>
              <Textarea
                id="notes"
                rows={2}
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => save.mutate()}
              disabled={save.isPending || (!editing && !form.passportCountryId)}
            >
              {save.isPending ? 'Saving…' : editing ? 'Save' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
