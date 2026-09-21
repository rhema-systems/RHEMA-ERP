# TDC Sales, Marketing, And CRM Gap Implementation Tracker

Last updated: 2026-09-21

## Purpose

This tracker is the delivery ledger for closing the gaps between the current ERP implementation and the requirements in `ERP QUESTIONNAIRE.docx`, completed by TDC's Marketing Department for the Sales, Marketing, and Customer Relationship Management module.

The source document defines the expected operating model for property and estate management: lead acquisition, lead scoring, contact management, customer preferences, property sales, rentals, leases, site visits, payment milestones, document generation, marketing campaigns, communications, post-sale support, reporting, integrations, privacy, access control, and audit.

## Source Of Truth And Boundaries

- Business source supplied for revalidation: `C:\Users\micha\AppData\Local\Microsoft\Windows\INetCache\IE\20V5N1JT\ERP_QUESTIONNAIRE[1].docx`
- Original business-source copy: `C:\Users\micha\Downloads\Questionnairs and SOPS\Questionnairs and SOPS\Marketing\ERP QUESTIONNAIRE.docx`
- Source identity check: the supplied cached copy and original Marketing copy have the same SHA-256 digest, `18072D720824C929BA754D2AC81E224553F417ADF999CD352BB0DCE129DFA6DE`.
- Authoritative source render evidence: 11 pages rendered through Microsoft Word to `artifacts\tdc-sales-marketing-crm-srs-revalidation\run-20260717-1\source-render`; the earlier 10-page render in `artifacts\tdc-sales-marketing-crm-doc-render` is superseded for page-count evidence.
- Current implementation evidence: Sales and CRM entities, DTOs, services, controllers, frontend routes, workflow adapters, sales allocation ledger, sales agreements, sales orders, campaigns and calculated ROI, CRM account health/readiness/risk/collaboration/service workspaces, forecasts, conversions, collections, commissions, competitors, project-unit sales handoff, Finance AR customer/payment touchpoints, EHC ticket/problem aggregation, SMS/email settings, and existing `docs/sales-management-tasklist.md`.
- This tracker covers Sales, Marketing, and CRM as one customer lifecycle. It references Finance, Development/Projects, Property/Land, Workflow, Notifications, Document Management, Microsoft/Google integrations, and Compliance only where the questionnaire requires them to complete the end-to-end process.
- This tracker does not certify TDC privacy, data protection, or anti-money-laundering policy interpretation. TDC Marketing, Sales, Finance/Revenue, Estates Development, Legal/Compliance, ICT, and Management must approve final configuration values and acceptance scenarios.

## Delivery Rule

A slice may be marked `Done` only when all applicable parts are complete:

1. Backend domain model and business rules.
2. Database migration and data backfill.
3. API endpoints and authorization.
4. Frontend routes, controls, validation, and status visibility.
5. Workflow, notifications, documents, audit events, and reports where required.
6. Automated tests for happy paths, hard stops, overrides, tenant isolation, and direct API attempts.
7. Migration applied to the test database.
8. Browser/API smoke verification.
9. Tracker evidence updated with commands, routes, and test results.

No requirement is complete merely because a page or field exists. TDC requires enforced behavior from enquiry through conversion, payment, handover, retention, reporting, and audit.

## Status Legend

| Status | Meaning |
| --- | --- |
| Verified baseline | Current implementation has material capability confirmed in source; final TDC acceptance is still required. |
| Partial | Useful capability exists, but required controls, fields, workflow steps, reports, integrations, or hard stops are incomplete or bypassable. |
| Gap | Required capability or enforcement is absent. |
| Configuration required | TDC must approve configurable values, roles, channels, templates, workflows, scoring, reports, or rules before acceptance. |
| Not started | Approved implementation task has not started. |
| In progress | Implementation is actively being changed and is not yet acceptance-ready. |
| Done | Full delivery rule above has passed and evidence is recorded. |
| Blocked | Work cannot safely continue until a named dependency is resolved. |

## Honest Readiness Statement

The ERP already contains a broad Sales/CRM foundation: leads, opportunities, quotes, campaigns, campaign members, campaign budget/cost/revenue and calculated ROI, products, sales orders, sales agreements, allocations/reservations, forecasts, conversions, collections, commissions, competitors, sales reports, CRM account detail/health/readiness/risk/collaboration/service workspaces, shared workflow adapters for major Sales records, project-unit saleable-source integration, Finance AR customer touchpoints, EHC ticket/problem/SLA aggregation, and SMS/email configuration.

The main gap is not the absence of Sales and CRM screens. The main gap is that TDC's property-specific customer journey is not yet enforced as one controlled lifecycle. The implementation must tighten TDC lead stages, automatic assignment, scoring, duplicate merge, canonical customer identity across Lead/Sales/Business Partner/Finance, customer KYC/document capture, property requirement matching, site visits, payment milestones, UCL, handover, key release, cadastral documents, CAM/ground-rent follow-up, amendments, refunds, referral/loyalty tracking, campaign attribution and reconciled ROI, social/WhatsApp engagement capture, daily dashboards, Microsoft/Google integrations, and AML/data-protection controls.

Most "decision" items below should become configuration rather than hard-coded code blockers. They are listed because TDC must approve the values before the system can be acceptance-tested.

## Source Requirement Baseline

The questionnaire contains 56 answered requirement rows. Fifty-five rows are explicitly marked `Must Have`. The customer-segmentation yes/no row has no entered priority, while the immediately following row that defines the required segmentation categories is marked `Must Have`; segmentation is therefore retained in scope, with its final priority to be confirmed by TDC. The item saying "Customer relationship management" may be deferred is itself marked `Must Have`, so the deferral is a phasing decision rather than removal from scope.

| Area | Stated TDC requirement baseline |
| --- | --- |
| Objectives | Centralize lead acquisition, track marketing campaign outcomes, streamline sales/rental/lease processes, and improve post-sales retention. |
| Business lines | Property sales, rentals, and leases. |
| Principal users | Sales and Marketing Manager, Sales and Marketing Officers, Sales and Marketing Assistants, Finance/Revenue for payment confirmation, and Estates Development. |
| Lead channels | Social media engagement, phone calls, and walk-ins. |
| Lead sources | Referrals, sales proposals, and exhibitions. |
| Lead stages | New Lead/Enquiry, Qualified after site viewing, Active after initial deposit, Customer after full payment. |
| Lead scoring | Low after enquiry call, Medium after site viewing, High after initial deposit. |
| Duplicate detection | Flag by identical client name, phone number, and property type; allow manual merge. |
| Contact categories | Individuals, companies, and institutions. |
| Customer segmentation | Individual clients, corporate clients, investors, joint acquisition, mortgage facility, and joint ownership. |
| Sales lifecycle | Enquiry, site visit, application, initial/one-time payment, customer registration, UCL after full payment, handover rules, key release, cadastral form, Ghana card, CAM fee, ground rent, amendments, refunds, and ITF. |
| Marketing channels | Facebook, TikTok, Instagram, television, radio, billboard, exhibitions, email, and community outreach. |
| Communications | Phone calls, emails, SMS, social media, and in-person interactions. |
| After-sales | Handover, move-in, renewals, support, complaints, referrals, repeat business, satisfaction, referral discounts, multiple-purchase discounts, and raffle draws. |
| Reporting | Weekly, monthly, quarterly, annual, and daily refreshed reports covering conversion, follow-up, sales targets, campaign performance, qualified leads, social engagement, new markets, and sales against budget. |
| Integrations | Microsoft 365 and Google Workspace, including Gmail, Docs, Sheets, Drive, and Calendar. |
| Mandatory documents | Payment receipts, application form, UCL, handover rules, key release form, cadastral requirement form, Ghana card, non-citizen ID card, passport picture, and affidavits. |
| Compliance | Data protection, anti-money laundering, customer information protection, mandatory documents, workplace professionalism, confidentiality, and interdepartmental communication. |

