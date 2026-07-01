 
// Service Worker for ERP System PWA
const CACHE_NAME = 'erp-system-v2026-07-01-asset-grid-fix'
const STATIC_CACHE_NAME = 'erp-static-v2026-07-01-asset-grid-fix'
const RUNTIME_CACHE_NAME = 'erp-runtime-v2026-07-01-asset-grid-fix'
const OFFLINE_PAGE = '/offline'
const LOCAL_DEV_HOSTS = new Set(['localhost', '127.0.0.1', '::1'])
const IS_LOCAL_DEV = LOCAL_DEV_HOSTS.has(self.location.hostname)

// Define what to cache during install
const STATIC_ASSETS = [
  '/',
  '/login',
  '/mobile',
  '/mobile/fleet/inspection',
  '/dashboard',
  '/offline',
  '/manifest.json',
  '/favicon.ico',
  '/icon-192.png',
  '/icon-512.png'
]

// Define runtime caching strategies
const RUNTIME_CACHE_URLS = [
  /^https:\/\/fonts\.googleapis\.com/,
  /^https:\/\/fonts\.gstatic\.com/,
  /^https:\/\/api\.dicebear\.com/, // Avatar service
]

// API endpoints that should be cached with network-first strategy
const API_CACHE_URLS = [
  /^\/api\/users/,
  /^\/api\/dashboard/,
  /^\/api\/settings/,
  /^\/api\/customers/,
  /^\/api\/products/,
  /^\/api\/orders/,
  /^\/api\/inventory/,
]

// Install event - cache static assets
self.addEventListener('install', (event) => {
  console.log('[SW] Install event')
  
  event.waitUntil(
    Promise.all([
      IS_LOCAL_DEV ? Promise.resolve() : precacheStaticAssets(),
      // Skip waiting to activate immediately
      self.skipWaiting()
    ])
  )
})

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
  console.log('[SW] Activate event')
  
  event.waitUntil(
    Promise.all([
      // Clean up old caches
      caches.keys().then((cacheNames) => {
        return Promise.all(
          cacheNames.map((cacheName) => {
            if (cacheName !== STATIC_CACHE_NAME && 
                cacheName !== RUNTIME_CACHE_NAME &&
                cacheName !== CACHE_NAME) {
              console.log('[SW] Deleting old cache:', cacheName)
              return caches.delete(cacheName)
            }
          })
        )
      }),
      // Take control of all pages immediately
      self.clients.claim()
    ])
  )
})

// Fetch event - implement caching strategies
self.addEventListener('fetch', (event) => {
  if (IS_LOCAL_DEV) {
    return
  }

  const { request } = event
  const url = new URL(request.url)
  
  // Skip non-GET requests and chrome-extension requests
  if (request.method !== 'GET' || url.protocol === 'chrome-extension:') {
    return
  }
  
  // Handle different types of requests
  if (request.url.includes('/api/')) {
    // API requests - Network first, cache as fallback
    event.respondWith(handleApiRequest(request))
  } else if (isStaticAsset(request.url)) {
    // Static assets - Cache first
    event.respondWith(handleStaticAsset(request))
  } else if (isRuntimeCacheable(request.url)) {
    // Runtime cacheable resources - Stale while revalidate
    event.respondWith(handleRuntimeCache(request))
  } else if (isNavigationRequest(request)) {
    // Navigation requests - Network first with offline fallback
    event.respondWith(handleNavigationRequest(request))
  }
})

async function precacheStaticAssets() {
  const cache = await caches.open(STATIC_CACHE_NAME)
  console.log('[SW] Caching static assets')

  await Promise.allSettled(
    STATIC_ASSETS.map(async (url) => {
      const request = new Request(url, { credentials: 'same-origin' })
      const response = await fetch(request)

      if (response.ok) {
        await cache.put(request, response)
      } else {
        console.log('[SW] Skipping non-OK precache response:', url, response.status)
      }
    })
  )
}

// Handle API requests with network-first strategy
async function handleApiRequest(request) {
  const cache = await caches.open(RUNTIME_CACHE_NAME)
  
  try {
    // Try network first
    const networkResponse = await fetch(request)
    
    // Cache successful responses
    if (networkResponse.ok) {
      cache.put(request, networkResponse.clone())
    }
    
    return networkResponse
  } catch (error) {
    // Network failed, try cache
    console.log('[SW] Network failed for API, trying cache:', request.url)
    const cachedResponse = await cache.match(request)
    
    if (cachedResponse) {
      return cachedResponse
    }
    
    // Return a fallback response for failed API calls
    return new Response(
      JSON.stringify({ 
        error: 'Network unavailable', 
        offline: true,
        timestamp: new Date().toISOString()
      }), 
      {
        status: 503,
        statusText: 'Service Unavailable',
        headers: { 'Content-Type': 'application/json' }
      }
    )
  }
}

