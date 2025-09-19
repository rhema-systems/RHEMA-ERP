'use client';

import { useQuery } from '@tanstack/react-query';
import { DollarSign, ShoppingCart, Users, Package } from 'lucide-react';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { KPICard } from '../../components/dashboard/KPICard';
import { SalesChart } from '../../components/dashboard/SalesChart';
import { RevenueByCategoryChart } from '../../components/dashboard/RevenueByCategoryChart';
import { TopProducts } from '../../components/dashboard/TopProducts';
import { RecentActivity } from '../../components/dashboard/RecentActivity';
import { ClientOnly } from '../../components/ClientOnly';
import { dashboardService } from '../../services/dashboard';
import { useAuth } from '../../hooks/use-auth';

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
  const { user } = useAuth();

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

  return (
    <DashboardLayout>
      <div className="space-y-8">
        {/* Enhanced Header */}
        <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-blue-50 via-indigo-50 to-purple-50 dark:from-gray-900 dark:via-blue-900/20 dark:to-purple-900/20 p-8 border border-blue-100 dark:border-gray-800">
          <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-200/20 to-purple-200/20 rounded-full blur-3xl -translate-y-8 translate-x-8" />
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-indigo-200/20 to-pink-200/20 rounded-full blur-2xl translate-y-4 -translate-x-4" />
          <div className="relative z-10">
            <ClientOnly fallback={
              <div className="space-y-2">
                <h1 className="text-4xl font-bold tracking-tight bg-gradient-to-r from-gray-900 via-blue-800 to-purple-800 dark:from-white dark:via-blue-200 dark:to-purple-200 bg-clip-text text-transparent">
                  Dashboard Overview 📊
                </h1>
                <p className="text-lg text-gray-600 dark:text-gray-300 max-w-2xl">
                  Welcome to your business command center
                </p>
              </div>
            }>
              <div className="space-y-2">
                <h1 className="text-4xl font-bold tracking-tight bg-gradient-to-r from-gray-900 via-blue-800 to-purple-800 dark:from-white dark:via-blue-200 dark:to-purple-200 bg-clip-text text-transparent">
                  Welcome back, {user?.firstName || user?.username}! 👋
                </h1>
                <p className="text-lg text-gray-600 dark:text-gray-300 max-w-2xl">
                  Here&rsquo;s your business overview and key metrics for today. Let&rsquo;s make it productive!
                </p>
              </div>
            </ClientOnly>
          </div>
        </div>
        
        {/* Enhanced KPI Cards */}
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4 animate-in slide-in-from-bottom-4 duration-700">
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
        </div>

        {/* Main Chart Section */}
        <div className="animate-in slide-in-from-bottom-6 duration-1000 delay-200">
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
