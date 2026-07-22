'use client';

import { useEffect, useMemo, useState } from 'react';
import { Download, FileSpreadsheet, Loader2, RefreshCw } from 'lucide-react';
import { format } from 'date-fns';
import { saveAs } from 'file-saver';
import * as XLSX from 'xlsx';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  procurementPlanService,
  type ProcurementPlanningReportDto,
  type ProcurementPlanningReportRowDto,
} from '@/services/procurementPlanningService';
import { FiscalYearSelect } from '../components/FiscalYearSelect';

const currentYear = new Date().getFullYear();

const reportOptions = [
  { value: 'annual', label: 'Annual Procurement Plan' },
  { value: 'quarterly', label: 'Quarterly Procurement Plan' },
  { value: 'department', label: 'Department Plan' },
  { value: 'budget-variance', label: 'Budget Variance' },
  { value: 'publish-register', label: 'Publish Register' },
];

export default function ProcurementPlanningReportsPage() {
  const [reportType, setReportType] = useState('annual');
  const [fiscalYear, setFiscalYear] = useState(currentYear);
  const [planningQuarter, setPlanningQuarter] = useState('all');
  const [report, setReport] = useState<ProcurementPlanningReportDto | null>(null);
  const [loading, setLoading] = useState(true);

  const query = useMemo(() => ({
    fiscalYear,
    planningQuarter: planningQuarter === 'all' ? undefined : planningQuarter,
  }), [fiscalYear, planningQuarter]);

  const loadReport = async () => {
    try {
      setLoading(true);
      const data = await procurementPlanService.getReport(reportType, query);
      setReport(data);
    } catch (error) {
      console.error('Error loading procurement planning report:', error);
      toast.error('Failed to load procurement planning report');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadReport();
  }, [reportType, query]);

  const formatCurrency = (amount: number, currency = report?.currency || 'USD') =>
    new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount || 0);

  const formatDate = (value?: string) => {
    if (!value) return '-';
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? '-' : format(parsed, 'MMM dd, yyyy');
  };

  const exportReport = () => {
    if (!report || report.rows.length === 0) {
      toast.error('No report data to export');
      return;
    }

    const rows = report.rows.map((row: ProcurementPlanningReportRowDto) => ({
      'Plan Number': row.planNumber,
      'Plan Title': row.planTitle,
      Department: row.departmentName || '',
      'Fiscal Year': row.fiscalYear,
      Cycle: row.planningCycle || '',
      Quarter: row.planningQuarter || '',
      Status: row.status,
      Category: row.itemCategory || '',
      Item: row.itemDescription || '',
      Quantity: row.quantity,
      'Estimated Cost': row.estimatedCost,
      'Approved Budget': row.approvedBudget,
      Variance: row.variance,
      'Published By': row.publishedByName || '',
      'Published Date': row.publishedDate ? formatDate(row.publishedDate) : '',
    }));

    const worksheet = XLSX.utils.json_to_sheet(rows);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, 'Procurement Planning');
    const buffer = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' });
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    saveAs(blob, `procurement_${report.reportType}_${format(new Date(), 'yyyyMMdd')}.xlsx`);
    toast.success('Report exported');
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Procurement Planning Reports</h1>
          <p className="text-sm text-muted-foreground">Annual, quarterly, departmental, budget variance, and publish register views.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Select value={reportType} onValueChange={setReportType}>
            <SelectTrigger className="h-9 w-56">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {reportOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <FiscalYearSelect
            value={fiscalYear}
            onValueChange={setFiscalYear}
            triggerClassName="h-9 w-56"
            autoSelectFirstAvailable
          />
          <Select value={planningQuarter} onValueChange={setPlanningQuarter}>
            <SelectTrigger className="h-9 w-36">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Quarters</SelectItem>
              <SelectItem value="Q1">Q1</SelectItem>
              <SelectItem value="Q2">Q2</SelectItem>
              <SelectItem value="Q3">Q3</SelectItem>
              <SelectItem value="Q4">Q4</SelectItem>
            </SelectContent>
          </Select>
          <Button variant="outline" size="sm" onClick={loadReport} disabled={loading}>
            {loading ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <RefreshCw className="h-4 w-4 mr-2" />}
            Refresh
          </Button>
          <Button size="sm" onClick={exportReport} disabled={!report || report.rows.length === 0}>
            <Download className="h-4 w-4 mr-2" />
            Export
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileSpreadsheet className="h-5 w-5" />
            {report?.title || 'Report'}
          </CardTitle>
          <CardDescription>
            {report ? `${report.rows.length} row(s), generated ${formatDate(report.generatedAt)}` : 'Loading report data'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="h-6 w-6 mr-2 animate-spin" />
              Loading report...
            </div>
          ) : !report || report.rows.length === 0 ? (
            <div className="py-12 text-center text-muted-foreground">No rows found for the selected report.</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Plan</TableHead>
                    <TableHead>Department</TableHead>
                    <TableHead>Period</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead>Item</TableHead>
                    <TableHead>Estimated</TableHead>
                    <TableHead>Approved</TableHead>
                    <TableHead>Variance</TableHead>
                    <TableHead>Published</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {report.rows.map((row, index) => (
                    <TableRow key={`${row.planNumber}-${row.itemDescription || 'plan'}-${index}`}>
                      <TableCell>
                        <div className="font-medium">{row.planNumber}</div>
                        <div className="max-w-56 truncate text-xs text-muted-foreground">{row.planTitle}</div>
                      </TableCell>
                      <TableCell>{row.departmentName || '-'}</TableCell>
                      <TableCell>
                        <div>{row.fiscalYear}</div>
                        <div className="text-xs text-muted-foreground">{row.planningCycle || '-'} {row.planningQuarter || ''}</div>
                      </TableCell>
                      <TableCell>{row.status}</TableCell>
                      <TableCell>{row.itemCategory || '-'}</TableCell>
                      <TableCell className="max-w-64 truncate">{row.itemDescription || '-'}</TableCell>
                      <TableCell>{formatCurrency(row.estimatedCost)}</TableCell>
                      <TableCell>{formatCurrency(row.approvedBudget)}</TableCell>
                      <TableCell className={row.variance < 0 ? 'text-red-600' : row.variance > 0 ? 'text-green-600' : ''}>
                        {formatCurrency(row.variance)}
                      </TableCell>
                      <TableCell>
                        <div>{row.publishedByName || '-'}</div>
                        <div className="text-xs text-muted-foreground">{formatDate(row.publishedDate)}</div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
