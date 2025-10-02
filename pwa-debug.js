// PWA Debug Script - Run this in browser console
// Copy and paste this into your browser's console at localhost:3000

console.log('🧪 PWA Debug Report');
console.log('==================');

// Check Service Worker
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.getRegistrations().then(registrations => {
    console.log('📋 Service Worker Registrations:', registrations.length);
    registrations.forEach((registration, index) => {
      console.log(`SW ${index + 1}:`, {
        scope: registration.scope,
        state: registration.active?.state,
        updatefound: registration.updatefound
      });
    });
  });
} else {
  console.log('❌ Service Worker not supported');
}

// Check Cache Storage
if ('caches' in window) {
  caches.keys().then(cacheNames => {
    console.log('💾 Cache Storage:', cacheNames);
    cacheNames.forEach(cacheName => {
      caches.open(cacheName).then(cache => {
        cache.keys().then(keys => {
          console.log(`Cache "${cacheName}": ${keys.length} items`);
        });
      });
    });
  });
} else {
  console.log('❌ Cache API not supported');
}

// Check PWA Install Prompt
let deferredPrompt;
window.addEventListener('beforeinstallprompt', (e) => {
  console.log('📱 PWA Install prompt available');
  deferredPrompt = e;
});

// Check Notification Permission
console.log('🔔 Notification Permission:', Notification.permission);

// Check Online Status
console.log('🌐 Online Status:', navigator.onLine);

// Check PWA Manifest
fetch('/manifest.json')
  .then(response => response.json())
  .then(manifest => {
    console.log('📄 PWA Manifest:', manifest);
  })
  .catch(error => {
    console.log('❌ Manifest not found:', error);
  });

// Check if running as PWA
if (window.matchMedia('(display-mode: standalone)').matches) {
  console.log('🎉 Running as installed PWA');
} else {
  console.log('🌐 Running in browser');
}

console.log('==================');
console.log('🔍 Check the logs above for PWA status');