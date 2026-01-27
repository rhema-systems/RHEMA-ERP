# Understanding GetAssetUsageSummaryAsync

## 📋 Method Overview

```csharp
public async Task<AssetUsageSummaryDto> GetAssetUsageSummaryAsync(Guid assetId)
```

This method retrieves a comprehensive usage summary for a specific asset by analyzing all usage tracking records.

---

## 🔍 Step-by-Step Breakdown

### Step 1: Validate Asset Exists
```csharp
var asset = await _assetService.GetAssetByIdAsync(assetId);
if (asset == null)
    throw new KeyNotFoundException($"Asset with ID {assetId} not found");
```

**Purpose**: Ensure the asset exists before processing
**Returns**: Asset details (Name, AssetNumber, etc.)
**Throws**: `KeyNotFoundException` if asset doesn't exist

---

### Step 2: Get All Usage Records for the Asset
```csharp
var records = await _unitOfWork.Repository<AssetUsageTracking>()
    .FindAsync(u => u.AssetId == assetId && u.TenantId == _currentUserService.TenantId);
```

**Query**: Gets ALL usage tracking records for the asset in the current tenant
**Filters**:
- `AssetId` = specified asset
- `TenantId` = current user's tenant (multi-tenant isolation)

**Database Table**: `AssetUsageTrackings`

---

### Step 3: Sort Records by Date (Newest First)
```csharp
var recordsList = records.OrderByDescending(u => u.RecordedAt).ToList();
var latestRecord = recordsList.FirstOrDefault();
```

**Purpose**: Order records chronologically to find the most recent data
**Result**:
- `recordsList` = All records, newest first
- `latestRecord` = Most recent usage record (or null if no records exist)

---

### Step 4: Calculate Average Daily Usage (Last 30 Days)
```csharp
var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
var recentRecords = recordsList.Where(u => u.RecordedAt >= thirtyDaysAgo).ToList();
```

**Purpose**: Focus on recent data for trend analysis
**Filter**: Only records from the last 30 days

#### 4a. Calculate Days Span
```csharp
var daysSpan = (DateTime.UtcNow - recentRecords.Last().RecordedAt).TotalDays;
```

**Formula**: 
```
Days Span = Now - Oldest Recent Record Date
```

**Example**:
- Oldest record: Jan 1
- Today: Jan 31
- Days Span = 30 days

#### 4b. Calculate Average Daily Mileage
```csharp
var mileageRecords = recentRecords.Where(r => r.Mileage.HasValue).ToList();
if (mileageRecords.Any())
{
    var mileageDiff = mileageRecords.First().Mileage - mileageRecords.Last().Mileage;
    avgDailyMileage = mileageDiff / (decimal)daysSpan;
}
```

**Formula**:
```
Average Daily Mileage = (Latest Mileage - Oldest Mileage) / Days Between
```

**Example**:
- Latest record (Jan 31): 10,250 km
- Oldest record (Jan 1): 9,400 km
- Days: 30
- **Average = (10,250 - 9,400) / 30 = 28.33 km/day**

#### 4c. Calculate Average Daily Operating Hours
```csharp
var hoursRecords = recentRecords.Where(r => r.OperatingHours.HasValue).ToList();
if (hoursRecords.Any())
{
    var hoursDiff = hoursRecords.First().OperatingHours - hoursRecords.Last().OperatingHours;
    avgDailyHours = hoursDiff / (decimal)daysSpan;
}
```

**Formula**:
```
Average Daily Hours = (Latest Hours - Oldest Hours) / Days Between
```

**Example**:
- Latest record (Jan 31): 407 hours
- Oldest record (Jan 1): 370 hours
- Days: 30
- **Average = (407 - 370) / 30 = 1.23 hours/day**

---

### Step 5: Build Summary DTO
```csharp
return new AssetUsageSummaryDto
{
    AssetId = assetId,
    AssetName = asset.Name,
    AssetNumber = asset.AssetNumber,
    CurrentMileage = latestRecord?.Mileage,
    CurrentOperatingHours = latestRecord?.OperatingHours,
    CurrentCycles = latestRecord?.Cycles,
    AverageDailyMileage = avgDailyMileage,
    AverageDailyOperatingHours = avgDailyHours,
    TotalFuelConsumed = recordsList.Where(r => r.FuelConsumed.HasValue).Sum(r => r.FuelConsumed),
    LastRecordedAt = latestRecord?.RecordedAt,
    TotalRecords = recordsList.Count
};
```

---

## 📊 Return Values Explained

### AssetUsageSummaryDto Structure