## Business Configuration Inputs

| ID | Priority | Status | Configuration or decision required | Why it matters | Owner |
| --- | --- | --- | --- | --- | --- |
| SMC-CFG-001 | P0 | Configuration required | Confirm Sales/Marketing roles, access levels, approval authority, Finance/Revenue confirmation role, and Estates Development responsibilities. | Role-specific access, workflow routing, dashboards, and audit visibility depend on this matrix. | Marketing + Sales + Finance + ICT |
| SMC-CFG-002 | P0 | Configuration required | Confirm lead stages, conversion rules, and stage names: New Lead/Enquiry, Qualified/Site Viewing, Active/Initial Deposit, Customer/Full Payment. | Lead-to-customer conversion and reporting must use TDC's accepted status model. | Sales/Marketing |
| SMC-CFG-003 | P0 | Configuration required | Confirm lead scoring rules and whether Low/Medium/High should become numeric scores, weighted factors, or status-derived bands. | Assignment priority, dashboards, and follow-up queues depend on consistent scoring. | Sales/Marketing |
| SMC-CFG-004 | P0 | Configuration required | Confirm automatic assignment rules: round robin, territory/location, property type, officer workload, campaign source, or manager assignment. | The questionnaire requires auto-assignment, but the exact distribution policy is not defined. | Sales/Marketing |
| SMC-CFG-005 | P0 | Configuration required | Confirm duplicate detection and merge policy for client name, phone number, property type, email, Ghana card, and company/institution records. | Duplicate prevention must be enforced at API and UI, with audited merge history. | Sales/Marketing + ICT |
| SMC-CFG-006 | P0 | Configuration required | Confirm mandatory customer/KYC fields and document evidence: passport picture, Ghana card, non-citizen ID, next of kin, addresses, age, nationality, customer code, property number, and affidavits. | Data capture, AML checks, and document hard stops depend on mandatory-field policy. | Sales/Marketing + Legal/Compliance |
| SMC-CFG-007 | P0 | Configuration required | Confirm property matching rules: selling price, payment plan, property type, location, budget, amenities, and availability status. | Matching must integrate with saleable sources and prevent unavailable-unit offers. | Sales/Marketing + Estates Development |
| SMC-CFG-008 | P0 | Configuration required | Confirm payment milestones and Finance/Revenue confirmation rules for initial deposit, one-time payment, full payment, CAM fee, and ground rent. | Customer stage changes and UCL/handover/key release should be blocked until valid payment confirmation. | Finance/Revenue + Sales |
| SMC-CFG-009 | P0 | Configuration required | Confirm document templates and signature/evidence rules for application form, UCL, handover rules, key release, cadastral form, receipts, and amendments. | Document generation should be standardized and auditable. | Sales/Marketing + Legal + ICT |
| SMC-CFG-010 | P1 | Configuration required | Confirm discount, special-offer, referral, multiple-purchase, raffle, refund, ITF, name addition, and property-number change approval rules. | Deal exceptions and amendments require workflow hard stops and audit. | Sales/Marketing + Finance + Management |
| SMC-CFG-011 | P1 | Configuration required | Confirm campaign channels, budgets, ROI formula, social engagement metrics, enquiry attribution rules, and campaign-to-lead source mapping. | Campaign performance and ROI cannot be accepted without approved definitions. | Marketing |
| SMC-CFG-012 | P1 | Configuration required | Confirm communication templates and integrations for phone, email, SMS, WhatsApp/social media, in-person interactions, Gmail, Outlook, and calendar appointments. | Communication history and follow-up reminders depend on channel definitions. | Marketing + ICT |
| SMC-CFG-013 | P1 | Configuration required | Confirm complaint, service request, customer satisfaction, retention, referral, and repeat-business processes. | After-sales must connect to CRM, service/support, and reporting. | Sales/Marketing + Customer Service |
| SMC-CFG-014 | P1 | Configuration required | Confirm management report catalogue, KPI definitions, daily refresh expectations, recipients, and export templates. | Dashboards and scheduled reports must reconcile to agreed formulas. | Sales/Marketing + Management |
| SMC-CFG-015 | P1 | Configuration required | Confirm data-protection, AML, retention, consent, privacy masking, access logging, and customer information confidentiality rules. | Sensitive customer records and mandatory documents require strict controls. | Legal/Compliance + ICT |
| SMC-CFG-016 | P2 | Configuration required | Confirm migration owners and source columns for existing customer database, leads, communications, documents, payments, site visits, and property allocation history. | Migration cannot be accepted without signed source ownership and reconciliation. | Sales/Marketing + ICT |
| SMC-CFG-017 | P0 | Configuration required | Confirm the canonical customer/account master and identifier mapping across Lead conversion, Sales Customer, Business Partner, Finance AR Customer, joint ownership, and institutional accounts. | The current code uses customer and business-partner identifiers across different CRM/Sales paths; an approved ownership and synchronization rule is required to prevent duplicate or mislinked customer records. | Sales/Marketing + Finance + ICT |

## Current Coverage And Gap Matrix

### Governance, Scope, And Roles

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| GOV-001 | Partial | The ERP has RBAC, workflow, audit, CRM, Sales, and Finance modules. TDC-specific Sales/Marketing roles, Finance/Revenue confirmation, Estates Development handoff, and data-access boundaries are not configured and acceptance-tested as one role matrix. |
| GOV-002 | Partial | Sales Order, Sales Agreement, Sales Allocation, Credit Note, Refund, Customer, and Service Request workflow adapters exist. Lead, Opportunity, Campaign, Activity/site visit, document milestone, payment confirmation, UCL, handover, and key-release records do not yet have a complete shared-workflow integration and direct-API enforcement path. |
| GOV-003 | Gap | No dedicated Sales/Marketing/CRM policy register covering lead scoring, assignment, KYC, AML, data protection, document evidence, retention, communications, and exception approvals. |
| GOV-004 | Partial | Audit fields and workflow history exist, but customer lifecycle audit events are not guaranteed across every enquiry, merge, assignment, communication, document, payment confirmation, handover, amendment, refund, and retention action. |

### Lead Management

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| LEAD-001 | Verified baseline | Lead entities, DTOs, services, controllers, CRM routes, lead source, status, qualification score, assigned user, next follow-up date, and lead conversion exist. |
| LEAD-002 | Partial | TDC channels and sources are not seeded as governed catalogues: social media, phone, walk-in, referral, sales proposal, and exhibition. |
| LEAD-003 | Partial | Lead capture includes name, phone, email, company, source, status, score, assigned user, estimated value, and follow-up fields. Property type, channel, preferred transaction, campaign attribution, and source-specific evidence are not first-class mandatory fields in the current model/DTO/UI. |
| LEAD-004 | Partial | Assigned-user fields and manual selection exist, but lead creation accepts the caller-supplied assignee; there is no automatic assignment engine for workload, property type, source, location, territory, or manager routing. |
| LEAD-005 | Partial | Status and qualification score exist, but TDC's stage/scoring model is not enforced: Low after enquiry, Medium after site visit, High after initial deposit, Customer after full payment. |
| LEAD-006 | Gap | Duplicate detection by identical client name, phone number, and property type with manual merge workflow and audited merge history is incomplete. |

