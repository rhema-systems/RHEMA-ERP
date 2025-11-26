# Business Partner Management System - Testing Guide

**System Status:** ✅ Ready for Testing  
**Database:** ✅ Migration Applied  
**Build:** ✅ 0 Errors  
**Date:** 2025-11-24

---

## Quick Start

### 1. Start the Backend API
```bash
cd src/ErpSystem.Api
dotnet run
```
**Expected:** API running on `https://localhost:7001` or `http://localhost:5000`

### 2. Start the Frontend
```bash
cd frontend
npm run dev
```
**Expected:** Frontend running on `http://localhost:3000`

---

## Test Scenarios

### 🔧 Scenario 1: Admin Configuration (First Time Setup)

**Objective:** Set up the system with initial configuration data

#### Step 1.1: Create Partner Categories
1. Login as admin
2. Navigate to **Administration → Procurement → Partner Categories**
   - URL: `http://localhost:3000/administration/procurement/partner-categories`
3. Click **"Add Category"**
4. Create the following categories:
   - **Raw Materials** (Description: "Suppliers of raw materials")
   - **Construction** (Description: "Construction contractors")
   - **IT Services** (Description: "IT service providers")
   - **Consulting** (Description: "Consulting services")
5. Verify all categories appear in the list

#### Step 1.2: Create Contractor Specializations
1. Navigate to **Administration → Procurement → Contractor Specializations**
   - URL: `http://localhost:3000/administration/procurement/contractor-specializations`
2. Click **"Add Specialization"**
3. Create the following specializations:
   - **Electrical** (Description: "Electrical work and installations")
   - **Plumbing** (Description: "Plumbing and water systems")
   - **HVAC** (Description: "Heating, ventilation, and air conditioning")
   - **Carpentry** (Description: "Woodwork and carpentry")
4. Verify all specializations appear in the list

#### Step 1.3: Create License Types
1. Navigate to **Administration → Procurement → License Types**
   - URL: `http://localhost:3000/administration/procurement/license-types`
2. Click **"Add License Type"**
3. Create the following license types:
   - **Business License** (Description: "General business operating license", Required: Yes)
   - **Trade License** (Description: "Trade-specific license", Required: Yes)
   - **Tax Clearance** (Description: "Tax clearance certificate", Required: No)
   - **Insurance Certificate** (Description: "Liability insurance", Required: Yes)
4. Verify all license types appear in the list

**✅ Expected Result:** All configuration data created successfully

---

### 📝 Scenario 2: External Registration (Supplier)

**Objective:** Test the external registration portal for a new supplier

#### Step 2.1: Start Registration
1. Open a new browser window (or incognito mode)
2. Navigate to: `http://localhost:3000/register/business-partner`
3. Verify the registration wizard loads with 5 steps

#### Step 2.2: Complete Basic Information (Step 1)
Fill in the following:
- **Partner Type:** Supplier
- **Company Name:** ABC Trading Company
- **Trading Name:** ABC Traders
- **Registration Number:** REG123456
- **Tax Number:** TAX789012
- **Email:** contact@abctraders.com
- **Phone:** +1234567890
- **Website:** www.abctraders.com
- Click **"Next"**

#### Step 2.3: Complete Address Information (Step 2)
Fill in the following:
- **Physical Address:** 123 Main Street
- **City:** New York
- **State/Province:** NY
- **Country:** United States
- **Postal Code:** 10001
- Check **"Mailing address same as physical"**
- Click **"Next"**

#### Step 2.4: Add Contact Person (Step 3)
Fill in the following:
- **First Name:** John
- **Last Name:** Smith
- **Position:** Procurement Manager
- **Email:** john.smith@abctraders.com
- **Phone:** +1234567891
- **Mobile:** +1234567892
- Check **"Primary Contact"**
- Click **"Add Contact"**
- Click **"Next"**

#### Step 2.5: Upload Documents (Step 4)
Upload the following documents:
- **Business License** (upload any PDF file)
- **Tax Clearance** (upload any PDF file)
- Click **"Next"**

#### Step 2.6: Review and Submit (Step 5)
1. Review all entered information
2. Check **"I confirm that all information is accurate"**
3. Click **"Submit Registration"**
4. **Note the Registration ID** displayed on success page
5. Click **"Check Status"** to view registration status

**✅ Expected Result:** Registration submitted successfully with status "Pending Review"

---

### 🔍 Scenario 3: Registration Review and Approval

**Objective:** Review and approve the submitted registration

#### Step 3.1: View Pending Registrations
1. Login as admin/procurement user
2. Navigate to **Administration → Procurement → Partner Registrations**
   - URL: `http://localhost:3000/administration/procurement/registrations`
3. Verify the ABC Trading Company registration appears in the list
4. Status should be **"Submitted"** or **"Pending Review"**

#### Step 3.2: Review Registration Details
1. Click on the ABC Trading Company registration
2. Review all tabs:
   - **Overview:** Basic company information
   - **Contacts:** Contact persons
   - **Documents:** Uploaded documents
