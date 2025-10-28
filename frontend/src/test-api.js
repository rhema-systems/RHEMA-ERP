// Simple API test to debug authentication
async function testApi() {
  console.log('=== API Test ===');
  
  const authToken = localStorage.getItem('authToken');
  const baseUrl = 'http://localhost:5000/api';
  
  console.log('🔑 Token:', authToken ? 'Present' : 'Missing');
  
  if (!authToken) {
    console.log('❌ No auth token found. Please log in first.');
    return;
  }
  
  try {
    console.log('🔍 Testing job cards API...');
    
    const response = await fetch(`${baseUrl}/maintenance/job-cards`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${authToken}`,
        'Content-Type': 'application/json'
      }
    });
    
    console.log(`📡 Response Status: ${response.status} ${response.statusText}`);
    
    if (response.ok) {
      const data = await response.json();
      console.log('✅ API call successful!');
      console.log('📊 Job Cards:', data);
    } else {
      const error = await response.text();
      console.log('❌ API call failed:', error);
      
      if (response.status === 401) {
        console.log('🚨 Authentication error - token might be invalid or expired');
      }
    }
  } catch (error) {
    console.error('💥 Network error:', error);
  }
  
  console.log('=== End API Test ===');
}

// Run the test if in browser
if (typeof window !== 'undefined') {
  testApi();
} else {
  console.log('Not in browser environment');
}