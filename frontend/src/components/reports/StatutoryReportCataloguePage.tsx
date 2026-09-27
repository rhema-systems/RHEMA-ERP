'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowLeft,
  BarChart3,
  BookOpenCheck,
  Boxes,
  Building2,
  CalendarClock,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Download,
  FileCheck2,
  FileSpreadsheet,
  History,
  Loader2,
  PackageSearch,
  RefreshCw,
  Scale,
  Send,
  ShieldCheck,
  SlidersHorizontal,
  Trash2,
  Warehouse,
  type LucideIcon,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { businessPartnerService } from '@/services/businessPartnerService';
import { financeDataService } from '@/services/finance/finance-data.service';
import { inventoryManagementService } from '@/services/inventoryManagementService';
import { projectService } from '@/services/projectService';
import { ReportResult, reportsService } from '@/services/reports';
import {
  ReportModuleNavigator,
  type ReportModuleNavigationItem,
} from './ReportModuleNavigator';

type CatalogueMode =
  'procurement' | 'inventory' | 'compliance' | 'quantity-survey' | 'civil-engineering';
type ExportFormat = 'pdf' | 'xlsx' | 'csv';

export interface CatalogueItem {
  code: string;
  title: string;
  description: string;
  group: string;
  icon: LucideIcon;
}

const procurementCatalogue: CatalogueItem[] = [
  {
    code: 'requisition-status',
    title: 'Requisition Status',
    description:
      'Track purchase requisition approval, budget, sourcing and order-conversion progress.',
    group: 'Planning and performance',
    icon: ClipboardCheck,
  },
  {
    code: 'app-vs-actual',
    title: 'APP vs Actual',
    description:
      'Compare approved procurement plans with actual sourcing and award activity.',
    group: 'Planning and performance',
    icon: BarChart3,
  },
  {
    code: 'savings-register',
    title: 'Savings Register',
    description:
      'Review planned, evaluated and awarded values with recorded savings.',
    group: 'Planning and performance',
    icon: Scale,
  },
  {
    code: 'tender-register',
    title: 'Tender Register',
    description:
      'Track tender methods, publication, evaluation and award status.',
    group: 'Sourcing and contracts',
    icon: FileSpreadsheet,
  },
  {
    code: 'contract-register',
    title: 'Contract Register',
    description:
      'Review executed contracts, suppliers, values and lifecycle status.',
    group: 'Sourcing and contracts',
    icon: FileCheck2,
  },
  {
    code: 'purchase-order-register',
    title: 'Purchase Order Register',
    description:
      'Review purchase orders, suppliers, governed sources, approvals and receipt progress.',
    group: 'Sourcing and contracts',
    icon: FileSpreadsheet,
  },
  {
    code: 'commitment-register',
    title: 'Commitment Register',
    description:
      'Review budget reservations, overrides, consumption, releases and PO adjustments.',
    group: 'Sourcing and contracts',
    icon: Scale,
  },
  {
    code: 'certificate-tracking',
    title: 'Certificate Tracking',
    description:
      'Track Works payment certificates through approval, AP handoff and payment status.',
    group: 'Sourcing and contracts',
    icon: FileCheck2,
  },
  {
    code: 'award-notification',
    title: 'Award Notifications',
    description:
      'Review successful and unsuccessful bidder notification evidence.',
    group: 'Sourcing and contracts',
    icon: Send,
  },
  {
    code: 'supplier-performance',
    title: 'Supplier Performance',
    description: 'Analyse supplier ratings, risk and delivery performance.',
    group: 'Supplier and governance',
    icon: Building2,
  },
  {
    code: 'etc-minutes-register',
    title: 'ETC Minutes',
    description: 'Review committee meeting, attendance and sign-off records.',
    group: 'Supplier and governance',
    icon: BookOpenCheck,
  },
];

const inventoryLedgerMovementTypes = ['PurchaseReceipt', 'SalesIssue', 'TransferOut', 'TransferIn', 'AdjustmentIn', 'AdjustmentOut', 'ProductionReceipt', 'ProductionIssue', 'CustomerReturn', 'SupplierReturn', 'Scrap', 'OpeningBalance', 'CountAdjustment', 'RequisitionIssue', 'RequisitionReturn', 'LandedCostRevaluation'];

