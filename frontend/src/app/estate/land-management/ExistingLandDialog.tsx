'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { FilePlus2, Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
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
  name: '', description: '', location: '', purpose: '', zoningClassification: '',
  planningComplianceStatus: '', gisLayerReference: '', cadastreDescription: '', region: '', district: '', town: '',
  areaValue: '', areaUnit: 'sq ft', surveyorName: '', surveyDate: '',
  surveyPlanNumber: '', mapSheetNumber: '', valuationAmount: '', ownerConsiderationCost: '',
  externalSurveyorCost: '', stampDutyCost: '', otherAcquisitionCost: '', currency: 'GHS', notes: '',
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

function toSquareMeters(value: string, unit: string) {
  const area = Number(value);
  if (!Number.isFinite(area) || area <= 0) return 0;

  switch (unit.toLowerCase()) {
    case 'sq ft':
      return area * 0.09290304;
    case 'acres':
      return area * 4046.8564224;
    case 'hectares':
      return area * 10000;
    case 'sqm':
      return area;
    default:
      return 0;
  }
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
  const [saving, setSaving] = React.useState(false);
  const boundaryPreviewCoordinates = React.useMemo(
    () => boundaryCoordinatesFromBeacons(beacons),
    [beacons]
  );
  const areaSquareMeters = React.useMemo(
    () => toSquareMeters(form.areaValue, form.areaUnit),
    [form.areaUnit, form.areaValue]
  );
  const capitalizedCost = React.useMemo(() => {
    const values = [
      form.ownerConsiderationCost,
      form.externalSurveyorCost,
      form.stampDutyCost,
      form.otherAcquisitionCost,
    ].map((value) => Number(value));
    const total = values.reduce((sum, value) => sum + (Number.isFinite(value) && value > 0 ? value : 0), 0);
    return total > 0 ? total : Number(form.valuationAmount);
  }, [form.externalSurveyorCost, form.otherAcquisitionCost, form.ownerConsiderationCost, form.stampDutyCost, form.valuationAmount]);

  const setValue = (key: string, value: string) => setForm((current) => ({ ...current, [key]: value }));
  const reset = () => {
    setForm(initialForm); setBeacons(initialBeacons()); setOwners([initialOwner()]); setDocuments([]);
    setBoundaryVerified(false);
  };

  const missing = React.useMemo(() => {
    const optional = ['description', 'notes', 'ownerConsiderationCost', 'externalSurveyorCost', 'stampDutyCost', 'otherAcquisitionCost'];
    const required = Object.entries(form).filter(([key]) => !optional.includes(key));
    if (required.some(([, value]) => !value.trim())) return true;
    if (areaSquareMeters <= 0) return true;
    if (!Number.isFinite(capitalizedCost) || capitalizedCost <= 0) return true;
    if (beacons.some((item) => !item.beacon.trim() || !item.northing.trim() || !item.easting.trim())) return true;
    if (owners.filter((owner) => owner.isCurrentOwner).length !== 1) return true;
    return owners.some((owner) => !owner.ownerName.trim() || !owner.ownershipType.trim() || !owner.interestHeld.trim() ||
      !owner.identificationType.trim() || !owner.identificationNumber.trim() || !owner.contactNumber.trim() ||
      !owner.address.trim() || !owner.ownershipStartDate || (!owner.isCurrentOwner && !owner.ownershipEndDate) ||
      owner.ownershipPercentage <= 0);
  }, [areaSquareMeters, capitalizedCost, form, beacons, owners]);

  const save = async () => {
    if (missing) return toast.error('Complete all required land, cadastral, beacon, and owner inputs.');
    const boundaryCoordinates = JSON.stringify(beacons.map((item) => ({
      beacon: item.beacon.trim(), northing: Number(item.northing), easting: Number(item.easting),
      bearing: item.bearing.trim() || undefined,
      distance: item.distance.trim() ? Number(item.distance) : undefined,
    })));
    const payload: CreateManualExistingLand = {
      ...(form as unknown as Omit<CreateManualExistingLand, 'areaValue' | 'areaSquareMeters' | 'valuationAmount' | 'beaconCount' | 'boundaryCoordinates' | 'boundaryVerified' | 'isReadyForProjectManagement' | 'ownershipHistory'>),
      areaValue: Number(form.areaValue), areaSquareMeters,
      valuationAmount: capitalizedCost,
      ownerConsiderationCost: Number(form.ownerConsiderationCost) || null,
      externalSurveyorCost: Number(form.externalSurveyorCost) || null,
      stampDutyCost: Number(form.stampDutyCost) || null,
      otherAcquisitionCost: Number(form.otherAcquisitionCost) || null,
      totalCapitalizedCost: capitalizedCost,
      beaconCount: beacons.length, boundaryCoordinates,
      boundaryVerified, isReadyForProjectManagement: false, ownershipHistory: owners,
    };
    try {
      setSaving(true);
      const asset = await estateLandManagementService.createManualLand(payload);
      let primaryListingImageAssigned = false;
      for (const document of documents) {
        const isListingImage = document.documentType === 'Listing Image';
        await estateLandManagementService.uploadDocument(
          asset.id,
          document.file,
          document.documentType,
          document.documentName,
          {
            isListingImage,
            isPrimaryListingImage: isListingImage && !primaryListingImageAssigned,
          }
        );
        primaryListingImageAssigned ||= isListingImage;
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
      <DialogContent className="max-h-[92vh] w-[calc(100vw-2rem)] max-w-6xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add Existing Land</DialogTitle>
          <DialogDescription className="sr-only">
            Register an existing parcel in the Estate Land Bank.
          </DialogDescription>
        </DialogHeader>
        <Tabs defaultValue="asset" className="space-y-4">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="asset">Land Asset</TabsTrigger>
            <TabsTrigger value="survey">Cadastral</TabsTrigger>
            <TabsTrigger value="owners">Owners</TabsTrigger>
            <TabsTrigger value="documents">Documents</TabsTrigger>
          </TabsList>

          <TabsContent value="asset" className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Asset Code</Label>
              <Input value="Generated on save" disabled />
            </div>
            {[
              ['name', 'Land Name'], ['location', 'Location'], ['purpose', 'Purpose'],
              ['zoningClassification', 'Zoning Classification'], ['planningComplianceStatus', 'Planning Compliance Status'],
              ['valuationAmount', 'Fallback Valuation Amount'], ['currency', 'Currency'],
            ].map(([key, label]) => <div key={key} className="space-y-2"><RequiredLabel>{label}</RequiredLabel><Input type={key === 'valuationAmount' ? 'number' : 'text'} value={form[key]} onChange={(event) => setValue(key, event.target.value)} /></div>)}
            <div className="grid gap-4 rounded-md border bg-muted/20 p-4 md:col-span-2 md:grid-cols-2">
              <div className="space-y-1 md:col-span-2">
                <h3 className="text-sm font-semibold">Land cost breakdown</h3>
                <p className="text-xs text-muted-foreground">These values set the capitalized land value used for demarcation costing.</p>
              </div>
              {[
                ['ownerConsiderationCost', 'Owner / Vendor Consideration'],
                ['externalSurveyorCost', 'External Surveyor Cost'],
                ['stampDutyCost', 'Stamp Duty Cost'],
                ['otherAcquisitionCost', 'Other Acquisition Cost'],
              ].map(([key, label]) => (
                <div key={key} className="space-y-2">
                  <Label>{label}</Label>
                  <Input type="number" min="0" step="0.01" value={form[key]} onChange={(event) => setValue(key, event.target.value)} />
                </div>
              ))}
              <div className="space-y-2 md:col-span-2">
                <Label>Total Capitalized Land Cost</Label>
                <Input value={Number.isFinite(capitalizedCost) && capitalizedCost > 0 ? capitalizedCost.toFixed(2) : ''} disabled />
              </div>
            </div>
            <div className="space-y-2 md:col-span-2"><Label>Description</Label><Textarea value={form.description} onChange={(event) => setValue('description', event.target.value)} /></div>
            <div className="space-y-2 md:col-span-2"><Label>Notes</Label><Textarea value={form.notes} onChange={(event) => setValue('notes', event.target.value)} /></div>
          </TabsContent>

          <TabsContent value="survey" className="space-y-5">
            <div className="grid gap-4 md:grid-cols-3">
              {[
                ['cadastreDescription', 'Cadastre Description'], ['region', 'Region'], ['district', 'District'], ['town', 'Town'],
                ['areaValue', 'Survey Area'], ['surveyorName', 'Surveyor Name'], ['surveyDate', 'Survey Date'],
                ['surveyPlanNumber', 'Survey Plan Number'], ['mapSheetNumber', 'Map Sheet Number'],
                ['gisLayerReference', 'GIS Layer Reference'],
              ].map(([key, label]) => <div key={key} className="space-y-2"><RequiredLabel>{label}</RequiredLabel><Input type={key === 'surveyDate' ? 'date' : key === 'areaValue' ? 'number' : 'text'} value={form[key]} onChange={(event) => setValue(key, event.target.value)} /></div>)}
              <div className="space-y-2"><RequiredLabel>Survey Area Unit</RequiredLabel><Select value={form.areaUnit} onValueChange={(value) => setValue('areaUnit', value)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['sq ft', 'acres', 'hectares', 'sqm'].map((unit) => <SelectItem key={unit} value={unit}>{unit}</SelectItem>)}</SelectContent></Select></div>
              <div className="space-y-2"><Label>Calculated Area (sqm)</Label><Input value={areaSquareMeters > 0 ? areaSquareMeters.toFixed(2) : ''} disabled /></div>
            </div>
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[850px] text-sm"><thead className="bg-muted"><tr>{['Beacon index', 'Northing (Y), ft', 'Easting (X), ft', 'Bearing', 'Distance, ft'].map((label) => <th key={label} className="px-3 py-2 text-left font-medium">{label}</th>)}</tr></thead>
                <tbody className="divide-y">{beacons.map((beacon, index) => <tr key={index}>{(['beacon', 'northing', 'easting', 'bearing', 'distance'] as const).map((key) => <td key={key} className="p-2"><Input type={['northing', 'easting', 'distance'].includes(key) ? 'number' : 'text'} value={beacon[key]} onChange={(event) => setBeacons((current) => current.map((item, row) => row === index ? { ...item, [key]: event.target.value } : item))} /></td>)}</tr>)}</tbody>
              </table>
            </div>
            <LandBankMap boundaryCoordinates={boundaryPreviewCoordinates} />
            <div className="flex flex-wrap gap-5">
              <label className="flex items-center gap-2 text-sm"><Checkbox checked={boundaryVerified} onCheckedChange={(checked) => setBoundaryVerified(checked === true)} /> Boundary verified</label>
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
              ].map(([key, label]) => {
                const endDate = key === 'ownershipEndDate';
                const required = !endDate || !owner.isCurrentOwner;
                return <div key={key} className="space-y-2">
                  {required ? <RequiredLabel>{label}</RequiredLabel> : <Label>{label}</Label>}
                  <Input
                    type={key.includes('Date') ? 'date' : key === 'ownershipPercentage' ? 'number' : 'text'}
                    value={`${owner[key as keyof ExistingLandOwner] ?? ''}`}
                    disabled={endDate && owner.isCurrentOwner}
                    onChange={(event) => setOwners((current) => current.map((item, row) => row === index ? { ...item, [key]: key === 'ownershipPercentage' ? Number(event.target.value) : event.target.value } : item))}
                  />
                </div>;
              })}</div>
              <label className="flex items-center gap-2 text-sm"><Checkbox checked={owner.isCurrentOwner} onCheckedChange={(checked) => setOwners((current) => current.map((item, row) => row === index ? { ...item, isCurrentOwner: checked === true, ownershipEndDate: checked === true ? '' : item.ownershipEndDate } : item))} /> Current owner</label>
            </section>)}
            <Button variant="outline" onClick={() => setOwners((current) => [...current, { ...initialOwner(), isCurrentOwner: false }])}><Plus className="mr-2 h-4 w-4" />Add Previous Owner</Button>
          </TabsContent>

          <TabsContent value="documents" className="space-y-4">
            <div className="rounded-md border border-dashed p-5"><RequiredLabel>Land Documents</RequiredLabel><Input className="mt-2" type="file" multiple accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png" onChange={(event) => { const files = Array.from(event.target.files || []); setDocuments((current) => [...current, ...files.map((file) => ({ file, documentType: file.type.startsWith('image/') ? 'Listing Image' : 'Title Document', documentName: file.name }))]); event.currentTarget.value = ''; }} /></div>
            {documents.map((document, index) => <div key={`${document.file.name}-${index}`} className="grid items-end gap-3 rounded-md border p-3 md:grid-cols-[1fr_220px_1fr_auto]">
              <div className="min-w-0"><p className="truncate text-sm font-medium">{document.file.name}</p><p className="text-xs text-muted-foreground">{(document.file.size / 1024).toFixed(1)} KB</p></div>
              <div className="space-y-2"><RequiredLabel>Document Type</RequiredLabel><Select value={document.documentType} onValueChange={(value) => setDocuments((current) => current.map((item, row) => row === index ? { ...item, documentType: value } : item))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Title Document', 'Survey Plan', 'Indenture', 'Allocation Letter', 'Search Report', 'Site Plan', 'Valuation Report', 'Listing Image', 'Other'].map((type) => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select></div>
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
