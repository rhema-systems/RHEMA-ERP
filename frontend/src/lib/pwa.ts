"use client"

type InstallPromptOutcome = 'accepted' | 'dismissed' | 'unavailable'

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
}

// PWA utilities for service worker registration and management
export class PWAManager {
  private static instance: PWAManager
  private serviceWorkerRegistration: ServiceWorkerRegistration | null = null
  private isOnline = true
  private updateAvailable = false
  private onlineHandlers: (() => void)[] = []
  private offlineHandlers: (() => void)[] = []
  private updateHandlers: (() => void)[] = []
  private deferredInstallPrompt: BeforeInstallPromptEvent | null = null
  private installPromptListenerReady = false
  private controllerChangeListenerReady = false

  static getInstance(): PWAManager {
    if (!PWAManager.instance) {
      PWAManager.instance = new PWAManager()
    }
    return PWAManager.instance
  }

  async init() {
    if (typeof window !== 'undefined') {
      // Capture this one-time browser event before service-worker registration awaits.
      this.setupInstallPrompt()

      if (this.shouldDisableServiceWorker()) {
        await this.unregisterServiceWorkers()
        this.setupNetworkListeners()
        return
      }

      // Check if service workers are supported
      if ('serviceWorker' in navigator) {
        try {
          await this.registerServiceWorker()
          if (this.isMobileSurface()) {
            await this.warmMobileShell()
            void this.requestPersistentStorage()
          }
        } catch (error) {
          console.error('Failed to register service worker:', error)
        }
      }

      // Set up online/offline listeners
      this.setupNetworkListeners()
      
    }
  }

  private shouldDisableServiceWorker() {
    if (process.env.NODE_ENV !== 'production') {
      return true
    }

    const localHosts = new Set(['localhost', '127.0.0.1', '::1'])
    return localHosts.has(window.location.hostname)
  }

  private isMobileSurface() {
    return window.location.pathname === '/mobile' || window.location.pathname.startsWith('/mobile/') ||
      window.location.pathname === '/inventory/mobile-scanning'
  }

  private async unregisterServiceWorkers() {
    if (!('serviceWorker' in navigator)) {
      return
    }

    try {
      const registrations = await navigator.serviceWorker.getRegistrations()
      await Promise.all(registrations.map((registration) => registration.unregister()))

      if ('caches' in window) {
        const cacheNames = await caches.keys()
        await Promise.all(
          cacheNames
            .filter((cacheName) => cacheName.startsWith('erp-'))
            .map((cacheName) => caches.delete(cacheName))
        )
      }
    } catch (error) {
      console.error('Failed to clear development service worker state:', error)
    }
  }

  private async registerServiceWorker() {
    const serviceWorker = navigator.serviceWorker
    if (!serviceWorker) return

    try {
      if (!this.controllerChangeListenerReady) {
        this.controllerChangeListenerReady = true
        serviceWorker.addEventListener('controllerchange', () => {
          // skipWaiting + clients.claim can otherwise trigger repeated reloads
          // when more than one initialization path observes the same update.
          const reloadKey = 'erp-sw-controller-reload-at'
          const lastReloadAt = Number(window.sessionStorage.getItem(reloadKey) || 0)
          if (Date.now() - lastReloadAt < 10_000) return

          window.sessionStorage.setItem(reloadKey, String(Date.now()))
          window.location.reload()
        })
      }

      const registration = await serviceWorker.register('/sw.js', {
        scope: '/',
        updateViaCache: 'none'
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

      // Do not wait for the browser's normal service-worker update interval.
      // Every ERP bootstrap must discover a newly deployed release promptly.
      await registration.update()

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
      if (this.isMobileSurface()) {
        void this.warmMobileShell()
      }
    })

    window.addEventListener('offline', () => {
      this.isOnline = false
      this.notifyOfflineHandlers()
    })
  }

  private setupInstallPrompt() {
    if (this.installPromptListenerReady) return
    this.installPromptListenerReady = true

    window.addEventListener('beforeinstallprompt', (event) => {
      event.preventDefault()
      this.deferredInstallPrompt = event as BeforeInstallPromptEvent
    })

    window.addEventListener('appinstalled', () => {
      this.deferredInstallPrompt = null
    })
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

  async requestInstall(): Promise<InstallPromptOutcome> {
    const prompt = this.deferredInstallPrompt
    if (!prompt) return 'unavailable'

    this.deferredInstallPrompt = null
    await prompt.prompt()
    const { outcome } = await prompt.userChoice
    return outcome
  }

  async showInstallPrompt(): Promise<boolean> {
    return await this.requestInstall() === 'accepted'
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
    if (this.serviceWorkerRegistration && 'sync' in this.serviceWorkerRegistration) {
      void (this.serviceWorkerRegistration as ServiceWorkerRegistration & {
        sync: { register: (tag: string) => Promise<void> }
      }).sync.register('background-sync')
    }
  }

  private async warmMobileShell() {
    if (!this.isMobileSurface() || !this.serviceWorkerRegistration || !navigator.onLine) return

    const registration = await navigator.serviceWorker.ready
    const worker = registration.active || navigator.serviceWorker.controller
    worker?.postMessage({ type: 'WARM_MOBILE_SHELL' })
  }

  private urlBase64ToUint8Array(base64String: string): ArrayBuffer {
    const padding = '='.repeat((4 - base64String.length % 4) % 4)
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
    const rawData = window.atob(base64)
    const outputArray = new Uint8Array(new ArrayBuffer(rawData.length))
    
    for (let i = 0; i < rawData.length; ++i) {
      outputArray[i] = rawData.charCodeAt(i)
    }
    return outputArray.buffer
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
    if (!this.db) throw new Error('Offline database is not initialized')
    const db = this.db
    
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(['cache'], 'readwrite')
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
    if (!this.db) throw new Error('Offline database is not initialized')
    const db = this.db
    
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(['cache'], 'readonly')
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
    if (!this.db) throw new Error('Offline database is not initialized')
    const db = this.db
    
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(['pending_operations'], 'readwrite')
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
    if (!this.db) throw new Error('Offline database is not initialized')
    const db = this.db
    
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(['pending_operations'], 'readonly')
      const store = transaction.objectStore('pending_operations')
      
      const request = store.getAll()
      
      request.onsuccess = () => resolve(request.result || [])
      request.onerror = () => reject(request.error)
    })
  }

  async clearPendingOperations(): Promise<void> {
    if (!this.db) await this.init()
    if (!this.db) throw new Error('Offline database is not initialized')
    const db = this.db
    
    return new Promise((resolve, reject) => {
      const transaction = db.transaction(['pending_operations'], 'readwrite')
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
