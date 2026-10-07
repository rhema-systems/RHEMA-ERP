'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { FileText, Plus, Loader2, Filter } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { leaseAccountingService, type LeaseContractList, type LeaseStatus } from '@/services/finance/leaseAccountingService';

export default function LeasesPage() {
  const [leases, setLeases] = useState<LeaseContractList[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | LeaseStatus>('all');

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await leaseAccountingService.getAll();
        setLeases(data);
      } catch (error) {
        console.error('Failed to load leases:', error);
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const filtered = useMemo(() => {
    return leases.filter((l) => {
      const matchesSearch = searchTerm === '' ||
        l.contractNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
        l.description.toLowerCase().includes(searchTerm.toLowerCase());
      const matchesStatus = statusFilter === 'all' || l.status === statusFilter;
      return matchesSearch && matchesStatus;
    });
  }, [leases, searchTerm, statusFilter]);

  const formatMoney = (amount: number) =>
    new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(amount || 0);

  const getStatusBadge = (status: LeaseStatus) => {
    const variants: Record<LeaseStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      Draft: 'outline', Active: 'default', Terminated: 'destructive', Completed: 'secondary',
      PendingApproval: 'secondary', Rejected: 'destructive',
    };
    return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
  };

  if (loading) {
    return <div className="flex items-center justify-center min-h-[400px]"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <FileText className="h-8 w-8" />
            Lease Accounting (IFRS 16)
          </h1>
          <p className="text-muted-foreground">Manage lease contracts with Right-of-Use asset recognition.</p>
        </div>
        <Link href="/finance/fixed-assets/leases/new">
          <Button><Plus className="mr-2 h-4 w-4" />New Lease</Button>
        </Link>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance/fixed-assets/dashboard">Fixed Assets</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>Leases</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card>
          <CardHeader><CardTitle className="text-base">Search</CardTitle></CardHeader>
          <CardContent>
            <Input placeholder="Search by contract number or description..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-base flex items-center gap-2"><Filter className="h-4 w-4" />Status</CardTitle></CardHeader>
          <CardContent>
            <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as LeaseStatus | 'all')}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Terminated">Terminated</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
              </SelectContent>
            </Select>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Lease Contracts</CardTitle></CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Contract #</TableHead>
                <TableHead>Description</TableHead>
                <TableHead>Lessor</TableHead>
                <TableHead>Start</TableHead>
                <TableHead>End</TableHead>
                <TableHead className="text-right">Payment</TableHead>
                <TableHead className="text-right">Present Value</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.length === 0 ? (
                <TableRow><TableCell colSpan={8} className="text-center py-8 text-muted-foreground">No leases found.</TableCell></TableRow>
              ) : (
                filtered.map((l) => (
                  <TableRow key={l.id}>
                    <TableCell className="font-mono">
                      <Link href={`/finance/fixed-assets/leases/${l.id}`} className="text-primary hover:underline">{l.contractNumber}</Link>
                    </TableCell>
                    <TableCell className="font-medium">{l.description}</TableCell>
                    <TableCell>{l.lessorName || '—'}</TableCell>
                    <TableCell>{new Date(l.startDate).toLocaleDateString('en-US', { year: 'numeric', month: 'short' })}</TableCell>
                    <TableCell>{new Date(l.endDate).toLocaleDateString('en-US', { year: 'numeric', month: 'short' })}</TableCell>
                    <TableCell className="text-right">{formatMoney(l.monthlyPaymentAmount)}</TableCell>
                    <TableCell className="text-right">{formatMoney(l.presentValue)}</TableCell>
                    <TableCell>{getStatusBadge(l.status)}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
