"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Badge } from '../ui/badge'
import { Button } from '../ui/button'
import { Progress } from '../ui/progress'
import MobileLayout from './MobileLayout'
import { TouchableOpacity, PullToRefresh, SwipeableCard, useMobile } from './TouchInteractions'
import { 
  TrendingUp, 
  TrendingDown, 
  Users, 
  DollarSign, 
  ShoppingCart, 
  Package, 
  Bell,
  Plus,
  ArrowRight,
  Eye,
  Trash2,
  Archive
} from 'lucide-react'

interface DashboardStats {
  title: string
  value: string
  change: number
  trend: 'up' | 'down'
  icon: React.ElementType
  color: string
}

interface QuickAction {
  title: string
  icon: React.ElementType
  color: string
  path: string
}

interface RecentActivity {
  id: string
  title: string
  description: string
  timestamp: string
  type: 'user' | 'order' | 'payment' | 'system'
  status: 'success' | 'warning' | 'error' | 'info'
}

const MobileDashboard: React.FC = () => {
  const { isMobile } = useMobile()
  const [refreshing, setRefreshing] = useState(false)

  const stats: DashboardStats[] = [
    {
      title: 'Revenue',
      value: '$12.4K',
      change: 12.5,
      trend: 'up',
      icon: DollarSign,
      color: 'text-green-600'
    },
    {
      title: 'Users',
      value: '2,345',
      change: 8.2,
      trend: 'up',
      icon: Users,
      color: 'text-blue-600'
    },
    {
      title: 'Orders',
      value: '1,582',
      change: -3.1,
      trend: 'down',
      icon: ShoppingCart,
      color: 'text-orange-600'
    },
    {
      title: 'Inventory',
      value: '89.4%',
      change: 5.7,
      trend: 'up',
      icon: Package,
      color: 'text-purple-600'
    }
  ]

  const quickActions: QuickAction[] = [
    { title: 'New Order', icon: Plus, color: 'bg-blue-500', path: '/orders/new' },
    { title: 'Add User', icon: Users, color: 'bg-green-500', path: '/users/new' },
    { title: 'Inventory', icon: Package, color: 'bg-purple-500', path: '/inventory' },
    { title: 'Reports', icon: TrendingUp, color: 'bg-orange-500', path: '/reports' }
  ]

  const recentActivity: RecentActivity[] = [
    {
      id: '1',
      title: 'New Order #1234',
      description: 'Order placed by John Smith',
      timestamp: '2 min ago',
      type: 'order',
      status: 'success'
    },
    {
      id: '2',
      title: 'Payment Processed',
      description: '$2,500 from TechCorp Inc',
      timestamp: '15 min ago',
      type: 'payment',
      status: 'success'
    },
    {
      id: '3',
      title: 'Low Stock Alert',
      description: 'Widget Premium (5 units left)',
      timestamp: '30 min ago',
      type: 'system',
      status: 'warning'
    },
    {
      id: '4',
      title: 'User Registration',
      description: 'Sarah Johnson joined',
      timestamp: '1 hour ago',
      type: 'user',
      status: 'info'
    }
  ]

  const handleRefresh = async () => {
    setRefreshing(true)
    // Simulate API call
    await new Promise(resolve => setTimeout(resolve, 2000))
    setRefreshing(false)
  }

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'success': return 'bg-green-100 text-green-800'
      case 'warning': return 'bg-yellow-100 text-yellow-800'
      case 'error': return 'bg-red-100 text-red-800'
      case 'info': return 'bg-blue-100 text-blue-800'
      default: return 'bg-gray-100 text-gray-800'
    }
  }

  const handleSwipeArchive = (activityId: string) => {
    console.log('Archive activity:', activityId)
  }

  const handleSwipeDelete = (activityId: string) => {
    console.log('Delete activity:', activityId)
  }

  if (!isMobile) {
    return (
      <div className="p-6">
        <h1 className="text-2xl font-bold mb-6">Dashboard</h1>
        <p className="text-muted-foreground">This is the desktop view. Switch to mobile to see the mobile dashboard.</p>
      </div>
    )
  }

  return (
    <MobileLayout 
      title="Dashboard" 
      showSearch={true}
      rightAction={
        <Button size="sm" variant="ghost" className="p-2 h-auto">
          <Bell className="h-5 w-5" />
        </Button>
      }
    >
      <PullToRefresh onRefresh={handleRefresh} className="min-h-full">
        <div className="p-4 space-y-6">
          {/* Stats Grid */}
          <div className="grid grid-cols-2 gap-4">
            {stats.map((stat, index) => {
              const Icon = stat.icon
              return (
                <TouchableOpacity key={index} activeOpacity={0.8}>
                  <Card>
                    <CardContent className="p-4">
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-sm font-medium text-muted-foreground">
                            {stat.title}
                          </p>
                          <p className="text-2xl font-bold">{stat.value}</p>
                          <div className="flex items-center gap-1 mt-1">
                            {stat.trend === 'up' ? (
                              <TrendingUp className="h-3 w-3 text-green-600" />
                            ) : (
                              <TrendingDown className="h-3 w-3 text-red-600" />
                            )}
                            <span className={`text-xs font-medium ${
                              stat.trend === 'up' ? 'text-green-600' : 'text-red-600'
                            }`}>
                              {stat.change > 0 ? '+' : ''}{stat.change}%
                            </span>
                          </div>
                        </div>
                        <div className={`p-2 rounded-lg bg-muted ${stat.color}`}>
                          <Icon className="h-5 w-5" />
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                </TouchableOpacity>
              )
            })}
          </div>

          {/* Quick Actions */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-lg">Quick Actions</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-4 gap-3">
                {quickActions.map((action, index) => {
                  const Icon = action.icon
                  return (
                    <TouchableOpacity key={index} activeOpacity={0.8}>
                      <div className="flex flex-col items-center gap-2">
                        <div className={`w-12 h-12 ${action.color} rounded-xl flex items-center justify-center`}>
                          <Icon className="h-6 w-6 text-white" />
                        </div>
                        <span className="text-xs font-medium text-center">
                          {action.title}
                        </span>
                      </div>
                    </TouchableOpacity>
                  )
                })}
              </div>
            </CardContent>
          </Card>

          {/* Progress Cards */}
          <div className="space-y-3">
            <TouchableOpacity activeOpacity={0.9}>
              <Card>
                <CardContent className="p-4">
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-medium">Sales Target</span>
                    <span className="text-sm text-muted-foreground">78%</span>
                  </div>
                  <Progress value={78} className="h-2" />
                  <p className="text-xs text-muted-foreground mt-2">
                    $78K of $100K monthly target
                  </p>
                </CardContent>
              </Card>
            </TouchableOpacity>

            <TouchableOpacity activeOpacity={0.9}>
              <Card>
                <CardContent className="p-4">
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-medium">Storage Used</span>
                    <span className="text-sm text-muted-foreground">64%</span>
                  </div>
                  <Progress value={64} className="h-2" />
                  <p className="text-xs text-muted-foreground mt-2">
                    6.4GB of 10GB used
                  </p>
                </CardContent>
              </Card>
            </TouchableOpacity>
          </div>

          {/* Recent Activity */}
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <CardTitle className="text-lg">Recent Activity</CardTitle>
                <Button variant="ghost" size="sm" className="text-xs h-auto p-1">
                  View All
                  <ArrowRight className="h-3 w-3 ml-1" />
                </Button>
              </div>
            </CardHeader>
            <CardContent className="space-y-3 px-0">
              {recentActivity.map((activity) => (
                <SwipeableCard
                  key={activity.id}
                  onSwipeRight={() => handleSwipeArchive(activity.id)}
                  onSwipeLeft={() => handleSwipeDelete(activity.id)}
                  leftAction={{
                    icon: Archive,
                    label: 'Archive',
                    color: 'bg-blue-500'
                  }}
                  rightAction={{
                    icon: Trash2,
                    label: 'Delete',
                    color: 'bg-red-500'
                  }}
                >
                  <div className="flex items-start gap-3 p-4 bg-background">
                    <div className="w-2 h-2 rounded-full bg-primary mt-2" />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex-1">
                          <h4 className="font-medium text-sm">{activity.title}</h4>
                          <p className="text-sm text-muted-foreground mt-1">
                            {activity.description}
                          </p>
                        </div>
                        <div className="flex flex-col items-end gap-1">
                          <span className="text-xs text-muted-foreground">
                            {activity.timestamp}
                          </span>
                          <Badge variant="secondary" className={getStatusColor(activity.status)}>
                            {activity.status}
                          </Badge>
                        </div>
                      </div>
                    </div>
                  </div>
                </SwipeableCard>
              ))}
            </CardContent>
          </Card>

          {/* Bottom Padding for Navigation */}
          <div className="h-4" />
        </div>
      </PullToRefresh>
    </MobileLayout>
  )
}

export default MobileDashboard