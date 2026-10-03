'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BadgeCheck, IdCard, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { useToast } from '@/hooks/use-toast';
import { countryService } from '@/services/hr/country.service';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelDocument, TravelDocumentType } from '@/types/hr/travel-compliance';

/**
 * Your own passport and other travel documents (travel final closure, lane 7, slice 7c1 — E2's self-service half).
 *
 * The travel desk keeps the register at `/hr/travel/documents`; this is the traveller's door to their own rows, through
 * `api/staff-travel/me/travel-documents`, which takes no employee id — whose they are is the token. Recording your
 * primary passport here is what lets the visa register answer for your trips (D-39) and the passport's expiry be checked
 * when you submit one (O-16).
 *
 * The desk's rules hold: the desk verifies a document; a change takes the verification off; one primary per type — a new
 * primary stands your other one down; a verified document is kept, not removed. The list shows numbers to the last four
 * (O-7); changing a document reads it in full.
 */

const DOCUMENT_TYPES: TravelDocumentType[] = [
  'Passport', 'NationalId', 'Visa', 'ResidentPermit', 'WorkPermit', 'FrequentFlyerCard', 'HotelLoyaltyCard',
  'DrivingLicence', 'VaccineCertificate',
];
const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const today = () => new Date().toISOString().slice(0, 10);
const inMonths = (n: number) => {
  const d = new Date();
  d.setMonth(d.getMonth() + n);
  return d.toISOString().slice(0, 10);
};

const blank = () => ({
  documentType: 'Passport' as TravelDocumentType,
  documentNumber: '',
  issuingCountryId: '',
  issueDate: '',
  expiryDate: '',
  isPrimary: true,
});

