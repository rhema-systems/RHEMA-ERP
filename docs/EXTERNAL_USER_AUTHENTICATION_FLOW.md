# External User Authentication Flow

## Overview

The ERP system now supports routing external users (suppliers, contractors, and future external user types) to a dedicated external portal after login, while internal users continue to access the main ERP system.

---

## Authentication Provider Logic

### User Types

1. **Internal Users** (LDAP Authentication)
   - `AuthenticationProvider = AuthenticationProvider.LDAP`
   - Authenticated via LDAP directory service
   - Access main ERP system
   - Go through tenant selection flow
   - Full access to internal modules

2. **External Users** (Local Authentication)
   - `AuthenticationProvider = AuthenticationProvider.Local`
   - Authenticated via local database
   - Access external portal
   - Limited to external-facing features (registration, profile management, etc.)
   - Examples: Suppliers, Contractors, Land Management users, Rent Management users, Permit applicants

---

## Implementation Details

### Backend Changes

#### 1. UserInfo DTO (`src/ErpSystem.Api/Models/AuthModels.cs`)

Added `AuthenticationProvider` field to the `UserInfo` DTO:

```csharp
public class UserInfo
{
    // ... existing fields ...
    
    // Authentication provider (Local or LDAP)
    public string AuthenticationProvider { get; set; } = "Local";
}
```

#### 2. AuthController Updates

Updated all endpoints that return `UserInfo` to include the `AuthenticationProvider`:

- **Login** (line 475-494): `AuthenticationProvider = user.AuthenticationProvider.ToString()`
- **RefreshToken** (line 530-547): `AuthenticationProvider = user.AuthenticationProvider.ToString()`
- **GetCurrentUser** (line 841-856): `AuthenticationProvider = user.AuthenticationProvider.ToString()`
- **SelectTenant** (line 999-1018): `AuthenticationProvider = user.AuthenticationProvider.ToString()`

### Frontend Changes

#### 1. UserInfo Interface (`frontend/src/services/api.service.ts`)

Added `authenticationProvider` field:

```typescript
export interface UserInfo {
  // ... existing fields ...
  authenticationProvider?: 'Local' | 'LDAP';
}
```

#### 2. Login Page Routing (`frontend/src/app/login/page.tsx`)

Updated login success handler to route based on authentication provider:

```typescript
onSuccess: (response) => {
  // ... 2FA check ...
  
  if (response.token) {
    // External users (Local authentication) → External Portal
    if (response.user?.authenticationProvider === 'Local') {
      router.push('/external-portal');
    } else {
      // Internal users (LDAP) → Tenant Selection
      router.push('/tenant-select');
    }
  }
}
```

---

## External Portal Structure

The external portal will be a separate section of the application with its own routing and modules:

### Planned Routes

- `/external-portal` - External portal dashboard
- `/external-portal/supplier-registration` - Supplier registration wizard
- `/external-portal/contractor-registration` - Contractor registration wizard
- `/external-portal/profile` - User profile management
- `/external-portal/applications` - View registration applications
- `/external-portal/documents` - Document upload and management

### Future Modules (Extensible Design)

- `/external-portal/land-management` - Land management registration
- `/external-portal/rent-management` - Rent management registration
- `/external-portal/permit-application` - Permit application forms

---

## Security Considerations

1. **Authentication Required**: All external portal routes should require authentication
2. **Authorization**: External users should only access external portal routes
3. **Internal Routes Protected**: External users should be blocked from accessing internal ERP routes
4. **Role-Based Access**: External users will have specific roles (e.g., "ExternalSupplier", "ExternalContractor")

---

## Next Steps

1. **Create External Portal Layout** - Dedicated layout for external users
2. **Create External Portal Dashboard** - Landing page after login
3. **Implement Registration Wizards** - Multi-step forms for supplier/contractor registration
4. **Add Route Guards** - Protect internal routes from external users and vice versa
5. **Create External User Roles** - Define roles for different external user types

---

## Testing

### Test Scenarios

1. **Internal User Login**:
   - User with `AuthenticationProvider.LDAP`
   - Should redirect to `/tenant-select`
   - Should access main ERP system

2. **External User Login**:
   - User with `AuthenticationProvider.Local`
   - Should redirect to `/external-portal`
   - Should only access external portal features

3. **Route Protection**:
   - External users attempting to access `/dashboard` should be redirected
   - Internal users attempting to access `/external-portal` should be redirected

---

## Database Migration Status

✅ **Phase 1 Complete**: Business Partner Management database tables created
- Migration: `20251123005630_AddBusinessPartnerManagement`
- 13 tables created for supplier/contractor management
- Ready for Phase 2: DTOs, Repositories, and Services