### Customer, Contact, KYC, And Preferences

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| CUST-001 | Verified baseline | CRM account pages, generic Sales Customer records, Business Partners, Finance AR customer/payment touchpoints, contact fields, account detail, health/readiness/risk views, and customer milestone surfaces exist. Canonical ownership and synchronization across these customer representations remains a required implementation control under `SMC-0114`. |
| CUST-002 | Partial | Contact categories for individuals, companies, and institutions exist only indirectly through business/customer structures; TDC-specific segmentation and mandatory fields are not enforced. |
| CUST-003 | Gap | Full TDC KYC profile is incomplete as a mandatory evidence pack: passport picture, postal/residential/business/email addresses, age, nationality, customer code, property number, Ghana card, next of kin, non-citizen ID, and affidavits. |
| CUST-004 | Partial | CRM activities and account detail provide a useful communication-history foundation across leads, accounts, and opportunities. Channel taxonomy, mandatory capture, consent, external message synchronization, attachments, delivery outcomes, and one reconciled phone/email/SMS/social/WhatsApp/in-person ledger remain incomplete. |
| CUST-005 | Partial | Customer preference capture is incomplete for property type, budget, preferred location, amenities, payment plan, and matching criteria. |
| CUST-006 | Partial | Customer health and CRM segments exist in parts, but TDC segmentation for individual, corporate, investor, joint acquisition, mortgage facility, and joint ownership is not configured and reported. |

### Sales, Rental, Lease, Opportunity, And Property Matching

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| SALE-001 | Verified baseline | Sales Orders, Sales Agreements, Sales Allocations, quote-to-order conversion, project-unit sales handoff, saleable-source setup, and workflow actions exist. |
| SALE-002 | Partial | Separate sale/rental/lease pipelines exist through Sales Agreements and transaction types, but TDC-specific pipeline stages and stage gates are not configured around enquiry, viewing, application, deposit, full payment, UCL, handover, and key release. |
| SALE-003 | Partial | Saleable-source search and project-unit integration exist, but matching is not yet TDC-specific for selling price, payment plan, property type, customer budget, preferred location, amenities, and availability. |
| SALE-004 | Partial | Discounts and approval workflows exist in Sales records, but discount/special-offer/deal-exception policy is not centrally configured and hard-stopped for TDC. |
| SALE-005 | Partial | Site visits, inspections, reservations, negotiations, and closures are represented across CRM activities, opportunities, quotes, allocations, and agreements, but not delivered as one property-sales journey with required evidence and reporting. |
| SALE-006 | Gap | Application form, UCL, handover rules, key release, cadastral requirement form, Ghana card evidence, CAM fee, ground rent, ITF, name addition, property-number change, and refund workflows are not implemented as one controlled document/payment lifecycle. |
| SALE-007 | Partial | Refund and credit-note workflows exist, but TDC refund/amendment conditions, Finance confirmation, allocation release, and customer communication pack need acceptance-ready controls. |

### Marketing And Campaign Management

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| MKT-001 | Verified baseline | Campaign entities, DTOs, services, controllers, campaign routes, campaign members, campaign budget, and campaign status exist. |
| MKT-002 | Partial | Campaign channels exist generically, but TDC channels are not configured and reported separately: Facebook, TikTok, Instagram, TV, radio, billboard, exhibition, email, and community outreach. |
| MKT-003 | Partial | Campaign budget, actual cost, expected/actual revenue, audience/response metrics, leads/opportunities, weighted pipeline, and calculated ROI are displayed. Cost/revenue values and lead counts are still manually maintained or only partially derived; campaign-to-enquiry/deposit/conversion/revenue attribution and Finance reconciliation are incomplete. |
| MKT-004 | Partial | Email campaign structures and SMS/email settings exist, but automated lead nurturing/drip campaigns across email/SMS/social/WhatsApp are incomplete. |
| MKT-005 | Gap | Social media reactions, shares, likes, comments, WhatsApp enquiries, phone enquiries, walk-in enquiries, exhibition enquiries, and source attribution are not captured as one campaign performance model. |

### Communication, Engagement, And Notifications

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| COM-001 | Partial | CRM activities, account detail, SMS/email configuration, and notification infrastructure exist, but channel-specific communication logging is incomplete for phone, email, SMS, Facebook, TikTok, Instagram, WhatsApp/social, and in-person interactions. |
| COM-002 | Partial | Email/SMS templates exist in the platform, but Sales/Marketing templates for email, SMS, quotations, promotional messages, site visits, renewals, and payment milestones are not seeded and acceptance-tested. |
| COM-003 | Partial | Follow-up dates and notifications exist, but reminders for follow-ups, appointments, site visits, renewals, and payment milestones are not configured end to end with escalation. |
| COM-004 | Partial | Communication history is visible on CRM account surfaces, but not every lead/customer communication channel is mandatory or synchronized from integrated platforms. |

### After-Sales, Relationship, Service, And Retention

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| REL-001 | Partial | Sales agreements, renewals, project handover context, service request workflow adapters, CRM account health, and collection activities exist. TDC post-sale handover, move-in, renewals, and support are not one controlled after-sales workflow. |
| REL-002 | Partial | The CRM Service workspace aggregates Business Partner-linked EHC tickets, problems, overdue work, SLA pressure, feedback, and attention status. TDC customer complaint categories, Sales customer identity mapping, intake channels, ownership, evidence, escalation, consent, closure communication, and end-to-end reporting are not yet configured and acceptance-tested as one customer-service lifecycle. |
| REL-003 | Gap | Referrals, repeat business, customer satisfaction, referral discounts, multiple-purchase discounts, and raffle/give-away retention processes are not implemented as a governed CRM capability. |

### Reporting, Dashboards, Forecasting, And Analytics

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| RPT-001 | Partial | Sales and CRM reports/dashboard surfaces exist for allocation, sold/leased, reservations, agreement expiry, refunds, conversions, forecasts, pipeline, campaign ROI, account health/risk, service pressure, and competitor signals. TDC's named daily/weekly/monthly/quarterly/annual report pack is not configured and reconciled to approved formulas. |
| RPT-002 | Partial | Some KPIs exist, but TDC KPI definitions are incomplete: conversion rate, follow-up rate, sales target achievement, campaign performance, staff development/reporting compliance, qualified leads, social engagement, new market opportunities, and sales against budget. |
| RPT-003 | Partial | Dashboards exist for Sales and CRM, but role-based dashboards for agents, managers, marketing teams, and executives are not configured and acceptance-tested. |
| RPT-004 | Partial | Sales/CRM forecasting, conversion, campaign analysis, account health, and trend surfaces exist. They do not yet fully reconcile campaign attribution, customer identity, communications, deposits, Finance revenue, property demand, and approved KPI definitions. |

