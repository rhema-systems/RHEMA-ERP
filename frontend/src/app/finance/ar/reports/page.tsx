'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
    BarChart3,
    Calendar as CalendarIcon,
    Download,
    Search,
    FileText,
    Filter
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import { Calendar } from '@/components/ui/calendar';
import { arService } from '@/services/ar-service';
import { formatCurrency, cn } from '@/lib/utils';
import { format } from 'date-fns';
import { Skeleton } from '@/components/ui/skeleton';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui/table';

const REPORT_TABS = ['aging', 'statements'] as const;

function getReportTab(tab: string | null) {
    return REPORT_TABS.find((reportTab) => reportTab === tab) ?? 'aging';
}

export default function ArReportsPage() {
    const searchParams = useSearchParams();
    const tabParam = searchParams.get('tab');
    const [activeTab, setActiveTab] = useState(() => getReportTab(tabParam));

    useEffect(() => {
        setActiveTab(getReportTab(tabParam));
    }, [tabParam]);

    return (
        <div className="space-y-8 p-8 max-w-[1600px] mx-auto">
            <div>
                <h1 className="text-3xl font-bold tracking-tight">AR Reports</h1>
                <p className="text-muted-foreground mt-2">
                    Analyze your receivables and generate customer statements.
                </p>
            </div>

            <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-4">
                <TabsList>
                    <TabsTrigger value="aging">Aging Analysis</TabsTrigger>
                    <TabsTrigger value="statements">Customer Statements</TabsTrigger>
                </TabsList>

                <TabsContent value="aging">
                    <AgingReportView />
                </TabsContent>

                <TabsContent value="statements">
                    <CustomerStatementsView />
                </TabsContent>
            </Tabs>
        </div>
    );
}

function AgingReportView() {
    const [asOfDate, setAsOfDate] = useState<Date>(new Date());

    const { data: agingReport, isLoading } = useQuery({
        queryKey: ['ar-aging-report', asOfDate],
        queryFn: () => arService.getAgingReport(format(asOfDate, 'yyyy-MM-dd')),
    });

    return (
        <Card>
            <CardHeader>
                <div className="flex items-center justify-between">
                    <div>
                        <CardTitle>Aged Receivables</CardTitle>
                        <CardDescription>Breakdown of outstanding balances by days overdue</CardDescription>
                    </div>
                    <div className="flex items-center space-x-2">
                        <Popover>
                            <PopoverTrigger asChild>
                                <Button
                                    variant="outline"
                                    className={cn(
                                        "w-[200px] justify-start text-left font-normal",
                                        !asOfDate && "text-muted-foreground"
                                    )}
                                >
                                    <CalendarIcon className="mr-2 h-4 w-4" />
                                    {asOfDate ? format(asOfDate, "PPP") : <span>As of Date</span>}
                                </Button>
                            </PopoverTrigger>
                            <PopoverContent className="w-auto p-0">
                                <Calendar
                                    mode="single"
                                    selected={asOfDate}
                                    onSelect={(date) => date && setAsOfDate(date)}
                                    initialFocus
                                />
                            </PopoverContent>
                        </Popover>
                        <Button variant="outline" size="icon">
                            <Download className="h-4 w-4" />
                        </Button>
                    </div>
                </div>
            </CardHeader>
            <CardContent>
                {isLoading ? (
                    <div className="space-y-4">
                        <Skeleton className="h-12 w-full" />
                        <Skeleton className="h-64 w-full" />
                    </div>
                ) : !agingReport ? (
                    <div className="text-center py-12 text-muted-foreground">No data available</div>
                ) : (
                    <div className="space-y-6">
                        {/* Summary Bar */}
                        <div className="grid grid-cols-5 gap-4">
                            {agingReport.buckets.map((bucket, i) => (
                                <Card key={i} className="bg-muted/30">
                                    <CardContent className="p-4 text-center">
                                        <div className="text-sm font-medium text-muted-foreground mb-1">{bucket.bucketName}</div>
                                        <div className="text-xl font-bold">{formatCurrency(bucket.amount)}</div>
                                        <div className="text-xs text-muted-foreground mt-1">{bucket.customerCount} customers</div>
                                    </CardContent>
                                </Card>
                            ))}
                        </div>

                        {/* Detailed Table (Placeholder structure as real detail data might be needed from API) */}
                        <div className="border rounded-md">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead>Bucket</TableHead>
                                        <TableHead className="text-right">Balance</TableHead>
                                        <TableHead className="text-right">Count</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {agingReport.buckets.map((bucket, i) => (
                                        <TableRow key={i}>
                                            <TableCell className="font-medium">{bucket.bucketName}</TableCell>
                                            <TableCell className="text-right">{formatCurrency(bucket.amount)}</TableCell>
                                            <TableCell className="text-right">{bucket.customerCount}</TableCell>
                                        </TableRow>
                                    ))}
                                    <TableRow className="bg-muted/50 font-bold">
                                        <TableCell>Total</TableCell>
                                        <TableCell className="text-right">{formatCurrency(agingReport.totalOutstanding)}</TableCell>
                                        <TableCell className="text-right"></TableCell>
                                    </TableRow>
                                </TableBody>
                            </Table>
                        </div>
                    </div>
                )}
            </CardContent>
        </Card>
    );
}

function CustomerStatementsView() {
    // Placeholder for future implementation
    return (
        <Card>
            <CardHeader>
                <CardTitle>Customer Statements</CardTitle>
                <CardDescription>Generate and download account statements</CardDescription>
            </CardHeader>
            <CardContent className="text-center py-12 text-muted-foreground">
                <FileText className="h-12 w-12 mx-auto mb-4 opacity-20" />
                <p>Select a customer and date range to generate a statement.</p>
                <div className="max-w-md mx-auto mt-4 p-4 bg-yellow-50 dark:bg-yellow-900/10 border border-yellow-200 dark:border-yellow-900 rounded-lg text-sm text-yellow-800 dark:text-yellow-200">
                    Feature coming soon. Please use individual Customer Details page to view history.
                </div>
            </CardContent>
        </Card>
    )
}
