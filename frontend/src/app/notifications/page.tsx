"use client"

import React, { useEffect, useMemo, useState } from 'react'
import { DashboardLayout } from '../../components/layout/dashboard-layout'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card'
import { Badge } from '../../components/ui/badge'
import { Button } from '../../components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { 
  Bell, 
  Mail, 
  Settings, 
  History, 
  FileText, 
  BarChart3,
  Tags,
  Search,
  Filter,
  Plus,
  Zap,
  Clock,
  CheckCircle,
  AlertCircle,
  Users,
  Building,
  Download
} from 'lucide-react'

import NotificationCenter from '../../components/notifications/NotificationCenter'
import EmailNotifications from '../../components/notifications/EmailNotifications'
import NotificationSettings from '../../components/notifications/NotificationSettings'
import NotificationTemplates from '../../components/notifications/NotificationTemplates'
import NotificationTopics from '../../components/notifications/NotificationTopics'
import NotificationHistory from '../../components/notifications/NotificationHistory'
import NotificationAnalytics from '../../components/notifications/NotificationAnalytics'
import NotificationMonitoring from '../../components/notifications/NotificationMonitoring'
import { apiService } from '../../services/api.service'
import authService from '../../services/auth'

interface NotificationStats {
  totalNotifications: number
  unreadCount: number
  emailsSent: number
  deliveryRate: number
  avgResponseTime: string
}

export default function NotificationsPage() {
  const [activeTab, setActiveTab] = useState('center')
  const [stats, setStats] = useState<NotificationStats>({
    totalNotifications: 0,
    unreadCount: 0,
    emailsSent: 0,
    deliveryRate: 0,
    avgResponseTime: '—'
  })

  const isAdmin = useMemo(() => authService.hasAnyRole(['SuperAdmin', 'TenantAdmin']), [])

  const loadStats = async () => {
    try {
      const [unreadCount, paged] = await Promise.all([
        apiService.silentRequest<number>('/notifications/unread-count'),
        apiService.silentRequest<any>('/notifications?page=1&pageSize=1'),
      ])

      const totalNotifications = typeof paged?.totalCount === 'number' ? paged.totalCount : 0

      if (!isAdmin) {
        setStats(s => ({
          ...s,
          totalNotifications,
          unreadCount,
        }))
        return
      }

      const adminStats = await apiService.silentRequest<any>('/notifications/statistics?period=last-30-days')
      const avgSeconds = typeof adminStats?.averageDeliveryTimeSeconds === 'number'
        ? adminStats.averageDeliveryTimeSeconds
        : 0

      setStats({
        totalNotifications,
        unreadCount,
        emailsSent: adminStats?.emailNotificationsSent ?? 0,
        deliveryRate: Math.round(((adminStats?.deliveryRate ?? 0) * 100) * 10) / 10,
        avgResponseTime: avgSeconds > 0 ? `${avgSeconds.toFixed(1)}s` : '—'
      })
    } catch {
      // Best-effort; keep UI usable even if stats fail to load.
    }
  }

  useEffect(() => {
    loadStats()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const deliveryRateLabel = isAdmin ? `${stats.deliveryRate}%` : '—'
  const emailsSentLabel = isAdmin ? stats.emailsSent.toLocaleString() : '—'
  const avgResponseLabel = isAdmin ? stats.avgResponseTime : '—'

  const onCreateNotificationClick = () => {
    // Placeholder – actual creation is typically module-driven.
  }

  return (
    <DashboardLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Notifications</h1>
            <p className="text-muted-foreground">
              Manage real-time alerts, email notifications, and messaging preferences
            </p>
          </div>
          
          <div className="flex items-center gap-3">
            <Button onClick={onCreateNotificationClick}>
              <Plus className="h-4 w-4 mr-2" />
              Create Notification
            </Button>
          </div>
        </div>

        {/* Statistics Cards */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-6">
          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Total Notifications</p>
                  <p className="text-2xl font-bold">{stats.totalNotifications.toLocaleString()}</p>
                </div>
                <Bell className="h-8 w-8 text-blue-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Unread</p>
                  <p className="text-2xl font-bold">{stats.unreadCount}</p>
                </div>
                <div className="relative">
                  <AlertCircle className="h-8 w-8 text-orange-600" />
                  {stats.unreadCount > 0 && (
                    <Badge 
                      variant="destructive" 
                      className="absolute -top-2 -right-2 h-5 w-5 p-0 flex items-center justify-center text-xs"
                    >
                      {stats.unreadCount > 99 ? '99+' : stats.unreadCount}
                    </Badge>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Emails Sent</p>
                  <p className="text-2xl font-bold">{emailsSentLabel}</p>
                </div>
                <Mail className="h-8 w-8 text-green-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Delivery Rate</p>
                  <p className="text-2xl font-bold">{deliveryRateLabel}</p>
                </div>
                <CheckCircle className="h-8 w-8 text-purple-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Avg Response</p>
                  <p className="text-2xl font-bold">{avgResponseLabel}</p>
                </div>
                <Zap className="h-8 w-8 text-red-600" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Main Content Tabs */}
        <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6" suppressHydrationWarning>
          <TabsList className={`grid ${isAdmin ? 'grid-cols-8' : 'grid-cols-6'} gap-4`} suppressHydrationWarning>
            <TabsTrigger value="center" className="flex items-center gap-2" suppressHydrationWarning>
              <Bell className="h-4 w-4" />
              Notification Center
            </TabsTrigger>
            <TabsTrigger value="email" className="flex items-center gap-2">
              <Mail className="h-4 w-4" />
              Email Notifications
            </TabsTrigger>
            <TabsTrigger value="templates" className="flex items-center gap-2">
              <FileText className="h-4 w-4" />
              Templates
            </TabsTrigger>
            {isAdmin ? (
              <TabsTrigger value="topics" className="flex items-center gap-2">
                <Tags className="h-4 w-4" />
                Topics / Groups
              </TabsTrigger>
            ) : null}
            {isAdmin ? (
              <TabsTrigger value="monitoring" className="flex items-center gap-2">
                <Clock className="h-4 w-4" />
                Queue Monitor
              </TabsTrigger>
            ) : null}
            <TabsTrigger value="history" className="flex items-center gap-2">
              <History className="h-4 w-4" />
              History
            </TabsTrigger>
            <TabsTrigger value="analytics" className="flex items-center gap-2">
              <BarChart3 className="h-4 w-4" />
              Analytics
            </TabsTrigger>
            <TabsTrigger value="settings" className="flex items-center gap-2">
              <Settings className="h-4 w-4" />
              Settings
            </TabsTrigger>
          </TabsList>

          <TabsContent value="center" className="space-y-6">
            <NotificationCenter />
          </TabsContent>

          <TabsContent value="email" className="space-y-6">
            <EmailNotifications />
          </TabsContent>

          <TabsContent value="templates" className="space-y-6">
            <NotificationTemplates />
          </TabsContent>

          {isAdmin ? (
            <TabsContent value="topics" className="space-y-6">
              <NotificationTopics />
            </TabsContent>
          ) : null}

          {isAdmin ? (
            <TabsContent value="monitoring" className="space-y-6">
              <NotificationMonitoring />
            </TabsContent>
          ) : null}

          <TabsContent value="history" className="space-y-6">
            <NotificationHistory />
          </TabsContent>

          <TabsContent value="analytics" className="space-y-6">
            <NotificationAnalytics />
          </TabsContent>

          <TabsContent value="settings" className="space-y-6">
            <NotificationSettings />
          </TabsContent>
        </Tabs>
      </div>
    </DashboardLayout>
  )
}
