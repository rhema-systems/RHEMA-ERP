'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, OctagonX } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { toast } from 'sonner';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { safetyStopWorkService } from '@/services/hr/safety-stop-work.service';
import { locationService } from '@/services/hr/location.service';
import type { SheStopWorkStatus } from '@/types/hr/safety-audits';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const STATUS_LABEL: Record<SheStopWorkStatus, string> = {
  Raised: 'Raised — work stopped',
  UnderReview: 'Under review — work stopped',
  Resolved: 'Resolved — awaiting clearance',
  Cleared: 'Cleared — work resumed',
  Cancelled: 'Cancelled',
};

/**
 * Stop-work authority (FR-SHE-200) — open to EVERY employee, like incident and
 * hazard reporting. If work looks imminently dangerous, stop it and record why;
 * the SHE team routes, resolves and clears resumption. You always raise as
 * yourself — the server takes the raiser from your login. Your own orders and
 * their progress are listed below.
 *
 * Area 25 slice 8: re-homed from /hr/safety/raise-stop-work under the portal's unified
 * report-a-concern surface (D3 — moved, not redirected).
 */
export default function RaiseStopWorkPage() {
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const [locationId, setLocationId] = useState('');
  const [specificArea, setSpecificArea] = useState('');
  const [workDescription, setWorkDescription] = useState('');
  const [reasonDescription, setReasonDescription] = useState('');
  const [immediateActions, setImmediateActions] = useState('');

  // HR-gated lookup: for a plain employee this 403s and the picker simply stays empty.
  const { data: locations = [] } = useQuery({
    queryKey: ['me', 'safety', 'locations'],
    queryFn: () => locationService.getAll(),
    retry: false,
  });

  const { data: mine = [], isLoading: mineLoading } = useQuery({
    queryKey: ['me', 'safety', 'stop-work'],
    queryFn: () => safetyStopWorkService.getMine(),
  });

  const submit = async () => {
    setBusy(true);
    try {
      const created = await safetyStopWorkService.raise({
        locationId: locationId || null,
        specificArea: specificArea.trim() || null,
        workDescription: workDescription.trim(),
        reasonDescription: reasonDescription.trim(),
        immediateActionsTaken: immediateActions.trim() || null,
      });
      await queryClient.invalidateQueries({ queryKey: ['me', 'safety', 'stop-work'] });
      setWorkDescription('');
      setReasonDescription('');
      setImmediateActions('');
      setSpecificArea('');
      setLocationId('');
      toast.success(
        `Stop-work order ${created.orderNumber} raised — the SHE team has been alerted. Work stays stopped until it is resolved and cleared.`,
      );
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to raise the order.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Raise a Stop-Work Order"
        description="If work looks imminently dangerous, you have the authority to stop it — no role needed, no permission asked. Say what you stopped and why; the SHE team takes it from there."
        backHref="/me/safety/report"
      />

      <Card className="border-destructive">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <OctagonX className="text-destructive h-5 w-5" />
            Stop the work first. Then record it here.
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>What work did you stop? *</Label>
            <Textarea
              rows={2}
              value={workDescription}
              onChange={(e) => setWorkDescription(e.target.value)}
              placeholder="e.g. Grinding on the tank farm walkway"
            />
          </div>
          <div className="space-y-2">
            <Label>Why — what danger did you see? *</Label>
            <Textarea
              rows={3}
              value={reasonDescription}
              onChange={(e) => setReasonDescription(e.target.value)}
              placeholder="e.g. Sparks landing near an open solvent drum; no fire watch present"
            />
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Location</Label>
              <Select value={locationId} onValueChange={setLocationId}>
                <SelectTrigger>
                  <SelectValue placeholder={locations.length ? 'Pick a location' : 'Location list unavailable'} />
                </SelectTrigger>
                <SelectContent>
                  {locations.map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {l.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Specific area</Label>
              <Input
                value={specificArea}
                onChange={(e) => setSpecificArea(e.target.value)}
                placeholder="e.g. Walkway between tanks 3 and 4"
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label>Immediate actions you took</Label>
            <Textarea
              rows={2}
              value={immediateActions}
              onChange={(e) => setImmediateActions(e.target.value)}
              placeholder="e.g. Told the crew to stand down; covered the drum"
            />
          </div>
          <div className="flex justify-end">
            <Button
              variant="destructive"
              disabled={busy || workDescription.trim().length === 0 || reasonDescription.trim().length === 0}
              onClick={() => void submit()}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Raise stop-work order
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">My stop-work orders</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {mineLoading ? (
            <div className="flex items-center justify-center py-10">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : mine.length === 0 ? (
            <p className="text-muted-foreground px-6 pb-6 text-sm">You have not raised any.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead className="max-w-sm">Work stopped</TableHead>
                  <TableHead>Raised</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Resolution</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {mine.map((o) => (
                  <TableRow key={o.id}>
                    <TableCell className="font-mono text-sm">{o.orderNumber}</TableCell>
                    <TableCell className="max-w-sm">
                      <span className="line-clamp-1">{o.workDescription}</span>
                    </TableCell>
                    <TableCell className="tabular-nums">{fmtDate(o.raisedDate)}</TableCell>
                    <TableCell>
                      <Badge variant={o.status === 'Cleared' ? 'secondary' : o.status === 'Cancelled' ? 'outline' : 'destructive'}>
                        {STATUS_LABEL[o.status]}
                      </Badge>
                    </TableCell>
                    <TableCell className="max-w-xs">
                      <span className="text-muted-foreground line-clamp-1 text-sm">
                        {o.resolutionDescription ?? '—'}
                      </span>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
