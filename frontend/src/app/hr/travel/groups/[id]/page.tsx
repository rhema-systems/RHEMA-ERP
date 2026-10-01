'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowUpRight, Loader2, Pencil, Trash2, UserMinus, UserPlus } from 'lucide-react';
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
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelService } from '@/services/hr/travel.service';
import type { GroupTravelStatus, StaffTravelRequestSummary } from '@/types/hr/travel';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const toDateInput = (v?: string | null) => (v ? String(v).slice(0, 10) : '');

const STATUSES: GroupTravelStatus[] =
  ['Planning', 'Open', 'Closed', 'InProgress', 'Completed', 'Cancelled'];

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
 * plus whatever the template below says about purpose, origin and cost. Anyone already on the
 * group is skipped rather than duplicated.
 *
 * ⚠ **Removing someone does NOT cancel their trip.** The server sets their request's group id to
 * null and leaves the request standing, which is right — "no longer travelling as part of this
 * group" is not "not travelling" — but it is surprising enough that the confirmation says so.
 * Deleting the whole group behaves the same way for every participant.
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

  // ⚠ The currency list is read through `api/hr/currencies` (inside CurrencyPicker). This page read
  // `api/finance/currencies`, which answers 403 without a Finance permission (finding O-19).

  const [editing, setEditing] = useState(false);
  const [adding, setAdding] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [removing, setRemoving] = useState<StaffTravelRequestSummary | null>(null);

  // Edit form
  const [form, setForm] = useState({
    groupName: '', eventName: '', leadEmployeeId: '', leadLabel: null as string | null,
    destinationCountryId: '', destinationCity: '',
    travelStartDate: '', travelEndDate: '', maxParticipants: '',
    status: 'Planning' as GroupTravelStatus,
  });

  // Add-participants form
  const [pickedId, setPickedId] = useState('');
  const [pickedLabel, setPickedLabel] = useState<string | null>(null);
  const [originCountryId, setOriginCountryId] = useState('');
  const [originCity, setOriginCity] = useState('');
  const [currencyCode, setCurrencyCode] = useState('');
  const [estimatedTotalCost, setEstimatedTotalCost] = useState('0');
  const [purposeDescription, setPurposeDescription] = useState('');

  const fail = (title: string) => (e: Error) =>
    toast({
      title,
      description: e?.message || 'Please try again.',
      variant: 'destructive',
    });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['travel-groups', id] });
    await queryClient.invalidateQueries({ queryKey: ['travel-groups'] });
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
      status: group.status,
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
        // Sent explicitly: it is on the update payload and not the create, so omitting it would
        // send the enum default and move the trip back to Planning.
        status: form.status,
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Group updated' });
      setEditing(false);
    },
    onError: fail('Could not save the group'),
  });

  const addParticipant = useMutation({
    mutationFn: () =>
      travelService.addGroupParticipants(id, [pickedId], {
        initiatedByRole: 'TravelDesk',
        travelType: group?.destinationCountryId === originCountryId ? 'Domestic' : 'International',
        travelPurpose: 'Conference',
        purposeDescription: purposeDescription.trim() || group?.eventName || group?.groupName,
        priority: 'Routine',
        originCountryId,
        originCity: originCity.trim(),
        estimatedTotalCost: Number(estimatedTotalCost) || 0,
        currencyCode,
        isInternational: group?.destinationCountryId !== originCountryId,
        requiresVisa: false,
        requiresHealthClearance: false,
        riskLevel: 'Low',
      }),
    onSuccess: async () => {
      await refresh();
      toast({
        title: 'Traveller added',
        description: 'A travel request has been raised for them, in draft.',
      });
      setAdding(false);
      setPickedId('');
      setPickedLabel(null);
    },
    onError: fail('Could not add the traveller'),
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
      toast({ title: 'Group deleted' });
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
  const full =
    group.maxParticipants != null && participants.length >= group.maxParticipants;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={group.groupName}
        description={group.eventName ?? 'A group trip.'}
        backHref="/hr/travel/groups"
        actions={
          <div className="flex flex-wrap gap-2">
            {canWrite && (
              <Button variant="outline" onClick={openEdit}>
                <Pencil className="mr-2 h-4 w-4" />
                Edit
              </Button>
            )}
            {canAdmin && (
              <Button variant="outline" onClick={() => setConfirmingDelete(true)}>
                <Trash2 className="mr-2 h-4 w-4" />
                Delete
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
            <Detail label="Travellers">
              {group.currentParticipantCount}
              {group.maxParticipants ? ` of ${group.maxParticipants}` : ''}
            </Detail>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <CardTitle className="text-base">Travellers</CardTitle>
          {canWrite && (
            <Button
              size="sm"
              onClick={() => setAdding(true)}
              disabled={full}
              title={full ? 'The group is at its maximum size.' : undefined}
            >
              <UserPlus className="mr-2 h-4 w-4" />
              Add a traveller
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {participants.length === 0 ? (
            <div className="px-6 pb-6">
              <EmptyState
                title="Nobody on this trip yet"
                description="Adding a traveller raises a travel request for them, carrying this group's destination and dates."
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Traveller</TableHead>
                  <TableHead>Request</TableHead>
                  <TableHead>Status</TableHead>
                  {canAdmin && <TableHead className="w-24 text-right">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {participants.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-medium">{r.employeeName}</TableCell>
                    <TableCell>
                      {/* The request belongs to the travel screens - link out, do not re-render it. */}
                      <Link href={`/hr/travel/${r.id}`} className="hover:underline">
                        {r.requestNumber}
                        <ArrowUpRight className="ml-1 inline h-3 w-3" />
                      </Link>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={r.status} />
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
              Changing the destination or dates here does not rewrite the travel requests already
              raised for the people on it.
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
                  onValueChange={(v) => setForm((f) => ({ ...f, destinationCountryId: v }))}
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
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ge-status">Status</Label>
              <Select
                value={form.status}
                onValueChange={(v) => setForm((f) => ({ ...f, status: v as GroupTravelStatus }))}
              >
                <SelectTrigger id="ge-status"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {STATUSES.map((st) => (
                    <SelectItem key={st} value={st}>{spaced(st)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Open while travellers are still being added; Closed once the group is settled.
              </p>
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
      <Dialog open={adding} onOpenChange={setAdding}>
        <DialogContent className="max-h-[85vh] max-w-xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add a traveller</DialogTitle>
            <DialogDescription>
              This raises a draft travel request for them, carrying the group&apos;s destination and
              dates. Someone already on the group is skipped rather than added twice. The draft is
              raised with purpose Conference, risk Low and no visa or health flag — open it from
              the list to correct those before it is submitted.
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
                <Select value={originCountryId} onValueChange={setOriginCountryId}>
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
                  Whether the trip counts as international is derived from this and the
                  destination.
                </p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="gp-origin-city">
                  From city<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input
                  id="gp-origin-city"
                  value={originCity}
                  onChange={(e) => setOriginCity(e.target.value)}
                />
              </div>
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

            <div className="space-y-2">
              <Label htmlFor="gp-purpose">Purpose</Label>
              <Input
                id="gp-purpose"
                value={purposeDescription}
                onChange={(e) => setPurposeDescription(e.target.value)}
                placeholder={group.eventName ?? group.groupName}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(false)} disabled={addParticipant.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => addParticipant.mutate()}
              disabled={
                !pickedId || !originCountryId || !originCity.trim() || !currencyCode
                || addParticipant.isPending
              }
            >
              {addParticipant.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add
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
        open={confirmingDelete}
        onOpenChange={setConfirmingDelete}
        title="Delete this group trip?"
        description={`"${group.groupName}" will be removed. The ${participants.length} travel request(s) raised for its travellers are NOT cancelled - they carry on individually.`}
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
