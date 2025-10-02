"use client"

import React, { useState, useEffect } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card'
import { Button } from '../../components/ui/button'
import { Badge } from '../../components/ui/badge'
import { usePWA, useOfflineData } from '../../lib/pwa'
import { 
  Wifi, 
  WifiOff, 
  Download, 
  Bell, 
  Smartphone, 
  Database,
  RefreshCw,
  CheckCircle,
  XCircle,
  Clock,
  HardDrive,
  Zap
} from 'lucide-react'

// Extend window interface for PWA
declare global {
  interface Window {
    deferredPrompt: any
  }
}

export default function PWATestPage() {
  const pwa = usePWA()
  const offlineData = useOfflineData()
  const [installPromptShown, setInstallPromptShown] = useState(false)
  const [installPromptAvailable, setInstallPromptAvailable] = useState(false)
  const [installError, setInstallError] = useState<string | null>(null)
  const [notificationPermission, setNotificationPermission] = useState<NotificationPermission>('default')
  const [testData, setTestData] = useState<any>(null)
  const [storageInfo, setStorageInfo] = useState<StorageEstimate | null>(null)
  const [pwaDiagnostics, setPwaDiagnostics] = useState<any>({})
  const [mounted, setMounted] = useState(false)

  useEffect(() => {
    setMounted(true)
    
    // Check notification permission
    if ('Notification' in window) {
      setNotificationPermission(Notification.permission)
    }

    // Get storage info
    pwa.getStorageEstimate().then(setStorageInfo)
    
    // PWA Diagnostics
    const diagnostics = {
      isHttps: window.location.protocol === 'https:' || window.location.hostname === 'localhost',
      hasManifest: document.querySelector('link[rel="manifest"]') !== null,
      hasServiceWorker: 'serviceWorker' in navigator,
      isStandalone: window.matchMedia('(display-mode: standalone)').matches,
      userAgent: navigator.userAgent,
      platform: navigator.platform
    }
    setPwaDiagnostics(diagnostics)
    
    // Check for existing beforeinstallprompt
    const checkInstallPrompt = () => {
      if (window.deferredPrompt) {
        setInstallPromptAvailable(true)
      }
    }
    
    checkInstallPrompt()
    
    // Listen for beforeinstallprompt event
    const handleBeforeInstallPrompt = (e: Event) => {
      console.log('beforeinstallprompt event fired', e)
      setInstallPromptAvailable(true)
      window.deferredPrompt = e
    }
    
    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
    
    return () => {
      window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
    }
  }, [pwa])

  const handleInstallApp = async () => {
    try {
      setInstallError(null)
      
      if (!installPromptAvailable && !window.deferredPrompt) {
        setInstallError('Install prompt not available. This might be because:\n• App is already installed\n• Browser doesn\'t support PWA installation\n• PWA criteria not met\n• Using HTTP instead of HTTPS')
        return
      }
      
      if (window.deferredPrompt) {
        const deferredPrompt = window.deferredPrompt
        deferredPrompt.prompt()
        const { outcome } = await deferredPrompt.userChoice
        console.log('Install prompt result:', outcome)
        setInstallPromptShown(true)
        
        if (outcome === 'accepted') {
          console.log('User accepted the install prompt')
        } else {
          console.log('User dismissed the install prompt')
        }
        
        window.deferredPrompt = null
        setInstallPromptAvailable(false)
      } else {
        const result = await pwa.showInstallPrompt()
        setInstallPromptShown(true)
        console.log('PWA manager install result:', result)
      }
    } catch (error) {
      console.error('Install prompt error:', error)
      setInstallError(`Install failed: ${error instanceof Error ? error.message : String(error)}`)
    }
  }

  const handleNotificationPermission = async () => {
    const permission = await pwa.requestNotificationPermission()
    setNotificationPermission(permission)
    
    if (permission === 'granted') {
      await pwa.subscribeToNotifications()
    }
  }

  const handleTestOfflineData = async () => {
    // Store some test data
    const data = {
      message: 'This is test offline data',
      timestamp: new Date().toISOString(),
      items: ['item1', 'item2', 'item3']
    }
    
    await offlineData.storeData('test-key', data)
    
    // Retrieve it
    const retrieved = await offlineData.getData('test-key')
    setTestData(retrieved)
  }

  const handleAddPendingOperation = async () => {
    await offlineData.addPendingOperation({
      type: 'CREATE_USER',
      data: { name: 'John Doe', email: 'john@example.com' },
      url: '/api/users'
    })
    
    const operations = await offlineData.getPendingOperations()
    console.log('Pending operations:', operations)
  }

  const handleTestPushNotification = () => {
    if ('serviceWorker' in navigator && 'Notification' in window && Notification.permission === 'granted') {
      // Simulate a push notification
      new Notification('PWA Test Notification', {
        body: 'This is a test notification from your ERP PWA!',
        icon: '/icon-192.png',
        badge: '/icon-192.png',
        tag: 'pwa-test'
      })
    }
  }

  const formatBytes = (bytes: number | undefined) => {
    if (!bytes) return 'Unknown'
    const mb = bytes / (1024 * 1024)
    return `${mb.toFixed(1)} MB`
  }

  const getStatusIcon = (status: boolean) => {
    return status ? (
      <CheckCircle className="h-4 w-4 text-green-500" />
    ) : (
      <XCircle className="h-4 w-4 text-red-500" />
    )
  }

  return (
    <div className="container mx-auto p-4 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">PWA Testing Dashboard</h1>
        <Badge variant={pwa.isOnline ? "default" : "destructive"}>
          {pwa.isOnline ? (
            <>
              <Wifi className="h-3 w-3 mr-1" />
              Online
            </>
          ) : (
            <>
              <WifiOff className="h-3 w-3 mr-1" />
              Offline
            </>
          )}
        </Badge>
      </div>

      {/* PWA Features Status */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        
        {/* Service Worker Status */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-lg flex items-center gap-2">
              <Zap className="h-5 w-5" />
              Service Worker
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span>Registered</span>
                {getStatusIcon('serviceWorker' in navigator)}
              </div>
              <div className="flex items-center justify-between">
                <span>Update Available</span>
                {getStatusIcon(pwa.isUpdateAvailable)}
              </div>
              {pwa.isUpdateAvailable && (
                <Button onClick={pwa.applyUpdate} size="sm" className="w-full">
                  <RefreshCw className="h-3 w-3 mr-1" />
                  Apply Update
                </Button>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Installation Status */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-lg flex items-center gap-2">
              <Smartphone className="h-5 w-5" />
              App Installation
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <span>Installable</span>
                {getStatusIcon(true)}
              </div>
              <Button onClick={handleInstallApp} size="sm" className="w-full">
                <Download className="h-3 w-3 mr-1" />
                Install App
              </Button>
              {installPromptShown && (
                <p className="text-xs text-muted-foreground">
                  Install prompt was triggered. Check your browser's address bar.
                </p>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Notifications */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-lg flex items-center gap-2">
              <Bell className="h-5 w-5" />
              Notifications
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <span>Permission</span>
                <Badge variant={notificationPermission === 'granted' ? 'default' : 'secondary'}>
                  {notificationPermission}
                </Badge>
              </div>
              {notificationPermission !== 'granted' ? (
                <Button onClick={handleNotificationPermission} size="sm" className="w-full">
                  Enable Notifications
                </Button>
              ) : (
                <Button onClick={handleTestPushNotification} size="sm" className="w-full">
                  Test Notification
                </Button>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* PWA Diagnostics */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Zap className="h-5 w-5" />
            PWA Diagnostics
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span>HTTPS/Localhost</span>
                {getStatusIcon(pwaDiagnostics.isHttps)}
              </div>
              <div className="flex items-center justify-between">
                <span>Manifest</span>
                {getStatusIcon(pwaDiagnostics.hasManifest)}
              </div>
              <div className="flex items-center justify-between">
                <span>Service Worker Support</span>
                {getStatusIcon(pwaDiagnostics.hasServiceWorker)}
              </div>
              <div className="flex items-center justify-between">
                <span>Install Prompt Available</span>
                {getStatusIcon(installPromptAvailable)}
              </div>
            </div>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span>Standalone Mode</span>
                {getStatusIcon(pwaDiagnostics.isStandalone)}
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Platform</p>
                <p className="font-semibold text-xs">{pwaDiagnostics.platform}</p>
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Browser</p>
                <p className="font-semibold text-xs truncate">
                  {pwaDiagnostics.userAgent?.includes('Chrome') ? 'Chrome' : 
                   pwaDiagnostics.userAgent?.includes('Firefox') ? 'Firefox' : 
                   pwaDiagnostics.userAgent?.includes('Safari') ? 'Safari' : 
                   pwaDiagnostics.userAgent?.includes('Edge') ? 'Edge' : 'Other'}
                </p>
              </div>
            </div>
          </div>
          
          {installError && (
            <div className="mt-4 p-3 bg-red-50 border border-red-200 rounded-lg">
              <p className="text-sm font-semibold text-red-800 mb-1">Install Error:</p>
              <pre className="text-xs text-red-700 whitespace-pre-wrap">{installError}</pre>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Storage Information */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <HardDrive className="h-5 w-5" />
            Storage Information
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div>
              <p className="text-sm text-muted-foreground">Used Storage</p>
              <p className="font-semibold">{formatBytes(storageInfo?.usage)}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Available Storage</p>
              <p className="font-semibold">{formatBytes(storageInfo?.quota)}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Usage %</p>
              <p className="font-semibold">
                {storageInfo?.usage && storageInfo?.quota 
                  ? `${((storageInfo.usage / storageInfo.quota) * 100).toFixed(1)}%`
                  : 'Unknown'
                }
              </p>
            </div>
            <div>
              <Button onClick={() => pwa.requestPersistentStorage()} size="sm">
                Request Persistent
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Offline Data Testing */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Database className="h-5 w-5" />
            Offline Data Testing
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="flex gap-2">
              <Button onClick={handleTestOfflineData} size="sm">
                Store Test Data
              </Button>
              <Button onClick={handleAddPendingOperation} size="sm" variant="outline">
                Add Pending Operation
              </Button>
            </div>
            
            {testData && (
              <div className="p-3 bg-muted rounded-lg">
                <p className="text-sm font-semibold mb-2">Retrieved Offline Data:</p>
                <pre className="text-xs overflow-auto">
                  {JSON.stringify(testData, null, 2)}
                </pre>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Network Status */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            {pwa.isOnline ? (
              <Wifi className="h-5 w-5 text-green-500" />
            ) : (
              <WifiOff className="h-5 w-5 text-red-500" />
            )}
            Network Status
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span>Connection Status</span>
              <Badge variant={pwa.isOnline ? "default" : "destructive"}>
                {pwa.isOnline ? 'Connected' : 'Disconnected'}
              </Badge>
            </div>
            <p className="text-sm text-muted-foreground">
              Try disconnecting your internet to test offline functionality.
              The app should continue working and show cached content.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}