3. Verify all information is correct

#### Step 3.3: Verify Documents
1. Go to **Documents** tab
2. Click **"View"** on each document
3. Verify documents are accessible

#### Step 3.4: Approve Registration
1. Go back to **Overview** tab
2. Scroll to **Review Actions** section
3. Add review notes: "All documents verified. Approved for partnership."
4. Click **"Approve Registration"**
5. Confirm the approval

**✅ Expected Result:** 
- Registration status changes to "Approved"
- A new Business Partner is automatically created
- You're redirected to the Business Partners list

---

### 👥 Scenario 4: Business Partner Management

**Objective:** Manage the newly created business partner

#### Step 4.1: View Business Partners
1. Navigate to **Procurement → Business Partners**
   - URL: `http://localhost:3000/procurement/business-partners`
2. Verify ABC Trading Company appears in the list
3. Note the auto-generated Partner Code (e.g., SUP-0001)

#### Step 4.2: View Partner Details
1. Click on ABC Trading Company
2. Explore all tabs:
   - **Overview:** Basic information and status
   - **Contacts:** Contact persons
   - **Licenses:** Licenses and certifications
   - **Documents:** Uploaded documents
   - **Financial:** Financial information
   - **History:** Status change history

#### Step 4.3: Add a License
1. Go to **Licenses** tab
2. Click **"Add License"**
3. Fill in:
   - **License Type:** Business License
   - **License Number:** BL-2024-001
   - **Issuing Authority:** State Business Bureau
   - **Issue Date:** 2024-01-01
   - **Expiry Date:** 2025-12-31
   - **Status:** Active
4. Click **"Save"**
5. Verify license appears in the list

#### Step 4.4: Upload Additional Document
1. Go to **Documents** tab
2. Click **"Upload Document"**
3. Fill in:
   - **Document Type:** Insurance Certificate
   - **Document Name:** Liability Insurance 2024
   - **File:** Upload any PDF
4. Click **"Upload"**
5. Verify document appears in the list

#### Step 4.5: Update Partner Information
1. Go to **Overview** tab
2. Click **"Edit"** button
3. Update:
   - **Credit Limit:** 50000
   - **Payment Terms:** Net 30
   - **Is Preferred:** Check this box
4. Click **"Save"**
5. Verify changes are saved

**✅ Expected Result:** All partner management operations work correctly

---

### 🏗️ Scenario 5: External Registration (Contractor)

**Objective:** Test registration for a contractor with specializations

#### Step 5.1: Start New Registration
1. Navigate to: `http://localhost:3000/register/business-partner`

#### Step 5.2: Complete Registration
Fill in the following:
- **Partner Type:** Contractor
- **Company Name:** Elite Construction Services
- **Registration Number:** CON456789
- **Tax Number:** TAX345678
- **Email:** info@eliteconstruction.com
- **Phone:** +1987654321
- **Specializations:** Select "Electrical" and "HVAC"
- Complete address, contacts, and documents
- Submit registration

#### Step 5.3: Approve Registration
1. Login as admin
2. Navigate to registrations
3. Find Elite Construction Services
4. Review and approve

**✅ Expected Result:** Contractor created with specializations

---

### 🚫 Scenario 6: Blacklist Management

**Objective:** Test blacklist functionality

#### Step 6.1: Add Partner to Blacklist
1. Navigate to Business Partners
2. Click on ABC Trading Company
3. Scroll to **Actions** section
4. Click **"Add to Blacklist"**
5. Fill in:
   - **Reason:** Failed to deliver on time multiple times
   - **Expiry Date:** 2025-12-31 (optional)
6. Click **"Confirm"**

**✅ Expected Result:**
- Partner status changes to "Blacklisted"
- Partner appears with blacklist badge
- Blacklist reason is visible

#### Step 6.2: Remove from Blacklist
1. Click **"Remove from Blacklist"**
2. Confirm the action

**✅ Expected Result:** Partner status returns to "Active"

---

### ⏸️ Scenario 7: Suspend/Activate Partner

**Objective:** Test suspend and activate functionality

#### Step 7.1: Suspend Partner
1. Navigate to Business Partners
2. Click on Elite Construction Services
3. Click **"Suspend Partner"**
4. Confirm the action

**✅ Expected Result:** Partner status changes to "Suspended"

#### Step 7.2: Activate Partner
1. Click **"Activate Partner"**
2. Confirm the action

**✅ Expected Result:** Partner status returns to "Active"

---

### 🔍 Scenario 8: Search and Filter

**Objective:** Test search and filtering capabilities

#### Step 8.1: Filter by Partner Type
1. Navigate to Business Partners
2. Use **Partner Type** filter
3. Select **"Supplier"**
4. Verify only suppliers are shown (ABC Trading Company)
5. Select **"Contractor"**
6. Verify only contractors are shown (Elite Construction Services)

