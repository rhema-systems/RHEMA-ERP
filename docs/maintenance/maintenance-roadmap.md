# Maintenance Module Roadmap

This document captures the phased roadmap and backlog for the Maintenance Management System, based on requirements 1.2.1–1.2.7.

## Complexity Scale

- **S** = 0.5–2 dev days  
- **M** = 2–4 dev days  
- **L** = 4–10 dev days

---

## Phase 1 – MVP for Go‑Live (Must‑Have)

Focus: safely operate day‑to‑day maintenance with correct data, minimal rework, and basic compliance.

### P1‑E1: Maintenance Admin Basics (1.2.1, 1.2.5, 1.2.7)

Goal: Make maintenance configuration manageable by admins, not developers.

- **P1‑E1‑S1 – Backend validation for maintenance configuration (S)**  
  Ensure `MaintenanceType`, priority levels, and work order types are validated (allowed categories, uniqueness, etc.).
- **P1‑E1‑S2 – Admin page: Maintenance Types Management (M)**  
  Admin route (e.g. `/admin/maintenance/types`) with CRUD for maintenance types (category, class, location, condition-based).
- **P1‑E1‑S3 – Admin page: Priority Levels Management (S–M)**  
  Admin route (e.g. `/admin/maintenance/priorities`) with CRUD for maintenance priorities and optional SLA fields.
- **P1‑E1‑S4 – Admin page: Work Order Types Management (S–M)**  
  Admin route (e.g. `/admin/maintenance/work-order-types`) with CRUD for work order types (preventive, corrective, emergency, etc.).
- **P1‑E1‑S5 – Use configured types/priorities in Job Card & Schedule UIs (S)**  
  Replace hardcoded lists in job card/schedule forms with these configured values.

### P1‑E2: Robust Job Card → Work Order Workflow (1.2.2)

Goal: Enforce a clear, auditable lifecycle from request to execution.

- **P1‑E2‑S1 – Define and document job card state machine (S)**  
  Agree and document allowed statuses and transitions.
- **P1‑E2‑S2 – Enforce state transitions in JobCard service (M)**  
  Implement guards in the job card service based on the state machine; block invalid transitions.
- **P1‑E2‑S3 – Supervisor approval enforcement (M)**  
  Require at least one supervisor approval before generating a work order and/or starting work.
- **P1‑E2‑S4 – UI: Approval status & actions on JobCardManagement (M)**  
  Show approval status, approvers, timestamps; expose Approve / Reject / Request Changes actions.
- **P1‑E2‑S5 – Traceability between Job Card and Work Order (S)**  
  Ensure job card pages link to generated work orders and vice versa.

### P1‑E3: Minimal QC Gate Before Completion (1.2.7)

Goal: Ensure work orders that require QC/inspection cannot be completed without sign‑off.

- **P1‑E3‑S1 – Define when QC is required (S)**  
  Simple rules by work order type and/or asset category indicating when QC is mandatory.
- **P1‑E3‑S2 – Implement QualityValidationResult checks in completion flow (M)**  
  In completion logic, call QC validation and block completion when `CanComplete` is false.
- **P1‑E3‑S3 – Minimal inspection/checklist handling (M)**  
  Ensure that, when QC is required, at least one inspection/checklist record by a supervisor exists.
- **P1‑E3‑S4 – Replace key mock QC endpoints with real data (M)**  
  Implement non-mock versions of the Inspection endpoints used by the QC gate.
- **P1‑E3‑S5 – UI: Show QC/inspection status and block completion (M)**  
  On work order detail, show inspection status and disable completion until QC passes.

### P1‑E4: Admission & Discharge Basic Flow (1.2.3)

Goal: Track when an asset enters and leaves maintenance, with condition/readings and downtime.

- **P1‑E4‑S1 – Decide primary admission workflow (S)**  
  Decide whether admission is driven via a dedicated admission screen or captured from job card/work order.
- **P1‑E4‑S2 – Implement admission details capture (M)**  
  Capture condition, mileage/hours, bay/station, etc., at admission.
- **P1‑E4‑S3 – Integrate downtime tracking (M–L)**  
  Create/close downtime records automatically on admission and discharge/completion.
- **P1‑E4‑S4 – UI: Show admission/discharge info and downtime (M)**  
  Display admission/discharge details and total downtime on asset, job card, and work order views.

### P1‑E5: Core Resource & Parts Usage Integration (1.2.4, 1.2.5)

Goal: Capture "who did what" and "what parts were used" reliably per work order.

- **P1‑E5‑S1 – Technician assignment on work orders (M)**  
  Backend and UI support for assigning technicians and viewing assignments on work order details.
- **P1‑E5‑S2 – Parts/consumables management in UI (M)**  
  Use existing parts endpoints to add/edit/delete/return parts and show parts costs per work order.
- **P1‑E5‑S3 – Basic labor & cost summary per work order (M)**  
  Combine labor hours and parts costs to show estimated vs actual cost.
