"use client"

import React from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { History } from 'lucide-react'

const NotificationHistory: React.FC = () => {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <History className="h-5 w-5" />
            Notification History
          </CardTitle>
          <CardDescription>View past notifications and delivery status</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="text-center py-8 text-muted-foreground">
            <History className="h-12 w-12 mx-auto mb-4" />
            <h3 className="text-lg font-medium mb-2">No history available</h3>
            <p>Notification history will appear here once notifications are sent.</p>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

export default NotificationHistory