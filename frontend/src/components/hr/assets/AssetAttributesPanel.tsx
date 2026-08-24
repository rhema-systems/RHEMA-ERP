'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';
import type { AssetAttributeDataType, AssetAttributeValue } from '@/types/hr/assets';

/**
 * Renders the right input for a declared attribute type.
 *
 * ⚠ The value is stored as a **string** whatever the declared type is — `AssetAttributeValue.value`
 * is `nvarchar`. So a checkbox writes `'true'` / `'false'` and a date writes `YYYY-MM-DD`; the type
 * decides the input, not the storage. Anything that later wants to compare these has to parse them,
 * which is worth knowing before building a report on top.
 */
function ValueInput({
  dataType, options, value, onChange, id,
}: {
  dataType: AssetAttributeDataType;
  options: string;
  value: string;
  onChange: (v: string) => void;
  id: string;
}) {
  if (dataType === 'Checkbox') {
    return (
      <div className="flex items-center gap-2">
        <Checkbox
          id={id}
          checked={value === 'true'}
          onCheckedChange={(c) => onChange(c === true ? 'true' : 'false')}
        />
        <Label htmlFor={id} className="font-normal">{value === 'true' ? 'Yes' : 'No'}</Label>
      </div>
    );
  }
  if (dataType === 'Date') {
    return <Input type="date" value={value} onChange={(e) => onChange(e.target.value)} />;
  }
  if (dataType === 'Integer') {
    return <Input type="number" step="1" value={value} onChange={(e) => onChange(e.target.value)} />;
  }
  if (dataType === 'Decimal') {
    return <Input type="number" step="0.01" value={value} onChange={(e) => onChange(e.target.value)} />;
  }
  if (dataType === 'Dropdown') {
    const choices = options.split(/\r?\n/).map((o) => o.trim()).filter(Boolean);
    // A dropdown whose type carries no options is a dropdown with nothing in it — say so rather
    // than render an empty menu the user cannot get past.
    if (choices.length === 0) {
      return (
        <Input
          value={value}
          placeholder="This attribute has no options defined"
          onChange={(e) => onChange(e.target.value)}
        />
      );
    }
    return (
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
        <SelectContent>
          {choices.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
        </SelectContent>
      </Select>
    );
  }
  return <Input value={value} onChange={(e) => onChange(e.target.value)} />;
}

/**
 * The custom attributes an asset carries because of its type — area 16 slice 12b.
 *
 * ⚠ Until this slice the type catalogue could declare attributes that **no screen could ever
 * fill**: `attributeValues` was on the create payload and the form rendered no inputs for it, and
 * the detail screen listed values read-only. A configurable field nobody can set is configuration
 * theatre.
 *
 * The panel reads the TYPE's declared attributes and the ASSET's values and joins them, so an
 * attribute added to the type after the asset was registered still appears — empty, and fillable.
 * Listing only the stored values would hide exactly the ones that need attention.
 */
export function AssetAttributesPanel({
  assetId, assetTypeId, hasExtraAttributes,
}: {
  assetId: string;
  assetTypeId: string;
  hasExtraAttributes: boolean;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [drafts, setDrafts] = useState<Record<string, string>>({});

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'attribute-values', assetId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'detail', assetId] });
  };

  const { data: type } = useQuery({
    queryKey: ['hr', 'assets', 'type-detail', assetTypeId],
    queryFn: () => assetRegisterService.getTypeWithAttributes(assetTypeId),
    enabled: Boolean(assetTypeId),
  });

  const { data: values = [], isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'attribute-values', assetId],
    queryFn: () => assetRegisterService.getAttributeValues(assetId),
  });

  const save = useMutation({
    mutationFn: ({ attributeId, existing, value }:
      { attributeId: string; existing: AssetAttributeValue | undefined; value: string }) =>
      existing
        ? assetRegisterService.updateAttributeValue(existing.id, {
            id: existing.id, assetId, assetTypeAttributeId: attributeId, value,
          })
        : assetRegisterService.addAttributeValue(assetId, {
            assetId, assetTypeAttributeId: attributeId, value,
          }),
    onSuccess: (_r, vars) => {
      invalidate();
      setDrafts((d) => { const next = { ...d }; delete next[vars.attributeId]; return next; });
      toast({ title: 'Attribute saved' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not save it', description: e.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => assetRegisterService.deleteAttributeValue(id),
    onSuccess: () => { invalidate(); toast({ title: 'Attribute cleared' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not clear it', description: e.message, variant: 'destructive' }),
  });

  const attributes = type?.attributes ?? [];

  if (!hasExtraAttributes || attributes.length === 0) {
    return (
      <Card>
        <CardContent className="p-0">
          <EmptyState
            icon={Plus}
            title="No custom attributes"
            description={hasExtraAttributes
              ? 'This asset type is marked as carrying extras but none have been defined. Add them under Administration → HR → Asset Types.'
              : 'This asset type carries none — its assets use the standard register fields only.'}
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="p-0">
        {isLoading ? (
          <div className="flex justify-center p-10">
            <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-1/4">Attribute</TableHead>
                <TableHead className="w-1/6">Kind</TableHead>
                <TableHead>Value</TableHead>
                <TableHead className="w-32" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {attributes.map((attr) => {
                const existing = values.find((v) => v.assetTypeAttributeId === attr.id);
                const draft = drafts[attr.id];
                const current = draft ?? existing?.value ?? '';
                const dirty = draft !== undefined && draft !== (existing?.value ?? '');
                return (
                  <TableRow key={attr.id}>
                    <TableCell className="font-medium">
                      {attr.attributeName}
                      {attr.isRequired && <span className="text-destructive"> *</span>}
                      {/* An attribute flagged as an expiry is a licence or a certificate date —
                          worth surfacing, because nothing in this area watches it yet. */}
                      {attr.isExpiryDate && (
                        <div className="text-xs text-muted-foreground">an expiry date</div>
                      )}
                    </TableCell>
                    <TableCell>{attr.dataTypeName}</TableCell>
                    <TableCell>
                      <ValueInput
                        id={`attr-${attr.id}`}
                        dataType={attr.dataType}
                        options={attr.attributeOptions}
                        value={current}
                        onChange={(v) => setDrafts((d) => ({ ...d, [attr.id]: v }))}
                      />
                      {attr.isRequired && !existing && !dirty && (
                        <p className="mt-1 text-xs text-amber-600 dark:text-amber-500">
                          Required by this asset type, and not set.
                        </p>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={!dirty || save.isPending}
                        onClick={() => save.mutate({ attributeId: attr.id, existing, value: current })}
                      >
                        {save.isPending
                          ? <Loader2 className="h-4 w-4 animate-spin" />
                          : <Save className="h-4 w-4" />}
                      </Button>
                      {existing && (
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={remove.isPending}
                          onClick={() => remove.mutate(existing.id)}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
