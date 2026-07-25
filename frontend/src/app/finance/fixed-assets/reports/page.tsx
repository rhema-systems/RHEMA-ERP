'use client';

import { useState, useEffect } from 'react';
import { useSearchParams } from 'next/navigation';
import {
    BarChart3,
    FileText,
    Download,
    Filter,
    Loader2,
    ChevronRight,
    Search,
    Calendar,
    Table as TableIcon
} from 'lucide-react';
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue
} from "@/components/ui/select";
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { format } from 'date-fns';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import { FixedAssetStatus } from '@/types/fixed-assets';
import {
    FixedAssetReportQuery,
    FixedAssetRegister,
    AssetDisposalReportItem,
    AssetTransferReportItem
} from '@/types/fixed-asset-reports';
import { toast } from 'sonner';

const REPORT_TYPES = [
    { id: 'AssetRegister', slug: 'asset-register', title: 'Asset Register', description: 'Comprehensive list of all fixed assets with their current valuation.' },
    { id: 'DisposalReport', slug: 'disposal-activity', title: 'Disposal Activity', description: 'Summary of all assets disposed of within a period, including gain/loss.' },
    { id: 'TransferReport', slug: 'transfer-history', title: 'Transfer History', description: 'Audit trail of movements between locations and departments.' },
];

function getReportType(reportSlug: string | null) {
    return REPORT_TYPES.find((report) => report.slug === reportSlug || report.id === reportSlug) ?? REPORT_TYPES[0];
}

