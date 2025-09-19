'use client';

import { Card, CardContent } from '../ui/card';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';
import { cn } from '../../lib/utils';

interface KPICardProps {
  title: string;
  value: string | number;
  change: number;
  icon: React.ComponentType<{ className?: string }>;
  trend?: 'up' | 'down' | 'neutral';
  loading?: boolean;
}

export function KPICard({ title, value, change, icon: Icon, trend = 'up', loading = false }: KPICardProps) {
  const getTrendIcon = () => {
    switch (trend) {
      case 'up':
        return <TrendingUp className="h-3 w-3" />;
      case 'down':
        return <TrendingDown className="h-3 w-3" />;
      default:
        return <Minus className="h-3 w-3" />;
    }
  };

  const getTrendColor = () => {
    switch (trend) {
      case 'up':
        return change >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400';
      case 'down':
        return change <= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400';
      default:
        return 'text-slate-600 dark:text-slate-400';
    }
  };

  if (loading) {
    return (
      <Card className="relative overflow-hidden">
        <CardContent className="p-6">
          <div className="flex items-center justify-between">
            <div className="space-y-2">
              <div className="h-4 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
              <div className="h-8 w-24 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
              <div className="h-3 w-16 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
            </div>
            <div className="h-12 w-12 bg-slate-200 dark:bg-slate-700 rounded-lg animate-pulse" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="relative overflow-hidden transition-all hover:shadow-md">
      <CardContent className="p-6">
        <div className="flex items-center justify-between">
          <div className="space-y-2">
            <p className="text-sm font-medium text-slate-600 dark:text-slate-400 uppercase tracking-wider">
              {title}
            </p>
            <div className="space-y-1">
              <h3 className="text-2xl font-bold text-slate-900 dark:text-slate-100">
                {value}
              </h3>
              <div className={cn(
                'flex items-center space-x-1 text-xs font-medium',
                getTrendColor()
              )}>
                {getTrendIcon()}
                <span>
                  {Math.abs(change)}% from last month
                </span>
              </div>
            </div>
          </div>
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-gradient-to-r from-blue-500/10 to-indigo-500/10 text-blue-600 dark:text-blue-400">
            <Icon className="h-6 w-6" />
          </div>
        </div>
      </CardContent>
      
      {/* Gradient overlay */}
      <div className="absolute inset-0 bg-gradient-to-r from-transparent via-transparent to-blue-500/5 pointer-events-none" />
    </Card>
  );
}