### Integrations, Data, Documents, And Migration

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| INT-001 | Partial | Finance AR, Projects/Development, Sales, CRM, EHC, SMS/email, Workflow, and document attachments have integration touchpoints. Customer identifiers are interpreted across Sales Customer and Business Partner paths, and a full Sales/Marketing/CRM interface control document with source-of-truth, idempotency, retry, reconciliation, and failure ownership is missing. |
| INT-002 | Gap | Sales/CRM Microsoft 365 and Google Workspace integrations for Word, Excel, Gmail, Google Docs, Sheets, Drive, Outlook, and Calendar are not implemented end to end. Existing Microsoft Graph/email support in EHC is a reusable platform foundation, not evidence of a completed Sales/CRM integration contract. |
| INT-003 | Partial | Import/export exists in parts of the ERP, but TDC existing-customer-database import, lead import, document import, and reconciliation workbench are incomplete. |
| DOC-001 | Partial | Sales Agreement documents and shared attachment/evidence patterns exist, but TDC mandatory generation/upload/verification, document-type rules, versioning, access masking, retention, and milestone hard stops are incomplete for receipts, application forms, UCL, handover, key release, cadastral forms, Ghana card, non-citizen ID, passport picture, and affidavits. |

### Security, Access, Compliance, And Audit

| Requirement ID | Current state | Requirement and remaining gap |
| --- | --- | --- |
| SEC-001 | Partial | RBAC and authorization exist, but TDC role-specific full/limited access for Sales and Marketing Manager, Officers, and Assistants is not configured and tested. |
| SEC-002 | Partial | Approval controls, workflow history, and audit fields exist, but a complete Sales/Marketing/CRM audit trail report and direct-API bypass tests are incomplete. |
| SEC-003 | Gap | Data protection and AML controls are not implemented as a Sales/CRM policy pack covering consent, identity/KYC, privacy masking, retention, legal hold, suspicious activity, and restricted access to sensitive customer documents. |

## Source Question Traceability

This table maps every answered questionnaire row to the detailed gap matrix and implementation ledger. Source references use the questionnaire table and row position so that TDC can certify scope without relying on a summarized interpretation.

| Source ref | Client requirement | Coverage IDs | Implementation and acceptance coverage |
| --- | --- | --- | --- |
| T02R02 | Centralize leads, measure campaigns, streamline sales/rentals/leases, and improve post-sale retention. | `GOV-001`, `RPT-001`, `REL-001` | `SMC-0001` through `SMC-0004`, `SMC-0501`, `SMC-E2E-009` |
| T02R03 | Support property sales, rentals, and leases. | `SALE-002` | `SMC-0201`, `SMC-E2E-003` |
| T02R04 | Support Sales/Marketing management, officers/assistants, Finance/Revenue, and Estates Development users. | `GOV-001`, `SEC-001` | `SMC-0004`, `SMC-E2E-011` |
| T02R05 | Resolve fragmented records, manual follow-up/reporting, limited visibility, and interdepartmental gaps. | `GOV-003`, `INT-001`, `INT-003`, `RPT-001` | `SMC-0002`, `SMC-0501`, `SMC-0604` |
| T02R06 | Measure success through conversion, follow-up, target achievement, campaign performance, reporting compliance, qualified leads, social engagement, market opportunities, and sales against budget. | `RPT-002`, `RPT-003` | `SMC-0502`, `SMC-0503`, `SMC-E2E-009` |
| T03R02 | Capture social-media, phone, and walk-in lead channels. | `LEAD-002` | `SMC-0101`, `SMC-E2E-001` |
| T03R03 | Capture referral, sales-proposal, and exhibition lead sources. | `LEAD-002` | `SMC-0101`, `SMC-E2E-001` |
| T03R04 | Capture required lead details, including property interest and contact information. | `LEAD-003` | `SMC-0102`, `SMC-E2E-001` |
| T03R05 | Assign new leads automatically. | `LEAD-004` | `SMC-0104`, `SMC-E2E-001` |
| T03R06 | Use New/Enquiry, Qualified after viewing, Active after deposit, and Customer after full payment stages. | `LEAD-005` | `SMC-0103`, `SMC-E2E-001` |
| T03R07 | Score and prioritize leads. | `LEAD-005` | `SMC-0105`, `SMC-E2E-001` |
| T03R08 | Apply Low after enquiry, Medium after viewing, and High after initial deposit scoring. | `LEAD-005` | `SMC-0105`, `SMC-E2E-001` |
| T03R09 | Flag duplicates by client name, phone, and property type and allow manual merge. | `LEAD-006` | `SMC-0106`, `SMC-E2E-002` |
| T04R02 | Support individual, company, and institution contact categories. | `CUST-002` | `SMC-0110`, `SMC-0111`, `SMC-E2E-002` |
| T04R03 | Capture the stated customer profile, addresses, nationality, identifiers, next of kin, property number, and identity evidence. | `CUST-003`, `DOC-001`, `SEC-003` | `SMC-0110`, `SMC-0205`, `SMC-0701`, `SMC-0702`, `SMC-E2E-004`, `SMC-E2E-011` |
| T04R04 | Maintain complete customer communication history. | `CUST-004`, `COM-004` | `SMC-0306`, `SMC-E2E-007` |
| T04R05 | Capture customer property, budget, location, amenity, and payment-plan preferences. | `CUST-005` | `SMC-0112`, `SMC-E2E-003` |
| T04R06 | Support customer segmentation; source priority is blank and requires TDC confirmation. | `CUST-006` | `SMC-CFG-002`, `SMC-0111`, `SMC-E2E-002` |
| T04R07 | Segment individual, corporate, investor, joint-acquisition, mortgage, and joint-ownership customers. | `CUST-006` | `SMC-0111`, `SMC-E2E-002` |
| T05R02 | Control the full property lifecycle from enquiry and viewing through application, payments, customer registration, UCL, handover, keys, cadastral documents, fees, amendments, refunds, and ITF. | `SALE-002`, `SALE-005`, `SALE-006`, `SALE-007` | `SMC-0201`, `SMC-0204`, `SMC-0205`, `SMC-0207`, `SMC-0208`, `SMC-E2E-003`, `SMC-E2E-004`, `SMC-E2E-012` |
| T05R03 | Maintain separate sales, rental, and lease pipelines. | `SALE-002` | `SMC-0201`, `SMC-E2E-003` |
| T05R04 | Match customer requirements to available properties. | `SALE-003` | `SMC-0202`, `SMC-0203`, `SMC-E2E-003` |
| T05R05 | Match by selling price and payment plan, with approved additional criteria. | `SALE-003` | `SMC-0202`, `SMC-E2E-003` |
| T05R06 | Generate standardized sales, rental, lease, payment, UCL, handover, key-release, and cadastral documents. | `SALE-006`, `DOC-001` | `SMC-0205`, `SMC-0603`, `SMC-E2E-004` |
| T05R07 | Route discounts, special offers, and deal exceptions for approval. | `SALE-004` | `SMC-0206`, `SMC-E2E-005` |
| T05R08 | Track site visits, inspections, reservations, negotiations, and closures. | `SALE-005` | `SMC-0204`, `SMC-E2E-003` |
| T06R02 | Support the stated campaign types and marketing initiatives. | `MKT-001`, `MKT-002` | `SMC-0301`, `SMC-E2E-006` |
| T06R03 | Support Facebook, TikTok, Instagram, TV, radio, billboard, exhibition, email, and community-outreach channels. | `MKT-002` | `SMC-0301`, `SMC-0302`, `SMC-E2E-006` |
| T06R04 | Track campaign budgets and ROI. | `MKT-003` | `SMC-0303`, `SMC-E2E-006` |
| T06R05 | Support automated nurturing and drip campaigns. | `MKT-004` | `SMC-0304`, `SMC-E2E-007` |
| T06R06 | Measure social reactions and WhatsApp, phone, walk-in, and exhibition enquiries by campaign. | `MKT-005` | `SMC-0305`, `SMC-E2E-006` |
| T07R02 | Record phone, email, SMS, social-media, and in-person communications. | `COM-001` | `SMC-0306`, `SMC-E2E-007` |
| T07R03 | Use reusable communication templates. | `COM-002` | `SMC-0304`, `SMC-0307`, `SMC-0603`, `SMC-E2E-007` |
| T07R04 | Notify and remind users about follow-ups, appointments, visits, renewals, and payment milestones. | `COM-003` | `SMC-0307`, `SMC-E2E-007` |
| T07R05 | Preserve complete communication history. | `COM-004` | `SMC-0306`, `SMC-E2E-007` |
| T08R02 | Manage handover, move-in, renewal, and post-sale/post-lease support. | `REL-001` | `SMC-0401`, `SMC-E2E-008` |
| T08R03 | Track complaints, issues, and service requests. | `REL-002` | `SMC-0402`, `SMC-E2E-008` |
| T08R04 | Track referrals, repeat business, and customer satisfaction. | `REL-003` | `SMC-0403`, `SMC-0405`, `SMC-E2E-008` |
| T08R05 | Govern referral discounts, multiple-purchase discounts, raffles, and give-aways. | `REL-003` | `SMC-0404`, `SMC-E2E-008` |
| T09R02 | Produce the stated daily, weekly, monthly, quarterly, and annual reports. | `RPT-001` | `SMC-0501`, `SMC-E2E-009` |
| T09R03 | Calculate the stated Sales and Marketing KPIs. | `RPT-002` | `SMC-0502`, `SMC-E2E-009` |
| T09R04 | Provide role-based dashboards. | `RPT-003` | `SMC-0503`, `SMC-E2E-009` |
| T09R05 | Refresh dashboard/report data daily. | `RPT-001`, `RPT-003` | `SMC-0501`, `SMC-0503`, `SMC-E2E-009` |
| T09R06 | Support forecasting, campaign analysis, and customer trends. | `RPT-004` | `SMC-0504`, `SMC-E2E-009` |
| T10R02 | Integrate Microsoft 365 and Google Workspace, including Word/Excel, Gmail, Docs, Sheets, Drive, and Calendar. | `INT-002` | `SMC-0601`, `SMC-0602`, `SMC-E2E-010` |
| T10R03 | Import existing customer and related Sales/CRM data. | `INT-003` | `SMC-0113`, `SMC-0604`, `SMC-E2E-002` |
| T10R04 | Export operational and management data. | `INT-003`, `RPT-001` | `SMC-0501`, `SMC-0604`, `SMC-E2E-009` |
| T10R05 | Control mandatory receipts, application, UCL, handover, key-release, cadastral, Ghana-card, non-citizen-ID, passport-picture, and affidavit evidence. | `DOC-001`, `CUST-003`, `SEC-003` | `SMC-0205`, `SMC-0603`, `SMC-0701`, `SMC-0702`, `SMC-E2E-004`, `SMC-E2E-011` |
| T11R02 | Configure Sales/Marketing Manager, Officer, Assistant, Finance/Revenue, Estates Development, and related roles. | `SEC-001` | `SMC-0004`, `SMC-E2E-011` |
| T11R03 | Enforce full and limited access by role and data sensitivity. | `SEC-001` | `SMC-0004`, `SMC-0701`, `SMC-0704`, `SMC-E2E-011` |
| T11R04 | Provide approvals, audit trails, and activity logs. | `SEC-002` | `SMC-0003`, `SMC-0703`, `SMC-0704`, `SMC-E2E-005`, `SMC-E2E-011` |
| T11R05 | Enforce data-protection and AML requirements. | `SEC-003` | `SMC-0701`, `SMC-0702`, `SMC-0704`, `SMC-E2E-011` |
| T12R02 | Deliver the initial mandatory feature set identified by TDC. | `LEAD-001` through `LEAD-006`, `RPT-001`, `DOC-001`, `INT-003` | `SMC-0101` through `SMC-0114`, `SMC-0501` through `SMC-0503`, `SMC-0603` through `SMC-0605`, `SMC-0705` |
| T12R03 | Retain CRM as a Must Have capability even where rollout is deferred. | `GOV-001`, `REL-001` through `REL-003` | `SMC-0401` through `SMC-0405`, `SMC-0705` |
| T12R04 | Protect customer information, enforce mandatory documents, and support workplace professionalism. | `SEC-003`, `DOC-001` | `SMC-0205`, `SMC-0701`, `SMC-0702`, `SMC-E2E-004`, `SMC-E2E-011` |
| T12R05 | Address usability, resource, confidentiality, accessibility, and interdepartmental communication gaps. | `GOV-001`, `INT-001`, `SEC-001`, `SEC-003` | `SMC-0004`, `SMC-0601`, `SMC-0701`, `SMC-0704`, `SMC-E2E-010`, `SMC-E2E-011` |