```csharp
public class AssetUsageSummaryDto
{
    public Guid AssetId { get; set; }                          // Asset unique ID
    public string AssetName { get; set; }                      // From MaintenanceAssets table
    public string AssetNumber { get; set; }                    // Asset identification number
    public decimal? CurrentMileage { get; set; }               // From LATEST record
    public decimal? CurrentOperatingHours { get; set; }        // From LATEST record
    public int? CurrentCycles { get; set; }                    // From LATEST record
    public decimal? AverageDailyMileage { get; set; }          // CALCULATED (last 30 days)
    public decimal? AverageDailyOperatingHours { get; set; }   // CALCULATED (last 30 days)
    public decimal? TotalFuelConsumed { get; set; }            // SUM of all records
    public DateTime? LastRecordedAt { get; set; }              // Date of latest record
    public int TotalRecords { get; set; }                      // Count of all records
    public DateTime? NextMaintenanceDue { get; set; }          // Not set by this method
    public decimal? MileageUntilMaintenance { get; set; }      // Not set by this method
    public decimal? HoursUntilMaintenance { get; set; }        // Not set by this method
}
```

### Field Sources

| Field | Source | Logic |
|-------|--------|-------|
| `AssetId` | Input parameter | Direct pass-through |
| `AssetName` | `MaintenanceAssets` table | From `_assetService.GetAssetByIdAsync()` |
| `AssetNumber` | `MaintenanceAssets` table | From `_assetService.GetAssetByIdAsync()` |
| `CurrentMileage` | `AssetUsageTrackings` | Latest record's `Mileage` field |
| `CurrentOperatingHours` | `AssetUsageTrackings` | Latest record's `OperatingHours` field |
| `CurrentCycles` | `AssetUsageTrackings` | Latest record's `Cycles` field |
| `AverageDailyMileage` | **CALCULATED** | (Latest Mileage - Oldest Mileage) / Days |
| `AverageDailyOperatingHours` | **CALCULATED** | (Latest Hours - Oldest Hours) / Days |
| `TotalFuelConsumed` | `AssetUsageTrackings` | SUM of all `FuelConsumed` values |
| `LastRecordedAt` | `AssetUsageTrackings` | Latest record's `RecordedAt` timestamp |
| `TotalRecords` | **CALCULATED** | COUNT of all records |

---

## 💡 Example Scenario

### Sample Data in Database

**Asset**: Vehicle #001 (Dump Truck)

**Usage Records** (last 7 days):
```
Day 7 (Jan 1):  Mileage: 9,400 km,  Hours: 370,  Fuel: 120.5 L
Day 6 (Jan 2):  Mileage: 9,550 km,  Hours: 375,  Fuel: 135.2 L
Day 5 (Jan 3):  Mileage: 9,700 km,  Hours: 382,  Fuel: 142.8 L
Day 4 (Jan 4):  Mileage: 9,850 km,  Hours: 388,  Fuel: 148.5 L
Day 3 (Jan 5):  Mileage: 9,950 km,  Hours: 393,  Fuel: 153.0 L
Day 2 (Jan 6):  Mileage: 10,050 km, Hours: 398,  Fuel: 158.2 L
Day 1 (Jan 7):  Mileage: 10,250 km, Hours: 407,  Fuel: 167.8 L  ← LATEST
```

### Method Execution

#### Step 1: Get Asset
```
Asset Name: "Dump Truck #001"
Asset Number: "VEH-001"
```

#### Step 2: Get Records
```
Query: WHERE AssetId = {guid} AND TenantId = {tenantId}
Result: 7 records
```

#### Step 3: Sort & Get Latest
```
Latest Record: Jan 7 (10,250 km, 407 hours)
```

#### Step 4: Calculate Averages (30-day window)
```
Recent Records: All 7 records (all within 30 days)
Days Span: Jan 7 - Jan 1 = 6 days

Mileage Calculation:
  Latest: 10,250 km
  Oldest: 9,400 km
  Diff: 850 km
  Average Daily Mileage = 850 / 6 = 141.67 km/day

Hours Calculation:
  Latest: 407 hours
  Oldest: 370 hours
  Diff: 37 hours
  Average Daily Hours = 37 / 6 = 6.17 hours/day

Fuel Total:
  Sum = 120.5 + 135.2 + 142.8 + 148.5 + 153.0 + 158.2 + 167.8 = 1,026 liters
```

#### Step 5: Build DTO
```json
{
  "assetId": "12345678-1234-1234-1234-123456789abc",
  "assetName": "Dump Truck #001",
  "assetNumber": "VEH-001",
  "currentMileage": 10250,
  "currentOperatingHours": 407,
  "currentCycles": 875,
  "averageDailyMileage": 141.67,
  "averageDailyOperatingHours": 6.17,
  "totalFuelConsumed": 1026.0,
  "lastRecordedAt": "2025-01-07T12:00:00Z",
  "totalRecords": 7
}
```

---

## 🎯 Key Insights

### 1. Current Values vs Averages

**Current Values** (Snapshot):
- Show the **most recent state** of the asset
- Useful for: "What is the current mileage?"

