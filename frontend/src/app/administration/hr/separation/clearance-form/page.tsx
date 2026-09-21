'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Info, Loader2, Plus, Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { Checkbox } from '@/components/ui/checkbox';
import { separationService } from '@/services/hr/separation.service';
import type { ClearanceItemKind } from '@/types/hr/separation';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

/**
 * The tenant's clearance form — the catalogue every leaver's checklist is built from (FR-HR-183).
 *
 * ⚠ **An empty catalogue is not a blank slate, it is a hole.** A clearance run built from no lines
 * would complete the instant it began and report "cleared" having checked nothing, so the server
 * refuses to start one. That is why the seed button is prominent rather than tucked away: on a new
 * tenant this screen is the difference between a working gate and a decorative one.
 */

const KINDS: { value: ClearanceItemKind; label: string; money: boolean }[] = [
  { value: 'OutstandingLoan', label: 'Outstanding loan', money: true },
  { value: 'SalaryAdvance', label: 'Salary advance', money: true },
  { value: 'PayrollRecovery', label: 'Payroll recovery', money: true },
  { value: 'CompanyProperty', label: 'Company property', money: false },
  { value: 'OfficeEquipment', label: 'Office equipment', money: false },
  { value: 'DutyPostKeys', label: 'Duty-post keys', money: false },
  { value: 'DocumentsAndRecords', label: 'Documents and records', money: false },
  { value: 'Other', label: 'Other', money: false },
];

const NO_UNIT = '__none__';