const inventoryCatalogue: CatalogueItem[] = [
  {
    code: 'inventory-ledger', title: 'Inventory Ledger',
    description: 'Follow posted movements chronologically, with balances before and after each movement by item, warehouse and location.',
    group: 'Stock position and movement', icon: History,
  },
  {
    code: 'balance-register',
    title: 'Balance Register',
    description:
      'Review current quantities and values by item, warehouse and location.',
    group: 'Stock position and movement',
    icon: Warehouse,
  },
  {
    code: 'movement-register',
    title: 'Movement Register',
    description:
      'Trace inventory receipts, issues, transfers, returns and adjustments.',
    group: 'Stock position and movement',
    icon: History,
  },
  {
    code: 'ageing-register',
    title: 'Ageing Register',
    description:
      'Analyse stock quantities and values across the configured ageing bands.',
    group: 'Stock position and movement',
    icon: CalendarClock,
  },
  {
    code: 'reorder-register',
    title: 'Reorder Register',
    description:
      'Review reorder exposure, demand cover and replenishment recommendations.',
    group: 'Planning and control',
    icon: PackageSearch,
  },
  {
    code: 'count-variance-register',
    title: 'Count Variances',
    description:
      'Review physical and cycle-count variances with adjustment status.',
    group: 'Planning and control',
    icon: ClipboardCheck,
  },
  {
    code: 'slow-non-moving-register',
    title: 'Slow / Non-moving',
    description: 'Identify slow-moving, non-moving and current-stockout items.',
    group: 'Planning and control',
    icon: Boxes,
  },
  {
    code: 'valuation-gl-register',
    title: 'Valuation to GL',
    description:
      'Reconcile inventory valuation with the Finance control account.',
    group: 'Valuation and compliance',
    icon: Scale,
  },
  {
    code: 'expiry-register',
    title: 'Expiry Register',
    description:
      'Review expired and near-expiry lots within the selected warning period.',
    group: 'Valuation and compliance',
    icon: AlertTriangle,
  },
  {
    code: 'disposal-register',
    title: 'Disposal Register',
    description:
      'Review governed disposal recommendations, approvals and completion.',
    group: 'Valuation and compliance',
    icon: Trash2,
  },
];

const complianceCatalogue: CatalogueItem[] = [
  {
    code: 'opening-register',
    title: 'Opening Register',
    description:
      'Review tender and RFQ opening entries, late submissions, participant sign-off and integrity evidence.',
    group: 'Sourcing integrity',
    icon: FileSpreadsheet,
  },
  {
    code: 'committee-signoff-register',
    title: 'Committee Sign-off',
    description:
      'Review committee quorum, signed attendance, score sheets and meeting evidence.',
    group: 'Sourcing integrity',
    icon: BookOpenCheck,
  },
  {
    code: 'due-diligence-register',
    title: 'Supplier Due Diligence',
    description:
      'Review supplier checks, outcomes, evidence and approval history.',
    group: 'Supplier and Finance compliance',
    icon: ShieldCheck,
  },
  {
    code: 'matching-exception-register',
    title: 'Matching Exceptions',
    description:
      'Review invoice matching exceptions, corrective actions, approvals and expiry.',
    group: 'Supplier and Finance compliance',
    icon: AlertTriangle,
  },
  {
    code: 'po-payment-register',
    title: 'Purchase Order Payments',
    description:
      'Trace purchase-order invoice allocations, readiness controls and Finance posting references.',
    group: 'Supplier and Finance compliance',
    icon: Scale,
  },
  {
    code: 'inventory-adjustment-register',
    title: 'Inventory Adjustments',
    description:
      'Review scoped adjustment quantities, values, approvals, evidence and Finance references.',
    group: 'Inventory controls',
    icon: ClipboardCheck,
  },
  {
    code: 'override-exception-register',
    title: 'Overrides and Exceptions',
    description:
      'Review procurement control events and authorised negative-stock overrides.',
    group: 'Inventory controls',
    icon: SlidersHorizontal,
  },
  {
    code: 'disposal-compliance-register',
    title: 'Disposal Compliance',
    description:
      'Review governed inventory disposals, committee controls, evidence and completion.',
    group: 'Inventory controls',
    icon: Trash2,
  },
];

export const quantitySurveyCatalogue: CatalogueItem[] = [
  {
    code: 'boq-summary',
    title: 'BoQ Summary',
    description:
      'Review governed BoQ versions, immutable line totals and publication history.',
    group: 'Cost plans and BoQs',
    icon: FileSpreadsheet,
  },
  {
    code: 'project-cost-status',
    title: 'Project Cost Status',
    description:
      'Reconcile budget, BoQ, contract, variations, certificates, actuals and forecast.',
    group: 'Cost plans and BoQs',
    icon: BarChart3,
  },
  {
    code: 'valuation-statement',
    title: 'Valuation Statement',
    description:
      'Review interim valuations, governed worksheets, retention and certificate readiness.',
    group: 'Valuations and certificates',
    icon: Scale,
  },
  {
    code: 'certificate-register',
    title: 'Payment Certificate Register',
    description:
      'Trace approved values, deductions, tax, AP handoff and Finance payment status.',
    group: 'Valuations and certificates',
    icon: ClipboardCheck,
  },
  {
    code: 'retention-register',
    title: 'Retention Register',
    description:
      'Reconcile retention held, released and outstanding by certificate and Works contract.',
    group: 'Valuations and certificates',
    icon: Scale,
  },
  {
    code: 'cost-to-complete',
    title: 'Cost-to-Complete Report',
    description:
      'Compare budget, actual cost, commitments, forecast and the projected final position.',
    group: 'Cost plans and BoQs',
    icon: BarChart3,
  },
  {
    code: 'contract-balance',
    title: 'Contract Balance Report',
    description:
      'Reconcile the original and revised contract against variations, certificates and retention.',
    group: 'Changes and closeout',
    icon: FileSpreadsheet,
  },
  {
    code: 'variation-log',
    title: 'Variation Log',
    description:
      'Review variation value, time impact, approval and downstream application.',
    group: 'Changes and closeout',
    icon: History,
  },
  {
    code: 'final-account',
    title: 'Final Account Register',
    description:
      'Reconcile final accounts to contract, BoQ, variations, claims, retention and payments.',
    group: 'Changes and closeout',
    icon: FileCheck2,
  },
  {
    code: 'audit-trail',
    title: 'Quantity Survey Audit Trail',
    description:
      'Review project-scoped creation, change, workflow, approval, posting and reversal history.',
    group: 'Controls and audit',
    icon: History,
  },
];

