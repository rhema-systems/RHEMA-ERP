'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { FileText, Search, Eye, RefreshCw, Download, FileSignature } from 'lucide-react';
import { toast } from 'sonner';
import { contractService, type ContractListDto } from '@/services/contractService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';

export default function ContractsPage() {
  const router = useRouter();
  const [contracts, setContracts] = useState<ContractListDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [typeFilter, setTypeFilter] = useState<string>('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  useEffect(() => {
    loadContracts();
  }, [page, statusFilter, typeFilter]);

  const loadContracts = async () => {
    try {
      setLoading(true);
      const data = await contractService.getContracts(page, pageSize, searchTerm, statusFilter, typeFilter);
      setContracts(data.items);
      setTotalCount(data.totalCount);
    } catch (error) {
      console.error('Error loading contracts:', error);
      toast.error('Failed to load contracts');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadContracts();
  };

  const handleViewContract = (contractId: string) => {
    router.push(`/procurement/contracts/${contractId}`);
  };

  const getStatusBadge = (status: string) => {
    const config: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
      'PendingSignature': { variant: 'secondary', className: 'bg-yellow-100 text-yellow-800' },
      'Active': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Completed': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Terminated': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Suspended': { variant: 'secondary', className: 'bg-orange-100 text-orange-800' },
      'Expired': { variant: 'secondary', className: 'bg-gray-100 text-gray-600' },
    };
    const c = config[status] || { variant: 'outline' as const, className: '' };
    return <Badge variant={c.variant} className={c.className}>{status}</Badge>;
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    try { return format(new Date(dateString), 'dd MMM yyyy'); } catch { return dateString; }
  };

  const formatCurrency = (amount: number, currency: string) => {
    return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  };

  const handleExport = () => {
    if (contracts.length === 0) { toast.error('No data to export'); return; }
    const exportData = contracts.map(c => ({
      'Contract Number': c.contractNumber,
      'Title': c.contractTitle,
      'Type': c.contractType,
      'Status': c.status,
      'Business Partner': c.businessPartnerName,
      'Tender Number': c.tenderNumber,
      'Value': c.contractValue,
      'Currency': c.currency,
      'Start Date': c.startDate ? format(new Date(c.startDate), 'yyyy-MM-dd') : '',
      'End Date': c.endDate ? format(new Date(c.endDate), 'yyyy-MM-dd') : '',
    }));
    const ws = XLSX.utils.json_to_sheet(exportData);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Contracts');
    XLSX.writeFile(wb, `contracts_${new Date().toISOString().split('T')[0]}.xlsx`);
    toast.success('Exported to Excel');
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  const stats = {
    total: totalCount,
    active: contracts.filter(c => c.status === 'Active').length,
    draft: contracts.filter(c => c.status === 'Draft').length,
    totalValue: contracts.reduce((sum, c) => sum + c.contractValue, 0),
  };

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <FileSignature className="h-8 w-8 text-blue-600" />
            Contracts
          </h1>
          <p className="text-gray-500">Manage procurement contracts</p>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Contracts</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{stats.total}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{stats.active}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Draft</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-gray-600">{stats.draft}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{stats.totalValue.toLocaleString()}</p></CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader><CardTitle>Filter Contracts</CardTitle></CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input placeholder="Search..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()} />
              <Button onClick={handleSearch}><Search className="h-4 w-4" /></Button>
            </div>
            <Select value={statusFilter || 'all'} onValueChange={(v) => setStatusFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="PendingSignature">Pending Signature</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Terminated">Terminated</SelectItem>
              </SelectContent>
            </Select>
            <Select value={typeFilter || 'all'} onValueChange={(v) => setTypeFilter(v === 'all' ? '' : v)}>
              <SelectTrigger><SelectValue placeholder="All Types" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="Service">Service</SelectItem>
                <SelectItem value="Supply">Supply</SelectItem>
                <SelectItem value="Works">Works</SelectItem>
                <SelectItem value="Consulting">Consulting</SelectItem>
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button variant="outline" onClick={handleExport}><Download className="h-4 w-4 mr-2" />Export</Button>
              <Button variant="outline" onClick={loadContracts}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Contracts Table */}
      <Card>
        <CardHeader>
          <CardTitle>Contracts List</CardTitle>
          <CardDescription>Showing {contracts.length} of {totalCount} contracts</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">
              <FileSignature className="h-12 w-12 animate-pulse mx-auto mb-4 text-blue-500" />
              <p className="text-gray-500">Loading contracts...</p>
            </div>
          ) : contracts.length === 0 ? (
            <div className="text-center py-8">
              <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No contracts found</p>
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Contract #</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Business Partner</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Value</TableHead>
                    <TableHead>Period</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {contracts.map((contract) => (
                    <TableRow key={contract.id}>
                      <TableCell className="font-mono text-sm text-blue-600">{contract.contractNumber}</TableCell>
                      <TableCell>
                        <div>
                          <p className="font-medium">{contract.contractTitle}</p>
                          <p className="text-xs text-gray-500">Tender: {contract.tenderNumber}</p>
                        </div>
                      </TableCell>
                      <TableCell>{contract.businessPartnerName}</TableCell>
                      <TableCell><Badge variant="outline">{contract.contractType}</Badge></TableCell>
                      <TableCell className="font-semibold">{formatCurrency(contract.contractValue, contract.currency)}</TableCell>
                      <TableCell>
                        <div className="text-sm">
                          <p>{formatDate(contract.startDate)}</p>
                          <p className="text-gray-500">to {formatDate(contract.endDate)}</p>
                        </div>
                      </TableCell>
                      <TableCell>{getStatusBadge(contract.status)}</TableCell>
                      <TableCell>
                        <Button variant="outline" size="sm" onClick={() => handleViewContract(contract.id)}>
                          <Eye className="h-4 w-4 mr-1" />View
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {totalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <p className="text-sm text-gray-600">Page {page} of {totalPages}</p>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1}>Previous</Button>
                    <Button variant="outline" size="sm" onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages}>Next</Button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

