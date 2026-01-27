'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { DollarSign, ShoppingCart, Users, Package, Settings, BarChart3 } from 'lucide-react';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { KPICard } from '../../components/dashboard/KPICard';
import { AdvancedKPICard } from '../../components/dashboard/AdvancedKPICard';
import { RealTimeAnalytics } from '../../components/dashboard/RealTimeAnalytics';
import { SalesChart } from '../../components/dashboard/SalesChart';
import { RevenueByCategoryChart } from '../../components/dashboard/RevenueByCategoryChart';
import { TopProducts } from '../../components/dashboard/TopProducts';
import { RecentActivity } from '../../components/dashboard/RecentActivity';
import { ClientOnly } from '../../components/ClientOnly';
import { Button } from '../../components/ui/button';
import { Badge } from '../../components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card';
import { dashboardService } from '../../services/dashboard';
import { useAuth } from '../../hooks/use-auth';
import { cn } from '../../lib/utils';
import { useIsClient, generateTrendData } from '../../lib/ssr-utils';
import { authService } from '../../services/auth';

// Utility function for formatting currency
const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
};

export default function Dashboard() {
  const router = useRouter();
  const { user } = useAuth();
  const [viewMode, setViewMode] = useState<'classic' | 'enhanced'>('enhanced');
  const [showRealTime, setShowRealTime] = useState(true);
  const isClient = useIsClient();

  // Redirect external users (Local authentication) to external portal
  useEffect(() => {
    if (typeof window === 'undefined') return;

    const storedUser = authService.getStoredUser();
    if (storedUser?.authenticationProvider === 'Local') {
      console.log('External user detected on internal dashboard, redirecting to external portal');
      router.push('/external-portal');
    }
  }, [router]);

  // Fetch dashboard data
  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: dashboardService.getDashboardStats,
  });

  const { data: salesData, isLoading: salesLoading } = useQuery({
    queryKey: ['sales-data'],
    queryFn: dashboardService.getSalesData,
  });

  const { data: categoryData, isLoading: categoryLoading } = useQuery({
    queryKey: ['revenue-by-category'],
    queryFn: dashboardService.getRevenueByCategory,
  });

  const { data: topProducts, isLoading: productsLoading } = useQuery({
    queryKey: ['top-products'],
    queryFn: dashboardService.getTopProducts,
  });

  const { data: recentActivity, isLoading: activityLoading } = useQuery({
    queryKey: ['recent-activity'],
    queryFn: dashboardService.getRecentActivity,
  });

  // Generate sample trend data for advanced KPI cards (SSR-safe)
  const getTrendData = () => {
    return generateTrendData(isClient);
  };

  return (
    <DashboardLayout>
      <div className="space-y-8">
        {/* Dashboard Header with Controls */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Dashboard</h1>
            <p className="text-muted-foreground">
              Welcome back, {user?.firstName || user?.username || 'User'}! Here's what's happening with your business.
            </p>
          </div>
          <div className="flex items-center space-x-2">
            <Badge variant={showRealTime ? 'default' : 'secondary'}>
              {showRealTime ? 'Real-time ON' : 'Real-time OFF'}
            </Badge>
            <Button
              variant={viewMode === 'classic' ? 'outline' : 'default'}
              size="sm"
              onClick={() => setViewMode(viewMode === 'classic' ? 'enhanced' : 'classic')}
            >
              {viewMode === 'classic' ? (
                <>
                  <BarChart3 className="h-4 w-4 mr-2" />
                  Enhanced View
                </>
              ) : (
                <>
                  <Settings className="h-4 w-4 mr-2" />
                  Classic View
                </>
              )}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowRealTime(!showRealTime)}
            >
              {showRealTime ? 'Disable' : 'Enable'} Real-time
            </Button>
          </div>
        </div>
        
        {/* KPI Cards - Enhanced or Classic */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4 animate-in slide-in-from-bottom-4 duration-700">
          {viewMode === 'enhanced' ? (
            <>
              <AdvancedKPICard
                title="Total Revenue"
                value={stats?.totalRevenue || 0}
                change={stats?.revenueGrowth || 0}
                icon={DollarSign}
                loading={statsLoading}
                format="currency"
                color="green"
                trend={getTrendData()}
                target={stats?.revenueTarget || 1000000}
                showTarget
                interactive
                description="Monthly recurring revenue"
              />
              <AdvancedKPICard
                title="Orders"
                value={stats?.totalOrders || 0}
                change={stats?.ordersGrowth || 0}
                icon={ShoppingCart}
                loading={statsLoading}
                color="blue"
                trend={getTrendData()}
                target={stats?.ordersTarget || 1000}
                showTarget
                interactive
                description="Total orders this month"
              />
              <AdvancedKPICard
                title="Customers"
                value={stats?.totalCustomers || 0}
                change={stats?.customersGrowth || 0}
                icon={Users}
                loading={statsLoading}
                color="purple"
                trend={getTrendData()}
                interactive
                description="Active customer base"
              />
              <AdvancedKPICard
                title="Products"
                value={stats?.totalProducts || 0}
                change={stats?.productsGrowth || 0}
                icon={Package}
                loading={statsLoading}
                color="orange"
                trend={getTrendData()}
                interactive
                description="Products in catalog"
              />
            </>
          ) : (
            <>
              <KPICard
                title="Total Revenue"
                value={stats ? formatCurrency(stats.totalRevenue) : '$0'}
                change={stats?.revenueGrowth || 0}
                icon={DollarSign}
                loading={statsLoading}
              />
              <KPICard
                title="Orders"
                value={stats?.totalOrders?.toLocaleString() || '0'}
                change={stats?.ordersGrowth || 0}
                icon={ShoppingCart}
                loading={statsLoading}
              />
              <KPICard
                title="Customers"
                value={stats?.totalCustomers?.toLocaleString() || '0'}
                change={stats?.customersGrowth || 0}
                icon={Users}
                loading={statsLoading}
              />
              <KPICard
                title="Products"
                value={stats?.totalProducts?.toLocaleString() || '0'}
                change={stats?.productsGrowth || 0}
                icon={Package}
                loading={statsLoading}
              />
            </>
          )}
        </div>

        {/* Real-time Analytics Widget */}
        {showRealTime && viewMode === 'enhanced' && (
          <div className="animate-in slide-in-from-bottom-6 duration-1000 delay-200">
            <RealTimeAnalytics className="" height={350} />
          </div>
        )}

        {/* Main Chart Section */}
        <div className={cn(
          'animate-in slide-in-from-bottom-6 duration-1000',
          showRealTime && viewMode === 'enhanced' ? 'delay-300' : 'delay-200'
        )}>
          <SalesChart data={salesData || []} loading={salesLoading} />
        </div>

        {/* Secondary Charts & Data */}
        <div className="grid gap-6 lg:grid-cols-3 animate-in slide-in-from-bottom-8 duration-1000 delay-300">
          <div className="lg:col-span-1">
            <RevenueByCategoryChart data={categoryData || []} loading={categoryLoading} />
          </div>
          <div className="lg:col-span-1">
            <TopProducts data={topProducts || []} loading={productsLoading} />
          </div>
          <div className="lg:col-span-1">
            <RecentActivity data={recentActivity || []} loading={activityLoading} />
          </div>
        </div>
      </div>
    </DashboardLayout>
  );
}
