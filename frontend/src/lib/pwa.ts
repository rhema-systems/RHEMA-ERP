"use client"

// PWA utilities for service worker registration and management
export class PWAManager {
  private static instance: PWAManager
  private serviceWorkerRegistration: ServiceWorkerRegistration | null = null
  private isOnline = true
  private updateAvailable = false
  private onlineHandlers: (() => void)[] = []
  private offlineHandlers: (() => void)[] = []
  private updateHandlers: (() => void)[] = []

  static getInstance(): PWAManager {
    if (!PWAManager.instance) {
      PWAManager.instance = new PWAManager()
    }
    return PWAManager.instance
  }

  async init() {
    if (typeof window !== 'undefined') {
      // Check if service workers are supported
      if ('serviceWorker' in navigator) {
        try {
          await this.registerServiceWorker()
        } catch (error) {
          console.error('Failed to register service worker:', error)
        }
      }

      // Set up online/offline listeners
      this.setupNetworkListeners()
      
      // Set up beforeinstallprompt listener for PWA installation
      this.setupInstallPrompt()
    }
  }

  private async registerServiceWorker() {
    try {
      const registration = await navigator.serviceWorker.register('/sw.js', {
        scope: '/'
      })
      
      this.serviceWorkerRegistration = registration
      console.log('Service Worker registered:', registration)

      // Listen for updates
      registration.addEventListener('updatefound', () => {
        const newWorker = registration.installing
        if (newWorker) {
          newWorker.addEventListener('statechange', () => {
            if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
              // New service worker is installed and ready
              this.updateAvailable = true
              this.notifyUpdateHandlers()
            }
          })
        }
      })

      // Listen for controlling service worker changes
      navigator.serviceWorker.addEventListener('controllerchange', () => {
        window.location.reload()
      })

    } catch (error) {
      console.error('Service Worker registration failed:', error)
    }
  }

  private setupNetworkListeners() {
    this.isOnline = navigator.onLine

    window.addEventListener('online', () => {
      this.isOnline = true
      this.notifyOnlineHandlers()
      this.syncOfflineData()
    })

    window.addEventListener('offline', () => {
      this.isOnline = false
      this.notifyOfflineHandlers()
    })
  }

  private setupInstallPrompt() {
    let deferredPrompt: any = null

    window.addEventListener('beforeinstallprompt', (e) => {
      // Prevent Chrome 67 and earlier from automatically showing the prompt
      e.preventDefault()
      // Stash the event so it can be triggered later
      deferredPrompt = e
    })

    // Store the prompt for later use
    ;(window as any).showInstallPrompt = async () => {
      if (deferredPrompt) {
        deferredPrompt.prompt()
        const { outcome } = await deferredPrompt.userChoice
        console.log(`User response to install prompt: ${outcome}`)
        deferredPrompt = null
        return outcome === 'accepted'
      }
      return false
    }
  }

  // Public API methods
  isAppOnline(): boolean {
    return this.isOnline
  }

  isUpdateAvailable(): boolean {
    return this.updateAvailable
  }

  async applyUpdate(): Promise<void> {
    if (this.serviceWorkerRegistration?.waiting) {
      this.serviceWorkerRegistration.waiting.postMessage({ type: 'SKIP_WAITING' })
    }
  }

  async requestPersistentStorage(): Promise<boolean> {
    if ('storage' in navigator && 'persist' in navigator.storage) {
      const granted = await navigator.storage.persist()
      console.log(`Persistent storage: ${granted ? 'granted' : 'denied'}`)
      return granted
    }
    return false
  }

  async getStorageEstimate(): Promise<StorageEstimate | null> {
    if ('storage' in navigator && 'estimate' in navigator.storage) {
      return await navigator.storage.estimate()
    }
    return null
  }

  async showInstallPrompt(): Promise<boolean> {
    if ((window as any).showInstallPrompt) {
      return await (window as any).showInstallPrompt()
    }
    return false
  }

  async requestNotificationPermission(): Promise<NotificationPermission> {
    if ('Notification' in window) {
      const permission = await Notification.requestPermission()
      console.log(`Notification permission: ${permission}`)
      return permission
    }
    return 'denied'
  }

  async subscribeToNotifications(): Promise<PushSubscription | null> {
    if (this.serviceWorkerRegistration && 'PushManager' in window) {
      try {
        const subscription = await this.serviceWorkerRegistration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: this.urlBase64ToUint8Array(
            process.env.NEXT_PUBLIC_VAPID_PUBLIC_KEY || ''
          )
        })
        console.log('Push subscription successful:', subscription)
        return subscription
      } catch (error) {
        console.error('Push subscription failed:', error)
        return null
      }
    }
    return null
  }

  async unsubscribeFromNotifications(): Promise<boolean> {
    if (this.serviceWorkerRegistration) {
      const subscription = await this.serviceWorkerRegistration.pushManager.getSubscription()
      if (subscription) {
        const result = await subscription.unsubscribe()
        console.log('Push unsubscription result:', result)
        return result
      }
    }
    return false
  }

  private syncOfflineData() {
    // Trigger background sync when back online
    if (this.serviceWorkerRegistration && 'sync' in window.ServiceWorkerRegistration.prototype) {
      this.serviceWorkerRegistration.sync.register('background-sync')
    }
  }

  private urlBase64ToUint8Array(base64String: string): Uint8Array {
    const padding = '='.repeat((4 - base64String.length % 4) % 4)
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
    const rawData = window.atob(base64)
    const outputArray = new Uint8Array(rawData.length)
    
    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i)
    }
    return outputArray
  }

  // Event handlers
  onOnline(handler: () => void) {
    this.onlineHandlers.push(handler)
  }

  onOffline(handler: () => void) {
    this.offlineHandlers.push(handler)
  }

  onUpdateAvailable(handler: () => void) {
    this.updateHandlers.push(handler)
  }

  private notifyOnlineHandlers() {
    this.onlineHandlers.forEach(handler => handler())
  }

  private notifyOfflineHandlers() {
    this.offlineHandlers.forEach(handler => handler())
  }

  private notifyUpdateHandlers() {
    this.updateHandlers.forEach(handler => handler())
  }
}