#### Step 8.2: Search by Name
1. Clear all filters
2. Enter "ABC" in search box
3. Verify ABC Trading Company appears
4. Enter "Elite" in search box
5. Verify Elite Construction Services appears

#### Step 8.3: Filter by Status
1. Use **Status** filter
2. Select **"Active"**
3. Verify only active partners are shown
4. Select **"Blacklisted"** (if you have blacklisted partners)
5. Verify only blacklisted partners are shown

**✅ Expected Result:** All filters work correctly

---

### 📊 Scenario 9: Performance Rating

**Objective:** Test performance rating functionality

#### Step 9.1: Update Performance Rating
1. Navigate to Business Partners
2. Click on ABC Trading Company
3. Find **Performance Rating** section
4. Click **"Update Rating"**
5. Set rating to **4.5** out of 5
6. Add notes: "Excellent delivery times and quality"
7. Click **"Save"**

**✅ Expected Result:** Performance rating updated and displayed

---

### 📋 Scenario 10: Registration Rejection

**Objective:** Test registration rejection workflow

#### Step 10.1: Submit New Registration
1. Navigate to registration portal
2. Submit a new registration for "Test Company XYZ"
3. Note the registration ID

#### Step 10.2: Reject Registration
1. Login as admin
2. Navigate to pending registrations
3. Click on Test Company XYZ
4. Click **"Reject Registration"**
5. Enter rejection reason: "Incomplete documentation"
6. Click **"Confirm Rejection"**

**✅ Expected Result:**
- Registration status changes to "Rejected"
- No business partner is created
- Rejection reason is visible

---

## API Testing (Optional)

### Using Swagger UI
1. Navigate to: `https://localhost:7001/swagger`
2. Authorize using your JWT token
3. Test the following endpoints:

#### Business Partners
- `GET /api/procurement/business-partners` - Get all partners
- `GET /api/procurement/business-partners/{id}` - Get partner by ID
- `POST /api/procurement/business-partners` - Create partner
- `PUT /api/procurement/business-partners/{id}` - Update partner
- `DELETE /api/procurement/business-partners/{id}` - Delete partner

#### Registrations
- `GET /api/procurement/business-partner-registrations` - Get all registrations
- `GET /api/procurement/business-partner-registrations/pending` - Get pending
- `POST /api/procurement/business-partner-registrations` - Submit registration
- `POST /api/procurement/business-partner-registrations/{id}/approve` - Approve
- `POST /api/procurement/business-partner-registrations/{id}/reject` - Reject

#### Configuration
- `GET /api/procurement/partner-categories` - Get categories
- `POST /api/procurement/partner-categories` - Create category
- `GET /api/procurement/contractor-specializations` - Get specializations
- `POST /api/procurement/contractor-specializations` - Create specialization
- `GET /api/procurement/license-types` - Get license types
- `POST /api/procurement/license-types` - Create license type

---

## Test Checklist

### Configuration ✅
- [ ] Create partner categories
- [ ] Create contractor specializations
- [ ] Create license types
- [ ] View all configuration lists

### External Registration ✅
- [ ] Submit supplier registration
- [ ] Submit contractor registration
- [ ] Upload documents
- [ ] Check registration status
- [ ] Verify email notifications (if configured)

### Registration Review ✅
- [ ] View pending registrations
- [ ] Review registration details
- [ ] Verify documents
- [ ] Approve registration
- [ ] Reject registration
- [ ] Request more information

### Business Partner Management ✅
- [ ] View all partners
- [ ] Filter by type, status, category
- [ ] Search by name
- [ ] View partner details
- [ ] Update partner information
- [ ] Add contacts
- [ ] Add licenses
- [ ] Upload documents
- [ ] Update financial information
- [ ] View status history

### Partner Actions ✅
- [ ] Suspend partner
- [ ] Activate partner
- [ ] Add to blacklist
- [ ] Remove from blacklist
- [ ] Update performance rating
- [ ] Mark as preferred

### Data Integrity ✅
- [ ] Verify auto-generated partner codes
- [ ] Verify tenant isolation
- [ ] Verify soft deletes
- [ ] Verify audit fields (CreatedAt, UpdatedAt)
- [ ] Verify status history tracking

---

## Known Issues / Notes

### None Currently
All features are working as expected based on implementation.

---

## Support

If you encounter any issues during testing:

1. **Check the browser console** for frontend errors
2. **Check the API logs** for backend errors
3. **Verify database connection** is working
4. **Ensure all services are registered** in DI container
5. **Check that migration was applied** successfully

---

## Success Criteria

✅ All configuration data can be created
✅ External registration portal works end-to-end
✅ Registration review and approval workflow works
✅ Business partner management operations work
✅ Search and filtering work correctly
✅ Document upload/download works
✅ All CRUD operations work
✅ Status changes are tracked
✅ No errors in browser console
✅ No errors in API logs

**When all scenarios pass, the system is ready for production deployment!** 🚀