export const civilEngineeringCatalogue: CatalogueItem[] = [
  {
    code: 'design-backlog',
    title: 'Design Backlog',
    description: 'Reconcile governed design cases and versioned engineering documents.',
    group: 'Design and drafting',
    icon: FileSpreadsheet,
  },
  {
    code: 'field-task-register',
    title: 'Field Task Register',
    description: 'Track Civil assignments, drafting workload, feedback, completion and overdue tasks.',
    group: 'Design and drafting',
    icon: ClipboardCheck,
  },
  {
    code: 'supervision-controls',
    title: 'Supervision Controls',
    description: 'Reconcile RFIs, instructions, weekly reports, test evidence and IPC endorsements.',
    group: 'Supervision and quality',
    icon: ShieldCheck,
  },
  {
    code: 'maintenance-complaints',
    title: 'Maintenance and Complaints',
    description: 'Trace Civil intake, assessment, costing, owner execution and completion controls.',
    group: 'Maintenance delivery',
    icon: Building2,
  },
  {
    code: 'permitting-watch',
    title: 'Permitting Watch',
    description: 'Track development approval files, handoffs, engineering reviews and HOD decisions.',
    group: 'Permitting and approvals',
    icon: BookOpenCheck,
  },
  {
    code: 'completion-handover',
    title: 'Completion and Handover Register',
    description:
      'Reconcile inspection, snag, handover, as-built, closeout and authoritative asset-history links.',
    group: 'Completion and handover',
    icon: FileCheck2,
  },
  {
    code: 'engineering-work-register',
    title: 'Engineering Work Register',
    description:
      'Register governed Civil design, maintenance, complaint and execution work for the selected project.',
    group: 'Architecture-required outputs',
    icon: FileSpreadsheet,
  },
  {
    code: 'inspection-report',
    title: 'Inspection Report',
    description:
      'Review governed inspections, outcomes, corrective actions, evidence and closure status.',
    group: 'Architecture-required outputs',
    icon: ClipboardCheck,
  },
  {
    code: 'site-instruction-log',
    title: 'Site Instruction Log',
    description:
      'Review governed site instructions, issue status, contractor responses and retained evidence.',
    group: 'Architecture-required outputs',
    icon: BookOpenCheck,
  },
  {
    code: 'progress-report',
    title: 'Progress Report',
    description:
      'Review governed progress, milestone, delay, recovery and overdue exposure.',
    group: 'Architecture-required outputs',
    icon: BarChart3,
  },
  {
    code: 'defect-report',
    title: 'Defect Report',
    description:
      'Review snags, defects, corrective actions, due dates, reinspections and closure evidence.',
    group: 'Architecture-required outputs',
    icon: AlertTriangle,
  },
  {
    code: 'completion-certificate-report',
    title: 'Completion Certificate Report',
    description:
      'Review completion, handover, closeout, as-built and approval evidence.',
    group: 'Architecture-required outputs',
    icon: FileCheck2,
  },
  {
    code: 'project-dashboard',
    title: 'Project Dashboard',
    description:
      'Summarise Civil design, field, supervision, maintenance, permitting and closeout exposure.',
    group: 'Architecture-required outputs',
    icon: BarChart3,
  },
  {
    code: 'engineering-audit-trail',
    title: 'Engineering Audit Trail',
    description:
      'Review tenant- and project-scoped Civil Engineering actions from the shared immutable audit log.',
    group: 'Architecture-required outputs',
    icon: History,
  },
];

const supplierFilterReports = new Set([
  'contract-register',
  'supplier-performance',
  'award-notification',
  'purchase-order-register',
  'certificate-tracking',
]);
const procurementFiscalYearReports = new Set([
  'app-vs-actual',
  'requisition-status',
  'commitment-register',
]);
const inventoryAnalyticsReports = new Set([
  'balance-register',
  'ageing-register',
  'reorder-register',
  'slow-non-moving-register',
  'expiry-register',
]);
const inventoryStatusReports = new Set([
  'reorder-register',
  'count-variance-register',
  'valuation-gl-register',
  'slow-non-moving-register',
  'disposal-register',
]);
const complianceStatusReports = new Set([
  'opening-register',
  'committee-signoff-register',
  'due-diligence-register',
  'matching-exception-register',
  'po-payment-register',
  'inventory-adjustment-register',
  'disposal-compliance-register',
]);
const complianceWarehouseReports = new Set([
  'inventory-adjustment-register',
  'override-exception-register',
  'disposal-compliance-register',
]);

function displayValue(value: unknown, dataType?: string, format?: string) {
  if (value === null || value === undefined || value === '') return '\u2014';
  if (dataType === 'Boolean') return value ? 'Yes' : 'No';
  if (dataType === 'DateTime') {
    const date = new Date(String(value));
    return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString();
  }
  if (dataType === 'Decimal' || format === 'N2') {
    const number = Number(value);
    return Number.isFinite(number)
      ? number.toLocaleString(undefined, {
          minimumFractionDigits: 2,
          maximumFractionDigits: 2,
        })
      : String(value);
  }
  return String(value);
}

