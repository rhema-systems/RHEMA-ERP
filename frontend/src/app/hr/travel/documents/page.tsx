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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { HR_ROLES } from '@/components/hr/common/PermissionGate';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { useToast } from '@/hooks/use-toast';
import { useAuth } from '@/hooks/use-auth';
import { countryService } from '@/services/hr/country.service';
import { travelComplianceService } from '@/services/hr/travel-compliance.service';
import type { StaffTravelDocument, TravelDocumentType } from '@/types/hr/travel-compliance';

/**
 * The travellers' passports and other travel documents (travel final closure, lane 7, E2).
 *
 * ⚠ **There was no screen.** `createDocument` had no caller, so the passport-based visa lookup never resolved for
 * anything entered through the UI and the expiry reminders had no rows a person had typed — while the compliance panel
 * told users to "record their passport under travel documents first".
 *
 * The rules are the server's: an edit takes the verification off (it was of what the document said before); one
 * primary document per type per employee — a new primary stands the other down; a verified document is not deleted
 * (O-15). Numbers are masked to their last four in every list (O-7); the edit dialog reads the document in full.
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
  employeeId: '',
  employeeLabel: '',
  documentType: 'Passport' as TravelDocumentType,
  documentNumber: '',
  issuingCountryId: '',
  issueDate: '',
  expiryDate: '',
  isPrimary: true,
});

export default function TravelDocumentsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canWrite = hasAnyPermission(['HR.Travel.Write', 'HR.Travel.Admin']) || hasAnyRole(HR_ROLES);
  // DELETE is Admin-gated on the server, a tier above writing.
  const canDelete = hasAnyPermission(['HR.Travel.Admin']);

  const [view, setView] = useState<'all' | 'expiring'>('all');
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState('');
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<StaffTravelDocument | null>(null);
  const [loadingEdit, setLoadingEdit] = useState(false);
  const [form, setForm] = useState(blank);

  const countries = useQuery({
    queryKey: ['hr', 'countries', 'active'],
    queryFn: () => countryService.getActive(),
  });

  const documents = useQuery({
    queryKey: ['travel-documents', view, employeeId],
    queryFn: () => (employeeId
      ? travelComplianceService.getDocumentsByEmployee(employeeId)
      : view === 'expiring'
        ? travelComplianceService.getExpiringDocuments(90)
        : travelComplianceService.getDocuments()),
  });
  const rows = (documents.data ?? [])
    .filter((d) => view === 'all' || !d.expiryDate || d.expiryDate.slice(0, 10) <= inMonths(3))
    .slice()
    .sort((a, b) => a.employeeName.localeCompare(b.employeeName) || a.documentTypeName.localeCompare(b.documentTypeName));

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['travel-documents'] });

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        employeeId: editing ? editing.employeeId : form.employeeId,
        documentType: form.documentType,
        documentNumber: form.documentNumber.trim(),
        issuingCountryId: form.issuingCountryId,
        issueDate: form.issueDate || null,
        expiryDate: form.expiryDate || null,
        isPrimary: form.isPrimary,
      };
      return editing
        ? travelComplianceService.updateDocument({ ...payload, id: editing.id })
        : travelComplianceService.createDocument(payload);
    },
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setEditing(null);
      setForm(blank());
      toast({ title: editing ? 'Document changed — it needs verifying again' : 'Document recorded' });
    },
    onError: (e: Error) =>
      toast({ title: 'The document was refused', description: e.message, variant: 'destructive' }),
  });

  const verify = useMutation({
    mutationFn: (id: string) => travelComplianceService.verifyDocument(id),
    onSuccess: async () => { await refresh(); toast({ title: 'Document verified' }); },
    onError: (e: Error) => toast({ title: 'Could not verify it', description: e.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => travelComplianceService.deleteDocument(id),
    onSuccess: async () => { await refresh(); toast({ title: 'Document removed' }); },
    onError: (e: Error) => toast({ title: 'Could not remove it', description: e.message, variant: 'destructive' }),
  });

  const startAdd = () => {
    setEditing(null);
    setForm({ ...blank(), employeeId: employeeId ?? '', employeeLabel });
    setOpen(true);
  };

  // The list shows the number masked; the edit reads the document's own detail, which is in full.
  const startEdit = async (d: StaffTravelDocument) => {
    setLoadingEdit(true);
    try {
      const full = await travelComplianceService.getDocument(d.id);
      setEditing(full);
      setForm({
        employeeId: full.employeeId,
        employeeLabel: full.employeeName,
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

  const canSave = (editing || form.employeeId) && form.documentNumber.trim() && form.issuingCountryId;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Travel documents"
        description="Travellers' passports and other travel documents. The visa lookup and the passport checks read the primary passport; numbers show in full only when a document is opened."
        backHref="/hr/travel"
        actions={canWrite ? (
          <Button size="sm" onClick={startAdd}>
            <Plus className="mr-2 h-4 w-4" /> Add a document
          </Button>
        ) : undefined}
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-4 pt-6">
          <div className="min-w-[16rem] flex-1 space-y-2">
            <Label>Traveller</Label>
            <EmployeePicker
              value={employeeId}
              initialLabel={employeeLabel}
              placeholder="Every traveller"
              onChange={(id, label) => { setEmployeeId(id); setEmployeeLabel(label ?? ''); }}
            />
          </div>
          <div className="space-y-2">
            <Label>Show</Label>
            <Select value={view} onValueChange={(v) => v && setView(v as 'all' | 'expiring')}>
              <SelectTrigger className="w-48"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Every document</SelectItem>
                <SelectItem value="expiring">Expiring within 90 days</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {documents.isError && !documents.data && <TravelQueryError error={documents.error} what="the travel documents" />}
      {documents.isLoading ? (
        <div className="flex justify-center p-10"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>
      ) : rows.length === 0 ? (
        <EmptyState
          icon={IdCard}
          title="No travel documents"
          description={view === 'expiring' ? 'Nothing expires in the next 90 days.' : 'Record a traveller\'s passport to start.'}
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Traveller</TableHead>
                  <TableHead>Document</TableHead>
                  <TableHead>Number</TableHead>
                  <TableHead>Issued by</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead>Verified</TableHead>
                  <TableHead className="w-32" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell className="font-medium">{d.employeeName}</TableCell>
                    <TableCell>
                      {humanize(d.documentTypeName)}
                      {d.isPrimary && <Badge variant="secondary" className="ml-2">primary</Badge>}
                    </TableCell>
                    <TableCell className="font-mono text-sm">{d.documentNumber}</TableCell>
                    <TableCell>{d.issuingCountryName ?? '—'}</TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDate(d.expiryDate)}{expiryBadge(d)}</TableCell>
                    <TableCell className="text-sm">
                      {d.isVerified
                        ? <span className="inline-flex items-center gap-1"><BadgeCheck className="h-4 w-4 text-green-600" />{d.verifiedByName ?? 'Verified'}</span>
                        : <span className="text-muted-foreground">Not yet</span>}
                    </TableCell>
                    <TableCell className="whitespace-nowrap text-right">
                      {canWrite && !d.isVerified && (
                        <Button variant="ghost" size="sm" disabled={verify.isPending} onClick={() => verify.mutate(d.id)}>
                          Verify
                        </Button>
                      )}
                      {canWrite && (
                        <Button variant="ghost" size="icon" aria-label="Change this document" disabled={loadingEdit}
                          onClick={() => startEdit(d)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                      )}
                      {canDelete && !d.isVerified && (
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
                ? 'This document is verified. Saving a change takes the verification off — another look confirms it again.'
                : 'One primary document per type: marking this one primary stands the traveller\'s other one down.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Traveller<span className="ml-0.5 text-red-500">*</span></Label>
              {editing
                ? <p className="text-sm">{editing.employeeName}</p>
                : (
                  <EmployeePicker
                    value={form.employeeId || null}
                    initialLabel={form.employeeLabel}
                    placeholder="Whose document"
                    onChange={(id, label) => setForm((f) => ({ ...f, employeeId: id ?? '', employeeLabel: label ?? '' }))}
                  />
                )}
            </div>
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
                <Label htmlFor="doc-number">Number<span className="ml-0.5 text-red-500">*</span></Label>
                <Input id="doc-number" value={form.documentNumber}
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
                <Label htmlFor="doc-issued">Issued</Label>
                <Input id="doc-issued" type="date" value={form.issueDate}
                  onChange={(e) => setForm((f) => ({ ...f, issueDate: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="doc-expires">Expires</Label>
                <Input id="doc-expires" type="date" value={form.expiryDate}
                  onChange={(e) => setForm((f) => ({ ...f, expiryDate: e.target.value }))} />
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox id="doc-primary" checked={form.isPrimary}
                onCheckedChange={(v) => setForm((f) => ({ ...f, isPrimary: v === true }))} />
              <Label htmlFor="doc-primary">The traveller&apos;s primary document of this type</Label>
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
