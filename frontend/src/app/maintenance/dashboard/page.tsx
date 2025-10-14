'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Progress } from '@/components/ui/progress';
import { 
  BarChart, 
  Bar, 
  XAxis, 
  YAxis, 
  CartesianGrid, 
  Tooltip, 
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  LineChart,
  Line
} from 'recharts';
import { 
  FileText, 
  Package, 
  Users, 
  Clock, 
  CheckCircle, 
  AlertTriangle, 
  TrendingUp, 
  Calendar,
  Wrench,
  ClipboardCheck
} from 'lucide-react';

const workOrderStatusData = [
  { name: 'Open', value: 12, color: '#3b82f6' },
  { name: 'In Progress', value: 8, color: '#f59e0b' },
  { name: 'Completed', value: 45, color: '#10b981' },
  { name: 'On Hold', value: 3, color: '#6b7280' },
];

const monthlyMaintenanceData = [
  { month: 'Jan', planned: 20, emergency: 5, completed: 18 },
  { month: 'Feb', planned: 18, emergency: 3, completed: 19 },
  { month: 'Mar', planned: 22, emergency: 7, completed: 21 },
  { month: 'Apr', planned: 25, emergency: 4, completed: 24 },
  { month: 'May', planned: 28, emergency: 6, completed: 26 },
  { month: 'Jun', planned: 30, emergency: 8, completed: 29 },
];

const assetHealthData = [
  { category: 'HVAC', good: 15, fair: 8, poor: 2 },
  { category: 'Electrical', good: 22, fair: 5, poor: 1 },
  { category: 'Plumbing', good: 18, fair: 6, poor: 3 },
  { category: 'Safety', good: 12, fair: 4, poor: 1 },
];

const upcomingMaintenance = [
  { id: 1, asset: 'HVAC Unit A1', type: 'Preventive', dueDate: '2024-01-20', technician: 'John Smith' },
  { id: 2, asset: 'Generator B2', type: 'Inspection', dueDate: '2024-01-22', technician: 'Mike Johnson' },
  { id: 3, asset: 'Elevator C1', type: 'Preventive', dueDate: '2024-01-25', technician: 'Sarah Davis' },
  { id: 4, asset: 'Fire System D1', type: 'Safety Check', dueDate: '2024-01-28', technician: 'Tom Wilson' },
];

const recentWorkOrders = [
  { id: 1, title: 'HVAC Filter Replacement', asset: 'Building A - Unit 1', status: 'Completed', priority: 'Medium' },
  { id: 2, title: 'Emergency Plumbing', asset: 'Building B - Basement', status: 'In Progress', priority: 'Critical' },
  { id: 3, title: 'Electrical Inspection', asset: 'Building C - Panel 3', status: 'Open', priority: 'High' },
  { id: 4, title: 'Generator Maintenance', asset: 'Backup Generator 1', status: 'Completed', priority: 'Medium' },
];

