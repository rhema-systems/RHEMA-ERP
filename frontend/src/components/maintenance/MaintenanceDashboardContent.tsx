'use client';

import React, { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
  Line,
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
} from 'lucide-react';
import { apiService } from '@/services/api.service';

interface DashboardData {
  workOrderStatusData: Array<{ name: string; value: number; color: string }>;
  monthlyMaintenanceData: Array<{ month: string; planned: number; emergency: number; completed: number }>;
  assetHealthData: Array<{ category: string; good: number; fair: number; poor: number }>;
  upcomingMaintenance: Array<{ id: string; asset: string; type: string; dueDate: string; technician: string }>;
  recentWorkOrders: Array<{ id: string; title: string; asset: string; status: string; priority: string }>;
}

interface MaintenanceDashboardOverview {
  summary?: {
    totalAssets?: number;
    totalWorkOrders?: number;
    activeWorkOrders?: number;
    overdueWorkOrders?: number;
    availableTechnicians?: number;
    completedWorkOrders?: number;
  };
  workOrdersByStatus?: Record<string, number>;
}

interface AssetHealthSummaryDto {
  totalAssets: number;
  healthyAssets: number;
  warningAssets: number;
  criticalAssets: number;
  offlineAssets: number;
}

interface TrendDataPointDto {
  date: string;
  value: number;
  metricType: string;
}

interface WorkOrderTrendsDto {
  startDate: string;
  endDate: string;
  creationTrend: TrendDataPointDto[];
  completionTrend: TrendDataPointDto[];
  costTrend: TrendDataPointDto[];
}

interface MaintenanceHistoryItemDto {
  id: string;
  title: string;
  assetName: string;
  status: string;
  priority: string;
}

interface MaintenanceScheduleDto {
  id: string;
  assetName: string;
  maintenanceTypeName?: string;
  maintenanceType?: string;
  name?: string;
  nextDue?: string;
  nextDueDate?: string;
  nextScheduledDate?: string;
  assignedTechnicianName?: string;
}

