'use client';

import React, { useState, useEffect } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import {
  Plus,
  Search,
  Eye,
  Edit,
  Calendar,
  AlertCircle,
  CheckCircle,
  Clock,
  Send,
  XCircle,
  ArrowUpDown,
  ArrowUp,
  ArrowDown,
  CalendarIcon,
  Check,
  ChevronsUpDown,
  MoreHorizontal,
} from 'lucide-react';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Calendar as CalendarComponent } from '@/components/ui/calendar';
import type { DateRange } from 'react-day-picker';
import { format } from 'date-fns';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import maintenanceApiService, {
  Asset,
  WorkOrderType,
  PriorityLevel,
  MaintenanceStaffSchedule,
  MaintenanceExpense,
} from '@/services/maintenanceApiService';
import { maintenanceDataService } from '@/services/maintenanceDataService';
import jobCardService, {
  JobCard as JobCardType,
  JobCardDetails,
  CreateJobCardRequest,
  JobCardApprovalAction,
  JobCardDocument,
} from '@/services/jobCardService';
import workOrderService, { WorkOrder } from '@/services/workOrderService';
import workOrderToolService, {
  WorkOrderToolDto,
} from '@/services/workOrderToolService';
import qualityControlService from '@/services/qualityControlService';
import workflowApiService from '@/services/workflow-api.service';
import { notificationService } from '@/services/notificationService';
import { adminApiService, User } from '@/services/admin-api.service';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import ClientOnly from '@/components/ui/client-only';
import { useToast } from '@/hooks/use-toast';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import assetAdmissionService, {
  CreateAdmissionRequest,
  CreateDischargeRequest,
  AssetAdmission,
  AssetDischarge,
} from '@/services/assetAdmissionService';
import assetConditionService, {
  AssetConditionChecklistTemplateDto,
  AssetConditionRecordDto,
  CreateAssetConditionRecordDto,
  SubmitAssetConditionItemDto,
} from '@/services/assetConditionService';
import type { Employee } from '@/services/maintenanceDataService';
import {
  ClipboardCheck,
  CheckCircle as CheckIcon,
  XCircle as XIcon,
} from 'lucide-react';
import { Switch } from '@/components/ui/switch';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import {
  WorkflowTabContent,
  WorkflowTabTrigger,
} from '@/components/workflow/WorkflowRecordTab';
import { useMaintenanceCurrency } from '@/hooks/useMaintenanceCurrency';
import {
  formatPendingApprovers,
  useWorkflowEntitySummaries,
} from '@/hooks/useWorkflowEntitySummaries';
import type { WorkflowEntitySummaryDto } from '@/types/workflow';

type JobCardStatus =
  'Draft' | 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Cancelled';
type JobCardApprovalStatus =
  'NotStarted' | 'Pending' | 'Approved' | 'Rejected' | 'ChangesRequested';
type JobCardPriority = 'Low' | 'Medium' | 'High' | 'Critical';
type WorkOrderBillingType = 'Maintenance' | 'Repairs';

// Use the JobCard type from service instead of local interface
interface JobCard {
  id: string;
  jobCardNumber: string;
  title: string;
  description?: string;
  problemDescription?: string;
  assetId: string;
  assetName: string;
  assetCode?: string;
  maintenanceTypeId: string;
  maintenanceType: string;
  priorityLevelId: string;
  priority: JobCardPriority;
  priorityColor?: string;
  customerBusinessPartnerId?: string;
  customerBusinessPartnerName?: string;
  workOrderBillingType?: WorkOrderBillingType;
  jobCardStatus: JobCardStatus;
  approvalStatus: JobCardApprovalStatus;
  requestedById?: string;
  requestedBy: string;
  requestedDate?: string;
  requiredCompletionDate?: string;
  estimatedHours: number;
  estimatedCost: number;
  requiresShutdown?: boolean;
  requiresSafetyPermit?: boolean;
  generatedWorkOrderId?: string;
  workOrderGeneratedAt?: string;
  createdAt: string;
}

interface JobCardAdmissionState {
  admission: AssetAdmission;
  admissionRecord?: AssetConditionRecordDto | null;
}

const mapJobCardResponseToGridCard = (card: JobCardType): JobCard => ({
  id: card.id,
  jobCardNumber: card.jobCardNumber,
  title: card.title,
  description: card.description,
  problemDescription: card.problemDescription,
  assetName: card.assetName,
  assetCode: card.assetCode,
  requestedBy: card.requestedBy,
  requestedById: card.requestedById,
  jobCardStatus: card.jobCardStatus as JobCard['jobCardStatus'],
  approvalStatus: card.approvalStatus as JobCard['approvalStatus'],
  priority: card.priority as JobCard['priority'],
  priorityColor: card.priorityColor,
  customerBusinessPartnerId: card.customerBusinessPartnerId,
  customerBusinessPartnerName: card.customerBusinessPartnerName,
  workOrderBillingType: card.workOrderBillingType || 'Repairs',
  createdAt: card.createdAt,
  requestedDate: card.requestedDate,
  requiredCompletionDate: card.requiredCompletionDate,
  estimatedHours: card.estimatedHours,
  estimatedCost: card.estimatedCost,
  maintenanceType: card.maintenanceType,
  generatedWorkOrderId: card.generatedWorkOrderId,
  workOrderGeneratedAt: card.workOrderGeneratedAt,
  assetId: card.assetId,
  maintenanceTypeId: card.maintenanceTypeId,
  priorityLevelId: card.priorityLevelId,
  requiresShutdown: card.requiresShutdown,
  requiresSafetyPermit: card.requiresSafetyPermit,
});

interface JobCardFormState {
  title: string;
  description: string;
  assetName: string;
  assetId: string;
  priority: JobCardPriority;
  maintenanceType: string;
  maintenanceTypeId: string;
  priorityLevelId: string;
  customerBusinessPartnerId: string;
  workOrderBillingType: WorkOrderBillingType;
  estimatedHours: number;
  estimatedCost: number;
  problemDescription: string;
}

const formatBusinessPartnerLabel = (partner: BusinessPartnerDto) =>
  `${partner.partnerName}${partner.partnerCode ? ` (${partner.partnerCode})` : ''}`;