// React hook for PWA functionality
export const usePWA = () => {
  const pwa = PWAManager.getInstance()
  
  return {
    isOnline: pwa.isAppOnline(),
    isUpdateAvailable: pwa.isUpdateAvailable(),
    applyUpdate: () => pwa.applyUpdate(),
    showInstallPrompt: () => pwa.showInstallPrompt(),
    requestNotificationPermission: () => pwa.requestNotificationPermission(),
    subscribeToNotifications: () => pwa.subscribeToNotifications(),
    unsubscribeFromNotifications: () => pwa.unsubscribeFromNotifications(),
    requestPersistentStorage: () => pwa.requestPersistentStorage(),
    getStorageEstimate: () => pwa.getStorageEstimate(),
    onOnline: (handler: () => void) => pwa.onOnline(handler),
    onOffline: (handler: () => void) => pwa.onOffline(handler),
    onUpdateAvailable: (handler: () => void) => pwa.onUpdateAvailable(handler)
  }
}

// Utility functions for offline data management
export class OfflineDataManager {
  private static instance: OfflineDataManager
  private dbName = 'erp-system-offline'
  private version = 1
  private db: IDBDatabase | null = null

  static getInstance(): OfflineDataManager {
    if (!OfflineDataManager.instance) {
      OfflineDataManager.instance = new OfflineDataManager()
    }
    return OfflineDataManager.instance
  }

  async init(): Promise<void> {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.dbName, this.version)
      
      request.onerror = () => reject(request.error)
      request.onsuccess = () => {
        this.db = request.result
        resolve()
      }
      
      request.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result
        
        // Create object stores for offline data
        if (!db.objectStoreNames.contains('cache')) {
          db.createObjectStore('cache', { keyPath: 'key' })
        }
        
        if (!db.objectStoreNames.contains('pending_operations')) {
          db.createObjectStore('pending_operations', { 
            keyPath: 'id', 
            autoIncrement: true 
          })
        }
      }
    })
  }

  async storeData(key: string, data: any): Promise<void> {
    if (!this.db) await this.init()
    
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction(['cache'], 'readwrite')
      const store = transaction.objectStore('cache')
      
      const request = store.put({
        key,
        data,
        timestamp: Date.now()
      })
      
      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }

  async getData(key: string): Promise<any> {
    if (!this.db) await this.init()
    
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction(['cache'], 'readonly')
      const store = transaction.objectStore('cache')
      
      const request = store.get(key)
      
      request.onsuccess = () => {
        const result = request.result
        resolve(result ? result.data : null)
      }
      request.onerror = () => reject(request.error)
    })
  }

  async addPendingOperation(operation: any): Promise<void> {
    if (!this.db) await this.init()
    
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction(['pending_operations'], 'readwrite')
      const store = transaction.objectStore('pending_operations')
      
      const request = store.add({
        ...operation,
        timestamp: Date.now()
      })
      
      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }

  async getPendingOperations(): Promise<any[]> {
    if (!this.db) await this.init()
    
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction(['pending_operations'], 'readonly')
      const store = transaction.objectStore('pending_operations')
      
      const request = store.getAll()
      
      request.onsuccess = () => resolve(request.result || [])
      request.onerror = () => reject(request.error)
    })
  }

  async clearPendingOperations(): Promise<void> {
    if (!this.db) await this.init()
    
    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction(['pending_operations'], 'readwrite')
      const store = transaction.objectStore('pending_operations')
      
      const request = store.clear()
      
      request.onsuccess = () => resolve()
      request.onerror = () => reject(request.error)
    })
  }
}

// React hook for offline data management
export const useOfflineData = () => {
  const dataManager = OfflineDataManager.getInstance()
  
  return {
    storeData: (key: string, data: any) => dataManager.storeData(key, data),
    getData: (key: string) => dataManager.getData(key),
    addPendingOperation: (operation: any) => dataManager.addPendingOperation(operation),
    getPendingOperations: () => dataManager.getPendingOperations(),
    clearPendingOperations: () => dataManager.clearPendingOperations()
  }
}