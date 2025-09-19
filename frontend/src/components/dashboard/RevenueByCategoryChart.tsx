'use client';

import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip } from 'recharts';
import type { RevenueByCategory } from '../../types/dashboard';

interface RevenueByCategoryChartProps {
  data: RevenueByCategory[];
  loading?: boolean;
}

export function RevenueByCategoryChart({ data, loading = false }: RevenueByCategoryChartProps) {
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
  };

  const CustomTooltip = ({ active, payload }: any) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="bg-white dark:bg-slate-800 p-3 rounded-lg shadow-lg border border-slate-200 dark:border-slate-700">
          <p className="font-medium text-slate-900 dark:text-slate-100">{data.category}</p>
          <div className="space-y-1 mt-2">
            <p className="text-sm" style={{ color: data.color }}>
              Revenue: {formatCurrency(data.amount)}
            </p>
            <p className="text-sm text-slate-600 dark:text-slate-400">
              {data.percentage}% of total
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
          <div className="h-6 w-40 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between">
            <div className="w-48 h-48 rounded-full bg-slate-100 dark:bg-slate-800 animate-pulse" />
            <div className="space-y-3 flex-1 ml-6">
              {[...Array(6)].map((_, i) => (
                <div key={i} className="flex items-center space-x-3">
                  <div className="w-4 h-4 rounded bg-slate-200 dark:bg-slate-700 animate-pulse" />
                  <div className="h-4 bg-slate-200 dark:bg-slate-700 rounded flex-1 animate-pulse" />
                </div>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Revenue by Category</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="flex items-center justify-between">
          <div className="w-48 h-48">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={data as any[]}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={90}
                  paddingAngle={2}
                  dataKey="amount"
                >
                  {data.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip content={<CustomTooltip />} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          
          <div className="flex-1 ml-6 space-y-3">
            {data.map((item, index) => (
              <div key={index} className="flex items-center justify-between">
                <div className="flex items-center space-x-3">
                  <div 
                    className="w-4 h-4 rounded"
                    style={{ backgroundColor: item.color }}
                  />
                  <span className="text-sm font-medium text-slate-700 dark:text-slate-300">
                    {item.category}
                  </span>
                </div>
                <div className="text-right">
                  <div className="text-sm font-medium text-slate-900 dark:text-slate-100">
                    {formatCurrency(item.amount)}
                  </div>
                  <div className="text-xs text-slate-500 dark:text-slate-400">
                    {item.percentage}%
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}