"use client"

import React from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { BarChart3 } from 'lucide-react'

const NotificationAnalytics: React.FC = () => {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <BarChart3 className="h-5 w-5" />
            Notification Analytics
          </CardTitle>
          <CardDescription>Analytics and performance metrics for notifications</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="text-center py-8 text-muted-foreground">
            <BarChart3 className="h-12 w-12 mx-auto mb-4" />
            <h3 className="text-lg font-medium mb-2">Analytics coming soon</h3>
            <p>Detailed analytics and metrics will be available here.</p>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

export default NotificationAnalytics