'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Award, Download, FileText, Plus, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';

import { SupplierBankAccountsPanel } from '@/components/procurement/SupplierContactBankDetails';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import type { SupplierBankAccountDetails } from '@/lib/supplier-registration-details';
import {
  businessPartnerService,
  type BusinessPartnerDocumentDto,
  type BusinessPartnerLicenseDto,
} from '@/services/businessPartnerService';
import { licenseTypeService, type LicenseTypeDto } from '@/services/partnerConfigService';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

export function BusinessPartnerBankAccountsEditPanel({
  partnerId,
  accounts,
}: {
  partnerId: string;
  accounts: SupplierBankAccountDetails[];
}) {
  return (
    <div className="space-y-4">
      <Card className="border-blue-200 bg-blue-50/40">
        <CardContent className="flex flex-col justify-between gap-3 p-4 sm:flex-row sm:items-center">
          <div className="flex gap-3">
            <ShieldCheck className="mt-0.5 h-5 w-5 text-blue-700" />
            <div>
              <p className="font-medium">Bank detail changes require independent approval</p>
              <p className="text-sm text-muted-foreground">
                Review the current accounts here, then stage the required change through the supplier master change workflow.
              </p>
            </div>
          </div>
          <Button asChild type="button">
            <Link href={`/administration/procurement/supplier-master-changes?partnerId=${encodeURIComponent(partnerId)}&resourceType=SupplierBankDetails&new=1`}>
              Request bank detail change
            </Link>
          </Button>
        </CardContent>
      </Card>
      <SupplierBankAccountsPanel accounts={accounts} />
    </div>
  );
}

