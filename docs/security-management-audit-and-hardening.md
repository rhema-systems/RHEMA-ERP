# Security Management audit and hardening

## Objective

Turn `/administration/security/dashboard` and its linked administration pages into a tenant-safe security operations workspace backed by real application data. The feature provides operational evidence and control visibility; it does not claim SOC 2 certification.

## Scope and authorization boundary

- All records, counts, events, users, sessions, alerts, policies, and retention data are restricted to the current authenticated tenant.
- Cross-tenant aggregation is outside this workstream.
- Existing permissions remain the source of authority: `audit.read` (read security telemetry), `settings.read` (read configuration), and `settings.update` (administrative actions). SuperAdmin keeps the platform's established permission bypass, within the active tenant context.
- Server authorization is mandatory for every read and mutation. UI visibility is only a usability aid.
- Secrets, authentication codes, tokens, full credentials, and CAPTCHA secret values must never be returned to the browser or written to logs/audit payloads.

## Current implementation audit

| Area | Classification | Evidence and disposition |
| --- | --- | --- |
| Login success/failure and account lockout | Real | `AuthController` persists tenant-owned `SecurityLog` rows. Use these rows for authentication KPIs and timelines. |
| Active sessions | Real, with unsafe administration paths | `UserSession` stores tenant, user, IP, user agent, timestamps, and termination state. Some `SessionController` actions called an unscoped service by ID; all administrative reads and mutations must first resolve records inside the active tenant. |
| Audit activity | Real | `AuditLog` is tenant-owned and append-only. The dashboard should summarize only security-relevant activity and link to the full audit module. |
| Security-log service | Incorrect | Read, detail, failed-login, and cleanup queries were not tenant-filtered. Fix every query and cleanup operation at the service boundary. |
| MFA adoption | Partially real | `ApplicationUser.TwoFactorEnabled` is authoritative. The old percentage included an in-memory all-user query and did not distinguish eligible/privileged accounts. Query tenant users directly. |
| MFA enabled date and recovery counts | Unavailable | The current schema does not persist a reliable enabled timestamp or recovery-code inventory for this view. Display unavailable rather than substituting account creation time. |
| Password compliance | Incorrect | The old calculation treated recent account creation or MFA enrollment as password compliance. Remove this metric until password policy and password-age evidence can support it. |
| Security health score | Incorrect | The old score used hidden fallback values, log volume, role diversity, and unrelated proxies. Replace it with explicit weighted controls and show points/evidence for every factor. |
| Daily trends | Incorrect | Trend values were calculated but not persisted consistently; cached daily values could remain stale all day. The operations overview must query the requested time window directly. |
| Security alerts | Partially real | `SecurityAlert` rows are persisted but mostly manual and only support dismissed/open. Do not imply automated detection or full incident lifecycle. Surface persisted alerts honestly. |
| Threat detections | Real only when persisted | `ThreatDetection` has severity/status/resolution fields. No reliable producer was identified during the initial trace, so empty data is a valid state and no detections will be fabricated. |
| Audit filters | Incorrect | `result` and `risk` filters were accepted by the UI but were inferred after paging and not applied reliably. Filter only fields backed by persisted evidence, and label inferred classifications. |
| Device identity and location | Partially real | Device/browser/OS/IP/location fields are persisted on sessions. Trust and risk are not authoritative unless explicit rules and evidence are returned with the result. |
| Session risk | Partially real | Stale, disabled-user, and concurrent-session findings can be derived using documented rules. Do not present geolocation or device novelty as risk without a dependable baseline. |
| Privileged access | Real | Derive from active tenant role assignments and MFA/status/login facts. Direct-permission review remains unavailable where the identity model has no reliable direct-user assignment source. |
| Security settings | Real, with secret exposure | Tenant security settings are persisted. CAPTCHA secrets were returned by API responses and copied into audit JSON. Return configured flags only and redact secret fields from audit evidence. |
| Data retention | Real where configured | The retention controller/policies are tenant scoped. Show policy/job facts only when persisted; do not invent next-run times. |
| System health | Separate concern | General service uptime is not a security control. Only show logging/configuration evidence that the security module can establish. |
| Legacy mock components | Dummy/unused | `SecuritySettings.tsx` contains mock initial state and `DeviceManagement.old.tsx` contains old sample behavior. They must not be data sources for the active page. |

