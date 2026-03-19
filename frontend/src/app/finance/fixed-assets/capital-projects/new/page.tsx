'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { HardHat, ArrowLeft, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { capitalProjectService, type CreateCapitalProjectDto } from '@/services/finance/capitalProjectService';

export default function NewCapitalProjectPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<CreateCapitalProjectDto>({
    projectCode: '',
    name: '',
    description: '',
    startDate: new Date().toISOString().split('T')[0],
    targetCompletionDate: '',
    totalBudgetAmount: 0,
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setSaving(true);
      const result = await capitalProjectService.create(form);
      router.push(`/finance/fixed-assets/capital-projects/${result.id}`);
    } catch (error: unknown) {
      alert(error instanceof Error ? error.message : 'Failed to create project');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.push('/finance/fixed-assets/capital-projects')}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <HardHat className="h-8 w-8" />
            New Capital Project
          </h1>
          <p className="text-muted-foreground">Create a new asset under construction (AUC) project.</p>
        </div>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance/fixed-assets/capital-projects">Capital Projects</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>New</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <form onSubmit={handleSubmit}>
        <Card>
          <CardHeader><CardTitle>Project Details</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label htmlFor="projectCode">Project Code *</Label>
                <Input id="projectCode" required value={form.projectCode} onChange={(e) => setForm({ ...form, projectCode: e.target.value })} placeholder="e.g. AUC-2026-001" />
              </div>
              <div>
                <Label htmlFor="name">Name *</Label>
                <Input id="name" required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="e.g. New Office Building" />
              </div>
            </div>
            <div>
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" value={form.description || ''} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Detailed description of the project..." />
            </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <Label htmlFor="startDate">Start Date *</Label>
                <Input id="startDate" type="date" required value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
              </div>
              <div>
                <Label htmlFor="targetDate">Target Completion Date</Label>
                <Input id="targetDate" type="date" value={form.targetCompletionDate || ''} onChange={(e) => setForm({ ...form, targetCompletionDate: e.target.value || undefined })} />
              </div>
              <div>
                <Label htmlFor="budget">Total Budget *</Label>
                <Input id="budget" type="number" required min={0} step={0.01} value={form.totalBudgetAmount} onChange={(e) => setForm({ ...form, totalBudgetAmount: parseFloat(e.target.value) || 0 })} />
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="flex justify-end mt-4">
          <Button type="submit" disabled={saving} size="lg">
            <Save className="mr-2 h-4 w-4" />
            {saving ? 'Creating...' : 'Create Project'}
          </Button>
        </div>
      </form>
    </div>
  );
}