export function BusinessPartnerDocumentsEditPanel({
  partnerId,
  documents,
}: {
  partnerId: string;
  documents: BusinessPartnerDocumentDto[];
}) {
  const download = async (document: BusinessPartnerDocumentDto) => {
    try {
      const token = localStorage.getItem('token') || localStorage.getItem('authToken');
      const response = await fetch(
        `${API_BASE_URL}/procurement/business-partners/${partnerId}/documents/${document.id}/download`,
        { headers: { ...(token && { Authorization: `Bearer ${token}` }) } }
      );
      if (!response.ok) throw new Error('The document could not be downloaded.');
      const url = URL.createObjectURL(await response.blob());
      const anchor = window.document.createElement('a');
      anchor.href = url;
      anchor.download = document.documentName || 'business-partner-document';
      window.document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      toast.success('Document downloaded.');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'The document could not be downloaded.');
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2"><FileText className="h-5 w-5" /> Documents</CardTitle>
        <p className="text-sm text-muted-foreground">
          Approved onboarding evidence remains controlled by the supplier registration and document review workflow.
        </p>
      </CardHeader>
      <CardContent>
        {documents.length === 0 ? (
          <p className="py-8 text-center text-muted-foreground">No documents are linked to this business partner.</p>
        ) : (
          <div className="space-y-3">
            {documents.map((document) => (
              <div key={document.id} className="flex flex-col justify-between gap-3 rounded-lg border p-4 sm:flex-row sm:items-center">
                <div>
                  <p className="font-medium">{document.documentType}</p>
                  <p className="text-sm text-muted-foreground">{document.documentName}</p>
                </div>
                <div className="flex items-center gap-2">
                  {document.isVerified && <Badge>Verified</Badge>}
                  {document.filePath && (
                    <Button type="button" size="sm" variant="outline" onClick={() => void download(document)}>
                      <Download className="mr-2 h-4 w-4" /> Download
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

type LicenseDraft = {
  licenseTypeId: string;
  licenseNumber: string;
  issuingAuthority: string;
  issueDate: string;
  expiryDate: string;
};

const emptyLicense = (): LicenseDraft => ({
  licenseTypeId: '',
  licenseNumber: '',
  issuingAuthority: '',
  issueDate: '',
  expiryDate: '',
});

export function BusinessPartnerLicensesManager({
  partnerId,
  licenses,
  onLicensesChange,
}: {
  partnerId: string;
  licenses: BusinessPartnerLicenseDto[];
  onLicensesChange: (licenses: BusinessPartnerLicenseDto[]) => void;
}) {
  const [types, setTypes] = useState<LicenseTypeDto[]>([]);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [draft, setDraft] = useState<LicenseDraft>(emptyLicense);

  useEffect(() => {
    void licenseTypeService.getActive().then(setTypes).catch(() => setTypes([]));
  }, []);

  const save = async () => {
    if (!draft.licenseTypeId || !draft.licenseNumber.trim() || !draft.issuingAuthority.trim() || !draft.issueDate) {
      toast.error('Select the licence type and enter its number, authority, and issue date.');
      return;
    }
    try {
      setBusy(true);
      const saved = await businessPartnerService.addPartnerLicense(partnerId, {
        licenseTypeId: draft.licenseTypeId,
        licenseNumber: draft.licenseNumber.trim(),
        issuingAuthority: draft.issuingAuthority.trim(),
        issueDate: draft.issueDate,
        expiryDate: draft.expiryDate || undefined,
      });
      onLicensesChange([...licenses, saved]);
      setOpen(false);
      setDraft(emptyLicense());
      toast.success('Licence added.');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to add licence.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <div>
          <CardTitle className="flex items-center gap-2"><Award className="h-5 w-5" /> Licences &amp; Certifications</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">Add approved licence records to this business partner.</p>
        </div>
        <Button type="button" variant="outline" onClick={() => setOpen(true)}>
          <Plus className="mr-2 h-4 w-4" /> Add licence
        </Button>
      </CardHeader>
      <CardContent>
        {licenses.length === 0 ? (
          <p className="py-8 text-center text-muted-foreground">No licences are recorded.</p>
        ) : (
          <div className="space-y-3">
            {licenses.map((license) => (
              <div key={license.id} className="rounded-lg border p-4">
                <p className="font-medium">{license.licenseTypeName || types.find((item) => item.id === license.licenseTypeId)?.licenseName || 'Licence'}</p>
                <p className="text-sm text-muted-foreground">
                  {[license.licenseNumber, license.issuingAuthority, license.expiryDate ? `Expires ${new Date(license.expiryDate).toLocaleDateString()}` : undefined]
                    .filter(Boolean)
                    .join(' · ')}
                </p>
              </div>
            ))}
          </div>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add licence</DialogTitle>
            <DialogDescription>Record a licence or certification against this business partner.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <div className="space-y-1.5 sm:col-span-2">
              <Label htmlFor="bp-license-type">Licence type</Label>
              <Select value={draft.licenseTypeId} onValueChange={(licenseTypeId) => setDraft((current) => ({ ...current, licenseTypeId }))}>
                <SelectTrigger id="bp-license-type"><SelectValue placeholder="Select licence type" /></SelectTrigger>
                <SelectContent>{types.map((type) => <SelectItem key={type.id} value={type.id}>{type.licenseName}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <LicenseField id="bp-license-number" label="Licence number" value={draft.licenseNumber} onChange={(licenseNumber) => setDraft((current) => ({ ...current, licenseNumber }))} />
            <LicenseField id="bp-license-authority" label="Issuing authority" value={draft.issuingAuthority} onChange={(issuingAuthority) => setDraft((current) => ({ ...current, issuingAuthority }))} />
            <LicenseField id="bp-license-issue" label="Issue date" type="date" value={draft.issueDate} onChange={(issueDate) => setDraft((current) => ({ ...current, issueDate }))} />
            <LicenseField id="bp-license-expiry" label="Expiry date" type="date" value={draft.expiryDate} onChange={(expiryDate) => setDraft((current) => ({ ...current, expiryDate }))} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={busy}>Cancel</Button>
            <Button type="button" onClick={() => void save()} disabled={busy}>{busy ? 'Saving...' : 'Add licence'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

function LicenseField({ id, label, value, type = 'text', onChange }: { id: string; label: string; value: string; type?: string; onChange: (value: string) => void }) {
  return <div className="space-y-1.5"><Label htmlFor={id}>{label}</Label><Input id={id} type={type} value={value} onChange={(event) => onChange(event.target.value)} /></div>;
}