## Target information architecture

1. **Overview**: real KPIs, explicit control score, active alert summary, recent security activity, and investigation links.
2. **Sessions & devices**: tenant-scoped, server-paged sessions with documented stale/session findings and authorized termination.
3. **Privileged access**: tenant users with administrative roles, MFA/status/last-login evidence, and review findings.
4. **Authentication & MFA**: current-user factor management plus tenant-level adoption evidence for authorized readers.
5. **Security events and audit**: server-filtered authentication/security events and a security-relevant audit summary, with links to complete log modules.
6. **Policies, retention, and configuration**: reuse existing pages and configuration controls; avoid duplicate editors.

## Security health scoring model

The operations overview scores a maximum of 100 points and returns each component's earned points, maximum points, status, and evidence:

| Control | Weight | Calculation |
| --- | ---: | --- |
| MFA coverage | 20 | Proportional share of active eligible tenant users with MFA enabled. |
| Privileged-account MFA | 20 | Proportional share of active privileged tenant users with MFA enabled. Full points when the tenant has no privileged users in scope. |
| Session hygiene | 15 | Starts at 15 and deducts proportionally for active sessions stale beyond the configured idle timeout and for disabled users with active sessions. |
| Audit telemetry | 15 | 15 when both audit and security log evidence exists in the selected window, 8 when one source exists, 0 when neither exists. This measures evidence availability, not event volume. |
| Authentication anomalies | 15 | Deducts for failed-login rate, account lockouts, and unresolved high/critical persisted detections in the selected window. |
| Security configuration | 15 | Evidence-based checks for lockout threshold/duration, session timeout, password length, CAPTCHA configuration, and retention configuration when those values exist. Missing configuration earns no points for that check. |

Snapshot metrics (active/locked users and active/stale sessions) are explicitly labeled separately from time-window metrics.

## Event severity rules

- **Critical**: persisted critical threat/alert or confirmed privileged compromise signal.
- **High**: persisted high-severity threat/alert, privileged-account MFA disabled, or privileged account locked after authentication failures.
- **Medium**: account lockout, brute-force event, repeated-failure threshold, disabled user with an active session.
- **Low**: stale session or newly observed session when the persisted record is the only evidence.
- **Informational**: successful login/logout and expected administrative security action.

The UI shows the source and reason for every derived classification.

## Performance and indexing review

Existing indexes are mostly single-column and do not match tenant-first dashboard filters. Add tenant-leading composite indexes for security logs, audit logs, sessions, alerts, and threats. Operations queries use `AsNoTracking`, projections, bounded time ranges, server pagination, and set-based joins. Avoid per-row user, role, or location lookups.

## Expected implementation surfaces

- `src/ErpSystem.Shared/SecurityDtos.cs`
- `src/ErpSystem.Core/Services/SecurityOperationsService.cs`
- `src/ErpSystem.Core/Services/LogService.cs`
- `src/ErpSystem.Core/Services/SecurityService.cs`
- `src/ErpSystem.Api/Controllers/SecurityController.cs`
- `src/ErpSystem.Api/Controllers/SessionController.cs`
- `src/ErpSystem.Api/Controllers/SettingsController.cs`
- authorization and dependency-registration configuration
- `src/ErpSystem.Data/ApplicationDbContext.cs` and a scoped migration if composite indexes are missing
- `frontend/src/services/security.ts`
- `frontend/src/app/administration/security/dashboard/page.tsx`
- active security/session/access-review components and focused tests

## Verification tracker

- [ ] Sensitive MFA/CAPTCHA values removed from logs, responses, and audit payloads.
- [ ] Security log reads/details/cleanup are tenant scoped.
- [ ] Administrative session reads and mutations are tenant scoped and audited.
- [ ] Backend policies enforce telemetry reads and administrative mutations.
- [ ] Operations overview uses direct, bounded, set-based tenant queries.
- [ ] Health score returns auditable factors and deterministic calculations.
- [ ] Events, sessions, privileged access, and configuration states have honest empty states.
- [ ] Composite indexes and migration verified.
- [ ] Backend tenant-isolation, authorization, redaction, and scoring tests pass.
- [ ] Frontend data/error/empty/permission states and build pass.
- [ ] Authenticated browser validation completed for desktop and responsive layouts.
