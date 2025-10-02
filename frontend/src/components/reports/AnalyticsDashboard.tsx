"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { 
  TrendingUp, 
  TrendingDown, 
  Users, 
  DollarSign, 
  ShoppingCart, 
  Package,
  Calendar,
  Download,
  RefreshCw,
  MoreHorizontal,
  ArrowUpRight,
  ArrowDownRight
} from 'lucide-react'

interface KPICard {
  title: string
  value: string
  change: number
  trend: 'up' | 'down'
  icon: React.ElementType
  description: string
}

interface ChartData {
  name: string
  value: number
  date?: string
}

const AnalyticsDashboard: React.FC = () => {
  const [selectedPeriod, setSelectedPeriod] = useState('last-30-days')
  const [selectedTenant, setSelectedTenant] = useState('all')

  // Mock KPI data
  const kpis: KPICard[] = [
    {
      title: 'Total Revenue',
      value: '$124,532',
      change: 12.5,
      trend: 'up',
      icon: DollarSign,
      description: '+12.5% from last month'
    },
    {
      title: 'Active Users',
      value: '2,345',
      change: 8.2,
      trend: 'up',
      icon: Users,
      description: '+8.2% from last month'
    },
    {
      title: 'Total Orders',
      value: '1,582',
      change: -3.1,
      trend: 'down',
      icon: ShoppingCart,
      description: '-3.1% from last month'
    },
    {
      title: 'Inventory Value',
      value: '$89,432',
      change: 5.7,
      trend: 'up',
      icon: Package,
      description: '+5.7% from last month'
    }
  ]

  // Mock chart data
  const revenueData: ChartData[] = [
    { name: 'Jan', value: 45000 },
    { name: 'Feb', value: 52000 },
    { name: 'Mar', value: 48000 },
    { name: 'Apr', value: 61000 },
    { name: 'May', value: 55000 },
    { name: 'Jun', value: 67000 },
    { name: 'Jul', value: 69000 },
    { name: 'Aug', value: 62000 },
    { name: 'Sep', value: 75000 },
    { name: 'Oct', value: 78000 },
    { name: 'Nov', value: 82000 },
    { name: 'Dec', value: 89000 }
  ]

  const userGrowthData: ChartData[] = [
    { name: 'Week 1', value: 150 },
    { name: 'Week 2', value: 189 },
    { name: 'Week 3', value: 167 },
    { name: 'Week 4', value: 203 },
    { name: 'Week 5', value: 234 },
    { name: 'Week 6', value: 267 },
    { name: 'Week 7', value: 289 },
    { name: 'Week 8', value: 312 }
  ]

  const topProducts = [
    { name: 'Premium Package', sales: 234, revenue: 12450, growth: 15.2 },
    { name: 'Standard License', sales: 187, revenue: 9350, growth: 8.7 },
    { name: 'Enterprise Suite', sales: 156, revenue: 31200, growth: 22.1 },
    { name: 'Basic Plan', sales: 89, revenue: 2670, growth: -5.3 },
    { name: 'Pro Features', sales: 67, revenue: 6700, growth: 12.8 }
  ]

  const tenantPerformance = [
    { name: 'TechCorp Inc', revenue: 45600, users: 234, growth: 18.5, status: 'excellent' },
    { name: 'StartupXYZ', revenue: 23400, users: 156, growth: 12.3, status: 'good' },
    { name: 'Enterprise Ltd', revenue: 67800, users: 345, growth: -2.1, status: 'declining' },
    { name: 'Innovation Co', revenue: 34500, users: 198, growth: 25.7, status: 'excellent' },
    { name: 'Global Systems', revenue: 56700, users: 287, growth: 8.9, status: 'good' }
  ]

  const periods = [
    { value: 'last-7-days', label: 'Last 7 Days' },
    { value: 'last-30-days', label: 'Last 30 Days' },
    { value: 'last-90-days', label: 'Last 90 Days' },
    { value: 'last-year', label: 'Last Year' },
    { value: 'custom', label: 'Custom Range' }
  ]

  const tenants = [
    { value: 'all', label: 'All Tenants' },
    { value: 'techcorp', label: 'TechCorp Inc' },
    { value: 'startupxyz', label: 'StartupXYZ' },
    { value: 'enterprise', label: 'Enterprise Ltd' }
  ]

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'excellent': return 'bg-green-100 text-green-800'
      case 'good': return 'bg-blue-100 text-blue-800'
      case 'declining': return 'bg-red-100 text-red-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  }

  const exportData = () => {
    console.log('Exporting analytics data...')
    // TODO: Implement data export functionality
  }

  const refreshData = () => {
    console.log('Refreshing analytics data...')
    // TODO: Implement data refresh functionality
  }

  return (
    <div className="space-y-6">
      {/* Header with Filters */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h2 className="text-2xl font-bold">Analytics Dashboard</h2>
          <p className="text-muted-foreground">Monitor your business performance and trends</p>
        </div>
        
        <div className="flex items-center gap-3">
          <Select value={selectedTenant} onValueChange={setSelectedTenant}>
            <SelectTrigger className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {tenants.map(tenant => (
                <SelectItem key={tenant.value} value={tenant.value}>
                  {tenant.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          
          <Select value={selectedPeriod} onValueChange={setSelectedPeriod}>
            <SelectTrigger className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {periods.map(period => (
                <SelectItem key={period.value} value={period.value}>
                  {period.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          
          <Button variant="outline" size="sm" onClick={refreshData}>
            <RefreshCw className="h-4 w-4" />
          </Button>
          
          <Button variant="outline" size="sm" onClick={exportData}>
            <Download className="h-4 w-4 mr-2" />
            Export
          </Button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {kpis.map((kpi, index) => {
          const Icon = kpi.icon
          return (
            <Card key={index}>
              <CardContent className="p-6">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <div className="p-2 bg-primary/10 rounded-lg">
                      <Icon className="h-4 w-4 text-primary" />
                    </div>
                    <h3 className="font-medium text-sm text-muted-foreground">{kpi.title}</h3>
                  </div>
                  <Button variant="ghost" size="sm">
                    <MoreHorizontal className="h-4 w-4" />
                  </Button>
                </div>
                
                <div className="mt-4">
                  <div className="text-2xl font-bold">{kpi.value}</div>
                  <div className="flex items-center gap-2 mt-2">
                    {kpi.trend === 'up' ? (
                      <div className="flex items-center text-green-600 text-sm">
                        <ArrowUpRight className="h-3 w-3 mr-1" />
                        +{kpi.change}%
                      </div>
                    ) : (
                      <div className="flex items-center text-red-600 text-sm">
                        <ArrowDownRight className="h-3 w-3 mr-1" />
                        {kpi.change}%
                      </div>
                    )}
                    <span className="text-muted-foreground text-sm">vs last month</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          )
        })}
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Revenue Chart */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <TrendingUp className="h-5 w-5" />
              Revenue Trends
            </CardTitle>
            <CardDescription>Monthly revenue performance over the last year</CardDescription>
          </CardHeader>
          <CardContent>
            {/* Placeholder for chart - in a real app, you'd use a charting library like Recharts */}
            <div className="h-64 bg-muted rounded-lg flex items-center justify-center">
              <div className="text-center text-muted-foreground">
                <TrendingUp className="h-8 w-8 mx-auto mb-2" />
                <p>Revenue Chart Placeholder</p>
                <p className="text-sm">Peak: $89,000 in December</p>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* User Growth Chart */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Users className="h-5 w-5" />
              User Growth
            </CardTitle>
            <CardDescription>New user registrations over time</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="h-64 bg-muted rounded-lg flex items-center justify-center">
              <div className="text-center text-muted-foreground">
                <Users className="h-8 w-8 mx-auto mb-2" />
                <p>User Growth Chart Placeholder</p>
                <p className="text-sm">Avg: 234 new users/week</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Data Tables Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Top Products */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Package className="h-5 w-5" />
              Top Performing Products
            </CardTitle>
            <CardDescription>Best selling products and services</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {topProducts.map((product, index) => (
                <div key={index} className="flex items-center justify-between p-3 bg-muted/50 rounded-lg">
                  <div>
                    <div className="font-medium">{product.name}</div>
                    <div className="text-sm text-muted-foreground">
                      {product.sales} sales • ${product.revenue.toLocaleString()} revenue
                    </div>
                  </div>
                  <div className="text-right">
                    <div className={`text-sm font-medium ${
                      product.growth > 0 ? 'text-green-600' : 'text-red-600'
                    }`}>
                      {product.growth > 0 ? '+' : ''}{product.growth}%
                    </div>
                    <div className="text-xs text-muted-foreground">growth</div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* Tenant Performance */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <TrendingUp className="h-5 w-5" />
              Tenant Performance
            </CardTitle>
            <CardDescription>Top performing tenant accounts</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {tenantPerformance.map((tenant, index) => (
                <div key={index} className="flex items-center justify-between p-3 bg-muted/50 rounded-lg">
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <div className="font-medium">{tenant.name}</div>
                      <Badge 
                        variant="secondary" 
                        className={getStatusColor(tenant.status)}
                      >
                        {tenant.status}
                      </Badge>
                    </div>
                    <div className="text-sm text-muted-foreground">
                      {tenant.users} users • ${tenant.revenue.toLocaleString()} revenue
                    </div>
                  </div>
                  <div className="text-right">
                    <div className={`text-sm font-medium ${
                      tenant.growth > 0 ? 'text-green-600' : 'text-red-600'
                    }`}>
                      {tenant.growth > 0 ? '+' : ''}{tenant.growth}%
                    </div>
                    <div className="text-xs text-muted-foreground">growth</div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Quick Actions */}
      <Card>
        <CardHeader>
          <CardTitle>Quick Actions</CardTitle>
          <CardDescription>Common analytics tasks and reports</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-4">
            <Button variant="outline" className="h-20 flex flex-col gap-2">
              <Calendar className="h-5 w-5" />
              <span className="text-sm">Schedule Report</span>
            </Button>
            <Button variant="outline" className="h-20 flex flex-col gap-2">
              <Download className="h-5 w-5" />
              <span className="text-sm">Export Data</span>
            </Button>
            <Button variant="outline" className="h-20 flex flex-col gap-2">
              <TrendingUp className="h-5 w-5" />
              <span className="text-sm">Create Alert</span>
            </Button>
            <Button variant="outline" className="h-20 flex flex-col gap-2">
              <Users className="h-5 w-5" />
              <span className="text-sm">User Analysis</span>
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

export default AnalyticsDashboard