export default function MyTravelDocumentsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<StaffTravelDocument | null>(null);
  const [loadingEdit, setLoadingEdit] = useState(false);
  const [form, setForm] = useState(blank);

  const countries = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const documents = useQuery({
    queryKey: ['my-travel-documents'],
    queryFn: () => travelService.getMyDocuments(),
  });
  const rows = (documents.data ?? [])
    .slice()
    .sort((a, b) => a.documentTypeName.localeCompare(b.documentTypeName) || Number(b.isPrimary) - Number(a.isPrimary));

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['my-travel-documents'] });

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        documentType: form.documentType,
        documentNumber: form.documentNumber.trim(),
        issuingCountryId: form.issuingCountryId,
        issueDate: form.issueDate || null,
        expiryDate: form.expiryDate || null,
        isPrimary: form.isPrimary,
      };
      return editing
        ? travelService.updateMyDocument({ ...payload, id: editing.id })
        : travelService.createMyDocument(payload);
    },
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setEditing(null);
      setForm(blank());
      toast({
        title: editing ? 'Document changed' : 'Document recorded',
        description: 'The travel desk verifies it.',
      });
    },
    onError: (e: Error) =>
      toast({ title: 'The document was refused', description: e.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => travelService.deleteMyDocument(id),
    onSuccess: async () => { await refresh(); toast({ title: 'Document removed' }); },
    onError: (e: Error) => toast({ title: 'Could not remove it', description: e.message, variant: 'destructive' }),
  });

  const startAdd = () => {
    setEditing(null);
    setForm(blank());
    setOpen(true);
  };

  // The list shows the number masked; a change reads the document's own detail, which is in full.
  const startEdit = async (d: StaffTravelDocument) => {
    setLoadingEdit(true);
    try {
      const full = await travelService.getMyDocument(d.id);
      setEditing(full);
      setForm({
        documentType: full.documentType,
        documentNumber: full.documentNumber,
        issuingCountryId: full.issuingCountryId,
        issueDate: full.issueDate?.slice(0, 10) ?? '',
        expiryDate: full.expiryDate?.slice(0, 10) ?? '',
        isPrimary: full.isPrimary,
      });
      setOpen(true);
    } catch (e) {
      toast({ title: 'Could not open the document', description: (e as Error).message, variant: 'destructive' });
    } finally {
      setLoadingEdit(false);
    }
  };

  const expiryBadge = (d: StaffTravelDocument) => {
    const exp = d.expiryDate?.slice(0, 10);
    if (!exp) return null;
    if (exp < today()) return <Badge variant="destructive" className="ml-2">expired</Badge>;
    if (exp <= inMonths(6)) return <Badge variant="outline" className="ml-2">within 6 months</Badge>;
    return null;
  };

  const canSave = form.documentNumber.trim() && form.issuingCountryId;

  return (
    <div className="space-y-6">
      <PageHeader
        title="My travel documents"
        description="Your passport and other travel documents. Your primary passport decides which of your trips need a visa, and is checked for expiry when you send a trip for approval."
        backHref="/me/travel"
        actions={
          <Button size="sm" onClick={startAdd}>
            <Plus className="mr-2 h-4 w-4" /> Add a document
          </Button>
        }
      />

      {documents.isError && !documents.data && <TravelQueryError error={documents.error} what="your travel documents" />}
      {documents.isLoading ? (
        <div className="flex justify-center p-10"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>
      ) : rows.length === 0 && !documents.isError ? (
        <EmptyState
          icon={IdCard}
          title="No travel documents"
          description="Record your passport so the travel desk can check visas and expiry for your trips."
        />
      ) : rows.length > 0 && (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Document</TableHead>
                  <TableHead>Number</TableHead>
                  <TableHead>Issued by</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead>Verified</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell className="font-medium">
                      {humanize(d.documentTypeName)}
                      {d.isPrimary && <Badge variant="secondary" className="ml-2">primary</Badge>}
                    </TableCell>
                    <TableCell className="font-mono text-sm">{d.documentNumber}</TableCell>
                    <TableCell>{d.issuingCountryName ?? '—'}</TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDate(d.expiryDate)}{expiryBadge(d)}</TableCell>
                    <TableCell className="text-sm">
                      {d.isVerified
                        ? <span className="inline-flex items-center gap-1"><BadgeCheck className="h-4 w-4 text-green-600" />By the travel desk</span>
                        : <span className="text-muted-foreground">Not yet</span>}
                    </TableCell>
                    <TableCell className="whitespace-nowrap text-right">
                      <Button variant="ghost" size="icon" aria-label="Change this document" disabled={loadingEdit}
                        onClick={() => startEdit(d)}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      {/* A verified document is kept: correct it (which takes the verification off) or add its replacement. */}
                      {!d.isVerified && (
                        <Button variant="ghost" size="icon" aria-label="Remove this document" disabled={remove.isPending}
                          onClick={() => remove.mutate(d.id)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <Dialog open={open} onOpenChange={(v) => { if (!v) { setOpen(false); setEditing(null); } }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Change this document' : 'Record a travel document'}</DialogTitle>
            <DialogDescription>
              {editing?.isVerified
                ? 'The travel desk has verified this document. Saving a change takes the verification off until they check it again.'
                : 'One primary document per type: marking this one primary stands your other one down. The travel desk verifies it.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Type<span className="ml-0.5 text-red-500">*</span></Label>
                <Select value={form.documentType}
                  onValueChange={(v) => v && setForm((f) => ({ ...f, documentType: v as TravelDocumentType }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {DOCUMENT_TYPES.map((t) => <SelectItem key={t} value={t}>{humanize(t)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="my-doc-number">Number<span className="ml-0.5 text-red-500">*</span></Label>
                <Input id="my-doc-number" value={form.documentNumber}
                  onChange={(e) => setForm((f) => ({ ...f, documentNumber: e.target.value }))} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Issuing country<span className="ml-0.5 text-red-500">*</span></Label>
              <Select value={form.issuingCountryId}
                onValueChange={(v) => v && setForm((f) => ({ ...f, issuingCountryId: v }))}>
                <SelectTrigger><SelectValue placeholder="Pick a country" /></SelectTrigger>
                <SelectContent>
                  {(countries.data ?? []).map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="my-doc-issued">Issued</Label>
                <Input id="my-doc-issued" type="date" value={form.issueDate}
                  onChange={(e) => setForm((f) => ({ ...f, issueDate: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="my-doc-expires">Expires</Label>
                <Input id="my-doc-expires" type="date" value={form.expiryDate}
                  onChange={(e) => setForm((f) => ({ ...f, expiryDate: e.target.value }))} />
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox id="my-doc-primary" checked={form.isPrimary}
                onCheckedChange={(v) => setForm((f) => ({ ...f, isPrimary: v === true }))} />
              <Label htmlFor="my-doc-primary">My primary document of this type</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setOpen(false); setEditing(null); }}>Cancel</Button>
            <Button disabled={!canSave || save.isPending} onClick={() => save.mutate()}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {editing ? 'Save' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
