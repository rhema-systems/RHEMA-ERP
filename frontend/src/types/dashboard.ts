export interface DashboardStats {
  totalRevenue: number;
  totalOrders: number;
  totalCustomers: number;
  totalProducts: number;
  revenueGrowth: number;
  ordersGrowth: number;
  customersGrowth: number;
  productsGrowth: number;
  revenueTarget?: number;
  ordersTarget?: number;
}

export interface SalesData {
  month: string;
  revenue: number;
  orders: number;
  target: number;
}

export interface RevenueByCategory {
  category: string;
  amount: number;
  percentage: number;
  color: string;
}

export interface TopProduct {
  id: string;
  name: string;
  sales: number;
  revenue: number;
  growth: number;
}

export interface RecentOrder {
  id: string;
  customer: string;
  amount: number;
  status: 'pending' | 'completed' | 'cancelled';
  date: string;
}

export interface ActivityItem {
  id: string;
  type: 'order' | 'customer' | 'product' | 'payment';
  description: string;
  timestamp: string;
  user: string;
}

export interface PerformanceMetric {
  name: string;
  value: number;
  target: number;
  unit: string;
  trend: 'up' | 'down' | 'stable';
}