- **P1‑E5‑S4 – Manual travel/fuel expense entry (S–M)**  
  Simple UI to add travel/fuel expenses linked to a work order using MaintenanceExpense.

---

## Phase 2 – Near‑Term Enhancements (Should‑Have)

Focus: improving operational maturity, visibility, and external collaboration.

### P2‑E1: Full QC & Inspection Module (1.2.7)

- **P2‑E1‑S1 – Real inspection history by work order & asset (M–L)**  
  Implement endpoints to fetch real inspection records, including rework/rejections.
- **P2‑E1‑S2 – Regulatory compliance status endpoint (M)**  
  Compute compliance based on completed inspections and certificate validity.
- **P2‑E1‑S3 – Implement rework/rejection flows (L)**  
  Use rework/rejection entities to reopen or spawn follow-up work orders, with UI support.
- **P2‑E1‑S4 – Inspection dashboard UI (M–L)**  
  Dashboard for active/overdue inspections, failure rates, filters by asset/location/technician.
- **P2‑E1‑S5 – Inspection report generation (M–L)**  
  Generate structured inspection reports (PDF/HTML) for audits.

### P2‑E2: External Maintenance / Contractors (1.2.6)

- **P2‑E2‑S1 – Contractor master data UI (M)**  
  CRUD for contractors with capabilities, contacts, and basic SLAs.
- **P2‑E2‑S2 – Assign contractors to job cards/work orders (M)**  
  Extend forms to assign contractors and distinguish internal vs external jobs.
- **P2‑E2‑S3 – Contractor invoices & approvals (L)**  
  CRUD and approval flow for contractor invoices, with maintenance/finance views.
- **P2‑E2‑S4 – Contractor logistics & expenses (M)**  
  Capture logistics (transport, shipping) and related expenses for contractor jobs.
- **P2‑E2‑S5 – Baseline integration with Finance/AP (M–L)**  
  Expose invoice status or exports for AP processing; plan deeper integration later.

### P2‑E3: Resource Planning & Vehicles (1.2.4, 1.2.5)

- **P2‑E3‑S1 – Technician capacity & schedule views (L)**  
  Calendar/board views using staff schedule and workload data.
- **P2‑E3‑S2 – Maintenance vehicle management (M)**  
  CRUD UI for maintenance vehicles and linking them to off-site work orders.
- **P2‑E3‑S3 – Enhanced expense views (M)**  
  Expense summaries per work order, per technician, and per vehicle.

### P2‑E4: Reporting & Compliance (1.2.5, 1.2.7)

- **P2‑E4‑S1 – Data-driven schedule compliance report (M)**  
  Real calculations using schedule history and work order data.
- **P2‑E4‑S2 – Asset maintenance history view (M–L)**  
  End-to-end view of job cards, work orders, inspections, downtime, and expenses per asset.
- **P2‑E4‑S3 – Regulatory dashboard (M–L)**  
  High-level view of upcoming/overdue regulatory inspections and certificates.

---

## Phase 3 – Advanced / Optimization (Nice‑to‑Have)

Focus: predictive, optimized, and high‑automation operations.

### P3‑E1: Condition-Based & Predictive Maintenance (1.2.1, 1.2.5)

- **P3‑E1‑S1 – Model condition metrics & rules (M)**  
  Extend entities/DTOs to store condition readings and rule definitions.
- **P3‑E1‑S2 – Implement condition trigger evaluation (L)**  
  Fully implement condition trigger evaluation and generate work orders when thresholds are breached.
- **P3‑E1‑S3 – Integrate with IoT/telemetry or manual feeds (L)**  
  Define APIs/interfaces for ingesting condition data from external systems.
- **P3‑E1‑S4 – Predictive dashboards (L)**  
  Show remaining useful life, risk scoring, and recommended interventions.

### P3‑E2: Advanced Workflow Engine Integration (1.2.2, 1.2.7)

- **P3‑E2‑S1 – Finish EnhancedMaintenanceWorkflowService (L)**  
  Implement configurable multi-step approvals and escalation logic.
- **P3‑E2‑S2 – Tenant-level workflow configuration UI (L)**  
  Allow per-tenant workflows for job cards, work orders, and QC.
- **P3‑E2‑S3 – Visual workflow representation (M–L)**  
  UI diagrams/timelines of workflow stages for a job card or work order.

### P3‑E3: Advanced Analytics & Optimization (All 1.2.x)

- **P3‑E3‑S1 – KPI dashboards (L)**  
  MTBF, MTTR, cost per asset/contractor/technician, etc.
- **P3‑E3‑S2 – Technician schedule optimization (L)**  
  Use optimization models to suggest technician assignments and routes.
- **P3‑E3‑S3 – Scenario simulation tools (L)**  
  "What if" analyses (e.g., additional shifts, changed frequencies) to assess impact on downtime and cost.

