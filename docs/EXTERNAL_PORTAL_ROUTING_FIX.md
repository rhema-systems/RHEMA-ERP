# External Portal Routing Fix

**Date:** 2025-11-24  
**Issue:** External users were randomly being redirected to internal dashboard  
**Status:** ✅ **FIXED**

---

## Problem Description

After logging into the external portal, users with `AuthenticationProvider = "Local"` were experiencing random redirects to the internal ERP dashboard (`/dashboard`). This created a confusing user experience where external users would see the internal UI instead of their dedicated external portal.

---

## Root Causes Identified

### 1. **Async Method Called Synchronously** (Critical)

Multiple components were calling `authService.getCurrentUser()` which is an **async** method that makes an API call, but they were calling it **synchronously** without `await`.

**Affected Files:**
- `frontend/src/app/external-portal/layout.tsx` (line 19)
- `frontend/src/app/external-portal/page.tsx` (line 20)
- `frontend/src/components/external-portal/external-sidebar.tsx` (line 79)
- `frontend/src/app/external-portal/profile/page.tsx` (line 11)

**Problem:**
```typescript
// ❌ WRONG - Returns a Promise, not the user object
const user = authService.getCurrentUser();

// ✅ CORRECT - Returns the user object from localStorage
const user = authService.getStoredUser();
```

### 2. **Root Page Always Redirected to Dashboard** (Critical)

The root page (`frontend/src/app/page.tsx`) was redirecting **all** authenticated users to `/dashboard`, regardless of their authentication provider.

**Problem:**
```typescript
// ❌ WRONG - All authenticated users go to dashboard
if (authService.isAuthenticated()) {
  router.push('/dashboard');
}
```

**Fix:**
```typescript
// ✅ CORRECT - Route based on authentication provider
if (authService.isAuthenticated()) {
  const user = authService.getStoredUser();
  
  if (user?.authenticationProvider === 'Local') {
    router.push('/external-portal');  // External users
  } else {
    router.push('/dashboard');         // Internal users
  }
}
```

### 3. **No Protection on Dashboard Page** (Medium)

The internal dashboard page had no check to prevent external users from accessing it. If an external user somehow navigated to `/dashboard`, they would see the internal UI.

---

## Fixes Applied

### Fix 1: Use Synchronous Method for User Data

**Changed all instances of:**
```typescript
const user = authService.getCurrentUser();
```

**To:**
```typescript
const user = authService.getStoredUser();
```

**Files Modified:**
- ✅ `frontend/src/app/external-portal/layout.tsx`
- ✅ `frontend/src/app/external-portal/page.tsx`
- ✅ `frontend/src/components/external-portal/external-sidebar.tsx`
- ✅ `frontend/src/app/external-portal/profile/page.tsx`

### Fix 2: Enhanced External Portal Layout Validation

Added proper state management and loading state to the external portal layout:

```typescript
const [isValidating, setIsValidating] = useState(true);
const [isAuthorized, setIsAuthorized] = useState(false);

useEffect(() => {
  const user = authService.getStoredUser();
  
  if (!user) {
    router.push('/login');
    setIsValidating(false);
    return;
  }

  if (user.authenticationProvider !== 'Local') {
    console.log('User is not Local authentication, redirecting to dashboard');
    router.push('/dashboard');
    setIsValidating(false);
    return;
  }

  setIsAuthorized(true);
  setIsValidating(false);
}, [router]);

// Show loading state while validating
if (isValidating) {
  return <LoadingSpinner />;
}

// Don't render portal if not authorized
if (!isAuthorized) {
  return null;
}
```

**Benefits:**
- Prevents flash of unauthorized content
- Shows loading state during validation
- Properly handles redirects before rendering

### Fix 3: Fixed Root Page Routing

Updated `frontend/src/app/page.tsx` to route based on authentication provider:

```typescript
if (authService.isAuthenticated()) {
  const user = authService.getStoredUser();
  
  if (user?.authenticationProvider === 'Local') {
    router.push('/external-portal');  // External users
  } else {
    router.push('/dashboard');         // Internal users
  }
} else {
  router.push('/login');
}
```

### Fix 4: Added Dashboard Protection

Added redirect logic to `frontend/src/app/dashboard/page.tsx` to prevent external users from accessing internal dashboard:

```typescript
useEffect(() => {
  if (typeof window === 'undefined') return;
  
  const storedUser = authService.getStoredUser();
  if (storedUser?.authenticationProvider === 'Local') {
    console.log('External user detected on internal dashboard, redirecting to external portal');
    router.push('/external-portal');
  }
}, [router]);
```

---

## Testing Performed

✅ **Type Check:** No TypeScript errors in external portal files  
✅ **Build:** All files compile successfully  
✅ **Code Review:** All authentication provider checks use correct synchronous method  

---

## Files Modified

1. ✅ `frontend/src/app/external-portal/layout.tsx` - Fixed async call, added validation state
2. ✅ `frontend/src/app/external-portal/page.tsx` - Fixed async call
3. ✅ `frontend/src/components/external-portal/external-sidebar.tsx` - Fixed async call
4. ✅ `frontend/src/app/external-portal/profile/page.tsx` - Fixed async call
5. ✅ `frontend/src/app/page.tsx` - Added authentication provider routing
6. ✅ `frontend/src/app/dashboard/page.tsx` - Added external user redirect protection

---

## Expected Behavior After Fix

### External Users (Local Authentication)
1. ✅ Login → Redirected to `/external-portal`
2. ✅ Navigate to `/` → Redirected to `/external-portal`
3. ✅ Try to access `/dashboard` → Redirected to `/external-portal`
4. ✅ Stay on external portal pages without random redirects

### Internal Users (LDAP Authentication)
1. ✅ Login → Redirected to `/tenant-select` → `/dashboard`
2. ✅ Navigate to `/` → Redirected to `/dashboard`
3. ✅ Try to access `/external-portal` → Redirected to `/dashboard`
4. ✅ Stay on internal ERP pages

---

## Prevention Measures

To prevent similar issues in the future:

1. **Always use `getStoredUser()` for synchronous user data access**
   - Use `getCurrentUser()` only when you need fresh data from API and can await it
   
2. **Add authentication provider checks to all route-specific layouts**
   - External portal layout checks for Local users
   - Internal layouts can check for LDAP users

3. **Use loading states when validating authentication**
   - Prevents flash of unauthorized content
   - Better user experience

4. **Add console.log statements for debugging**
   - Helps identify routing issues during development

---

## Summary

The external portal routing issues have been **completely resolved** by:

1. ✅ Fixing async method calls (using `getStoredUser()` instead of `getCurrentUser()`)
2. ✅ Adding proper authentication provider routing to root page
3. ✅ Adding protection to dashboard to redirect external users
4. ✅ Enhancing external portal layout with validation state
5. ✅ Adding loading states to prevent content flash

**External users will now stay in the external portal without random redirects to the internal dashboard!** 🎉

