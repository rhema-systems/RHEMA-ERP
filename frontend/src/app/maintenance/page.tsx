'use client';

import React from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { LayoutDashboard, FileText, Package, Users, ClipboardCheck, Calendar, AlertTriangle, Clock, BarChart3 } from 'lucide-react';

const maintenanceModules = [
  {
    title: 'Dashboard',
    description: 'Overview of maintenance operations and key metrics',
    href: '/maintenance/dashboard',
    icon: LayoutDashboard,
    color: 'bg-blue-500',
  },
  {
    title: 'Work Orders',
    description: 'Create, manage, and track maintenance work orders',
    href: '/maintenance/work-orders',
    icon: FileText,
    color: 'bg-green-500',
  },
  {
    title: 'Assets',
    description: 'Manage equipment, machinery, and facility assets',
    href: '/maintenance/assets',
    icon: Package,
    color: 'bg-purple-500',
  },
  {
    title: 'Technicians',
    description: 'Manage technician schedules and assignments',
    href: '/maintenance/technicians',
    icon: Users,
    color: 'bg-orange-500',
  },
  {
    title: 'Quality Control',
    description: 'Quality inspections and control processes',
    href: '/maintenance/quality-control',
    icon: ClipboardCheck,
    color: 'bg-red-500',
  },
  {
    title: 'Scheduled Maintenance',
    description: 'Plan and manage preventive maintenance schedules',
    href: '/maintenance/scheduled',
    icon: Calendar,
    color: 'bg-teal-500',
  },
  {
    title: 'Emergency Maintenance',
    description: 'Handle urgent and emergency maintenance requests',
    href: '/maintenance/emergency',
    icon: AlertTriangle,
    color: 'bg-yellow-500',
  },
  {
    title: 'Maintenance History',
    description: 'View historical maintenance records and logs',
    href: '/maintenance/history',
    icon: Clock,
    color: 'bg-indigo-500',
  },
  {
    title: 'Reports',
    description: 'Generate maintenance reports and analytics',
    href: '/maintenance/reports',
    icon: BarChart3,
    color: 'bg-pink-500',
  },
];

export default function MaintenancePage() {
  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Maintenance Management</h1>
          <p className="text-muted-foreground">
            Manage work orders, assets, technicians, and quality control processes
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
            <BreadcrumbPage>Maintenance</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {maintenanceModules.map((module) => {
          const IconComponent = module.icon;
          return (
            <Card key={module.href} className="hover:shadow-lg transition-shadow">
              <CardHeader className="flex flex-row items-center space-y-0 pb-2">
                <div className={`${module.color} rounded-lg p-3 text-white mr-4`}>
                  <IconComponent className="h-6 w-6" />
                </div>
                <div>
                  <CardTitle className="text-lg">{module.title}</CardTitle>
                </div>
              </CardHeader>
              <CardContent>
                <CardDescription className="mb-4">
                  {module.description}
                </CardDescription>
                <Button asChild className="w-full">
                  <Link href={module.href}>
                    Open {module.title}
                  </Link>
                </Button>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Quick Stats</CardTitle>
          <CardDescription>
            Overview of current maintenance operations
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
            <div className="space-y-2">
              <p className="text-2xl font-bold text-blue-600">0</p>
              <p className="text-sm text-muted-foreground">Active Work Orders</p>
            </div>
            <div className="space-y-2">
              <p className="text-2xl font-bold text-green-600">0</p>
              <p className="text-sm text-muted-foreground">Assets Managed</p>
            </div>
            <div className="space-y-2">
              <p className="text-2xl font-bold text-orange-600">0</p>
              <p className="text-sm text-muted-foreground">Available Technicians</p>
            </div>
            <div className="space-y-2">
              <p className="text-2xl font-bold text-red-600">0</p>
              <p className="text-sm text-muted-foreground">Overdue Tasks</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}