export function StatutoryReportCataloguePage({
  mode,
  reportCode,
}: {
  mode: CatalogueMode;
  reportCode?: string;
}) {
  const { hasAnyRole, hasPermission } = useAuth();
  const { toast } = useToast();
  const isInventory = mode === 'inventory';
  const isCompliance = mode === 'compliance';
  const isQuantitySurvey = mode === 'quantity-survey';
  const isCivilEngineering = mode === 'civil-engineering';
  const isProjectScopedReports = isQuantitySurvey || isCivilEngineering;
  const catalogue = isInventory
    ? inventoryCatalogue
    : isCompliance
      ? complianceCatalogue
      : isQuantitySurvey
        ? quantitySurveyCatalogue
        : isCivilEngineering
          ? civilEngineeringCatalogue
          : procurementCatalogue;
  const moduleName = isInventory
    ? 'Inventory'
    : isCompliance
      ? 'Audit & Compliance'
      : isQuantitySurvey
        ? 'Quantity Survey'
        : isCivilEngineering
          ? 'Civil Engineering'
          : 'Procurement';
  const modulePath = isInventory
    ? '/reports/inventory'
    : isCompliance
      ? '/reports/audit-compliance'
      : isQuantitySurvey
        ? '/reports/quantity-survey'
        : isCivilEngineering
          ? '/reports/civil-engineering'
          : '/reports/purchasing';
  const catalogueItem = reportCode
    ? catalogue.find((item) => item.code === reportCode)
    : undefined;
  const selectedCode = reportCode ?? '';
  const showStatus = isProjectScopedReports
    ? false
    : isInventory
      ? inventoryStatusReports.has(selectedCode)
      : isCompliance
        ? complianceStatusReports.has(selectedCode)
        : !!selectedCode;
  const showFiscalYear =
    !isInventory && !isCompliance && procurementFiscalYearReports.has(selectedCode);
  const showSupplier =
    !isInventory && !isCompliance && supplierFilterReports.has(selectedCode);
  const showWarehouse =
    (isInventory && selectedCode !== 'valuation-gl-register') ||
    (isCompliance && complianceWarehouseReports.has(selectedCode));
  const showCategory =
    isInventory && inventoryAnalyticsReports.has(selectedCode);
  const isInventoryLedger = isInventory && selectedCode === 'inventory-ledger';
  const showMovementType = isInventory && (selectedCode === 'movement-register' || isInventoryLedger);
  const showFiscalPeriod =
    isInventory && selectedCode === 'valuation-gl-register';
  const showMovementThresholds =
    isInventory && selectedCode === 'slow-non-moving-register';
  const showExpiryThreshold = isInventory && selectedCode === 'expiry-register';
  const isAdministrator = hasAnyRole(['SuperAdmin', 'TenantAdmin']);
  const readPermission = isQuantitySurvey
    ? 'quantity-survey.reports.read'
    : isCivilEngineering
      ? 'civil-engineering.reports.read'
      : 'procurement.reports.read';
  const exportPermission = isQuantitySurvey
    ? 'quantity-survey.reports.export'
    : isCivilEngineering
      ? 'civil-engineering.reports.export'
      : 'procurement.reports.export';
  const canRead = isAdministrator || hasPermission(readPermission);
  const canExport = isAdministrator || hasPermission(exportPermission);
  const [mounted, setMounted] = useState(false);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [fiscalYear, setFiscalYear] = useState('');
  const [status, setStatus] = useState('');
  const [supplierId, setSupplierId] = useState('all');
  const [warehouseId, setWarehouseId] = useState('all');
  const [categoryId, setCategoryId] = useState('all');
  const [movementType, setMovementType] = useState('all');
  const [inventoryItemId, setInventoryItemId] = useState('all');
  const [fiscalPeriodId, setFiscalPeriodId] = useState('all');
  const [slowMovingDays, setSlowMovingDays] = useState('90');
  const [nonMovingDays, setNonMovingDays] = useState('180');
  const [expiryWarningDays, setExpiryWarningDays] = useState('90');
  const [projectId, setProjectId] = useState('');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<ReportResult | null>(null);
  const [exporting, setExporting] = useState<ExportFormat | null>(null);

  const reportsQuery = useQuery({
    queryKey: ['reports', mode, 'statutory-catalogue'],
    queryFn: () => reportsService.getReports(mode, 'published'),
    enabled: canRead,
    refetchOnWindowFocus: false,
  });
  const reports = useMemo(
    () =>
      (reportsQuery.data ?? []).filter((report) =>
        catalogue.some((item) => report.tags?.includes(item.code))
      ),
    [catalogue, reportsQuery.data]
  );
  const navigatorItems: Array<
    ReportModuleNavigationItem & Pick<CatalogueItem, 'description'>
  > = [
    ...(isQuantitySurvey
      ? [
          {
            code: 'dashboard',
            title: 'QS Cost Dashboard',
            description: 'Authority-scoped cost control dashboard.',
            group: 'Cost control',
            icon: BarChart3,
            available: true,
          },
        ]
      : []),
    ...catalogue.map((item) => ({
      ...item,
      available: reports.some((report) => report.tags?.includes(item.code)),
    })),
  ];
  const suppliersQuery = useQuery({
    queryKey: ['business-partners', 'report-filter'],
    queryFn: () => businessPartnerService.getAllPartnersForDropdown(),
    enabled: canRead && showSupplier,
    staleTime: 5 * 60 * 1000,
  });
  const warehousesQuery = useQuery({
    queryKey: ['inventory-warehouses', 'statutory-report-filter'],
    queryFn: () => inventoryManagementService.getWarehouses(true),
    enabled: canRead && showWarehouse,
    staleTime: 5 * 60 * 1000,
  });
  const categoriesQuery = useQuery({
    queryKey: ['inventory-categories', 'statutory-report-filter'],
    queryFn: () => inventoryManagementService.getInventoryCategories(true),
    enabled: canRead && showCategory,
    staleTime: 5 * 60 * 1000,
  });
  const inventoryItemsQuery = useQuery({
    queryKey: ['inventory-items', 'ledger-report-filter'],
    queryFn: () => inventoryManagementService.getInventoryItems(),
    enabled: canRead && isInventoryLedger,
    staleTime: 5 * 60 * 1000,
  });
  const movementTypesQuery = useQuery({
    queryKey: ['inventory-movement-types', 'statutory-report-filter'],
    queryFn: () => inventoryManagementService.getStockMovementTypes(),
    enabled: canRead && showMovementType && !isInventoryLedger,
    staleTime: 5 * 60 * 1000,
  });
  const fiscalPeriodsQuery = useQuery({
    queryKey: ['fiscal-periods', 'inventory-statutory-report-filter'],
    queryFn: () => financeDataService.getFiscalPeriods(),
    enabled: canRead && showFiscalPeriod,
    staleTime: 5 * 60 * 1000,
  });
  const projectsQuery = useQuery({
    queryKey: ['projects', mode, 'report-filter'],
    queryFn: () => projectService.getProjects({ page: 1, pageSize: 500 }),
    enabled: canRead && isProjectScopedReports,
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    setResult(null);
    setPage(1);
  }, [reportCode]);

  const selectedReport = reportCode
    ? reports.find((report) => report.tags?.includes(reportCode))
    : undefined;
  const selectedReportId = selectedReport?.id ?? '';
  const parameters = useMemo(
    () => ({
      ...(startDate ? { startDate } : {}),
      ...(endDate ? { endDate } : {}),
      ...(showFiscalYear && fiscalYear
        ? { fiscalYear: Number(fiscalYear) }
        : {}),
      ...(showStatus && status.trim() ? { status: status.trim() } : {}),
      ...(showSupplier && supplierId !== 'all' ? { supplierId } : {}),
      ...(showWarehouse && warehouseId !== 'all' ? { warehouseId } : {}),
      ...(showCategory && categoryId !== 'all' ? { categoryId } : {}),
      ...(showMovementType && movementType !== 'all' ? { movementType } : {}),
      ...(isInventoryLedger && inventoryItemId !== 'all' ? { inventoryItemId } : {}),
      ...(showFiscalPeriod && fiscalPeriodId !== 'all'
        ? { fiscalPeriodId }
        : {}),
      ...(showMovementThresholds && slowMovingDays
        ? { slowMovingDays: Number(slowMovingDays) }
        : {}),
      ...(showMovementThresholds && nonMovingDays
        ? { nonMovingDays: Number(nonMovingDays) }
        : {}),
      ...(showExpiryThreshold && expiryWarningDays
        ? { expiryWarningDays: Number(expiryWarningDays) }
        : {}),
      ...(isProjectScopedReports && projectId ? { projectId } : {}),
    }),
    [
      categoryId,
      endDate,
      expiryWarningDays,
      fiscalPeriodId,
      fiscalYear,
      movementType,
      inventoryItemId,
      isInventoryLedger,
      nonMovingDays,
      showCategory,
      showExpiryThreshold,
      showFiscalPeriod,
      showFiscalYear,
      showMovementThresholds,
      showMovementType,
      showStatus,
      showSupplier,
      showWarehouse,
      slowMovingDays,
      startDate,
      status,
      supplierId,
      warehouseId,
      isQuantitySurvey,
      isCivilEngineering,
      isProjectScopedReports,
      projectId,
    ]
  );

  const executeMutation = useMutation({
    mutationFn: ({
      reportId,
      targetPage,
    }: {
      reportId: string;
      targetPage: number;
    }) =>
      reportsService.executeReport(reportId, {
        parameters,
        includeMetadata: true,
        page: targetPage,
        pageSize: 100,
      }),
    onSuccess: (data) => {
      setResult(data);
      setPage(data.currentPage);
    },
    onError: (error: Error) =>
      toast({
        title: 'Report generation failed',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const runReport = (targetPage = 1) => {
    if (isProjectScopedReports && !projectId) {
      toast({
        title: 'Select a project',
        description: `${moduleName} reports are restricted to an assigned project.`,
        variant: 'destructive',
      });
      return;
    }
    if (selectedReportId)
      executeMutation.mutate({ reportId: selectedReportId, targetPage });
  };

  const exportReport = async (format: ExportFormat) => {
    if (!selectedReportId) return;
    setExporting(format);
    try {
      const { blob, fileName } = await reportsService.exportReport(
        selectedReportId,
        { format, parameters }
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
      toast({ title: 'Report exported', description: fileName });
    } catch (error) {
      toast({
        title: 'Export failed',
        description:
          error instanceof Error
            ? error.message
            : 'The report could not be exported.',
        variant: 'destructive',
      });
    } finally {
      setExporting(null);
    }
  };

  if (!mounted) {
    return (
      <Card>
        <CardContent className="flex h-32 items-center justify-center">
          <Loader2 className="h-5 w-5 animate-spin" />
        </CardContent>
      </Card>
    );
  }

  if (!canRead) {
    return (
      <Card className="border-amber-200 bg-amber-50">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5" /> {moduleName} reports
          </CardTitle>
          <CardDescription>
            An active tenant responsibility granting {readPermission} is
            required.
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const catalogueStatus = reportsQuery.isLoading ? (
    <Card>
      <CardContent className="flex h-32 items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin" />
      </CardContent>
    </Card>
  ) : reportsQuery.isError ? (
    <Card className="border-red-200">
      <CardContent className="py-6 text-sm text-red-700">
        The {moduleName.toLowerCase()} report catalogue could not be loaded.
      </CardContent>
    </Card>
  ) : reports.length !== catalogue.length ? (
    <Card className="border-amber-200 bg-amber-50">
      <CardContent className="py-4 text-sm text-amber-900">
        Some reports are currently unavailable. Contact your system
        administrator if they remain unavailable after refreshing.
      </CardContent>
    </Card>
  ) : null;

  if (!reportCode) {
    const groups = Array.from(new Set(catalogue.map((item) => item.group)));

    return (
      <div className="space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <h1 className="text-xl font-semibold tracking-tight">
            {moduleName} reports
          </h1>
          <Button
            variant="outline"
            size="sm"
            onClick={() => reportsQuery.refetch()}
            disabled={reportsQuery.isFetching}
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${reportsQuery.isFetching ? 'animate-spin' : ''}`}
            />{' '}
            Refresh catalogue
          </Button>
        </div>

        {catalogueStatus}

        {isQuantitySurvey && (
          <Link
            href="/reports/quantity-survey/dashboard"
            className="group flex items-center justify-between gap-3 rounded-lg border border-blue-200 bg-blue-50/60 p-3 transition hover:border-blue-400 hover:shadow-sm dark:border-blue-900 dark:bg-blue-950/20"
          >
            <div className="flex items-center gap-3">
              <span className="rounded-md bg-blue-100 p-2 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300">
                <BarChart3 className="h-4 w-4" />
              </span>
              <div>
                <h2 className="font-medium">QS cost dashboard</h2>
                <p className="text-xs text-muted-foreground">
                  Budget, commitments, certificates, actuals, variations,
                  forecast and BoQ-line drilldown.
                </p>
              </div>
            </div>
            <Badge>Open dashboard</Badge>
          </Link>
        )}

        {groups.map((group) => (
          <section
            key={group}
            className="space-y-2"
            aria-labelledby={`${mode}-${group.replaceAll(' ', '-').toLowerCase()}`}
          >
            <h2
              id={`${mode}-${group.replaceAll(' ', '-').toLowerCase()}`}
              className="text-sm font-semibold"
            >
              {group}
            </h2>
            <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
              {catalogue
                .filter((item) => item.group === group)
                .map((item) => {
                  const report = reports.find((candidate) =>
                    candidate.tags?.includes(item.code)
                  );
                  const Icon = item.icon;
                  const reportCard = (
                    <div className="flex items-start gap-3">
                      <span className="rounded-md bg-slate-100 p-2 text-slate-700 transition group-hover:bg-blue-50 group-hover:text-blue-700">
                        <Icon className="h-4 w-4" />
                      </span>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-start justify-between gap-2">
                          <h3 className="font-medium">{item.title}</h3>
                          <Badge variant={report ? 'secondary' : 'outline'}>
                            {report ? 'Available' : 'Unavailable'}
                          </Badge>
                        </div>
                        <p className="mt-1 line-clamp-2 text-xs leading-4 text-muted-foreground">
                          {item.description}
                        </p>
                      </div>
                    </div>
                  );

                  return report ? (
                    <Link
                      key={item.code}
                      href={`${modulePath}/${item.code}`}
                      className="group rounded-lg border bg-card p-3 transition hover:border-blue-400 hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                    >
                      {reportCard}
                    </Link>
                  ) : (
                    <div
                      key={item.code}
                      className="group rounded-lg border bg-card p-3 opacity-55"
                    >
                      {reportCard}
                    </div>
                  );
                })}
            </div>
          </section>
        ))}
      </div>
    );
  }

  if (!catalogueItem) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Report not found</CardTitle>
          <CardDescription>
            The requested report is not part of the {moduleName.toLowerCase()}{' '}
            catalogue.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild variant="outline">
            <Link href={modulePath}>
              <ArrowLeft className="mr-2 h-4 w-4" /> Back to{' '}
              {moduleName.toLowerCase()} reports
            </Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex min-h-9 items-center gap-2">
        <ReportModuleNavigator
          moduleName={moduleName}
          modulePath={modulePath}
          activeReportCode={selectedCode}
          items={navigatorItems}
        />
        <div className="min-w-0">
          <h1 className="truncate text-xl font-semibold tracking-tight">
            {catalogueItem.title}
          </h1>
        </div>
      </div>

      {catalogueStatus}

      {!reportsQuery.isLoading && !reportsQuery.isError && !selectedReport && (
        <Card className="border-amber-200 bg-amber-50">
          <CardContent className="py-5 text-sm text-amber-900">
            This report is currently unavailable. Refresh the catalogue or
            contact your system administrator.
          </CardContent>
        </Card>
      )}

      {selectedReport && (
        <Card>
          <CardContent className="p-2">
            <div className="flex w-full flex-nowrap items-end gap-2 overflow-x-auto pb-1">
              <div className="flex h-9 shrink-0 items-center gap-1.5 px-1 text-sm font-medium">
                <SlidersHorizontal className="h-4 w-4" /> Filters:
              </div>
              {isProjectScopedReports && (
                <div className="min-w-64 flex-[2] space-y-1">
                  <Label className="text-xs">Project</Label>
                  <Select value={projectId} onValueChange={setProjectId}>
                    <SelectTrigger className="h-9" aria-label="Project">
                      <SelectValue placeholder="Select assigned project" />
                    </SelectTrigger>
                    <SelectContent>
                      {(projectsQuery.data?.items ?? []).map((project) => (
                        <SelectItem key={project.id} value={project.id}>
                          {project.projectCode} · {project.title}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <div className="min-w-36 flex-1 space-y-1">
                <Label className="text-xs" htmlFor={`${mode}-report-start`}>
                  Start date
                </Label>
                <Input
                  className="h-9"
                  id={`${mode}-report-start`}
                  type="date"
                  value={startDate}
                  onChange={(event) => setStartDate(event.target.value)}
                />
              </div>
              <div className="min-w-36 flex-1 space-y-1">
                <Label className="text-xs" htmlFor={`${mode}-report-end`}>
                  End date
                </Label>
                <Input
                  className="h-9"
                  id={`${mode}-report-end`}
                  type="date"
                  value={endDate}
                  onChange={(event) => setEndDate(event.target.value)}
                />
              </div>
              {showStatus && (
                <div className="min-w-44 flex-1 space-y-1">
                  <Label className="text-xs" htmlFor={`${mode}-report-status`}>
                    Status / classification
                  </Label>
                  <Input
                    className="h-9"
                    id={`${mode}-report-status`}
                    placeholder="All statuses"
                    value={status}
                    onChange={(event) => setStatus(event.target.value)}
                  />
                </div>
              )}
              {showFiscalYear && (
                <div className="min-w-32 flex-1 space-y-1">
                  <Label className="text-xs" htmlFor="procurement-report-year">
                    Fiscal year
                  </Label>
                  <Input
                    className="h-9"
                    id="procurement-report-year"
                    type="number"
                    min="2000"
                    max="2200"
                    placeholder="All years"
                    value={fiscalYear}
                    onChange={(event) => setFiscalYear(event.target.value)}
                  />
                </div>
              )}
              {showSupplier && (
                <div className="min-w-48 flex-1 space-y-1">
                  <Label className="text-xs">Supplier</Label>
                  <Select value={supplierId} onValueChange={setSupplierId}>
                    <SelectTrigger className="h-9" aria-label="Supplier">
                      <SelectValue placeholder="All suppliers" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All suppliers</SelectItem>
                      {(suppliersQuery.data ?? []).map((partner) => (
                        <SelectItem key={partner.id} value={partner.id}>
                          {partner.partnerCode} · {partner.partnerName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              {showWarehouse && (
                <div className="min-w-48 flex-1 space-y-1">
                  <Label className="text-xs">Warehouse</Label>
                  <Select value={warehouseId} onValueChange={setWarehouseId}>
                    <SelectTrigger className="h-9" aria-label="Warehouse">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">
                        All permitted warehouses
                      </SelectItem>
                      {(warehousesQuery.data ?? []).map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.code} · {item.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              {showCategory && (
                <div className="min-w-44 flex-1 space-y-1">
                  <Label className="text-xs">Category</Label>
                  <Select value={categoryId} onValueChange={setCategoryId}>
                    <SelectTrigger className="h-9" aria-label="Category">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All categories</SelectItem>
                      {(categoriesQuery.data ?? []).map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.code} · {item.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              {isInventoryLedger && <div className="min-w-60 flex-1 space-y-1">
                <Label className="text-xs">Item / code</Label>
                <Select value={inventoryItemId} onValueChange={setInventoryItemId}>
                  <SelectTrigger className="h-9" aria-label="Ledger item"><SelectValue /></SelectTrigger>
                  <SelectContent><SelectItem value="all">All items</SelectItem>
                    {(inventoryItemsQuery.data ?? []).map(item => <SelectItem key={item.id} value={item.id}>{item.itemCode} — {item.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>}
              {showMovementType && (
                <div className="min-w-44 flex-1 space-y-1">
                  <Label className="text-xs">Movement type</Label>
                  <Select value={movementType} onValueChange={setMovementType}>
                    <SelectTrigger className="h-9" aria-label="Movement type">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All movement types</SelectItem>
                      {(isInventoryLedger ? inventoryLedgerMovementTypes : movementTypesQuery.data ?? []).map((item) => (
                        <SelectItem key={item} value={item}>
                          {item}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              {showFiscalPeriod && (
                <div className="min-w-48 flex-1 space-y-1">
                  <Label className="text-xs">Fiscal period</Label>
                  <Select
                    value={fiscalPeriodId}
                    onValueChange={setFiscalPeriodId}
                  >
                    <SelectTrigger className="h-9" aria-label="Fiscal period">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All fiscal periods</SelectItem>
                      {(fiscalPeriodsQuery.data ?? []).map((item) => (
                        <SelectItem key={item.id} value={item.id}>
                          {item.periodCode} · {item.periodName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              {showMovementThresholds && (
                <div className="min-w-32 flex-1 space-y-1">
                  <Label className="text-xs" htmlFor="slow-moving-days">
                    Slow-moving days
                  </Label>
                  <Input
                    className="h-9"
                    id="slow-moving-days"
                    type="number"
                    min="1"
                    value={slowMovingDays}
                    onChange={(event) => setSlowMovingDays(event.target.value)}
                  />
                </div>
              )}
              {showMovementThresholds && (
                <div className="min-w-32 flex-1 space-y-1">
                  <Label className="text-xs" htmlFor="non-moving-days">
                    Non-moving days
                  </Label>
                  <Input
                    className="h-9"
                    id="non-moving-days"
                    type="number"
                    min="1"
                    value={nonMovingDays}
                    onChange={(event) => setNonMovingDays(event.target.value)}
                  />
                </div>
              )}
              {showExpiryThreshold && (
                <div className="min-w-32 flex-1 space-y-1">
                  <Label className="text-xs" htmlFor="expiry-warning-days">
                    Expiry warning days
                  </Label>
                  <Input
                    className="h-9"
                    id="expiry-warning-days"
                    type="number"
                    min="1"
                    value={expiryWarningDays}
                    onChange={(event) =>
                      setExpiryWarningDays(event.target.value)
                    }
                  />
                </div>
              )}
              <Button
                size="sm"
                className="h-9 shrink-0"
                onClick={() => runReport(1)}
                disabled={
                  executeMutation.isPending || (isProjectScopedReports && !projectId)
                }
              >
                {executeMutation.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <FileSpreadsheet className="mr-2 h-4 w-4" />
                )}{' '}
                Run report
              </Button>
              {canExport && (
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button
                      variant="outline"
                      size="sm"
                      className="h-9 shrink-0"
                      disabled={
                        !!exporting ||
                        executeMutation.isPending ||
                        (isProjectScopedReports && !projectId)
                      }
                    >
                      {exporting ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Download className="mr-2 h-4 w-4" />
                      )}
                      Export <ChevronDown className="ml-1 h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem
                      onSelect={() => void exportReport('xlsx')}
                    >
                      Excel workbook (.xlsx)
                    </DropdownMenuItem>
                    <DropdownMenuItem onSelect={() => void exportReport('pdf')}>
                      PDF document (.pdf)
                    </DropdownMenuItem>
                    <DropdownMenuItem onSelect={() => void exportReport('csv')}>
                      CSV data (.csv)
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {result && (
        <Card>
          <div className="flex flex-wrap items-center justify-between gap-2 border-b px-3 py-2">
            <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <h2 className="text-sm font-semibold">Results</h2>
              <span className="text-xs text-muted-foreground">
                {result.totalRows.toLocaleString()} row(s) ·{' '}
                {new Date(result.executedAt).toLocaleString()}
              </span>
            </div>
            <Badge variant="secondary">
              Page {result.currentPage} of {Math.max(result.totalPages, 1)}
            </Badge>
          </div>
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  {result.columns
                    .filter((column) => column.isVisible)
                    .map((column) => (
                      <TableHead key={column.name} className="h-9 px-2">
                        {column.displayName ?? column.name}
                      </TableHead>
                    ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {result.data.length === 0 ? (
                  <TableRow>
                    <TableCell
                      colSpan={
                        result.columns.filter((column) => column.isVisible)
                          .length
                      }
                      className="h-16 text-center text-muted-foreground"
                    >
                      No source transactions match the selected filters.
                    </TableCell>
                  </TableRow>
                ) : (
                  result.data.map((row, rowIndex) => (
                    <TableRow key={`${result.currentPage}-${rowIndex}`}>
                      {result.columns
                        .filter((column) => column.isVisible)
                        .map((column) => (
                          <TableCell
                            key={column.name}
                            className="whitespace-nowrap px-2 py-2"
                          >
                            {displayValue(
                              row[column.name],
                              column.dataType,
                              column.format
                            )}
                          </TableCell>
                        ))}
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
          <div className="flex justify-end gap-2 border-t px-3 py-2">
            <Button
              variant="outline"
              size="sm"
              className="h-8"
              disabled={!result.hasPreviousPage || executeMutation.isPending}
              onClick={() => runReport(page - 1)}
            >
              <ChevronLeft className="mr-1 h-4 w-4" /> Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="h-8"
              disabled={!result.hasNextPage || executeMutation.isPending}
              onClick={() => runReport(page + 1)}
            >
              Next <ChevronRight className="ml-1 h-4 w-4" />
            </Button>
          </div>
        </Card>
      )}
    </div>
  );
}
