const fetch = require('node-fetch');

async function testAnalyticsApi() {
  try {
    console.log('Testing Analytics API...');
    
    // Test 1: Direct call to backend (no auth)
    console.log('\n1. Direct backend API call (no auth):');
    const directResponse = await fetch('http://localhost:5000/api/maintenance/analytics/data', {
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json'
      }
    });
    
    console.log('Status:', directResponse.status, directResponse.statusText);
    
    if (directResponse.ok) {
      const data = await directResponse.json();
      console.log('✅ Backend API working - Data keys:', Object.keys(data));
      console.log('Assets count:', data.assets?.length || 0);
      console.log('Performance data count:', data.performanceData?.length || 0);
    } else {
      console.log('❌ Backend API failed');
      const errorText = await directResponse.text();
      console.log('Error:', errorText);
    }

    // Test 2: Check if frontend dev server is proxying requests
    console.log('\n2. Frontend dev server proxy test:');
    try {
      const proxyResponse = await fetch('http://localhost:3000/api/maintenance/analytics/data', {
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json'
        }
      });
      
      console.log('Proxy Status:', proxyResponse.status, proxyResponse.statusText);
      
      if (proxyResponse.ok) {
        const proxyData = await proxyResponse.json();
        console.log('✅ Frontend proxy working - Data keys:', Object.keys(proxyData));
      } else {
        console.log('❌ Frontend proxy failed or not configured');
        const errorText = await proxyResponse.text();
        console.log('Error:', errorText);
      }
    } catch (proxyError) {
      console.log('❌ Frontend proxy test failed:', proxyError.message);
    }

  } catch (error) {
    console.error('Test failed:', error.message);
  }
}

testAnalyticsApi();