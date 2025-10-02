'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui/select';
import {
  LineChart,
  Line,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell
} from 'recharts';
import { 
  Activity, 
  Users, 
  ShoppingCart, 
  DollarSign,
  TrendingUp,
  Clock,
  RefreshCw
} from 'lucide-react';
import { cn } from '../../lib/utils';
import { ClientOnly } from '../ui/client-only';

interface RealTimeDataPoint {
  timestamp: string;
  users: number;
  orders: number;
  revenue: number;
  sessions: number;
}

interface AnalyticsMetric {
  name: string;
  value: number;
  change: number;
  icon: React.ComponentType<any>;
  color: string;
}

interface RealTimeAnalyticsProps {
  className?: string;
  height?: number;
  refreshInterval?: number;
}

// Mock data generator for real-time simulation
const generateRealTimeData = (isClient = false): RealTimeDataPoint[] => {
  const data: RealTimeDataPoint[] = [];
  
  // Provide consistent mock data for SSR
  if (!isClient) {
    for (let i = 23; i >= 0; i--) {
      data.push({
        timestamp: `${String(Math.floor((23 - i) / 60) + 8).padStart(2, '0')}:${String((23 - i) % 60).padStart(2, '0')}`,
        users: 35 + (i % 15), // Consistent pattern
        orders: 5 + (i % 5),
        revenue: 500 + (i * 10),
        sessions: 50 + (i % 20),
      });
    }
    return data;
  }
  
  // Generate real random data on client
  const now = new Date();
  for (let i = 23; i >= 0; i--) {
    const timestamp = new Date(now.getTime() - (i * 60 * 1000));
    data.push({
      timestamp: timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      users: Math.floor(Math.random() * 50) + 20,
      orders: Math.floor(Math.random() * 10) + 1,
      revenue: Math.floor(Math.random() * 1000) + 200,
      sessions: Math.floor(Math.random() * 80) + 30,
    });
  }
  
  return data;
};

const pieColors = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'];

