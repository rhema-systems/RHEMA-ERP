'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { FilePlus2, Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import {
  estateLandManagementService,
  type CreateManualExistingLand,
  type EstateManagedAsset,
  type ExistingLandOwner,
} from '@/services/estate-land-management.service';

type FormState = Record<string, string>;
type Beacon = { beacon: string; northing: string; easting: string; bearing: string; distance: string };
type PendingDocument = { file: File; documentType: string; documentName: string };

const LandBankMap = dynamic(() => import('./LandBankMap'), { ssr: false });

const initialForm: FormState = {
  assetCode: '', name: '', description: '', location: '', purpose: '', zoningClassification: '',
  planningComplianceStatus: '', cadastreDescription: '', region: '', district: '', town: '',
  areaValue: '', areaUnit: 'sq ft', areaSquareMeters: '', surveyorName: '', surveyDate: '',
  surveyPlanNumber: '', mapSheetNumber: '', valuationAmount: '', currency: 'GHS', notes: '',
};

const initialBeacons = (): Beacon[] => Array.from({ length: 4 }, (_, index) => ({
  beacon: `Beacon ${index + 1}`, northing: '', easting: '', bearing: '', distance: '',
}));

const initialOwner = (): ExistingLandOwner => ({
  ownerName: '', ownershipType: '', interestHeld: '', identificationType: '', identificationNumber: '',
  contactNumber: '', address: '', ownershipStartDate: '', ownershipEndDate: '', ownershipPercentage: 100,
  isCurrentOwner: true,
});

function RequiredLabel({ children }: { children: React.ReactNode }) {
  return <Label>{children} <span className="text-destructive">*</span></Label>;
}

function boundaryCoordinatesFromBeacons(beacons: Beacon[]) {
  const points = beacons
    .map((item) => {
      const northing = Number(item.northing);
      const easting = Number(item.easting);
      if (!Number.isFinite(northing) || !Number.isFinite(easting)) return null;

      const distance = Number(item.distance);
      return {
        beacon: item.beacon.trim() || 'Beacon',
        northing,
        easting,
        bearing: item.bearing.trim(),
        distance: Number.isFinite(distance) ? distance : undefined,
      };
    })
    .filter((item): item is NonNullable<typeof item> => Boolean(item));

  return points.length >= 3 ? JSON.stringify(points) : undefined;
}