const safeFileName = (fileName?: string) => {
  const cleaned = (fileName || 'job-card-document')
    .replace(/[<>:"/\\|?*]+/g, '_')
    .trim();
  return cleaned || 'job-card-document';
};

const safeLookup = async <T,>(
  label: string,
  loader: () => Promise<T>,
  fallback: T
): Promise<T> => {
  try {
    return await loader();
  } catch (error) {
    console.warn(
      `Unable to load ${label}; continuing with fallback data.`,
      error
    );
    return fallback;
  }
};

const normalizeWorkflowStatusValue = (value?: string) =>
  (value || '').replace(/[\s_-]+/g, '').toLowerCase();

const isJobCardAwaitingApproval = (
  card: { jobCardStatus?: string; approvalStatus?: string },
  workflowSummary?: WorkflowEntitySummaryDto
) => {
  if (workflowSummary?.hasActiveInstance) {
    return true;
  }

  const status = normalizeWorkflowStatusValue(card.jobCardStatus);
  const approvalStatus = normalizeWorkflowStatusValue(card.approvalStatus);

  return (
    approvalStatus === 'pending' ||
    status === 'underreview' ||
    status === 'pendingapproval'
  );
};

interface CustomerBusinessPartnerPickerProps {
  value: string;
  onChange: (value: string) => void;
  partners: BusinessPartnerDto[];
  disabled?: boolean;
  placeholder?: string;
}

function CustomerBusinessPartnerPicker({
  value,
  onChange,
  partners,
  disabled = false,
  placeholder = 'Select customer',
}: CustomerBusinessPartnerPickerProps) {
  const [open, setOpen] = useState(false);
  const selectedPartner = partners.find((partner) => partner.id === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className="w-full justify-between font-normal"
        >
          <span className="truncate">
            {selectedPartner
              ? formatBusinessPartnerLabel(selectedPartner)
              : placeholder}
          </span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        className="w-[var(--radix-popover-trigger-width)] p-0"
        align="start"
      >
        <Command>
          <CommandInput placeholder="Search customers..." />
          <CommandList>
            <CommandEmpty>No customer found.</CommandEmpty>
            <CommandGroup>
              <CommandItem
                value="none no customer linked"
                onSelect={() => {
                  onChange('none');
                  setOpen(false);
                }}
              >
                <Check
                  className={`mr-2 h-4 w-4 ${value === 'none' ? 'opacity-100' : 'opacity-0'}`}
                />
                No customer linked
              </CommandItem>
              {partners.map((partner) => {
                const label = formatBusinessPartnerLabel(partner);
                return (
                  <CommandItem
                    key={partner.id}
                    value={`${label} ${partner.email || ''} ${partner.phone || ''}`}
                    onSelect={() => {
                      onChange(partner.id);
                      setOpen(false);
                    }}
                  >
                    <Check
                      className={`mr-2 h-4 w-4 ${value === partner.id ? 'opacity-100' : 'opacity-0'}`}
                    />
                    <span className="truncate">{label}</span>
                  </CommandItem>
                );
              })}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}

export default function JobCardsPage() {
  const { toast } = useToast();
  const { formatMoney } = useMaintenanceCurrency();
  const router = useRouter();
  const searchParams = useSearchParams();
  const openCreateFromFacilities =
    searchParams?.get('create') === '1' &&
    ['facilities', 'estate-facilities'].includes(
      searchParams?.get('source') ?? ''
    );
  const [jobCards, setJobCards] = useState<JobCard[]>([]);
  const [filteredCards, setFilteredCards] = useState<JobCard[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [priorityFilter, setPriorityFilter] = useState<string>('all');
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [selectedCard, setSelectedCard] = useState<JobCard | null>(null);
  const [selectedCardDetails, setSelectedCardDetails] =
    useState<JobCardDetails | null>(null);
  const [selectedWorkOrder, setSelectedWorkOrder] = useState<WorkOrder | null>(
    null
  );
  const [selectedQCInspection, setSelectedQCInspection] = useState<any | null>(
    null
  );
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [sortColumn, setSortColumn] = useState<string>('createdAt');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');
  const [hasWorkOrderFilter, setHasWorkOrderFilter] = useState<
    'all' | 'with' | 'without'
  >('all');
  const jobCardWorkflowIds = React.useMemo(
    () => jobCards.map((card) => card.id),
    [jobCards]
  );
  const workflowSummaryRefreshKey = React.useMemo(
    () =>
      jobCards
        .map(
          (card) => `${card.id}:${card.jobCardStatus}:${card.approvalStatus}`
        )
        .join('|'),
    [jobCards]
  );
  const { summariesById: workflowSummariesById } = useWorkflowEntitySummaries(
    'JobCard',
    jobCardWorkflowIds,
    jobCardWorkflowIds.length > 0,
    workflowSummaryRefreshKey
  );

  // Testing mode - set to true to use mock data and bypass API calls
  const TESTING_MODE = false; // Using real API calls

  // Data from services
  const [technicians, setTechnicians] = useState<Employee[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [priorityLevels, setPriorityLevels] = useState<PriorityLevel[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<any[]>([]);
  const [customerBusinessPartners, setCustomerBusinessPartners] = useState<
    BusinessPartnerDto[]
  >([]);
  const [loadingData, setLoadingData] = useState(true);
  const [workOrderStaffSchedules, setWorkOrderStaffSchedules] = useState<
    MaintenanceStaffSchedule[]
  >([]);
  const [workOrderExpenses, setWorkOrderExpenses] = useState<
    MaintenanceExpense[]
  >([]);
  const [workOrderTools, setWorkOrderTools] = useState<WorkOrderToolDto[]>([]);
  // Compact cost snapshot state for linked work order (mirrors Work Orders "Actual to Date")
  const [workOrderLaborForSnapshot, setWorkOrderLaborForSnapshot] = useState<
    any[]
  >([]);
  const [toolSummaryForSnapshot, setToolSummaryForSnapshot] = useState<
    import('@/services/workOrderToolService').WorkOrderToolSummaryDto | null
  >(null);
  const [totalExpensesForSnapshot, setTotalExpensesForSnapshot] =
    useState<number>(0);

  // Admission dialog state (inline on job cards page)
  const [isAdmissionDialogOpen, setIsAdmissionDialogOpen] = useState(false);
  const [admissionForm, setAdmissionForm] = useState({
    assetId: '',
    jobCardId: '',
    workOrderId: '',
    admissionType: 'Scheduled' as 'Scheduled' | 'Emergency' | 'Breakdown',
    assetConditionOnAdmission: 'Good' as
      'Excellent' | 'Good' | 'Fair' | 'Poor' | 'Critical',
    admissionNotes: '',
    observedProblems: '',
    mileageReading: 0,
    hoursReading: 0,
    fuelLevel: 0,
    admissionLocation: '',
    bayOrStation: '',
    estimatedCompletionDate: '',
    estimatedDischargeDate: '',
  });
  const [isSubmittingAdmission, setIsSubmittingAdmission] = useState(false);

  // Discharge dialog state (inline on job cards page)
  const [isDischargeDialogOpen, setIsDischargeDialogOpen] = useState(false);
  const [dischargeForm, setDischargeForm] = useState({
    admissionId: '',
    assetConditionOnDischarge: 'Good' as
      'Excellent' | 'Good' | 'Fair' | 'Poor' | 'Critical',
    dischargeNotes: '',
    workCompleted: '',
    remainingIssues: '',
    mileageReading: 0,
    hoursReading: 0,
    fuelLevel: 0,
    qualityCheckPassed: true,
    qualityCheckNotes: '',
    customerAcceptance: true,
    acceptanceNotes: '',
    requiresFollowUp: false,
    followUpDate: '',
    followUpInstructions: '',
    warrantyDays: 0,
    warrantyTerms: '',
  });
  const [isSubmittingDischarge, setIsSubmittingDischarge] = useState(false);

  // Admission/Discharge data for current job card workflow
  const [activeAdmissionForJobCard, setActiveAdmissionForJobCard] =
    useState<AssetAdmission | null>(null);
  const [latestDischargeForAdmission, setLatestDischargeForAdmission] =
    useState<AssetDischarge | null>(null);

  // Track admissions by job card so the grid can show new/continue/edit states.
  const [jobCardAdmissionStates, setJobCardAdmissionStates] = useState<
    Record<string, JobCardAdmissionState>
  >({});

  // Condition Inspection states (at Job Card level)
  const [isConditionDialogOpen, setIsConditionDialogOpen] = useState(false);
  const [conditionTemplates, setConditionTemplates] = useState<
    AssetConditionChecklistTemplateDto[]
  >([]);
  const [selectedTemplate, setSelectedTemplate] =
    useState<AssetConditionChecklistTemplateDto | null>(null);
  const [currentInspection, setCurrentInspection] =
    useState<AssetConditionRecordDto | null>(null);
  const [inspectionType, setInspectionType] = useState<
    'Admission' | 'Discharge'
  >('Admission');
  const [itemResponses, setItemResponses] = useState<
    Record<string, SubmitAssetConditionItemDto>
  >({});
  const [existingAdmissionRecord, setExistingAdmissionRecord] =
    useState<AssetConditionRecordDto | null>(null);
  const [existingDischargeRecord, setExistingDischargeRecord] =
    useState<AssetConditionRecordDto | null>(null);
  const [isSubmittingInspection, setIsSubmittingInspection] = useState(false);
  const [inspectionAdmission, setInspectionAdmission] =
    useState<AssetAdmission | null>(null);
  const [itemPhotos, setItemPhotos] = useState<Record<string, File[]>>({});
  const [uploadingPhotoItemId, setUploadingPhotoItemId] = useState<
    string | null
  >(null);
  const [selectedInspectorId, setSelectedInspectorId] = useState<string>('');
  const photoInputRef = React.useRef<HTMLInputElement>(null);
  const openedUrlJobCardIdRef = React.useRef<string | null>(null);

  const upsertJobCardAdmissionState = React.useCallback(
    (
      jobCardId: string,
      admission: AssetAdmission,
      admissionRecord?: AssetConditionRecordDto | null
    ) => {
      setJobCardAdmissionStates((prev) => {
        const nextRecord =
          admissionRecord === undefined
            ? (prev[jobCardId]?.admissionRecord ?? null)
            : admissionRecord;

        return {
          ...prev,
          [jobCardId]: {
            admission,
            admissionRecord: nextRecord,
          },
        };
      });
    },
    []
  );

  const openAdmissionDialogForJobCard = React.useCallback((card: JobCard) => {
    setAdmissionForm((prev) => ({
      ...prev,
      assetId: card.assetId,
      jobCardId: card.id,
      workOrderId: card.generatedWorkOrderId || '',
    }));
    setIsAdmissionDialogOpen(true);
  }, []);

  const getAdmissionActionMeta = (card: JobCard) => {
    const admissionState = jobCardAdmissionStates[card.id];
    const admissionStatus = admissionState?.admission.status;
    const inspectionStatus = admissionState?.admissionRecord?.status;

    if (!admissionState || admissionStatus === 'Cancelled') {
      return {
        label: 'Admit Asset',
        title: 'Admit asset for maintenance',
        className: 'bg-blue-50 hover:bg-blue-100 border-blue-200 text-blue-700',
        iconClassName: 'text-blue-600',
      };
    }

    if (admissionStatus === 'Completed' || inspectionStatus === 'Completed') {
      return {
        label: 'Edit Admission',
        title: 'Open existing admission details',
        className:
          'bg-emerald-50 hover:bg-emerald-100 border-emerald-200 text-emerald-700',
        iconClassName: 'text-emerald-600',
      };
    }

    return {
      label: 'Continue Admission',
      title:
        inspectionStatus === 'InProgress'
          ? 'Continue admission checklist'
          : 'Continue asset admission',
      className:
        'bg-amber-50 hover:bg-amber-100 border-amber-300 text-amber-800',
      iconClassName: 'text-amber-600',
    };
  };

  // Approval dialog state
  const [isApprovalDialogOpen, setIsApprovalDialogOpen] = useState(false);
  const [approvalCardId, setApprovalCardId] = useState<string | null>(null);
  const [approvalBillingType, setApprovalBillingType] = useState<
    'Maintenance' | 'Repairs'
  >('Repairs');

  const [newJobCard, setNewJobCard] = useState<JobCardFormState>({
    title: '',
    description: '',
    assetName: '',
    assetId: '',
    priority: 'Medium' as const,
    maintenanceType: 'Preventive',
    maintenanceTypeId: '',
    priorityLevelId: '',
    customerBusinessPartnerId: 'none',
    workOrderBillingType: 'Repairs',
    estimatedHours: 0,
    estimatedCost: 0,
    problemDescription: '',
  });

  useEffect(() => {
    if (!openCreateFromFacilities) {
      return;
    }

    setIsCreateDialogOpen(true);
    setNewJobCard((current) => ({
      ...current,
      title: current.title || 'Facilities maintenance request',
      description:
        current.description ||
        'Source: Estate / Facilities. Facilities-originated maintenance request. Add the Facilities case reference, property/unit, requester, SLA target, and supporting notes before submission.',
      problemDescription:
        current.problemDescription ||
        'Source: Estate / Facilities. Maintenance issue raised from Estate > Facilities.',
    }));
  }, [openCreateFromFacilities]);

  const [editJobCard, setEditJobCard] = useState<JobCardFormState>({
    title: '',
    description: '',
    assetName: '',
    assetId: '',
    priority: 'Medium' as const,
    maintenanceType: '',
    maintenanceTypeId: '',
    priorityLevelId: '',
    customerBusinessPartnerId: 'none',
    workOrderBillingType: 'Repairs',
    estimatedHours: 0,
    estimatedCost: 0,
    problemDescription: '',
  });

  // Admission/Discharge data loader for current job card
  const loadAdmissionAndDischargeForJobCard = React.useCallback(
    async (jobCardId: string) => {
      try {
        const admissionsResult = await assetAdmissionService.getAdmissions({
          jobCardId,
          status: 'Active',
          page: 1,
          pageSize: 1,
        });

        const activeAdmission =
          admissionsResult.items && admissionsResult.items.length > 0
            ? admissionsResult.items[0]
            : null;

        setActiveAdmissionForJobCard(activeAdmission);

        if (activeAdmission) {
          const [discharges, admissionRecord] = await Promise.all([
            assetAdmissionService.getDischargesByAdmission(activeAdmission.id),
            activeAdmission.jobCardId
              ? assetConditionService.getAdmissionRecordForJobCard(
                  activeAdmission.jobCardId
                )
              : Promise.resolve(null),
          ]);
          if (activeAdmission.jobCardId) {
            upsertJobCardAdmissionState(
              activeAdmission.jobCardId,
              activeAdmission,
              admissionRecord
            );
          }
          if (discharges && discharges.length > 0) {
            setLatestDischargeForAdmission(discharges[discharges.length - 1]);
          } else {
            setLatestDischargeForAdmission(null);
          }
        } else {
          setLatestDischargeForAdmission(null);
        }
      } catch (error) {
        console.error('Error loading admission/discharge for job card:', error);
        setActiveAdmissionForJobCard(null);
        setLatestDischargeForAdmission(null);
      }
    },
    [upsertJobCardAdmissionState]
  );

  // Load data on component mount
  useEffect(() => {
    const loadData = async () => {
      setLoadingData(true);

      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Using mock data');

        // Mock data for testing
        const mockAssets: Asset[] = [
          {
            id: '1',
            name: 'HVAC Unit 1',
            assetNumber: 'HVAC-001',
            status: 'Active',
            criticality: 'Medium',
            assetCategoryId: '1',
          },
          {
            id: '2',
            name: 'Elevator Unit 1',
            assetNumber: 'ELEV-001',
            status: 'Active',
            criticality: 'High',
            assetCategoryId: '2',
          },
          {
            id: '3',
            name: 'Generator Unit 1',
            assetNumber: 'GEN-001',
            status: 'Active',
            criticality: 'Critical',
            assetCategoryId: '3',
          },
          {
            id: '4',
            name: 'Fire Pump System',
            assetNumber: 'FP-001',
            status: 'Active',
            criticality: 'High',
            assetCategoryId: '4',
          },
        ];

        const mockPriorityLevels: PriorityLevel[] = [
          {
            id: '1',
            name: 'Low',
            code: 'LOW',
            level: 1,
            isActive: true,
            responseTime: 72,
            escalationTime: 96,
            slaHours: 72,
            autoAssign: false,
          },
          {
            id: '2',
            name: 'Medium',
            code: 'MED',
            level: 2,
            isActive: true,
            responseTime: 48,
            escalationTime: 72,
            slaHours: 48,
            autoAssign: false,
          },
          {
            id: '3',
            name: 'High',
            code: 'HIGH',
            level: 3,
            isActive: true,
            responseTime: 24,
            escalationTime: 36,
            slaHours: 24,
            autoAssign: true,
          },
          {
            id: '4',
            name: 'Critical',
            code: 'CRIT',
            level: 4,
            isActive: true,
            responseTime: 4,
            escalationTime: 8,
            slaHours: 4,
            autoAssign: true,
          },
        ];

        const mockMaintenanceTypes = [
          { id: '1', name: 'Preventive' },
          { id: '2', name: 'Corrective' },
          { id: '3', name: 'Emergency' },
          { id: '4', name: 'Inspection' },
        ];

        const mockJobCards: JobCard[] = [
          {
            id: '1',
            jobCardNumber: 'JC-2024-001',
            title: 'HVAC Filter Replacement',
            description: 'Replace air filters and inspect system',
            assetName: 'HVAC Unit 1',
            requestedBy: 'John Doe',
            jobCardStatus: 'Draft',
            approvalStatus: 'NotStarted',
            priority: 'Medium',
            createdAt: new Date().toISOString(),
            estimatedHours: 4,
            estimatedCost: 250,
            maintenanceType: 'Preventive',
            assetId: '1',
            maintenanceTypeId: '1',
            priorityLevelId: '2',
          },
          {
            id: '2',
            jobCardNumber: 'JC-2024-002',
            title: 'Elevator Emergency Repair',
            description: 'Elevator malfunction - immediate attention required',
            assetName: 'Elevator Unit 1',
            requestedBy: 'Jane Smith',
            jobCardStatus: 'Submitted',
            approvalStatus: 'Pending',
            priority: 'Critical',
            createdAt: new Date(Date.now() - 86400000).toISOString(), // Yesterday
            estimatedHours: 8,
            estimatedCost: 1200,
            maintenanceType: 'Emergency',
            assetId: '2',
            maintenanceTypeId: '3',
            priorityLevelId: '4',
          },
        ];

        // Simulate loading delay
        await new Promise((resolve) => setTimeout(resolve, 1000));

        setAssets(mockAssets);
        setPriorityLevels(mockPriorityLevels);
        setMaintenanceTypes(mockMaintenanceTypes);
        setJobCards(mockJobCards);
        setFilteredCards(mockJobCards);

        console.log('✅ Mock data loaded successfully');
      } else {
        // Debug authentication token
        const authToken = localStorage.getItem('authToken');
        console.log(
          '🔑 Authentication token:',
          authToken ? 'Present' : 'Missing'
        );
        console.log(
          '🔑 Token preview:',
          authToken ? `${authToken.substring(0, 20)}...` : 'N/A'
        );

        try {
          const customerPartnerListPromise = safeLookup(
            'customer business partners',
            async () => {
              try {
                return await businessPartnerService.getActivePartners(
                  'Customer'
                );
              } catch {
                const result = await businessPartnerService.getPartners({
                  page: 1,
                  pageSize: 100,
                  partnerType: 'Customer',
                  status: 'Active',
                });
                return result.items || [];
              }
            },
            [] as BusinessPartnerDto[]
          );

          const [
            assetsResponse,
            priorityLevelsList,
            maintenanceTypesList,
            techniciansList,
            usersList,
            customerPartnersList,
          ] = await Promise.all([
            safeLookup(
              'maintenance assets',
              () => maintenanceApiService.getAssets(),
              { items: [] } as any
            ),
            safeLookup(
              'priority levels',
              () => maintenanceApiService.getPriorityLevels(),
              [] as PriorityLevel[]
            ),
            safeLookup(
              'maintenance types',
              () => maintenanceApiService.getMaintenanceTypes(),
              [] as any[]
            ),
            safeLookup(
              'technicians',
              () => maintenanceDataService.getTechnicians(),
              [] as Employee[]
            ),
            safeLookup('users', () => adminApiService.getUsers(), [] as User[]),
            customerPartnerListPromise,
          ]);

          console.log('Loaded assets:', assetsResponse);
          console.log('Loaded priority levels:', priorityLevelsList);
          console.log('Loaded maintenance types:', maintenanceTypesList);
          console.log('Loaded technicians:', techniciansList);
          console.log('Loaded users:', usersList);

          setAssets(assetsResponse.items || []);
          setPriorityLevels(priorityLevelsList || []);
          setMaintenanceTypes(maintenanceTypesList || []);
          setTechnicians(Array.isArray(techniciansList) ? techniciansList : []);
          setUsers(
            Array.isArray(usersList) ? usersList.filter((u) => u.isActive) : []
          );
          setCustomerBusinessPartners(
            Array.isArray(customerPartnersList) ? customerPartnersList : []
          );

          // Job cards are loaded separately based on the current Has Work Order filter
        } catch (error) {
          console.error('❌ Error loading data:', error);
          console.error(
            'Error details:',
            error instanceof Error ? error.message : error
          );

          // Check if it's an authentication error
          if (
            error instanceof Error &&
            (error.message.includes('401') ||
              error.message.includes('Unauthorized'))
          ) {
            console.error(
              '🚨 Authentication error detected. Please check your login status.'
            );
          }

          setAssets([]);
          setPriorityLevels([]);
          setMaintenanceTypes([]);
          setCustomerBusinessPartners([]);
        }
      }

      setLoadingData(false);
    };

    loadData();
  }, []);

  const handleCreateAdmissionFromJobCard = async (
    openConditionChecklist = false
  ) => {
    if (!admissionForm.assetId || !admissionForm.jobCardId) {
      toast({
        title: 'Missing data',
        description:
          'Asset or Job Card information is missing for this admission.',
        variant: 'destructive',
      });
      return;
    }

    setIsSubmittingAdmission(true);
    try {
      const request: CreateAdmissionRequest = {
        assetId: admissionForm.assetId,
        jobCardId: admissionForm.jobCardId || undefined,
        workOrderId: admissionForm.workOrderId || undefined,
        admissionType: admissionForm.admissionType,
        assetConditionOnAdmission: admissionForm.assetConditionOnAdmission,
        admissionNotes: admissionForm.admissionNotes || undefined,
        observedProblems: admissionForm.observedProblems || undefined,
        mileageReading: admissionForm.mileageReading || undefined,
        hoursReading: admissionForm.hoursReading || undefined,
        fuelLevel: admissionForm.fuelLevel || undefined,
        admissionLocation: admissionForm.admissionLocation || undefined,
        bayOrStation: admissionForm.bayOrStation || undefined,
        estimatedCompletionDate:
          admissionForm.estimatedCompletionDate || undefined,
        estimatedDischargeDate:
          admissionForm.estimatedDischargeDate || undefined,
      };

      const createdAdmission =
        await assetAdmissionService.createAdmission(request);
      const asset = assets.find((a) => a.id === admissionForm.assetId);
      const admissionWithDetails: AssetAdmission = {
        ...createdAdmission,
        assetName: createdAdmission.assetName || asset?.name || '',
        assetNumber: createdAdmission.assetNumber || asset?.assetNumber || '',
      };

      toast({
        title: 'Admission created',
        description: 'Asset has been admitted for this job card.',
      });

      setIsAdmissionDialogOpen(false);

      if (admissionForm.jobCardId) {
        upsertJobCardAdmissionState(
          admissionForm.jobCardId,
          admissionWithDetails,
          null
        );
      }

      // If the user wants to fill the condition checklist, open it
      if (openConditionChecklist && createdAdmission) {
        handleOpenConditionInspection(admissionWithDetails, 'Admission');
      }

      setAdmissionForm({
        assetId: '',
        jobCardId: '',
        workOrderId: '',
        admissionType: 'Scheduled',
        assetConditionOnAdmission: 'Good',
        admissionNotes: '',
        observedProblems: '',
        mileageReading: 0,
        hoursReading: 0,
        fuelLevel: 0,
        admissionLocation: '',
        bayOrStation: '',
        estimatedCompletionDate: '',
        estimatedDischargeDate: '',
      });
    } catch (error: any) {
      console.error('Error creating admission from job card:', error);
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create admission',
        variant: 'destructive',
      });
    } finally {
      setIsSubmittingAdmission(false);
    }
  };

  // Reset all condition inspection state - call this when switching job cards
  const resetConditionInspectionState = () => {
    setConditionTemplates([]);
    setSelectedTemplate(null);
    setCurrentInspection(null);
    setInspectionType('Admission');
    setItemResponses({});
    setExistingAdmissionRecord(null);
    setExistingDischargeRecord(null);
    setInspectionAdmission(null);
    setIsConditionDialogOpen(false);
    setItemPhotos({});
    setUploadingPhotoItemId(null);
    setSelectedInspectorId('');
  };

  // Photo upload handler for checklist items
  const handlePhotoUpload = async (itemId: string, files: FileList | null) => {
    if (!files || files.length === 0 || !currentInspection) return;

    setUploadingPhotoItemId(itemId);
    try {
      const file = files[0];
      // Upload the photo - returns { filePath: string }
      const uploadResult = await assetAdmissionService.uploadAdmissionPhoto(
        currentInspection.id,
        file
      );

      // Update local state to show the photo
      setItemPhotos((prev) => ({
        ...prev,
        [itemId]: [...(prev[itemId] || []), file],
      }));

      // Update the item response with the photo path (extract the string from the object)
      setItemResponses((prev) => ({
        ...prev,
        [itemId]: {
          ...prev[itemId],
          checklistItemId: itemId,
          photoPaths: [
            ...(prev[itemId]?.photoPaths || []),
            uploadResult.filePath,
          ],
        },
      }));

      toast({
        title: 'Photo uploaded',
        description: 'Photo has been attached to this checklist item',
      });
    } catch (err) {
      console.error('Error uploading photo:', err);
      toast({
        title: 'Upload failed',
        description: 'Failed to upload photo. Please try again.',
        variant: 'destructive',
      });
    } finally {
      setUploadingPhotoItemId(null);
    }
  };

  // Trigger photo input for a specific item
  const triggerPhotoUpload = (itemId: string) => {
    setUploadingPhotoItemId(itemId);
    photoInputRef.current?.click();
  };

  // Condition Inspection handlers (at Job Card level)
  const handleOpenConditionInspection = async (
    admission: AssetAdmission,
    type: 'Admission' | 'Discharge'
  ) => {
    // Use the admission's jobCardId directly, fallback to selectedCardDetails
    const jobCardId = admission.jobCardId || selectedCardDetails?.id;
    console.log(
      '🔍 handleOpenConditionInspection called with jobCardId:',
      jobCardId,
      'type:',
      type
    );
    setInspectionAdmission(admission);
    setInspectionType(type);
    setItemResponses({});
    setSelectedTemplate(null);
    setCurrentInspection(null);
    setExistingAdmissionRecord(null);
    setExistingDischargeRecord(null);

    if (!jobCardId) {
      console.error('❌ No job card ID available');
      setIsConditionDialogOpen(true);
      return;
    }

    try {
      // Use JobCardId to find admission, then get condition records
      console.log('🔍 Fetching existing records for job card:', jobCardId);
      const [admissionRecord, dischargeRecord] = await Promise.all([
        assetConditionService.getAdmissionRecordForJobCard(jobCardId),
        assetConditionService.getDischargeRecordForJobCard(jobCardId),
      ]);
      console.log('🔍 Existing admission record:', admissionRecord);
      console.log('🔍 Existing discharge record:', dischargeRecord);
      setExistingAdmissionRecord(admissionRecord);
      setExistingDischargeRecord(dischargeRecord);

      // Get templates for the asset's category first
      const asset = assets.find((a) => a.id === admission.assetId);
      let templates: AssetConditionChecklistTemplateDto[] = [];
      if (asset?.assetCategoryId) {
        templates = await assetConditionService.getTemplatesByAssetCategory(
          asset.assetCategoryId
        );
        setConditionTemplates(templates);
      } else {
        templates = await assetConditionService.getAllTemplates(false);
        setConditionTemplates(templates);
      }

      // If showing an existing record, load it
      if (type === 'Admission' && admissionRecord) {
        console.log(
          '✅ Found existing ADMISSION record, loading it:',
          admissionRecord
        );
        console.log('   - Status:', admissionRecord.status);
        console.log('   - TemplateId:', admissionRecord.templateId);
        console.log(
          '   - ItemResults count:',
          admissionRecord.itemResults?.length || 0
        );
        setCurrentInspection(admissionRecord);
        // Find and set the template used in the record
        const template = templates.find(
          (t) => t.id === admissionRecord.templateId
        );
        console.log(
          '   - Found template:',
          template ? template.name : 'NOT FOUND'
        );
        if (template) {
          setSelectedTemplate(template);
        }
        const responses: Record<string, SubmitAssetConditionItemDto> = {};
        admissionRecord.itemResults.forEach((result) => {
          responses[result.checklistItemId] = {
            checklistItemId: result.checklistItemId,
            isPresent: result.isPresent,
            textValue: result.textValue,
            numericValue: result.numericValue,
            selectedOption: result.selectedOption,
            comment: result.comment,
            repairReplacementAction: result.repairReplacementAction as
              'None' | 'Repair' | 'Replace' | undefined,
            photoPaths: result.photoPaths,
          };
        });
        setItemResponses(responses);
        console.log(
          '   - Populated responses:',
          Object.keys(responses).length,
          'items'
        );
      } else if (type === 'Discharge' && dischargeRecord) {
        console.log(
          '✅ Found existing DISCHARGE record, loading it:',
          dischargeRecord
        );
        setCurrentInspection(dischargeRecord);
        // Find and set the template used in the record
        const template = templates.find(
          (t) => t.id === dischargeRecord.templateId
        );
        if (template) {
          setSelectedTemplate(template);
        }
        const responses: Record<string, SubmitAssetConditionItemDto> = {};
        dischargeRecord.itemResults.forEach((result) => {
          responses[result.checklistItemId] = {
            checklistItemId: result.checklistItemId,
            isPresent: result.isPresent,
            textValue: result.textValue,
            numericValue: result.numericValue,
            selectedOption: result.selectedOption,
            comment: result.comment,
            repairReplacementAction: result.repairReplacementAction as
              'None' | 'Repair' | 'Replace' | undefined,
            photoPaths: result.photoPaths,
          };
        });
        setItemResponses(responses);
      } else {
        console.log('❌ No existing record found for type:', type);
        console.log('   - Admission record:', admissionRecord);
        console.log('   - Discharge record:', dischargeRecord);
        // No existing record - auto-select default template
        const defaultTemplate =
          templates.find((t) => t.isDefault) || templates[0];
        if (defaultTemplate) {
          setSelectedTemplate(defaultTemplate);
        }
      }
    } catch (err) {
      console.error('Error loading condition inspection data:', err);
    }

    setIsConditionDialogOpen(true);
  };

  const handleAdmissionGridAction = async (card: JobCard) => {
    const admissionState = jobCardAdmissionStates[card.id];
    const existingAdmission = admissionState?.admission;

    if (!existingAdmission || existingAdmission.status === 'Cancelled') {
      openAdmissionDialogForJobCard(card);
      return;
    }

    const admissionForInspection: AssetAdmission = {
      ...existingAdmission,
      jobCardId: existingAdmission.jobCardId || card.id,
      workOrderId: existingAdmission.workOrderId || card.generatedWorkOrderId,
      assetName: existingAdmission.assetName || card.assetName,
      assetNumber: existingAdmission.assetNumber || card.assetCode || '',
    };

    setSelectedCard(card);
    setActiveAdmissionForJobCard(admissionForInspection);
    upsertJobCardAdmissionState(
      card.id,
      admissionForInspection,
      admissionState.admissionRecord
    );
    await handleOpenConditionInspection(admissionForInspection, 'Admission');
  };

  const handleViewJobCard = async (card: JobCard) => {
    resetConditionInspectionState();
    setSelectedWorkOrder(null);
    setSelectedQCInspection(null);
    setActiveAdmissionForJobCard(null);
    setLatestDischargeForAdmission(null);

    setSelectedCard(card);
    setIsViewDialogOpen(true);
    setLoadingDetails(true);
    try {
      const details = await jobCardService.getJobCardById(card.id);
      setSelectedCardDetails(details);
      if (details.id) {
        await loadAdmissionAndDischargeForJobCard(details.id);
      }

      if (details.generatedWorkOrderId) {
        try {
          const workOrder = await workOrderService.getWorkOrderById(
            details.generatedWorkOrderId
          );
          setSelectedWorkOrder(workOrder);

          try {
            const [schedules, expenses, tools, summary, totalExpenses] =
              await Promise.all([
                maintenanceApiService.getStaffSchedulesByWorkOrder(
                  workOrder.id
                ),
                maintenanceApiService.getExpensesByWorkOrder(workOrder.id),
                workOrderToolService.getWorkOrderTools(workOrder.id),
                workOrderToolService.getToolSummary(workOrder.id),
                maintenanceApiService.getTotalExpensesByWorkOrder(workOrder.id),
              ]);

            const enrichedSchedules = (
              Array.isArray(schedules) ? schedules : []
            ).map((schedule) => {
              const technician = technicians.find(
                (t) => t.id === schedule.technicianId
              );
              if (
                technician &&
                !schedule.technicianFullName &&
                !schedule.technicianName
              ) {
                return {
                  ...schedule,
                  technicianFullName: `${technician.firstName} ${technician.lastName}`,
                  technicianName: `${technician.firstName} ${technician.lastName}`,
                };
              }
              return schedule;
            });

            setWorkOrderStaffSchedules(enrichedSchedules);
            setWorkOrderExpenses(Array.isArray(expenses) ? expenses : []);
            setWorkOrderTools(Array.isArray(tools) ? tools : []);
            setToolSummaryForSnapshot(summary || null);
            setTotalExpensesForSnapshot(
              typeof totalExpenses === 'number' ? totalExpenses : 0
            );
            setWorkOrderLaborForSnapshot(
              Array.isArray((workOrder as any).labor)
                ? (workOrder as any).labor
                : []
            );
          } catch (resourceError) {
            console.error(
              'Error loading schedules/tools/expenses for job card work order:',
              resourceError
            );
            setWorkOrderStaffSchedules([]);
            setWorkOrderExpenses([]);
            setWorkOrderTools([]);
            setToolSummaryForSnapshot(null);
            setTotalExpensesForSnapshot(0);
            setWorkOrderLaborForSnapshot([]);
          }

          if (workOrder.status === 'Completed') {
            try {
              const inspections =
                await qualityControlService.getCompletedInspections();
              const qcInspection = inspections.find(
                (insp: any) => insp.workOrderId === workOrder.id
              );
              setSelectedQCInspection(qcInspection || null);
            } catch {
              console.log('No QC inspection found');
            }
          }
        } catch {
          console.log('Work order not found or not accessible');
        }
      }
    } catch (error) {
      console.error('Error loading job card details:', error);
      toast({
        title: 'Error',
        description: 'Failed to load job card details',
        variant: 'destructive',
      });
    } finally {
      setLoadingDetails(false);
    }
  };

  const handleEditJobCard = (card: JobCard) => {
    setSelectedCard(card);
    setEditJobCard({
      title: card.title,
      description: card.description || '',
      assetName: card.assetName,
      assetId: card.assetId,
      priority: card.priority,
      maintenanceType: card.maintenanceType,
      maintenanceTypeId: card.maintenanceTypeId,
      priorityLevelId: card.priorityLevelId,
      customerBusinessPartnerId: card.customerBusinessPartnerId || 'none',
      workOrderBillingType: card.workOrderBillingType || 'Repairs',
      estimatedHours: card.estimatedHours,
      estimatedCost: card.estimatedCost,
      problemDescription: card.problemDescription || '',
    });
    setIsEditDialogOpen(true);
  };

  const handleStartInspection = async () => {
    if (!inspectionAdmission || !selectedTemplate) return;

    try {
      setIsSubmittingInspection(true);
      const dto: CreateAssetConditionRecordDto = {
        assetId: inspectionAdmission.assetId,
        templateId: selectedTemplate.id,
        inspectionType: inspectionType,
        admissionId: inspectionAdmission.id,
        inspectorId: selectedInspectorId || undefined,
        generalNotes: '',
      };

      const record = await assetConditionService.startConditionInspection(dto);
      setCurrentInspection(record);
      if (inspectionType === 'Admission') {
        setExistingAdmissionRecord(record);
        const jobCardId =
          inspectionAdmission.jobCardId ||
          selectedCard?.id ||
          selectedCardDetails?.id;
        if (jobCardId) {
          upsertJobCardAdmissionState(jobCardId, inspectionAdmission, record);
        }
      }

      // Initialize responses
      const responses: Record<string, SubmitAssetConditionItemDto> = {};
      selectedTemplate.checklistItems.forEach((item) => {
        responses[item.id] = {
          checklistItemId: item.id,
          isPresent: undefined,
          textValue: '',
          numericValue: undefined,
          selectedOption: '',
          comment: '',
          repairReplacementAction: item.allowRepairReplacement
            ? item.defaultRepairReplacementAction || 'None'
            : undefined,
        };
      });
      setItemResponses(responses);
    } catch (err) {
      console.error('Error starting inspection:', err);
      toast({
        title: 'Error',
        description: 'Failed to start condition inspection',
        variant: 'destructive',
      });
    } finally {
      setIsSubmittingInspection(false);
    }
  };

  const handleSaveDraft = async () => {
    if (!currentInspection) {
      setIsConditionDialogOpen(false);
      return;
    }

    try {
      setIsSubmittingInspection(true);

      // Submit all responses to save them as draft
      for (const itemId of Object.keys(itemResponses)) {
        const response = itemResponses[itemId];
        // Only submit if there's actual data
        if (
          response.isPresent !== undefined ||
          response.textValue ||
          response.numericValue !== undefined ||
          response.selectedOption ||
          response.comment ||
          (response.repairReplacementAction &&
            response.repairReplacementAction !== 'None')
        ) {
          await assetConditionService.submitItemResult(
            currentInspection.id,
            response
          );
        }
      }

      toast({
        title: 'Draft Saved',
        description:
          'Your inspection draft has been saved. You can resume it later.',
      });

      setIsConditionDialogOpen(false);
    } catch (err) {
      console.error('Error saving draft:', err);
      toast({
        title: 'Error',
        description: 'Failed to save draft. Please try again.',
        variant: 'destructive',
      });
    } finally {
      setIsSubmittingInspection(false);
    }
  };

  const handleCompleteInspection = async () => {
    if (!currentInspection) return;

    try {
      setIsSubmittingInspection(true);

      // Submit all responses
      for (const itemId of Object.keys(itemResponses)) {
        await assetConditionService.submitItemResult(
          currentInspection.id,
          itemResponses[itemId]
        );
      }

      // Complete the inspection
      const completedInspection =
        await assetConditionService.completeInspection(currentInspection.id, {
          generalNotes: '',
        });
      if (inspectionType === 'Admission' && inspectionAdmission) {
        setExistingAdmissionRecord(completedInspection);
        const jobCardId =
          inspectionAdmission.jobCardId ||
          selectedCard?.id ||
          selectedCardDetails?.id;
        if (jobCardId) {
          upsertJobCardAdmissionState(
            jobCardId,
            inspectionAdmission,
            completedInspection
          );
        }
      }

      toast({
        title: 'Inspection completed',
        description: `${inspectionType} condition inspection saved successfully`,
      });

      setIsConditionDialogOpen(false);
      setCurrentInspection(null);
      setItemResponses({});
    } catch (err) {
      console.error('Error completing inspection:', err);
      toast({
        title: 'Error',
        description: 'Failed to complete condition inspection',
        variant: 'destructive',
      });
    } finally {
      setIsSubmittingInspection(false);
    }
  };

  const refreshJobCards = async () => {
    if (TESTING_MODE) {
      console.log('🧪 TESTING MODE: Skipping job card refresh');
      return;
    }

    console.log('🔄 Refreshing job cards from API...');

    try {
      const jobCardsResponse = await jobCardService.getJobCards({
        hasWorkOrder:
          hasWorkOrderFilter === 'all'
            ? undefined
            : hasWorkOrderFilter === 'with',
      });
      const mappedJobCards: JobCard[] = jobCardsResponse.items.map(
        mapJobCardResponseToGridCard
      );
      setJobCards(mappedJobCards);
      setFilteredCards(mappedJobCards);
      console.log(
        '✅ Job cards refreshed successfully:',
        mappedJobCards.length,
        'cards loaded'
      );

      // Load admission status for all job cards
      const allJobCardIds = mappedJobCards.map((card: JobCard) => card.id);

      if (allJobCardIds.length > 0) {
        await loadAdmissionStatusForJobCards(allJobCardIds);
      } else {
        setJobCardAdmissionStates({});
      }
    } catch (error) {
      console.error('❌ Error refreshing job cards:', error);
      console.error(
        'Error details:',
        error instanceof Error ? error.message : error
      );
    }
  };

  // Load admission status for a list of job cards
  const loadAdmissionStatusForJobCards = async (jobCardIds: string[]) => {
    try {
      const admissionEntries = await Promise.all(
        jobCardIds.map(
          async (
            jobCardId
          ): Promise<[string, JobCardAdmissionState] | null> => {
            try {
              const admissions = await assetAdmissionService.getAdmissions({
                jobCardId,
                pageSize: 5,
              });

              const admission =
                admissions.items?.find((item) => item.status !== 'Cancelled') ||
                null;
              if (!admission) {
                return null;
              }

              const admissionRecord =
                await assetConditionService.getAdmissionRecordForJobCard(
                  jobCardId
                );
              return [jobCardId, { admission, admissionRecord }];
            } catch {
              // Ignore errors for individual job cards
              return null;
            }
          }
        )
      );

      const admissionStateMap: Record<string, JobCardAdmissionState> = {};
      admissionEntries.forEach((entry) => {
        if (entry) {
          admissionStateMap[entry[0]] = entry[1];
        }
      });

      setJobCardAdmissionStates(admissionStateMap);
      console.log(
        '✅ Loaded admission status for job cards:',
        Object.keys(admissionStateMap).length,
        'have admissions'
      );
    } catch (error) {
      console.error('❌ Error loading admission status:', error);
    }
  };

  // Load job cards from API whenever the Has Work Order filter changes (and on initial mount)
  useEffect(() => {
    if (TESTING_MODE) {
      return;
    }

    refreshJobCards();
  }, [hasWorkOrderFilter]);

  useEffect(() => {
    let filtered = jobCards;

    if (searchTerm) {
      filtered = filtered.filter(
        (card) =>
          card.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
          (card.assetName &&
            card.assetName.toLowerCase().includes(searchTerm.toLowerCase())) ||
          card.jobCardNumber.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (statusFilter && statusFilter !== 'all') {
      filtered = filtered.filter((card) => card.jobCardStatus === statusFilter);
    }

    if (priorityFilter && priorityFilter !== 'all') {
      filtered = filtered.filter((card) => card.priority === priorityFilter);
    }

    if (hasWorkOrderFilter !== 'all') {
      filtered = filtered.filter((card) => {
        const hasWO = !!card.generatedWorkOrderId;
        return hasWorkOrderFilter === 'with' ? hasWO : !hasWO;
      });
    }

    if (dateRange?.from) {
      const { from } = dateRange;
      filtered = filtered.filter((card) => new Date(card.createdAt) >= from);
    }

    if (dateRange?.to) {
      const end = new Date(dateRange.to);
      end.setHours(23, 59, 59, 999);
      filtered = filtered.filter((card) => new Date(card.createdAt) <= end);
    }

    setFilteredCards(filtered);
  }, [
    jobCards,
    searchTerm,
    statusFilter,
    priorityFilter,
    hasWorkOrderFilter,
    dateRange,
  ]);

  // Handle opening job card from URL parameter
  useEffect(() => {
    const jobCardId = searchParams?.get('id');
    if (!jobCardId) {
      openedUrlJobCardIdRef.current = null;
      return;
    }

    if (
      isViewDialogOpen ||
      loadingData ||
      openedUrlJobCardIdRef.current === jobCardId
    ) {
      return;
    }

    openedUrlJobCardIdRef.current = jobCardId;
    console.log('Opening job card from URL:', jobCardId);

    resetConditionInspectionState();
    setSelectedWorkOrder(null);
    setSelectedQCInspection(null);
    setActiveAdmissionForJobCard(null);
    setLatestDischargeForAdmission(null);
    setLoadingDetails(true);

    (async () => {
      try {
        const details = await jobCardService.getJobCardById(jobCardId);
        const jobCard =
          jobCards.find((jc) => jc.id === jobCardId) ??
          mapJobCardResponseToGridCard(details);

        setSelectedCard(jobCard);
        setSelectedCardDetails(details);
        setIsViewDialogOpen(true);
        setJobCards((prev) =>
          prev.some((card) => card.id === jobCard.id)
            ? prev
            : [jobCard, ...prev]
        );

        if (details.id) {
          await loadAdmissionAndDischargeForJobCard(details.id);
        }

        // Load work order if it exists
        if (details.generatedWorkOrderId) {
          try {
            const workOrder = await workOrderService.getWorkOrderById(
              details.generatedWorkOrderId
            );
            setSelectedWorkOrder(workOrder);

            // Load technician schedules, tools, and expenses for this work order
            try {
              const [schedules, expenses, tools, summary, totalExpenses] =
                await Promise.all([
                  maintenanceApiService.getStaffSchedulesByWorkOrder(
                    workOrder.id
                  ),
                  maintenanceApiService.getExpensesByWorkOrder(workOrder.id),
                  workOrderToolService.getWorkOrderTools(workOrder.id),
                  workOrderToolService.getToolSummary(workOrder.id),
                  maintenanceApiService.getTotalExpensesByWorkOrder(
                    workOrder.id
                  ),
                ]);

              const enrichedSchedules = (
                Array.isArray(schedules) ? schedules : []
              ).map((schedule) => {
                const technician = technicians.find(
                  (t) => t.id === schedule.technicianId
                );
                if (
                  technician &&
                  !schedule.technicianFullName &&
                  !schedule.technicianName
                ) {
                  return {
                    ...schedule,
                    technicianFullName: `${technician.firstName} ${technician.lastName}`,
                    technicianName: `${technician.firstName} ${technician.lastName}`,
                  };
                }
                return schedule;
              });

              setWorkOrderStaffSchedules(enrichedSchedules);
              setWorkOrderExpenses(Array.isArray(expenses) ? expenses : []);
              setWorkOrderTools(Array.isArray(tools) ? tools : []);
              setToolSummaryForSnapshot(summary || null);
              setTotalExpensesForSnapshot(
                typeof totalExpenses === 'number' ? totalExpenses : 0
              );
              setWorkOrderLaborForSnapshot(
                Array.isArray((workOrder as any).labor)
                  ? (workOrder as any).labor
                  : []
              );
            } catch (resourceError) {
              console.error(
                'Error loading schedules/tools/expenses for job card work order:',
                resourceError
              );
              setWorkOrderStaffSchedules([]);
              setWorkOrderExpenses([]);
              setWorkOrderTools([]);
              setToolSummaryForSnapshot(null);
              setTotalExpensesForSnapshot(0);
              setWorkOrderLaborForSnapshot([]);
            }

            // Load QC inspection if work order is completed
            if (workOrder.status === 'Completed') {
              try {
                const inspections =
                  await qualityControlService.getCompletedInspections();
                const qcInspection = inspections.find(
                  (insp: any) => insp.workOrderId === workOrder.id
                );
                setSelectedQCInspection(qcInspection || null);
              } catch (qcError) {
                console.log('No QC inspection found');
              }
            }
          } catch (woError) {
            console.log('Work order not found or not accessible');
          }
        } else {
          setSelectedWorkOrder(null);
          setWorkOrderStaffSchedules([]);
          setWorkOrderExpenses([]);
          setWorkOrderTools([]);
          setToolSummaryForSnapshot(null);
          setTotalExpensesForSnapshot(0);
          setWorkOrderLaborForSnapshot([]);
        }
      } catch (error) {
        openedUrlJobCardIdRef.current = null;
        console.error('Error loading job card details from URL:', error);
        toast({
          title: 'Error',
          description: 'Failed to load the requested job card',
          variant: 'destructive',
        });
      } finally {
        setLoadingDetails(false);
      }
    })();
  }, [
    jobCards,
    searchParams,
    isViewDialogOpen,
    loadingData,
    toast,
    technicians,
  ]);

  const handleCreateJobCard = async () => {
    try {
      // Find selected asset and other data
      const selectedAsset = assets.find((a) => a.name === newJobCard.assetName);
      const selectedPriority = priorityLevels.find(
        (pl) => pl.name === newJobCard.priority
      );
      const selectedMaintenanceType = maintenanceTypes.find(
        (mt) => mt.name === newJobCard.maintenanceType
      );

      if (!selectedAsset || !selectedPriority || !selectedMaintenanceType) {
        console.error('Missing required selections');
        return;
      }

      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Creating mock job card');

        // Create mock job card
        const newMockCard: JobCard = {
          id: `mock-${Date.now()}`,
          jobCardNumber: `JC-${new Date().getFullYear()}-${String(jobCards.length + 1).padStart(3, '0')}`,
          title: newJobCard.title,
          description: newJobCard.description,
          assetName: selectedAsset.name,
          requestedBy: 'Current User',
          jobCardStatus: 'Draft',
          approvalStatus: 'NotStarted',
          priority: newJobCard.priority as JobCard['priority'],
          createdAt: new Date().toISOString(),
          estimatedHours: newJobCard.estimatedHours,
          estimatedCost: newJobCard.estimatedCost,
          maintenanceType: newJobCard.maintenanceType,
          workOrderBillingType: newJobCard.workOrderBillingType,
          assetId: selectedAsset.id,
          maintenanceTypeId: selectedMaintenanceType.id,
          priorityLevelId: selectedPriority.id,
        };

        setJobCards((prev) => [newMockCard, ...prev]);
        setFilteredCards((prev) => [newMockCard, ...prev]);
        console.log('✅ Mock job card created:', newMockCard.jobCardNumber);
      } else {
        // Create job card using real API
        const createRequest: CreateJobCardRequest = {
          title: newJobCard.title,
          description: newJobCard.description,
          problemDescription: newJobCard.problemDescription,
          assetId: selectedAsset.id,
          maintenanceTypeId: selectedMaintenanceType.id,
          priorityLevelId: selectedPriority.id,
          customerBusinessPartnerId:
            newJobCard.customerBusinessPartnerId !== 'none'
              ? newJobCard.customerBusinessPartnerId
              : undefined,
          maintenanceLocation: 'Internal',
          workOrderBillingType: newJobCard.workOrderBillingType,
          estimatedHours: newJobCard.estimatedHours,
          estimatedCost: newJobCard.estimatedCost,
          customFieldValues: {
            workOrderBillingType: newJobCard.workOrderBillingType,
          },
          requiresSpecialTools: false,
          requiresShutdown: false,
          requiresSafetyPermit: false,
        };

        console.log('📤 Creating job card with request:', createRequest);
        const createdJobCard =
          await jobCardService.createJobCard(createRequest);
        console.log('✅ Job card created successfully:', createdJobCard);

        // Refresh job cards list
        await refreshJobCards();

        toast({
          title: 'Success',
          description: `Job card ${createdJobCard.jobCardNumber} created successfully`,
        });
      }

      setIsCreateDialogOpen(false);
      setNewJobCard({
        title: '',
        description: '',
        assetName: '',
        assetId: '',
        priority: 'Medium',
        maintenanceType: 'Preventive',
        maintenanceTypeId: '',
        priorityLevelId: '',
        customerBusinessPartnerId: 'none',
        workOrderBillingType: 'Repairs',
        estimatedHours: 0,
        estimatedCost: 0,
        problemDescription: '',
      });
    } catch (error) {
      console.error('❌ Error creating job card:', error);

      // Log additional error details for debugging
      if (error instanceof Error) {
        console.error('Error message:', error.message);
        console.error('Error stack:', error.stack);
      }

      // Check if it's an Axios error with response data
      let errorMessage = 'Failed to create job card. Please try again.';
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as any;
        console.error('Response status:', axiosError.response?.status);
        console.error('Response data:', axiosError.response?.data);
        console.error('Response headers:', axiosError.response?.headers);
        errorMessage =
          axiosError.response?.data?.message ||
          axiosError.response?.data ||
          errorMessage;
      }

      toast({
        title: 'Error',
        description: errorMessage,
        variant: 'destructive',
      });
    }
  };

  const handleDownloadDocument = async (doc: JobCardDocument) => {
    if (!selectedCardDetails?.id) {
      return;
    }

    try {
      const blob = await jobCardService.downloadDocument(
        selectedCardDetails.id,
        doc.id
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = safeFileName(doc.fileName);
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error: any) {
      const message =
        error?.response?.data?.message ||
        error?.message ||
        'Failed to download document';
      toast({
        title: 'Download failed',
        description: message,
        variant: 'destructive',
      });
    }
  };

  const handleSort = (column: string) => {
    if (sortColumn === column) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(column);
      setSortDirection('asc');
    }
  };

  const getSortedCards = () => {
    const sorted = [...filteredCards].sort((a, b) => {
      let aVal: any = a[sortColumn as keyof JobCard];
      let bVal: any = b[sortColumn as keyof JobCard];

      if (aVal === null || aVal === undefined) aVal = '';
      if (bVal === null || bVal === undefined) bVal = '';

      if (typeof aVal === 'string') {
        aVal = aVal.toLowerCase();
        bVal = bVal.toLowerCase();
      }

      if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
      if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
      return 0;
    });
    return sorted;
  };

  const SortHeader = ({
    label,
    column,
    className = '',
  }: {
    label: string;
    column: string;
    className?: string;
  }) => (
    <TableHead className={`h-12 px-4 ${className}`}>
      <div
        className="cursor-pointer hover:bg-gray-100 select-none p-2 rounded inline-flex items-center gap-2"
        onClick={() => handleSort(column)}
      >
        <span>{label}</span>
        {sortColumn === column ? (
          sortDirection === 'asc' ? (
            <ArrowUp className="h-4 w-4" />
          ) : (
            <ArrowDown className="h-4 w-4" />
          )
        ) : (
          <ArrowUpDown className="h-4 w-4 opacity-30" />
        )}
      </div>
    </TableHead>
  );

  const getStatusBadge = (
    jobCardStatus: string,
    approvalStatus: string,
    currentStepName?: string,
    awaitingApproval = false,
    title?: string
  ) => {
    const statusColors = {
      Draft: 'bg-gray-100 text-gray-800',
      Submitted: 'bg-blue-100 text-blue-800',
      UnderReview: 'bg-yellow-100 text-yellow-800',
      Approved: 'bg-green-100 text-green-800',
      Rejected: 'bg-red-100 text-red-800',
      Cancelled: 'bg-red-100 text-red-800',
    } satisfies Record<JobCardStatus, string>;

    const displayStatus = awaitingApproval
      ? currentStepName || 'Awaiting Approval'
      : jobCardStatus === 'Approved' && approvalStatus === 'ChangesRequested'
        ? 'Changes Requested'
        : jobCardStatus;

    return (
      <Badge
        title={title}
        className={`${statusColors[jobCardStatus as JobCardStatus] || 'bg-gray-100 text-gray-800'} h-5 max-w-[8.5rem] truncate whitespace-nowrap px-2 py-0 text-[11px] leading-5`}
      >
        {displayStatus}
      </Badge>
    );
  };

  const getPendingApproverBadge = (shortLabel: string, fullLabel?: string) => {
    if (!shortLabel) return null;

    return (
      <Badge
        variant="outline"
        title={fullLabel ? `Pending: ${fullLabel}` : `Pending: ${shortLabel}`}
        className="h-5 max-w-[8rem] truncate whitespace-nowrap px-2 py-0 text-[11px] leading-5 text-slate-600"
      >
        Pending: {shortLabel}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const colors = {
      Low: 'bg-green-100 text-green-800',
      Medium: 'bg-blue-100 text-blue-800',
      High: 'bg-orange-100 text-orange-800',
      Critical: 'bg-red-100 text-red-800',
    } satisfies Record<JobCardPriority, string>;

    return (
      <Badge
        className={
          colors[priority as JobCardPriority] || 'bg-gray-100 text-gray-800'
        }
      >
        {priority}
      </Badge>
    );
  };

  const submitJobCard = async (cardId: string) => {
    try {
      const jobCard = jobCards.find((card) => card.id === cardId);

      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Submitting mock job card');

        // Update mock job card status
        setJobCards((prev) =>
          prev.map((card) =>
            card.id === cardId
              ? {
                  ...card,
                  jobCardStatus: 'Submitted',
                  approvalStatus: 'Pending',
                }
              : card
          )
        );
        setFilteredCards((prev) =>
          prev.map((card) =>
            card.id === cardId
              ? {
                  ...card,
                  jobCardStatus: 'Submitted',
                  approvalStatus: 'Pending',
                }
              : card
          )
        );

        console.log('✅ Mock job card submitted for approval');
        return;
      }

      await jobCardService.submitJobCard(cardId, { confirmReadiness: true });

      // Refresh job cards list
      await refreshJobCards();

      toast({
        title: 'Success',
        description: `Job card ${jobCard?.jobCardNumber || ''} submitted for approval successfully`,
      });
    } catch (error) {
      console.error('Error submitting job card:', error);
      toast({
        title: 'Error',
        description: 'Failed to submit job card. Please try again.',
        variant: 'destructive',
      });
    }
  };

  // Opens the approval dialog to select billing type
  const openApprovalDialog = (cardId: string) => {
    setApprovalCardId(cardId);
    setApprovalBillingType('Repairs'); // Default to Repairs
    setIsApprovalDialogOpen(true);
  };

  // Process the actual approval after billing type is selected
  const confirmApproveJobCard = async () => {
    if (!approvalCardId) return;

    try {
      const jobCard = jobCards.find((card) => card.id === approvalCardId);

      if (TESTING_MODE) {
        console.log(
          '🧪 TESTING MODE: Approving mock job card with billing type:',
          approvalBillingType
        );

        // Update mock job card status
        setJobCards((prev) =>
          prev.map((card) =>
            card.id === approvalCardId
              ? {
                  ...card,
                  jobCardStatus: 'Approved',
                  approvalStatus: 'Approved',
                }
              : card
          )
        );
        setFilteredCards((prev) =>
          prev.map((card) =>
            card.id === approvalCardId
              ? {
                  ...card,
                  jobCardStatus: 'Approved',
                  approvalStatus: 'Approved',
                }
              : card
          )
        );

        console.log(
          '✅ Mock job card approved - ready for work order generation'
        );
        setIsApprovalDialogOpen(false);
        setApprovalCardId(null);
        return;
      }

      const approvalAction: JobCardApprovalAction = {
        action: 'Approve',
        comments: 'Approved via job card management',
        billingType: jobCard?.workOrderBillingType || approvalBillingType,
      };
      await jobCardService.processApproval(approvalCardId, approvalAction);

      // Refresh job cards list
      await refreshJobCards();

      toast({
        title: 'Success',
        description: `Job card ${jobCard?.jobCardNumber || ''} approved successfully with ${jobCard?.workOrderBillingType || approvalBillingType} billing type`,
      });

      setIsApprovalDialogOpen(false);
      setApprovalCardId(null);
    } catch (error) {
      console.error('Error approving job card:', error);
      toast({
        title: 'Error',
        description: 'Failed to approve job card. Please try again.',
        variant: 'destructive',
      });
    }
  };

  const rejectJobCard = async (cardId: string) => {
    try {
      const jobCard = jobCards.find((card) => card.id === cardId);

      if (TESTING_MODE) {
        console.log('🧪 TESTING MODE: Rejecting mock job card');

        // Update mock job card status
        setJobCards((prev) =>
          prev.map((card) =>
            card.id === cardId
              ? {
                  ...card,
                  jobCardStatus: 'Rejected',
                  approvalStatus: 'Rejected',
                }
              : card
          )
        );
        setFilteredCards((prev) =>
          prev.map((card) =>
            card.id === cardId
              ? {
                  ...card,
                  jobCardStatus: 'Rejected',
                  approvalStatus: 'Rejected',
                }
              : card
          )
        );

        console.log('❌ Mock job card rejected');
        return;
      }

      const approvalAction: JobCardApprovalAction = {
        action: 'Reject',
        comments: 'Rejected via job card management',
      };
      await jobCardService.processApproval(cardId, approvalAction);

      // Refresh job cards list
      await refreshJobCards();

      toast({
        title: 'Job Card Rejected',
        description: `Job card ${jobCard?.jobCardNumber || ''} has been rejected`,
      });
    } catch (error) {
      console.error('Error rejecting job card:', error);
      toast({
        title: 'Error',
        description: 'Failed to reject job card. Please try again.',
        variant: 'destructive',
      });
    }
  };

  // Work orders are now auto-generated on approval in the backend.
  // No manual "Generate Work Order" action is exposed in the UI.

  const currentAdmissionJobCard = jobCards.find(
    (card) => card.id === admissionForm.jobCardId
  );
  const currentAdmissionAsset = assets.find(
    (asset) => asset.id === admissionForm.assetId
  );

  const admissionAssetDisplay = currentAdmissionAsset
    ? `${currentAdmissionAsset.name} (${currentAdmissionAsset.assetNumber})`
    : admissionForm.assetId || '';

  const admissionJobCardDisplay = currentAdmissionJobCard
    ? `${currentAdmissionJobCard.jobCardNumber} - ${currentAdmissionJobCard.title}`
    : admissionForm.jobCardId || '';

  const selectedCardWorkflowSummary = selectedCardDetails?.id
    ? workflowSummariesById[selectedCardDetails.id]
    : selectedCard?.id
      ? workflowSummariesById[selectedCard.id]
      : undefined;
  const selectedCardAwaitingApproval = selectedCardDetails
    ? isJobCardAwaitingApproval(
        selectedCardDetails,
        selectedCardWorkflowSummary
      )
    : false;

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Job Cards</h1>
          <p className="text-muted-foreground">
            Create and manage maintenance job cards
          </p>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Job Cards</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="flex items-center justify-between">
        <div></div>
        <ClientOnly
          fallback={
            <div className="h-10 w-32 bg-gray-100 rounded animate-pulse"></div>
          }
        >
          <Dialog
            open={isCreateDialogOpen}
            onOpenChange={setIsCreateDialogOpen}
          >
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                Create Job Card
              </Button>
            </DialogTrigger>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>Create New Job Card</DialogTitle>
                <DialogDescription>
                  Fill in the details to create a new maintenance job card.
                </DialogDescription>
              </DialogHeader>
              <div className="grid gap-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="title">Title</Label>
                    <Input
                      id="title"
                      value={newJobCard.title}
                      onChange={(e) =>
                        setNewJobCard((prev) => ({
                          ...prev,
                          title: e.target.value,
                        }))
                      }
                      placeholder="Job card title"
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="assetName">Asset</Label>
                    <Select
                      value={newJobCard.assetName}
                      onValueChange={(value) =>
                        setNewJobCard((prev) => ({ ...prev, assetName: value }))
                      }
                      disabled={loadingData}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={
                            loadingData
                              ? 'Loading assets...'
                              : assets.length === 0
                                ? 'No assets available'
                                : 'Select asset'
                          }
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {loadingData ? (
                          <SelectItem value="loading" disabled>
                            Loading assets...
                          </SelectItem>
                        ) : assets.length === 0 ? (
                          <SelectItem value="no-assets" disabled>
                            No assets found. Check console for errors.
                          </SelectItem>
                        ) : (
                          assets.map((asset) => (
                            <SelectItem key={asset.id} value={asset.name}>
                              {asset.name} ({asset.assetNumber})
                            </SelectItem>
                          ))
                        )}
                      </SelectContent>
                    </Select>
                    {!loadingData && assets.length === 0 && (
                      <p className="text-sm text-red-600">
                        No assets loaded. Check browser console for API errors.
                      </p>
                    )}
                    {!loadingData && assets.length > 0 && (
                      <p className="text-sm text-green-600">
                        {assets.length} assets loaded successfully
                      </p>
                    )}
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="customerBusinessPartner">Customer</Label>
                  <CustomerBusinessPartnerPicker
                    value={newJobCard.customerBusinessPartnerId}
                    onChange={(value) =>
                      setNewJobCard((prev) => ({
                        ...prev,
                        customerBusinessPartnerId: value,
                      }))
                    }
                    partners={customerBusinessPartners}
                    disabled={loadingData}
                    placeholder={
                      loadingData
                        ? 'Loading customers...'
                        : 'Search/select customer'
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="maintenanceType">Maintenance Type</Label>
                  <Select
                    value={newJobCard.maintenanceType}
                    onValueChange={(value) =>
                      setNewJobCard((prev) => ({
                        ...prev,
                        maintenanceType: value,
                      }))
                    }
                    disabled={loadingData}
                  >
                    <SelectTrigger>
                      <SelectValue
                        placeholder={
                          loadingData
                            ? 'Loading types...'
                            : 'Select maintenance type'
                        }
                      />
                    </SelectTrigger>
                    <SelectContent>
                      {maintenanceTypes.map((type) => (
                        <SelectItem key={type.id} value={type.name}>
                          {type.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="workOrderBillingType">
                    Work Order Billing Type
                  </Label>
                  <Select
                    value={newJobCard.workOrderBillingType}
                    onValueChange={(value: WorkOrderBillingType) =>
                      setNewJobCard((prev) => ({
                        ...prev,
                        workOrderBillingType: value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select billing type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Repairs">
                        Repairs - itemized billing
                      </SelectItem>
                      <SelectItem value="Maintenance">
                        Maintenance - fixed amount
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="description">Description</Label>
                  <Textarea
                    id="description"
                    value={newJobCard.description}
                    onChange={(e) =>
                      setNewJobCard((prev) => ({
                        ...prev,
                        description: e.target.value,
                      }))
                    }
                    placeholder="Detailed description of the maintenance needed"
                    rows={2}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="problemDescription">
                    Problem Description
                  </Label>
                  <Textarea
                    id="problemDescription"
                    value={newJobCard.problemDescription}
                    onChange={(e) =>
                      setNewJobCard((prev) => ({
                        ...prev,
                        problemDescription: e.target.value,
                      }))
                    }
                    placeholder="Specific problem or issue identified"
                    rows={2}
                  />
                </div>
                <div className="grid grid-cols-3 gap-4">
                  <div className="space-y-2">
                    <Label htmlFor="priority">Priority</Label>
                    <Select
                      value={newJobCard.priority}
                      onValueChange={(value: any) =>
                        setNewJobCard((prev) => ({ ...prev, priority: value }))
                      }
                      disabled={loadingData}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={
                            loadingData ? 'Loading...' : 'Select priority'
                          }
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {priorityLevels.map((priority) => (
                          <SelectItem key={priority.id} value={priority.name}>
                            {priority.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="estimatedHours">Estimated Hours</Label>
                    <Input
                      id="estimatedHours"
                      type="number"
                      min="0"
                      step="0.5"
                      value={newJobCard.estimatedHours}
                      onChange={(e) =>
                        setNewJobCard((prev) => ({
                          ...prev,
                          estimatedHours: parseFloat(e.target.value) || 0,
                        }))
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="estimatedCost">Estimated Cost</Label>
                    <Input
                      id="estimatedCost"
                      type="number"
                      min="0"
                      step="0.01"
                      value={newJobCard.estimatedCost}
                      onChange={(e) =>
                        setNewJobCard((prev) => ({
                          ...prev,
                          estimatedCost: parseFloat(e.target.value) || 0,
                        }))
                      }
                    />
                  </div>
                </div>
              </div>
              <DialogFooter>
                <Button
                  variant="outline"
                  onClick={() => setIsCreateDialogOpen(false)}
                >
                  Cancel
                </Button>
                <Button onClick={handleCreateJobCard}>Create Job Card</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
          {/* Admission dialog inline on Job Cards page */}
          <Dialog
            open={isAdmissionDialogOpen}
            onOpenChange={setIsAdmissionDialogOpen}
          >
            <DialogContent className="max-w-3xl">
              <DialogHeader>
                <DialogTitle>Admit Asset for Job Card</DialogTitle>
                <DialogDescription>
                  Capture the asset condition and details at the point of
                  admission for this job card.
                </DialogDescription>
              </DialogHeader>

              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label>Asset</Label>
                    <Input
                      value={admissionAssetDisplay}
                      disabled
                      className="mt-1"
                    />
                  </div>
                  <div>
                    <Label>Job Card</Label>
                    <Input
                      value={admissionJobCardDisplay}
                      disabled
                      className="mt-1"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-4">
                  <div>
                    <Label>Admission Type</Label>
                    <Select
                      value={admissionForm.admissionType}
                      onValueChange={(value) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          admissionType:
                            value as typeof admissionForm.admissionType,
                        }))
                      }
                    >
                      <SelectTrigger className="mt-1">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Scheduled">Scheduled</SelectItem>
                        <SelectItem value="Emergency">Emergency</SelectItem>
                        <SelectItem value="Breakdown">Breakdown</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label>Condition on Admission</Label>
                    <Select
                      value={admissionForm.assetConditionOnAdmission}
                      onValueChange={(value) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          assetConditionOnAdmission:
                            value as typeof admissionForm.assetConditionOnAdmission,
                        }))
                      }
                    >
                      <SelectTrigger className="mt-1">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Excellent">Excellent</SelectItem>
                        <SelectItem value="Good">Good</SelectItem>
                        <SelectItem value="Fair">Fair</SelectItem>
                        <SelectItem value="Poor">Poor</SelectItem>
                        <SelectItem value="Critical">Critical</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label>Fuel Level (%)</Label>
                    <Input
                      type="number"
                      min={0}
                      max={100}
                      value={admissionForm.fuelLevel}
                      onChange={(e) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          fuelLevel: Number(e.target.value) || 0,
                        }))
                      }
                      className="mt-1"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-4">
                  <div>
                    <Label>Mileage</Label>
                    <Input
                      type="number"
                      value={admissionForm.mileageReading}
                      onChange={(e) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          mileageReading: Number(e.target.value) || 0,
                        }))
                      }
                      className="mt-1"
                    />
                  </div>
                  <div>
                    <Label>Hours</Label>
                    <Input
                      type="number"
                      value={admissionForm.hoursReading}
                      onChange={(e) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          hoursReading: Number(e.target.value) || 0,
                        }))
                      }
                      className="mt-1"
                    />
                  </div>
                  <div>
                    <Label>Bay / Station</Label>
                    <Input
                      value={admissionForm.bayOrStation}
                      onChange={(e) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          bayOrStation: e.target.value,
                        }))
                      }
                      className="mt-1"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label>Admission Notes</Label>
                    <Textarea
                      rows={3}
                      value={admissionForm.admissionNotes}
                      onChange={(e) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          admissionNotes: e.target.value,
                        }))
                      }
                      className="mt-1"
                    />
                  </div>
                  <div>
                    <Label>Observed Problems</Label>
                    <Textarea
                      rows={3}
                      value={admissionForm.observedProblems}
                      onChange={(e) =>
                        setAdmissionForm((prev) => ({
                          ...prev,
                          observedProblems: e.target.value,
                        }))
                      }
                      className="mt-1"
                    />
                  </div>
                </div>
              </div>

              <DialogFooter className="mt-4">
                <Button
                  variant="outline"
                  onClick={() => setIsAdmissionDialogOpen(false)}
                >
                  Cancel
                </Button>
                <Button
                  variant="outline"
                  onClick={() => handleCreateAdmissionFromJobCard(false)}
                  disabled={isSubmittingAdmission}
                >
                  {isSubmittingAdmission ? 'Admitting...' : 'Admit Only'}
                </Button>
                <Button
                  onClick={() => handleCreateAdmissionFromJobCard(true)}
                  disabled={isSubmittingAdmission}
                  className="flex items-center gap-2"
                >
                  <ClipboardCheck className="h-4 w-4" />
                  {isSubmittingAdmission
                    ? 'Admitting...'
                    : 'Admit & Start Checklist'}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </ClientOnly>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="p-4">
          <div className="flex items-center space-x-4 flex-wrap gap-4">
            <div className="flex-1 min-w-[200px] max-w-sm">
              <Label htmlFor="search" className="sr-only">
                Search
              </Label>
              <div className="relative">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  id="search"
                  placeholder="Search job cards..."
                  className="pl-8"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>
            </div>
            <ClientOnly
              fallback={
                <div className="w-[140px] h-10 bg-gray-100 rounded animate-pulse"></div>
              }
            >
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Status</SelectItem>
                  <SelectItem value="Draft">Draft</SelectItem>
                  <SelectItem value="Submitted">Submitted</SelectItem>
                  <SelectItem value="UnderReview">Under Review</SelectItem>
                  <SelectItem value="Approved">Approved</SelectItem>
                  <SelectItem value="Rejected">Rejected</SelectItem>
                </SelectContent>
              </Select>
            </ClientOnly>
            <ClientOnly
              fallback={
                <div className="w-[140px] h-10 bg-gray-100 rounded animate-pulse"></div>
              }
            >
              <Select value={priorityFilter} onValueChange={setPriorityFilter}>
                <SelectTrigger className="w-[140px]">
                  <SelectValue placeholder="All Priority" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Priority</SelectItem>
                  {priorityLevels.map((priority) => (
                    <SelectItem key={priority.id} value={priority.name}>
                      {priority.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </ClientOnly>
            <ClientOnly
              fallback={
                <div className="w-[160px] h-10 bg-gray-100 rounded animate-pulse"></div>
              }
            >
              <Select
                value={hasWorkOrderFilter}
                onValueChange={(value) =>
                  setHasWorkOrderFilter(value as 'all' | 'with' | 'without')
                }
              >
                <SelectTrigger className="w-[160px]">
                  <SelectValue placeholder="Work order link" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All job cards</SelectItem>
                  <SelectItem value="with">With work order</SelectItem>
                  <SelectItem value="without">Without work order</SelectItem>
                </SelectContent>
              </Select>
            </ClientOnly>

            <Popover>
              <PopoverTrigger asChild>
                <Button
                  variant="outline"
                  className="justify-start text-left font-normal w-[250px]"
                >
                  <CalendarIcon className="mr-2 h-4 w-4" />
                  {dateRange?.from ? (
                    dateRange.to ? (
                      <>
                        {format(dateRange.from, 'LLL dd, y')} -{' '}
                        {format(dateRange.to, 'LLL dd, y')}
                      </>
                    ) : (
                      format(dateRange.from, 'LLL dd, y')
                    )
                  ) : (
                    <span>Pick a date range</span>
                  )}
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-auto p-0" align="start">
                <CalendarComponent
                  initialFocus
                  mode="range"
                  defaultMonth={dateRange?.from}
                  selected={dateRange}
                  onSelect={setDateRange}
                  numberOfMonths={2}
                />
              </PopoverContent>
            </Popover>
          </div>
        </CardContent>
      </Card>

      {/* Job Cards Table */}
      <Card>
        <CardHeader>
          <CardTitle>Job Cards ({filteredCards.length})</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <SortHeader
                  label="Job Card #"
                  column="jobCardNumber"
                  className="w-[110px]"
                />
                <SortHeader label="Title" column="title" />
                <SortHeader label="Asset" column="assetName" />
                <TableHead>Customer</TableHead>
                <SortHeader
                  label="Status"
                  column="jobCardStatus"
                  className="w-[220px]"
                />
                <SortHeader label="Priority" column="priority" />
                <SortHeader label="Requested By" column="requestedBy" />
                <SortHeader label="Created" column="createdAt" />
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {getSortedCards().map((card) => {
                const workflowSummary = workflowSummariesById[card.id];
                const awaitingApproval = isJobCardAwaitingApproval(
                  card,
                  workflowSummary
                );
                const stepName = workflowSummary?.currentStepName;
                const pendingApprovers = formatPendingApprovers(
                  workflowSummary?.pendingApprovers || []
                );
                const statusTitle = awaitingApproval
                  ? [
                      stepName ? `Step: ${stepName}` : 'Awaiting Approval',
                      pendingApprovers.full
                        ? `Pending: ${pendingApprovers.full}`
                        : undefined,
                    ]
                      .filter(Boolean)
                      .join(' | ')
                  : undefined;
                const admissionAction = getAdmissionActionMeta(card);

                return (
                  <TableRow key={card.id}>
                    <TableCell className="w-[110px] max-w-[110px] truncate font-medium">
                      {card.jobCardNumber}
                    </TableCell>
                    <TableCell>{card.title}</TableCell>
                    <TableCell>{card.assetName}</TableCell>
                    <TableCell>
                      {card.customerBusinessPartnerName || '-'}
                    </TableCell>
                    <TableCell className="min-w-[210px] max-w-[240px]">
                      <div className="flex flex-wrap items-center gap-1">
                        {getStatusBadge(
                          card.jobCardStatus,
                          card.approvalStatus,
                          stepName,
                          awaitingApproval,
                          statusTitle
                        )}
                        {awaitingApproval &&
                          getPendingApproverBadge(
                            pendingApprovers.short,
                            pendingApprovers.full
                          )}
                      </div>
                    </TableCell>
                    <TableCell>{getPriorityBadge(card.priority)}</TableCell>
                    <TableCell>{card.requestedBy}</TableCell>
                    <TableCell>
                      {format(new Date(card.createdAt), 'MMM dd, yyyy HH:mm')}
                    </TableCell>
                    <TableCell className="text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            variant="ghost"
                            size="icon"
                            title="Actions"
                            aria-label={`Actions for ${card.jobCardNumber}`}
                          >
                            <MoreHorizontal className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent
                          align="end"
                          className="w-56"
                          forceMount
                        >
                          <DropdownMenuItem
                            onSelect={() => void handleViewJobCard(card)}
                          >
                            <Eye className="mr-2 h-4 w-4" />
                            View Details
                          </DropdownMenuItem>

                          {card.jobCardStatus === 'Draft' && (
                            <>
                              <DropdownMenuItem
                                onSelect={() => handleEditJobCard(card)}
                              >
                                <Edit className="mr-2 h-4 w-4" />
                                Edit Job Card
                              </DropdownMenuItem>
                              <WorkflowApprovalActions
                                entityType="JobCard"
                                entityId={card.id}
                                entityLabel="Job Card"
                                entityNumber={card.jobCardNumber}
                                status={card.jobCardStatus}
                                renderMode="menu-items"
                                canSubmit
                                canApproveReject={false}
                                onSubmit={async () => {
                                  if (TESTING_MODE) {
                                    setJobCards((prev) =>
                                      prev.map((c) =>
                                        c.id === card.id
                                          ? {
                                              ...c,
                                              jobCardStatus: 'Submitted',
                                              approvalStatus: 'Pending',
                                            }
                                          : c
                                      )
                                    );
                                    setFilteredCards((prev) =>
                                      prev.map((c) =>
                                        c.id === card.id
                                          ? {
                                              ...c,
                                              jobCardStatus: 'Submitted',
                                              approvalStatus: 'Pending',
                                            }
                                          : c
                                      )
                                    );
                                    return;
                                  }

                                  try {
                                    await jobCardService.submitJobCard(
                                      card.id,
                                      { confirmReadiness: true }
                                    );
                                  } catch (err: any) {
                                    const msg =
                                      err?.response?.data?.error ||
                                      err?.response?.data ||
                                      err?.message ||
                                      'Failed to submit job card';
                                    throw new Error(
                                      typeof msg === 'string'
                                        ? msg
                                        : 'Failed to submit job card'
                                    );
                                  }
                                }}
                                onAfterAction={refreshJobCards}
                                onOpenWorkflows={() =>
                                  router.push('/administration/workflow')
                                }
                              />
                            </>
                          )}

                          {awaitingApproval && (
                            <WorkflowApprovalActions
                              entityType="JobCard"
                              entityId={card.id}
                              entityLabel="Job Card"
                              entityNumber={card.jobCardNumber}
                              status={card.jobCardStatus}
                              currentStepName={stepName}
                              workflowSummary={workflowSummary}
                              renderMode="menu-items"
                              canSubmit={false}
                              canApproveReject
                              onApprove={async (comments) => {
                                if (TESTING_MODE) {
                                  setJobCards((prev) =>
                                    prev.map((c) =>
                                      c.id === card.id
                                        ? {
                                            ...c,
                                            jobCardStatus: 'Approved',
                                            approvalStatus: 'Approved',
                                          }
                                        : c
                                    )
                                  );
                                  setFilteredCards((prev) =>
                                    prev.map((c) =>
                                      c.id === card.id
                                        ? {
                                            ...c,
                                            jobCardStatus: 'Approved',
                                            approvalStatus: 'Approved',
                                          }
                                        : c
                                    )
                                  );
                                  return;
                                }

                                try {
                                  const approvalAction: JobCardApprovalAction =
                                    {
                                      action: 'Approve',
                                      comments: comments || undefined,
                                      billingType:
                                        card.workOrderBillingType || 'Repairs',
                                    };
                                  await jobCardService.processApproval(
                                    card.id,
                                    approvalAction
                                  );
                                } catch (err: any) {
                                  const msg =
                                    err?.response?.data?.error ||
                                    err?.response?.data ||
                                    err?.message ||
                                    'Failed to approve job card';
                                  throw new Error(
                                    typeof msg === 'string'
                                      ? msg
                                      : 'Failed to approve job card'
                                  );
                                }
                              }}
                              onReject={async (comments) => {
                                if (TESTING_MODE) {
                                  setJobCards((prev) =>
                                    prev.map((c) =>
                                      c.id === card.id
                                        ? {
                                            ...c,
                                            jobCardStatus: 'Rejected',
                                            approvalStatus: 'Rejected',
                                          }
                                        : c
                                    )
                                  );
                                  setFilteredCards((prev) =>
                                    prev.map((c) =>
                                      c.id === card.id
                                        ? {
                                            ...c,
                                            jobCardStatus: 'Rejected',
                                            approvalStatus: 'Rejected',
                                          }
                                        : c
                                    )
                                  );
                                  return;
                                }

                                try {
                                  const approvalAction: JobCardApprovalAction =
                                    {
                                      action: 'Reject',
                                      comments,
                                    };
                                  await jobCardService.processApproval(
                                    card.id,
                                    approvalAction
                                  );
                                } catch (err: any) {
                                  const msg =
                                    err?.response?.data?.error ||
                                    err?.response?.data ||
                                    err?.message ||
                                    'Failed to reject job card';
                                  throw new Error(
                                    typeof msg === 'string'
                                      ? msg
                                      : 'Failed to reject job card'
                                  );
                                }
                              }}
                              onAfterAction={refreshJobCards}
                              onOpenWorkflows={() =>
                                router.push('/administration/workflow')
                              }
                            />
                          )}

                          <DropdownMenuSeparator />

                          <DropdownMenuItem
                            className={admissionAction.className}
                            onSelect={() =>
                              void handleAdmissionGridAction(card)
                            }
                          >
                            <ClipboardCheck
                              className={`mr-2 h-4 w-4 ${admissionAction.iconClassName}`}
                            />
                            {admissionAction.label}
                          </DropdownMenuItem>

                          {card.generatedWorkOrderId && (
                            <DropdownMenuItem disabled>
                              <CheckCircle className="mr-2 h-4 w-4 text-muted-foreground" />
                              Work Order Generated
                            </DropdownMenuItem>
                          )}
                          {card.jobCardStatus === 'Approved' &&
                            !card.generatedWorkOrderId && (
                              <DropdownMenuItem disabled>
                                <Clock className="mr-2 h-4 w-4 text-muted-foreground" />
                                Work Order Pending
                              </DropdownMenuItem>
                            )}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Edit Job Card Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              Edit Job Card{' '}
              {selectedCard?.jobCardNumber
                ? `- ${selectedCard.jobCardNumber}`
                : ''}
            </DialogTitle>
            <DialogDescription>Update job card details</DialogDescription>
          </DialogHeader>
          {selectedCard && (
            <div className="grid gap-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-title">Title</Label>
                  <Input
                    id="edit-title"
                    value={editJobCard.title}
                    onChange={(e) =>
                      setEditJobCard((prev) => ({
                        ...prev,
                        title: e.target.value,
                      }))
                    }
                    placeholder="Job card title"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-asset">Asset</Label>
                  <Select
                    value={editJobCard.assetId}
                    onValueChange={(value) => {
                      const asset = assets.find((a) => a.id === value);
                      setEditJobCard((prev) => ({
                        ...prev,
                        assetId: value,
                        assetName: asset?.name || '',
                      }));
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select asset" />
                    </SelectTrigger>
                    <SelectContent>
                      {assets.map((asset) => (
                        <SelectItem key={asset.id} value={asset.id}>
                          {asset.name} ({asset.assetNumber})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-customerBusinessPartner">Customer</Label>
                  <CustomerBusinessPartnerPicker
                    value={editJobCard.customerBusinessPartnerId}
                    onChange={(value) =>
                      setEditJobCard((prev) => ({
                        ...prev,
                        customerBusinessPartnerId: value,
                      }))
                    }
                    partners={customerBusinessPartners}
                    placeholder="Search/select customer"
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-maintenanceType">Maintenance Type</Label>
                <Select
                  value={editJobCard.maintenanceTypeId}
                  onValueChange={(value) => {
                    const type = maintenanceTypes.find((t) => t.id === value);
                    setEditJobCard((prev) => ({
                      ...prev,
                      maintenanceTypeId: value,
                      maintenanceType: type?.name || '',
                    }));
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select maintenance type" />
                  </SelectTrigger>
                  <SelectContent>
                    {maintenanceTypes.map((type) => (
                      <SelectItem key={type.id} value={type.id}>
                        {type.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-workOrderBillingType">
                  Work Order Billing Type
                </Label>
                <Select
                  value={editJobCard.workOrderBillingType}
                  onValueChange={(value: WorkOrderBillingType) =>
                    setEditJobCard((prev) => ({
                      ...prev,
                      workOrderBillingType: value,
                    }))
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select billing type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Repairs">
                      Repairs - itemized billing
                    </SelectItem>
                    <SelectItem value="Maintenance">
                      Maintenance - fixed amount
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-description">Description</Label>
                <Textarea
                  id="edit-description"
                  value={editJobCard.description}
                  onChange={(e) =>
                    setEditJobCard((prev) => ({
                      ...prev,
                      description: e.target.value,
                    }))
                  }
                  placeholder="Detailed description of the maintenance needed"
                  rows={2}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-problemDescription">
                  Problem Description
                </Label>
                <Textarea
                  id="edit-problemDescription"
                  value={editJobCard.problemDescription}
                  onChange={(e) =>
                    setEditJobCard((prev) => ({
                      ...prev,
                      problemDescription: e.target.value,
                    }))
                  }
                  placeholder="Specific problem or issue identified"
                  rows={2}
                />
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="edit-priority">Priority</Label>
                  <Select
                    value={editJobCard.priorityLevelId}
                    onValueChange={(value) => {
                      const priority = priorityLevels.find(
                        (p) => p.id === value
                      );
                      setEditJobCard((prev) => ({
                        ...prev,
                        priorityLevelId: value,
                        priority: priority?.name as any,
                      }));
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select priority" />
                    </SelectTrigger>
                    <SelectContent>
                      {priorityLevels.map((priority) => (
                        <SelectItem key={priority.id} value={priority.id}>
                          {priority.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-hours">Estimated Hours</Label>
                  <Input
                    id="edit-hours"
                    type="number"
                    min="0"
                    step="0.5"
                    value={editJobCard.estimatedHours}
                    onChange={(e) =>
                      setEditJobCard((prev) => ({
                        ...prev,
                        estimatedHours: parseFloat(e.target.value) || 0,
                      }))
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="edit-cost">Estimated Cost</Label>
                  <Input
                    id="edit-cost"
                    type="number"
                    min="0"
                    step="0.01"
                    value={editJobCard.estimatedCost}
                    onChange={(e) =>
                      setEditJobCard((prev) => ({
                        ...prev,
                        estimatedCost: parseFloat(e.target.value) || 0,
                      }))
                    }
                  />
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsEditDialogOpen(false)}
            >
              Cancel
            </Button>
            <Button
              onClick={async () => {
                if (!selectedCard?.id) return;

                try {
                  console.log('Updating job card:', selectedCard.id);
                  console.log('Job card status:', selectedCard.jobCardStatus);
                  console.log('Update data:', editJobCard);

                  const updateData = {
                    title: editJobCard.title,
                    description: editJobCard.description,
                    problemDescription: editJobCard.problemDescription,
                    maintenanceTypeId: editJobCard.maintenanceTypeId,
                    priorityLevelId: editJobCard.priorityLevelId,
                    customerBusinessPartnerId:
                      editJobCard.customerBusinessPartnerId !== 'none'
                        ? editJobCard.customerBusinessPartnerId
                        : undefined,
                    estimatedHours: editJobCard.estimatedHours,
                    estimatedCost: editJobCard.estimatedCost,
                    maintenanceLocation: 'Internal',
                    workOrderBillingType: editJobCard.workOrderBillingType,
                    customFieldValues: {
                      workOrderBillingType: editJobCard.workOrderBillingType,
                    },
                    requiresSpecialTools: false,
                    requiresShutdown: false,
                    requiresSafetyPermit: false,
                  };

                  console.log('Sending update request...', updateData);
                  const response = await jobCardService.updateJobCard(
                    selectedCard.id,
                    updateData
                  );
                  console.log('Update response:', response);

                  // Refresh the job cards list
                  await refreshJobCards();

                  setIsEditDialogOpen(false);
                  toast({
                    title: 'Success',
                    description: `Job card ${selectedCard.jobCardNumber} updated successfully`,
                  });
                } catch (error: any) {
                  console.error('Error updating job card:', error);
                  console.error('Error response:', error.response);
                  const errorMsg =
                    error.response?.data?.message ||
                    error.response?.data ||
                    error.message;
                  toast({
                    title: 'Error',
                    description: `Failed to update job card: ${errorMsg}`,
                    variant: 'destructive',
                  });
                }
              }}
            >
              Save Changes
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* View Job Card Dialog */}
      <Dialog
        open={isViewDialogOpen}
        onOpenChange={(open) => {
          setIsViewDialogOpen(open);
          if (!open) {
            setSelectedCardDetails(null);
            setSelectedWorkOrder(null);
            setSelectedQCInspection(null);
            setWorkOrderStaffSchedules([]);
            setWorkOrderExpenses([]);
            setWorkOrderTools([]);
          }
        }}
      >
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Job Card Details</DialogTitle>
            <DialogDescription>
              {selectedCard?.jobCardNumber || 'Loading...'}
            </DialogDescription>
          </DialogHeader>
          {loadingDetails ? (
            <div className="flex items-center justify-center py-8">
              <Clock className="h-6 w-6 animate-spin" />
              <span className="ml-2">Loading details...</span>
            </div>
          ) : (
            selectedCardDetails && (
              <Tabs defaultValue="overview" className="w-full">
                <TabsList className="grid w-full grid-cols-5">
                  <TabsTrigger value="overview">Overview</TabsTrigger>
                  <TabsTrigger
                    value="admission"
                    disabled={!activeAdmissionForJobCard}
                  >
                    Admission
                  </TabsTrigger>
                  <TabsTrigger value="workorder" disabled={!selectedWorkOrder}>
                    Work Order
                  </TabsTrigger>
                  <TabsTrigger value="qc" disabled={!selectedQCInspection}>
                    QC Inspection
                  </TabsTrigger>
                  <WorkflowTabTrigger value="workflow" />
                </TabsList>

                {/* Overview Tab */}
                <TabsContent value="overview" className="space-y-6 mt-4">
                  {/* Key Information Card */}
                  <Card>
                    <CardHeader>
                      <CardTitle>{selectedCardDetails.title}</CardTitle>
                      <CardDescription>
                        Job Card #{selectedCardDetails.jobCardNumber}
                      </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="grid grid-cols-3 gap-4">
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Status
                          </Label>
                          <div className="pt-1">
                            {getStatusBadge(
                              selectedCardDetails.jobCardStatus,
                              selectedCardDetails.approvalStatus,
                              selectedCardWorkflowSummary?.currentStepName,
                              selectedCardAwaitingApproval
                            )}
                          </div>
                        </div>
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Approval Status
                          </Label>
                          <div className="pt-1">
                            <Badge
                              style={
                                selectedCardDetails.approvalStatus ===
                                'Approved'
                                  ? {
                                      backgroundColor: '#d1fae5',
                                      color: '#065f46',
                                    }
                                  : selectedCardDetails.approvalStatus ===
                                      'Rejected'
                                    ? {
                                        backgroundColor: '#fee2e2',
                                        color: '#991b1b',
                                      }
                                    : {
                                        backgroundColor: '#fef3c7',
                                        color: '#92400e',
                                      }
                              }
                            >
                              {selectedCardDetails.approvalStatus}
                            </Badge>
                          </div>
                        </div>
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Priority
                          </Label>
                          <div className="pt-1">
                            {getPriorityBadge(selectedCardDetails.priority)}
                          </div>
                        </div>
                      </div>

                      <div className="grid grid-cols-2 gap-4 border-t pt-4">
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Requested By
                          </Label>
                          <p className="text-sm font-medium">
                            {selectedCardDetails.requestedBy}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {format(
                              new Date(selectedCardDetails.requestedDate),
                              'MMM dd, yyyy HH:mm:ss'
                            )}
                          </p>
                        </div>
                        {selectedCardDetails.approvedBy && (
                          <div>
                            <Label className="text-sm font-medium text-muted-foreground">
                              Approved By
                            </Label>
                            <p className="text-sm font-medium">
                              {selectedCardDetails.approvedBy}
                            </p>
                            {selectedCardDetails.approvedAt && (
                              <p className="text-xs text-muted-foreground">
                                {format(
                                  new Date(selectedCardDetails.approvedAt),
                                  'MMM dd, yyyy HH:mm:ss'
                                )}
                              </p>
                            )}
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>

                  {/* Asset Information */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">
                        Asset Information
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="grid grid-cols-2 gap-4">
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Asset Name
                          </Label>
                          <p className="text-sm font-medium">
                            {selectedCardDetails.assetName}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {selectedCardDetails.assetCode}
                          </p>
                        </div>
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Asset Category/Type
                          </Label>
                          <p className="text-sm">
                            {selectedCardDetails.assetType ||
                              selectedCardDetails.maintenanceCategory ||
                              'N/A'}
                          </p>
                        </div>
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Location
                          </Label>
                          <p className="text-sm">
                            {selectedCardDetails.assetLocation ||
                              selectedCardDetails.maintenanceLocation ||
                              'N/A'}
                          </p>
                        </div>
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Maintenance Type
                          </Label>
                          <p className="text-sm">
                            {selectedCardDetails.maintenanceType}
                          </p>
                        </div>
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Customer
                          </Label>
                          <p className="text-sm">
                            {selectedCardDetails.customerBusinessPartnerName ||
                              'Not linked'}
                          </p>
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Work Description Card */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">
                        Work Description
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      {selectedCardDetails.description && (
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Description
                          </Label>
                          <p className="text-sm whitespace-pre-wrap">
                            {selectedCardDetails.description}
                          </p>
                        </div>
                      )}
                      {selectedCardDetails.problemDescription && (
                        <div>
                          <Label className="text-sm font-medium text-muted-foreground">
                            Problem Description
                          </Label>
                          <p className="text-sm whitespace-pre-wrap bg-red-50 p-3 rounded">
                            {selectedCardDetails.problemDescription}
                          </p>
                        </div>
                      )}
                    </CardContent>
                  </Card>

                  {/* Comments */}
                  {selectedCardDetails.comments &&
                    selectedCardDetails.comments.length > 0 && (
                      <div className="border-t pt-4">
                        <h3 className="text-lg font-semibold mb-3">Comments</h3>
                        <div className="space-y-2">
                          {selectedCardDetails.comments.map((comment) => (
                            <div
                              key={comment.id}
                              className="border rounded-lg p-3"
                            >
                              <div className="flex items-center justify-between mb-1">
                                <span className="text-sm font-medium">
                                  {comment.commentBy}
                                </span>
                                <div className="flex items-center space-x-2">
                                  {comment.isInternal && (
                                    <Badge
                                      variant="outline"
                                      className="text-xs"
                                    >
                                      Internal
                                    </Badge>
                                  )}
                                  <span className="text-xs text-muted-foreground">
                                    {format(
                                      new Date(comment.commentDate),
                                      'MMM dd, yyyy HH:mm:ss'
                                    )}
                                  </span>
                                </div>
                              </div>
                              <p className="text-sm">{comment.comment}</p>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}

                  {/* Documents */}
                  {selectedCardDetails.documents &&
                    selectedCardDetails.documents.length > 0 && (
                      <div className="border-t pt-4">
                        <h3 className="text-lg font-semibold mb-3">
                          Documents
                        </h3>
                        <div className="space-y-2">
                          {selectedCardDetails.documents.map((doc) => (
                            <div
                              key={doc.id}
                              className="flex items-center justify-between border rounded-lg p-3"
                            >
                              <div>
                                <p className="text-sm font-medium">
                                  {doc.fileName}
                                </p>
                                <p className="text-xs text-muted-foreground">
                                  {doc.documentType} •{' '}
                                  {(doc.fileSize / 1024).toFixed(2)} KB •
                                  Uploaded by {doc.uploadedBy}
                                </p>
                              </div>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => handleDownloadDocument(doc)}
                              >
                                Download
                              </Button>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                </TabsContent>

                {/* ADMISSION TAB */}
                <TabsContent value="admission" className="space-y-4 mt-4">
                  {activeAdmissionForJobCard ? (
                    <Card>
                      <CardHeader>
                        <div className="flex items-start justify-between">
                          <div>
                            <CardTitle className="text-lg">
                              Asset Admission Details
                            </CardTitle>
                            <CardDescription>
                              Admission #
                              {activeAdmissionForJobCard.admissionNumber}
                            </CardDescription>
                          </div>
                          <Badge
                            style={
                              activeAdmissionForJobCard.status === 'Active'
                                ? {
                                    backgroundColor: '#dbeafe',
                                    color: '#1e40af',
                                  }
                                : activeAdmissionForJobCard.status ===
                                    'Completed'
                                  ? {
                                      backgroundColor: '#d1fae5',
                                      color: '#065f46',
                                    }
                                  : {
                                      backgroundColor: '#fef3c7',
                                      color: '#92400e',
                                    }
                            }
                          >
                            {activeAdmissionForJobCard.status}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-6">
                        {/* Basic Information */}
                        <div className="grid grid-cols-3 gap-4">
                          <div>
                            <Label className="text-sm font-medium text-muted-foreground">
                              Asset
                            </Label>
                            <p className="text-sm font-medium mt-1">
                              {activeAdmissionForJobCard.assetName}
                            </p>
                            <p className="text-xs text-muted-foreground">
                              {activeAdmissionForJobCard.assetNumber}
                            </p>
                          </div>
                          <div>
                            <Label className="text-sm font-medium text-muted-foreground">
                              Admission Date
                            </Label>
                            <p className="text-sm font-medium mt-1">
                              {format(
                                new Date(
                                  activeAdmissionForJobCard.admissionDate
                                ),
                                'MMM dd, yyyy HH:mm'
                              )}
                            </p>
                          </div>
                          <div>
                            <Label className="text-sm font-medium text-muted-foreground">
                              Admitted By
                            </Label>
                            <p className="text-sm font-medium mt-1">
                              {activeAdmissionForJobCard.admittedBy || 'N/A'}
                            </p>
                          </div>
                        </div>

                        {/* Admission Type and Condition */}
                        <div className="grid grid-cols-3 gap-4 border-t pt-4">
                          <div>
                            <Label className="text-sm font-medium text-muted-foreground">
                              Admission Type
                            </Label>
                            <div className="mt-1">
                              <Badge
                                style={
                                  activeAdmissionForJobCard.admissionType ===
                                  'Emergency'
                                    ? {
                                        backgroundColor: '#fee2e2',
                                        color: '#991b1b',
                                      }
                                    : activeAdmissionForJobCard.admissionType ===
                                        'Breakdown'
                                      ? {
                                          backgroundColor: '#fed7aa',
                                          color: '#9a3412',
                                        }
                                      : {
                                          backgroundColor: '#dbeafe',
                                          color: '#1e40af',
                                        }
                                }
                              >
                                {activeAdmissionForJobCard.admissionType}
                              </Badge>
                            </div>
                          </div>
                          <div>
                            <Label className="text-sm font-medium text-muted-foreground">
                              Asset Condition
                            </Label>
                            <div className="mt-1">
                              <Badge
                                style={
                                  activeAdmissionForJobCard.assetConditionOnAdmission ===
                                  'Excellent'
                                    ? {
                                        backgroundColor: '#d1fae5',
                                        color: '#065f46',
                                      }
                                    : activeAdmissionForJobCard.assetConditionOnAdmission ===
                                        'Good'
                                      ? {
                                          backgroundColor: '#dbeafe',
                                          color: '#1e40af',
                                        }
                                      : activeAdmissionForJobCard.assetConditionOnAdmission ===
                                          'Fair'
                                        ? {
                                            backgroundColor: '#fef3c7',
                                            color: '#92400e',
                                          }
                                        : activeAdmissionForJobCard.assetConditionOnAdmission ===
                                            'Poor'
                                          ? {
                                              backgroundColor: '#fed7aa',
                                              color: '#9a3412',
                                            }
                                          : {
                                              backgroundColor: '#fee2e2',
                                              color: '#991b1b',
                                            }
                                }
                              >
                                {
                                  activeAdmissionForJobCard.assetConditionOnAdmission
                                }
                              </Badge>
                            </div>
                          </div>
                          <div>
                            <Label className="text-sm font-medium text-muted-foreground">
                              Location
                            </Label>
                            <p className="text-sm font-medium mt-1">
                              {activeAdmissionForJobCard.bayOrStation ||
                                activeAdmissionForJobCard.admissionLocation ||
                                'Not specified'}
                            </p>
                          </div>
                        </div>

                        {/* Admission Notes */}
                        {activeAdmissionForJobCard.admissionNotes && (
                          <div className="border-t pt-4">
                            <Label className="text-sm font-medium text-muted-foreground">
                              Admission Notes
                            </Label>
                            <p className="text-sm mt-1 bg-gray-50 p-3 rounded">
                              {activeAdmissionForJobCard.admissionNotes}
                            </p>
                          </div>
                        )}

                        {/* Observed Problems */}
                        {activeAdmissionForJobCard.observedProblems && (
                          <div className="border-t pt-4">
                            <Label className="text-sm font-medium text-muted-foreground">
                              Observed Problems
                            </Label>
                            <p className="text-sm mt-1 bg-yellow-50 p-3 rounded border border-yellow-200">
                              {activeAdmissionForJobCard.observedProblems}
                            </p>
                          </div>
                        )}

                        {/* Estimated Dates */}
                        <div className="grid grid-cols-2 gap-4 border-t pt-4">
                          {activeAdmissionForJobCard.estimatedCompletionDate && (
                            <div>
                              <Label className="text-sm font-medium text-muted-foreground">
                                Estimated Completion
                              </Label>
                              <p className="text-sm font-medium mt-1">
                                {format(
                                  new Date(
                                    activeAdmissionForJobCard.estimatedCompletionDate
                                  ),
                                  'MMM dd, yyyy'
                                )}
                              </p>
                            </div>
                          )}
                          {activeAdmissionForJobCard.estimatedDischargeDate && (
                            <div>
                              <Label className="text-sm font-medium text-muted-foreground">
                                Estimated Discharge
                              </Label>
                              <p className="text-sm font-medium mt-1">
                                {format(
                                  new Date(
                                    activeAdmissionForJobCard.estimatedDischargeDate
                                  ),
                                  'MMM dd, yyyy'
                                )}
                              </p>
                            </div>
                          )}
                        </div>

                        {/* Discharge Information if available */}
                        {latestDischargeForAdmission && (
                          <div className="border-t pt-4">
                            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                              <div className="flex items-center gap-2 mb-3">
                                <CheckCircle className="h-5 w-5 text-green-600" />
                                <Label className="text-sm font-semibold text-green-900">
                                  Asset Discharged
                                </Label>
                              </div>
                              <div className="grid grid-cols-2 gap-4">
                                <div>
                                  <Label className="text-xs text-green-700">
                                    Discharge Date
                                  </Label>
                                  <p className="text-sm font-medium text-green-900">
                                    {format(
                                      new Date(
                                        latestDischargeForAdmission.dischargeDate
                                      ),
                                      'MMM dd, yyyy HH:mm'
                                    )}
                                  </p>
                                </div>
                                <div>
                                  <Label className="text-xs text-green-700">
                                    Discharged By
                                  </Label>
                                  <p className="text-sm font-medium text-green-900">
                                    {latestDischargeForAdmission.dischargedBy ||
                                      'N/A'}
                                  </p>
                                </div>
                                <div>
                                  <Label className="text-xs text-green-700">
                                    Condition on Discharge
                                  </Label>
                                  <Badge
                                    style={{
                                      backgroundColor: '#d1fae5',
                                      color: '#065f46',
                                    }}
                                  >
                                    {
                                      latestDischargeForAdmission.assetConditionOnDischarge
                                    }
                                  </Badge>
                                </div>
                                <div>
                                  <Label className="text-xs text-green-700">
                                    Quality Check
                                  </Label>
                                  <Badge
                                    style={
                                      latestDischargeForAdmission.qualityCheckPassed
                                        ? {
                                            backgroundColor: '#d1fae5',
                                            color: '#065f46',
                                          }
                                        : {
                                            backgroundColor: '#fee2e2',
                                            color: '#991b1b',
                                          }
                                    }
                                  >
                                    {latestDischargeForAdmission.qualityCheckPassed
                                      ? 'Passed'
                                      : 'Failed'}
                                  </Badge>
                                </div>
                              </div>
                              {latestDischargeForAdmission.dischargeNotes && (
                                <div className="mt-3">
                                  <Label className="text-xs text-green-700">
                                    Discharge Notes
                                  </Label>
                                  <p className="text-sm text-green-900 mt-1">
                                    {latestDischargeForAdmission.dischargeNotes}
                                  </p>
                                </div>
                              )}
                            </div>
                          </div>
                        )}

                        {/* Condition Inspection Actions */}
                        <div className="border-t pt-4">
                          <Label className="text-sm font-medium text-muted-foreground mb-3 block">
                            Condition Inspections
                          </Label>
                          <div className="flex gap-3">
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => {
                                if (activeAdmissionForJobCard) {
                                  console.log(
                                    '🔍 Opening admission inspection for admission:',
                                    activeAdmissionForJobCard
                                  );
                                  handleOpenConditionInspection(
                                    activeAdmissionForJobCard,
                                    'Admission'
                                  );
                                } else {
                                  console.log(
                                    '❌ No active admission for job card'
                                  );
                                }
                              }}
                            >
                              <ClipboardCheck className="h-4 w-4 mr-2" />
                              Admission Inspection
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => {
                                if (activeAdmissionForJobCard) {
                                  console.log(
                                    '🔍 Opening discharge inspection for admission:',
                                    activeAdmissionForJobCard
                                  );
                                  handleOpenConditionInspection(
                                    activeAdmissionForJobCard,
                                    'Discharge'
                                  );
                                } else {
                                  console.log(
                                    '❌ No active admission for job card'
                                  );
                                }
                              }}
                            >
                              <ClipboardCheck className="h-4 w-4 mr-2" />
                              Discharge Inspection
                            </Button>
                          </div>
                        </div>
                      </CardContent>
                    </Card>
                  ) : (
                    <Card>
                      <CardContent className="py-8 text-center text-muted-foreground">
                        No admission record found for this job card.
                      </CardContent>
                    </Card>
                  )}
                </TabsContent>

                {/* WORKFLOW TAB */}
                <WorkflowTabContent
                  value="workflow"
                  entityType="JobCard"
                  entityId={selectedCardDetails.id}
                  entityLabel="Job Card"
                  entityNumber={selectedCardDetails.jobCardNumber}
                  status={selectedCardDetails.jobCardStatus}
                  currentStepName={selectedCardWorkflowSummary?.currentStepName}
                  workflowSummary={selectedCardWorkflowSummary}
                  canSubmit={selectedCardDetails.jobCardStatus === 'Draft'}
                  canApproveReject={selectedCardAwaitingApproval}
                  onSubmit={async () => {
                    if (TESTING_MODE) {
                      setJobCards((prev) =>
                        prev.map((card) =>
                          card.id === selectedCardDetails.id
                            ? {
                                ...card,
                                jobCardStatus: 'Submitted',
                                approvalStatus: 'Pending',
                              }
                            : card
                        )
                      );
                      setFilteredCards((prev) =>
                        prev.map((card) =>
                          card.id === selectedCardDetails.id
                            ? {
                                ...card,
                                jobCardStatus: 'Submitted',
                                approvalStatus: 'Pending',
                              }
                            : card
                        )
                      );
                      return;
                    }

                    await jobCardService.submitJobCard(selectedCardDetails.id, {
                      confirmReadiness: true,
                    });
                  }}
                  onApprove={async (comments) => {
                    if (TESTING_MODE) {
                      setJobCards((prev) =>
                        prev.map((card) =>
                          card.id === selectedCardDetails.id
                            ? {
                                ...card,
                                jobCardStatus: 'Approved',
                                approvalStatus: 'Approved',
                              }
                            : card
                        )
                      );
                      setFilteredCards((prev) =>
                        prev.map((card) =>
                          card.id === selectedCardDetails.id
                            ? {
                                ...card,
                                jobCardStatus: 'Approved',
                                approvalStatus: 'Approved',
                              }
                            : card
                        )
                      );
                      return;
                    }

                    const approvalAction: JobCardApprovalAction = {
                      action: 'Approve',
                      comments: comments || undefined,
                      billingType:
                        selectedCardDetails.workOrderBillingType || 'Repairs',
                    };
                    await jobCardService.processApproval(
                      selectedCardDetails.id,
                      approvalAction
                    );
                  }}
                  onReject={async (comments) => {
                    if (TESTING_MODE) {
                      setJobCards((prev) =>
                        prev.map((card) =>
                          card.id === selectedCardDetails.id
                            ? {
                                ...card,
                                jobCardStatus: 'Rejected',
                                approvalStatus: 'Rejected',
                              }
                            : card
                        )
                      );
                      setFilteredCards((prev) =>
                        prev.map((card) =>
                          card.id === selectedCardDetails.id
                            ? {
                                ...card,
                                jobCardStatus: 'Rejected',
                                approvalStatus: 'Rejected',
                              }
                            : card
                        )
                      );
                      return;
                    }

                    const approvalAction: JobCardApprovalAction = {
                      action: 'Reject',
                      comments,
                    };
                    await jobCardService.processApproval(
                      selectedCardDetails.id,
                      approvalAction
                    );
                  }}
                  onAfterAction={async () => {
                    await refreshJobCards();
                    const refreshed = await jobCardService.getJobCardById(
                      selectedCardDetails.id
                    );
                    setSelectedCardDetails(refreshed);
                    setSelectedCard(mapJobCardResponseToGridCard(refreshed));
                  }}
                  onOpenWorkflows={() =>
                    router.push('/administration/workflow')
                  }
                >
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">
                        Maintenance Lifecycle Timeline
                      </CardTitle>
                      <CardDescription>
                        Track the operational journey from job card request to
                        work completion
                      </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-6">
                      {/* Job Card Creation */}
                      <div className="flex gap-4">
                        <div className="flex flex-col items-center">
                          <div className="w-10 h-10 rounded-full bg-blue-100 flex items-center justify-center">
                            <Calendar className="h-5 w-5 text-blue-600" />
                          </div>
                          <div className="w-0.5 h-full bg-blue-200 mt-2"></div>
                        </div>
                        <div className="flex-1 pb-6">
                          <h4 className="font-semibold text-base">
                            Job Card Created
                          </h4>
                          <p className="text-sm text-muted-foreground mt-1">
                            Requested by {selectedCardDetails.requestedBy}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {format(
                              new Date(selectedCardDetails.requestedDate),
                              'MMM dd, yyyy HH:mm:ss'
                            )}
                          </p>
                        </div>
                      </div>

                      {/* Approval Steps */}
                      {selectedCardDetails.approvalSteps &&
                        selectedCardDetails.approvalSteps.length > 0 &&
                        selectedCardDetails.approvalSteps.map((step, index) => (
                          <div key={step.id} className="flex gap-4">
                            <div className="flex flex-col items-center">
                              <div
                                className={`w-10 h-10 rounded-full flex items-center justify-center ${
                                  step.status === 'Approved'
                                    ? 'bg-green-100'
                                    : step.status === 'Rejected'
                                      ? 'bg-red-100'
                                      : 'bg-yellow-100'
                                }`}
                              >
                                {step.status === 'Approved' ? (
                                  <CheckCircle className="h-5 w-5 text-green-600" />
                                ) : step.status === 'Rejected' ? (
                                  <XCircle className="h-5 w-5 text-red-600" />
                                ) : (
                                  <Clock className="h-5 w-5 text-yellow-600" />
                                )}
                              </div>
                              {(selectedCardDetails.generatedWorkOrderId ||
                                index <
                                  selectedCardDetails.approvalSteps.length -
                                    1) && (
                                <div className="w-0.5 h-full bg-gray-200 mt-2"></div>
                              )}
                            </div>
                            <div className="flex-1 pb-6">
                              <div className="flex items-center gap-2">
                                <h4 className="font-semibold text-base">
                                  {step.stepName}
                                </h4>
                                <Badge
                                  style={
                                    step.status === 'Approved'
                                      ? {
                                          backgroundColor: '#d1fae5',
                                          color: '#065f46',
                                        }
                                      : step.status === 'Rejected'
                                        ? {
                                            backgroundColor: '#fee2e2',
                                            color: '#991b1b',
                                          }
                                        : {
                                            backgroundColor: '#fef3c7',
                                            color: '#92400e',
                                          }
                                  }
                                >
                                  {step.status}
                                </Badge>
                              </div>
                              {step.approverName && (
                                <p className="text-sm text-muted-foreground mt-1">
                                  Approver: {step.approverName}
                                </p>
                              )}
                              {step.actionDate && (
                                <p className="text-xs text-muted-foreground">
                                  {format(
                                    new Date(step.actionDate),
                                    'MMM dd, yyyy HH:mm:ss'
                                  )}
                                </p>
                              )}
                              {step.comments && (
                                <p className="text-sm mt-2 bg-gray-50 p-2 rounded">
                                  {step.comments}
                                </p>
                              )}
                            </div>
                          </div>
                        ))}

                      {/* Asset Admission Step */}
                      {
                        <div className="flex gap-4">
                          <div className="flex flex-col items-center">
                            <div
                              className={`w-10 h-10 rounded-full flex items-center justify-center ${
                                activeAdmissionForJobCard
                                  ? 'bg-green-100'
                                  : 'bg-yellow-100'
                              }`}
                            >
                              {activeAdmissionForJobCard ? (
                                <CheckCircle className="h-5 w-5 text-green-600" />
                              ) : (
                                <Clock className="h-5 w-5 text-yellow-600" />
                              )}
                            </div>
                            {(activeAdmissionForJobCard ||
                              selectedCardDetails.generatedWorkOrderId) && (
                              <div className="w-0.5 h-full bg-gray-200 mt-2"></div>
                            )}
                          </div>
                          <div className="flex-1 pb-6">
                            <div className="flex items-center gap-2">
                              <h4 className="font-semibold text-base">
                                Asset Admission
                              </h4>
                              <Badge
                                style={
                                  activeAdmissionForJobCard
                                    ? {
                                        backgroundColor: '#d1fae5',
                                        color: '#065f46',
                                      }
                                    : {
                                        backgroundColor: '#fef3c7',
                                        color: '#92400e',
                                      }
                                }
                              >
                                {activeAdmissionForJobCard
                                  ? 'Completed'
                                  : 'Pending'}
                              </Badge>
                            </div>
                            {activeAdmissionForJobCard ? (
                              <>
                                <p className="text-sm text-muted-foreground mt-1">
                                  Admission #
                                  {activeAdmissionForJobCard.admissionNumber}
                                </p>
                                <p className="text-xs text-muted-foreground">
                                  {format(
                                    new Date(
                                      activeAdmissionForJobCard.admissionDate
                                    ),
                                    'MMM dd, yyyy HH:mm:ss'
                                  )}
                                </p>
                                <p className="text-sm mt-1">
                                  Condition:{' '}
                                  <span className="font-medium">
                                    {
                                      activeAdmissionForJobCard.assetConditionOnAdmission
                                    }
                                  </span>
                                </p>
                              </>
                            ) : (
                              <div className="mt-2">
                                <p className="text-sm text-muted-foreground mb-2">
                                  Admit the asset to record its condition before
                                  maintenance work begins.
                                </p>
                                <Button
                                  size="sm"
                                  onClick={() => {
                                    // Pre-fill the admission form with job card data
                                    setAdmissionForm((prev) => ({
                                      ...prev,
                                      assetId: selectedCardDetails.assetId,
                                      jobCardId: selectedCardDetails.id,
                                      workOrderId:
                                        selectedCardDetails.generatedWorkOrderId ||
                                        '',
                                    }));
                                    setIsAdmissionDialogOpen(true);
                                  }}
                                >
                                  <ClipboardCheck className="h-4 w-4 mr-2" />
                                  Admit Asset
                                </Button>
                              </div>
                            )}
                          </div>
                        </div>
                      }

                      {/* Work Order Generation */}
                      {selectedCardDetails.generatedWorkOrderId && (
                        <div className="flex gap-4">
                          <div className="flex flex-col items-center">
                            <div className="w-10 h-10 rounded-full bg-purple-100 flex items-center justify-center">
                              <CheckCircle className="h-5 w-5 text-purple-600" />
                            </div>
                            {selectedWorkOrder && (
                              <div className="w-0.5 h-full bg-purple-200 mt-2"></div>
                            )}
                          </div>
                          <div className="flex-1 pb-6">
                            <h4 className="font-semibold text-base">
                              Work Order Generated
                            </h4>
                            <p className="text-sm font-mono mt-1">
                              {selectedCardDetails.generatedWorkOrderNumber ||
                                selectedCardDetails.generatedWorkOrderId}
                            </p>
                            {selectedCardDetails.workOrderGeneratedAt && (
                              <p className="text-xs text-muted-foreground">
                                {format(
                                  new Date(
                                    selectedCardDetails.workOrderGeneratedAt
                                  ),
                                  'MMM dd, yyyy HH:mm:ss'
                                )}
                              </p>
                            )}
                          </div>
                        </div>
                      )}

                      {/* Work Order Execution */}
                      {selectedWorkOrder && (
                        <>
                          <div className="flex gap-4">
                            <div className="flex flex-col items-center">
                              <div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center">
                                <AlertCircle className="h-5 w-5 text-orange-600" />
                              </div>
                              {selectedWorkOrder.status === 'Completed' && (
                                <div className="w-0.5 h-full bg-orange-200 mt-2"></div>
                              )}
                            </div>
                            <div className="flex-1 pb-6">
                              <div className="flex items-center gap-2">
                                <h4 className="font-semibold text-base">
                                  Work Order Execution
                                </h4>
                                <Badge>{selectedWorkOrder.status}</Badge>
                              </div>
                              {selectedWorkOrder.actualStartDate && (
                                <p className="text-xs text-muted-foreground">
                                  Started:{' '}
                                  {format(
                                    new Date(selectedWorkOrder.actualStartDate),
                                    'MMM dd, yyyy HH:mm:ss'
                                  )}
                                </p>
                              )}
                              {selectedWorkOrder.actualCompletionDate && (
                                <p className="text-xs text-muted-foreground">
                                  Completed:{' '}
                                  {format(
                                    new Date(
                                      selectedWorkOrder.actualCompletionDate
                                    ),
                                    'MMM dd, yyyy HH:mm:ss'
                                  )}
                                </p>
                              )}

                              {/* Technician + cost snapshot (read-only, mirrors Work Orders "Actual to Date") */}
                              {selectedWorkOrder && (
                                <div className="mt-3 rounded-md border bg-muted/40 p-3 text-xs space-y-1">
                                  <div className="flex justify-between">
                                    <span className="font-semibold">
                                      Technician
                                    </span>
                                    <span>
                                      {selectedWorkOrder.assignedTechnician ||
                                        'Unassigned'}
                                    </span>
                                  </div>
                                  {(() => {
                                    const laborCost =
                                      workOrderLaborForSnapshot?.reduce(
                                        (sum: number, l: any) =>
                                          sum + (l.totalCost || 0),
                                        0
                                      ) || 0;
                                    const partsCost = (
                                      selectedWorkOrder.parts || []
                                    ).reduce(
                                      (sum: number, p: any) =>
                                        sum + (p.totalCost || 0),
                                      0
                                    );
                                    const toolsCost =
                                      toolSummaryForSnapshot?.totalRentalCost ||
                                      0;
                                    const expensesCost =
                                      totalExpensesForSnapshot || 0;
                                    const actualTotal =
                                      laborCost +
                                      partsCost +
                                      toolsCost +
                                      expensesCost;

                                    return (
                                      <>
                                        <div className="flex justify-between text-muted-foreground">
                                          <span>Labor</span>
                                          <span className="font-medium">
                                            {formatMoney(laborCost)}
                                          </span>
                                        </div>
                                        <div className="flex justify-between text-muted-foreground">
                                          <span>Parts</span>
                                          <span className="font-medium">
                                            {formatMoney(partsCost)}
                                          </span>
                                        </div>
                                        <div className="flex justify-between text-muted-foreground">
                                          <span>Tools</span>
                                          <span className="font-medium">
                                            {formatMoney(toolsCost)}
                                          </span>
                                        </div>
                                        <div className="flex justify-between text-muted-foreground">
                                          <span>Expenses</span>
                                          <span className="font-medium">
                                            {formatMoney(expensesCost)}
                                          </span>
                                        </div>
                                        <div className="mt-2 border-t pt-2 flex justify-between">
                                          <span className="font-semibold">
                                            Actual Total
                                          </span>
                                          <span className="font-semibold">
                                            {formatMoney(actualTotal)}
                                          </span>
                                        </div>
                                      </>
                                    );
                                  })()}
                                </div>
                              )}
                            </div>
                          </div>

                          {/* QC Inspection */}
                          {selectedWorkOrder.status === 'Completed' && (
                            <div className="flex gap-4">
                              <div className="flex flex-col items-center">
                                <div
                                  className={`w-10 h-10 rounded-full flex items-center justify-center ${
                                    selectedQCInspection
                                      ? 'bg-green-100'
                                      : 'bg-gray-100'
                                  }`}
                                >
                                  {selectedQCInspection ? (
                                    <CheckCircle className="h-5 w-5 text-green-600" />
                                  ) : (
                                    <Clock className="h-5 w-5 text-gray-400" />
                                  )}
                                </div>
                              </div>
                              <div className="flex-1">
                                <h4 className="font-semibold text-base">
                                  Quality Control Inspection
                                </h4>
                                {selectedQCInspection ? (
                                  <>
                                    <div className="flex items-center gap-2 mt-1">
                                      <Badge
                                        style={
                                          selectedQCInspection.overallResult ===
                                          'Pass'
                                            ? {
                                                backgroundColor: '#d1fae5',
                                                color: '#065f46',
                                              }
                                            : {
                                                backgroundColor: '#fee2e2',
                                                color: '#991b1b',
                                              }
                                        }
                                      >
                                        {selectedQCInspection.overallResult}
                                      </Badge>
                                      <span className="text-sm font-semibold">
                                        Score: {selectedQCInspection.score}%
                                      </span>
                                    </div>
                                    <p className="text-xs text-muted-foreground">
                                      {format(
                                        new Date(
                                          selectedQCInspection.inspectionDate
                                        ),
                                        'MMM dd, yyyy HH:mm:ss'
                                      )}
                                    </p>
                                    {selectedQCInspection.overallResult ===
                                      'Pass' && (
                                      <p className="text-sm mt-2 text-green-600 font-semibold">
                                        ✓ Certificate Generated
                                      </p>
                                    )}
                                  </>
                                ) : (
                                  <p className="text-sm text-muted-foreground mt-1">
                                    Pending inspection
                                  </p>
                                )}
                              </div>
                            </div>
                          )}

                          {/* Discharge / Close-out step */}
                          {selectedWorkOrder.status === 'Completed' &&
                            selectedQCInspection &&
                            selectedQCInspection.overallResult === 'Pass' && (
                              <div className="flex gap-4 mt-6">
                                <div className="flex flex-col items-center">
                                  <div
                                    className={`w-10 h-10 rounded-full flex items-center justify-center ${
                                      latestDischargeForAdmission
                                        ? 'bg-green-100'
                                        : 'bg-blue-100'
                                    }`}
                                  >
                                    <CheckCircle className="h-5 w-5 text-green-600" />
                                  </div>
                                </div>
                                <div className="flex-1">
                                  <div className="flex items-center justify-between">
                                    <h4 className="font-semibold text-base">
                                      Discharge / Close-out
                                    </h4>
                                    {latestDischargeForAdmission ? (
                                      <Badge
                                        variant="outline"
                                        className="bg-green-50 text-green-700 border-green-200"
                                      >
                                        Completed
                                      </Badge>
                                    ) : (
                                      <Badge
                                        variant="outline"
                                        className="bg-blue-50 text-blue-700 border-blue-200"
                                      >
                                        Pending
                                      </Badge>
                                    )}
                                  </div>
                                  {latestDischargeForAdmission ? (
                                    <div className="mt-2 text-sm text-muted-foreground space-y-1">
                                      <p>
                                        Discharged on{' '}
                                        {format(
                                          new Date(
                                            latestDischargeForAdmission.dischargeDate
                                          ),
                                          'MMM dd, yyyy HH:mm:ss'
                                        )}{' '}
                                        with condition{' '}
                                        <span className="font-medium">
                                          {
                                            latestDischargeForAdmission.assetConditionOnDischarge
                                          }
                                        </span>
                                      </p>
                                      <p>
                                        Customer acceptance:{' '}
                                        <span
                                          className={
                                            latestDischargeForAdmission.customerAcceptance
                                              ? 'text-green-600'
                                              : 'text-red-600'
                                          }
                                        >
                                          {latestDischargeForAdmission.customerAcceptance
                                            ? 'Accepted'
                                            : 'Not accepted'}
                                        </span>
                                      </p>
                                      {latestDischargeForAdmission.warrantyDays >
                                        0 && (
                                        <p>
                                          Warranty:{' '}
                                          {
                                            latestDischargeForAdmission.warrantyDays
                                          }{' '}
                                          days
                                          {latestDischargeForAdmission.warrantyExpiration && (
                                            <span>
                                              {' '}
                                              (until
                                              {format(
                                                new Date(
                                                  latestDischargeForAdmission.warrantyExpiration
                                                ),
                                                'MMM dd, yyyy'
                                              )}
                                              )
                                            </span>
                                          )}
                                        </p>
                                      )}
                                    </div>
                                  ) : (
                                    <div className="mt-2 flex flex-col gap-2">
                                      <p className="text-sm text-muted-foreground">
                                        Work order is completed and QC has
                                        passed. You can now discharge the asset
                                        and close out this job card.
                                      </p>
                                      <Button
                                        size="sm"
                                        onClick={() => {
                                          if (!activeAdmissionForJobCard) {
                                            toast({
                                              title: 'No active admission',
                                              description:
                                                'No active admission was found for this job card.',
                                              variant: 'destructive',
                                            });
                                            return;
                                          }
                                          setDischargeForm((prev) => ({
                                            ...prev,
                                            admissionId:
                                              activeAdmissionForJobCard.id,
                                            workCompleted:
                                              prev.workCompleted ||
                                              (selectedWorkOrder?.tasks || [])
                                                .map((t) => `• ${t.taskName}`)
                                                .join('\n'),
                                          }));
                                          setIsDischargeDialogOpen(true);
                                        }}
                                      >
                                        Discharge / Close-out Asset
                                      </Button>
                                    </div>
                                  )}
                                </div>
                              </div>
                            )}
                        </>
                      )}
                    </CardContent>
                  </Card>
                  {/* Discharge / Close-out Dialog */}
                  <Dialog
                    open={isDischargeDialogOpen}
                    onOpenChange={setIsDischargeDialogOpen}
                  >
                    <DialogContent className="max-w-3xl">
                      <DialogHeader>
                        <DialogTitle>Discharge / Close-out Asset</DialogTitle>
                        <DialogDescription>
                          Capture the final condition and close-out details for
                          this maintenance job.
                        </DialogDescription>
                      </DialogHeader>

                      <div className="space-y-4 mt-2">
                        {/* Context summary */}
                        <div className="grid grid-cols-2 gap-4 border rounded-md p-3 bg-muted/40">
                          <div>
                            <Label className="text-xs font-medium text-muted-foreground">
                              Asset
                            </Label>
                            <p className="text-sm font-semibold">
                              {selectedCardDetails?.assetName}{' '}
                              {selectedCardDetails?.assetCode && (
                                <span className="text-xs text-muted-foreground">
                                  ({selectedCardDetails.assetCode})
                                </span>
                              )}
                            </p>
                          </div>
                          <div>
                            <Label className="text-xs font-medium text-muted-foreground">
                              Job Card
                            </Label>
                            <p className="text-sm font-semibold">
                              {selectedCardDetails?.jobCardNumber} -{' '}
                              {selectedCardDetails?.title}
                            </p>
                          </div>
                        </div>

                        {/* Condition & work summary */}
                        <div className="grid grid-cols-2 gap-4">
                          <div>
                            <Label>Condition on Discharge</Label>
                            <Select
                              value={dischargeForm.assetConditionOnDischarge}
                              onValueChange={(value) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  assetConditionOnDischarge: value as any,
                                }))
                              }
                            >
                              <SelectTrigger className="mt-1">
                                <SelectValue placeholder="Select condition" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="Excellent">
                                  Excellent
                                </SelectItem>
                                <SelectItem value="Good">Good</SelectItem>
                                <SelectItem value="Fair">Fair</SelectItem>
                                <SelectItem value="Poor">Poor</SelectItem>
                                <SelectItem value="Critical">
                                  Critical
                                </SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                          <div>
                            <Label>Customer Acceptance</Label>
                            <div className="flex items-center gap-3 mt-2">
                              <Button
                                type="button"
                                size="sm"
                                variant={
                                  dischargeForm.customerAcceptance
                                    ? 'default'
                                    : 'outline'
                                }
                                onClick={() =>
                                  setDischargeForm((prev) => ({
                                    ...prev,
                                    customerAcceptance: true,
                                  }))
                                }
                              >
                                Accepted
                              </Button>
                              <Button
                                type="button"
                                size="sm"
                                variant={
                                  !dischargeForm.customerAcceptance
                                    ? 'default'
                                    : 'outline'
                                }
                                onClick={() =>
                                  setDischargeForm((prev) => ({
                                    ...prev,
                                    customerAcceptance: false,
                                  }))
                                }
                              >
                                Not Accepted
                              </Button>
                            </div>
                          </div>
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                          <div>
                            <Label>Work Completed</Label>
                            <Textarea
                              rows={4}
                              className="mt-1"
                              value={dischargeForm.workCompleted}
                              onChange={(e) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  workCompleted: e.target.value,
                                }))
                              }
                            />
                          </div>
                          <div>
                            <Label>Remaining Issues / Notes</Label>
                            <Textarea
                              rows={4}
                              className="mt-1"
                              value={dischargeForm.remainingIssues}
                              onChange={(e) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  remainingIssues: e.target.value,
                                }))
                              }
                            />
                          </div>
                        </div>

                        {/* Readings & warranty */}
                        <div className="grid grid-cols-3 gap-4">
                          <div>
                            <Label>Mileage Reading</Label>
                            <Input
                              type="number"
                              className="mt-1"
                              value={dischargeForm.mileageReading}
                              onChange={(e) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  mileageReading: Number(e.target.value) || 0,
                                }))
                              }
                            />
                          </div>
                          <div>
                            <Label>Hours Reading</Label>
                            <Input
                              type="number"
                              className="mt-1"
                              value={dischargeForm.hoursReading}
                              onChange={(e) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  hoursReading: Number(e.target.value) || 0,
                                }))
                              }
                            />
                          </div>
                          <div>
                            <Label>Fuel Level (%)</Label>
                            <Input
                              type="number"
                              className="mt-1"
                              value={dischargeForm.fuelLevel}
                              onChange={(e) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  fuelLevel: Number(e.target.value) || 0,
                                }))
                              }
                            />
                          </div>
                        </div>

                        <div className="grid grid-cols-2 gap-4">
                          <div>
                            <Label>Warranty Days</Label>
                            <Input
                              type="number"
                              className="mt-1"
                              value={dischargeForm.warrantyDays}
                              onChange={(e) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  warrantyDays: Number(e.target.value) || 0,
                                }))
                              }
                            />
                          </div>
                          <div>
                            <Label>Warranty Terms</Label>
                            <Textarea
                              rows={3}
                              className="mt-1"
                              value={dischargeForm.warrantyTerms}
                              onChange={(e) =>
                                setDischargeForm((prev) => ({
                                  ...prev,
                                  warrantyTerms: e.target.value,
                                }))
                              }
                            />
                          </div>
                        </div>
                      </div>

                      <DialogFooter className="mt-4">
                        <Button
                          variant="outline"
                          onClick={() => setIsDischargeDialogOpen(false)}
                        >
                          Cancel
                        </Button>
                        <Button
                          onClick={async () => {
                            if (!activeAdmissionForJobCard) {
                              toast({
                                title: 'No active admission',
                                description:
                                  'No active admission was found for this job card.',
                                variant: 'destructive',
                              });
                              return;
                            }
                            try {
                              setIsSubmittingDischarge(true);
                              const request: CreateDischargeRequest = {
                                admissionId: activeAdmissionForJobCard.id,
                                assetConditionOnDischarge:
                                  dischargeForm.assetConditionOnDischarge,
                                dischargeNotes:
                                  dischargeForm.dischargeNotes || undefined,
                                workCompleted:
                                  dischargeForm.workCompleted || undefined,
                                remainingIssues:
                                  dischargeForm.remainingIssues || undefined,
                                mileageReading:
                                  dischargeForm.mileageReading || undefined,
                                hoursReading:
                                  dischargeForm.hoursReading || undefined,
                                fuelLevel: dischargeForm.fuelLevel || undefined,
                                qualityCheckPassed:
                                  dischargeForm.qualityCheckPassed,
                                qualityCheckNotes:
                                  dischargeForm.qualityCheckNotes || undefined,
                                customerAcceptance:
                                  dischargeForm.customerAcceptance,
                                acceptanceNotes:
                                  dischargeForm.acceptanceNotes || undefined,
                                requiresFollowUp:
                                  dischargeForm.requiresFollowUp,
                                followUpDate:
                                  dischargeForm.followUpDate || undefined,
                                followUpInstructions:
                                  dischargeForm.followUpInstructions ||
                                  undefined,
                                warrantyDays:
                                  dischargeForm.warrantyDays || undefined,
                                warrantyTerms:
                                  dischargeForm.warrantyTerms || undefined,
                              };

                              const discharge =
                                await assetAdmissionService.createDischarge(
                                  request
                                );
                              setLatestDischargeForAdmission(discharge);

                              toast({
                                title: 'Asset discharged',
                                description:
                                  'The asset has been discharged and close-out recorded.',
                              });

                              setIsDischargeDialogOpen(false);
                            } catch (error) {
                              console.error('Error creating discharge:', error);
                              toast({
                                title: 'Error',
                                description: 'Failed to create discharge',
                                variant: 'destructive',
                              });
                            } finally {
                              setIsSubmittingDischarge(false);
                            }
                          }}
                          disabled={isSubmittingDischarge}
                        >
                          {isSubmittingDischarge
                            ? 'Discharging...'
                            : 'Discharge Asset'}
                        </Button>
                      </DialogFooter>
                    </DialogContent>
                  </Dialog>
                </WorkflowTabContent>

                {/* WORK ORDER TAB */}
                <TabsContent value="workorder" className="space-y-4 mt-4">
                  {selectedWorkOrder ? (
                    <>
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-lg">
                            Work Order Information
                          </CardTitle>
                          <CardDescription>
                            WO# {selectedWorkOrder.workOrderNumber}
                          </CardDescription>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          <div className="grid grid-cols-1 gap-4">
                            <div>
                              <Label className="text-sm font-semibold">
                                Status
                              </Label>
                              <Badge className="mt-1">
                                {selectedWorkOrder.status}
                              </Badge>
                            </div>
                          </div>

                          <div className="grid grid-cols-2 gap-4 border-t pt-4">
                            <div>
                              <Label className="text-sm font-semibold">
                                Estimated Hours
                              </Label>
                              <p className="text-xl font-bold mt-1">
                                {selectedWorkOrder.estimatedHours}
                              </p>
                            </div>
                            <div>
                              <Label className="text-sm font-semibold">
                                Actual Hours
                              </Label>
                              <p className="text-xl font-bold mt-1">
                                {selectedWorkOrder.actualHours}
                              </p>
                            </div>
                            <div>
                              <Label className="text-sm font-semibold">
                                Estimated Cost
                              </Label>
                              <p className="text-xl font-bold mt-1">
                                {formatMoney(selectedWorkOrder.estimatedCost)}
                              </p>
                            </div>
                            <div>
                              <Label className="text-sm font-semibold">
                                Actual Cost
                              </Label>
                              <p className="text-xl font-bold mt-1">
                                {formatMoney(selectedWorkOrder.actualCost)}
                              </p>
                            </div>
                          </div>

                          {selectedWorkOrder.actualStartDate && (
                            <div className="border-t pt-4">
                              <Label className="text-sm font-semibold">
                                Actual Start Date
                              </Label>
                              <p className="text-base mt-1">
                                {format(
                                  new Date(selectedWorkOrder.actualStartDate),
                                  'MMM dd, yyyy HH:mm:ss'
                                )}
                              </p>
                            </div>
                          )}

                          {selectedWorkOrder.actualCompletionDate && (
                            <div>
                              <Label className="text-sm font-semibold">
                                Actual Completion Date
                              </Label>
                              <p className="text-base mt-1">
                                {format(
                                  new Date(
                                    selectedWorkOrder.actualCompletionDate
                                  ),
                                  'MMM dd, yyyy HH:mm:ss'
                                )}
                              </p>
                            </div>
                          )}
                        </CardContent>
                      </Card>

                      {selectedWorkOrder.tasks &&
                        selectedWorkOrder.tasks.length > 0 && (
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-lg">
                                Work Order Tasks
                              </CardTitle>
                            </CardHeader>
                            <CardContent>
                              <div className="space-y-2">
                                {selectedWorkOrder.tasks.map((task) => (
                                  <div
                                    key={task.id}
                                    className="flex items-center justify-between border rounded-lg p-3"
                                  >
                                    <div className="flex-1">
                                      <p className="font-medium text-sm">
                                        {task.taskName}
                                      </p>
                                      {task.description && (
                                        <p className="text-xs text-muted-foreground">
                                          {task.description}
                                        </p>
                                      )}
                                    </div>
                                    <Badge>{task.status}</Badge>
                                  </div>
                                ))}
                              </div>
                            </CardContent>
                          </Card>
                        )}

                      {/* Scheduled Technicians */}
                      {workOrderStaffSchedules.length > 0 && (
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-lg">
                              Scheduled Technicians
                            </CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="overflow-x-auto">
                              <Table>
                                <TableHeader>
                                  <TableRow>
                                    <TableHead>Technician</TableHead>
                                    <TableHead>Schedule Type</TableHead>
                                    <TableHead>Start</TableHead>
                                    <TableHead>End</TableHead>
                                    <TableHead>Status</TableHead>
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                  {workOrderStaffSchedules.map(
                                    (schedule, index) => (
                                      <TableRow key={schedule.id || index}>
                                        <TableCell>
                                          {schedule.technicianFullName ||
                                            schedule.technicianName ||
                                            'N/A'}
                                        </TableCell>
                                        <TableCell>
                                          {schedule.scheduleType}
                                        </TableCell>
                                        <TableCell>
                                          {schedule.startDateTime
                                            ? format(
                                                new Date(
                                                  schedule.startDateTime
                                                ),
                                                'MMM dd, yyyy HH:mm'
                                              )
                                            : 'N/A'}
                                        </TableCell>
                                        <TableCell>
                                          {schedule.endDateTime
                                            ? format(
                                                new Date(schedule.endDateTime),
                                                'MMM dd, yyyy HH:mm'
                                              )
                                            : 'N/A'}
                                        </TableCell>
                                        <TableCell>
                                          <Badge
                                            variant={
                                              schedule.status === 'Completed'
                                                ? 'default'
                                                : schedule.status ===
                                                    'InProgress'
                                                  ? 'secondary'
                                                  : 'outline'
                                            }
                                          >
                                            {schedule.status}
                                          </Badge>
                                        </TableCell>
                                      </TableRow>
                                    )
                                  )}
                                </TableBody>
                              </Table>
                            </div>
                          </CardContent>
                        </Card>
                      )}

                      {/* Parts */}
                      {selectedWorkOrder.parts &&
                        selectedWorkOrder.parts.length > 0 && (
                          <Card>
                            <CardHeader>
                              <CardTitle className="text-lg">
                                Parts Used
                              </CardTitle>
                            </CardHeader>
                            <CardContent>
                              <div className="overflow-x-auto">
                                <Table>
                                  <TableHeader>
                                    <TableRow>
                                      <TableHead>Item</TableHead>
                                      <TableHead>Quantity Used</TableHead>
                                      <TableHead>Unit Cost</TableHead>
                                      <TableHead>Total Cost</TableHead>
                                    </TableRow>
                                  </TableHeader>
                                  <TableBody>
                                    {selectedWorkOrder.parts.map(
                                      (part: any, index: number) => (
                                        <TableRow key={part.id || index}>
                                          <TableCell>
                                            {part.itemName ||
                                              part.itemCode ||
                                              'N/A'}
                                          </TableCell>
                                          <TableCell>
                                            {part.quantityUsed ?? 0}
                                          </TableCell>
                                          <TableCell>
                                            {part.unitCost != null
                                              ? formatMoney(part.unitCost)
                                              : '-'}
                                          </TableCell>
                                          <TableCell>
                                            {part.totalCost != null
                                              ? formatMoney(part.totalCost)
                                              : '-'}
                                          </TableCell>
                                        </TableRow>
                                      )
                                    )}
                                  </TableBody>
                                </Table>
                              </div>
                            </CardContent>
                          </Card>
                        )}

                      {/* Tools */}
                      {workOrderTools.length > 0 && (
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-lg">Tools</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="overflow-x-auto">
                              <Table>
                                <TableHeader>
                                  <TableRow>
                                    <TableHead>Tool</TableHead>
                                    <TableHead>Required</TableHead>
                                    <TableHead>Allocated</TableHead>
                                    <TableHead>Notes</TableHead>
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                  {workOrderTools.map((tool, index) => (
                                    <TableRow key={tool.id || index}>
                                      <TableCell>
                                        {tool.toolName ||
                                          tool.toolCode ||
                                          'N/A'}
                                      </TableCell>
                                      <TableCell>
                                        {tool.isRequired ? 'Yes' : 'No'}
                                      </TableCell>
                                      <TableCell>
                                        {tool.isAllocated ? 'Yes' : 'No'}
                                      </TableCell>
                                      <TableCell>{tool.notes || '-'}</TableCell>
                                    </TableRow>
                                  ))}
                                </TableBody>
                              </Table>
                            </div>
                          </CardContent>
                        </Card>
                      )}

                      {/* Expenses */}
                      {workOrderExpenses.length > 0 && (
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-lg">Expenses</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="overflow-x-auto">
                              <Table>
                                <TableHeader>
                                  <TableRow>
                                    <TableHead>Type</TableHead>
                                    <TableHead>Description</TableHead>
                                    <TableHead>Date</TableHead>
                                    <TableHead>Amount</TableHead>
                                  </TableRow>
                                </TableHeader>
                                <TableBody>
                                  {workOrderExpenses.map((expense, index) => (
                                    <TableRow key={expense.id || index}>
                                      <TableCell>
                                        {expense.expenseType}
                                      </TableCell>
                                      <TableCell>
                                        {expense.description}
                                      </TableCell>
                                      <TableCell>
                                        {expense.expenseDate
                                          ? format(
                                              new Date(expense.expenseDate),
                                              'MMM dd, yyyy'
                                            )
                                          : 'N/A'}
                                      </TableCell>
                                      <TableCell>
                                        {formatMoney(expense.amount)}
                                      </TableCell>
                                    </TableRow>
                                  ))}
                                </TableBody>
                              </Table>
                            </div>
                          </CardContent>
                        </Card>
                      )}
                    </>
                  ) : (
                    <Card>
                      <CardContent className="py-8 text-center text-muted-foreground">
                        No work order has been generated yet.
                      </CardContent>
                    </Card>
                  )}
                </TabsContent>

                {/* QC INSPECTION TAB */}
                <TabsContent value="qc" className="space-y-4 mt-4">
                  {selectedQCInspection ? (
                    <Card>
                      <CardHeader>
                        <CardTitle className="text-lg">
                          Quality Control Inspection Results
                        </CardTitle>
                      </CardHeader>
                      <CardContent className="space-y-4">
                        <div className="grid grid-cols-3 gap-4">
                          <div>
                            <Label className="text-sm font-semibold">
                              Overall Result
                            </Label>
                            <div className="mt-1">
                              <Badge
                                style={
                                  selectedQCInspection.overallResult === 'Pass'
                                    ? {
                                        backgroundColor: '#d1fae5',
                                        color: '#065f46',
                                        fontSize: '16px',
                                        padding: '8px 12px',
                                      }
                                    : {
                                        backgroundColor: '#fee2e2',
                                        color: '#991b1b',
                                        fontSize: '16px',
                                        padding: '8px 12px',
                                      }
                                }
                              >
                                {selectedQCInspection.overallResult}
                              </Badge>
                            </div>
                          </div>
                          <div>
                            <Label className="text-sm font-semibold">
                              Quality Score
                            </Label>
                            <p className="text-3xl font-bold mt-1">
                              {selectedQCInspection.score}%
                            </p>
                          </div>
                          <div>
                            <Label className="text-sm font-semibold">
                              Inspection Date
                            </Label>
                            <p className="text-base mt-1">
                              {format(
                                new Date(selectedQCInspection.inspectionDate),
                                'MMM dd, yyyy HH:mm:ss'
                              )}
                            </p>
                          </div>
                        </div>

                        <div className="border-t pt-4">
                          <Label className="text-sm font-semibold">
                            Inspector
                          </Label>
                          <p className="text-base mt-1">
                            {selectedQCInspection.inspectorName || 'Unknown'}
                          </p>
                        </div>

                        {selectedQCInspection.notes && (
                          <div className="border-t pt-4">
                            <Label className="text-sm font-semibold">
                              Inspection Notes
                            </Label>
                            <p className="text-base mt-1 bg-gray-50 p-3 rounded">
                              {selectedQCInspection.notes}
                            </p>
                          </div>
                        )}

                        {selectedQCInspection.overallResult === 'Pass' && (
                          <div className="border-t pt-4">
                            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                              <div className="flex items-center justify-between">
                                <div className="flex items-center gap-2">
                                  <CheckCircle className="h-6 w-6 text-green-600" />
                                  <div>
                                    <p className="font-semibold text-green-900">
                                      Quality Certificate Generated
                                    </p>
                                    <p className="text-sm text-green-700">
                                      This work order has passed quality
                                      inspection and a certificate has been
                                      generated.
                                    </p>
                                  </div>
                                </div>
                                <Button
                                  variant="outline"
                                  size="sm"
                                  className="bg-white hover:bg-green-50"
                                  onClick={async () => {
                                    try {
                                      // Open certificate in new window
                                      const baseUrl = '';
                                      window.open(
                                        `${baseUrl}/api/maintenance/quality-control/certificate/${selectedQCInspection.id}`,
                                        '_blank'
                                      );
                                      toast({
                                        title: 'Opening Certificate',
                                        description:
                                          'Certificate is opening in a new window',
                                      });
                                    } catch (error) {
                                      console.error(
                                        'Error opening certificate:',
                                        error
                                      );
                                      toast({
                                        title: 'Error',
                                        description:
                                          'Failed to open certificate',
                                        variant: 'destructive',
                                      });
                                    }
                                  }}
                                >
                                  <Eye className="h-4 w-4 mr-2" />
                                  View Certificate
                                </Button>
                              </div>
                            </div>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  ) : (
                    <Card>
                      <CardContent className="py-8 text-center text-muted-foreground">
                        QC inspection not yet performed.
                      </CardContent>
                    </Card>
                  )}
                </TabsContent>
              </Tabs>
            )
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsViewDialogOpen(false)}
            >
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Condition Inspection Dialog (at Job Card level) */}
      <Dialog
        open={isConditionDialogOpen}
        onOpenChange={setIsConditionDialogOpen}
      >
        <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <ClipboardCheck className="h-5 w-5" />
              {inspectionType} Condition Inspection
            </DialogTitle>
            <DialogDescription>
              {inspectionAdmission &&
                `Asset: ${inspectionAdmission.assetName} (${inspectionAdmission.assetNumber})`}
            </DialogDescription>
          </DialogHeader>

          {/* Template Selection - only show if no inspection started */}
          {!currentInspection && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Select Checklist Template</Label>
                <Select
                  value={selectedTemplate?.id || ''}
                  onValueChange={(value) =>
                    setSelectedTemplate(
                      conditionTemplates.find((t) => t.id === value) || null
                    )
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select a checklist template" />
                  </SelectTrigger>
                  <SelectContent>
                    {conditionTemplates.map((template) => (
                      <SelectItem key={template.id} value={template.id}>
                        {template.name} ({template.itemCount} items)
                        {template.isDefault && ' - Default'}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label>Inspector</Label>
                <Select
                  value={selectedInspectorId}
                  onValueChange={(value) => setSelectedInspectorId(value)}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select inspector" />
                  </SelectTrigger>
                  <SelectContent>
                    {users.map((user) => (
                      <SelectItem key={user.id} value={user.id}>
                        {user.firstName && user.lastName
                          ? `${user.firstName} ${user.lastName}`
                          : user.username}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Select the person who performed the inspection
                </p>
              </div>

              {conditionTemplates.length === 0 && (
                <div className="p-4 border rounded-lg bg-yellow-50 text-yellow-700">
                  <p className="font-medium">
                    No checklist templates available
                  </p>
                  <p className="text-sm">
                    Please create a checklist template in Administration &gt;
                    Maintenance &gt; Admission Checklists
                  </p>
                </div>
              )}

              {selectedTemplate && (
                <div className="p-4 border rounded-lg bg-muted/30">
                  <h4 className="font-medium mb-2">{selectedTemplate.name}</h4>
                  <p className="text-sm text-muted-foreground mb-2">
                    {selectedTemplate.description}
                  </p>
                  <div className="text-sm">
                    <span className="font-medium">
                      {selectedTemplate.itemCount}
                    </span>{' '}
                    items to check
                  </div>
                </div>
              )}

              {/* Show existing records info */}
              {existingAdmissionRecord && inspectionType === 'Admission' && (
                <div
                  className={`p-4 border rounded-lg ${existingAdmissionRecord.status === 'Completed' ? 'bg-green-50' : 'bg-yellow-50'}`}
                >
                  <div
                    className={`flex items-center gap-2 ${existingAdmissionRecord.status === 'Completed' ? 'text-green-700' : 'text-yellow-700'}`}
                  >
                    {existingAdmissionRecord.status === 'Completed' ? (
                      <>
                        <CheckIcon className="h-5 w-5" />
                        <span>
                          Admission inspection completed on{' '}
                          {new Date(
                            existingAdmissionRecord.inspectionDate
                          ).toLocaleDateString()}
                        </span>
                      </>
                    ) : (
                      <>
                        <Clock className="h-5 w-5" />
                        <span>
                          Draft inspection in progress - started on{' '}
                          {new Date(
                            existingAdmissionRecord.inspectionDate
                          ).toLocaleDateString()}
                        </span>
                      </>
                    )}
                  </div>
                </div>
              )}

              {existingDischargeRecord && inspectionType === 'Discharge' && (
                <div
                  className={`p-4 border rounded-lg ${existingDischargeRecord.status === 'Completed' ? 'bg-green-50' : 'bg-yellow-50'}`}
                >
                  <div
                    className={`flex items-center gap-2 ${existingDischargeRecord.status === 'Completed' ? 'text-green-700' : 'text-yellow-700'}`}
                  >
                    {existingDischargeRecord.status === 'Completed' ? (
                      <>
                        <CheckIcon className="h-5 w-5" />
                        <span>
                          Discharge inspection completed on{' '}
                          {new Date(
                            existingDischargeRecord.inspectionDate
                          ).toLocaleDateString()}
                        </span>
                      </>
                    ) : (
                      <>
                        <Clock className="h-5 w-5" />
                        <span>
                          Draft inspection in progress - started on{' '}
                          {new Date(
                            existingDischargeRecord.inspectionDate
                          ).toLocaleDateString()}
                        </span>
                      </>
                    )}
                  </div>
                </div>
              )}

              {/* Show Resume button for in-progress inspections */}
              {(inspectionType === 'Admission' &&
                existingAdmissionRecord?.status === 'InProgress') ||
              (inspectionType === 'Discharge' &&
                existingDischargeRecord?.status === 'InProgress') ? (
                <Button
                  onClick={() => {
                    const record =
                      inspectionType === 'Admission'
                        ? existingAdmissionRecord
                        : existingDischargeRecord;
                    if (record) {
                      setCurrentInspection(record);
                      // Find and set the template
                      const template = conditionTemplates.find(
                        (t) => t.id === record.templateId
                      );
                      if (template) {
                        setSelectedTemplate(template);
                      }
                      // Populate item responses from the saved record
                      const responses: Record<
                        string,
                        SubmitAssetConditionItemDto
                      > = {};
                      record.itemResults.forEach((result) => {
                        responses[result.checklistItemId] = {
                          checklistItemId: result.checklistItemId,
                          isPresent: result.isPresent,
                          textValue: result.textValue,
                          numericValue: result.numericValue,
                          selectedOption: result.selectedOption,
                          comment: result.comment,
                          repairReplacementAction:
                            result.repairReplacementAction as
                              'None' | 'Repair' | 'Replace' | undefined,
                          photoPaths: result.photoPaths,
                        };
                      });
                      setItemResponses(responses);
                    }
                  }}
                  className="w-full bg-yellow-600 hover:bg-yellow-700"
                >
                  Resume Draft Inspection
                </Button>
              ) : (
                <Button
                  onClick={handleStartInspection}
                  disabled={
                    !selectedTemplate ||
                    isSubmittingInspection ||
                    conditionTemplates.length === 0 ||
                    (inspectionType === 'Admission' &&
                      existingAdmissionRecord?.status === 'Completed') ||
                    (inspectionType === 'Discharge' &&
                      existingDischargeRecord?.status === 'Completed')
                  }
                  className="w-full"
                >
                  {isSubmittingInspection
                    ? 'Starting...'
                    : 'Start New Inspection'}
                </Button>
              )}
            </div>
          )}

          {/* Inspection Form - show checklist items */}
          {currentInspection && selectedTemplate && (
            <div className="space-y-4">
              <div className="flex items-center justify-between p-3 border rounded-lg bg-blue-50">
                <div>
                  <span className="font-medium">
                    Inspection #{currentInspection.inspectionNumber}
                  </span>
                  <span className="text-sm text-muted-foreground ml-4">
                    Status: {currentInspection.status}
                  </span>
                </div>
                <Badge>
                  {
                    Object.keys(itemResponses).filter(
                      (k) =>
                        itemResponses[k].isPresent !== undefined ||
                        itemResponses[k].textValue ||
                        itemResponses[k].numericValue !== undefined ||
                        itemResponses[k].selectedOption
                    ).length
                  }
                  /{selectedTemplate.checklistItems.length} completed
                </Badge>
              </div>

              <div className="space-y-3 max-h-[400px] overflow-y-auto">
                {selectedTemplate.checklistItems.map((item, index) => (
                  <div key={item.id} className="p-4 border rounded-lg">
                    <div className="flex items-start justify-between mb-2">
                      <div>
                        <span className="font-medium">
                          {index + 1}. {item.itemName}
                        </span>
                        {item.isRequired && (
                          <Badge variant="destructive" className="ml-2 text-xs">
                            Required
                          </Badge>
                        )}
                        {item.requiresPhoto && (
                          <Badge variant="outline" className="ml-2 text-xs">
                            📷
                          </Badge>
                        )}
                      </div>
                      <Badge variant="outline">{item.category}</Badge>
                    </div>
                    {item.helpText && (
                      <p className="text-sm text-muted-foreground mb-3">
                        {item.helpText}
                      </p>
                    )}

                    {/* Response input based on item type */}
                    {item.itemType === 'Boolean' && (
                      <div className="flex items-center gap-4">
                        <Button
                          size="sm"
                          variant={
                            itemResponses[item.id]?.isPresent === true
                              ? 'default'
                              : 'outline'
                          }
                          onClick={() =>
                            setItemResponses((prev) => ({
                              ...prev,
                              [item.id]: {
                                ...prev[item.id],
                                checklistItemId: item.id,
                                isPresent: true,
                              },
                            }))
                          }
                          className="flex items-center gap-2"
                        >
                          <CheckIcon className="h-4 w-4" /> Present
                        </Button>
                        <Button
                          size="sm"
                          variant={
                            itemResponses[item.id]?.isPresent === false
                              ? 'destructive'
                              : 'outline'
                          }
                          onClick={() =>
                            setItemResponses((prev) => ({
                              ...prev,
                              [item.id]: {
                                ...prev[item.id],
                                checklistItemId: item.id,
                                isPresent: false,
                              },
                            }))
                          }
                          className="flex items-center gap-2"
                        >
                          <XIcon className="h-4 w-4" /> Absent
                        </Button>
                      </div>
                    )}

                    {item.itemType === 'Text' && (
                      <Textarea
                        value={itemResponses[item.id]?.textValue || ''}
                        onChange={(e) =>
                          setItemResponses((prev) => ({
                            ...prev,
                            [item.id]: {
                              ...prev[item.id],
                              checklistItemId: item.id,
                              textValue: e.target.value,
                            },
                          }))
                        }
                        placeholder="Enter description..."
                        rows={2}
                      />
                    )}

                    {item.itemType === 'Numeric' && (
                      <div className="flex items-center gap-2">
                        <Input
                          type="number"
                          value={itemResponses[item.id]?.numericValue ?? ''}
                          onChange={(e) =>
                            setItemResponses((prev) => ({
                              ...prev,
                              [item.id]: {
                                ...prev[item.id],
                                checklistItemId: item.id,
                                numericValue:
                                  parseFloat(e.target.value) || undefined,
                              },
                            }))
                          }
                          placeholder={`${item.minValue ?? 0} - ${item.maxValue ?? 100}`}
                          className="w-32"
                        />
                        {item.unit && (
                          <span className="text-sm text-muted-foreground">
                            {item.unit}
                          </span>
                        )}
                      </div>
                    )}

                    {item.itemType === 'Choice' && item.choiceOptions && (
                      <Select
                        value={itemResponses[item.id]?.selectedOption || ''}
                        onValueChange={(value) =>
                          setItemResponses((prev) => ({
                            ...prev,
                            [item.id]: {
                              ...prev[item.id],
                              checklistItemId: item.id,
                              selectedOption: value,
                            },
                          }))
                        }
                      >
                        <SelectTrigger className="w-48">
                          <SelectValue placeholder="Select option" />
                        </SelectTrigger>
                        <SelectContent>
                          {item.choiceOptions.map((opt) => (
                            <SelectItem key={opt} value={opt}>
                              {opt}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}

                    {/* Comment field for all types */}
                    <div className="mt-2">
                      <Input
                        placeholder="Add comment (optional)"
                        value={itemResponses[item.id]?.comment || ''}
                        onChange={(e) =>
                          setItemResponses((prev) => ({
                            ...prev,
                            [item.id]: {
                              ...prev[item.id],
                              checklistItemId: item.id,
                              comment: e.target.value,
                            },
                          }))
                        }
                      />
                    </div>

                    {/* Photo upload for items that require photos */}
                    {item.requiresPhoto && (
                      <div className="mt-3 p-3 border rounded-lg bg-muted/50">
                        <div className="flex items-center justify-between mb-2">
                          <Label className="text-sm font-medium">
                            📷 Photo Required
                          </Label>
                          <Button
                            type="button"
                            size="sm"
                            variant="outline"
                            onClick={() => triggerPhotoUpload(item.id)}
                            disabled={uploadingPhotoItemId === item.id}
                          >
                            {uploadingPhotoItemId === item.id
                              ? 'Uploading...'
                              : 'Browse & Attach Photo'}
                          </Button>
                        </div>
                        {/* Show uploaded photos */}
                        {(itemResponses[item.id]?.photoPaths?.length ?? 0) >
                          0 && (
                          <div className="flex flex-wrap gap-2 mt-2">
                            {itemResponses[item.id]?.photoPaths?.map(
                              (path, idx) => (
                                <Badge
                                  key={idx}
                                  variant="secondary"
                                  className="text-xs"
                                >
                                  📷 Photo {idx + 1} attached
                                </Badge>
                              )
                            )}
                          </div>
                        )}
                        {/* Show local file previews */}
                        {(itemPhotos[item.id]?.length ?? 0) > 0 && (
                          <div className="flex flex-wrap gap-2 mt-2">
                            {itemPhotos[item.id]?.map((file, idx) => (
                              <div key={idx} className="relative">
                                <img
                                  src={URL.createObjectURL(file)}
                                  alt={`Photo ${idx + 1}`}
                                  className="h-16 w-16 object-cover rounded border"
                                />
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}

                    {/* Repair/Replacement option for items that allow it */}
                    {item.allowRepairReplacement &&
                      inspectionType === 'Admission' && (
                        <div className="mt-3 p-3 border rounded-lg bg-orange-50 dark:bg-orange-950/20">
                          <div className="flex items-center gap-4">
                            <Label className="text-sm font-medium">
                              🔧 Action Required:
                            </Label>
                            <Select
                              value={
                                itemResponses[item.id]
                                  ?.repairReplacementAction ||
                                item.defaultRepairReplacementAction ||
                                'None'
                              }
                              onValueChange={(value) => {
                                const repairReplacementAction =
                                  value as SubmitAssetConditionItemDto['repairReplacementAction'];
                                setItemResponses((prev) => ({
                                  ...prev,
                                  [item.id]: {
                                    ...prev[item.id],
                                    checklistItemId: item.id,
                                    repairReplacementAction,
                                  },
                                }));
                              }}
                            >
                              <SelectTrigger className="w-40">
                                <SelectValue placeholder="Select action" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="None">None</SelectItem>
                                <SelectItem value="Repair">Repair</SelectItem>
                                <SelectItem value="Replace">Replace</SelectItem>
                              </SelectContent>
                            </Select>
                            {itemResponses[item.id]?.repairReplacementAction &&
                              itemResponses[item.id]
                                ?.repairReplacementAction !== 'None' && (
                                <span className="text-xs text-muted-foreground">
                                  Est.{' '}
                                  {itemResponses[item.id]
                                    ?.repairReplacementAction === 'Repair'
                                    ? `${item.estimatedRepairHours ?? 1} hrs`
                                    : `${item.estimatedReplacementHours ?? 1} hrs`}
                                </span>
                              )}
                          </div>
                          <p className="text-xs text-muted-foreground mt-1">
                            This will create a task in the work order for this
                            item.
                          </p>
                        </div>
                      )}
                  </div>
                ))}
              </div>

              {/* Hidden file input for photo uploads */}
              <input
                type="file"
                ref={photoInputRef}
                className="hidden"
                accept="image/*"
                onChange={(e) => {
                  if (uploadingPhotoItemId) {
                    handlePhotoUpload(uploadingPhotoItemId, e.target.files);
                    e.target.value = ''; // Reset input
                  }
                }}
              />

              <DialogFooter>
                <Button
                  variant="outline"
                  onClick={handleSaveDraft}
                  disabled={isSubmittingInspection}
                >
                  {isSubmittingInspection ? 'Saving...' : 'Save Draft'}
                </Button>
                <Button
                  onClick={handleCompleteInspection}
                  disabled={isSubmittingInspection}
                >
                  {isSubmittingInspection
                    ? 'Submitting...'
                    : 'Complete Inspection'}
                </Button>
              </DialogFooter>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Approval Dialog with Billing Type Selection */}
      <Dialog
        open={isApprovalDialogOpen}
        onOpenChange={setIsApprovalDialogOpen}
      >
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <CheckCircle className="h-5 w-5 text-green-600" />
              Approve Job Card
            </DialogTitle>
            <DialogDescription>
              Select the billing type for the work order that will be created
              from this job card.
            </DialogDescription>
          </DialogHeader>

          <div className="py-4 space-y-4">
            <div className="space-y-2">
              <Label className="text-sm font-medium">
                Work Order Billing Type
              </Label>
              <Select
                value={approvalBillingType}
                onValueChange={(value: 'Maintenance' | 'Repairs') =>
                  setApprovalBillingType(value)
                }
              >
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Select billing type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Repairs">
                    <div className="flex items-center gap-2">
                      <span className="font-medium">Repairs</span>
                      <span className="text-xs text-muted-foreground">
                        - Itemized costs (parts, labor, tools, expenses)
                      </span>
                    </div>
                  </SelectItem>
                  <SelectItem value="Maintenance">
                    <div className="flex items-center gap-2">
                      <span className="font-medium">Maintenance</span>
                      <span className="text-xs text-muted-foreground">
                        - Fixed amount from maintenance type
                      </span>
                    </div>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3">
              <p className="text-sm text-blue-700">
                {approvalBillingType === 'Repairs' ? (
                  <>
                    <strong>Repairs:</strong> The work order will use itemized
                    costing. Parts, labor hours, tools, and expenses will be
                    tracked individually.
                  </>
                ) : (
                  <>
                    <strong>Maintenance:</strong> The work order will use the
                    fixed billing amount defined in the maintenance type
                    configuration.
                  </>
                )}
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsApprovalDialogOpen(false)}
            >
              Cancel
            </Button>
            <Button
              onClick={confirmApproveJobCard}
              className="bg-green-600 hover:bg-green-700"
            >
              <CheckCircle className="mr-2 h-4 w-4" />
              Approve & Create Work Order
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
