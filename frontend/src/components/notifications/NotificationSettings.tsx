"use client"

import React, { useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card'
import { Switch } from '../ui/switch'
import { Label } from '../ui/label'
import { Button } from '../ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../ui/select'
import { Badge } from '../ui/badge'
import { Bell, Mail, Phone, Settings, Save } from 'lucide-react'

const NotificationSettings: React.FC = () => {
  const [settings, setSettings] = useState({
    inApp: {
      userActivity: true,
      systemAlerts: true,
      financialUpdates: true,
      tenantNotifications: false,
    },
    email: {
      userActivity: false,
      systemAlerts: true,
      financialUpdates: true,
      tenantNotifications: true,
    },
    sms: {
      userActivity: false,
      systemAlerts: false,
      financialUpdates: false,
      tenantNotifications: false,
    }
  })

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Settings className="h-5 w-5" />
            Notification Preferences
          </CardTitle>
          <CardDescription>Configure how and when you receive notifications</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* In-App Notifications */}
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Bell className="h-5 w-5" />
              <h3 className="text-lg font-medium">In-App Notifications</h3>
              <Badge variant="secondary">Real-time</Badge>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 ml-7">
              {Object.entries(settings.inApp).map(([key, value]) => (
                <div key={key} className="flex items-center justify-between p-3 border rounded-lg">
                  <Label htmlFor={`inapp-${key}`} className="capitalize">
                    {key.replace(/([A-Z])/g, ' $1')}
                  </Label>
                  <Switch
                    id={`inapp-${key}`}
                    checked={value}
                    onCheckedChange={(checked) => 
                      setSettings(prev => ({
                        ...prev,
                        inApp: { ...prev.inApp, [key]: checked }
                      }))
                    }
                  />
                </div>
              ))}
            </div>
          </div>

          {/* Email Notifications */}
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Mail className="h-5 w-5" />
              <h3 className="text-lg font-medium">Email Notifications</h3>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 ml-7">
              {Object.entries(settings.email).map(([key, value]) => (
                <div key={key} className="flex items-center justify-between p-3 border rounded-lg">
                  <Label htmlFor={`email-${key}`} className="capitalize">
                    {key.replace(/([A-Z])/g, ' $1')}
                  </Label>
                  <Switch
                    id={`email-${key}`}
                    checked={value}
                    onCheckedChange={(checked) => 
                      setSettings(prev => ({
                        ...prev,
                        email: { ...prev.email, [key]: checked }
                      }))
                    }
                  />
                </div>
              ))}
            </div>
          </div>

          {/* SMS Notifications */}
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Phone className="h-5 w-5" />
              <h3 className="text-lg font-medium">SMS Notifications</h3>
              <Badge variant="outline">Premium</Badge>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 ml-7">
              {Object.entries(settings.sms).map(([key, value]) => (
                <div key={key} className="flex items-center justify-between p-3 border rounded-lg">
                  <Label htmlFor={`sms-${key}`} className="capitalize">
                    {key.replace(/([A-Z])/g, ' $1')}
                  </Label>
                  <Switch
                    id={`sms-${key}`}
                    checked={value}
                    onCheckedChange={(checked) => 
                      setSettings(prev => ({
                        ...prev,
                        sms: { ...prev.sms, [key]: checked }
                      }))
                    }
                  />
                </div>
              ))}
            </div>
          </div>

          <Button>
            <Save className="h-4 w-4 mr-2" />
            Save Preferences
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}

export default NotificationSettings