## Key Questionnaire Requirement Traceability

| No. | Questionnaire requirement | Current coverage | Implementation task IDs |
| ---: | --- | --- | --- |
| 1 | Centralize lead acquisition | Partial. Leads exist, but TDC lead channels, source catalogues, assignment, duplicate merge, and import are incomplete. | `SMC-0101` through `SMC-0106`, `SMC-E2E-001` |
| 2 | Track marketing campaign outcomes | Partial. Campaign cost, revenue, response, pipeline, and calculated ROI exist, but social metrics, enquiry/deposit attribution, conversion lineage, and Finance-reconciled revenue remain incomplete. | `SMC-0301` through `SMC-0305`, `SMC-E2E-006` |
| 3 | Streamline sales, rentals, and leases | Partial. Sales Orders, Agreements, Allocations, and project-unit handoff exist, but TDC property journey gates and mandatory documents are incomplete. | `SMC-0201` through `SMC-0208`, `SMC-E2E-003` |
| 4 | Improve post-sales retention | Partial. Renewals/account health exist in parts; referrals, loyalty, satisfaction, complaints, and retention programs are incomplete. | `SMC-0401` through `SMC-0405`, `SMC-E2E-008` |
| 5 | Automatic lead assignment | Partial. Assigned user fields exist; assignment engine/rules are incomplete. | `SMC-0104`, `SMC-E2E-001` |
| 6 | Lead scoring/prioritization | Partial. Qualification score exists; TDC Low/Medium/High criteria are not enforced. | `SMC-0105`, `SMC-E2E-001` |
| 7 | Duplicate lead detection and manual merge | Gap. Requires duplicate detection by name, phone, property type and audited merge. | `SMC-0106`, `SMC-E2E-002` |
| 8 | Customer/contact KYC and segmentation | Partial. Customer/account records exist; TDC mandatory fields, documents, next of kin, and segmentation need completion. | `SMC-0110` through `SMC-0114`, `SMC-E2E-002` |
| 9 | Communication history | Partial. Activities exist; channel-wide communication ledger and integrations are incomplete. | `SMC-0306`, `SMC-0307`, `SMC-E2E-007` |
| 10 | Customer preferences and property matching | Partial. Saleable-source lookup exists; preference capture and matching by selling price/payment plan are incomplete. | `SMC-0202`, `SMC-0203`, `SMC-E2E-003` |
| 11 | Site visits, reservations, negotiations, closures | Partial. Pieces exist across CRM/Sales; the single enforced property-sales pipeline is incomplete. | `SMC-0204`, `SMC-E2E-003` |
| 12 | Document generation | Partial. Document surfaces exist; TDC application/UCL/handover/key/cadastral document pack is incomplete. | `SMC-0205`, `SMC-E2E-004` |
| 13 | Discount/special offer approvals | Partial. Workflow exists; TDC exception approval rules need configuration. | `SMC-0206`, `SMC-E2E-005` |
| 14 | Campaign budgeting and ROI | Partial. Budget exists; ROI formula and attribution are incomplete. | `SMC-0303`, `SMC-E2E-006` |
| 15 | Automated lead nurturing/drip campaigns | Partial. Messaging foundations exist; drip workflow is incomplete. | `SMC-0304`, `SMC-E2E-007` |
| 16 | Follow-up/site visit/renewal/payment reminders | Partial. Follow-up fields and notifications exist; TDC reminder calendar and escalation are incomplete. | `SMC-0307`, `SMC-E2E-007` |
| 17 | Complaints/service requests | Partial. CRM already aggregates Business Partner-linked EHC tickets, problems, overdue work, feedback, and SLA pressure; TDC customer identity, categories, intake, evidence, ownership, escalation, and closure communication remain incomplete. | `SMC-0402`, `SMC-E2E-008` |
| 18 | Reporting/dashboards/forecasting | Partial. Reports exist; TDC report catalogue, KPI formulas, role dashboards, forecasting, and trend reporting need completion. | `SMC-0501` through `SMC-0504`, `SMC-E2E-009` |
| 19 | Microsoft 365 and Google Workspace integration | Gap. Not implemented end to end. | `SMC-0601`, `SMC-E2E-010` |
| 20 | Data protection and AML | Gap. Needs CRM policy pack, KYC evidence controls, privacy masking, retention, audit, and suspicious activity handling. | `SMC-0701` through `SMC-0704`, `SMC-E2E-011` |

