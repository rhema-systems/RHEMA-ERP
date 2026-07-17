# Extracted: FACILITIES MANAGEMENT ERP MODULE.docx

[1] (Normal) FACILITIES MANAGEMENT ERP MODULE
[2] (Normal) PROCESS FLOW & FUNCTIONAL REQUIREMENT DOCUMENT
[4] (Normal) 1. Purpose of the System
[5] (Normal) The Facilities Management ERP module will provide a centralized platform to manage properties, tenants, leases, maintenance activities, service providers, complaints, financial transactions, assets, and operational reporting.
[6] (Normal) The system should improve:
[7] (Normal) Visibility of facility operations
[8] (Normal) Tracking of service requests
[9] (Normal) Lease monitoring
[10] (Normal) Revenue collection
[11] (Normal) Cost control
[12] (Normal) Document management
[13] (Normal) Decision-making through reports
[15] (Normal) 2. User Roles & Access Control
[16] (Normal) Users
[17] (List Paragraph) Facilities Manager / Supervisor
[18] (Normal) Responsibilities:
[19] (Normal) Approvals
[20] (Normal) Monitoring operations
[21] (Normal) Reviewing reports
[22] (Normal) Managing sites and clients
[23] (List Paragraph) Facility Officer
[24] (Normal) Responsibilities:
[25] (Normal) Lease processing
[26] (Normal) Complaint handling
[27] (Normal) Maintenance coordination
[28] (Normal) Billing activities
[29] (List Paragraph) Finance Officer
[30] (Normal) Responsibilities:
[31] (Normal) Payment confirmation
[32] (Normal) Invoice verification
[33] (Normal) Financial reporting
[34] (List Paragraph) Service Provider / Contractor
[35] (Normal) Responsibilities:
[36] (Normal) View assigned jobs
[37] (Normal) Upload invoices
[38] (Normal) Update job completion status
[39] (List Paragraph) Customer / Tenant Portal User
[40] (Normal) Responsibilities:
[41] (Normal) Submit complaints
[42] (Normal) View invoices
[43] (Normal) Upload documents
[44] (Normal) Sign agreements electronically
[46] (Normal) 3. ERP MODULE STRUCTURE
[47] (Normal) MODULE 1: USER MANAGEMENT
[48] (Normal) Process Flow
[49] (Normal) User Registration
↓
Role Assignment
↓
System Approval
↓
Login Access
↓
Dashboard Access Based on Role
[51] (Normal) System Requirements
[52] (Normal) The system must allow:
[53] (Normal) ✓ User registration
✓ Login authentication
✓ Password reset
✓ Password change
✓ Profile update
✓ Role-based access permissions
✓ User activity tracking
[54] (Normal) (From user stories: Facility Officers must be able to register, login, recover passwords and update profiles.)
[56] (Normal) MODULE 2: PROPERTY & SITE MANAGEMENT
[58] (Normal) Purpose
[59] (Normal) Maintain all properties and locations managed by Facilities.
[61] (Normal) Process Flow
[62] (Normal) Add Site
↓
Create Property Record
↓
Capture Units/Spaces
↓
Assign Occupancy Status
↓
Monitor Availability
[64] (Normal) Required Data Fields
[66] (Normal) Site Information
[67] (Normal) Site Name
[68] (Normal) Location
[69] (Normal) Property Type
[70] (Normal) Number of Units
[71] (Normal) Responsible Officer
[72] (Normal) Properties
[73] (Normal) Property ID
[74] (Normal) Floor/Unit Number
[75] (Normal) Size
[76] (Normal) Status:
[77] (Normal) Available
[78] (Normal) Occupied
[79] (Normal) Under Maintenance
[80] (Normal) The document requires ability to add and retrieve sites and property information.
[82] (Normal) MODULE 3: LEASE MANAGEMENT
[84] (Normal) Process Flow
[85] (Normal) Prospective Client Registration
↓
Property Viewing
↓
Client Interest Confirmation
↓
Generate Proforma Invoice
↓
Draft Lease Agreement
↓
Electronic Signing
↓
Payment Confirmation
↓
Property Allocation
↓
Lease Monitoring
[87] (Normal) Requirements
[89] (Normal) Client Database
[90] (Normal) Capture:
[91] (Normal) Client Name
[92] (Normal) Company Name
[93] (Normal) Contact Details
[94] (Normal) Business Registration Documents
[95] (Normal) Legal Documents
[96] (Normal) Lease Details
[97] (Normal) System must capture:
[98] (Normal) Lease Start Date
[99] (Normal) Lease End Date
[100] (Normal) Lease Duration
[101] (Normal) Rental Amount
[102] (Normal) Renewal Date
[103] (Normal) Termination Date
[104] (Normal) Lease Status
[105] (Normal) Automation Required
[106] (Normal) System alerts:
[107] (Normal) Lease expiry reminders
[108] (Normal) Renewal notifications
[109] (Normal) Billing reminders
[110] (Normal) The document specifically requires lease reports, lease expiry prompts, electronic signing, document storage, and property allocation after payment confirmation.
[112] (Normal) MODULE 4: MAINTENANCE MANAGEMENT
[113] (Normal) Process Flow
[114] (Normal) Maintenance Request Raised
↓
Complaint/Issue Logged
↓
Priority Assigned
↓
Technician/Contractor Assigned
↓
Timeline Set
↓
Work Completed
↓
Inspection
↓
Closure
[116] (Normal) Maintenance Categories
[117] (Normal) Preventive Maintenance
[118] (Normal) For:
[119] (Normal) AC units
[120] (Normal) Generators
[121] (Normal) Equipment
[122] (Normal) Building systems
[123] (Normal) Corrective Maintenance
[124] (Normal) For:
[125] (Normal) Electrical faults
[126] (Normal) Plumbing issues
[127] (Normal) Civil works
[128] (Normal) Repairs
[129] (Normal) Required Fields
[130] (Normal) Request Number
[131] (Normal) Date Reported
[132] (Normal) Location
[133] (Normal) Problem Description
[134] (Normal) Priority Level
[135] (Normal) Assigned Service Provider
[136] (Normal) Estimated Completion Date
[137] (Normal) Actual Completion Date
[138] (Normal) Cost
[139] (Normal) Status
[141] (Normal) MODULE 5: COMPLAINT MANAGEMENT
[142] (Normal) Process Flow
[143] (Normal) Tenant Complaint Submitted
↓
Complaint Logged
↓
Assigned to Officer
↓
Investigation
↓
Resolution
↓
Customer Feedback
↓
Closure
[144] (Normal) Requirements:
[145] (Normal) System must allow:
[146] (Normal) ✓ Complaint registration
✓ Tracking status
✓ Escalation
✓ Resolution history
✓ Reports
[147] (Normal) The uploaded document requires complaint capturing and tracking until resolution.
[149] (Normal) MODULE 6: SERVICE PROVIDER MANAGEMENT
[150] (Normal) Process Flow
[151] (Normal) Register Contractor
↓
Approve Contractor
↓
Assign Work
↓
Track Performance
↓
Process Invoice
[153] (Normal) Database Requirements
[154] (Normal) Capture:
[155] (Normal) Company Name
[156] (Normal) Contact Person
[157] (Normal) Phone
[158] (Normal) Email
[159] (Normal) Service Category
[160] (Normal) Contract Period
[161] (Normal) Rates
[162] (Normal) Performance Records
[163] (Normal) Required for plumbers, electricians, engineers, cleaners, and other contractors.
[167] (Normal) MODULE 7: STAFF & CLEANER MANAGEMENT
[168] (Normal) Requirements
[169] (Normal) Maintain:
[170] (Normal) Employee Name
[171] (Normal) Contact Details
[172] (Normal) Assigned Location
[173] (Normal) Duty Schedule
[174] (Normal) Status
[175] (Normal) For cleaners:
[176] (Normal) Name
[177] (Normal) Assigned Site
[178] (Normal) Attendance
[179] (Normal) Work Status
[180] (Normal) The document requires a cleaner database and staff assignment capability.
[183] (Normal) MODULE 8: INVENTORY MANAGEMENT
[184] (Normal) Process Flow
[185] (Normal) Stock Received
↓
Inventory Updated
↓
Items Issued
↓
Balance Updated
↓
Reorder Alert
[186] (Normal) Items:
[187] (Normal) Cleaning materials
[188] (Normal) Consumables
[189] (Normal) Maintenance supplies
[190] (Normal) Required:
[191] (Normal) Item Code
[192] (Normal) Description
[193] (Normal) Quantity Available
[194] (Normal) Minimum Stock Level
[195] (Normal) Supplier
[196] (Normal) Issue History
[200] (Normal) MODULE 9: FINANCIAL MANAGEMENT
[201] (Normal) A. Client Billing
[202] (Normal) Process:
[203] (Normal) Service/Lease Created
↓
Invoice Generated
↓
Invoice Sent
↓
Payment Received
↓
Payment Recorded
[204] (Normal) Requirements:
[205] (Normal) Invoice Number
[206] (Normal) Customer
[207] (Normal) Amount
[208] (Normal) Tax
[209] (Normal) Due Date
[210] (Normal) Payment Status
[212] (Normal) B. Contractor Payments
[213] (Normal) Process:
[214] (Normal) Service Completed
↓
Invoice Submitted
↓
Verification
↓
Approval
↓
Payment
[215] (Normal) Requirements:
[216] (Normal) Contractor Invoice
[217] (Normal) Work Order Reference
[218] (Normal) Amount
[219] (Normal) Approval Status
[220] (Normal) Payment Date
[221] (Normal) The document requires capturing contractor invoices, payments, client billing and financial documents.
[224] (Normal) MODULE 10: BUDGET MANAGEMENT
[225] (Normal) Process Flow
[226] (Normal) Create Budget
↓
Allocate Resources
↓
Track Expenses
↓
Compare Budget vs Actual
[227] (Normal) Requirements:
[228] (Normal) Budget Year
[229] (Normal) Department
[230] (Normal) Planned Amount
[231] (Normal) Actual Expense
[232] (Normal) Variance
[234] (Normal) MODULE 11: ASSET MANAGEMENT
[235] (Normal) Asset Register
[236] (Normal) Capture:
[237] (Normal) Asset Number
[238] (Normal) Description
[239] (Normal) Location
[240] (Normal) Purchase Date
[241] (Normal) Cost
[242] (Normal) Warranty
[243] (Normal) Maintenance History
[244] (Normal) Current Status
[246] (Normal) MODULE 12: DOCUMENT MANAGEMENT
[247] (Normal) System must support:
[248] (Normal) Upload:
[249] (Normal) Lease agreements
[250] (Normal) Certificates
[251] (Normal) Contracts
[252] (Normal) Invoices
[253] (Normal) Payment records
[254] (Normal) Property documents
[255] (Normal) Requirements:
[256] (Normal) ✓ Scan documents
✓ Store securely
✓ Search and retrieve
✓ Attach documents to records
[257] (Normal) The document requires storage of legal, non-legal, and financial documents.
[259] (Normal) MODULE 13: REPORTING & DASHBOARD
[260] (Normal) Operational Reports
[261] (Normal) Include:
[262] (Normal) Open complaints
[263] (Normal) Maintenance status
[264] (Normal) Contractor performance
[265] (Normal) Inventory status
[266] (Normal) Lease Reports
[267] (Normal) Include:
[268] (Normal) Tenant list
[269] (Normal) Lease expiry
[270] (Normal) Occupancy
[271] (Normal) Revenue
[272] (Normal) Financial Reports
[273] (Normal) Include:
[274] (Normal) Invoice reports
[275] (Normal) Payment status
[276] (Normal) Expenses
[277] (Normal) Taxes
[278] (Normal) Reports should support:
[279] (Normal) PDF export
[280] (Normal) Excel/CSV export
[281] (Normal) The document specifies operational, lease, and financial reporting capabilities.
[283] (Normal) SYSTEM USER EXPERIENCE REQUIREMENTS
[284] (Normal) For the ERP to be user-friendly:
[286] (Normal) Dashboard
[287] (Normal) Each user should see:
[288] (Normal) Pending tasks
[289] (Normal) Notifications
[290] (Normal) Approvals
[291] (Normal) Upcoming deadlines
[292] (Normal) Outstanding payments
[293] (Normal) Search Function
[294] (Normal) Users should search by:
[295] (Normal) Client name
[296] (Normal) Property
[297] (Normal) Invoice number
[298] (Normal) Complaint number
[299] (Normal) Asset number
[300] (Normal) Notifications
[301] (Normal) Automatic alerts for:
[302] (Normal) Lease expiry
[303] (Normal) Billing dates
[304] (Normal) Maintenance deadlines
[305] (Normal) Low inventory
[306] (Normal) Pending approvals
[308] (Normal) HIGH LEVEL ERP WORKFLOW MAP
[309] (Normal) CLIENT
[310] (Normal) |
[311] (Normal) ↓
[312] (Normal) Lease Request
[313] (Normal) |
[314] (Normal) ↓
[315] (Normal) Property Allocation
[316] (Normal) |
[317] (Normal) ↓
[318] (Normal) Billing
[319] (Normal) |
[320] (Normal) ↓
[321] (Normal) Payment Confirmation
[322] (Normal) |
[323] (Normal) ↓
[324] (Normal) Facility Operations
[325] (Normal) |
[326] (Normal) ↓
[327] (Normal) Maintenance / Complaints
[328] (Normal) |
[329] (Normal) ↓
[330] (Normal) Service Providers
[331] (Normal) |
[332] (Normal) ↓
[333] (Normal) Invoice Processing
[334] (Normal) |
[335] (Normal) ↓
[336] (Normal) Reports & Management Decisions
[337] (Normal) Recommended Additional ERP Features
[338] (Normal) To make the system complete:
[339] (Normal) Mobile-friendly interface
[340] (Normal) Approval workflow
[341] (Normal) Audit trail
[342] (Normal) Email/SMS notifications
[343] (Normal) Document expiry alerts
[344] (Normal) Vendor performance rating
[345] (Normal) SLA monitoring
[346] (Normal) Integration with Finance/Accounting ERP
[347] (Normal) Asset QR code tracking
[348] (Normal) Tenant self-service portal