# RHEMA ERP Architecture Diagrams

These diagrams are stored as text-based Mermaid definitions so they can be reviewed in pull requests and rendered directly by GitHub. On GitHub, open this file to view the diagrams. To export PNG or SVG, copy a Mermaid block into [Mermaid Live](https://mermaid.live/) and choose **Actions → Export**.

## Solution architecture

```mermaid
flowchart TB
    classDef channel fill:#ffffff,stroke:#2778e8,stroke-width:2px,color:#172033
    classDef edge fill:#183451,stroke:#183451,color:#ffffff
    classDef app fill:#eaf3ff,stroke:#2778e8,stroke-width:2px,color:#172033
    classDef shared fill:#e8f7f4,stroke:#17a398,stroke-width:2px,color:#172033
    classDef data fill:#fff5e8,stroke:#e8932e,stroke-width:2px,color:#172033
    classDef ops fill:#edf8ef,stroke:#48a868,stroke-width:2px,color:#172033

    subgraph Channels[Channels and actors]
      Internal[Internal ERP users<br/>Finance · HR · Operations · Administration]
      External[External portal users<br/>Suppliers · Bidders · Partners · Requesters]
      Mobile[Mobile and field users<br/>Fleet · Maintenance · Inspections]
      Oversight[Support and oversight<br/>Helpdesk · Approvers · Auditors]
    end

    subgraph Experience[Experience and edge]
      Nginx[Nginx load balancer<br/>TLS · routing · health-aware traffic]
      Web[Next.js 15 / React 19 web tier<br/>Three active instances plus backup]
      Access[REST/JSON + OpenAPI<br/>SignalR dashboard hub]
    end

    subgraph Platform[Application and domain platform]
      API[ASP.NET Core 8 API<br/>Controllers · middleware · versioning · health]
      Services[Modular ERP application services<br/>Business orchestration and DTO contracts]
      Core[Core domain<br/>Entities · validation · interfaces · domain services]
      Cross[Cross-cutting services<br/>Identity · RBAC · tenancy · workflow · notifications]
      Infra[Data and infrastructure<br/>EF Core · repositories · migrations · caching]
      Jobs[Hosted background services<br/>Notifications · M365 ingest · EHC SLA monitoring]
    end

    subgraph Stores[Data, security, and operations]
      SQL[(SQL Server 2022<br/>Transactional system of record)]
      Redis[(Redis 7<br/>Distributed cache and shared state)]
      Files[Secure file storage<br/>ClamAV malware scanning]
      Observe[Observability<br/>Serilog · Fluentd · Elastic · Kibana<br/>Prometheus · Grafana]
    end

    Internal & External & Mobile & Oversight --> Nginx
    Nginx --> Web --> Access --> API
    API --> Services
    API --> Cross
    Services --> Core
    Services --> Infra
    Jobs --> Services
    Infra --> SQL
    Infra --> Redis
    Services --> Files
    API -. telemetry .-> Observe
    Jobs -. telemetry .-> Observe

    class Internal,External,Mobile,Oversight channel
    class Nginx edge
    class Web,Access,API,Services,Core app
    class Cross,Jobs shared
    class Infra,SQL,Redis,Files data
    class Observe ops
```

## Capability and integration map

```mermaid
flowchart TB
    classDef platform fill:#183451,stroke:#183451,color:#ffffff,stroke-width:3px
    classDef domain fill:#ffffff,stroke:#2778e8,color:#172033,stroke-width:2px
    classDef foundation fill:#eef4f9,stroke:#7890a8,color:#172033

    Platform([Shared ERP platform<br/>Identity and RBAC · Tenant isolation · Workflow engine<br/>Notifications · Document numbering · Reporting<br/>Audit trail · SignalR])

    Finance[Finance and accounting<br/>GL · AP/AR · bank and cash · budgets<br/>tax · fixed assets · reporting]
    Procurement[Procurement<br/>Planning · requisitions · RFQs · tenders<br/>purchase orders · contracts · suppliers]
    HR[HR and payroll<br/>Employees · organization · positions<br/>leave · benefits · payroll]
    Operations[Inventory and maintenance<br/>Warehouses · stock movements · fleet<br/>assets · work orders · inspections]
    Estate[Estate and facilities<br/>Land · property · facilities · permits<br/>GIS views · service delivery]
    Projects[Projects and planning<br/>Portfolios · programs · resources<br/>timelines · expenses · billing]
    Sales[Sales, CRM, and pricing<br/>Customers · partners · campaigns<br/>agreements · products · price lists]
    Support[Documents and helpdesk<br/>Records · secure attachments · knowledge base<br/>tickets · complaints · enquiries · SLAs]

    SQL[(SQL Server<br/>System of record)]
    Redis[(Redis<br/>Cache and distributed state)]
    Files[Secure files<br/>ClamAV protected]
    Ops[Operations<br/>Metrics · logs · health]
    Channels[External channels<br/>Partner portal · email · mobile]

    Finance --- Platform
    Procurement --- Platform
    HR --- Platform
    Operations --- Platform
    Estate --- Platform
    Projects --- Platform
    Sales --- Platform
    Support --- Platform

    Platform --> SQL
    Platform --> Redis
    Platform --> Files
    Platform -.-> Ops
    Channels --> Platform

    class Platform platform
    class Finance,Procurement,HR,Operations,Estate,Projects,Sales,Support domain
    class SQL,Redis,Files,Ops,Channels foundation
```
