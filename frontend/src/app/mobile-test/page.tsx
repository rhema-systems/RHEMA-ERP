"use client"

import React, { useState, useEffect } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card'
import { Button } from '../../components/ui/button'
import { Badge } from '../../components/ui/badge'
import { MobileLayout } from '../../components/mobile/MobileLayout'
import { MobileInput, MobileForm, MobileSelect, MobileSwitch, MobileTextarea } from '../../components/mobile/MobileForms'
import { SwipeableCard, PullToRefresh, TouchableOpacity, LongPress } from '../../components/mobile/TouchInteractions'
import MobileDataTable from '../../components/mobile/MobileDataTable'
import { useTouch, useMobile } from '../../components/mobile/TouchInteractions'
import { 
  Smartphone, 
  Tablet, 
  Monitor, 
  Hand, 
  Zap, 
  Layers,
  Eye,
  Edit,
  Trash2
} from 'lucide-react'

// Mock data for testing
const mockTableData = [
  { id: 1, name: 'John Doe', email: 'john@example.com', status: 'Active', role: 'Admin' },
  { id: 2, name: 'Jane Smith', email: 'jane@example.com', status: 'Inactive', role: 'User' },
  { id: 3, name: 'Bob Johnson', email: 'bob@example.com', status: 'Active', role: 'Manager' },
  { id: 4, name: 'Alice Brown', email: 'alice@example.com', status: 'Pending', role: 'User' },
  { id: 5, name: 'Charlie Wilson', email: 'charlie@example.com', status: 'Active', role: 'Admin' },
]

const columns = [
  { key: 'name', label: 'Name', mobile: { primary: true } },
  { key: 'email', label: 'Email', mobile: { primary: true } },
  { key: 'status', label: 'Status', mobile: { primary: true }, render: (value: string) => (
    <Badge variant={value === 'Active' ? 'default' : value === 'Inactive' ? 'destructive' : 'secondary'}>
      {value}
    </Badge>
  )},
  { key: 'role', label: 'Role', mobile: { secondary: true } },
]

const tableActions = [
  { label: 'View', icon: Eye, onClick: (row: any) => console.log('View:', row) },
  { label: 'Edit', icon: Edit, onClick: (row: any) => console.log('Edit:', row) },
  { label: 'Delete', icon: Trash2, onClick: (row: any) => console.log('Delete:', row), variant: 'destructive' as const },
]