export default function ExistingLandDialog({
  open,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (asset: EstateManagedAsset) => Promise<void> | void;
}) {
  const [form, setForm] = React.useState<FormState>(initialForm);
  const [beacons, setBeacons] = React.useState<Beacon[]>(initialBeacons);
  const [owners, setOwners] = React.useState<ExistingLandOwner[]>([initialOwner()]);
  const [documents, setDocuments] = React.useState<PendingDocument[]>([]);
  const [boundaryVerified, setBoundaryVerified] = React.useState(false);
  const [ready, setReady] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const boundaryPreviewCoordinates = React.useMemo(
    () => boundaryCoordinatesFromBeacons(beacons),
    [beacons]
  );

  const setValue = (key: string, value: string) => setForm((current) => ({ ...current, [key]: value }));
  const reset = () => {
    setForm(initialForm); setBeacons(initialBeacons()); setOwners([initialOwner()]); setDocuments([]);
    setBoundaryVerified(false); setReady(false);
  };

  const missing = React.useMemo(() => {
    const required = Object.entries(form).filter(([key]) => !['description', 'notes'].includes(key));
    if (required.some(([, value]) => !value.trim())) return true;
    if (beacons.some((item) => Object.values(item).some((value) => !value.trim()))) return true;
    return owners.some((owner) => !owner.ownerName.trim() || !owner.ownershipType.trim() || !owner.interestHeld.trim() ||
      !owner.identificationType.trim() || !owner.identificationNumber.trim() || !owner.contactNumber.trim() ||
      !owner.address.trim() || !owner.ownershipStartDate || owner.ownershipPercentage <= 0);
  }, [form, beacons, owners]);

  const save = async () => {
    if (missing) return toast.error('Complete all required land, cadastral, beacon, and owner inputs.');
    if (ready && !boundaryVerified) return toast.error('Verify the boundary before project handoff.');
    const boundaryCoordinates = JSON.stringify(beacons.map((item) => ({
      beacon: item.beacon.trim(), northing: Number(item.northing), easting: Number(item.easting),
      bearing: item.bearing.trim(), distance: Number(item.distance),
    })));
    const payload: CreateManualExistingLand = {
      ...(form as unknown as Omit<CreateManualExistingLand, 'areaValue' | 'areaSquareMeters' | 'valuationAmount' | 'beaconCount' | 'boundaryCoordinates' | 'boundaryVerified' | 'isReadyForProjectManagement' | 'ownershipHistory'>),
      areaValue: Number(form.areaValue), areaSquareMeters: Number(form.areaSquareMeters),
      valuationAmount: Number(form.valuationAmount), beaconCount: beacons.length, boundaryCoordinates,
      boundaryVerified, isReadyForProjectManagement: ready, ownershipHistory: owners,
    };
    try {
      setSaving(true);
      const asset = await estateLandManagementService.createManualLand(payload);
      for (const document of documents) {
        await estateLandManagementService.uploadDocument(asset.id, document.file, document.documentType, document.documentName);
      }
      toast.success('Existing land added to the Land Bank.');
      await onCreated(asset);
      reset();
      onOpenChange(false);
    } catch (error: any) {
      toast.error(error?.message || 'Unable to add existing land.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!saving) onOpenChange(next); }}>
      <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
        <DialogHeader><DialogTitle>Add Existing Land</DialogTitle></DialogHeader>
        <Tabs defaultValue="asset" className="space-y-4">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="asset">Land Asset</TabsTrigger>
            <TabsTrigger value="survey">Cadastral</TabsTrigger>
            <TabsTrigger value="owners">Owners</TabsTrigger>
            <TabsTrigger value="documents">Documents</TabsTrigger>
          </TabsList>

          <TabsContent value="asset" className="grid gap-4 md:grid-cols-2">
            {[
              ['assetCode', 'Asset Code'], ['name', 'Land Name'], ['location', 'Location'], ['purpose', 'Purpose'],
              ['zoningClassification', 'Zoning Classification'], ['planningComplianceStatus', 'Planning Compliance Status'],
              ['valuationAmount', 'Valuation Amount'], ['currency', 'Currency'], ['areaSquareMeters', 'Area (sqm)'],
            ].map(([key, label]) => <div key={key} className="space-y-2"><RequiredLabel>{label}</RequiredLabel><Input type={['valuationAmount', 'areaSquareMeters'].includes(key) ? 'number' : 'text'} value={form[key]} onChange={(event) => setValue(key, event.target.value)} /></div>)}
            <div className="space-y-2 md:col-span-2"><Label>Description</Label><Textarea value={form.description} onChange={(event) => setValue('description', event.target.value)} /></div>
            <div className="space-y-2 md:col-span-2"><Label>Notes</Label><Textarea value={form.notes} onChange={(event) => setValue('notes', event.target.value)} /></div>
          </TabsContent>

          <TabsContent value="survey" className="space-y-5">
            <div className="grid gap-4 md:grid-cols-3">
              {[
                ['cadastreDescription', 'Cadastre Description'], ['region', 'Region'], ['district', 'District'], ['town', 'Town'],
                ['areaValue', 'Survey Area'], ['surveyorName', 'Surveyor Name'], ['surveyDate', 'Survey Date'],
                ['surveyPlanNumber', 'Survey Plan Number'], ['mapSheetNumber', 'Map Sheet Number'],
              ].map(([key, label]) => <div key={key} className="space-y-2"><RequiredLabel>{label}</RequiredLabel><Input type={key === 'surveyDate' ? 'date' : key === 'areaValue' ? 'number' : 'text'} value={form[key]} onChange={(event) => setValue(key, event.target.value)} /></div>)}
              <div className="space-y-2"><RequiredLabel>Survey Area Unit</RequiredLabel><Select value={form.areaUnit} onValueChange={(value) => setValue('areaUnit', value)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['sq ft', 'acres', 'hectares', 'sqm'].map((unit) => <SelectItem key={unit} value={unit}>{unit}</SelectItem>)}</SelectContent></Select></div>
            </div>
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[850px] text-sm"><thead className="bg-muted"><tr>{['Beacon index', 'Northing (Y), ft', 'Easting (X), ft', 'Bearing', 'Distance, ft'].map((label) => <th key={label} className="px-3 py-2 text-left font-medium">{label}</th>)}</tr></thead>
                <tbody className="divide-y">{beacons.map((beacon, index) => <tr key={index}>{(['beacon', 'northing', 'easting', 'bearing', 'distance'] as const).map((key) => <td key={key} className="p-2"><Input type={['northing', 'easting', 'distance'].includes(key) ? 'number' : 'text'} value={beacon[key]} onChange={(event) => setBeacons((current) => current.map((item, row) => row === index ? { ...item, [key]: event.target.value } : item))} /></td>)}</tr>)}</tbody>
              </table>
            </div>
            <LandBankMap boundaryCoordinates={boundaryPreviewCoordinates} />
            <div className="flex flex-wrap gap-5">
              <label className="flex items-center gap-2 text-sm"><Checkbox checked={boundaryVerified} onCheckedChange={(checked) => setBoundaryVerified(checked === true)} /> Boundary verified</label>
              <label className="flex items-center gap-2 text-sm"><Checkbox checked={ready} onCheckedChange={(checked) => setReady(checked === true)} /> Ready for Project Management</label>
            </div>
          </TabsContent>

          <TabsContent value="owners" className="space-y-4">
            {owners.map((owner, index) => <section key={index} className="space-y-4 rounded-md border p-4">
              <div className="flex items-center justify-between"><h3 className="text-sm font-semibold">Owner {index + 1}</h3>{owners.length > 1 && <Button size="icon" variant="ghost" onClick={() => setOwners((current) => current.filter((_, row) => row !== index))}><Trash2 className="h-4 w-4" /></Button>}</div>
              <div className="grid gap-4 md:grid-cols-3">{[
                ['ownerName', 'Owner Name'], ['ownershipType', 'Ownership Type'], ['interestHeld', 'Interest Held'],
                ['identificationType', 'Identification Type'], ['identificationNumber', 'Identification Number'],
                ['contactNumber', 'Contact Number'], ['address', 'Address'], ['ownershipStartDate', 'Start Date'],
                ['ownershipEndDate', 'End Date'], ['ownershipPercentage', 'Ownership Percentage'],
              ].map(([key, label]) => <div key={key} className="space-y-2"><RequiredLabel>{label}</RequiredLabel><Input type={key.includes('Date') ? 'date' : key === 'ownershipPercentage' ? 'number' : 'text'} value={`${owner[key as keyof ExistingLandOwner] ?? ''}`} onChange={(event) => setOwners((current) => current.map((item, row) => row === index ? { ...item, [key]: key === 'ownershipPercentage' ? Number(event.target.value) : event.target.value } : item))} /></div>)}</div>
              <label className="flex items-center gap-2 text-sm"><Checkbox checked={owner.isCurrentOwner} onCheckedChange={(checked) => setOwners((current) => current.map((item, row) => row === index ? { ...item, isCurrentOwner: checked === true } : item))} /> Current owner</label>
            </section>)}
            <Button variant="outline" onClick={() => setOwners((current) => [...current, { ...initialOwner(), isCurrentOwner: false }])}><Plus className="mr-2 h-4 w-4" />Add Previous Owner</Button>
          </TabsContent>

          <TabsContent value="documents" className="space-y-4">
            <div className="rounded-md border border-dashed p-5"><RequiredLabel>Land Documents</RequiredLabel><Input className="mt-2" type="file" multiple accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png" onChange={(event) => { const files = Array.from(event.target.files || []); setDocuments((current) => [...current, ...files.map((file) => ({ file, documentType: 'Title Document', documentName: file.name }))]); event.currentTarget.value = ''; }} /></div>
            {documents.map((document, index) => <div key={`${document.file.name}-${index}`} className="grid items-end gap-3 rounded-md border p-3 md:grid-cols-[1fr_220px_1fr_auto]">
              <div className="min-w-0"><p className="truncate text-sm font-medium">{document.file.name}</p><p className="text-xs text-muted-foreground">{(document.file.size / 1024).toFixed(1)} KB</p></div>
              <div className="space-y-2"><RequiredLabel>Document Type</RequiredLabel><Select value={document.documentType} onValueChange={(value) => setDocuments((current) => current.map((item, row) => row === index ? { ...item, documentType: value } : item))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Title Document', 'Survey Plan', 'Indenture', 'Allocation Letter', 'Search Report', 'Site Plan', 'Valuation Report', 'Other'].map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select></div>
              <div className="space-y-2"><RequiredLabel>Document Name</RequiredLabel><Input value={document.documentName} onChange={(event) => setDocuments((current) => current.map((item, row) => row === index ? { ...item, documentName: event.target.value } : item))} /></div>
              <Button size="icon" variant="ghost" onClick={() => setDocuments((current) => current.filter((_, row) => row !== index))}><Trash2 className="h-4 w-4" /></Button>
            </div>)}
            {documents.length === 0 && <div className="flex items-center gap-2 text-sm text-muted-foreground"><FilePlus2 className="h-4 w-4" />Add the title, survey, and supporting land documents before saving.</div>}
          </TabsContent>
        </Tabs>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={save} disabled={saving || missing || documents.length === 0} title={missing || documents.length === 0 ? 'Complete every required input and attach at least one land document.' : undefined}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}Save Existing Land
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
