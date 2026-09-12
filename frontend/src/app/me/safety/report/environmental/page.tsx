'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Leaf, Loader2 } from 'lucide-react';
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
import { safetyEnvironmentalService } from '@/services/hr/safety-environmental.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_ENV_INCIDENT_TYPE_OPTIONS,
  SHE_ENV_MEDIA_OPTIONS,
  SHE_INCIDENT_SEVERITY_OPTIONS,
} from '@/types/hr/safety-environment';
import type {
  SheEnvironmentalIncidentType,
  SheEnvironmentalMedia,
  SheIncidentSeverity,
} from '@/types/hr/safety-environment';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const isoDay = (d: Date) => d.toISOString().slice(0, 10);

/**
 * Environmental incident reporting (FR-ENV-025) — open to EVERY employee, like
 * incident, hazard and stop-work reporting. Spills, exceedances, dumping and
 * contamination reported here alert the SHE team automatically; you always
 * report as yourself — the server takes the reporter from your login. Your own
 * reports and their progress are listed below.
 *
 * Area 25 slice 8: re-homed from /hr/safety/report-environmental-incident under the portal's
 * unified report-a-concern surface (D3 — moved, not redirected).
 */
export default function ReportEnvironmentalIncidentPage() {
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const [type, setType] = useState<string>('OilSpill');
  const [affectedMedia, setAffectedMedia] = useState<string>('Soil');
  const [severity, setSeverity] = useState<string>('Minor');
  const [incidentDate, setIncidentDate] = useState(isoDay(new Date()));
  const [locationId, setLocationId] = useState('');
  const [specificArea, setSpecificArea] = useState('');
  const [description, setDescription] = useState('');
  const [spillVolume, setSpillVolume] = useState('');
  const [substanceInvolved, setSubstanceInvolved] = useState('');
  const [immediateResponseAction, setImmediateResponseAction] = useState('');

  // HR-gated lookup: for a plain employee this 403s and the picker simply stays empty.
  const { data: locations = [] } = useQuery({
    queryKey: ['me', 'safety', 'locations'],
    queryFn: () => locationService.getAll(),
    retry: false,
  });

  const { data: mine = [], isLoading: mineLoading } = useQuery({
    queryKey: ['me', 'safety', 'environmental'],
    queryFn: () => safetyEnvironmentalService.getMyIncidents(),
  });

  const submit = async () => {
    if (!description.trim()) {
      toast.error('Describe what happened');
      return;
    }
    setBusy(true);
    try {
      const created = await safetyEnvironmentalService.createIncident({
        type: type as SheEnvironmentalIncidentType,
        affectedMedia: affectedMedia as SheEnvironmentalMedia,
        severity: severity as SheIncidentSeverity,
        incidentDate: new Date(incidentDate).toISOString(),
        locationId: locationId || null,
        specificArea: specificArea.trim() || null,
        description: description.trim(),
        spillVolume: spillVolume.trim() || null,
        substanceInvolved: substanceInvolved.trim() || null,
        immediateResponseAction: immediateResponseAction.trim() || null,
        // The server takes the reporter from the login — an empty id defers to the token.
        reportedById: '00000000-0000-0000-0000-000000000000',
      });
      toast.success(
        `Your report ${created.incidentNumber} has been logged and the SHE team alerted.`,
      );
      setSpecificArea('');
      setDescription('');
      setSpillVolume('');
      setSubstanceInvolved('');
      setImmediateResponseAction('');
      await queryClient.invalidateQueries({ queryKey: ['me', 'safety', 'environmental'] });
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Submitting the report failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Report an Environmental Incident"
        description="Open to every employee. Spills, exceedances, uncontrolled dumping and contamination reported here alert the SHE team automatically — you always report as yourself."
        backHref="/me/safety/report"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What happened?</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label>Type</Label>
              <Select value={type} onValueChange={setType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SHE_ENV_INCIDENT_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Affected media</Label>
              <Select value={affectedMedia} onValueChange={setAffectedMedia}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SHE_ENV_MEDIA_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Severity</Label>
              <Select value={severity} onValueChange={setSeverity}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SHE_INCIDENT_SEVERITY_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div className="space-y-2">
              <Label>When</Label>
              <Input
                type="date"
                value={incidentDate}
                onChange={(e) => setIncidentDate(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Location</Label>
              <Select value={locationId || 'none'} onValueChange={(v) => setLocationId(v === 'none' ? '' : v)}>
                <SelectTrigger>
                  <SelectValue placeholder="Not set" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Not set</SelectItem>
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
                placeholder="Where exactly (e.g. workshop apron, drain 3)"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label>
              Description <span className="text-destructive">*</span>
            </Label>
            <Textarea
              rows={4}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="What happened, what was released or affected, and anything else the SHE team should know."
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Estimated volume / extent</Label>
              <Input
                value={spillVolume}
                onChange={(e) => setSpillVolume(e.target.value)}
                placeholder="e.g. about 20 litres"
              />
            </div>
            <div className="space-y-2">
              <Label>Substance involved</Label>
              <Input
                value={substanceInvolved}
                onChange={(e) => setSubstanceInvolved(e.target.value)}
                placeholder="e.g. hydraulic oil"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label>Immediate action taken</Label>
            <Textarea
              rows={2}
              value={immediateResponseAction}
              onChange={(e) => setImmediateResponseAction(e.target.value)}
              placeholder="Containment, isolation, who was told…"
            />
          </div>

          <div className="flex justify-end">
            <Button disabled={busy} onClick={() => void submit()}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Submit report
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">My environmental reports</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {mineLoading ? (
            <div className="flex justify-center py-8">
              <Loader2 className="text-muted-foreground h-5 w-5 animate-spin" />
            </div>
          ) : mine.length === 0 ? (
            <div className="text-muted-foreground flex flex-col items-center gap-2 py-10 text-sm">
              <Leaf className="h-6 w-6" />
              You have not reported any environmental incidents.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {mine.map((i) => (
                  <TableRow key={i.id}>
                    <TableCell className="font-mono">{i.incidentNumber}</TableCell>
                    <TableCell>{i.typeName}</TableCell>
                    <TableCell>{i.severityName}</TableCell>
                    <TableCell>{fmtDate(i.incidentDate)}</TableCell>
                    <TableCell>
                      {i.status === 'Closed' ? (
                        <Badge variant="outline" className="text-muted-foreground">
                          Closed
                        </Badge>
                      ) : (
                        <Badge variant="secondary">{i.statusName}</Badge>
                      )}
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
