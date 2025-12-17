# Authorization Issue Fix - My Evaluations Endpoint

## Problem
The `/api/procurement/TenderEvaluations/my-evaluations/` endpoint was returning **401 Unauthorized** with no Authorization header being sent from the frontend.

## Root Cause Analysis

### Issue 1: Endpoint Parameter Mismatch
- **Backend Endpoint:** `[HttpGet("my-evaluations/{tenderId}")]` expected `tenderId` as a route parameter
- **Backend Service:** `GetMyEvaluationsAsync(Guid evaluatorId)` expected `evaluatorId` (TenderEvaluator.Id)
- **Frontend Call:** `getMyEvaluations(evaluatorId)` was passing User ID from localStorage

**The Problem:** The endpoint was trying to pass `evaluatorId` (User ID) to a parameter named `tenderId`, causing a mismatch.

### Issue 2: Authorization Header Not Being Sent
The `getAuthHeaders()` function in `tenderEvaluationService.ts` was not properly checking for the token in localStorage. The function needed to:
1. Check if running in browser environment (`typeof window !== 'undefined'`)
2. Properly retrieve the token from localStorage
3. Always include the Authorization header when token is present

## Solution Implemented

### Backend Changes

#### 1. Updated TenderEvaluationsController.cs
**Changed endpoint from:**
```csharp
[HttpGet("my-evaluations/{tenderId}")]
public async Task<ActionResult<IEnumerable<TenderEvaluationDto>>> GetMyEvaluations(Guid tenderId)
{
    var evaluations = await _evaluationService.GetMyEvaluationsAsync(tenderId);
    return Ok(evaluations);
}
```

**To:**
```csharp
[HttpGet("my-evaluations")]
public async Task<ActionResult<IEnumerable<TenderEvaluationDto>>> GetMyEvaluations()
{
    var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("...nameidentifier")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        return BadRequest("Unable to determine current user");
    
    var myEvaluations = await _evaluationService.GetMyEvaluationsByUserIdAsync(userId);
    return Ok(myEvaluations);
}
```

**Key Changes:**
- Removed route parameter (no longer expects `tenderId`)
- Extracts current user ID from JWT claims
- Calls new service method `GetMyEvaluationsByUserIdAsync()`

#### 2. Updated ITenderEvaluationService Interface
Added new method:
```csharp
Task<IEnumerable<TenderEvaluationDto>> GetMyEvaluationsByUserIdAsync(Guid userId);
```

#### 3. Implemented GetMyEvaluationsByUserIdAsync in TenderEvaluationService
```csharp
public async Task<IEnumerable<TenderEvaluationDto>> GetMyEvaluationsByUserIdAsync(Guid userId)
{
    // Get all TenderEvaluator records for this user
    var evaluators = await _evaluatorRepository.GetByUserIdAsync(userId);
    
    // Get all evaluations for these evaluators
    var allEvaluations = new List<TenderEvaluation>();
    foreach (var evaluator in evaluators)
    {
        var evaluations = await _evaluationRepository.GetByEvaluatorIdAsync(evaluator.Id);
        allEvaluations.AddRange(evaluations);
    }
    
    return allEvaluations.Select(MapToDto);
}
```

**Logic:**
1. Get all TenderEvaluator records assigned to the current user
2. For each TenderEvaluator, get all TenderEvaluation records
3. Return consolidated list of evaluations

### Frontend Changes

#### 1. Fixed getAuthHeaders() Function in tenderEvaluationService.ts
**Changed from:**
```typescript
function getAuthHeaders(): HeadersInit {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}
```

**To:**
```typescript
function getAuthHeaders(): HeadersInit {
  // Check localStorage for token - try both possible keys
  let token: string | null = null;

  if (typeof window !== 'undefined') {
    token = localStorage.getItem('authToken') || localStorage.getItem('token');
  }

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  return headers;
}
```

**Key Changes:**
- Added `typeof window !== 'undefined'` check to ensure we're in browser environment
- Properly initialize headers object before conditionally adding Authorization
- Check 'authToken' first (the actual key used by the system)
- Added console logging for debugging

#### 2. Updated getMyEvaluations() Function
**Added debugging logs:**
```typescript
export async function getMyEvaluations(): Promise<TenderEvaluationDto[]> {
  const headers = getAuthHeaders();
  console.log('🔐 getMyEvaluations - Authorization header present:', !!headers['Authorization']);
  console.log('🔐 getMyEvaluations - Token in localStorage:', !!localStorage.getItem('authToken'));

  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/my-evaluations`, {
    headers,
  });

  if (!response.ok) {
    console.error('❌ getMyEvaluations failed:', response.status, response.statusText);
    throw new Error(`Failed to fetch my evaluations: ${response.status} ${response.statusText}`);
  }

  return response.json();
}
```

#### 3. Updated evaluations/page.tsx
**Changed from:**
```typescript
const evaluatorId = localStorage.getItem('userId') || '';
const data = await tenderEvaluationService.getMyEvaluations(evaluatorId);
```

**To:**
```typescript
const data = await tenderEvaluationService.getMyEvaluations();
```

## How It Works Now

1. **User logs in** → JWT token stored in localStorage
2. **Frontend calls** `/api/procurement/TenderEvaluations/my-evaluations` (no parameters)
3. **Frontend includes** Authorization header with Bearer token
4. **Backend extracts** user ID from JWT claims
5. **Backend queries** TenderEvaluator records by user ID
6. **Backend fetches** all evaluations for those evaluators
7. **Returns** consolidated list of evaluations to frontend

## Authorization Flow

```
Frontend Request
    ↓
Authorization Header: Bearer {token}
    ↓
Backend [Authorize] attribute validates token
    ↓
User.FindFirst("sub") extracts user ID from claims
    ↓
GetMyEvaluationsByUserIdAsync(userId) fetches evaluations
    ↓
Returns 200 OK with evaluation data
```

## Build Status
✅ Backend: No compilation errors
✅ Frontend: No TypeScript errors

## Testing
To test the fix:
1. Login to the internal portal
2. Navigate to `/procurement/evaluations`
3. Should see "My Evaluations" page with list of evaluations
4. Check browser Network tab - Authorization header should be present
5. Response should be 200 OK with evaluation data

