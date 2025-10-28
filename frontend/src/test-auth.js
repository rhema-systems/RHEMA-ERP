// Test script to check authentication status
console.log('=== Authentication Test ===');

// Check if we're in browser environment
if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
  const authToken = localStorage.getItem('authToken');
  const refreshToken = localStorage.getItem('refreshToken');
  const user = localStorage.getItem('user');
  
  console.log('🔑 Auth Token:', authToken ? 'Present' : 'Missing');
  console.log('🔄 Refresh Token:', refreshToken ? 'Present' : 'Missing');
  console.log('👤 User:', user ? 'Present' : 'Missing');
  
  if (authToken) {
    console.log('🔍 Token Preview:', authToken.substring(0, 50) + '...');
    
    // Basic JWT format validation
    const parts = authToken.split('.');
    if (parts.length === 3) {
      console.log('✅ Token has valid JWT format (3 parts)');
      
      try {
        // Decode header
        const header = JSON.parse(atob(parts[0]));
        console.log('📋 Token Header:', header);
        
        // Decode payload
        const payload = JSON.parse(atob(parts[1]));
        console.log('📄 Token Payload:', {
          sub: payload.sub,
          exp: payload.exp ? new Date(payload.exp * 1000).toISOString() : 'No expiration',
          iat: payload.iat ? new Date(payload.iat * 1000).toISOString() : 'No issued at',
          tenantId: payload.tenantId || 'No tenant ID'
        });
        
        // Check expiration
        if (payload.exp) {
          const now = Math.floor(Date.now() / 1000);
          if (payload.exp > now) {
            console.log('✅ Token is not expired');
          } else {
            console.log('⚠️ Token is expired!');
          }
        }
      } catch (e) {
        console.log('❌ Failed to decode token:', e.message);
      }
    } else {
      console.log('❌ Token has invalid JWT format');
    }
  }
  
  if (user) {
    try {
      const parsedUser = JSON.parse(user);
      console.log('👤 User Info:', {
        username: parsedUser.username,
        email: parsedUser.email,
        tenantId: parsedUser.tenantId,
        roles: parsedUser.roles
      });
    } catch (e) {
      console.log('❌ Failed to parse user data:', e.message);
    }
  }
} else {
  console.log('❌ Not in browser environment or localStorage not available');
}

console.log('=== End Authentication Test ===');