export default function MobileTestPage() {
  const isTouchDevice = useTouch()
  const { isMobile: isMobileDevice } = useMobile()
  const [refreshing, setRefreshing] = useState(false)
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    message: '',
    category: '',
    notifications: false
  })
  const [longPressTriggered, setLongPressTriggered] = useState(false)
  const [windowDimensions, setWindowDimensions] = useState({ width: 0, height: 0 })
  const [deviceInfo, setDeviceInfo] = useState({ userAgent: '', platform: '', devicePixelRatio: 1 })
  const [mounted, setMounted] = useState(false)

  useEffect(() => {
    setMounted(true)
    
    const updateDimensions = () => {
      setWindowDimensions({
        width: window.innerWidth,
        height: window.innerHeight
      })
    }
    
    const updateDeviceInfo = () => {
      setDeviceInfo({
        userAgent: navigator.userAgent,
        platform: navigator.platform,
        devicePixelRatio: window.devicePixelRatio
      })
    }
    
    updateDimensions()
    updateDeviceInfo()
    
    window.addEventListener('resize', updateDimensions)
    
    return () => {
      window.removeEventListener('resize', updateDimensions)
    }
  }, [])

  const handleRefresh = async () => {
    setRefreshing(true)
    // Simulate refresh delay
    await new Promise(resolve => setTimeout(resolve, 2000))
    setRefreshing(false)
  }

  const handleSwipeLeft = () => {
    console.log('Swiped left - Archive action')
  }

  const handleSwipeRight = () => {
    console.log('Swiped right - Delete action')
  }

  const getDeviceType = () => {
    if (!mounted) return 'unknown'
    if (isMobileDevice) return 'mobile'
    if (windowDimensions.width <= 1024) return 'tablet'
    return 'desktop'
  }

  const getDeviceIcon = () => {
    const deviceType = getDeviceType()
    switch (deviceType) {
      case 'mobile': return <Smartphone className="h-4 w-4" />
      case 'tablet': return <Tablet className="h-4 w-4" />
      default: return <Monitor className="h-4 w-4" />
    }
  }

  return (
    <MobileLayout>
      <div className="container mx-auto p-4 space-y-6">
        
        {/* Header with Device Info */}
        <div className="flex items-center justify-between">
          <h1 className="text-3xl font-bold">Mobile Testing Dashboard</h1>
          <div className="flex gap-2">
            <Badge variant="outline">
              {getDeviceIcon()}
              <span className="ml-1 capitalize">{getDeviceType()}</span>
            </Badge>
            <Badge variant={isTouchDevice ? "default" : "secondary"}>
              <Hand className="h-3 w-3 mr-1" />
              {isTouchDevice ? 'Touch' : 'No Touch'}
            </Badge>
          </div>
        </div>

        {/* Device Detection */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Eye className="h-5 w-5" />
              Device Detection
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div>
                <p className="text-sm text-muted-foreground">Screen Width</p>
                <p className="font-semibold">{mounted ? `${windowDimensions.width}px` : 'Loading...'}</p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Screen Height</p>
                <p className="font-semibold">{mounted ? `${windowDimensions.height}px` : 'Loading...'}</p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Device Pixel Ratio</p>
                <p className="font-semibold">{mounted ? deviceInfo.devicePixelRatio : 'Loading...'}</p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">User Agent</p>
                <p className="font-semibold text-xs truncate">
                  {mounted ? 
                    (deviceInfo.userAgent.includes('Mobile') ? 'Mobile' : 'Desktop')
                    : 'Loading...'
                  }
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Touch Interactions Testing */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Hand className="h-5 w-5" />
              Touch Interactions Testing
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              
              {/* Pull to Refresh */}
              <div>
                <h4 className="font-semibold mb-2">Pull to Refresh</h4>
                <PullToRefresh onRefresh={handleRefresh} refreshing={refreshing}>
                  <div className="p-4 bg-muted rounded-lg">
                    <p className="text-center">
                      {refreshing ? 'Refreshing...' : 'Pull down to refresh this content'}
                    </p>
                  </div>
                </PullToRefresh>
              </div>

              {/* Swipeable Card */}
              <div>
                <h4 className="font-semibold mb-2">Swipeable Card</h4>
                <SwipeableCard
                  leftAction={{ 
                    icon: () => <span>📁</span>, 
                    label: 'Archive', 
                    action: handleSwipeLeft, 
                    color: 'blue' 
                  }}
                  rightAction={{ 
                    icon: () => <span>🗑️</span>, 
                    label: 'Delete', 
                    action: handleSwipeRight, 
                    color: 'red' 
                  }}
                >
                  <Card>
                    <CardContent className="p-4">
                      <p className="font-medium">Swipeable Item</p>
                      <p className="text-sm text-muted-foreground">
                        Swipe left to archive, right to delete
                      </p>
                    </CardContent>
                  </Card>
                </SwipeableCard>
              </div>

              {/* Touch Feedback */}
              <div>
                <h4 className="font-semibold mb-2">Touch Feedback</h4>
                <div className="flex gap-2">
                  <TouchableOpacity onPress={() => console.log('Touched!')}>
                    <div className="p-4 bg-primary text-primary-foreground rounded-lg">
                      Touch me for feedback
                    </div>
                  </TouchableOpacity>
                  
                  <LongPress
                    onLongPress={() => setLongPressTriggered(true)}
                    onPressEnd={() => setLongPressTriggered(false)}
                  >
                    <div className={`p-4 rounded-lg transition-colors ${
                      longPressTriggered ? 'bg-destructive text-destructive-foreground' : 'bg-secondary'
                    }`}>
                      Long press me
                    </div>
                  </LongPress>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Mobile Forms Testing */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Layers className="h-5 w-5" />
              Mobile Forms
            </CardTitle>
          </CardHeader>
          <CardContent>
            <MobileForm className="space-y-4">
              <MobileInput
                label="Name"
                placeholder="Enter your name"
                value={formData.name}
                onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
              />
              
              <MobileInput
                label="Email"
                type="email"
                placeholder="Enter your email"
                value={formData.email}
                onChange={(e) => setFormData(prev => ({ ...prev, email: e.target.value }))}
              />
              
              <MobileSelect
                label="Category"
                value={formData.category}
                onValueChange={(value) => setFormData(prev => ({ ...prev, category: value }))}
                options={[
                  { value: 'general', label: 'General' },
                  { value: 'support', label: 'Support' },
                  { value: 'billing', label: 'Billing' },
                ]}
                placeholder="Select a category"
              />
              
              <MobileTextarea
                label="Message"
                placeholder="Enter your message"
                value={formData.message}
                onChange={(e) => setFormData(prev => ({ ...prev, message: e.target.value }))}
                autoResize
              />
              
              <MobileSwitch
                label="Enable Notifications"
                description="Receive updates about your account"
                checked={formData.notifications}
                onCheckedChange={(checked) => setFormData(prev => ({ ...prev, notifications: checked }))}
              />
              
              <Button className="w-full" size="lg">
                Submit Form
              </Button>
            </MobileForm>
          </CardContent>
        </Card>

        {/* Mobile Data Table Testing */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Zap className="h-5 w-5" />
              Mobile Data Table
            </CardTitle>
          </CardHeader>
          <CardContent>
            <MobileDataTable
              data={mockTableData}
              columns={columns}
              actions={tableActions}
              swipeActions={{
                left: { 
                  label: 'Archive', 
                  icon: () => <span>📁</span>, 
                  action: (row) => console.log('Archive:', row), 
                  color: 'blue' 
                },
                right: { 
                  label: 'Delete', 
                  icon: () => <span>🗑️</span>, 
                  action: (row) => console.log('Delete:', row), 
                  color: 'red' 
                }
              }}
              searchable
              filterable
              filters={[
                {
                  key: 'status',
                  label: 'Status',
                  options: [
                    { value: 'Active', label: 'Active' },
                    { value: 'Inactive', label: 'Inactive' },
                    { value: 'Pending', label: 'Pending' }
                  ]
                }
              ]}
              onRowClick={(row) => console.log('Row clicked:', row)}
            />
          </CardContent>
        </Card>

        {/* Responsive Breakpoints Info */}
        <Card>
          <CardHeader>
            <CardTitle>Responsive Breakpoints</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span>Mobile (sm):</span>
                <span>≤ 640px</span>
              </div>
              <div className="flex justify-between">
                <span>Tablet (md):</span>
                <span>641px - 768px</span>
              </div>
              <div className="flex justify-between">
                <span>Laptop (lg):</span>
                <span>769px - 1024px</span>
              </div>
              <div className="flex justify-between">
                <span>Desktop (xl):</span>
                <span>≥ 1025px</span>
              </div>
              <div className="mt-4 p-3 bg-muted rounded">
                <p className="font-semibold">Current: {mounted ? `${windowDimensions.width}px` : 'Loading...'}</p>
                <p className="text-muted-foreground">
                  Resize your browser window to test different breakpoints
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </MobileLayout>
  )
}
