'use client';

import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Area, AreaChart } from 'recharts';
import type { SalesData } from '../../types/dashboard';

interface SalesChartProps {
  data: SalesData[];
  loading?: boolean;
}

export function SalesChart({ data, loading = false }: SalesChartProps) {
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const CustomTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload.length) {
      return (
        <div className="bg-white dark:bg-slate-800 p-3 rounded-lg shadow-lg border border-slate-200 dark:border-slate-700">
          <p className="font-medium text-slate-900 dark:text-slate-100">{label}</p>
          <div className="space-y-1 mt-2">
            <p className="text-sm text-blue-600 dark:text-blue-400">
              Revenue: {formatCurrency(payload[0]?.value || 0)}
            </p>
            <p className="text-sm text-green-600 dark:text-green-400">
              Target: {formatCurrency(payload[1]?.value || 0)}
            </p>
            <p className="text-sm text-purple-600 dark:text-purple-400">
              Orders: {payload[2]?.value || 0}
            </p>
          </div>
        </div>
      );
    }
    return null;
  };

  if (loading) {
    return (
      <Card>
        <CardHeader>
          <div className="h-6 w-32 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
        </CardHeader>
        <CardContent>
          <div className="h-80 bg-slate-100 dark:bg-slate-800 rounded animate-pulse" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center justify-between">
          <span>Sales Performance</span>
          <div className="flex items-center space-x-4 text-sm">
            <div className="flex items-center space-x-2">
              <div className="w-3 h-3 rounded-full bg-blue-500" />
              <span className="text-slate-600 dark:text-slate-400">Revenue</span>
            </div>
            <div className="flex items-center space-x-2">
              <div className="w-3 h-3 rounded-full bg-green-500" />
              <span className="text-slate-600 dark:text-slate-400">Target</span>
            </div>
            <div className="flex items-center space-x-2">
              <div className="w-3 h-3 rounded-full bg-purple-500" />
              <span className="text-slate-600 dark:text-slate-400">Orders</span>
            </div>
          </div>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-80">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 10, right: 30, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="revenueGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#3b82f6" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" className="stroke-slate-200 dark:stroke-slate-700" />
              <XAxis 
                dataKey="month" 
                className="text-slate-600 dark:text-slate-400"
                fontSize={12}
              />
              <YAxis 
                className="text-slate-600 dark:text-slate-400"
                fontSize={12}
                tickFormatter={formatCurrency}
              />
              <Tooltip content={<CustomTooltip />} />
              <Area
                type="monotone"
                dataKey="revenue"
                stroke="#3b82f6"
                strokeWidth={2}
                fill="url(#revenueGradient)"
              />
              <Line
                type="monotone"
                dataKey="target"
                stroke="#10b981"
                strokeWidth={2}
                strokeDasharray="5 5"
                dot={false}
              />
              <Line
                type="monotone"
                dataKey="orders"
                stroke="#8b5cf6"
                strokeWidth={2}
                yAxisId="right"
                dot={{ fill: '#8b5cf6', strokeWidth: 2, r: 4 }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  );
}