'use client';

import React from 'react';
import { Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { Plus, Search, Eye, Truck, CheckCircle2, Trash2, Droplet, ReceiptText, Hotel, Paperclip } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/hooks/use-toast';

import MaintenanceAttachmentsPanel from '@/components/maintenance/MaintenanceAttachmentsPanel';
import fleetService, {
  CompleteFleetTripInspectionDto,
  CreateFleetCostEntryDto,
  CreateFleetFuelTransactionDto,
  CreateFleetTripDto,
  DispatchFleetTripDto,
  FleetCostEntryDto,
  FleetTripDestinationDto,
  FleetTripDto,
  FleetTripInspectionDto,
  FleetVehicleListDto,
  PagedResult,
  StartFleetTripInspectionDto,
} from '@/services/fleetService';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';
import { formatFleetDateTime } from '@/lib/date-format';
import maintenanceSettingsService from '@/services/maintenanceSettingsService';
import { MaintenanceAttachmentEntityType } from '@/services/maintenanceAttachmentsService';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

type EmployeeDto = {
  id: string;
  firstName?: string | null;
  lastName?: string | null;
  employeeNumber?: string | null;
  status?: string | null;
};

type InspectionTemplateLite = {
  id: string;
  name: string;
  category?: string | null;
  isActive?: boolean;
};

type InspectionChecklistItem = {
  id: string;
  item: string;
  type: string;
  required: boolean;
  order: number;
};

type InspectionTemplateDetail = {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  category?: string | null;
  frequency?: string | null;
  estimatedDuration?: number | null;
  isActive?: boolean;
  checklistItems: InspectionChecklistItem[];
};

function toDatetimeLocal(value: Date) {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

function getTripStatusBadge(statusRaw: string | null | undefined) {
  const status = (statusRaw || '').trim() || 'Unknown';
  const colors: Record<string, string> = {
    Draft: 'bg-gray-100 text-gray-800',
    Submitted: 'bg-blue-100 text-blue-800',
    Approved: 'bg-purple-100 text-purple-800',
    Rejected: 'bg-red-100 text-red-800',
    Dispatched: 'bg-yellow-100 text-yellow-800',
    Completed: 'bg-green-100 text-green-800',
    Cancelled: 'bg-gray-100 text-gray-800',
    Unknown: 'bg-gray-100 text-gray-800',
  };

  return <Badge className={colors[status] ?? 'bg-gray-100 text-gray-800'}>{status}</Badge>;
}

export default function FleetTripsPage() {
  return (
    <Suspense fallback={<div className="p-6 text-muted-foreground">Loading...</div>}>
      <FleetTripsPageContent />
    </Suspense>
  );
}

function FleetTripsPageContent() {
  const { toast } = useToast();
  const searchParams = useSearchParams();
  const initialOpenId = searchParams?.get('id');

  const [loading, setLoading] = React.useState(true);
  const [vehicles, setVehicles] = React.useState<FleetVehicleListDto[]>([]);
  const [employees, setEmployees] = React.useState<EmployeeDto[]>([]);
  const [tripDestinations, setTripDestinations] = React.useState<FleetTripDestinationDto[]>([]);
  const [tripDestinationById, setTripDestinationById] = React.useState<Record<string, FleetTripDestinationDto>>({});
  const [requirePredefinedDestinationOnDispatch, setRequirePredefinedDestinationOnDispatch] = React.useState(false);

  const [page, setPage] = React.useState(1);
  const [pageSize] = React.useState(25);
  const [searchTerm, setSearchTerm] = React.useState('');
  const [status, setStatus] = React.useState<string>('all');

  const [result, setResult] = React.useState<PagedResult<FleetTripDto>>({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize,
  });

  const [createOpen, setCreateOpen] = React.useState(false);
  const [viewOpen, setViewOpen] = React.useState(false);
  const [tripDetailsTab, setTripDetailsTab] = React.useState<'details' | 'expenses' | 'approvals'>('details');
  const [selected, setSelected] = React.useState<FleetTripDto | null>(null);
  const [assignDriverOpen, setAssignDriverOpen] = React.useState(false);
  const [assignDriverId, setAssignDriverId] = React.useState<string>('none');

  const [inspections, setInspections] = React.useState<FleetTripInspectionDto[]>([]);
  const [inspectionTemplates, setInspectionTemplates] = React.useState<InspectionTemplateLite[]>([]);
  const [inspectionsLoading, setInspectionsLoading] = React.useState(false);

  const [startInspectionOpen, setStartInspectionOpen] = React.useState(false);
  const [completeInspectionOpen, setCompleteInspectionOpen] = React.useState(false);
  const [selectedInspection, setSelectedInspection] = React.useState<FleetTripInspectionDto | null>(null);
  const [selectedInspectionTemplate, setSelectedInspectionTemplate] = React.useState<InspectionTemplateDetail | null>(null);
  const [inspectionAnswers, setInspectionAnswers] = React.useState<Record<string, string>>({});
  const [startInspectionForm, setStartInspectionForm] = React.useState<StartFleetTripInspectionDto>({
    fleetTripId: '',
    inspectionTemplateId: '',
    inspectorEmployeeId: null,
    inspectionKind: 'PreTrip',
  });
  const [completeInspectionForm, setCompleteInspectionForm] = React.useState<CompleteFleetTripInspectionDto>({
    completedAtUtc: null,
    overallResult: 'Pass',
    inspectionData: '{}',
    notes: '',
  });
  const inspectionReadOnly = !!selectedInspection && selectedInspection.status !== 'InProgress';

  const [tripCostsLoading, setTripCostsLoading] = React.useState(false);
  const [tripCosts, setTripCosts] = React.useState<FleetCostEntryDto[]>([]);

  const [addFuelOpen, setAddFuelOpen] = React.useState(false);
  const [addExpenseOpen, setAddExpenseOpen] = React.useState(false);
  const [savingExpense, setSavingExpense] = React.useState(false);

  const [fuelForm, setFuelForm] = React.useState<CreateFleetFuelTransactionDto>({
    vehicleAssetId: '',
    fleetTripId: null,
    fuelledAt: new Date().toISOString(),
    quantity: 0,
    unit: 'L',
    unitCost: null,
    mileageAtFuel: null,
    operatingHoursAtFuel: null,
    vendorName: null,
    receiptReference: null,
    notes: null,
  });

  const [expenseForm, setExpenseForm] = React.useState<CreateFleetCostEntryDto>({
    fleetTripId: null,
    vehicleAssetId: null,
    costDateUtc: new Date().toISOString(),
    costType: 'Other',
    amount: 0,
    currencyCode: null,
    notes: null,
  });

  const [expenseDocsOpen, setExpenseDocsOpen] = React.useState(false);
  const [expenseDocsEntityType, setExpenseDocsEntityType] = React.useState<MaintenanceAttachmentEntityType>('FleetCostEntry');
  const [expenseDocsEntityId, setExpenseDocsEntityId] = React.useState<string>('');
  const [expenseDocsCategory, setExpenseDocsCategory] = React.useState<string>('TripExpenseReceipt');
  const [expenseDocsTitle, setExpenseDocsTitle] = React.useState<string>('Expense Documents');

  const loadInspectionTemplate = React.useCallback(
    async (templateId: string) => {
      if (!templateId) return null;
      const res = await fetch(`${API_BASE_URL}/inspection-templates/${templateId}`, { headers: getAuthHeaders() });
      if (!res.ok) throw new Error(await res.text());
      const t: InspectionTemplateDetail = await res.json();
      t.checklistItems = (t.checklistItems || []).slice().sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
      return t;
    },
    []
  );

  function buildInspectionDataJson(template: InspectionTemplateDetail | null, answers: Record<string, string>) {
    const payload = {
      schema: 'FleetTripInspectionChecklist.v1',
      templateId: template?.id || null,
      templateCode: template?.code || null,
      checklist: (template?.checklistItems || []).map((i) => ({
        id: i.id,
        item: i.item,
        type: i.type,
        required: !!i.required,
        order: i.order,
        value: answers[i.id] ?? '',
      })),
    };
    return JSON.stringify(payload);
  }

  function deriveOverallResult(template: InspectionTemplateDetail | null, answers: Record<string, string>) {
    const items = template?.checklistItems || [];
    const anyFail = items.some((i) => (answers[i.id] || '').toLowerCase() === 'fail');
    return anyFail ? 'Fail' : 'Pass';
  }

  React.useEffect(() => {
    if (!completeInspectionOpen || !selectedInspection) return;
    (async () => {
      try {
        const template = await loadInspectionTemplate(selectedInspection.inspectionTemplateId);
        setSelectedInspectionTemplate(template);

        // Attempt to hydrate answers from stored inspectionData if it matches our schema.
        const nextAnswers: Record<string, string> = {};
        try {
          const raw = selectedInspection.inspectionData || '{}';
          const parsed = JSON.parse(raw);
          if (parsed && parsed.schema === 'FleetTripInspectionChecklist.v1' && Array.isArray(parsed.checklist)) {
            for (const row of parsed.checklist) {
              if (!row?.id) continue;
              nextAnswers[String(row.id)] = row.value != null ? String(row.value) : '';
            }
          }
        } catch {
          // ignore
        }

        setInspectionAnswers(nextAnswers);
        setCompleteInspectionForm((p) => ({
          ...p,
          inspectionData: buildInspectionDataJson(template, nextAnswers),
          overallResult: deriveOverallResult(template, nextAnswers),
        }));
      } catch (e: any) {
        setSelectedInspectionTemplate(null);
        setInspectionAnswers({});
        toast({ title: 'Failed to load inspection template', description: e?.message || String(e), variant: 'destructive' });
      }
    })();
  }, [completeInspectionOpen, selectedInspection?.inspectionTemplateId]);

  const [dispatchOpen, setDispatchOpen] = React.useState(false);
  const [dispatchForm, setDispatchForm] = React.useState<DispatchFleetTripDto>({
    dispatchedAt: toDatetimeLocal(new Date()),
    startMileage: null,
    startOperatingHours: null,
  });

  const [completeOpen, setCompleteOpen] = React.useState(false);
  const [completeForm, setCompleteForm] = React.useState({
    completedAt: toDatetimeLocal(new Date()),
    endMileage: null as number | null,
    notes: '' as string,
  });

  const [cancelOpen, setCancelOpen] = React.useState(false);
  const [cancelReason, setCancelReason] = React.useState('');

  const [createForm, setCreateForm] = React.useState<CreateFleetTripDto>({
    vehicleAssetId: '',
    driverEmployeeId: null,
    purpose: '',
    origin: '',
    destination: '',
    fleetTripDestinationId: null,
    notes: '',
    plannedStartAt: null,
    plannedEndAt: null,
  });

  const loadLookups = React.useCallback(async () => {
    try {
      const [vRes, eRes, dests] = await Promise.all([
        fleetService.getVehicles({ page: 1, pageSize: 100, assetType: 'Vehicle' }),
        fetch(`${API_BASE_URL}/employees?page=1&pageSize=100`, { headers: getAuthHeaders() }),
        fleetService.getTripDestinations({ activeOnly: true }),
      ]);

      const activeVehicles = (vRes.items || []).filter((v) => (v.status || '').toLowerCase() === 'active');
      setVehicles(activeVehicles);

      if (!eRes.ok) throw new Error(await eRes.text());
      const rawEmployees: EmployeeDto[] = await eRes.json();
      const activeEmployees = (rawEmployees || []).filter((e) => (e.status || '').toLowerCase() !== 'inactive');
      setEmployees(activeEmployees);

      setTripDestinations((dests || []).filter((d) => d.isActive !== false));

      // Maintenance settings (for dispatch enforcement)
      try {
        const settings = await maintenanceSettingsService.getSettings();
        setRequirePredefinedDestinationOnDispatch(!!settings.requirePredefinedFleetTripDestinationOnDispatch);
      } catch {
        setRequirePredefinedDestinationOnDispatch(false);
      }

      // Inspection templates (for fleet pre/post inspections)
      try {
        const tRes = await fetch(`${API_BASE_URL}/inspection-templates?activeOnly=true`, { headers: getAuthHeaders() });
        if (tRes.ok) {
          const templates: InspectionTemplateLite[] = await tRes.json();
          setInspectionTemplates((templates || []).filter((t) => t.isActive !== false));
        }
      } catch {
        setInspectionTemplates([]);
      }
    } catch (e: any) {
      toast({ title: 'Failed to load lookups', description: e?.message || String(e), variant: 'destructive' });
    }
  }, [toast]);

  const loadTrips = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await fleetService.getTrips({
        page,
        pageSize,
        searchTerm: searchTerm.trim() || undefined,
        status: status !== 'all' ? status : undefined,
      });
      setResult(res);
    } catch (e: any) {
      toast({ title: 'Failed to load trips', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, searchTerm, status, toast]);

  React.useEffect(() => {
    loadLookups();
  }, []);

  React.useEffect(() => {
    loadTrips();
  }, [loadTrips]);

  React.useEffect(() => {
    if (!initialOpenId) return;
    (async () => {
      try {
        const trip = await fleetService.getTrip(initialOpenId);
        setSelected(trip);
        setTripDetailsTab('details');
        setViewOpen(true);
      } catch {
        // Ignore
      }
    })();
  }, [initialOpenId]);

  React.useEffect(() => {
    if (!viewOpen || !selected?.id) return;
    (async () => {
      setInspectionsLoading(true);
      try {
        const res = await fleetService.getTripInspections(selected.id);
        setInspections(res || []);
      } catch {
        setInspections([]);
      } finally {
        setInspectionsLoading(false);
      }
    })();
  }, [viewOpen, selected?.id]);

  const loadTripCosts = React.useCallback(async () => {
    if (!viewOpen || !selected?.id) return;
    setTripCostsLoading(true);
    try {
      const res = await fleetService.getTripCosts(selected.id, 1, 200);
      setTripCosts(res.items || []);
    } catch {
      setTripCosts([]);
    } finally {
      setTripCostsLoading(false);
    }
  }, [viewOpen, selected?.id]);

  React.useEffect(() => {
    loadTripCosts();
  }, [loadTripCosts]);

  React.useEffect(() => {
    if (!viewOpen) return;
    if (!selected?.fleetTripDestinationId) return;

    const id = selected.fleetTripDestinationId;
    if (tripDestinationById[id]) return;
    if (tripDestinations.some((d) => d.id === id)) return;

    (async () => {
      try {
        const d = await fleetService.getTripDestination(id);
        setTripDestinationById((p) => ({ ...p, [id]: d }));
      } catch {
        // ignore - we just won't show template expected values
      }
    })();
  }, [viewOpen, selected?.fleetTripDestinationId, tripDestinationById, tripDestinations]);

  const openView = async (id: string) => {
    try {
      const trip = await fleetService.getTrip(id);
      setSelected(trip);
      setTripDetailsTab('details');
      setViewOpen(true);
    } catch (e: any) {
      toast({ title: 'Failed to load trip', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const refreshSelected = async () => {
    if (!selected) return;
    const trip = await fleetService.getTrip(selected.id);
    setSelected(trip);
  };

  const openAddFuel = () => {
    if (!selected) return;
    setFuelForm({
      vehicleAssetId: selected.vehicleAssetId,
      fleetTripId: selected.id,
      fuelledAt: new Date().toISOString(),
      quantity: 0,
      unit: 'L',
      unitCost: null,
      mileageAtFuel: selected.startMileage ?? null,
      operatingHoursAtFuel: selected.startOperatingHours ?? null,
      vendorName: null,
      receiptReference: null,
      notes: null,
    });
    setAddFuelOpen(true);
  };

  const openAddExpense = (presetType: string) => {
    if (!selected) return;
    setExpenseForm({
      fleetTripId: selected.id,
      vehicleAssetId: null,
      costDateUtc: new Date().toISOString(),
      costType: presetType,
      amount: 0,
      currencyCode: null,
      notes: null,
    });
    setAddExpenseOpen(true);
  };

  const saveFuelExpense = async () => {
    if (!selected) return;
    try {
      setSavingExpense(true);
      if (!fuelForm.quantity || fuelForm.quantity <= 0) throw new Error('Quantity must be greater than 0');

      const dto: CreateFleetFuelTransactionDto = {
        ...fuelForm,
        vehicleAssetId: selected.vehicleAssetId,
        fleetTripId: selected.id,
        fuelledAt: fuelForm.fuelledAt ? new Date(fuelForm.fuelledAt).toISOString() : new Date().toISOString(),
        unit: fuelForm.unit?.trim() || 'L',
        vendorName: fuelForm.vendorName?.trim() || null,
        receiptReference: fuelForm.receiptReference?.trim() || null,
        notes: fuelForm.notes?.trim() || null,
        unitCost: fuelForm.unitCost != null && Number.isFinite(Number(fuelForm.unitCost)) ? Number(fuelForm.unitCost) : null,
      };

      await fleetService.createFuel(dto);
      toast({ title: 'Fuel expense added' });
      setAddFuelOpen(false);
      await loadTripCosts();
    } catch (e: any) {
      toast({ title: 'Failed to add fuel expense', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setSavingExpense(false);
    }
  };

  const saveTripExpense = async () => {
    if (!selected) return;
    try {
      setSavingExpense(true);
      if (!expenseForm.costType?.trim()) throw new Error('Expense type is required');
      if (!expenseForm.amount || expenseForm.amount <= 0) throw new Error('Amount must be greater than 0');

      const dto: CreateFleetCostEntryDto = {
        fleetTripId: selected.id,
        vehicleAssetId: null,
        costDateUtc: expenseForm.costDateUtc ? new Date(expenseForm.costDateUtc).toISOString() : new Date().toISOString(),
        costType: expenseForm.costType.trim(),
        source: 'TripExpense',
        amount: Number(expenseForm.amount),
        currencyCode: expenseForm.currencyCode?.trim() || null,
        notes: expenseForm.notes?.trim() || null,
      };

      await fleetService.createCost(dto);
      toast({ title: 'Trip expense added' });
      setAddExpenseOpen(false);
      await loadTripCosts();
    } catch (e: any) {
      toast({ title: 'Failed to add expense', description: e?.message || String(e), variant: 'destructive' });
    } finally {
      setSavingExpense(false);
    }
  };

  const deleteTripCostRow = async (row: FleetCostEntryDto) => {
    try {
      if (row.fleetFuelTransactionId) {
        await fleetService.deleteFuel(row.fleetFuelTransactionId);
      } else {
        await fleetService.deleteCost(row.id);
      }
      toast({ title: 'Deleted' });
      await loadTripCosts();
    } catch (e: any) {
      toast({ title: 'Delete failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const openExpenseDocs = (row: FleetCostEntryDto) => {
    const isFuel = !!row.fleetFuelTransactionId;
    const entityType: MaintenanceAttachmentEntityType = isFuel ? 'FleetFuelTransaction' : 'FleetCostEntry';
    const entityId = isFuel ? row.fleetFuelTransactionId || '' : row.id;

    setExpenseDocsEntityType(entityType);
    setExpenseDocsEntityId(entityId);
    setExpenseDocsCategory(isFuel ? 'FuelReceipt' : 'TripExpenseReceipt');
    setExpenseDocsTitle(`${row.costType || 'Expense'} Documents`);
    setExpenseDocsOpen(true);
  };

  const onAssignDriver = async () => {
    if (!selected) return;
    try {
      const updated = await fleetService.updateTrip(selected.id, {
        vehicleAssetId: selected.vehicleAssetId,
        driverEmployeeId: assignDriverId === 'none' ? null : assignDriverId,
      });
      toast({ title: 'Driver updated' });
      setSelected(updated);
      setAssignDriverOpen(false);
      await loadTrips();
    } catch (e: any) {
      toast({ title: 'Failed to update driver', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const onCreate = async () => {
    try {
      if (!createForm.vehicleAssetId) throw new Error('Vehicle is required');
      if (requirePredefinedDestinationOnDispatch && !createForm.fleetTripDestinationId) {
        throw new Error('Trip destination is required (per Maintenance settings)');
      }
      const created = await fleetService.createTrip({
        ...createForm,
        purpose: createForm.purpose?.trim() || null,
        origin: createForm.origin?.trim() || null,
        destination: createForm.destination?.trim() || null,
        notes: createForm.notes?.trim() || null,
      });

      toast({ title: 'Trip created' });
      setCreateOpen(false);
      setCreateForm({
        vehicleAssetId: '',
        driverEmployeeId: null,
        purpose: '',
        origin: '',
        destination: '',
        fleetTripDestinationId: null,
        notes: '',
        plannedStartAt: null,
        plannedEndAt: null,
      });
      await loadTrips();
      setSelected(created);
      setViewOpen(true);
    } catch (e: any) {
      toast({ title: 'Failed to create trip', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const onDispatch = async () => {
    if (!selected) return;
    try {
      if (requirePredefinedDestinationOnDispatch && !selected.fleetTripDestinationId) {
        toast({
          title: 'Trip destination required',
          description: 'Trip destination must be selected on the trip before dispatch (per Maintenance settings).',
          variant: 'destructive',
        });
        return;
      }

      const dto: DispatchFleetTripDto = {
        dispatchedAt: dispatchForm.dispatchedAt ? new Date(dispatchForm.dispatchedAt).toISOString() : null,
        startMileage: dispatchForm.startMileage ?? null,
        startOperatingHours: dispatchForm.startOperatingHours ?? null,
      };
      const updated = await fleetService.dispatchTrip(selected.id, dto);
      toast({ title: 'Trip dispatched' });
      setSelected(updated);
      setDispatchOpen(false);
      await loadTrips();
    } catch (e: any) {
      toast({ title: 'Dispatch failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const onComplete = async () => {
    if (!selected) return;
    try {
      const dto = {
        completedAt: completeForm.completedAt ? new Date(completeForm.completedAt).toISOString() : null,
        endMileage: completeForm.endMileage ?? null,
        notes: completeForm.notes?.trim() || null,
      };
      const updated = await fleetService.completeTrip(selected.id, dto);
      toast({ title: 'Trip completed' });
      setSelected(updated);
      setCompleteOpen(false);
      await loadTrips();
    } catch (e: any) {
      toast({ title: 'Complete failed', description: e?.message || String(e), variant: 'destructive' });
    }
  };

  const totalPages = Math.max(1, Math.ceil((result.totalCount || 0) / pageSize));

  const selectedDestinationTemplate = selected?.fleetTripDestinationId
    ? tripDestinations.find((d) => d.id === selected.fleetTripDestinationId) ?? tripDestinationById[selected.fleetTripDestinationId] ?? null
    : null;

  const tripDestinationLabel = selected?.fleetTripDestinationName ?? selectedDestinationTemplate?.name ?? null;

  const plannedExpectedHours =
    selected?.plannedStartAt && selected?.plannedEndAt
      ? (() => {
          const start = new Date(selected.plannedStartAt).getTime();
          const end = new Date(selected.plannedEndAt).getTime();
          if (!Number.isFinite(start) || !Number.isFinite(end)) return null;
          const hours = (end - start) / 36e5;
          if (!Number.isFinite(hours) || hours < 0) return null;
          return Math.round(hours * 10) / 10;
        })()
      : null;

  const selectedExpectedHours = selected?.expectedHours ?? selectedDestinationTemplate?.expectedHours ?? plannedExpectedHours ?? null;
  const selectedExpectedMileage = selected?.expectedMileage ?? selectedDestinationTemplate?.expectedMileage ?? null;
  const selectedVehicleFuelType =
    selected?.vehicleFuelType ??
    vehicles.find((vehicle) => vehicle.id === selected?.vehicleAssetId)?.fuelType ??
    null;

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Fleet Trips</h1>
          <p className="text-muted-foreground">Trip requests, approvals and dispatch tracking</p>
        </div>

        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              New Trip
            </Button>
          </DialogTrigger>
          <DialogContent className="flex max-h-[90vh] w-[95vw] max-w-5xl flex-col overflow-hidden">
            <DialogHeader>
              <DialogTitle>Create Trip</DialogTitle>
              <DialogDescription>Create a draft trip request, then submit for workflow approval.</DialogDescription>
            </DialogHeader>

            <div className="flex-1 overflow-y-auto pr-1">
              <div className="grid grid-cols-1 gap-4 md:grid-cols-6">
                <div className="space-y-2 md:col-span-3">
                  <Label>Vehicle</Label>
                  <Select value={createForm.vehicleAssetId || undefined} onValueChange={(v) => setCreateForm((p) => ({ ...p, vehicleAssetId: v }))}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select vehicle" />
                    </SelectTrigger>
                    <SelectContent>
                      {vehicles.map((v) => (
                        <SelectItem key={v.id} value={v.id}>
                          {v.assetNumber} — {v.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Driver</Label>
                  <Select
                    value={createForm.driverEmployeeId || 'none'}
                    onValueChange={(v) => setCreateForm((p) => ({ ...p, driverEmployeeId: v === 'none' ? null : v }))}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select driver (optional)" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Unassigned</SelectItem>
                      {employees.map((e) => (
                        <SelectItem key={e.id} value={e.id}>
                          {(e.firstName || '').trim()} {(e.lastName || '').trim()} {e.employeeNumber ? `(${e.employeeNumber})` : ''}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2 md:col-span-6">
                  <Label>Purpose</Label>
                  <Input value={createForm.purpose || ''} onChange={(e) => setCreateForm((p) => ({ ...p, purpose: e.target.value }))} />
                </div>

                <div className="space-y-2 md:col-span-6">
                  <Label>Trip Destination {requirePredefinedDestinationOnDispatch ? '(required)' : '(optional)'}</Label>
                  <Select
                    value={createForm.fleetTripDestinationId || 'none'}
                    onValueChange={(v) => {
                      const nextId = v === 'none' ? null : v;
                      setCreateForm((p) => {
                        const d = nextId ? tripDestinations.find((x) => x.id === nextId) : null;
                        return {
                          ...p,
                          fleetTripDestinationId: nextId,
                          origin: d?.origin ?? p.origin,
                          destination: d?.destination ?? p.destination,
                        };
                      });
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select predefined destination" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">None</SelectItem>
                      {tripDestinations.map((d) => (
                        <SelectItem key={d.id} value={d.id}>
                          {d.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {createForm.fleetTripDestinationId ? (
                    <div className="text-xs text-muted-foreground">
                      {(() => {
                        const d = tripDestinations.find((x) => x.id === createForm.fleetTripDestinationId);
                        if (!d) return null;
                        const parts: string[] = [];
                        if (d.origin) parts.push(`Origin: ${d.origin}`);
                        if (d.destination) parts.push(`Destination: ${d.destination}`);
                        if (d.expectedHours != null) parts.push(`Expected hours: ${d.expectedHours}`);
                        if (d.expectedMileage != null) parts.push(`Expected mileage: ${d.expectedMileage} km`);
                        return parts.join(' • ');
                      })()}
                    </div>
                  ) : null}
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Origin</Label>
                  <Input value={createForm.origin || ''} onChange={(e) => setCreateForm((p) => ({ ...p, origin: e.target.value }))} />
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Destination</Label>
                  <Input value={createForm.destination || ''} onChange={(e) => setCreateForm((p) => ({ ...p, destination: e.target.value }))} />
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Planned Start</Label>
                  <Input
                    type="datetime-local"
                    value={createForm.plannedStartAt || ''}
                    onChange={(e) => setCreateForm((p) => ({ ...p, plannedStartAt: e.target.value || null }))}
                  />
                </div>

                <div className="space-y-2 md:col-span-3">
                  <Label>Planned End</Label>
                  <Input
                    type="datetime-local"
                    value={createForm.plannedEndAt || ''}
                    onChange={(e) => setCreateForm((p) => ({ ...p, plannedEndAt: e.target.value || null }))}
                  />
                </div>

                <div className="space-y-2 md:col-span-6">
                  <Label>Notes</Label>
                  <Textarea
                    className="min-h-28"
                    value={createForm.notes || ''}
                    onChange={(e) => setCreateForm((p) => ({ ...p, notes: e.target.value }))}
                  />
                </div>
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => setCreateOpen(false)}>
                Cancel
              </Button>
              <Button onClick={onCreate}>Create</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      <Card>
        <CardHeader className="space-y-4">
          <CardTitle>Trips</CardTitle>
          <div className="flex flex-col gap-3 md:flex-row md:items-center">
            <div className="relative flex-1">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-8"
                placeholder="Search by vehicle, plate, origin/destination, purpose..."
                value={searchTerm}
                onChange={(e) => {
                  setPage(1);
                  setSearchTerm(e.target.value);
                }}
              />
            </div>

            <Select
              value={status}
              onValueChange={(v) => {
                setPage(1);
                setStatus(v);
              }}
            >
              <SelectTrigger className="w-full md:w-56">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Submitted">Submitted</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Dispatched">Dispatched</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>

            <div className="flex items-center gap-2">
              <Button variant="outline" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                Prev
              </Button>
              <div className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </div>
              <Button variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/40">
                  <TableHead>Status</TableHead>
                  <TableHead>Vehicle</TableHead>
                  <TableHead>Driver</TableHead>
                  <TableHead>Planned</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                      Loading...
                    </TableCell>
                  </TableRow>
                ) : result.items.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="py-8 text-center text-muted-foreground">
                      No trips found
                    </TableCell>
                  </TableRow>
                ) : (
                  result.items.map((t, idx) => (
                    <TableRow key={t.id} className={idx % 2 === 1 ? 'bg-muted/10 hover:bg-muted/30' : 'hover:bg-muted/30'}>
                      <TableCell>{getTripStatusBadge(t.status)}</TableCell>
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{t.vehicleName}</span>
                          {t.vehicleAssetNumber || t.vehicleLicensePlate ? (
                            <span className="text-xs text-muted-foreground">
                              {t.vehicleAssetNumber ? <span className="font-mono">{t.vehicleAssetNumber}</span> : null}
                              {t.vehicleLicensePlate ? (
                                <>
                                  {t.vehicleAssetNumber ? <span> • </span> : null}
                                  <span className="font-mono">{t.vehicleLicensePlate}</span>
                                </>
                              ) : null}
                            </span>
                          ) : null}
                        </div>
                      </TableCell>
                      <TableCell>
                        {t.driverEmployeeName ? <Badge variant="outline">{t.driverEmployeeName}</Badge> : <span className="text-muted-foreground">-</span>}
                      </TableCell>
                      <TableCell className="text-sm">
                        {t.plannedStartAt ? formatFleetDateTime(t.plannedStartAt) : <span className="text-muted-foreground">-</span>}
                        {t.plannedEndAt ? <span className="text-muted-foreground"> → </span> : null}
                        {t.plannedEndAt ? formatFleetDateTime(t.plannedEndAt) : null}
                      </TableCell>
                      <TableCell className="text-sm">{formatFleetDateTime(t.createdAt)}</TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" onClick={() => openView(t.id)} title="View trip">
                          <Eye className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={viewOpen} onOpenChange={setViewOpen}>
        <DialogContent className="flex max-h-[90vh] w-[95vw] max-w-6xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>Trip Details</DialogTitle>
            <DialogDescription>View trip information, approvals, and dispatch/completion.</DialogDescription>
          </DialogHeader>

          <Tabs
            value={tripDetailsTab}
            onValueChange={(v) => setTripDetailsTab(v === 'approvals' ? 'approvals' : v === 'expenses' ? 'expenses' : 'details')}
            className="flex min-h-0 flex-1 flex-col overflow-hidden"
          >
            <TabsList className="w-fit">
              <TabsTrigger value="details">Details</TabsTrigger>
              <TabsTrigger value="expenses">Expenses</TabsTrigger>
              <TabsTrigger value="approvals">Approval History</TabsTrigger>
            </TabsList>

            <TabsContent value="details" className="mt-4 min-h-0 flex-1 overflow-hidden">
              {!selected ? (
                <div className="py-10 text-center text-muted-foreground">No trip selected</div>
              ) : (
                <div className="flex h-full min-h-0 flex-col overflow-hidden">
                  <div className="flex-1 min-h-0 overflow-y-auto pr-1 pb-2">
                    <div className="space-y-4">
                      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                      <Card>
                        <CardHeader className="py-3">
                          <CardTitle className="text-base">Status</CardTitle>
                        </CardHeader>
                        <CardContent className="pb-4">{getTripStatusBadge(selected.status)}</CardContent>
                      </Card>
                      <Card>
                        <CardHeader className="py-3">
                          <CardTitle className="text-base">Vehicle</CardTitle>
                        </CardHeader>
                        <CardContent className="pb-4">
                          <div className="font-medium">{selected.vehicleName}</div>
                          {selected.vehicleAssetNumber || selected.vehicleLicensePlate ? (
                            <div className="mt-1 text-xs text-muted-foreground">
                              {selected.vehicleAssetNumber ? <span className="font-mono">{selected.vehicleAssetNumber}</span> : null}
                              {selected.vehicleLicensePlate ? (
                                <>
                                  {selected.vehicleAssetNumber ? <span> • </span> : null}
                                  <span className="font-mono">{selected.vehicleLicensePlate}</span>
                                </>
                              ) : null}
                            </div>
                          ) : null}
                        </CardContent>
                      </Card>
                      <Card>
                        <CardHeader className="py-3">
                          <CardTitle className="text-base">Driver</CardTitle>
                        </CardHeader>
                        <CardContent className="pb-4">
                          <div className="flex items-center justify-between gap-2">
                            <div>
                              {selected.driverEmployeeName ? <Badge variant="outline">{selected.driverEmployeeName}</Badge> : <span className="text-muted-foreground">Unassigned</span>}
                            </div>
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={selected.status !== 'Approved' || !employees.length}
                              onClick={() => {
                                setAssignDriverId(selected.driverEmployeeId || 'none');
                                setAssignDriverOpen(true);
                              }}
                            >
                              Assign
                            </Button>
                          </div>
                        </CardContent>
                      </Card>
                    </div>

                    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                      <Card>
                        <CardHeader className="py-3">
                          <CardTitle className="text-base">Plan</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2 pb-4 text-sm">
                          <div>
                            <span className="text-muted-foreground">Purpose:</span> {selected.purpose || '-'}
                          </div>
                          {tripDestinationLabel ? (
                            <div>
                              <span className="text-muted-foreground">Trip destination:</span> {tripDestinationLabel}
                            </div>
                          ) : null}
                          <div>
                            <span className="text-muted-foreground">Origin:</span> {selected.origin || '-'}
                          </div>
                          <div>
                            <span className="text-muted-foreground">Destination:</span> {selected.destination || '-'}
                          </div>
                          <div>
                            <span className="text-muted-foreground">Expected:</span> {selectedExpectedHours ?? '-'} hrs • {selectedExpectedMileage ?? '-'} km
                          </div>
                          <div>
                            <span className="text-muted-foreground">Planned:</span> {formatFleetDateTime(selected.plannedStartAt)} → {formatFleetDateTime(selected.plannedEndAt)}
                          </div>
                        </CardContent>
                      </Card>

                      <Card>
                        <CardHeader className="py-3">
                          <CardTitle className="text-base">Execution</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2 pb-4 text-sm">
                          <div>
                            <span className="text-muted-foreground">Dispatched:</span> {formatFleetDateTime(selected.dispatchedAt)}
                          </div>
                          <div>
                            <span className="text-muted-foreground">Completed:</span> {formatFleetDateTime(selected.completedAt)}
                          </div>
                          <div>
                            <span className="text-muted-foreground">Start odometer:</span> {selected.startMileage ?? '-'} km
                          </div>
                          <div>
                            <span className="text-muted-foreground">End odometer:</span> {selected.endMileage ?? '-'} km
                          </div>
                        </CardContent>
                      </Card>

                      <Card>
                        <CardHeader className="py-3">
                          <CardTitle className="text-base">Inspections</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-3 pb-4 text-sm">
                          {inspectionsLoading ? (
                            <div className="text-muted-foreground">Loading…</div>
                          ) : (
                            <>
                              {(['PreTrip', 'PostTrip'] as const).map((kind) => {
                                const current = inspections
                                  .filter((i) => i.inspectionKind === kind && i.status !== 'Cancelled')
                                  .sort((a, b) => (a.startedAtUtc < b.startedAtUtc ? 1 : -1))[0];

                                return (
                                  <div key={kind} className="rounded-md border p-3">
                                    <div className="flex items-start justify-between gap-3">
                                      <div>
                                        <div className="font-medium">{kind === 'PreTrip' ? 'Pre-trip' : 'Post-trip'}</div>
                                        <div className="text-muted-foreground">
                                          {current ? `${current.status}${current.overallResult ? ` (${current.overallResult})` : ''}` : 'Not started'}
                                        </div>
                                        {current?.inspectionTemplateName ? (
                                          <div className="text-muted-foreground">Template: {current.inspectionTemplateName}</div>
                                        ) : null}
                                      </div>
                                      <div className="flex gap-2">
                                        <Button
                                          variant="outline"
                                          size="sm"
                                          onClick={() => {
                                            setStartInspectionForm({
                                              fleetTripId: selected.id,
                                              inspectionTemplateId: inspectionTemplates[0]?.id || '',
                                              inspectorEmployeeId: selected.driverEmployeeId || null,
                                              inspectionKind: kind,
                                            });
                                            setStartInspectionOpen(true);
                                          }}
                                          disabled={!inspectionTemplates.length || !!current}
                                        >
                                          Start
                                        </Button>
                                        <Button
                                          variant="outline"
                                          size="sm"
                                          onClick={() => {
                                            if (!current) return;
                                            setSelectedInspection(current);
                                            setCompleteInspectionForm({
                                              completedAtUtc:
                                                current.status === 'InProgress'
                                                  ? toDatetimeLocal(new Date())
                                                  : (current.completedAtUtc ?? null),
                                              overallResult: current.overallResult || 'Pass',
                                              inspectionData: current.inspectionData || '{}',
                                              notes: current.notes || '',
                                            });
                                            setCompleteInspectionOpen(true);
                                          }}
                                          disabled={!current}
                                        >
                                          {current?.status === 'InProgress' ? 'Complete' : 'View'}
                                        </Button>
                                      </div>
                                    </div>
                                  </div>
                                );
                              })}
                              {!inspectionTemplates.length ? (
                                <div className="text-xs text-muted-foreground">
                                  No inspection templates found. Create templates via `api/inspection-templates` (or seed them).
                                </div>
                              ) : null}
                            </>
                          )}
                        </CardContent>
                      </Card>

                    </div>
                    </div>
                  </div>

                </div>
              )}
            </TabsContent>

            <TabsContent value="expenses" className="mt-4 flex-1 overflow-hidden">
              <div className="h-full overflow-y-auto pr-1">
                {!selected ? (
                  <div className="py-10 text-center text-muted-foreground">No trip selected</div>
                ) : (
                  <Card>
                    <CardHeader className="flex-row items-center justify-between py-3">
                      <CardTitle className="text-base">Trip Expenses</CardTitle>
                      <div className="flex flex-wrap gap-2">
                        <Button variant="outline" size="sm" onClick={openAddFuel}>
                          <Droplet className="mr-2 h-4 w-4" />
                          Add Fuel
                        </Button>
                        <Button variant="outline" size="sm" onClick={() => openAddExpense('Toll')}>
                          <ReceiptText className="mr-2 h-4 w-4" />
                          Add Toll
                        </Button>
                        <Button variant="outline" size="sm" onClick={() => openAddExpense('Accommodation')}>
                          <Hotel className="mr-2 h-4 w-4" />
                          Add Accommodation
                        </Button>
                        <Button variant="outline" size="sm" onClick={() => openAddExpense('Other')}>
                          <Plus className="mr-2 h-4 w-4" />
                          Add Other
                        </Button>
                        <Button variant="outline" size="sm" onClick={loadTripCosts} disabled={tripCostsLoading}>
                          Refresh
                        </Button>
                      </div>
                    </CardHeader>
                    <CardContent className="pb-4">
                      <div className="rounded-md border">
                        <Table>
                          <TableHeader>
                            <TableRow className="bg-muted/40">
                              <TableHead>Date</TableHead>
                              <TableHead>Type</TableHead>
                              <TableHead>Source</TableHead>
                              <TableHead className="text-right">Amount</TableHead>
                              <TableHead>Notes</TableHead>
                              <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {tripCostsLoading ? (
                              <TableRow>
                                <TableCell colSpan={6} className="py-6 text-center text-muted-foreground">
                                  Loading...
                                </TableCell>
                              </TableRow>
                            ) : tripCosts.length === 0 ? (
                              <TableRow>
                                <TableCell colSpan={6} className="py-6 text-center text-muted-foreground">
                                  No expenses for this trip yet
                                </TableCell>
                              </TableRow>
                            ) : (
                              tripCosts.map((c) => (
                                <TableRow key={c.id} className="hover:bg-muted/30">
                                  <TableCell className="text-sm">{formatFleetDateTime(c.costDateUtc)}</TableCell>
                                  <TableCell className="font-medium">{c.costType}</TableCell>
                                  <TableCell>
                                    <Badge variant="outline">{c.source}</Badge>
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {typeof c.amount === 'number' ? c.amount.toFixed(2) : String(c.amount)} {c.currencyCode || ''}
                                  </TableCell>
                                  <TableCell className="max-w-[420px] truncate text-muted-foreground" title={c.notes || ''}>
                                    {c.notes || '-'}
                                  </TableCell>
                                  <TableCell className="text-right">
                                    <div className="flex items-center justify-end gap-1">
                                      <Button variant="ghost" size="sm" onClick={() => openExpenseDocs(c)} title="Attachments">
                                        <Paperclip className="h-4 w-4" />
                                      </Button>
                                      <Button variant="ghost" size="sm" onClick={() => deleteTripCostRow(c)} title="Delete">
                                        <Trash2 className="h-4 w-4" />
                                      </Button>
                                    </div>
                                  </TableCell>
                                </TableRow>
                              ))
                            )}
                          </TableBody>
                        </Table>
                      </div>

                      {tripCosts.length > 0 ? (
                        <div className="mt-3 flex items-center justify-between text-xs text-muted-foreground">
                          <div>Total entries: {tripCosts.length}</div>
                          <div>
                            Total:{' '}
                            {tripCosts.reduce((sum, x) => sum + (typeof x.amount === 'number' ? x.amount : Number(x.amount) || 0), 0).toFixed(2)}
                          </div>
                        </div>
                      ) : null}
                    </CardContent>
                  </Card>
                )}
              </div>
            </TabsContent>

            <TabsContent value="approvals" className="mt-4 flex-1 overflow-hidden">
              <div className="h-full overflow-y-auto pr-1">
                {!selected ? (
                  <div className="py-10 text-center text-muted-foreground">No trip selected</div>
                ) : (
                  <WorkflowApprovalHistoryPanel entityType="FleetTrip" entityId={selected.id} />
                )}
              </div>
            </TabsContent>
          </Tabs>

          <DialogFooter className="border-t pt-3 flex-col gap-2 sm:flex-row sm:items-center sm:justify-between sm:space-x-0">
            {selected && tripDetailsTab === 'details' ? (
              <div className="flex w-full flex-col gap-2 sm:flex-1 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
                <WorkflowApprovalActions
                  entityType="FleetTrip"
                  entityId={selected.id}
                  entityLabel="Fleet Trip"
                  entityNumber={selected.vehicleAssetNumber || undefined}
                  status={selected.status}
                  loadWorkflowSummary
                  onSubmit={async () => {
                    await fleetService.submitTrip(selected.id);
                    await refreshSelected();
                    await loadTrips();
                  }}
                  onApprove={async (comments) => {
                    await fleetService.approveTrip(selected.id, comments);
                    await refreshSelected();
                    await loadTrips();
                  }}
                  onReject={async (comments) => {
                    await fleetService.rejectTrip(selected.id, 'Rejected', comments);
                    await refreshSelected();
                    await loadTrips();
                  }}
                />

                <div className="flex flex-wrap items-center justify-end gap-2">
                  <Button
                    variant="destructive"
                    disabled={['Dispatched', 'Completed', 'Cancelled'].includes(selected.status)}
                    onClick={() => {
                      setCancelReason('');
                      setCancelOpen(true);
                    }}
                  >
                    Cancel
                  </Button>
                  <Button
                    variant="outline"
                    disabled={selected.status !== 'Approved'}
                    onClick={() => {
                      setDispatchForm({
                        dispatchedAt: toDatetimeLocal(new Date()),
                        startMileage: selected.startMileage ?? null,
                        startOperatingHours: selected.startOperatingHours ?? null,
                      });
                      setDispatchOpen(true);
                    }}
                  >
                    <Truck className="mr-2 h-4 w-4" />
                    Dispatch
                  </Button>
                  <Button
                    variant="outline"
                    disabled={selected.status !== 'Dispatched'}
                    onClick={() => {
                      setCompleteForm({
                        completedAt: toDatetimeLocal(new Date()),
                        endMileage: selected.endMileage ?? null,
                        notes: selected.notes || '',
                      });
                      setCompleteOpen(true);
                    }}
                  >
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Complete
                  </Button>
                </div>
              </div>
            ) : (
              <div />
            )}

            <Button variant="outline" onClick={() => setViewOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={startInspectionOpen} onOpenChange={setStartInspectionOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Start Inspection</DialogTitle>
            <DialogDescription>Create a {startInspectionForm.inspectionKind === 'PreTrip' ? 'pre-trip' : 'post-trip'} inspection record.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4">
            <div className="space-y-2">
              <Label>Template</Label>
              <Select
                value={startInspectionForm.inspectionTemplateId || 'none'}
                onValueChange={(v) => setStartInspectionForm((p) => ({ ...p, inspectionTemplateId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select template" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Select template</SelectItem>
                  {inspectionTemplates.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Inspector (optional)</Label>
              <Select
                value={startInspectionForm.inspectorEmployeeId || 'none'}
                onValueChange={(v) => setStartInspectionForm((p) => ({ ...p, inspectorEmployeeId: v === 'none' ? null : v }))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select employee" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No inspector</SelectItem>
                  {employees.map((e) => (
                    <SelectItem key={e.id} value={e.id}>
                      {(e.firstName || '').trim()} {(e.lastName || '').trim()} {e.employeeNumber ? `(${e.employeeNumber})` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStartInspectionOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={async () => {
                try {
                  await fleetService.startTripInspection(startInspectionForm);
                  setStartInspectionOpen(false);
                  if (selected?.id) setInspections(await fleetService.getTripInspections(selected.id));
                } catch (e: any) {
                  toast({ title: 'Failed to start inspection', description: e?.message || String(e), variant: 'destructive' });
                }
              }}
              disabled={!startInspectionForm.fleetTripId || !startInspectionForm.inspectionTemplateId}
            >
              Start
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={completeInspectionOpen} onOpenChange={setCompleteInspectionOpen}>
        <DialogContent className="flex max-h-[90vh] w-[95vw] max-w-4xl flex-col overflow-hidden">
          <DialogHeader>
            <DialogTitle>Complete Inspection</DialogTitle>
            <DialogDescription>{selectedInspection?.inspectionTemplateName}</DialogDescription>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto pr-1">
          <div className="grid grid-cols-1 gap-4">
            <div className="space-y-2">
              {selectedInspection ? (
                <div className="text-xs text-muted-foreground">
                  Status: {selectedInspection.status}
                  {selectedInspection.startedAtUtc ? ` • Started: ${formatFleetDateTime(selectedInspection.startedAtUtc)}` : ''}
                  {selectedInspection.completedAtUtc ? ` • Completed: ${formatFleetDateTime(selectedInspection.completedAtUtc)}` : ''}
                </div>
              ) : null}
              <Label>Overall Result</Label>
              <Select
                value={completeInspectionForm.overallResult}
                onValueChange={(v) => setCompleteInspectionForm((p) => ({ ...p, overallResult: v }))}
                disabled={inspectionReadOnly}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select result" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Pass">Pass</SelectItem>
                  <SelectItem value="Fail">Fail</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Checklist</Label>
              {!selectedInspectionTemplate ? (
                <div className="text-sm text-muted-foreground">Loading template…</div>
              ) : selectedInspectionTemplate.checklistItems.length === 0 ? (
                <div className="rounded-md border p-3 text-sm text-muted-foreground">
                  This template has no checklist items. Configure items in Administration → Maintenance → Inspection Templates.
                </div>
              ) : (
                <div className="space-y-3 rounded-md border p-3">
                  {selectedInspectionTemplate.checklistItems.map((i, idx) => {
                    const type = (i.type || '').toLowerCase();
                    const value = inspectionAnswers[i.id] ?? '';
                    return (
                      <div key={i.id} className="space-y-1">
                        <div className="text-sm font-medium">
                          {idx + 1}. {i.item} {i.required ? <span className="text-destructive">*</span> : null}
                        </div>
                        {type === 'checklist' ? (
                          <Select
                            value={value || 'none'}
                            onValueChange={(v) => {
                              if (inspectionReadOnly) return;
                              const next = { ...inspectionAnswers, [i.id]: v === 'none' ? '' : v };
                              setInspectionAnswers(next);
                              const derived = deriveOverallResult(selectedInspectionTemplate, next);
                              setCompleteInspectionForm((p) => ({
                                ...p,
                                overallResult: derived,
                                inspectionData: buildInspectionDataJson(selectedInspectionTemplate, next),
                              }));
                            }}
                            disabled={inspectionReadOnly}
                          >
                            <SelectTrigger>
                              <SelectValue placeholder="Select result" />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="none">Select…</SelectItem>
                              <SelectItem value="Pass">Pass</SelectItem>
                              <SelectItem value="Fail">Fail</SelectItem>
                            </SelectContent>
                          </Select>
                        ) : type === 'number' || type === 'measurement' ? (
                          <Input
                            type="number"
                            value={value}
                            onChange={(e) => {
                              if (inspectionReadOnly) return;
                              const next = { ...inspectionAnswers, [i.id]: e.target.value };
                              setInspectionAnswers(next);
                              setCompleteInspectionForm((p) => ({
                                ...p,
                                inspectionData: buildInspectionDataJson(selectedInspectionTemplate, next),
                              }));
                            }}
                            placeholder="Enter value"
                            disabled={inspectionReadOnly}
                          />
                        ) : (
                          <Textarea
                            value={value}
                            onChange={(e) => {
                              if (inspectionReadOnly) return;
                              const next = { ...inspectionAnswers, [i.id]: e.target.value };
                              setInspectionAnswers(next);
                              setCompleteInspectionForm((p) => ({
                                ...p,
                                inspectionData: buildInspectionDataJson(selectedInspectionTemplate, next),
                              }));
                            }}
                            placeholder="Enter response"
                            rows={2}
                            disabled={inspectionReadOnly}
                          />
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
              {selectedInspectionTemplate?.checklistItems?.length ? (
                <div className="text-xs text-muted-foreground">Overall result auto-derives from item results (any Fail → Fail).</div>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea
                value={completeInspectionForm.notes || ''}
                onChange={(e) => setCompleteInspectionForm((p) => ({ ...p, notes: e.target.value }))}
                rows={5}
                disabled={inspectionReadOnly}
              />
            </div>
          </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteInspectionOpen(false)}>
              {inspectionReadOnly ? 'Close' : 'Cancel'}
            </Button>
            {!inspectionReadOnly ? (
              <Button
                onClick={async () => {
                  if (!selectedInspection) return;
                  try {
                    if (selectedInspectionTemplate?.checklistItems?.length) {
                      const missing = selectedInspectionTemplate.checklistItems
                        .filter((i) => i.required)
                        .filter((i) => {
                          const v = (inspectionAnswers[i.id] || '').trim();
                          return !v;
                        });

                      if (missing.length > 0) {
                        toast({
                          title: 'Checklist incomplete',
                          description: `Please complete required items (${missing.length}) before finishing the inspection.`,
                          variant: 'destructive',
                        });
                        return;
                      }
                    }

                    await fleetService.completeTripInspection(selectedInspection.id, {
                      ...completeInspectionForm,
                      completedAtUtc: completeInspectionForm.completedAtUtc || toDatetimeLocal(new Date()),
                      inspectionData: completeInspectionForm.inspectionData || '{}',
                    });
                    setCompleteInspectionOpen(false);
                    if (selected?.id) setInspections(await fleetService.getTripInspections(selected.id));
                  } catch (e: any) {
                    toast({ title: 'Failed to complete inspection', description: e?.message || String(e), variant: 'destructive' });
                  }
                }}
                disabled={!selectedInspection}
              >
                Complete
              </Button>
            ) : null}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Cancel Trip</DialogTitle>
            <DialogDescription>Cancel this trip request before dispatch. This will set the trip status to Cancelled.</DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label>Reason (optional)</Label>
            <Textarea value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} placeholder="e.g. Trip no longer required" />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              Close
            </Button>
            <Button
              variant="destructive"
              disabled={!selected}
              onClick={async () => {
                if (!selected) return;
                try {
                  await fleetService.cancelTrip(selected.id, cancelReason.trim());
                  toast({ title: 'Trip cancelled' });
                  setCancelOpen(false);
                  await refreshSelected();
                  await loadTrips();
                } catch (e: any) {
                  toast({ title: 'Cancel failed', description: e?.message || String(e), variant: 'destructive' });
                }
              }}
            >
              Cancel Trip
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={addFuelOpen} onOpenChange={setAddFuelOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Add Fuel Expense</DialogTitle>
            <DialogDescription>
              Fuel captured here is stored in Fleet Fuel Transactions and auto-creates a linked Fleet Cost Entry (with this trip as reference).
            </DialogDescription>
          </DialogHeader>

          {selected ? (
            <div className="rounded-md border bg-muted/20 px-4 py-3 text-sm">
              <div className="font-medium">{selected.vehicleName}</div>
              <div className="mt-1 text-muted-foreground">
                Expected fuel type: {selectedVehicleFuelType || 'Not set on vehicle'}
              </div>
            </div>
          ) : null}

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Fuelled At</Label>
              <Input
                type="datetime-local"
                value={fuelForm.fuelledAt ? toDatetimeLocal(new Date(fuelForm.fuelledAt)) : toDatetimeLocal(new Date())}
                onChange={(e) => setFuelForm((p) => ({ ...p, fuelledAt: new Date(e.target.value).toISOString() }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Quantity</Label>
              <Input
                type="number"
                value={String(fuelForm.quantity ?? 0)}
                onChange={(e) => setFuelForm((p) => ({ ...p, quantity: parseFloat(e.target.value || '0') }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Unit</Label>
              <Input value={fuelForm.unit || 'L'} onChange={(e) => setFuelForm((p) => ({ ...p, unit: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Unit Cost (optional)</Label>
              <Input
                type="number"
                value={fuelForm.unitCost == null ? '' : String(fuelForm.unitCost)}
                onChange={(e) => setFuelForm((p) => ({ ...p, unitCost: e.target.value ? parseFloat(e.target.value) : null }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Vendor (optional)</Label>
              <Input value={fuelForm.vendorName || ''} onChange={(e) => setFuelForm((p) => ({ ...p, vendorName: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Receipt Ref (optional)</Label>
              <Input
                value={fuelForm.receiptReference || ''}
                onChange={(e) => setFuelForm((p) => ({ ...p, receiptReference: e.target.value }))}
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea value={fuelForm.notes || ''} onChange={(e) => setFuelForm((p) => ({ ...p, notes: e.target.value }))} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAddFuelOpen(false)} disabled={savingExpense}>
              Cancel
            </Button>
            <Button onClick={saveFuelExpense} disabled={savingExpense}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={addExpenseOpen} onOpenChange={setAddExpenseOpen}>
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Add Trip Expense</DialogTitle>
            <DialogDescription>Toll fees, accommodation, and other trip expenses are recorded against this trip.</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Date</Label>
              <Input
                type="datetime-local"
                value={expenseForm.costDateUtc ? toDatetimeLocal(new Date(expenseForm.costDateUtc)) : toDatetimeLocal(new Date())}
                onChange={(e) => setExpenseForm((p) => ({ ...p, costDateUtc: new Date(e.target.value).toISOString() }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Type</Label>
              <Input value={expenseForm.costType} onChange={(e) => setExpenseForm((p) => ({ ...p, costType: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Amount</Label>
              <Input
                type="number"
                value={String(expenseForm.amount ?? 0)}
                onChange={(e) => setExpenseForm((p) => ({ ...p, amount: parseFloat(e.target.value || '0') }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Currency (optional)</Label>
              <Input
                value={expenseForm.currencyCode || ''}
                onChange={(e) => setExpenseForm((p) => ({ ...p, currencyCode: e.target.value }))}
                placeholder="e.g. USD"
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Notes (optional)</Label>
              <Textarea value={expenseForm.notes || ''} onChange={(e) => setExpenseForm((p) => ({ ...p, notes: e.target.value }))} />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAddExpenseOpen(false)} disabled={savingExpense}>
              Cancel
            </Button>
            <Button onClick={saveTripExpense} disabled={savingExpense}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={expenseDocsOpen}
        onOpenChange={(o) => {
          setExpenseDocsOpen(o);
          if (!o) {
            setExpenseDocsEntityId('');
            setExpenseDocsCategory('TripExpenseReceipt');
            setExpenseDocsEntityType('FleetCostEntry');
            setExpenseDocsTitle('Expense Documents');
          }
        }}
      >
        <DialogContent className="max-w-5xl">
          <DialogHeader>
            <DialogTitle>{expenseDocsTitle}</DialogTitle>
            <DialogDescription>Upload and view receipts/documents for this trip expense.</DialogDescription>
          </DialogHeader>

          {expenseDocsEntityId ? (
            <MaintenanceAttachmentsPanel
              entityType={expenseDocsEntityType}
              entityId={expenseDocsEntityId}
              category={expenseDocsCategory}
              title="Documents"
              description="Upload receipts, invoices, and related supporting files."
            />
          ) : (
            <div className="py-6 text-sm text-muted-foreground">Select an expense entry to view documents.</div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => setExpenseDocsOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={dispatchOpen} onOpenChange={setDispatchOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Dispatch Trip</DialogTitle>
            <DialogDescription>Dispatch is blocked when critical compliance is due soon/overdue (per Maintenance settings).</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-1 md:col-span-2 text-sm">
              <div>
                <span className="text-muted-foreground">Trip destination:</span> {selected?.destination || '-'}
              </div>
              <div>
                <span className="text-muted-foreground">Expected:</span> {selectedExpectedHours ?? '-'} hrs • {selectedExpectedMileage ?? '-'} km
              </div>
              {requirePredefinedDestinationOnDispatch && !selected?.fleetTripDestinationId ? (
                <div className="text-xs text-muted-foreground">
                  A predefined Trip Destination template is required (per Maintenance settings). Please edit the trip and select it before dispatch.
                </div>
              ) : null}
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Dispatched At</Label>
              <Input
                type="datetime-local"
                value={dispatchForm.dispatchedAt || ''}
                onChange={(e) => setDispatchForm((p) => ({ ...p, dispatchedAt: e.target.value || null }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Start Mileage (km)</Label>
              <Input
                type="number"
                value={dispatchForm.startMileage ?? ''}
                onChange={(e) => setDispatchForm((p) => ({ ...p, startMileage: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Start Operating Hours</Label>
              <Input
                type="number"
                value={dispatchForm.startOperatingHours ?? ''}
                onChange={(e) => setDispatchForm((p) => ({ ...p, startOperatingHours: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDispatchOpen(false)}>
              Cancel
            </Button>
            <Button onClick={onDispatch} disabled={requirePredefinedDestinationOnDispatch && !selected?.fleetTripDestinationId}>
              Dispatch
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={assignDriverOpen} onOpenChange={setAssignDriverOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Assign Driver</DialogTitle>
            <DialogDescription>Approved trips allow driver assignment before dispatch.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Driver</Label>
            <Select value={assignDriverId} onValueChange={(v) => setAssignDriverId(v)}>
              <SelectTrigger>
                <SelectValue placeholder="Select driver" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Unassigned</SelectItem>
                {employees.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {(e.firstName || '').trim()} {(e.lastName || '').trim()} {e.employeeNumber ? `(${e.employeeNumber})` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssignDriverOpen(false)}>
              Cancel
            </Button>
            <Button onClick={onAssignDriver} disabled={!selected || selected.status !== 'Approved'}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={completeOpen} onOpenChange={setCompleteOpen}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>Complete Trip</DialogTitle>
            <DialogDescription>Capture end odometer reading. This updates the vehicle metrics.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-2 md:col-span-2">
              <Label>Completed At</Label>
              <Input
                type="datetime-local"
                value={completeForm.completedAt}
                onChange={(e) => setCompleteForm((p) => ({ ...p, completedAt: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>End Mileage (km)</Label>
              <Input
                type="number"
                value={completeForm.endMileage ?? ''}
                onChange={(e) => setCompleteForm((p) => ({ ...p, endMileage: e.target.value === '' ? null : Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Notes</Label>
              <Textarea value={completeForm.notes} onChange={(e) => setCompleteForm((p) => ({ ...p, notes: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteOpen(false)}>
              Cancel
            </Button>
            <Button onClick={onComplete}>Complete</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
