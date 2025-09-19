import type { 
  DashboardStats, 
  SalesData, 
  RevenueByCategory, 
  TopProduct, 
  RecentOrder, 
  ActivityItem, 
  PerformanceMetric 
} from '../types/dashboard';

export class DashboardService {
  async getDashboardStats(): Promise<DashboardStats> {
    // Simulate API delay
    await new Promise(resolve => setTimeout(resolve, 800));
    
    return {
      totalRevenue: 2847690,
      totalOrders: 1284,
      totalCustomers: 8934,
      totalProducts: 456,
      revenueGrowth: 12.5,
      ordersGrowth: 8.2,
      customersGrowth: 15.3,
      productsGrowth: 4.7,
    };
  }

  async getSalesData(): Promise<SalesData[]> {
    await new Promise(resolve => setTimeout(resolve, 600));
    
    return [
      { month: 'Jan', revenue: 185000, orders: 120, target: 180000 },
      { month: 'Feb', revenue: 195000, orders: 135, target: 190000 },
      { month: 'Mar', revenue: 215000, orders: 150, target: 200000 },
      { month: 'Apr', revenue: 245000, orders: 165, target: 220000 },
      { month: 'May', revenue: 285000, orders: 180, target: 250000 },
      { month: 'Jun', revenue: 295000, orders: 195, target: 280000 },
      { month: 'Jul', revenue: 315000, orders: 210, target: 300000 },
      { month: 'Aug', revenue: 325000, orders: 225, target: 320000 },
      { month: 'Sep', revenue: 345000, orders: 240, target: 340000 },
      { month: 'Oct', revenue: 365000, orders: 255, target: 360000 },
      { month: 'Nov', revenue: 385000, orders: 270, target: 380000 },
      { month: 'Dec', revenue: 425000, orders: 285, target: 400000 },
    ];
  }

  async getRevenueByCategory(): Promise<RevenueByCategory[]> {
    await new Promise(resolve => setTimeout(resolve, 500));
    
    return [
      { category: 'Electronics', amount: 1250000, percentage: 35.2, color: '#3b82f6' },
      { category: 'Clothing', amount: 890000, percentage: 25.1, color: '#10b981' },
      { category: 'Home & Garden', amount: 567000, percentage: 16.0, color: '#f59e0b' },
      { category: 'Sports', amount: 445000, percentage: 12.5, color: '#ef4444' },
      { category: 'Books', amount: 289000, percentage: 8.1, color: '#8b5cf6' },
      { category: 'Other', amount: 109000, percentage: 3.1, color: '#6b7280' },
    ];
  }

  async getTopProducts(): Promise<TopProduct[]> {
    await new Promise(resolve => setTimeout(resolve, 400));
    
    return [
      { id: '1', name: 'MacBook Pro 14"', sales: 156, revenue: 312000, growth: 15.2 },
      { id: '2', name: 'iPhone 15 Pro', sales: 234, revenue: 280800, growth: 8.7 },
      { id: '3', name: 'Samsung Galaxy S24', sales: 189, revenue: 189000, growth: 12.3 },
      { id: '4', name: 'Dell XPS 13', sales: 98, revenue: 147000, growth: -2.1 },
      { id: '5', name: 'iPad Pro', sales: 145, revenue: 145000, growth: 6.8 },
    ];
  }

  async getRecentOrders(): Promise<RecentOrder[]> {
    await new Promise(resolve => setTimeout(resolve, 300));
    
    return [
      { id: 'ORD-001', customer: 'John Smith', amount: 1250, status: 'completed', date: '2025-01-18T10:30:00Z' },
      { id: 'ORD-002', customer: 'Sarah Johnson', amount: 890, status: 'pending', date: '2025-01-18T09:45:00Z' },
      { id: 'ORD-003', customer: 'Mike Davis', amount: 2100, status: 'completed', date: '2025-01-18T08:20:00Z' },
      { id: 'ORD-004', customer: 'Emily Brown', amount: 450, status: 'cancelled', date: '2025-01-17T16:15:00Z' },
      { id: 'ORD-005', customer: 'David Wilson', amount: 1680, status: 'pending', date: '2025-01-17T14:30:00Z' },
    ];
  }

  async getRecentActivity(): Promise<ActivityItem[]> {
    await new Promise(resolve => setTimeout(resolve, 350));
    
    return [
      { id: '1', type: 'order', description: 'New order #ORD-001 received', timestamp: '2025-01-18T10:30:00Z', user: 'System' },
      { id: '2', type: 'customer', description: 'New customer registered: John Doe', timestamp: '2025-01-18T09:15:00Z', user: 'Registration' },
      { id: '3', type: 'product', description: 'iPhone 15 Pro stock updated', timestamp: '2025-01-18T08:45:00Z', user: 'Admin' },
      { id: '4', type: 'payment', description: 'Payment received for order #ORD-003', timestamp: '2025-01-18T08:20:00Z', user: 'PaymentGateway' },
      { id: '5', type: 'order', description: 'Order #ORD-002 status updated', timestamp: '2025-01-17T17:30:00Z', user: 'Manager' },
    ];
  }

  async getPerformanceMetrics(): Promise<PerformanceMetric[]> {
    await new Promise(resolve => setTimeout(resolve, 450));
    
    return [
      { name: 'Sales Conversion', value: 3.2, target: 3.5, unit: '%', trend: 'up' },
      { name: 'Customer Satisfaction', value: 4.7, target: 4.5, unit: '/5', trend: 'up' },
      { name: 'Order Fulfillment', value: 98.5, target: 95.0, unit: '%', trend: 'up' },
      { name: 'Return Rate', value: 2.1, target: 3.0, unit: '%', trend: 'down' },
      { name: 'Inventory Turnover', value: 8.3, target: 8.0, unit: 'x', trend: 'stable' },
    ];
  }
}

export const dashboardService = new DashboardService();