'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Boxes, Loader2, PenLine, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';
import { ASSET_ATTRIBUTE_DATA_TYPES } from '@/types/hr/assets';

/**
 * Asset types and their custom attributes — setup, so it lives under Administration.
 *
 * ⚠ **`AssetAttributeDataType` is alphabetical**: `Checkbox` is 1 and `Text` is 6, although the
 * backend's own default is Text. A picker built in reading order would have offered "Text" and
 * filed a checkbox — measured, not deduced: the probe sent `dataType: 1` for a field it called IMEI
 * and the read came back `"Checkbox"`. The options below come from a named constant for that
 * reason, listed in the order a person would want them rather than the order the numbers run.
 */
export default function AssetTypesSetupPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ name: '', description: '', hasExtraAttributes: false });
  const [expanded, setExpanded] = useState<string | null>(null);
  const [editing, setEditing] = useState<{ id: string; name: string; description: string;
    hasExtraAttributes: boolean } | null>(null);
  const [attrFor, setAttrFor] = useState<string | null>(null);
  const [attrForm, setAttrForm] = useState({
    attributeName: '', dataType: 'Text', isRequired: false, isExpiryDate: false, attributeOptions: '',
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'types'] });

  const { data: types = [], isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'types'],
    queryFn: () => assetRegisterService.getTypes(),
  });

  const { data: detail } = useQuery({
    queryKey: ['hr', 'assets', 'type-detail', expanded],
    queryFn: () => assetRegisterService.getTypeWithAttributes(expanded as string),
    enabled: Boolean(expanded),
  });

  const create = useMutation({
    mutationFn: () =>
      assetRegisterService.createType({
        name: form.name.trim(),
        description: form.description.trim() || null,
        hasExtraAttributes: form.hasExtraAttributes,
      }),
    onSuccess: () => {
      invalidate();
      setCreating(false);
      setForm({ name: '', description: '', hasExtraAttributes: false });
      toast({ title: 'Asset type added' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not add the type', description: e.message, variant: 'destructive' }),
  });

  const rename = useMutation({
    // The edited type is a mutation VARIABLE rather than a read of component state, so there is
    // nothing to assert non-null about: the dialog only opens with a type in hand, and passing it
    // through `mutate()` says that in the types instead of with `!`.
    mutationFn: (t: { id: string; name: string; description: string; hasExtraAttributes: boolean }) =>
      assetRegisterService.updateType(t.id, {
        // ⚠ `id` in the body as well as the route — the same shape as the register PUT.
        id: t.id,
        name: t.name.trim(),
        description: t.description.trim() || null,
        hasExtraAttributes: t.hasExtraAttributes,
      }),
    onSuccess: () => { invalidate(); setEditing(null); toast({ title: 'Asset type updated' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not update the type', description: e.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => assetRegisterService.deleteType(id),
    onSuccess: () => { invalidate(); toast({ title: 'Asset type removed' }); },
    onError: (e: Error) =>
      toast({ title: 'Could not remove the type', description: e.message, variant: 'destructive' }),
  });

  const addAttribute = useMutation({
    mutationFn: () =>
      assetRegisterService.createTypeAttribute(attrFor as string, {
        assetTypeId: attrFor as string,
        attributeName: attrForm.attributeName.trim(),
        // ⚠ The NUMBER, and it is not the position in this list — see the note at the top.
        dataType: ASSET_ATTRIBUTE_DATA_TYPES.find((d) => d.label === attrForm.dataType)?.value ?? 6,
        isRequired: attrForm.isRequired,
        isExpiryDate: attrForm.isExpiryDate,
        attributeOptions: attrForm.attributeOptions.trim(),
      }),
    onSuccess: () => {
      invalidate();
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'type-detail'] });
      setAttrFor(null);
      setAttrForm({
        attributeName: '', dataType: 'Text', isRequired: false, isExpiryDate: false, attributeOptions: '',
      });
      toast({ title: 'Attribute added' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not add the attribute', description: e.message, variant: 'destructive' }),
  });

  const removeAttribute = useMutation({
    mutationFn: (id: string) => assetRegisterService.deleteTypeAttribute(id),
    onSuccess: () => {
      invalidate();
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets', 'type-detail'] });
      toast({ title: 'Attribute removed' });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not remove the attribute', description: e.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Asset types"
        description="The categories the company-asset register is built on, and their custom fields."
        backHref="/administration/hr"
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="mr-2 h-4 w-4" /> Add a type
          </Button>
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : types.length === 0 ? (
            <EmptyState
              icon={Boxes}
              title="No asset types yet"
              description="Nothing can be added to the register until at least one type exists."
              action={<Button onClick={() => setCreating(true)}>Add a type</Button>}
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Type</TableHead>
                  <TableHead className="text-right">Assets</TableHead>
                  <TableHead className="text-right">Custom fields</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {types.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell>
                      <button
                        type="button"
                        className="text-left hover:underline"
                        onClick={() => setExpanded(expanded === t.id ? null : t.id)}
                      >
                        <div className="font-medium">{t.name}</div>
                        {t.description && (
                          <div className="text-xs text-muted-foreground">{t.description}</div>
                        )}
                      </button>
                    </TableCell>
                    <TableCell className="text-right">{t.assetCount}</TableCell>
                    <TableCell className="text-right">
                      {t.hasExtraAttributes
                        ? t.attributeCount
                        : <span className="text-muted-foreground">—</span>}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setEditing({
                          id: t.id,
                          name: t.name,
                          description: t.description ?? '',
                          hasExtraAttributes: t.hasExtraAttributes,
                        })}
                      >
                        <PenLine className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        // A type with assets on it cannot be removed — the API refuses in words.
                        onClick={() => remove.mutate(t.id)}
                        disabled={remove.isPending}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {expanded && detail && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between gap-2 text-base">
              <span>{detail.name} — custom fields</span>
              <Button size="sm" variant="outline" onClick={() => setAttrFor(detail.id)}>
                <Plus className="mr-2 h-4 w-4" /> Add a field
              </Button>
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {detail.attributes.length === 0 ? (
              <p className="p-6 text-sm text-muted-foreground">
                No custom fields. {detail.hasExtraAttributes
                  ? 'This type is marked as carrying extras, so add them here.'
                  : 'This type carries none — assets of it use the standard register fields only.'}
              </p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Field</TableHead>
                    <TableHead>Kind</TableHead>
                    <TableHead>Required</TableHead>
                    <TableHead>Is an expiry date</TableHead>
                    <TableHead />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {detail.attributes.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.attributeName}</TableCell>
                      <TableCell>{a.dataTypeName}</TableCell>
                      <TableCell>{a.isRequired ? 'Yes' : 'No'}</TableCell>
                      <TableCell>{a.isExpiryDate ? 'Yes' : 'No'}</TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm"
                          onClick={() => removeAttribute.mutate(a.id)}
                          disabled={removeAttribute.isPending}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add an asset type</DialogTitle>
            <DialogDescription>
              Laptops, vehicles, uniforms, tools — whatever the register needs to group by.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Name *</Label>
              <Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea rows={2} value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox id="extras" checked={form.hasExtraAttributes}
                onCheckedChange={(c) => setForm((f) => ({ ...f, hasExtraAttributes: c === true }))} />
              <Label htmlFor="extras" className="font-normal">
                Assets of this type carry extra fields — an IMEI, a registration number
              </Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>Cancel</Button>
            <Button onClick={() => create.mutate()} disabled={!form.name.trim() || create.isPending}>
              {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add type
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit asset type</DialogTitle>
            <DialogDescription>
              Renaming a type renames it everywhere it is shown — the register list, the report
              breakdown and every requisition that asked for one.
            </DialogDescription>
          </DialogHeader>
          {editing && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Name *</Label>
                <Input value={editing.name}
                  onChange={(e) => setEditing((t) => t && { ...t, name: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label>Description</Label>
                <Textarea rows={2} value={editing.description}
                  onChange={(e) => setEditing((t) => t && { ...t, description: e.target.value })} />
              </div>
              <div className="flex items-center gap-2">
                <Checkbox id="editExtras" checked={editing.hasExtraAttributes}
                  onCheckedChange={(c) =>
                    setEditing((t) => t && { ...t, hasExtraAttributes: c === true })} />
                <Label htmlFor="editExtras" className="font-normal">
                  Assets of this type carry extra fields
                </Label>
              </div>
              {/* Turning the flag off does not delete the attributes, and the assets keep their
                  values — it only stops the asset screens offering them. Say so. */}
              {!editing.hasExtraAttributes && (
                <p className="text-xs text-muted-foreground">
                  Any attributes already defined stay defined, and existing values stay stored —
                  they simply stop being offered on the asset screens.
                </p>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
            <Button onClick={() => editing && rename.mutate(editing)}
              disabled={!editing?.name.trim() || rename.isPending}>
              {rename.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={attrFor !== null} onOpenChange={(o) => !o && setAttrFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a custom field</DialogTitle>
            <DialogDescription>
              Every asset of this type can then carry a value for it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Field name *</Label>
              <Input value={attrForm.attributeName}
                onChange={(e) => setAttrForm((f) => ({ ...f, attributeName: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Kind</Label>
              <Select value={attrForm.dataType}
                onValueChange={(v) => setAttrForm((f) => ({ ...f, dataType: v }))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ASSET_ATTRIBUTE_DATA_TYPES.map((d) =>
                    <SelectItem key={d.label} value={d.label}>{d.text}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            {attrForm.dataType === 'Dropdown' && (
              <div className="space-y-2">
                <Label>Options</Label>
                <Textarea rows={2} placeholder="One per line"
                  value={attrForm.attributeOptions}
                  onChange={(e) => setAttrForm((f) => ({ ...f, attributeOptions: e.target.value }))} />
              </div>
            )}
            <div className="flex items-center gap-2">
              <Checkbox id="required" checked={attrForm.isRequired}
                onCheckedChange={(c) => setAttrForm((f) => ({ ...f, isRequired: c === true }))} />
              <Label htmlFor="required" className="font-normal">Required</Label>
            </div>
            {attrForm.dataType === 'Date' && (
              <div className="flex items-center gap-2">
                <Checkbox id="expiry" checked={attrForm.isExpiryDate}
                  onCheckedChange={(c) => setAttrForm((f) => ({ ...f, isExpiryDate: c === true }))} />
                <Label htmlFor="expiry" className="font-normal">
                  This date is an expiry — a licence, a certificate
                </Label>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAttrFor(null)}>Cancel</Button>
            <Button onClick={() => addAttribute.mutate()}
              disabled={!attrForm.attributeName.trim() || addAttribute.isPending}>
              {addAttribute.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add field
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
