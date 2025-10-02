'use client';

import React, { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui/select';
import { Input } from '../ui/input';
import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Download,
  Search,
  Loader2,
  Filter,
  X,
} from 'lucide-react';
import { reportsService, ReportResult, ReportColumn } from '../../services/reports';
import { useToast } from '../../hooks/use-toast';
import { useIsClient } from '../../lib/ssr-utils';

interface ReportResultsDialogProps {
  reportId: string | null;
  reportName?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

interface PaginatedReportResult {
  reportId: string;
  reportName: string;
  executedAt: string;
  executionTime: string;
  totalRows: number;
  data: Record<string, any>[];
  columns: ReportColumn[];
  pagination: {
    currentPage: number;
    pageSize: number;
    totalPages: number;
    hasNext: boolean;
    hasPrevious: boolean;
  };
  metadata?: {
    parameters?: Record<string, any>;
    query?: string;
    dataAsOf?: string;
    dataSource?: string;
    statistics?: Record<string, any>;
  };
}

export default function ReportResultsDialog({
  reportId,
  reportName,
  open,
  onOpenChange,
}: ReportResultsDialogProps) {
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortColumn, setSortColumn] = useState<string | null>(null);
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');
  const [isExporting, setIsExporting] = useState(false);
  
  const { toast } = useToast();
  const isClient = useIsClient();

