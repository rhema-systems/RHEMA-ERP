'use client';

import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { ShoppingCart, User, Package, CreditCard, Clock } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import type { ActivityItem } from '../../types/dashboard';

interface RecentActivityProps {
  data: ActivityItem[];
  loading?: boolean;
}

const activityIcons = {
  order: ShoppingCart,
  customer: User,
  product: Package,
  payment: CreditCard,
} as const;

const activityColors = {
  order: 'bg-blue-500',
  customer: 'bg-green-500',
  product: 'bg-purple-500',
  payment: 'bg-yellow-500',
} as const;

export function RecentActivity({ data, loading = false }: RecentActivityProps) {
  const formatRelativeTime = (timestamp: string) => {
    return formatDistanceToNow(new Date(timestamp), { addSuffix: true });
  };

  if (loading) {
    return (
      <Card>
        <CardHeader>
          <div className="h-6 w-32 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="flex items-start space-x-3">
                <div className="w-8 h-8 bg-slate-200 dark:bg-slate-700 rounded-full animate-pulse" />
                <div className="flex-1 space-y-2">
                  <div className="h-4 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
                  <div className="h-3 w-24 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center space-x-2">
          <Clock className="h-5 w-5" />
          <span>Recent Activity</span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {data.map((activity) => {
            const Icon = activityIcons[activity.type];
            const iconColorClass = activityColors[activity.type];

            return (
              <div key={activity.id} className="flex items-start space-x-3">
                <div className={`flex-shrink-0 w-8 h-8 ${iconColorClass} rounded-full flex items-center justify-center`}>
                  <Icon className="h-4 w-4 text-white" />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-slate-900 dark:text-slate-100">
                    {activity.description}
                  </p>
                  <div className="flex items-center space-x-2 mt-1">
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                      by {activity.user}
                    </p>
                    <span className="text-slate-300 dark:text-slate-600">•</span>
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                      {formatRelativeTime(activity.timestamp)}
                    </p>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}