export function MaintenanceDashboardContent() {
  const [loading, setLoading] = useState(true);
  const [dashboardData, setDashboardData] = useState<DashboardData>({
    workOrderStatusData: [],
    monthlyMaintenanceData: [],
    assetHealthData: [],
    upcomingMaintenance: [],
    recentWorkOrders: [],
  });
  const [totalWorkOrders, setTotalWorkOrders] = useState(0);
  const [activeWorkOrders, setActiveWorkOrders] = useState(0);
  const [totalAssets, setTotalAssets] = useState(0);
  const [availableTechnicians, setAvailableTechnicians] = useState(0);
  const [overdueItems, setOverdueItems] = useState(0);
  const [completedWorkOrders, setCompletedWorkOrders] = useState(0);

  useEffect(() => {
    const loadDashboardData = async () => {
      setLoading(true);
      try {
        const endDate = new Date();
        const startDate = new Date();
        startDate.setMonth(endDate.getMonth() - 5);

        const [overview, assetHealth, trends, history, schedules] = await Promise.all([
          apiService.get<MaintenanceDashboardOverview>('/maintenance/dashboard/overview'),
          apiService.get<AssetHealthSummaryDto>('/maintenance/dashboard/asset-health'),
          apiService.get<WorkOrderTrendsDto>(
            `/maintenance/dashboard/work-order-trends?startDate=${startDate.toISOString()}&endDate=${endDate.toISOString()}`,
          ),
          apiService.get<MaintenanceHistoryItemDto[]>(`/maintenance/history?endDate=${endDate.toISOString()}`),
          apiService.get<MaintenanceScheduleDto[]>('/maintenance/schedules/due-in-days/14'),
        ]);

        const summary = overview?.summary || {};
        setTotalWorkOrders(summary.totalWorkOrders ?? 0);
        setActiveWorkOrders(summary.activeWorkOrders ?? 0);
        setTotalAssets(summary.totalAssets ?? 0);
        setAvailableTechnicians(summary.availableTechnicians ?? 0);
        setOverdueItems(summary.overdueWorkOrders ?? 0);
        setCompletedWorkOrders(summary.completedWorkOrders ?? 0);

        const statusColors: Record<string, string> = {
          Draft: '#9ca3af',
          Open: '#3b82f6',
          'In Progress': '#10b981',
          'On Hold': '#f59e0b',
          Completed: '#22c55e',
          Cancelled: '#6b7280',
          Emergency: '#ef4444',
        };
        const fallbackColors = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#6366f1'];
        const workOrderStatusData = Object.entries(overview.workOrdersByStatus || {}).map(([name, value], index) => ({
          name,
          value,
          color: statusColors[name] || fallbackColors[index % fallbackColors.length],
        }));

        const assetHealthData = assetHealth
          ? [
              {
                category: 'All Assets',
                good: assetHealth.healthyAssets ?? 0,
                fair: assetHealth.warningAssets ?? 0,
                poor: (assetHealth.criticalAssets ?? 0) + (assetHealth.offlineAssets ?? 0),
              },
            ]
          : [];

        const creation = trends?.creationTrend || [];
        const completion = trends?.completionTrend || [];
        const monthlyMaintenanceData = creation.map((point, index) => {
          const completedPoint = completion[index];
          const monthLabel = new Date(point.date).toLocaleDateString(undefined, {
            month: 'short',
            day: 'numeric',
          });
          return {
            month: monthLabel,
            planned: point.value,
            completed: completedPoint?.value ?? 0,
            emergency: 0,
          };
        });

        const upcomingMaintenance = (schedules || []).slice(0, 5).map((schedule) => ({
          id: schedule.id,
          asset: schedule.assetName,
          type:
            schedule.maintenanceTypeName ||
            schedule.maintenanceType ||
            schedule.name ||
            'Scheduled Maintenance',
          dueDate:
            schedule.nextDue ||
            schedule.nextDueDate ||
            schedule.nextScheduledDate ||
            new Date().toISOString(),
          technician: schedule.assignedTechnicianName || 'Unassigned',
        }));

        const recentWorkOrders = (history || []).slice(0, 5).map((item) => ({
          id: item.id,
          title: item.title,
          asset: item.assetName,
          status: item.status,
          priority: item.priority,
        }));

        setDashboardData({
          workOrderStatusData,
          monthlyMaintenanceData,
          assetHealthData,
          upcomingMaintenance,
          recentWorkOrders,
        });
      } catch (error) {
        console.error('Failed to load maintenance dashboard data from backend:', error);
      } finally {
        setLoading(false);
      }
    };

    loadDashboardData();
  }, []);

  const getStatusBadge = (status: string) => {
    const colors = {
      Open: 'bg-blue-100 text-blue-800',
      'In Progress': 'bg-yellow-100 text-yellow-800',
      Completed: 'bg-green-100 text-green-800',
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
      Low: 'bg-green-100 text-green-800',
      Medium: 'bg-blue-100 text-blue-800',
      High: 'bg-orange-100 text-orange-800',
      Critical: 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[priority] || 'bg-gray-100 text-gray-800'}>
        {priority}
      </Badge>
    );
  };

  const formatDueDate = (dateString: string) => {
    const date = new Date(dateString);
    if (Number.isNaN(date.getTime())) return '';

    return date.toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const completionRate = totalWorkOrders > 0 ? (completedWorkOrders / totalWorkOrders) * 100 : 0;

  if (loading) {
    return (
      <div className="flex h-[60vh] w-full items-center justify-center">
        <div className="flex flex-col items-center space-y-3">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-muted-foreground border-t-transparent" />
          <p className="text-sm text-muted-foreground">Loading maintenance dashboard…</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Work Orders</CardTitle>
            <FileText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalWorkOrders}</div>
            <p className="text-xs text-muted-foreground">+12% from last month</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Orders</CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{activeWorkOrders}</div>
            <p className="text-xs text-muted-foreground">Currently in progress</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Assets</CardTitle>
            <Package className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalAssets}</div>
            <p className="text-xs text-muted-foreground">Under maintenance management</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Available Technicians</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{availableTechnicians}</div>
            <p className="text-xs text-muted-foreground">Ready for assignments</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Overdue Items</CardTitle>
            <AlertTriangle className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{overdueItems}</div>
            <p className="text-xs text-muted-foreground">Requires immediate attention</p>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Work Order Status Distribution</CardTitle>
            <CardDescription>Current status of all work orders</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={dashboardData.workOrderStatusData}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={100}
                  paddingAngle={2}
                  dataKey="value"
                >
                  {dashboardData.workOrderStatusData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
            <div className="flex flex-wrap justify-center gap-4 mt-4">
              {dashboardData.workOrderStatusData.map((item) => (
                <div key={item.name} className="flex items-center space-x-2">
                  <div className="w-3 h-3 rounded-full" style={{ backgroundColor: item.color }} />
                  <span className="text-sm">
                    {item.name} ({item.value})
                  </span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Monthly Maintenance Trend</CardTitle>
            <CardDescription>Planned vs completed maintenance over time</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={dashboardData.monthlyMaintenanceData}>
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

      <Card>
        <CardHeader>
          <CardTitle>Asset Health Overview</CardTitle>
          <CardDescription>Health status of assets by category</CardDescription>
        </CardHeader>
        <CardContent>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={dashboardData.assetHealthData}>
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

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Recent Work Orders</CardTitle>
            <CardDescription>Latest maintenance activities</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {dashboardData.recentWorkOrders.map((order) => (
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

        <Card>
          <CardHeader>
            <CardTitle>Upcoming Maintenance</CardTitle>
            <CardDescription>Scheduled maintenance tasks due in the next 14 days</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {dashboardData.upcomingMaintenance.map((task) => (
                <div key={task.id} className="flex items-center justify-between p-3 border rounded-lg">
                  <div className="space-y-1">
                    <p className="text-sm font-medium">{task.asset}</p>
                    <p className="text-xs text-muted-foreground">
                      {task.type} - {task.technician}
                    </p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm font-medium">{formatDueDate(task.dueDate)}</p>
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
                <span className="text-sm text-muted-foreground">
                  {completedWorkOrders}/{totalWorkOrders}
                </span>
              </div>
              <Progress value={completionRate} className="w-full" />
              <p className="text-xs text-muted-foreground">
                {completionRate.toFixed(1)}% completion rate
              </p>
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
