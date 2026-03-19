'use client';

import React, { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { HardHat, Plus, Loader2, Filter } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { capitalProjectService, type CapitalProjectList, type ProjectStatus } from '@/services/finance/capitalProjectService';

export default function CapitalProjectsPage() {
  const [projects, setProjects] = useState<CapitalProjectList[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | ProjectStatus>('all');

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const data = await capitalProjectService.getAll();
        setProjects(data);
      } catch (error) {
        console.error('Failed to load capital projects:', error);
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const filtered = useMemo(() => {
    return projects.filter((p) => {
      const matchesSearch = searchTerm === '' ||
        p.projectCode.toLowerCase().includes(searchTerm.toLowerCase()) ||
        p.name.toLowerCase().includes(searchTerm.toLowerCase());
      const matchesStatus = statusFilter === 'all' || p.status === statusFilter;
      return matchesSearch && matchesStatus;
    });
  }, [projects, searchTerm, statusFilter]);

  const formatMoney = (amount: number) =>
    new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(amount || 0);

  const getStatusBadge = (status: ProjectStatus) => {
    const variants: Record<ProjectStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      Planning: 'outline',
      InProgress: 'default',
      OnHold: 'secondary',
      Completed: 'default',
      Cancelled: 'destructive',
    };
    const labels: Record<ProjectStatus, string> = {
      Planning: 'Planning',
      InProgress: 'In Progress',
      OnHold: 'On Hold',
      Completed: 'Completed',
      Cancelled: 'Cancelled',
    };
    return <Badge variant={variants[status] || 'default'}>{labels[status] || status}</Badge>;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <HardHat className="h-8 w-8" />
            Capital Projects (AUC)
          </h1>
          <p className="text-muted-foreground">Track assets under construction and capitalize on completion.</p>
        </div>
        <Link href="/finance/fixed-assets/capital-projects/new">
          <Button>
            <Plus className="mr-2 h-4 w-4" />
            New Project
          </Button>
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
          <BreadcrumbItem><BreadcrumbPage>Capital Projects</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card>
          <CardHeader><CardTitle className="text-base">Search</CardTitle></CardHeader>
          <CardContent>
            <Input placeholder="Search by code or name..." value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-base flex items-center gap-2"><Filter className="h-4 w-4" />Status</CardTitle></CardHeader>
          <CardContent>
            <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as ProjectStatus | 'all')}>
              <SelectTrigger><SelectValue placeholder="All Statuses" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Planning">Planning</SelectItem>
                <SelectItem value="InProgress">In Progress</SelectItem>
                <SelectItem value="OnHold">On Hold</SelectItem>
                <SelectItem value="Completed">Completed</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Projects</CardTitle></CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Start Date</TableHead>
                <TableHead className="text-right">Budget</TableHead>
                <TableHead className="text-right">Accumulated Cost</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-8 text-muted-foreground">
                    No capital projects found.
                  </TableCell>
                </TableRow>
              ) : (
                filtered.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell className="font-mono">
                      <Link href={`/finance/fixed-assets/capital-projects/${p.id}`} className="text-primary hover:underline">{p.projectCode}</Link>
                    </TableCell>
                    <TableCell className="font-medium">{p.name}</TableCell>
                    <TableCell>{new Date(p.startDate).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })}</TableCell>
                    <TableCell className="text-right">{formatMoney(p.totalBudgetAmount)}</TableCell>
                    <TableCell className="text-right">{formatMoney(p.totalAccumulatedCost)}</TableCell>
                    <TableCell>{getStatusBadge(p.status)}</TableCell>
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