export default function MaintenanceDashboard() {
  const [totalWorkOrders] = useState(68);
  const [activeWorkOrders] = useState(23);
  const [totalAssets] = useState(156);
  const [availableTechnicians] = useState(12);
  const [overdueItems] = useState(5);

  const getStatusBadge = (status: string) => {
    const colors = {
      'Open': 'bg-blue-100 text-blue-800',
      'In Progress': 'bg-yellow-100 text-yellow-800',
      'Completed': 'bg-green-100 text-green-800',
      'On Hold': 'bg-gray-100 text-gray-800',
    } as any;

    return (
      <Badge className={colors[status] || 'bg-gray-100 text-gray-800'}>
        {status}
      </Badge>
    );
  };

  const getPriorityBadge = (priority: string) => {
    const colors = {
      'Low': 'bg-green-100 text-green-800',
      'Medium': 'bg-blue-100 text-blue-800',
      'High': 'bg-orange-100 text-orange-800',
      'Critical': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>
        {priority}
      </Badge>
    );
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Maintenance Dashboard</h1>
          <p className="text-muted-foreground">
            Overview of maintenance operations, performance metrics, and key indicators
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
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Dashboard</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Key Performance Indicators */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Work Orders</CardTitle>
            <FileText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalWorkOrders}</div>
            <p className="text-xs text-muted-foreground">
              +12% from last month
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Orders</CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{activeWorkOrders}</div>
            <p className="text-xs text-muted-foreground">
              Currently in progress
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Assets</CardTitle>
            <Package className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalAssets}</div>
            <p className="text-xs text-muted-foreground">
              Under maintenance management
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Available Technicians</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{availableTechnicians}</div>
            <p className="text-xs text-muted-foreground">
              Ready for assignments
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Overdue Items</CardTitle>
            <AlertTriangle className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{overdueItems}</div>
            <p className="text-xs text-muted-foreground">
              Requires immediate attention
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Work Order Status Distribution */}
        <Card>
          <CardHeader>
            <CardTitle>Work Order Status Distribution</CardTitle>
            <CardDescription>Current status of all work orders</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={workOrderStatusData}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={100}
                  paddingAngle={2}
                  dataKey="value"
                >
                  {workOrderStatusData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
            <div className="flex flex-wrap justify-center gap-4 mt-4">
              {workOrderStatusData.map((item) => (
                <div key={item.name} className="flex items-center space-x-2">
                  <div
                    className="w-3 h-3 rounded-full"
                    style={{ backgroundColor: item.color }}
                  />
                  <span className="text-sm">{item.name} ({item.value})</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* Monthly Maintenance Trend */}
        <Card>
          <CardHeader>
            <CardTitle>Monthly Maintenance Trend</CardTitle>
            <CardDescription>Planned vs completed maintenance over time</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={monthlyMaintenanceData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" />
                <YAxis />
                <Tooltip />
                <Line type="monotone" dataKey="planned" stroke="#3b82f6" strokeWidth={2} name="Planned" />
                <Line type="monotone" dataKey="completed" stroke="#10b981" strokeWidth={2} name="Completed" />
                <Line type="monotone" dataKey="emergency" stroke="#ef4444" strokeWidth={2} name="Emergency" />
              </LineChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      {/* Asset Health Overview */}
      <Card>
        <CardHeader>
          <CardTitle>Asset Health Overview</CardTitle>
          <CardDescription>Health status of assets by category</CardDescription>
        </CardHeader>
        <CardContent>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={assetHealthData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="category" />
              <YAxis />
              <Tooltip />
              <Bar dataKey="good" fill="#10b981" name="Good" />
              <Bar dataKey="fair" fill="#f59e0b" name="Fair" />
              <Bar dataKey="poor" fill="#ef4444" name="Poor" />
            </BarChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      {/* Recent Activity and Upcoming Tasks */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Recent Work Orders */}
        <Card>
          <CardHeader>
            <CardTitle>Recent Work Orders</CardTitle>
            <CardDescription>Latest maintenance activities</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {recentWorkOrders.map((order) => (
                <div key={order.id} className="flex items-center justify-between p-3 border rounded-lg">
                  <div className="space-y-1">
                    <p className="font-medium text-sm">{order.title}</p>
                    <p className="text-xs text-muted-foreground">{order.asset}</p>
                  </div>
                  <div className="flex items-center space-x-2">
                    {getPriorityBadge(order.priority)}
                    {getStatusBadge(order.status)}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* Upcoming Maintenance */}
        <Card>
          <CardHeader>
            <CardTitle>Upcoming Maintenance</CardTitle>
            <CardDescription>Scheduled maintenance tasks</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {upcomingMaintenance.map((task) => (
                <div key={task.id} className="flex items-center justify-between p-3 border rounded-lg">
                  <div className="space-y-1">
                    <p className="font-medium text-sm">{task.asset}</p>
                    <p className="text-xs text-muted-foreground">{task.type} - {task.technician}</p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm font-medium">{new Date(task.dueDate).toLocaleDateString()}</p>
                    <Badge variant="outline" className="text-xs">
                      <Calendar className="w-3 h-3 mr-1" />
                      Due
                    </Badge>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Performance Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Work Order Completion</CardTitle>
            <CardDescription>This month's completion rate</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Completed</span>
                <span className="text-sm text-muted-foreground">45/52</span>
              </div>
              <Progress value={86.5} className="w-full" />
              <p className="text-xs text-muted-foreground">86.5% completion rate</p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Average Response Time</CardTitle>
            <CardDescription>Time to start work orders</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="text-2xl font-bold">2.4 hrs</div>
              <div className="flex items-center space-x-1">
                <TrendingUp className="w-4 h-4 text-green-600" />
                <span className="text-sm text-green-600">15% faster</span>
              </div>
              <p className="text-xs text-muted-foreground">Than previous month</p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Quality Score</CardTitle>
            <CardDescription>Post-maintenance quality ratings</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="text-2xl font-bold">4.8/5.0</div>
              <div className="flex items-center space-x-1">
                <CheckCircle className="w-4 h-4 text-green-600" />
                <span className="text-sm text-green-600">Excellent</span>
              </div>
              <p className="text-xs text-muted-foreground">Based on 156 reviews</p>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}