export default function FixedAssetReportsPage() {
    const searchParams = useSearchParams();
    const reportParam = searchParams.get('report');
    const [selectedReport, setSelectedReport] = useState(() => getReportType(reportParam));
    const [loading, setLoading] = useState(false);
    const [exporting, setExporting] = useState<string | null>(null);
    const [reportData, setReportData] = useState<any>(null);

    // Filters
    const [filters, setFilters] = useState<FixedAssetReportQuery>({
        fromDate: '',
        toDate: '',
        searchTerm: '',
    });

    const fetchReportData = async () => {
        setLoading(true);
        try {
            if (selectedReport.id === 'AssetRegister') {
                const data = await fixedAssetsDataService.getAssetRegister(filters);
                setReportData(data);
            } else if (selectedReport.id === 'DisposalReport') {
                const data = await fixedAssetsDataService.getDisposalReport(filters);
                setReportData(data);
            } else if (selectedReport.id === 'TransferReport') {
                const data = await fixedAssetsDataService.getTransferReport(filters);
                setReportData(data);
            }
        } catch (error) {
            console.error('Failed to fetch report data:', error);
            toast.error('Failed to generate report preview');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchReportData();
    }, [selectedReport, filters.status, filters.categoryId]);

    useEffect(() => {
        setSelectedReport(getReportType(reportParam));
    }, [reportParam]);

    const handleDownload = async (format: 'pdf' | 'excel') => {
        setExporting(format);
        try {
            const downloadFn = format === 'pdf'
                ? fixedAssetsDataService.downloadPdf
                : fixedAssetsDataService.downloadExcel;

            const { fileName, blob } = await downloadFn(selectedReport.id, filters);

            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.setAttribute('download', fileName);
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.URL.revokeObjectURL(url);

            toast.success(`${selectedReport.title} exported as ${format.toUpperCase()}`);
        } catch (error) {
            console.error(`Export to ${format} failed:`, error);
            toast.error(`Failed to export report as ${format.toUpperCase()}`);
        } finally {
            setExporting(null);
        }
    };

    const renderPreview = () => {
        if (loading) {
            return (
                <div className="flex flex-col items-center justify-center h-64 space-y-4">
                    <Loader2 className="h-8 w-8 animate-spin text-blue-500" />
                    <p className="text-slate-500">Generating preview...</p>
                </div>
            );
        }

        if (!reportData) return null;

        if (selectedReport.id === 'AssetRegister') {
            const data = reportData as FixedAssetRegister;
            return (
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Code</TableHead>
                            <TableHead>Asset Name</TableHead>
                            <TableHead>Category</TableHead>
                            <TableHead className="text-right">Cost</TableHead>
                            <TableHead className="text-right">NBV</TableHead>
                            <TableHead>Status</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {(data?.items || []).map((item, idx) => (
                            <TableRow key={idx}>
                                <TableCell className="font-medium">{item.assetCode}</TableCell>
                                <TableCell>{item.name}</TableCell>
                                <TableCell>{item.categoryName}</TableCell>
                                <TableCell className="text-right">{item.cost?.toLocaleString()}</TableCell>
                                <TableCell className="text-right">{item.netBookValue?.toLocaleString()}</TableCell>
                                <TableCell>
                                    <Badge variant={item.status === 'Active' ? 'default' : 'outline'}>
                                        {item.status}
                                    </Badge>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            );
        }

        if (selectedReport.id === 'DisposalReport') {
            const items = reportData as AssetDisposalReportItem[];
            return (
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Code</TableHead>
                            <TableHead>Asset Name</TableHead>
                            <TableHead>Type</TableHead>
                            <TableHead>Date</TableHead>
                            <TableHead className="text-right">Proceeds</TableHead>
                            <TableHead className="text-right">Gain/Loss</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {(items || []).map((item, idx) => (
                            <TableRow key={idx}>
                                <TableCell className="font-medium">{item.assetCode}</TableCell>
                                <TableCell>{item.name}</TableCell>
                                <TableCell>{item.disposalType}</TableCell>
                                <TableCell>{item.disposalDate ? format(new Date(item.disposalDate), 'dd MMM yyyy') : ''}</TableCell>
                                <TableCell className="text-right">{item.saleProceeds?.toLocaleString()}</TableCell>
                                <TableCell className={`text-right font-semibold ${item.gainLoss >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                                    {item.gainLoss >= 0 ? '+' : ''}{item.gainLoss?.toLocaleString()}
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            );
        }

        if (selectedReport.id === 'TransferReport') {
            const items = reportData as AssetTransferReportItem[];
            return (
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Code</TableHead>
                            <TableHead>Asset Name</TableHead>
                            <TableHead>From</TableHead>
                            <TableHead>To</TableHead>
                            <TableHead>Date</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {(items || []).map((item, idx) => (
                            <TableRow key={idx}>
                                <TableCell className="font-medium">{item.assetCode}</TableCell>
                                <TableCell>{item.name}</TableCell>
                                <TableCell>{item.fromLocation || item.fromDepartment || 'N/A'}</TableCell>
                                <TableCell>{item.toLocation || item.toDepartment || 'N/A'}</TableCell>
                                <TableCell>{item.transferDate ? format(new Date(item.transferDate), 'dd MMM yyyy') : ''}</TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            );
        }

        return (
            <div className="flex flex-col items-center justify-center h-48 text-slate-400">
                <TableIcon className="h-12 w-12 mb-2 opacity-20" />
                <p>No preview available for this report type.</p>
            </div>
        );
    };

    return (
        <div className="p-6 space-y-6">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Fixed Asset Reports</h1>
                    <p className="text-slate-500 dark:text-slate-400">Generate financial and operational insights.</p>
                </div>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
                {/* Sidebar: Report Types */}
                <div className="space-y-4">
                    <Card>
                        <CardHeader>
                            <CardTitle className="text-sm font-semibold uppercase tracking-wider text-slate-500">Available Reports</CardTitle>
                        </CardHeader>
                        <CardContent className="p-0">
                            <div className="flex flex-col">
                                {REPORT_TYPES.map((report) => (
                                    <button
                                        key={report.id}
                                        onClick={() => setSelectedReport(report)}
                                        className={`flex items-center justify-between p-4 text-left hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors border-l-4 ${selectedReport.id === report.id
                                            ? 'border-blue-600 bg-blue-50/50 dark:bg-blue-900/20'
                                            : 'border-transparent'
                                            }`}
                                    >
                                        <div>
                                            <p className={`font-semibold ${selectedReport.id === report.id ? 'text-blue-600' : 'text-slate-700 dark:text-slate-200'}`}>
                                                {report.title}
                                            </p>
                                            <p className="text-xs text-slate-500 mt-1 line-clamp-1">{report.description}</p>
                                        </div>
                                        <ChevronRight className={`h-4 w-4 ${selectedReport.id === report.id ? 'text-blue-600' : 'text-slate-300'}`} />
                                    </button>
                                ))}
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Main Content: Filters and Preview */}
                <div className="lg:col-span-3 space-y-6">
                    <Card>
                        <CardHeader className="flex flex-row items-center justify-between">
                            <div>
                                <CardTitle>{selectedReport.title}</CardTitle>
                                <CardDescription>{selectedReport.description}</CardDescription>
                            </div>
                            <div className="flex gap-2">
                                <Button
                                    onClick={() => handleDownload('excel')}
                                    disabled={exporting !== null}
                                    variant="outline"
                                    className="bg-green-50 text-green-700 border-green-200 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400 dark:border-green-800"
                                >
                                    {exporting === 'excel' ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Download className="h-4 w-4 mr-2" />}
                                    Excel
                                </Button>
                                <Button
                                    onClick={() => handleDownload('pdf')}
                                    disabled={exporting !== null}
                                    variant="outline"
                                    className="bg-red-50 text-red-700 border-red-200 hover:bg-red-100 dark:bg-red-900/20 dark:text-red-400 dark:border-red-800"
                                >
                                    {exporting === 'pdf' ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Download className="h-4 w-4 mr-2" />}
                                    PDF
                                </Button>
                            </div>
                        </CardHeader>
                        <CardContent>
                            {/* Filter Panel */}
                            <div className="bg-slate-50 dark:bg-slate-800/50 p-4 rounded-lg mb-6 flex flex-wrap gap-4 items-end">
                                <div className="space-y-1.5 flex-1 min-w-[200px]">
                                    <Label htmlFor="searchTerm">Search</Label>
                                    <div className="relative">
                                        <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-slate-500" />
                                        <Input
                                            id="searchTerm"
                                            placeholder="Asset name or code..."
                                            className="pl-9"
                                            value={filters.searchTerm}
                                            onChange={(e) => setFilters({ ...filters, searchTerm: e.target.value })}
                                        />
                                    </div>
                                </div>

                                {selectedReport.id !== 'AssetRegister' && (
                                    <>
                                        <div className="space-y-1.5">
                                            <Label htmlFor="fromDate">From Date</Label>
                                            <Input
                                                id="fromDate"
                                                type="date"
                                                value={filters.fromDate}
                                                onChange={(e) => setFilters({ ...filters, fromDate: e.target.value })}
                                            />
                                        </div>
                                        <div className="space-y-1.5">
                                            <Label htmlFor="toDate">To Date</Label>
                                            <Input
                                                id="toDate"
                                                type="date"
                                                value={filters.toDate}
                                                onChange={(e) => setFilters({ ...filters, toDate: e.target.value })}
                                            />
                                        </div>
                                    </>
                                )}

                                <div className="space-y-1.5 w-[150px]">
                                    <Label htmlFor="status">Status</Label>
                                    <Select
                                        onValueChange={(val) => setFilters({ ...filters, status: val as any })}
                                    >
                                        <SelectTrigger id="status">
                                            <SelectValue placeholder="All Status" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="All">All Status</SelectItem>
                                            {['Draft', 'Active', 'FullyDepreciated', 'Disposed', 'HeldForSale', 'WrittenOff', 'UnderConstruction', 'OnHold'].map((s) => (
                                                <SelectItem key={s} value={s}>{s}</SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                </div>

                                <Button onClick={fetchReportData} className="bg-blue-600 hover:bg-blue-700">
                                    Generate
                                </Button>
                            </div>

                            {/* Preview Content */}
                            <div className="border rounded-md overflow-hidden">
                                <div className="bg-slate-50 dark:bg-slate-900 px-4 py-2 border-b flex items-center justify-between">
                                    <span className="text-xs font-semibold uppercase text-slate-500">Data Preview</span>
                                    {reportData?.items?.length > 0 && (
                                        <span className="text-xs text-slate-400">{reportData.items.length} records found</span>
                                    )}
                                </div>
                                <div className="max-h-[500px] overflow-auto">
                                    {renderPreview()}
                                </div>
                            </div>
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
}