## Implementation Roadmap

### Phase 0 - Configuration, Policy, And Architecture

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0001 | P0 | Not started | Resolve `SMC-CFG-001` through `SMC-CFG-017` as effective-dated configuration. | Every configuration has owner, approved value, effective date, evidence, and tenant seed plan. |
| SMC-0002 | P0 | Not started | Build Sales/Marketing/CRM policy register. | Lead scoring, assignment, duplicate merge, KYC, AML, data protection, documents, approvals, reminders, and report definitions are versioned and queryable. |
| SMC-0003 | P0 | Not started | Define customer lifecycle audit event map. | Every lifecycle action has event type, actor, role, before/after values, source, evidence links, and report visibility. |
| SMC-0004 | P0 | Not started | Configure TDC roles, permissions, and dashboards. | Test users can only see and act on records permitted by role, access level, department, and sensitive-document policy. |

### Phase 1 - Lead, Customer, KYC, Preferences, And Migration

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0101 | P0 | Not started | Seed lead channels and sources. | Social, phone, walk-in, referral, proposal, and exhibition sources persist, filter, export, and report. |
| SMC-0102 | P0 | Not started | Add TDC lead capture profile. | Name, phone, email, property type, source, channel, preferred transaction, and campaign context are mandatory as configured. |
| SMC-0103 | P0 | Not started | Configure TDC lead stages. | Enquiry, qualified/site viewing, active/deposit, and customer/full-payment states drive dashboards and conversion rules. |
| SMC-0104 | P0 | Not started | Implement automatic lead assignment rules. | New leads are assigned by approved rules with workload visibility, override reason, and audit. |
| SMC-0105 | P0 | Not started | Implement TDC lead scoring. | Low/Medium/High scores derive from enquiry, site viewing, deposit, and approved extra factors. |
| SMC-0106 | P0 | Not started | Implement duplicate lead detection and merge. | Name + phone + property type duplicates are flagged; manual merge preserves history, evidence, and audit. |
| SMC-0110 | P0 | Not started | Add TDC customer/KYC profile and mandatory evidence. | Addresses, age, nationality, customer code, property number, Ghana card, non-citizen ID, passport picture, affidavits, and next of kin persist and validate. |
| SMC-0111 | P1 | Not started | Configure customer segmentation. | Individual, corporate, investor, joint acquisition, mortgage facility, and joint ownership segments drive filters and reports. |
| SMC-0112 | P1 | Not started | Add customer preference model. | Property type, budget, location, amenities, payment plan, and other preferences are searchable and used in matching. |
| SMC-0113 | P1 | Not started | Build existing customer database import staging. | Spreadsheet import validates duplicates, missing KYC, contact quality, document gaps, and signed reconciliation before posting. |
| SMC-0114 | P0 | Not started | Implement canonical customer identity and cross-module mapping. | Lead conversion creates or links one governed customer identity; Sales Customer, Business Partner, CRM Account, Finance AR Customer, joint owners, documents, communications, payments, and migration records reconcile without duplicate masters. |

### Phase 2 - Property Sales, Rentals, Leases, Documents, And Payments

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0201 | P0 | Not started | Configure separate sales, rental, lease, and property transaction pipelines. | Pipeline stages and statuses route correctly for sales, rentals, and leases without duplicate state machines. |
| SMC-0202 | P0 | Not started | Complete property matching against saleable sources. | Customer requirements match available units/properties by selling price, payment plan, property type, location, budget, amenities, and availability. |
| SMC-0203 | P0 | Not started | Enforce property availability and reservation controls. | Unavailable, reserved, sold, leased, or blocked units cannot be offered or allocated through direct API calls. |
| SMC-0204 | P1 | Not started | Add site visit, inspection, negotiation, and closure workflow. | Visit scheduling, attendance, outcome, follow-up, negotiation notes, and closure reasons are auditable. |
| SMC-0205 | P0 | Not started | Build TDC document generation/evidence pack. | Application form, receipts, UCL, handover rules, key release, cadastral requirement form, Ghana card, non-citizen ID, passport picture, and affidavits are generated/uploaded/verified as configured. |
| SMC-0206 | P0 | Not started | Configure discount, special-offer, and deal-exception approvals. | Unauthorized discounts/exceptions are blocked; approved exceptions carry reason, evidence, and audit. |
| SMC-0207 | P0 | Not started | Integrate payment milestones with Finance/Revenue confirmation. | Deposit, one-time/full payment, CAM, and ground-rent confirmations drive lead/customer stage, UCL, handover, and key-release gates. |
| SMC-0208 | P1 | Not started | Implement amendments, refunds, and ITF workflows. | Addition of names, property-number change, refund, and in-trust-for actions require configured approvals, documents, and audit. |

### Phase 3 - Marketing Campaigns, Communications, Nurture, And Reminders

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0301 | P1 | Not started | Seed marketing channel catalogue. | Facebook, TikTok, Instagram, TV, radio, billboard, exhibition, email, and community outreach are selectable and reportable. |
| SMC-0302 | P1 | Not started | Add campaign-to-lead attribution. | Leads retain campaign, channel, source, enquiry type, and conversion lineage. |
| SMC-0303 | P1 | Not started | Implement campaign ROI and performance model. | Budget, cost, leads, enquiries, deposits, conversions, revenue, and ROI reconcile. |
| SMC-0304 | P1 | Not started | Add automated lead nurture/drip campaigns. | Email/SMS/social sequence rules send approved templates, observe opt-out/consent, and log outcomes. |
| SMC-0305 | P2 | Not started | Capture social and enquiry metrics. | Shares, likes, comments, WhatsApp enquiries, calls, walk-ins, and exhibitions update campaign KPIs. |
| SMC-0306 | P1 | Not started | Build unified communication ledger. | Phone, email, SMS, social, WhatsApp, and in-person interactions attach to lead/customer/opportunity/campaign. |
| SMC-0307 | P1 | Not started | Configure reminders and notifications. | Follow-ups, appointments, site visits, renewals, and payment milestones notify correct users and escalate overdue items. |

