'use client';

import { use, useMemo, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowUpRight, Ban, DoorClosed, DoorOpen, Link2, Loader2, Pencil, Trash2, UserMinus, UserPlus,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CurrencyPicker } from '@/components/hr/common/CurrencyPicker';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import {
  TRAVEL_PURPOSE_LABELS,
  TRAVEL_REQUEST_STATUS_LABELS,
  TRAVEL_RISK_LEVEL_LABELS,
  enumLabel,
  enumOptions,
} from '@/components/hr/travel/travel-enums';
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelService } from '@/services/hr/travel.service';
import type {
  StaffGroupTravel,
  StaffTravelPurpose,
  StaffTravelRequestSummary,
  TravelRiskLevel,
} from '@/types/hr/travel';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const toDateInput = (v?: string | null) => (v ? String(v).slice(0, 10) : '');

/** A trip holds no place on the group once it is cancelled or rejected. */
const HOLDS_A_PLACE = (r: StaffTravelRequestSummary) => r.status !== 'Cancelled' && r.status !== 'Rejected';
/** The two states in which a trip can still change — and so take the group's destination and dates. */
const EDITABLE = (r: StaffTravelRequestSummary) => r.status === 'Draft' || r.status === 'ReturnedForRevision';

/** Whether a traveller's trip says something different from the group about where and when (T-32). */
function differsFromGroup(r: StaffTravelRequestSummary, g: StaffGroupTravel) {
  return (
    toDateInput(r.travelStartDate) !== toDateInput(g.travelStartDate)
    || toDateInput(r.travelEndDate) !== toDateInput(g.travelEndDate)
    || (r.destinationCity ?? '').trim().toLowerCase() !== (g.destinationCity ?? '').trim().toLowerCase()
    || (!!r.destinationCountryName && !!g.destinationCountryName && r.destinationCountryName !== g.destinationCountryName)
  );
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

/**
 * One group trip and the people on it.
 *
 * ⚠ **Adding someone raises a real travel request for them** — the group's destination and dates,
 * plus what the dialog says about purpose, origin, cost, risk and visa. Anyone already holding a place
 * is skipped rather than duplicated. **Linking** puts an existing draft request on the group instead
 * (travel closure lane 1 — finding T-30: there was no way to), and gives it the group's destination
 * and dates.
 *
 * Since lane 1 (slice 1c) the group's **status moves by its own buttons** — open, close, cancel —
 * rather than an edit's dropdown that wrote whatever it was given (A11); **the limit binds** (T-31,
 * and a cancelled trip no longer holds a place); and **a new destination or new dates reach** every
 * traveller whose trip is still a draft or returned for revision. A submitted or approved trip keeps
 * its own, and the table marks it (T-32).
 *
 * ⚠ **Removing someone does NOT cancel their trip.** "No longer travelling as part of this group" is
 * not "not travelling". Deleting the whole group takes everyone off it first; their trips carry on.
 */
export default function TravelGroupDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { canWrite, canAdmin } = useTravelAccess();

  const { data: group, isLoading, isError, error } = useQuery({
    queryKey: ['travel-groups', id],
    queryFn: () => travelService.getGroupById(id),
  });

  const { data: countries } = useQuery({
    queryKey: ['countries'],
    queryFn: () => countryService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const [editing, setEditing] = useState(false);
  const [adding, setAdding] = useState(false);
  const [linking, setLinking] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const [removing, setRemoving] = useState<StaffTravelRequestSummary | null>(null);

  // Edit form — no status: the verbs move it.
  const [form, setForm] = useState({
    groupName: '', eventName: '', leadEmployeeId: '', leadLabel: null as string | null,
    destinationCountryId: '', destinationCity: '',
    travelStartDate: '', travelEndDate: '', maxParticipants: '',
  });

  // Add-a-traveller form
  const [pickedId, setPickedId] = useState('');
  const [pickedLabel, setPickedLabel] = useState<string | null>(null);
  const [originCountryId, setOriginCountryId] = useState('');
  const [originCity, setOriginCity] = useState('');
  const [currencyCode, setCurrencyCode] = useState('');
  const [estimatedTotalCost, setEstimatedTotalCost] = useState('0');
  const [purposeDescription, setPurposeDescription] = useState('');
  const [travelPurpose, setTravelPurpose] = useState<StaffTravelPurpose>('Conference');
  const [riskLevel, setRiskLevel] = useState<TravelRiskLevel>('Low');
  const [requiresVisa, setRequiresVisa] = useState<boolean | null>(null);
  const [requiresHealthClearance, setRequiresHealthClearance] = useState(false);

  // Link-an-existing-request form
  const [linkEmployeeId, setLinkEmployeeId] = useState('');
  const [linkEmployeeLabel, setLinkEmployeeLabel] = useState<string | null>(null);
  const [linkRequestId, setLinkRequestId] = useState('');

  const { data: linkCandidates, isLoading: linkLoading } = useQuery({
    queryKey: ['travel-requests', 'employee', linkEmployeeId],
    queryFn: () => travelService.getByEmployee(linkEmployeeId),
    enabled: linking && !!linkEmployeeId,
  });
  const linkable = useMemo(
    () => (linkCandidates ?? []).filter(EDITABLE).filter((r) => !(group?.requests ?? []).some((p) => p.id === r.id)),
    [linkCandidates, group],
  );

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e?.message || 'Please try again.', variant: 'destructive' });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['travel-groups', id] });
    await queryClient.invalidateQueries({ queryKey: ['travel-groups'] });
    await queryClient.invalidateQueries({ queryKey: ['travel-requests'] });
  };

  const openEdit = () => {
    if (!group) return;
    setForm({
      groupName: group.groupName,
      eventName: group.eventName ?? '',
      leadEmployeeId: group.leadEmployeeId,
      leadLabel: group.leadEmployeeName,
      destinationCountryId: group.destinationCountryId,
      destinationCity: group.destinationCity,
      travelStartDate: toDateInput(group.travelStartDate),
      travelEndDate: toDateInput(group.travelEndDate),
      maxParticipants: group.maxParticipants == null ? '' : String(group.maxParticipants),
    });
    setEditing(true);
  };

  const save = useMutation({
    mutationFn: () =>
      travelService.updateGroup({
        id,
        groupName: form.groupName.trim(),
        eventName: form.eventName.trim() || null,
        leadEmployeeId: form.leadEmployeeId,
        destinationCountryId: form.destinationCountryId,
        destinationCity: form.destinationCity.trim(),
        travelStartDate: form.travelStartDate,
        travelEndDate: form.travelEndDate,
        maxParticipants: form.maxParticipants === '' ? null : Number(form.maxParticipants),
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Group updated', description: 'Draft and returned trips now carry its destination and dates.' });
      setEditing(false);
    },
    onError: fail('Could not save the group'),
  });

  const verb = (fn: () => Promise<unknown>, done: string, failed: string) =>
    ({
      mutationFn: fn,
      onSuccess: async () => {
        await refresh();
        toast({ title: done });
      },
      onError: fail(failed),
    });
  const openGroup = useMutation(verb(() => travelService.openGroup(id), 'Open to travellers', 'Could not open the group'));
  const closeGroup = useMutation(verb(() => travelService.closeGroup(id), 'Closed to new travellers', 'Could not close the group'));
  const cancelGroup = useMutation({
    ...verb(() => travelService.cancelGroup(id), 'Group trip cancelled', 'Could not cancel the group'),
    onSettled: () => setConfirmingCancel(false),
  });

  const international = !!originCountryId && !!group && originCountryId !== group.destinationCountryId;
  const visaRequired = requiresVisa ?? international;

  const resetAdd = () => {
    setAdding(false);
    setPickedId('');
    setPickedLabel(null);
    setRequiresVisa(null);
  };

  const addParticipant = useMutation({
    mutationFn: () =>
      travelService.addGroupParticipants(id, [pickedId], {
        initiatedByRole: 'TravelDesk',
        travelType: international ? 'International' : 'Domestic',
        travelPurpose,
        purposeDescription: purposeDescription.trim() || group?.eventName || group?.groupName,
        priority: 'Routine',
        originCountryId,
        originCity: originCity.trim(),
        estimatedTotalCost: Number(estimatedTotalCost) || 0,
        currencyCode,
        // No unit and no international flag: each participant's own unit, and the two countries,
        // decide them on the server (lane 1).
        requiresVisa: visaRequired,
        requiresHealthClearance,
        riskLevel,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Traveller added', description: 'A travel request has been raised for them, in draft.' });
      resetAdd();
    },
    onError: fail('Could not add the traveller'),
  });

  const linkRequest = useMutation({
    mutationFn: () => travelService.linkGroupRequest(id, linkRequestId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Request linked', description: "It now carries the group's destination and dates." });
      setLinking(false);
      setLinkEmployeeId('');
      setLinkEmployeeLabel(null);
      setLinkRequestId('');
    },
    onError: fail('Could not link the request'),
  });

  const removeParticipant = useMutation({
    mutationFn: (r: StaffTravelRequestSummary) => travelService.removeGroupParticipant(id, r.id),
    onSuccess: async () => {
      await refresh();
      toast({
        title: 'Taken off the group',
        description: 'Their travel request stands — it is simply no longer part of this group.',
      });
      setRemoving(null);
    },
    onError: fail('Could not remove the traveller'),
  });

  const removeGroup = useMutation({
    mutationFn: () => travelService.deleteGroup(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['travel-groups'] });
      toast({ title: 'Group deleted', description: 'Its travellers carry on with their own trips.' });
      router.push('/hr/travel/groups');
    },
    onError: fail('Could not delete the group'),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (isError && !group) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="this group trip" />
      </div>
    );
  }
  if (!group) return null;

  const participants = group.requests ?? [];
  const placesTaken = group.currentParticipantCount;
  const full = group.maxParticipants != null && placesTaken >= group.maxParticipants;
  const accepting = group.status === 'Planning' || group.status === 'Open';
  const finished = group.status === 'Cancelled' || group.status === 'Completed';
  const addBlockedReason = !accepting
    ? `The group is ${spaced(group.status).toLowerCase()} and takes no new travellers.`
    : full ? 'The group is at its maximum size.' : undefined;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={group.groupName}
        description={group.eventName ?? 'A group trip.'}
        backHref="/hr/travel/groups"
        actions={
          <div className="flex flex-wrap gap-2">
            {canWrite && group.status === 'Planning' && (
              <Button variant="outline" onClick={() => openGroup.mutate()} disabled={openGroup.isPending}>
                <DoorOpen className="mr-2 h-4 w-4" /> Open to travellers
              </Button>
            )}
            {canWrite && group.status === 'Closed' && (
              <Button variant="outline" onClick={() => openGroup.mutate()} disabled={openGroup.isPending}>
                <DoorOpen className="mr-2 h-4 w-4" /> Reopen
              </Button>
            )}
            {canWrite && accepting && (
              <Button variant="outline" onClick={() => closeGroup.mutate()} disabled={closeGroup.isPending}>
                <DoorClosed className="mr-2 h-4 w-4" /> Close to new travellers
              </Button>
            )}
            {canWrite && !finished && (
              <Button variant="outline" onClick={openEdit}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}
            {canWrite && !finished && (
              <Button variant="outline" onClick={() => setConfirmingCancel(true)}>
                <Ban className="mr-2 h-4 w-4" /> Cancel group
              </Button>
            )}
            {canAdmin && (
              <Button variant="outline" onClick={() => setConfirmingDelete(true)}>
                <Trash2 className="mr-2 h-4 w-4" /> Delete
              </Button>
            )}
          </div>
        }
      />

      <Card>
        <CardContent className="p-4">
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Detail label="Status">{spaced(group.statusName || group.status)}</Detail>
            <Detail label="Lead">{group.leadEmployeeName || '—'}</Detail>
            <Detail label="Destination">
              {group.destinationCity}
              {group.destinationCountryName && `, ${group.destinationCountryName}`}
            </Detail>
            <Detail label="Dates">
              {fmtDate(group.travelStartDate)} – {fmtDate(group.travelEndDate)}
            </Detail>
            <Detail label="Places taken">
              {placesTaken}
              {group.maxParticipants ? ` of ${group.maxParticipants}` : ''}
              <span className="block text-xs text-muted-foreground">A cancelled or rejected trip holds no place.</span>
            </Detail>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">Travellers</CardTitle>
          {canWrite && (
            <div className="flex flex-wrap gap-2">
              <Button size="sm" variant="outline" onClick={() => setLinking(true)} disabled={!!addBlockedReason} title={addBlockedReason}>
                <Link2 className="mr-2 h-4 w-4" /> Link an existing request
              </Button>
              <Button size="sm" onClick={() => setAdding(true)} disabled={!!addBlockedReason} title={addBlockedReason}>
                <UserPlus className="mr-2 h-4 w-4" /> Add a traveller
              </Button>
            </div>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {participants.length === 0 ? (
            <div className="px-6 pb-6">
              <EmptyState
                title="Nobody on this trip yet"
                description="Adding a traveller raises a travel request for them; linking puts an existing draft on the group. Either way it carries this group's destination and dates."
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Traveller</TableHead>
                  <TableHead>Request</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Where and when</TableHead>
                  {canAdmin && <TableHead className="w-24 text-right">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {participants.map((r) => (
                  <TableRow key={r.id} className={HOLDS_A_PLACE(r) ? undefined : 'text-muted-foreground'}>
                    <TableCell className="font-medium">{r.employeeName}</TableCell>
                    <TableCell>
                      {/* The request belongs to the travel screens - link out, do not re-render it. */}
                      <Link href={`/hr/travel/${r.id}`} className="hover:underline">
                        {r.requestNumber}
                        <ArrowUpRight className="ml-1 inline h-3 w-3" />
                      </Link>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status)} />
                    </TableCell>
                    <TableCell>
                      {differsFromGroup(r, group) ? (
                        <Badge variant="outline" title={`${r.destinationCity}, ${fmtDate(r.travelStartDate)} – ${fmtDate(r.travelEndDate)}`}>
                          Differs from the group
                        </Badge>
                      ) : (
                        <span className="text-sm text-muted-foreground">As the group</span>
                      )}
                    </TableCell>
                    {canAdmin && (
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => setRemoving(r)}>
                          <UserMinus className="mr-1 h-3.5 w-3.5" />
                          Remove
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* ── Edit ──────────────────────────────────────────────────────────── */}
      <Dialog open={editing} onOpenChange={setEditing}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit group trip</DialogTitle>
            <DialogDescription>
              A new destination or new dates are given to every traveller whose trip is still a draft or
              returned for revision. Submitted and approved trips keep their own — the list marks them.
              The group&apos;s status is changed with the buttons on the page.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="ge-name">Group name</Label>
                <Input
                  id="ge-name"
                  value={form.groupName}
                  onChange={(e) => setForm((f) => ({ ...f, groupName: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="ge-event">Event</Label>
                <Input
                  id="ge-event"
                  value={form.eventName}
                  onChange={(e) => setForm((f) => ({ ...f, eventName: e.target.value }))}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label>Lead traveller</Label>
              <EmployeePicker
                value={form.leadEmployeeId || null}
                initialLabel={form.leadLabel}
                onChange={(empId, label) =>
                  setForm((f) => ({ ...f, leadEmployeeId: empId ?? '', leadLabel: label }))
                }
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="ge-country">Destination country</Label>
                <Select
                  value={form.destinationCountryId}
                  onValueChange={(v) => { if (v) setForm((f) => ({ ...f, destinationCountryId: v })); }}
                >
                  <SelectTrigger id="ge-country"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(countries ?? []).map((c) => (
                      <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="ge-city">Destination city</Label>
                <Input
                  id="ge-city"
                  value={form.destinationCity}
                  onChange={(e) => setForm((f) => ({ ...f, destinationCity: e.target.value }))}
                />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="ge-start">From</Label>
                <Input
                  id="ge-start"
                  type="date"
                  value={form.travelStartDate}
                  onChange={(e) => setForm((f) => ({ ...f, travelStartDate: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="ge-end">To</Label>
                <Input
                  id="ge-end"
                  type="date"
                  value={form.travelEndDate}
                  onChange={(e) => setForm((f) => ({ ...f, travelEndDate: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="ge-max">Max travellers</Label>
                <Input
                  id="ge-max"
                  type="number"
                  min={1}
                  value={form.maxParticipants}
                  onChange={(e) => setForm((f) => ({ ...f, maxParticipants: e.target.value }))}
                />
                <p className="text-xs text-muted-foreground">Not below the {placesTaken} place(s) already taken.</p>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(false)} disabled={save.isPending}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Add a traveller ───────────────────────────────────────────────── */}
      <Dialog open={adding} onOpenChange={(o) => (o ? setAdding(true) : resetAdd())}>
        <DialogContent className="max-h-[85vh] max-w-xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add a traveller</DialogTitle>
            <DialogDescription>
              This raises a draft travel request for them, carrying the group&apos;s destination and
              dates and what you choose below. Someone already on the group is skipped rather than added
              twice.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label>
                Traveller<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <EmployeePicker
                value={pickedId || null}
                initialLabel={pickedLabel}
                onChange={(empId, label) => { setPickedId(empId ?? ''); setPickedLabel(label); }}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="gp-origin-country">
                  Travelling from<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Select value={originCountryId} onValueChange={(v) => { if (v) setOriginCountryId(v); }}>
                  <SelectTrigger id="gp-origin-country">
                    <SelectValue placeholder="Country" />
                  </SelectTrigger>
                  <SelectContent>
                    {(countries ?? []).map((c) => (
                      <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Whether the trip counts as international is decided from this and the destination.
                </p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="gp-origin-city">
                  From city<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input id="gp-origin-city" value={originCity} onChange={(e) => setOriginCity(e.target.value)} />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="gp-purpose-type">Purpose</Label>
                <Select value={travelPurpose} onValueChange={(v) => { if (v) setTravelPurpose(v as StaffTravelPurpose); }}>
                  <SelectTrigger id="gp-purpose-type"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {enumOptions(TRAVEL_PURPOSE_LABELS).map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="gp-risk">Risk level</Label>
                <Select value={riskLevel} onValueChange={(v) => { if (v) setRiskLevel(v as TravelRiskLevel); }}>
                  <SelectTrigger id="gp-risk"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {enumOptions(TRAVEL_RISK_LEVEL_LABELS).map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="gp-purpose">Justification</Label>
              <Input
                id="gp-purpose"
                value={purposeDescription}
                onChange={(e) => setPurposeDescription(e.target.value)}
                placeholder={group.eventName ?? group.groupName}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="gp-cost">Estimated cost</Label>
                <Input
                  id="gp-cost"
                  type="number"
                  min={0}
                  step="0.01"
                  value={estimatedTotalCost}
                  onChange={(e) => setEstimatedTotalCost(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="gp-currency">
                  Currency<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <CurrencyPicker id="gp-currency" value={currencyCode} onChange={setCurrencyCode} />
              </div>
            </div>

            <div className="flex flex-wrap gap-6">
              <div className="flex items-center gap-2">
                <Switch id="gp-visa" checked={visaRequired} onCheckedChange={setRequiresVisa} />
                <Label htmlFor="gp-visa" className="font-normal">
                  Requires a visa{international && requiresVisa === null ? ' (an international trip)' : ''}
                </Label>
              </div>
              <div className="flex items-center gap-2">
                <Switch id="gp-health" checked={requiresHealthClearance} onCheckedChange={setRequiresHealthClearance} />
                <Label htmlFor="gp-health" className="font-normal">Requires health clearance</Label>
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={resetAdd} disabled={addParticipant.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => addParticipant.mutate()}
              disabled={!pickedId || !originCountryId || !originCity.trim() || !currencyCode || addParticipant.isPending}
            >
              {addParticipant.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Link an existing request (T-30) ───────────────────────────────── */}
      <Dialog open={linking} onOpenChange={setLinking}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Link an existing request</DialogTitle>
            <DialogDescription>
              Puts a traveller&apos;s draft (or returned) request on this group. It takes the group&apos;s
              destination and dates. A submitted or approved trip cannot join — recall it first.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Traveller</Label>
              <EmployeePicker
                value={linkEmployeeId || null}
                initialLabel={linkEmployeeLabel}
                onChange={(empId, label) => { setLinkEmployeeId(empId ?? ''); setLinkEmployeeLabel(label); setLinkRequestId(''); }}
              />
            </div>
            {linkEmployeeId && (
              <div className="space-y-2">
                <Label htmlFor="gl-request">Request</Label>
                {linkLoading ? (
                  <p className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Reading their requests…
                  </p>
                ) : linkable.length === 0 ? (
                  <p className="text-sm text-muted-foreground">They have no draft or returned request to link.</p>
                ) : (
                  <Select value={linkRequestId} onValueChange={(v) => { if (v) setLinkRequestId(v); }}>
                    <SelectTrigger id="gl-request"><SelectValue placeholder="Choose a request" /></SelectTrigger>
                    <SelectContent>
                      {linkable.map((r) => (
                        <SelectItem key={r.id} value={r.id}>
                          {r.requestNumber} — {r.destinationCity}, {fmtDate(r.travelStartDate)} ({enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status)})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLinking(false)} disabled={linkRequest.isPending}>
              Cancel
            </Button>
            <Button onClick={() => linkRequest.mutate()} disabled={!linkRequestId || linkRequest.isPending}>
              {linkRequest.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Link to the group
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={removing !== null}
        onOpenChange={(o) => !o && setRemoving(null)}
        title="Take them off this group?"
        description={`${removing?.employeeName ?? 'This traveller'} comes off the group. Their travel request ${removing?.requestNumber ?? ''} is NOT cancelled - it carries on as an ordinary trip. Cancel it separately if they are not going.`}
        confirmText="Remove from group"
        variant="destructive"
        isLoading={removeParticipant.isPending}
        onConfirm={async () => {
          if (removing) await removeParticipant.mutateAsync(removing);
        }}
      />

      <ConfirmationDialog
        open={confirmingCancel}
        onOpenChange={setConfirmingCancel}
        title="Cancel this group trip?"
        description={`"${group.groupName}" is called off. It can be cancelled only once none of its travellers has a trip still going ahead — cancel those trips, or take the travellers off the group, first.`}
        confirmText="Cancel group trip"
        variant="destructive"
        isLoading={cancelGroup.isPending}
        onConfirm={async () => {
          await cancelGroup.mutateAsync();
        }}
      />

      <ConfirmationDialog
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title="Delete this group trip?"
        description={`"${group.groupName}" will be removed. Its ${participants.length} traveller(s) come off it first; their travel requests are NOT cancelled - they carry on individually.`}
        confirmText="Delete"
        variant="destructive"
        isLoading={removeGroup.isPending}
        onConfirm={async () => {
          await removeGroup.mutateAsync();
        }}
      />
    </div>
  );
}
