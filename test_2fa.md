# Test 2FA Service

You can test the 2FA service manually by:

## 1. Check if your TwoFactorAuthService generates real data:

Run this in a C# test or console app:

```csharp
var service = new TwoFactorAuthService(logger, userService);
var user = new ApplicationUser { Email = "test@example.com" };
var result = await service.SetupTwoFactorAsync(user);

Console.WriteLine($"Secret Key: {result.SecretKey}");
Console.WriteLine($"Manual Entry: {result.ManualEntryKey}");
Console.WriteLine($"QR Code starts with: {result.QrCodeDataUri.Substring(0, 50)}...");
```

Expected output should be REAL values like:
- Secret Key: `JBSWY3DPEHPK3PXP` (random Base32 string)
- Manual Entry: `JBSW Y3DP EHPK 3PXP` (formatted for typing)
- QR Code: `data:image/png;base64,iVBORw0KGgoAAAANS...` (real image data)

## 2. Test the API endpoint directly:

Using curl or Postman:

```bash
# First login and get a JWT token
curl -X POST https://localhost:7001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}'

# Use the token to enable 2FA
curl -X POST https://localhost:7001/api/security/two-factor/enable \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"verificationCode":"123456","password":"Admin123!"}'
```

## 3. Check Frontend Network Calls:

1. Open browser DevTools (F12)
2. Go to Network tab
3. Try to enable 2FA in the UI
4. Look for the API call to `/api/security/two-factor/enable`
5. Check the response body - it should contain real secret keys, not demo data

## 4. If you see demo data in the response:

The issue is in the backend. Check:
- Is the TwoFactorAuthService properly registered in DI?
- Are there any exceptions being caught and returning fallback data?
- Is the SetupTwoFactorAsync method actually being called?

## 5. If the API returns real data but UI shows demo:

The issue is in the frontend. Check:
- Is the frontend making the API call?
- Is there error handling that falls back to demo data?
- Are there hardcoded mock values in the component?