### Phase 4 - After-Sales, Service, Retention, And Relationship Management

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0401 | P1 | Not started | Build post-sale/post-lease workflow. | Handover, move-in, renewal, and support tasks are linked to customer, property/unit, agreement, and payment status. |
| SMC-0402 | P1 | Not started | Add CRM-linked complaint/service request workflow. | Complaints and issues track category, SLA, owner, evidence, status, closure, and customer communication. |
| SMC-0403 | P1 | Not started | Implement referral and repeat-business tracking. | Referrer, referred customer, conversion, reward eligibility, and repeat-purchase lineage are visible and reportable. |
| SMC-0404 | P1 | Not started | Add loyalty/retention programs. | Referral discounts, multiple-purchase discounts, raffle/give-away campaigns, approvals, and usage history are controlled. |
| SMC-0405 | P1 | Not started | Add customer satisfaction tracking. | Survey, score, sentiment, complaint linkage, follow-up, and trend reports are available. |

### Phase 5 - Reports, Dashboards, Forecasting, And Analytics

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0501 | P1 | Not started | Deliver TDC Sales/Marketing report catalogue. | Weekly, monthly, quarterly, annual, and daily refreshed reports reconcile to leads, sales, campaigns, payments, and CRM activities. |
| SMC-0502 | P1 | Not started | Implement KPI definitions. | Conversion rate, follow-up rate, target achievement, campaign performance, staff reporting compliance, qualified leads, social engagement, new market opportunities, and sales against budget are reproducible. |
| SMC-0503 | P1 | Not started | Deliver role-based dashboards. | Agents, managers, marketing teams, executives, Finance/Revenue, and Estates Development see appropriate dashboards and filters. |
| SMC-0504 | P1 | Not started | Complete forecasting, campaign analysis, and customer trend reporting. | Forecasts, campaign ROI, customer trends, source attribution, and property demand patterns reconcile to source transactions. |

### Phase 6 - Integrations, Documents, Migration, And Productivity

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0601 | P1 | Not started | Produce Microsoft 365 and Google Workspace integration contract. | Word, Excel, Gmail, Docs, Sheets, Drive, and Calendar integration direction, ownership, authentication, storage, retries, and audit are documented. |
| SMC-0602 | P1 | Not started | Implement calendar/email integration for site visits and follow-ups. | Appointments sync, reminders fire, emails are logged, and failures reconcile. |
| SMC-0603 | P1 | Not started | Implement document template generation and storage. | Word/PDF templates generate controlled application, UCL, handover, key release, cadastral, promotional, quotation, and communication documents. |
| SMC-0604 | P1 | Not started | Build migration and data-quality workbench. | Customer, lead, document, communication, payment, property allocation, and campaign history imports validate before posting. |
| SMC-0605 | P2 | Not started | Prepare staff training and adoption materials. | Manager, officer, assistant, Finance/Revenue, and Estates Development training materials are approved. |

### Phase 7 - Security, Compliance, AML, Audit, And UAT

| Task ID | Priority | Status | Deliverable | Acceptance criteria |
| --- | --- | --- | --- | --- |
| SMC-0701 | P0 | Not started | Configure data-protection and customer privacy policy. | Sensitive fields/documents are access-controlled, masked where required, consent-aware, retained correctly, and audited. |
| SMC-0702 | P0 | Not started | Add AML/KYC compliance controls. | Mandatory identity documents, risk flags, suspicious activity markers, approvals, and audit reports are enforced. |
| SMC-0703 | P0 | Not started | Deliver complete Sales/CRM audit report. | Lead, customer, document, communication, payment, approval, merge, amendment, refund, and handover events are exportable. |
| SMC-0704 | P0 | Not started | Run direct-API bypass and tenant-isolation tests. | Unauthorized access, missing documents, unapproved discounts, duplicate merges, and invalid stage changes are rejected and audited. |
| SMC-0705 | P1 | Not started | Run end-to-end UAT and go-live acceptance. | Representative lead, sale, lease, rental, campaign, complaint, referral, document, payment, dashboard, integration, and compliance scenarios pass. |

## Mandatory End-To-End Acceptance Scenarios

| Scenario ID | Status | Scenario |
| --- | --- | --- |
| SMC-E2E-001 | Not started | Social/phone/walk-in enquiry -> lead created -> auto assignment -> Low score -> site viewing -> Medium score -> initial deposit -> High score. |
| SMC-E2E-002 | Not started | Duplicate lead by name + phone + property type -> duplicate flagged -> user manually merges -> audit shows surviving record and merged history. |
| SMC-E2E-003 | Not started | Lead preferences -> property match by selling price/payment plan -> site visit -> reservation -> negotiation -> closure. |
| SMC-E2E-004 | Not started | Customer registration -> mandatory KYC/documents -> payment confirmation -> UCL generation -> handover rules -> key release -> cadastral form. |
| SMC-E2E-005 | Not started | Discount/special offer request -> approval workflow -> approved exception applied; direct API discount without approval is rejected. |
| SMC-E2E-006 | Not started | Campaign budget -> social/exhibition enquiry attribution -> deposit/full payment conversion -> ROI and campaign report. |
| SMC-E2E-007 | Not started | Lead nurture sequence -> email/SMS/social follow-up -> appointment reminder -> communication ledger and overdue escalation. |
| SMC-E2E-008 | Not started | Post-sale handover -> complaint/service request -> SLA ownership -> closure -> customer satisfaction -> retention/referral tracking. |
| SMC-E2E-009 | Not started | Daily dashboard reproduces conversion rate, follow-up rate, sales target, qualified leads, social engagement, sales against budget, and staff reporting compliance. |
| SMC-E2E-010 | Not started | Gmail/Outlook/Google Calendar or Microsoft 365 event sync logs outbound communication, appointment, failure, retry, and reconciliation. |
| SMC-E2E-011 | Not started | Sensitive customer record with Ghana card/passport picture -> limited role denied -> authorized role views masked/full record per policy -> audit report proves access trail. |
| SMC-E2E-012 | Not started | Refund/amendment/ITF/name addition/property-number change -> required documents -> approval -> Finance confirmation -> customer communication -> audit pack. |
| SMC-E2E-013 | Not started | Existing person or organization enters as a lead -> duplicate check -> customer conversion -> one canonical customer identity links CRM account, Sales transaction, Finance payment, documents, ownership parties, communications, and audit history. |

## Current Code Evidence Anchors

These anchors justify the baseline classifications; they are not proof of final TDC acceptance:

