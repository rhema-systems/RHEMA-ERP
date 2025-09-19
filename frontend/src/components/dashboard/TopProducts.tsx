'use client';

import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { TrendingUp, TrendingDown } from 'lucide-react';
import { cn } from '../../lib/utils';
import type { TopProduct } from '../../types/dashboard';

interface TopProductsProps {
  data: TopProduct[];
  loading?: boolean;
}

export function TopProducts({ data, loading = false }: TopProductsProps) {
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(value);
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
              <div key={i} className="flex items-center justify-between p-3 rounded-lg border">
                <div className="space-y-2">
                  <div className="h-4 w-32 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
                  <div className="h-3 w-20 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
                </div>
                <div className="text-right space-y-2">
                  <div className="h-4 w-20 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
                  <div className="h-3 w-16 bg-slate-200 dark:bg-slate-700 rounded animate-pulse" />
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
        <CardTitle>Top Products</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {data.map((product, index) => (
            <div 
              key={product.id} 
              className="flex items-center justify-between p-3 rounded-lg border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
            >
              <div className="flex items-center space-x-3">
                <div className="flex-shrink-0">
                  <div className="w-8 h-8 bg-gradient-to-r from-blue-500 to-indigo-500 rounded-lg flex items-center justify-center text-white font-bold text-sm">
                    #{index + 1}
                  </div>
                </div>
                <div>
                  <h4 className="font-medium text-slate-900 dark:text-slate-100">
                    {product.name}
                  </h4>
                  <p className="text-sm text-slate-500 dark:text-slate-400">
                    {product.sales} units sold
                  </p>
                </div>
              </div>
              
              <div className="text-right">
                <div className="font-medium text-slate-900 dark:text-slate-100">
                  {formatCurrency(product.revenue)}
                </div>
                <div className={cn(
                  'flex items-center justify-end space-x-1 text-sm',
                  product.growth >= 0 
                    ? 'text-green-600 dark:text-green-400' 
                    : 'text-red-600 dark:text-red-400'
                )}>
                  {product.growth >= 0 ? (
                    <TrendingUp className="h-3 w-3" />
                  ) : (
                    <TrendingDown className="h-3 w-3" />
                  )}
                  <span>{Math.abs(product.growth)}%</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}