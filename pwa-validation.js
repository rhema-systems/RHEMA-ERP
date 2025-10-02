// PWA Installation Validation Script
// Copy and paste this into your browser console at localhost:3000

console.clear();
console.log('🔍 PWA Installation Validation');
console.log('================================');

async function validatePWA() {
  const issues = [];
  const passed = [];
  
  // 1. Check Service Worker
  if ('serviceWorker' in navigator) {
    try {
      const registrations = await navigator.serviceWorker.getRegistrations();
      if (registrations.length > 0) {
        passed.push('✅ Service Worker registered');
        console.log('📋 SW Registrations:', registrations.length);
      } else {
        issues.push('❌ No Service Worker registered');
      }
    } catch (error) {
      issues.push('❌ Service Worker error: ' + error.message);
    }
  } else {
    issues.push('❌ Service Worker not supported');
  }
  
  // 2. Check Manifest
  try {
    const response = await fetch('/manifest.json');
    if (response.ok) {
      const manifest = await response.json();
      passed.push('✅ Manifest accessible');
      
      // Check required fields
      if (manifest.name) passed.push('✅ Manifest has name');
      else issues.push('❌ Manifest missing name');
      
      if (manifest.start_url) passed.push('✅ Manifest has start_url');
      else issues.push('❌ Manifest missing start_url');
      
      if (manifest.display) passed.push('✅ Manifest has display mode');
      else issues.push('❌ Manifest missing display');
      
      if (manifest.icons && manifest.icons.length >= 2) {
        passed.push('✅ Manifest has icons');
      } else {
        issues.push('❌ Manifest needs at least 2 icons');
      }
      
    } else {
      issues.push('❌ Manifest not accessible');
    }
  } catch (error) {
    issues.push('❌ Manifest error: ' + error.message);
  }
  
  // 3. Check Icons
  const iconUrls = ['/icon-192.png', '/icon-512.png'];
  for (const iconUrl of iconUrls) {
    try {
      const response = await fetch(iconUrl);
      if (response.ok) {
        passed.push(`✅ Icon ${iconUrl} exists`);
      } else {
        issues.push(`❌ Icon ${iconUrl} not found (${response.status})`);
      }
    } catch (error) {
      issues.push(`❌ Icon ${iconUrl} error: ${error.message}`);
    }
  }
  
  // 4. Check HTTPS/localhost
  if (location.protocol === 'https:' || location.hostname === 'localhost') {
    passed.push('✅ Secure context (HTTPS/localhost)');
  } else {
    issues.push('❌ Not secure context - need HTTPS or localhost');
  }
  
  // 5. Check if already installed
  if (window.matchMedia('(display-mode: standalone)').matches) {
    issues.push('⚠️  PWA already running in standalone mode');
  } else {
    passed.push('✅ Not currently in standalone mode');
  }
  
  // 6. Listen for install prompt
  let promptAvailable = false;
  window.addEventListener('beforeinstallprompt', (e) => {
    promptAvailable = true;
    console.log('🎉 Install prompt available!');
    window.deferredPrompt = e;
  });
  
  // Wait a moment for the event
  setTimeout(() => {
    if (promptAvailable) {
      passed.push('✅ Install prompt available');
    } else {
      issues.push('❌ Install prompt not available');
    }
    
    // Display results
    console.log('\n🎯 PASSED CHECKS:');
    passed.forEach(item => console.log(item));
    
    console.log('\n🚨 ISSUES FOUND:');
    if (issues.length === 0) {
      console.log('✅ No issues found! PWA should be installable.');
    } else {
      issues.forEach(item => console.log(item));
    }
    
    console.log('\n💡 NEXT STEPS:');
    if (issues.length === 0) {
      console.log('• Look for install icon in address bar');
      console.log('• Or trigger manually: window.deferredPrompt?.prompt()');
    } else {
      console.log('• Fix the issues above');
      console.log('• Refresh page and run validation again');
    }
    
  }, 3000);
}

validatePWA();