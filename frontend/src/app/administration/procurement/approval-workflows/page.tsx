'use client';

import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { GitBranch, CheckCircle, Clock, AlertCircle } from 'lucide-react';

export default function ApprovalWorkflowsPage() {
  // Placeholder workflow data
  const workflows = [
    {
      id: '1',
      name: 'Business Partner Registration Approval',
      description: 'Workflow for approving new business partner registrations',
      steps: [
        { name: 'Submit Registration', status: 'Active' },
        { name: 'Initial Review', status: 'Active' },
        { name: 'Document Verification', status: 'Active' },
        { name: 'Final Approval', status: 'Active' }
      ],
      isActive: true,
      averageCompletionDays: 5
    },
    {
      id: '2',
      name: 'License Verification Workflow',
      description: 'Workflow for verifying partner licenses and certifications',
      steps: [
        { name: 'License Submission', status: 'Active' },
        { name: 'Document Check', status: 'Active' },
        { name: 'Authority Verification', status: 'Active' },
        { name: 'Approval', status: 'Active' }
      ],
      isActive: true,
      averageCompletionDays: 3
    },
    {
      id: '3',
      name: 'Partner Status Change Approval',
      description: 'Workflow for approving changes to partner status (suspension, reactivation)',
      steps: [
        { name: 'Request Submission', status: 'Active' },
        { name: 'Review', status: 'Active' },
        { name: 'Approval', status: 'Active' }
      ],
      isActive: true,
      averageCompletionDays: 2
    }
  ];

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Approval Workflows</h1>
          <p className="text-muted-foreground">
            Manage approval workflows for business partner processes
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
            <BreadcrumbLink href="/administration">Administration</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Approval Workflows</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{workflows.length}</p>
                <p className="text-sm text-muted-foreground">Total Workflows</p>
              </div>
              <GitBranch className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {workflows.filter(w => w.isActive).length}
                </p>
                <p className="text-sm text-muted-foreground">Active</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {Math.round(workflows.reduce((sum, w) => sum + w.averageCompletionDays, 0) / workflows.length)}
                </p>
                <p className="text-sm text-muted-foreground">Avg. Days</p>
              </div>
              <Clock className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">
                  {workflows.reduce((sum, w) => sum + w.steps.length, 0)}
                </p>
                <p className="text-sm text-muted-foreground">Total Steps</p>
              </div>
              <AlertCircle className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Workflows List */}
      <div className="space-y-4">
        {workflows.map((workflow) => (
          <Card key={workflow.id}>
            <CardHeader>
              <div className="flex items-start justify-between">
                <div className="space-y-1">
                  <div className="flex items-center space-x-3">
                    <CardTitle>{workflow.name}</CardTitle>
                    <Badge className={workflow.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                      {workflow.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                  </div>
                  <CardDescription>{workflow.description}</CardDescription>
                </div>
                <div className="text-right text-sm text-muted-foreground">
                  <div>Avg. Completion</div>
                  <div className="text-lg font-semibold text-foreground">{workflow.averageCompletionDays} days</div>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                <div className="text-sm font-medium">Workflow Steps:</div>
                <div className="flex items-center space-x-2">
                  {workflow.steps.map((step, index) => (
                    <React.Fragment key={index}>
                      <div className="flex-1">
                        <div className="border rounded-lg p-3 bg-muted/30">
                          <div className="font-medium text-sm">{step.name}</div>
                          <Badge variant="outline" className="mt-1 text-xs">{step.status}</Badge>
                        </div>
                      </div>
                      {index < workflow.steps.length - 1 && (
                        <div className="text-muted-foreground">→</div>
                      )}
                    </React.Fragment>
                  ))}
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Info Message */}
      <Card className="border-blue-200 bg-blue-50">
        <CardContent className="pt-6">
          <div className="flex items-start space-x-3">
            <AlertCircle className="h-5 w-5 text-blue-600 mt-0.5" />
            <div className="space-y-1">
              <p className="text-sm font-medium text-blue-900">
                Workflow Configuration
              </p>
              <p className="text-sm text-blue-700">
                This page displays the current approval workflows for business partner management. 
                Advanced workflow configuration features will be available in a future update.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