  // Fetch paginated results
  const {
    data: results,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['reportResults', reportId, currentPage, pageSize],
    queryFn: async () => {
      if (!reportId) return null;
      
      console.log('🔍 ReportResultsDialog: Fetching results for reportId:', reportId);
      
      // Execute the report to get fresh data
      const fullResult = await reportsService.executeReport(reportId, {
        parameters: {},
        maxRows: 1000, // Request up to 1000 rows to get all 150
        includeMetadata: true,
      });
      
      console.log('📊 ReportResultsDialog: Received data:', fullResult);
      console.log('📊 Total rows:', fullResult.totalRows);
      console.log('📊 Actual data rows received:', fullResult.data.length);
      console.log('📊 Columns:', fullResult.columns);
      console.log('📊 Sample data (first 3 rows):', fullResult.data.slice(0, 3));
      console.log('📊 All data received:', fullResult.data);

      // Handle pagination client-side for now
      // In production, you'd want the backend to handle pagination
      const startIndex = (currentPage - 1) * pageSize;
      const endIndex = startIndex + pageSize;
      const paginatedData = fullResult.data.slice(startIndex, endIndex);
      
      return {
        ...fullResult,
        data: paginatedData,
        pagination: {
          currentPage,
          pageSize,
          totalPages: Math.ceil(fullResult.totalRows / pageSize),
          hasNext: endIndex < fullResult.totalRows,
          hasPrevious: currentPage > 1,
        },
      } as PaginatedReportResult;
    },
    enabled: open && !!reportId,
    refetchOnWindowFocus: false,
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });

  // Filter and sort data client-side for better UX
  const processedData = useMemo(() => {
    if (!results?.data) return [];

    let filtered = results.data;

    // Apply search filter
    if (searchQuery) {
      filtered = filtered.filter(row =>
        Object.values(row).some(value =>
          String(value).toLowerCase().includes(searchQuery.toLowerCase())
        )
      );
    }

    // Apply sorting
    if (sortColumn) {
      filtered = [...filtered].sort((a, b) => {
        const aVal = a[sortColumn];
        const bVal = b[sortColumn];
        
        if (aVal === bVal) return 0;
        
        const comparison = aVal < bVal ? -1 : 1;
        return sortDirection === 'asc' ? comparison : -comparison;
      });
    }

    return filtered;
  }, [results?.data, searchQuery, sortColumn, sortDirection]);

  const handleSort = (column: string) => {
    if (sortColumn === column) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(column);
      setSortDirection('asc');
    }
  };

  const handleExport = async (format: 'pdf' | 'csv' | 'xlsx' | 'json') => {
    if (!reportId) return;
    
    setIsExporting(true);
    try {
      const blob = await reportsService.exportReport(reportId, { format });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = url;
      a.download = `${reportName || 'report'}-results.${format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      
      toast({
        title: 'Export Successful',
        description: `Report exported as ${format.toUpperCase()}`,
      });
    } catch (error: any) {
      toast({
        title: 'Export Failed',
        description: error.response?.data?.message || 'Failed to export report',
        variant: 'destructive',
      });
    } finally {
      setIsExporting(false);
    }
  };

  const resetFilters = () => {
    setSearchQuery('');
    setSortColumn(null);
    setSortDirection('asc');
    setCurrentPage(1);
  };

  if (!open || !reportId) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-7xl max-h-[90vh] overflow-hidden">
        <DialogHeader>
          <DialogTitle className="flex items-center justify-between">
            <div>
              <span>Report Results: {reportName || 'Unnamed Report'}</span>
              {results && (
                <Badge variant="secondary" className="ml-2">
                  {results.totalRows} rows
                </Badge>
              )}
            </div>
            <div className="flex items-center space-x-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => handleExport('csv')}
                disabled={isExporting}
              >
                {isExporting ? (
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                ) : (
                  <Download className="h-4 w-4 mr-2" />
                )}
                Export CSV
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => handleExport('xlsx')}
                disabled={isExporting}
              >
                Export Excel
              </Button>
            </div>
          </DialogTitle>
          <DialogDescription>
            {results && isClient && (
              <span>
                Executed on {new Date(results.executedAt).toLocaleString()} • 
                Duration: {results.executionTime}
              </span>
            )}
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col space-y-4 h-full">
          {/* Search and Filter Controls */}
          <div className="flex items-center space-x-4">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground h-4 w-4" />
              <Input
                placeholder="Search in results..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-10"
              />
            </div>
            <Select value={pageSize.toString()} onValueChange={(value) => setPageSize(Number(value))}>
              <SelectTrigger className="w-24">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="25">25</SelectItem>
                <SelectItem value="50">50</SelectItem>
                <SelectItem value="100">100</SelectItem>
                <SelectItem value="200">200</SelectItem>
              </SelectContent>
            </Select>
            <Button
              variant="outline"
              size="sm"
              onClick={resetFilters}
              disabled={!searchQuery && !sortColumn}
            >
              <X className="h-4 w-4" />
              Clear
            </Button>
          </div>

          {/* Results Table */}
          <div className="flex-1 overflow-auto border rounded-md">
            {isLoading ? (
              <div className="flex items-center justify-center h-64">
                <Loader2 className="h-8 w-8 animate-spin" />
              </div>
            ) : error ? (
              <div className="flex items-center justify-center h-64 text-red-600">
                <p>Failed to load results. Please try again.</p>
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    {results?.columns.map((column) => (
                      <TableHead
                        key={column.name}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => handleSort(column.name)}
                      >
                        <div className="flex items-center">
                          {column.displayName || column.name}
                          {sortColumn === column.name && (
                            <span className="ml-1">
                              {sortDirection === 'asc' ? '↑' : '↓'}
                            </span>
                          )}
                        </div>
                      </TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {processedData.map((row, index) => (
                    <TableRow key={index}>
                      {results?.columns.map((column) => (
                        <TableCell key={column.name}>
                          {formatCellValue(row[column.name], column)}
                        </TableCell>
                      ))}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </div>

          {/* Pagination Controls */}
          {results && (
            <div className="flex items-center justify-between">
              <div className="text-sm text-muted-foreground">
                Showing {((currentPage - 1) * pageSize) + 1} to{' '}
                {Math.min(currentPage * pageSize, results.totalRows)} of{' '}
                {results.totalRows} results
              </div>
              
              <div className="flex items-center space-x-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setCurrentPage(1)}
                  disabled={!results.pagination.hasPrevious}
                >
                  <ChevronsLeft className="h-4 w-4" />
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setCurrentPage(prev => prev - 1)}
                  disabled={!results.pagination.hasPrevious}
                >
                  <ChevronLeft className="h-4 w-4" />
                </Button>
                
                <span className="text-sm">
                  Page {currentPage} of {results.pagination.totalPages}
                </span>
                
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setCurrentPage(prev => prev + 1)}
                  disabled={!results.pagination.hasNext}
                >
                  <ChevronRight className="h-4 w-4" />
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setCurrentPage(results.pagination.totalPages)}
                  disabled={!results.pagination.hasNext}
                >
                  <ChevronsRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function formatCellValue(value: any, column: ReportColumn): React.ReactNode {
  if (value === null || value === undefined) {
    return <span className="text-muted-foreground">—</span>;
  }

  // Format based on data type
  switch (column.dataType?.toLowerCase()) {
    case 'datetime':
    case 'date':
      return new Date(value).toLocaleString();
    case 'number':
    case 'decimal':
    case 'float':
      return typeof value === 'number' ? value.toLocaleString() : value;
    case 'currency':
      return typeof value === 'number' ? 
        new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value) : 
        value;
    case 'boolean':
      return value ? '✓' : '✗';
    default:
      return String(value);
  }
}
