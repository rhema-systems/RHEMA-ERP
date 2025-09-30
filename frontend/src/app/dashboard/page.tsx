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
        {/* Welcome back header is temporarily hidden */}
        
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