// Handle static assets with cache-first strategy
async function handleStaticAsset(request) {
  const cache = await caches.open(STATIC_CACHE_NAME)
  const cachedResponse = await cache.match(request)
  
  if (cachedResponse) {
    return cachedResponse
  }
  
  try {
    const networkResponse = await fetch(request)
    cache.put(request, networkResponse.clone())
    return networkResponse
  } catch (error) {
    console.log('[SW] Failed to fetch static asset:', request.url)
    return new Response('Asset not available offline', { status: 404 })
  }
}

// Handle runtime cacheable resources with stale-while-revalidate
async function handleRuntimeCache(request) {
  const cache = await caches.open(RUNTIME_CACHE_NAME)
  const cachedResponse = await cache.match(request)
  
  // Start fetch in background for revalidation
  const fetchPromise = fetch(request).then((networkResponse) => {
    if (networkResponse.ok) {
      cache.put(request, networkResponse.clone())
    }
    return networkResponse
  }).catch(() => null)
  
  // Return cached response immediately if available
  if (cachedResponse) {
    return cachedResponse
  }
  
  // Wait for network if no cached response
  return fetchPromise || new Response('Resource not available', { status: 404 })
}

// Handle navigation requests
async function handleNavigationRequest(request) {
  try {
    // Try network first
    const networkResponse = await fetch(request)
    return networkResponse
  } catch (error) {
    // Network failed, try cache
    const cache = await caches.open(STATIC_CACHE_NAME)
    const cachedResponse = await cache.match(request.url)
    
    if (cachedResponse) {
      return cachedResponse
    }
    
    // Return offline page if available
    const offlinePage = await cache.match('/offline')
    if (offlinePage) {
      return offlinePage
    }
    
    // Fallback offline response
    return new Response(`
      <!DOCTYPE html>
      <html>
        <head>
          <title>Offline - ERP System</title>
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <style>
            body { 
              font-family: -apple-system, BlinkMacSystemFont, sans-serif; 
              text-align: center; 
              padding: 2rem; 
              background: #f7fafc; 
              color: #2d3748;
            }
            .container { max-width: 400px; margin: 0 auto; }
            .offline-icon { font-size: 4rem; margin-bottom: 1rem; }
            h1 { color: #2b6cb0; margin-bottom: 1rem; }
            p { margin-bottom: 1.5rem; color: #4a5568; }
            .retry-btn { 
              background: #2b6cb0; 
              color: white; 
              border: none; 
              padding: 0.75rem 1.5rem; 
              border-radius: 0.375rem; 
              cursor: pointer;
              font-size: 1rem;
            }
            .retry-btn:hover { background: #2c5aa0; }
          </style>
        </head>
        <body>
          <div class="container">
            <div class="offline-icon">📱</div>
            <h1>You're Offline</h1>
            <p>It seems you're not connected to the internet. Please check your connection and try again.</p>
            <button class="retry-btn" onclick="window.location.reload()">
              Try Again
            </button>
          </div>
        </body>
      </html>
    `, {
      status: 200,
      headers: { 'Content-Type': 'text/html' }
    })
  }
}

// Utility functions
function isStaticAsset(url) {
  return url.includes('/_next/static/') || 
         url.includes('/static/') ||
         url.includes('/icon-') ||
         url.includes('/manifest.json')
}

function isRuntimeCacheable(url) {
  return RUNTIME_CACHE_URLS.some(pattern => pattern.test(url))
}

function isNavigationRequest(request) {
  return request.mode === 'navigate' || 
         (request.method === 'GET' && request.headers.get('accept').includes('text/html'))
}

// Background sync for offline operations
self.addEventListener('sync', (event) => {
  console.log('[SW] Background sync event:', event.tag)
  
  if (event.tag === 'background-sync') {
    event.waitUntil(syncPendingOperations())
  }
})