**Average Values** (Trend):
- Show the **rate of usage** over time
- Useful for: "How fast is this asset being used?"
- Helps predict: "When will maintenance be due?"

### 2. Time Window (30 Days)

The method uses a **30-day rolling window** for averages:
- Only looks at recent records (last 30 days)
- Provides relevant, up-to-date trends
- Ignores old data that might skew averages

**Why 30 days?**
- Recent enough to be relevant
- Long enough to smooth out daily variations
- Industry standard for short-term trend analysis

### 3. Null Safety

All optional fields use nullable types (`decimal?`, `int?`, `DateTime?`):
- If no records exist → Returns nulls for current values
- If no mileage recorded → `CurrentMileage` is null
- If only 1 record → Cannot calculate averages (needs at least 2 points)

### 4. Multi-Tenant Isolation

```csharp
u.TenantId == _currentUserService.TenantId
```

**Critical**: Always filters by tenant
- Ensures users only see their organization's data
- Prevents data leakage between tenants
- Maintains security in multi-tenant architecture

---

## 🔗 Usage in Trigger Evaluation

### How It's Used by MaintenanceTriggerEvaluationService

```csharp
private async Task<bool> EvaluateUsageBasedTriggerAsync(MaintenanceSchedule schedule)
{
    var usageSummary = await _usageTrackingService.GetAssetUsageSummaryAsync(schedule.AssetId);
    
    // Check mileage trigger
    if (schedule.MileageTrigger.HasValue && usageSummary.CurrentMileage.HasValue)
    {
        if (usageSummary.CurrentMileage.Value >= schedule.MileageTrigger.Value)
        {
            // TRIGGER MAINTENANCE!
            return true;
        }
    }
    
    // Check operating hours trigger
    if (schedule.OperatingHoursTrigger.HasValue && usageSummary.CurrentOperatingHours.HasValue)
    {
        if (usageSummary.CurrentOperatingHours.Value >= schedule.OperatingHoursTrigger.Value)
        {
            // TRIGGER MAINTENANCE!
            return true;
        }
    }
}
```

**Example**:
```
Schedule: "Oil Change at 10,300 km"
Current Mileage: 10,250 km
MileageTrigger: 10,300 km

Check: 10,250 >= 10,300? NO (not yet)

[50km later...]

Current Mileage: 10,350 km
Check: 10,350 >= 10,300? YES → Generate Work Order!
```

---

## 🛠️ API Endpoint

**Controller**: `AssetUsageTrackingController`
**Endpoint**: `GET /api/maintenance/usage-tracking/asset/{assetId}/summary`

**Example Request**:
```bash
GET http://localhost:5000/api/maintenance/usage-tracking/asset/12345678-1234-1234-1234-123456789abc/summary
Authorization: Bearer {token}
```

**Example Response**:
```json
{
  "assetId": "12345678-1234-1234-1234-123456789abc",
  "assetName": "Dump Truck #001",
  "assetNumber": "VEH-001",
  "currentMileage": 10250.00,
  "currentOperatingHours": 407.00,
  "currentCycles": 875,
  "averageDailyMileage": 141.67,
  "averageDailyOperatingHours": 6.17,
  "totalFuelConsumed": 1026.00,
  "lastRecordedAt": "2025-01-07T12:00:00Z",
  "totalRecords": 7,
  "nextMaintenanceDue": null,
  "mileageUntilMaintenance": null,
  "hoursUntilMaintenance": null
}
```

---

## 📝 Related Methods

### GetAllAssetUsageSummariesAsync()
```csharp
public async Task<IEnumerable<AssetUsageSummaryDto>> GetAllAssetUsageSummariesAsync()
{
    var assets = await _assetService.GetAllAssetsAsync();
    var summaries = new List<AssetUsageSummaryDto>();
    
    foreach (var asset in assets)
    {
        summaries.Add(await GetAssetUsageSummaryAsync(asset.Id));
    }
    
    return summaries;
}
```

**Purpose**: Get summaries for ALL assets (used in Condition Monitoring dashboard)
**Endpoint**: `GET /api/maintenance/usage-tracking/summaries`

---

## ✅ Summary

**What It Does**:
1. Validates asset exists
2. Gets all usage records for the asset
3. Finds the most recent values (current state)
4. Calculates averages from last 30 days (trends)
5. Returns comprehensive summary

**Key Data Points**:
- **Current State**: Latest mileage, hours, cycles
- **Trends**: Average daily usage rates
- **Totals**: Total fuel consumed, total records
- **Metadata**: Asset info, last updated timestamp

**Used By**:
- Condition Monitoring Dashboard (display current state)
- Trigger Evaluation Service (check if maintenance needed)
- Asset Health Scoring (calculate health metrics)
- Reports & Analytics (usage trend analysis)

This method is the **core data source** for usage-based maintenance decisions! 🎯
