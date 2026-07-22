import React, { useState, useEffect } from 'react';
import {
  Grid,
  Card,
  CardContent,
  Typography,
  Box,
  Chip,
  IconButton,
  Tabs,
  Tab,
  Alert,
  LinearProgress,
  Divider
} from '@mui/material';
import {
  Build as BuildIcon,
  Assignment as AssignmentIcon,
  Schedule as ScheduleIcon,
  Warning as WarningIcon,
  CheckCircle as CheckCircleIcon,
  Timeline as TimelineIcon,
  TrendingUp as TrendingUpIcon,
  Engineering as EngineeringIcon
} from '@mui/icons-material';
import { PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line } from 'recharts';
import { useMaintenanceCurrency } from '@/hooks/useMaintenanceCurrency';

interface DashboardStats {
  totalAssets: number;
  activeWorkOrders: number;
  overdueMaintenance: number;
  scheduledToday: number;
  pendingJobCards: number;
  completionRate: number;
  averageDowntime: number;
  costThisMonth: number;
}

interface WorkOrderStatus {
  name: string;
  value: number;
  color: string;
}

interface MaintenanceType {
  type: string;
  count: number;
}

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8', '#82CA9D'];

const MaintenanceDashboard: React.FC = () => {
  const { formatMoney } = useMaintenanceCurrency();
  const [activeTab, setActiveTab] = useState(0);
  const [stats, setStats] = useState<DashboardStats>({
    totalAssets: 0,
    activeWorkOrders: 0,
    overdueMaintenance: 0,
    scheduledToday: 0,
    pendingJobCards: 0,
    completionRate: 0,
    averageDowntime: 0,
    costThisMonth: 0
  });
  
  const [workOrderStatuses, setWorkOrderStatuses] = useState<WorkOrderStatus[]>([]);
  const [maintenanceTypes, setMaintenanceTypes] = useState<MaintenanceType[]>([]);
  const [recentAlerts, setRecentAlerts] = useState<any[]>([]);

  useEffect(() => {
    // Fetch dashboard data
    fetchDashboardData();
  }, []);

  const fetchDashboardData = async () => {
    try {
      // Mock data - replace with actual API calls
      setStats({
        totalAssets: 1247,
        activeWorkOrders: 87,
        overdueMaintenance: 12,
        scheduledToday: 15,
        pendingJobCards: 23,
        completionRate: 94.2,
        averageDowntime: 4.2,
        costThisMonth: 145600
      });

      setWorkOrderStatuses([
        { name: 'Assigned', value: 25, color: '#0088FE' },
        { name: 'In Progress', value: 35, color: '#00C49F' },
        { name: 'On Hold', value: 8, color: '#FFBB28' },
        { name: 'Completed', value: 140, color: '#82CA9D' },
        { name: 'Cancelled', value: 5, color: '#FF8042' }
      ]);

      setMaintenanceTypes([
        { type: 'Preventive', count: 45 },
        { type: 'Corrective', count: 28 },
        { type: 'Emergency', count: 8 },
        { type: 'Routine', count: 62 },
        { type: 'Inspection', count: 18 }
      ]);

      setRecentAlerts([
        { id: 1, type: 'warning', message: 'Asset HVAC-001 is overdue for maintenance', asset: 'HVAC-001' },
        { id: 2, type: 'error', message: 'Work Order WO-2024-1205 has failed quality inspection', workOrder: 'WO-2024-1205' },
        { id: 3, type: 'info', message: '15 work orders scheduled for today', count: 15 }
      ]);
    } catch (error) {
      console.error('Failed to fetch dashboard data:', error);
    }
  };

  const handleTabChange = (event: React.SyntheticEvent, newValue: number) => {
    setActiveTab(newValue);
  };

  const StatCard = ({ title, value, icon, color, subtext }: any) => (
    <Card elevation={2} sx={{ height: '100%' }}>
      <CardContent>
        <Box display="flex" alignItems="center" justifyContent="space-between">
          <Box>
            <Typography variant="h4" component="div" color={color} fontWeight="bold">
              {typeof value === 'number' && value > 1000 ? 
                `${(value / 1000).toFixed(1)}k` : 
                value.toLocaleString()}
            </Typography>
            <Typography variant="subtitle2" color="text.secondary" gutterBottom>
              {title}
            </Typography>
            {subtext && (
              <Typography variant="caption" color="text.secondary">
                {subtext}
              </Typography>
            )}
          </Box>
          <Box sx={{ color }}>
            {icon}
          </Box>
        </Box>
      </CardContent>
    </Card>
  );

  return (
    <Box sx={{ flexGrow: 1, p: 3 }}>
      <Typography variant="h4" gutterBottom fontWeight="bold">
        Maintenance Management Dashboard
      </Typography>

      <Tabs value={activeTab} onChange={handleTabChange} sx={{ mb: 3 }}>
        <Tab label="Overview" />
        <Tab label="Work Orders" />
        <Tab label="Assets" />
        <Tab label="Performance" />
      </Tabs>

      {/* Key Metrics Row */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Total Assets"
            value={stats.totalAssets}
            icon={<BuildIcon sx={{ fontSize: 40 }} />}
            color="primary.main"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Active Work Orders"
            value={stats.activeWorkOrders}
            icon={<AssignmentIcon sx={{ fontSize: 40 }} />}
            color="info.main"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Overdue Maintenance"
            value={stats.overdueMaintenance}
            icon={<WarningIcon sx={{ fontSize: 40 }} />}
            color="warning.main"
            subtext="Requires immediate attention"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Scheduled Today"
            value={stats.scheduledToday}
            icon={<ScheduleIcon sx={{ fontSize: 40 }} />}
            color="success.main"
          />
        </Grid>
      </Grid>

      {/* Secondary Metrics Row */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Pending Job Cards"
            value={stats.pendingJobCards}
            icon={<EngineeringIcon sx={{ fontSize: 40 }} />}
            color="secondary.main"
            subtext="Awaiting approval"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Completion Rate"
            value={`${stats.completionRate}%`}
            icon={<CheckCircleIcon sx={{ fontSize: 40 }} />}
            color="success.main"
            subtext="This month"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Avg Downtime"
            value={`${stats.averageDowntime}h`}
            icon={<TimelineIcon sx={{ fontSize: 40 }} />}
            color="error.main"
            subtext="Per incident"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title="Cost This Month"
            value={formatMoney(stats.costThisMonth, 0)}
            icon={<TrendingUpIcon sx={{ fontSize: 40 }} />}
            color="warning.main"
            subtext={`Budget: ${formatMoney(180000, 0)}`}
          />
        </Grid>
      </Grid>

      {/* Charts and Alerts Row */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        {/* Work Order Status Distribution */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card elevation={2} sx={{ height: 400 }}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Work Order Status Distribution
              </Typography>
              <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                  <Pie
                    data={workOrderStatuses as unknown as Array<Record<string, string | number>>}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    label={({ name, percent }: any) => `${name} ${(Number(percent ?? 0) * 100).toFixed(0)}%`}
                    outerRadius={80}
                    fill="#8884d8"
                    dataKey="value"
                  >
                    {workOrderStatuses.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>

        {/* Maintenance Types */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card elevation={2} sx={{ height: 400 }}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Maintenance Types (This Month)
              </Typography>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={maintenanceTypes}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="type" />
                  <YAxis />
                  <Tooltip />
                  <Bar dataKey="count" fill="#8884d8" />
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Recent Alerts */}
      <Grid container spacing={3}>
        <Grid size={{ xs: 12 }}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Recent Alerts & Notifications
              </Typography>
              <Divider sx={{ mb: 2 }} />
              {recentAlerts.length === 0 ? (
                <Typography color="text.secondary">No recent alerts</Typography>
              ) : (
                recentAlerts.map((alert) => (
                  <Alert 
                    key={alert.id} 
                    severity={alert.type} 
                    sx={{ mb: 1 }}
                    action={
                      <IconButton size="small">
                        <Typography variant="caption">View</Typography>
                      </IconButton>
                    }
                  >
                    {alert.message}
                  </Alert>
                ))
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Quick Actions - could be added as floating action buttons */}
      <Box sx={{ position: 'fixed', bottom: 20, right: 20 }}>
        {/* Quick action buttons could be added here */}
      </Box>
    </Box>
  );
};

export default MaintenanceDashboard;