// Push notification handling
self.addEventListener('push', (event) => {
  console.log('[SW] Push received')
  
  if (!event.data) {
    console.log('[SW] Push event has no data')
    return
  }
  
  let notificationData
  try {
    notificationData = event.data.json()
  } catch (error) {
    console.log('[SW] Error parsing push data:', error)
    notificationData = {
      title: 'ERP System Notification',
      body: event.data.text() || 'New notification received',
      icon: '/icon-192.png',
      badge: '/icon-192.png'
    }
  }
  
  const options = {
    body: notificationData.body,
    icon: notificationData.icon || '/icon-192.png',
    badge: notificationData.badge || '/icon-192.png',
    data: notificationData.data || {},
    actions: notificationData.actions || [],
    tag: notificationData.tag || 'erp-notification',
    renotify: true,
    requireInteraction: notificationData.requireInteraction || false,
    silent: notificationData.silent || false,
    vibrate: notificationData.vibrate || [200, 100, 200],
    timestamp: Date.now()
  }
  
  event.waitUntil(
    self.registration.showNotification(notificationData.title || 'ERP System', options)
  )
})

// Handle notification clicks
self.addEventListener('notificationclick', (event) => {
  console.log('[SW] Notification clicked')
  
  event.notification.close()
  
  const urlToOpen = event.notification.data?.url || '/dashboard'
  
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true })
      .then((clientList) => {
        // Check if there's already a window/tab open with the target URL
        for (const client of clientList) {
          if (client.url.includes(urlToOpen) && 'focus' in client) {
            return client.focus()
          }
        }
        
        // If no existing window/tab, open a new one
        if (self.clients.openWindow) {
          return self.clients.openWindow(urlToOpen)
        }
      })
  )
})

// Handle notification close
self.addEventListener('notificationclose', (event) => {
  console.log('[SW] Notification closed')
  
  // Track notification close analytics if needed
  const notificationData = event.notification.data || {}
  if (notificationData.trackClose) {
    // Send analytics event
    fetch('/api/analytics/notification-closed', {
      method: 'POST',
      body: JSON.stringify({
        notificationId: notificationData.id,
        timestamp: Date.now()
      }),
      headers: {
        'Content-Type': 'application/json'
      }
    }).catch(() => {
      // Silently fail analytics
    })
  }
})

// Sync pending operations when back online
async function syncPendingOperations() {
  try {
    // This would integrate with your OfflineDataManager
    const cache = await caches.open(RUNTIME_CACHE_NAME)
    
    // Get pending operations from IndexedDB (simplified)
    const pendingOpsRequest = indexedDB.open('erp-system-offline', 1)
    
    return new Promise((resolve, reject) => {
      pendingOpsRequest.onsuccess = async (event) => {
        try {
          const db = event.target.result
          const transaction = db.transaction(['pending_operations'], 'readonly')
          const store = transaction.objectStore('pending_operations')
          const getAllRequest = store.getAll()
          
          getAllRequest.onsuccess = async () => {
            const pendingOps = getAllRequest.result
            
            for (const operation of pendingOps) {
              try {
                await fetch(operation.url, {
                  method: operation.method || 'POST',
                  headers: {
                    'Content-Type': 'application/json',
                    ...operation.headers
                  },
                  body: JSON.stringify(operation.data)
                })
                
                // Remove successful operation
                const deleteTransaction = db.transaction(['pending_operations'], 'readwrite')
                const deleteStore = deleteTransaction.objectStore('pending_operations')
                deleteStore.delete(operation.id)
              } catch (error) {
                console.log('[SW] Failed to sync operation:', operation.id, error)
              }
            }
            
            resolve()
          }
        } catch (error) {
          reject(error)
        }
      }
      
      pendingOpsRequest.onerror = () => reject(pendingOpsRequest.error)
    })
  } catch (error) {
    console.log('[SW] Error syncing pending operations:', error)
  }
}

// Handle messages from the main thread
self.addEventListener('message', (event) => {
  console.log('[SW] Message received:', event.data)
  
  if (event.data && event.data.type) {
    switch (event.data.type) {
      case 'SKIP_WAITING':
        self.skipWaiting()
        break
        
      case 'GET_CACHE_STATUS':
        event.ports[0].postMessage({
          caches: {
            static: STATIC_CACHE_NAME,
            runtime: RUNTIME_CACHE_NAME
          },
          version: CACHE_NAME
        })
        break
        
      case 'CLEAR_CACHE':
        caches.keys().then((cacheNames) => {
          return Promise.all(
            cacheNames.map((cacheName) => caches.delete(cacheName))
          )
        }).then(() => {
          event.ports[0].postMessage({ success: true })
        }).catch((error) => {
          event.ports[0].postMessage({ success: false, error: error.message })
        })
        break
        
      default:
        console.log('[SW] Unknown message type:', event.data.type)
    }
  }
})

// Log service worker ready
console.log('[SW] Service Worker loaded and ready')