export default function ClearanceFormPage() {
  const queryClient = useQueryClient();
  // ⚠ The add strip used to post `{ name, kind }` and nothing else, so five more fields on the
  // create DTO were unreachable — instrument 03 flagged only `sourcesFromAssetRegister`, because
  // the other names occur elsewhere in the frontend. Rebuilt from the DTO (lane 5b, 2026-09-01).
  const [name, setName] = useState('');
  const [kind, setKind] = useState<ClearanceItemKind>('Other');
  const [description, setDescription] = useState('');
  const [owningUnitId, setOwningUnitId] = useState<string>(NO_UNIT);
  const [isMandatory, setIsMandatory] = useState(true);
  const [sortOrder, setSortOrder] = useState('');
  const [sourcesAssets, setSourcesAssets] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { data: templates, isLoading } = useQuery({
    queryKey: ['clearance-templates'],
    queryFn: () => separationService.getClearanceTemplates(true),
  });


  const refresh = () => queryClient.invalidateQueries({ queryKey: ['clearance-templates'] });

  const act = useMutation({
    mutationFn: (fn: () => Promise<unknown>) => fn(),
    onSuccess: () => {
      setError(null);
      setName('');
      setDescription('');
      setSortOrder('');
      setSourcesAssets(false);
      refresh();
    },
    // The service's refusals are worth reading in full — "only one line may source the asset
    // register" explains itself and names the line that already does.
    onError: (e: any) => setError(e?.data?.message ?? e?.message ?? 'Something went wrong'),
  });

  const lines = templates ?? [];
  const activeCount = lines.filter((t) => t.isActive).length;
  const assetSource = lines.find((t) => t.sourcesFromAssetRegister);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Clearance form"
        description="What every leaver must be cleared against before their entitlements are computed (FR-HR-183)."
        actions={
          <Button
            variant="outline"
            onClick={() => act.mutate(() => separationService.seedDefaultClearanceTemplates())}
            disabled={act.isPending}
          >
            <Sparkles className="mr-2 h-4 w-4" />
            Add the standard seven
          </Button>
        }
      />

      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {!isLoading && activeCount === 0 && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            No clearance lines are active, so no clearance can be started at all. Until at least one
            exists, nobody can be cleared — and a form with no lines would report “cleared” having
            checked nothing.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader><CardTitle className="text-base">Add a line</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-3">
            <div className="min-w-[240px] flex-1 space-y-2">
              <Label>Name</Label>
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Library books"
              />
            </div>
            <div className="w-[220px] space-y-2">
              <Label>Kind</Label>
              <Select value={kind} onValueChange={(v) => setKind(v as ClearanceItemKind)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {KINDS.map((k) => <SelectItem key={k.value} value={k.value}>{k.label}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="w-[120px] space-y-2">
              <Label>Order</Label>
              <Input
                type="number"
                value={sortOrder}
                onChange={(e) => setSortOrder(e.target.value)}
                placeholder="0"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label>What the signer is confirming</Label>
            <Input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Shown on the leaver's form beneath the line, so the person signing knows what they are attesting to."
            />
          </div>

          <div className="flex flex-wrap items-end gap-3">
            <div className="min-w-[260px] flex-1 space-y-2">
              <OrganizationUnitPicker
                value={owningUnitId === NO_UNIT ? '' : owningUnitId}
                onChange={(id) => setOwningUnitId(id || NO_UNIT)}
                allowNone="Anyone with clearance rights"
                unitLabel="Cleared by"
                idPrefix="clearance-unit"
              />
            </div>
            <div className="flex items-center gap-2 pb-2">
              <Checkbox
                id="isMandatory"
                checked={isMandatory}
                onCheckedChange={(c) => setIsMandatory(c === true)}
              />
              <Label htmlFor="isMandatory" className="font-normal">Required on every form</Label>
            </div>
          </div>

          {/*
            ⚠ At most one line may be fed by the asset register (RequireSoleAssetSourceAsync).
            Say which line holds it rather than offering a checkbox that answers 400 — the seeded
            catalogue puts it on "Company property", so on a seeded tenant this is always taken.
          */}
          <div className="flex items-start gap-2">
            <Checkbox
              id="sourcesAssets"
              className="mt-0.5"
              checked={sourcesAssets}
              disabled={!!assetSource}
              onCheckedChange={(c) => setSourcesAssets(c === true)}
            />
            <div className="space-y-0.5">
              <Label htmlFor="sourcesAssets" className="font-normal">
                Fill this line from the HR Assets register
              </Label>
              <p className="text-xs text-muted-foreground">
                {assetSource
                  ? `“${assetSource.name}” already does, and only one line may — two would list every unreturned asset twice and deduct every surcharge twice. Turn it off there first.`
                  : 'Everything on the register the leaver has not given back is listed under this line automatically.'}
              </p>
            </div>
          </div>

          <Button
            onClick={() =>
              act.mutate(() =>
                separationService.createClearanceTemplate({
                  name: name.trim(),
                  kind,
                  description: description.trim() || null,
                  owningOrganizationUnitId: owningUnitId === NO_UNIT ? null : owningUnitId,
                  isMandatory,
                  isActive: true,
                  sortOrder: sortOrder === '' ? undefined : Number(sortOrder),
                  sourcesFromAssetRegister: sourcesAssets,
                }),
              )
            }
            disabled={act.isPending || !name.trim()}
          >
            <Plus className="mr-2 h-4 w-4" />
            Add
          </Button>
        </CardContent>
      </Card>

      <Alert>
        <Info className="h-4 w-4" />
        <AlertDescription>
          Only loans, salary advances and payroll recoveries can carry an amount — the settlement
          reads those figures. A line for keys or equipment records whether they came back, not what
          they were worth.
        </AlertDescription>
      </Alert>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" /> Loading…
            </div>
          ) : lines.length === 0 ? (
            <EmptyState
              title="No clearance lines"
              description="Add the standard seven to start — outstanding loans, salary advances, company property, office equipment, duty-post keys, documents and payroll recoveries."
            />
          ) : (
            <div className="divide-y">
              {lines.map((t) => (
                <div key={t.id} className="flex flex-wrap items-center justify-between gap-2 p-4">
                  <div>
                    <div className="font-medium">
                      {t.name}
                      {!t.isActive && <span className="ml-2 text-xs text-muted-foreground">(retired)</span>}
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {[t.kindName, t.owningOrganizationUnitName, t.description]
                        .filter(Boolean).join(' · ')}
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {t.carriesAmount && <Badge variant="outline" className="text-xs">Carries an amount</Badge>}
                    {t.isMandatory && <Badge variant="secondary" className="text-xs">Required</Badge>}
                    {t.sourcesFromAssetRegister && (
                      <Badge className="text-xs">From the asset register</Badge>
                    )}
                    {t.sourcesFromAssetRegister && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => act.mutate(() =>
                          separationService.updateClearanceTemplate(t.id, { sourcesFromAssetRegister: false }))}
                        disabled={act.isPending}
                      >
                        Stop sourcing assets
                      </Button>
                    )}
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => act.mutate(() =>
                        separationService.updateClearanceTemplate(t.id, { isActive: !t.isActive }))}
                      disabled={act.isPending}
                    >
                      {t.isActive ? 'Retire' : 'Reinstate'}
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Retiring a line keeps it off future clearance forms and leaves every form already issued
        exactly as it was signed — each one holds its own copy of the line it was cleared against.
      </p>
    </div>
  );
}
