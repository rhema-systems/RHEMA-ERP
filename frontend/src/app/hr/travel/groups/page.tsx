'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Users2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelService } from '@/services/hr/travel.service';
import type { GroupTravelStatus } from '@/types/hr/travel';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

const STATUS_TONE: Record<GroupTravelStatus, string> = {
  Planning: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200',
  Open: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  Closed: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  InProgress: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  Completed: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200',
  Cancelled: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

/**
 * Group trips — several people travelling to one place for one thing.
 *
 * ⚠ **This subsystem had no screen of any kind.** Not "could not be edited": it could not be
 * created, listed or opened either. `getGroups`, `getGroupById`, `createGroup` and
 * `addGroupParticipants` have been in the client throughout with nothing calling them, which is
 * why the coverage instrument counted them wired — it matches the service layer, not screens.
 *
 * ⚠ **Adding someone raises a real travel request for them**, carrying the group's destination and
 * dates. That is the whole point of a group trip, and it is also why removing someone does not
 * cancel their trip — see the detail page.
 */
export default function TravelGroupsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  const canWrite =
    hasAnyPermission(['HR.Travel.Write', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES);

  const { data: groups, isLoading } = useQuery({
    queryKey: ['travel-groups'],
    queryFn: () => travelService.getGroups(),
  });

  const { data: countries } = useQuery({
    queryKey: ['countries'],
    queryFn: () => countryService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  const [open, setOpen] = useState(false);
  const [groupName, setGroupName] = useState('');
  const [eventName, setEventName] = useState('');
  const [leadEmployeeId, setLeadEmployeeId] = useState('');
  const [leadLabel, setLeadLabel] = useState<string | null>(null);
  const [destinationCountryId, setDestinationCountryId] = useState('');
  const [destinationCity, setDestinationCity] = useState('');
  const [travelStartDate, setTravelStartDate] = useState('');
  const [travelEndDate, setTravelEndDate] = useState('');
  const [maxParticipants, setMaxParticipants] = useState('');

  const reset = () => {
    setGroupName('');
    setEventName('');
    setLeadEmployeeId('');
    setLeadLabel(null);
    setDestinationCountryId('');
    setDestinationCity('');
    setTravelStartDate('');
    setTravelEndDate('');
    setMaxParticipants('');
  };

  const create = useMutation({
    mutationFn: () =>
      travelService.createGroup({
        groupName: groupName.trim(),
        eventName: eventName.trim() || null,
        leadEmployeeId,
        destinationCountryId,
        destinationCity: destinationCity.trim(),
        travelStartDate,
        travelEndDate,
        maxParticipants: maxParticipants === '' ? null : Number(maxParticipants),
      }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ['travel-groups'] });
      toast({ title: 'Group trip created', description: 'Add the travellers on the next screen.' });
      setOpen(false);
      reset();
      router.push(`/hr/travel/groups/${created.id}`);
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not create the group',
        description: e?.response?.data?.message ?? e?.response?.data ?? e?.message,
      }),
  });

  const rows = groups ?? [];
  const valid =
    groupName.trim() && leadEmployeeId && destinationCountryId &&
    destinationCity.trim() && travelStartDate && travelEndDate;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Group travel"
        description="Several people travelling to one place for one thing."
        backHref="/hr/travel"
        actions={
          canWrite ? (
            <Button onClick={() => { reset(); setOpen(true); }}>
              <Plus className="mr-2 h-4 w-4" />
              New group trip
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
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Users2}
              title="No group trips"
              description="A group trip raises one travel request per traveller, so everyone's is tracked individually while the trip is organised as one."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Group</TableHead>
                  <TableHead>Lead</TableHead>
                  <TableHead>Destination</TableHead>
                  <TableHead>Dates</TableHead>
                  <TableHead className="text-right">Travellers</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((g) => (
                  <TableRow key={g.id}>
                    <TableCell>
                      <Link href={`/hr/travel/groups/${g.id}`} className="font-medium hover:underline">
                        {g.groupName}
                      </Link>
                      {g.eventName && (
                        <div className="text-xs text-muted-foreground">{g.eventName}</div>
                      )}
                    </TableCell>
                    <TableCell>{g.leadEmployeeName || '—'}</TableCell>
                    <TableCell>{g.destinationCity}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {fmtDate(g.travelStartDate)} – {fmtDate(g.travelEndDate)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {/* currentParticipantCount, not participantCount - the latter matched no
                          field the API sends and rendered undefined on every row. */}
                      {g.currentParticipantCount}
                      {g.maxParticipants ? ` / ${g.maxParticipants}` : ''}
                    </TableCell>
                    <TableCell>
                      <Badge className={STATUS_TONE[g.status] ?? ''}>
                        {spaced(g.statusName || g.status)}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) reset(); }}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>New group trip</DialogTitle>
            <DialogDescription>
              The destination and dates here are copied onto every traveller&apos;s own request when
              you add them.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="gt-name">
                  Group name<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input
                  id="gt-name"
                  maxLength={200}
                  value={groupName}
                  onChange={(e) => setGroupName(e.target.value)}
                  placeholder="Annual audit team"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="gt-event">Event</Label>
                <Input
                  id="gt-event"
                  maxLength={200}
                  value={eventName}
                  onChange={(e) => setEventName(e.target.value)}
                  placeholder="What they are attending, if anything"
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label>
                Lead traveller<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <EmployeePicker
                value={leadEmployeeId || null}
                initialLabel={leadLabel}
                placeholder="Who is responsible for the group"
                onChange={(id, label) => { setLeadEmployeeId(id ?? ''); setLeadLabel(label); }}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="gt-country">
                  Destination country<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Select value={destinationCountryId} onValueChange={setDestinationCountryId}>
                  <SelectTrigger id="gt-country">
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
                <Label htmlFor="gt-city">
                  Destination city<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input
                  id="gt-city"
                  maxLength={100}
                  value={destinationCity}
                  onChange={(e) => setDestinationCity(e.target.value)}
                />
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="gt-start">
                  From<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input
                  id="gt-start"
                  type="date"
                  value={travelStartDate}
                  onChange={(e) => setTravelStartDate(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="gt-end">
                  To<span className="ml-0.5 text-red-500">*</span>
                </Label>
                <Input
                  id="gt-end"
                  type="date"
                  value={travelEndDate}
                  onChange={(e) => setTravelEndDate(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="gt-max">Max travellers</Label>
                <Input
                  id="gt-max"
                  type="number"
                  min={1}
                  value={maxParticipants}
                  onChange={(e) => setMaxParticipants(e.target.value)}
                />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={create.isPending}>
              Cancel
            </Button>
            <Button onClick={() => create.mutate()} disabled={!valid || create.isPending}>
              {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