- Existing Sales expansion tracker: `docs/sales-management-tasklist.md`
- Sales and CRM enums: `src/ErpSystem.Core/Enums/SalesEnums.cs`
- Core CRM entities: `src/ErpSystem.Core/Entities/Sales/SalesEntities.cs`
- Customer entity: `src/ErpSystem.Core/Entities/Sales/Customer.cs`
- Sales Order entities: `src/ErpSystem.Core/Entities/Sales/SalesOrderEntities.cs`
- Sales Agreement entities: `src/ErpSystem.Core/Entities/Sales/SalesAgreementEntities.cs`
- Sales Allocation entities: `src/ErpSystem.Core/Entities/Sales/SalesAllocationEntities.cs`
- Campaign/email entities: `src/ErpSystem.Core/Entities/EmailCampaign.cs`, `src/ErpSystem.Core/Entities/EmailCampaignRecipient.cs`
- CRM aggregation DTOs: `src/ErpSystem.Core/DTOs/Crm/CrmDtos.cs`
- Sales lead/opportunity/activity/campaign DTOs: `src/ErpSystem.Core/DTOs/Sales/CrmDTOs.cs`
- Sales Order DTOs: `src/ErpSystem.Core/DTOs/Sales/SalesOrderDTOs.cs`
- Sales Agreement DTOs: `src/ErpSystem.Core/DTOs/Sales/SalesAgreementDTOs.cs`
- Sales Allocation DTOs: `src/ErpSystem.Core/DTOs/Sales/SalesAllocationDTOs.cs`
- Sales services: `src/ErpSystem.Core/Services/Sales/LeadService.cs`, `OpportunityService.cs`, `QuoteService.cs`, `CampaignService.cs`, `SalesOrderService.cs`, `SalesAgreementService.cs`, `SalesAllocationService.cs`, `SalesReportingService.cs`
- CRM aggregation service and account/health/readiness/risk/collaboration/service/campaign/reporting workspaces: `src/ErpSystem.Core/Services/Crm/CrmService.cs`
- EHC service linkage used by the CRM service workspace: `src/ErpSystem.Core/Entities/EHC/**`, `src/ErpSystem.Core/Services/EHC/**`
- Project sales handoff: `src/ErpSystem.Core/Services/Projects/ProjectService.SalesHandoff.cs`, `ProjectUnitSalesSyncRules.cs`
- Workflow adapters: `src/ErpSystem.Core/Services/Workflow/WorkflowStatusAdapters.cs`
- Sales API controllers: `src/ErpSystem.Api/Controllers/Sales/LeadController.cs`, `OpportunityController.cs`, `QuoteController.cs`, `CampaignController.cs`, `SalesOrderController.cs`, `SalesAgreementController.cs`, `SalesAllocationsController.cs`, `SalesReportingController.cs`
- CRM API controller: `src/ErpSystem.Api/Controllers/Crm/CrmController.cs`
- Finance AR customer/payment touchpoints: `src/ErpSystem.Api/Controllers/Finance/CustomerController.cs`, `src/ErpSystem.Core/Entities/Finance/CustomerPayment.cs`
- CRM frontend routes: `frontend/src/app/crm/**`
- Sales frontend routes: `frontend/src/app/sales/**`
- Marketing frontend routes: `frontend/src/app/marketing/**`
- Frontend services: `frontend/src/services/crmService.ts`, `campaignService.ts`, `salesOrderService.ts`, `salesAgreementService.ts`, `salesAllocationService.ts`, `salesReportingService.ts`

## Verification Log

| Date | Check | Status | Result |
| --- | --- | --- | --- |
| 2026-07-09 | Source DOCX render with artifact-tool | Passed | `ERP QUESTIONNAIRE.docx` rendered to 10 page images in `artifacts\tdc-sales-marketing-crm-doc-render`. |
| 2026-07-09 | Structured DOCX extraction | Passed | All 17 paragraphs and 12 tables were extracted, including every Must Have response. |
| 2026-07-09 | Rendered page visual contact review | Passed | Contact sheet confirmed the rendered source structure from business objectives through priorities/additional comments. |
| 2026-07-09 | Live source audit | Passed | Sales/CRM/Marketing entities, DTOs, services, controllers, routes, workflow adapters, reports, campaign structures, AR touchpoints, and existing Sales tracker were inspected. |
| 2026-07-09 | Tracker creation | Passed | Full coverage matrix, configuration inputs, roadmap, acceptance scenarios, requirement traceability, and code anchors created. |
| 2026-07-17 | Source identity and authoritative Word render | Passed | The supplied cached DOCX and original Marketing DOCX have the same SHA-256 digest; Microsoft Word rendered the source as 11 pages. The prior 10-page count is superseded. |
| 2026-07-17 | Questionnaire row reconciliation | Passed | All 56 answered rows were mapped to requirement IDs, implementation tasks, and acceptance scenarios. Fifty-five rows are explicitly Must Have; the segmentation yes/no row has a blank priority while its definition row is Must Have. |
| 2026-07-17 | Live implementation revalidation | Passed | Lead/customer models, CRM aggregation, campaign ROI, account health/readiness/risk/collaboration/service, EHC ticket/problem linkage, Sales agreements/allocations, workflow adapters, Finance touchpoints, frontend routes, and services were re-inspected. |
| 2026-07-17 | Tracker integrity review | Passed | Added canonical-customer configuration/task/scenario coverage, resolved the previously referenced but undefined `SMC-0114`, and confirmed source-row traceability to the implementation ledger. |

## Deferred CRM Follow-ups — Property Enquiries

Recorded 2026-09-21 at the user's request. These are notes for the later CRM implementation phase; no runtime changes are authorized by this note and neither item is implemented or accepted yet.

| Task ID | Status | Current gap | Later implementation and verification |
| --- | --- | --- | --- |
| SMC-FU-001 | Not started — deferred to CRM phase | Property enquiry messages, notes and status events feed CRM activities, but activities created or edited directly in CRM do not appear back in the linked enquiry conversation/history. | Reflect relevant CRM engagements back into the linked enquiry through existing CRM/EHC services. Preserve actor, timestamps and source linkage; respect internal/customer-visible distinctions and tenant/access rules. Prevent synchronization loops and duplicates on retries. Verify activity creation/update from CRM and existing enquiry-to-CRM synchronization together. |
| SMC-FU-002 | Not started — deferred to CRM phase | Reassigning a property enquiry does not update the assignee on its existing linked CRM Lead and Opportunity. | Synchronize linked CRM ownership when the enquiry assignee changes. Resolve clearing/reassignment and intentional independent CRM ownership behavior during that slice, rather than introducing a rigid rule now. Preserve record IDs/history, enforce authorized tenant-scoped assignment, and verify repeated saves do not create duplicate CRM records. |

Baseline from the 20 September code review: an enquiry assigned to active organizational unit `UNIT-MKT` (currently **Marketing Unit**) and moved out of **New** creates its linked Qualified Lead and Qualification-stage Opportunity during assignment/status save. Closed Won requires the separate **Hand off to Estate** action; successful Estate acceptance resolves the enquiry, while final Closed remains a Helpdesk transition. Preserve this existing flow when implementing the follow-ups.

Starting code anchors: `src/ErpSystem.Core/Services/Ehc/EhcTicketService.cs` (`SynchronizePropertyEnquiryCrmAsync`, assignment and transition handlers), `src/ErpSystem.Core/Services/Crm/CrmService.cs` (activity create/update), and `src/ErpSystem.Api/Controllers/Ehc/EhcPropertyEnquiriesController.cs` (Estate handoff and resolution).

## Tracker Maintenance

- Start every implementation slice by selecting explicit `SMC-*` task IDs.
- Keep this tracker synchronized when code changes alter baseline coverage.
- Do not mark a task `Done` until backend, frontend, configuration, workflow, audit, tests, migrations, and smoke evidence are present.
- Prefer configuration over hard-coded policy for TDC lead stages, scoring, assignment, documents, approval routes, report formulas, privacy rules, and integrations.
- Do not remove requirements from the tracker. Record an approved scope decision with evidence if TDC defers or changes a requirement.