export function RealTimeAnalytics({ 
  className, 
  height = 400, 
  refreshInterval = 30000 
}: RealTimeAnalyticsProps) {
  const [data, setData] = useState<RealTimeDataPoint[]>(() => generateRealTimeData(false));
  const [selectedMetric, setSelectedMetric] = useState('users');
  const [viewMode, setViewMode] = useState<'line' | 'area'>('area');
  const [isLive, setIsLive] = useState(true);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);
  const [isClient, setIsClient] = useState(false);

  // Initialize data and client state
  useEffect(() => {
    setIsClient(true);
    setData(generateRealTimeData(true)); // Generate real client data
    setLastUpdate(new Date());
  }, []);

  // Real-time updates
  useEffect(() => {
    if (!isLive) return;

    const interval = setInterval(() => {
      setData(prevData => {
        const newData = [...prevData];
        // Remove oldest point
        newData.shift();
        // Add new point
        const now = new Date();
        newData.push({
          timestamp: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          users: Math.floor(Math.random() * 50) + 20,
          orders: Math.floor(Math.random() * 10) + 1,
          revenue: Math.floor(Math.random() * 1000) + 200,
          sessions: Math.floor(Math.random() * 80) + 30,
        });
        return newData;
      });
      setLastUpdate(new Date());
    }, refreshInterval);

    return () => clearInterval(interval);
  }, [isLive, refreshInterval]);

  const currentMetrics: AnalyticsMetric[] = [
    {
      name: 'Active Users',
      value: data[data.length - 1]?.users || 0,
      change: data.length > 1 ? 
        ((data[data.length - 1]?.users - data[data.length - 2]?.users) / data[data.length - 2]?.users) * 100 : 0,
      icon: Users,
      color: 'blue'
    },
    {
      name: 'Live Orders',
      value: data[data.length - 1]?.orders || 0,
      change: data.length > 1 ? 
        ((data[data.length - 1]?.orders - data[data.length - 2]?.orders) / data[data.length - 2]?.orders) * 100 : 0,
      icon: ShoppingCart,
      color: 'green'
    },
    {
      name: 'Revenue/Min',
      value: data[data.length - 1]?.revenue || 0,
      change: data.length > 1 ? 
        ((data[data.length - 1]?.revenue - data[data.length - 2]?.revenue) / data[data.length - 2]?.revenue) * 100 : 0,
      icon: DollarSign,
      color: 'orange'
    },
    {
      name: 'Sessions',
      value: data[data.length - 1]?.sessions || 0,
      change: data.length > 1 ? 
        ((data[data.length - 1]?.sessions - data[data.length - 2]?.sessions) / data[data.length - 2]?.sessions) * 100 : 0,
      icon: Activity,
      color: 'purple'
    }
  ];

  const pieData = currentMetrics.map((metric, index) => ({
    name: metric.name,
    value: metric.value,
    color: pieColors[index]
  }));

  const refreshData = () => {
    setData(generateRealTimeData(isClient));
    setLastUpdate(new Date());
  };

  return (
    <Card className={cn('w-full', className)}>
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center space-x-2">
              <Activity className="h-5 w-5 text-blue-600" />
              <span>Real-Time Analytics</span>
              <Badge variant={isLive ? 'default' : 'secondary'} className="ml-2">
                {isLive ? 'LIVE' : 'PAUSED'}
              </Badge>
            </CardTitle>
            <ClientOnly fallback={
              <p className="text-sm text-muted-foreground mt-1">
                Last updated: Loading...
              </p>
            }>
              <p className="text-sm text-muted-foreground mt-1">
                Last updated: {lastUpdate?.toLocaleTimeString() || 'Loading...'}
              </p>
            </ClientOnly>
          </div>
          <div className="flex items-center space-x-2">
            <ClientOnly fallback={
              <div className="w-32 h-9 bg-muted/50 rounded-md border" />
            }>
              <Select value={selectedMetric} onValueChange={setSelectedMetric}>
                <SelectTrigger className="w-32">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="users">Users</SelectItem>
                  <SelectItem value="orders">Orders</SelectItem>
                  <SelectItem value="revenue">Revenue</SelectItem>
                  <SelectItem value="sessions">Sessions</SelectItem>
                </SelectContent>
              </Select>
            </ClientOnly>
            
            <ClientOnly fallback={
              <div className="w-20 h-9 bg-muted/50 rounded-md border" />
            }>
              <Select value={viewMode} onValueChange={(value: 'line' | 'area') => setViewMode(value)}>
                <SelectTrigger className="w-20">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="line">Line</SelectItem>
                  <SelectItem value="area">Area</SelectItem>
                </SelectContent>
              </Select>
            </ClientOnly>
            
            <Button
              variant="outline"
              size="sm"
              onClick={() => setIsLive(!isLive)}
              className={cn(isLive && 'bg-green-50 border-green-200 text-green-700')}
            >
              {isLive ? 'Live' : 'Paused'}
            </Button>
            
            <Button variant="outline" size="sm" onClick={refreshData}>
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </CardHeader>

      <CardContent>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Real-time metrics */}
          <div className="lg:col-span-2">
            <div className="mb-4">
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                {currentMetrics.map((metric, index) => {
                  const Icon = metric.icon;
                  return (
                    <div key={metric.name} className="bg-muted/50 rounded-lg p-3">
                      <div className="flex items-center justify-between">
                        <Icon className="h-4 w-4 text-muted-foreground" />
                        <div className={cn(
                          'text-xs font-medium',
                          metric.change >= 0 ? 'text-green-600' : 'text-red-600'
                        )}>
                          {metric.change >= 0 ? '+' : ''}{metric.change.toFixed(1)}%
                        </div>
                      </div>
                      <div className="mt-2">
                        <div className="text-lg font-bold">
                          {metric.name.includes('Revenue') ? `$${metric.value}` : metric.value}
                        </div>
                        <div className="text-xs text-muted-foreground">{metric.name}</div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
            
            {/* Chart */}
            <div style={{ height }}>
              <ResponsiveContainer width="100%" height="100%">
                {viewMode === 'area' ? (
                  <AreaChart data={data}>
                    <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
                    <XAxis 
                      dataKey="timestamp" 
                      tick={{ fontSize: 12 }}
                      interval="preserveStartEnd"
                    />
                    <YAxis tick={{ fontSize: 12 }} />
                    <Tooltip 
                      contentStyle={{
                        backgroundColor: 'hsl(var(--card))',
                        border: '1px solid hsl(var(--border))',
                        borderRadius: '6px'
                      }}
                    />
                    <Area
                      type="monotone"
                      dataKey={selectedMetric}
                      stroke="#3b82f6"
                      fill="#3b82f6"
                      fillOpacity={0.1}
                      strokeWidth={2}
                    />
                  </AreaChart>
                ) : (
                  <LineChart data={data}>
                    <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
                    <XAxis 
                      dataKey="timestamp" 
                      tick={{ fontSize: 12 }}
                      interval="preserveStartEnd"
                    />
                    <YAxis tick={{ fontSize: 12 }} />
                    <Tooltip 
                      contentStyle={{
                        backgroundColor: 'hsl(var(--card))',
                        border: '1px solid hsl(var(--border))',
                        borderRadius: '6px'
                      }}
                    />
                    <Line
                      type="monotone"
                      dataKey={selectedMetric}
                      stroke="#3b82f6"
                      strokeWidth={2}
                      dot={false}
                    />
                  </LineChart>
                )}
              </ResponsiveContainer>
            </div>
          </div>

          {/* Distribution pie chart */}
          <div className="lg:col-span-1">
            <div className="bg-muted/50 rounded-lg p-4">
              <h3 className="text-sm font-medium mb-4">Current Distribution</h3>
              <div style={{ height: 200 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={pieData}
                      cx="50%"
                      cy="50%"
                      innerRadius={40}
                      outerRadius={80}
                      dataKey="value"
                    >
                      {pieData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip 
                      contentStyle={{
                        backgroundColor: 'hsl(var(--card))',
                        border: '1px solid hsl(var(--border))',
                        borderRadius: '6px'
                      }}
                    />
                  </PieChart>
                </ResponsiveContainer>
              </div>
              
              <div className="space-y-2 mt-4">
                {pieData.map((entry, index) => (
                  <div key={entry.name} className="flex items-center justify-between text-sm">
                    <div className="flex items-center space-x-2">
                      <div 
                        className="w-3 h-3 rounded-full" 
                        style={{ backgroundColor: entry.color }}
                      />
                      <span className="text-muted-foreground">{entry.name}</span>
                    </div>
                    <span className="font-medium">{entry.value}</span>
                  </div>
                ))}
              </div>
            </div>
            
            {/* Status indicators */}
            <div className="mt-4 space-y-2">
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Status</span>
                <Badge variant="default">Healthy</Badge>
              </div>
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Load</span>
                <Badge variant="outline">Normal</Badge>
              </div>
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Alerts</span>
                <Badge variant="secondary">0</Badge>
              </div>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default RealTimeAnalytics;