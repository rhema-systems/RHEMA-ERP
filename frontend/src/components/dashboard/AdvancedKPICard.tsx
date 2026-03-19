'use client';

import React, { useState } from 'react';
import { LucideIcon, TrendingUp, TrendingDown, MoreHorizontal, Eye } from 'lucide-react';
import { Card, CardContent, CardHeader } from '../ui/card';
import { Button } from '../ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import { cn } from '../../lib/utils';
import { useIsClient } from '../../lib/ssr-utils';

interface TrendData {
  period: string;
  value: number;
}

interface AdvancedKPICardProps {
  title: string;
  value: string | number;
  change?: number;
  icon: LucideIcon;
  loading?: boolean;
  description?: string;
  trend?: TrendData[];
  target?: number;
  format?: 'currency' | 'number' | 'percentage';
  color?: 'blue' | 'green' | 'orange' | 'red' | 'purple';
  size?: 'sm' | 'md' | 'lg';
  showTarget?: boolean;
  interactive?: boolean;
  onClick?: () => void;
}

export function AdvancedKPICard({
  title,
  value,
  change = 0,
  icon: Icon,
  loading = false,
  description,
  trend = [],
  target,
  format = 'number',
  color = 'blue',
  size = 'md',
  showTarget = false,
  interactive = false,
  onClick
}: AdvancedKPICardProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const isClient = useIsClient();

  const formatValue = (val: string | number) => {
    if (typeof val === 'string') return val;
    
    if (!isClient) {
      // Provide simple formatting for SSR
      switch (format) {
        case 'currency':
          return `$${val.toFixed(0)}`;
        case 'percentage':
          return `${val.toFixed(1)}%`;
        default:
          return val.toString();
      }
    }
    
    // Use full formatting on client
    switch (format) {
      case 'currency':
        return new Intl.NumberFormat('en-US', {
          style: 'currency',
          currency: 'USD',
          minimumFractionDigits: 0,
          maximumFractionDigits: 0,
        }).format(val);
      case 'percentage':
        return `${val.toFixed(1)}%`;
      default:
        return val.toLocaleString();
    }
  };

  const getColorClasses = (colorScheme: string) => {
    const colors: Record<string, {
      icon: string;
      gradient: string;
      border: string;
      text: string;
      progress: string;
    }> = {
      blue: {
        icon: 'bg-blue-500',
        gradient: 'from-blue-50 to-blue-100 dark:from-blue-900/20 dark:to-blue-800/20',
        border: 'border-blue-200 dark:border-blue-800',
        text: 'text-blue-700 dark:text-blue-300',
        progress: 'bg-blue-500'
      },
      green: {
        icon: 'bg-green-500',
        gradient: 'from-green-50 to-green-100 dark:from-green-900/20 dark:to-green-800/20',
        border: 'border-green-200 dark:border-green-800',
        text: 'text-green-700 dark:text-green-300',
        progress: 'bg-green-500'
      },
      orange: {
        icon: 'bg-orange-500',
        gradient: 'from-orange-50 to-orange-100 dark:from-orange-900/20 dark:to-orange-800/20',
        border: 'border-orange-200 dark:border-orange-800',
        text: 'text-orange-700 dark:text-orange-300',
        progress: 'bg-orange-500'
      },
      red: {
        icon: 'bg-red-500',
        gradient: 'from-red-50 to-red-100 dark:from-red-900/20 dark:to-red-800/20',
        border: 'border-red-200 dark:border-red-800',
        text: 'text-red-700 dark:text-red-300',
        progress: 'bg-red-500'
      },
      purple: {
        icon: 'bg-purple-500',
        gradient: 'from-purple-50 to-purple-100 dark:from-purple-900/20 dark:to-purple-800/20',
        border: 'border-purple-200 dark:border-purple-800',
        text: 'text-purple-700 dark:text-purple-300',
        progress: 'bg-purple-500'
      }
    };
    return colors[colorScheme] || colors.blue;
  };

  const colorScheme = getColorClasses(color);
  const isPositive = change >= 0;
  const currentValue = typeof value === 'string' ? parseFloat(value.replace(/[^0-9.-]/g, '')) : value;
  const targetProgress = target && currentValue ? Math.min((currentValue / target) * 100, 100) : 0;

  const cardSizeClasses = {
    sm: 'p-4',
    md: 'p-6',
    lg: 'p-8'
  };

  return (
    <Card className={cn(
      'relative overflow-hidden transition-all duration-300 hover:shadow-lg',
      `bg-gradient-to-br ${colorScheme.gradient}`,
      `border ${colorScheme.border}`,
      interactive && 'cursor-pointer hover:scale-[1.02]',
      isExpanded && 'row-span-2'
    )}
    onClick={interactive ? onClick : undefined}
    >
      <CardHeader className={cn('pb-2', cardSizeClasses[size])}>
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className={cn(
              'flex h-10 w-10 items-center justify-center rounded-xl text-white shadow-lg',
              colorScheme.icon
            )}>
              <Icon className="h-5 w-5" />
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">{title}</p>
              {description && (
                <p className="text-xs text-muted-foreground/70">{description}</p>
              )}
            </div>
          </div>
          
          <div className="flex items-center space-x-1">
            {interactive && (
              <Button 
                variant="ghost" 
                size="sm"
                className="h-6 w-6 p-0"
                onClick={(e) => {
                  e.stopPropagation();
                  setIsExpanded(!isExpanded);
                }}
              >
                <Eye className="h-3 w-3" />
              </Button>
            )}
            
            {isClient && (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="sm" className="h-6 w-6 p-0" suppressHydrationWarning>
                    <MoreHorizontal className="h-3 w-3" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-40">
                  <DropdownMenuItem>View Details</DropdownMenuItem>
                  <DropdownMenuItem>Export Data</DropdownMenuItem>
                  <DropdownMenuItem>Set Target</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
        </div>
      </CardHeader>

      <CardContent className={cn('pt-0', cardSizeClasses[size])}>
        {loading ? (
          <div className="space-y-3">
            <div className="h-8 bg-muted animate-pulse rounded" />
            <div className="h-4 bg-muted animate-pulse rounded w-24" />
          </div>
        ) : (
          <div className="space-y-4">
            {/* Main Value */}
            <div className="flex items-baseline justify-between">
              <div className={cn('text-2xl font-bold', colorScheme.text)}>
                {formatValue(value)}
              </div>
              {change !== 0 && (
                <div className={cn(
                  'flex items-center space-x-1 text-sm font-medium',
                  isPositive ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'
                )}>
                  {isPositive ? (
                    <TrendingUp className="h-3 w-3" />
                  ) : (
                    <TrendingDown className="h-3 w-3" />
                  )}
                  <span>{Math.abs(change).toFixed(1)}%</span>
                </div>
              )}
            </div>

            {/* Target Progress */}
            {showTarget && target && (
              <div className="space-y-2">
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Target Progress</span>
                  <span className={colorScheme.text}>{targetProgress.toFixed(0)}%</span>
                </div>
                <div className="w-full bg-muted rounded-full h-2">
                  <div 
                    className={cn('h-2 rounded-full transition-all duration-500', colorScheme.progress)}
                    style={{ width: `${targetProgress}%` }}
                  />
                </div>
              </div>
            )}

            {/* Mini Trend Chart */}
            {isExpanded && trend.length > 0 && (
              <div className="pt-4 border-t border-muted">
                <div className="flex justify-between items-end h-16 space-x-1">
                  {trend.slice(-7).map((point, index) => {
                    const maxValue = Math.max(...trend.map(t => t.value));
                    const height = (point.value / maxValue) * 100;
                    
                    return (
                      <div key={index} className="flex flex-col items-center flex-1">
                        <div 
                          className={cn('w-full rounded-t transition-all duration-300', colorScheme.progress)}
                          style={{ height: `${height}%`, minHeight: '4px' }}
                        />
                        <span className="text-xs text-muted-foreground mt-1 truncate">
                          {point.period}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

export default AdvancedKPICard;
