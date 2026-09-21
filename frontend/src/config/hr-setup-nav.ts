import {
  AlarmClock,
  ArrowRightLeft,
  BadgeCheck,
  BellRing,
  Boxes,
  Briefcase,
  Building,
  Building2,
  CalendarClock,
  CalendarDays,
  CalendarRange,
  ClipboardCheck,
  ClipboardPen,
  Clock,
  Coins,
  CreditCard,
  Database,
  DollarSign,
  FileSignature,
  DoorOpen,
  Fingerprint,
  Flag,
  FolderTree,
  Gauge,
  Gavel,
  Globe,
  GraduationCap,
  Handshake,
  Hash,
  HelpCircle,
  IdCard,
  Landmark,
  Layers,
  LayoutList,
  Library,
  ListChecks,
  ListOrdered,
  ListTree,
  MapPin,
  Medal,
  Plane,
  Repeat2,
  Route,
  Settings,
  ShieldCheck,
  ShieldPlus,
  SlidersHorizontal,
  Tag,
  Tags,
  Target,
  Timer,
  TriangleAlert,
  Trophy,
  UserPlus,
  UserRoundCheck,
  Users,
  Users2,
  HeartHandshake,
  Accessibility,
  Languages,
  Workflow,
  Wrench,
  type LucideIcon,
} from 'lucide-react';

/**
 * The single source of truth for HR setup navigation.
 *
 * Three surfaces used to carry their own copy of this list — the sidebar's Administration → HR
 * node, the `/administration/hr` hub page's card grid, and `/settings/modules/human-resources`
 * (which derives its list from the sidebar). All three had drifted: Awards had a hub tile and no
 * sidebar entry, the Discipline Catalogue was sidebar-unreachable behind a childless Discipline
 * node, and thirteen sidebar entries had no hub tile at all.
 *
 * ⚠ **Every entry must sit inside a group.** The settings module page prints
 * `trail.slice(1, -1)` as a tile's subtext, so a leaf hanging directly off the HR node renders
 * with an empty subtitle — which is why twenty-two tiles on that page had no caption. Grouping is
 * not decoration here; it is what supplies the caption.
 */
export interface HrSetupLink {
  title: string;
  href: string;
  icon: LucideIcon;
  /** Shown on the hub card and as the settings tile's subtext. Required — see the note above. */
  description: string;
}

export interface HrSetupGroup {
  title: string;
  /** The group's landing page. Also the sidebar row's own href. */
  href: string;
  icon: LucideIcon;
  description: string;
  links: HrSetupLink[];
}

export const hrSetupGroups: HrSetupGroup[] = [
  {
    title: 'HR Settings',
    href: '/administration/hr/settings',
    icon: Settings,
    description: 'Tenant-wide HR rules that letters, reminders and enforcement read.',
    links: [
      {
        title: 'Company Profile',
        href: '/administration/hr/settings/company-profile',
        icon: Building2,
        description:
          'Legal identity, statutory numbers, registered address and the letterhead used on offer and confirmation letters.',
      },
      {
        title: 'Policy Settings',
        href: '/administration/hr/settings/policy',
        icon: SlidersHorizontal,
        description:
          'Retirement ages, probation and notice defaults, reminder lead times, enforcement modes and succession weights.',
      },
      {
        // Per-register numbering rules AND the counter behind them — the counter only knows about
        // numbers it issued, so a loaded register leaves it at zero while thousands are in use.
        title: 'Staff Numbering',
        href: '/administration/hr/settings/staff-numbering',
        icon: Hash,
        description: 'Numbering rules per register, and the counter each one issues from.',
      },
      {
        // HR finish plan lane 8: which Finance accounts HR posts to, which money events post,
        // and the register of what reached the ledger.
        title: 'Finance Posting',
        href: '/administration/hr/settings/finance-posting',
        icon: Landmark,
        description: 'Account roles, the HR money events that post to Finance, and the posting register.',
      },
    ],
  },
  {
    title: 'Organization',
    href: '/administration/hr/organization',
    icon: Building2,
    description:
      'The org backbone: a structure defines its levels, and units are the actual nodes.',
    // Unit Change Log is deliberately absent. It is the audit trail OF the units configured here,
    // not a fourth thing to configure, so it hangs off the Units screen instead. The route
    // /administration/hr/organization/unit-history is unchanged.
    links: [
      {
        title: 'Structures',
        href: '/administration/hr/organization/structures',
        icon: Building2,
        description: 'Templates that group a set of organizational levels.',
      },
      {
        title: 'Levels',
        href: '/administration/hr/organization/levels',
        icon: ListTree,
        description: 'The tiers within a structure — division, department, unit.',
      },
      {
        title: 'Units',
        href: '/administration/hr/organization/units',
        icon: FolderTree,
        description: 'The actual org nodes employees and positions belong to.',
      },
      {
        title: 'Teams',
        href: '/administration/hr/organization/teams',
        icon: Users2,
        description:
          'Working groups — permanent, project, task force, committee — and who is in them.',
      },
      {
        title: 'Departments',
        href: '/administration/hr/departments',
        icon: Building,
        description: 'Read-only lookup shared with other modules.',
      },
    ],
  },
  {
    title: 'Locations',
    href: '/administration/hr/location',
    icon: MapPin,
    description: 'Location structures, their levels and the sites themselves.',
    links: [
      {
        title: 'Structures',
        href: '/administration/hr/location/structures',
        icon: MapPin,
        description: 'Templates that group a set of location levels.',
      },
      {
        title: 'Levels',
        href: '/administration/hr/location/levels',
        icon: ListTree,
        description: 'The tiers within a location structure — region, district, site.',
      },
      {
        title: 'Locations',
        href: '/administration/hr/location/locations',
        icon: MapPin,
        description: 'The physical sites employees are posted to.',
      },
    ],
  },
  {
    title: 'Jobs & Establishment',
    href: '/administration/hr/positions',
    icon: Briefcase,
    description: 'Posts, the taxonomy above them, and what each one requires.',
    links: [
      {
        title: 'Job Positions',
        href: '/administration/hr/positions',
        icon: Users,
        description: 'Positions, their skill requirements and grades.',
      },
      {
        title: 'Job Architecture',
        href: '/administration/hr/job-architecture',
        icon: Layers,
        description: 'Job families, sub-families and the career-level ladder.',
      },
      {
        title: 'Staff Levels',
        href: '/administration/hr/staff-levels',
        icon: ListTree,
        description: 'Ranked staff tiers.',
      },
      {
        title: 'Competencies',
        href: '/administration/hr/competencies',
        icon: Layers,
        description:
          'What the organisation expects someone to be able to do, above the individual skills that evidence it.',
      },
      {
        title: 'Skills',
        href: '/administration/hr/skills',
        icon: Wrench,
        description: 'Skills positions require and employees hold.',
      },
      {
        // Round 2, lane C3. A bundle of skills a post needs, maintained in one place: attaching the
        // set is exactly the same as attaching its skills one at a time, and the post may not then
        // list one of them individually as well.
        title: 'Skill Sets',
        href: '/administration/hr/skill-sets',
        icon: Layers,
        description: 'Named bundles of skills, attached to a position in one move.',
      },
      {
        // Renamed from the bare "Establishment": an operational screen of that exact name already
        // exists at /hr/recruitment/establishment, showing headcount against the establishment and
        // the gaps. Two identically-named screens in two menus doing opposite jobs is what made
        // this one unfindable.
        title: 'Manual Establishment',
        href: '/administration/hr/establishment',
        icon: ShieldCheck,
        description:
          'Establish a post no approved manpower budget covers — the one place the budget chain can be bypassed, which is why it is Admin-tier.',
      },
    ],
  },
  {
    title: 'People Reference Data',
    href: '/administration/hr/qualifications',
    icon: Database,
    description: 'The catalogues employee records, offers and job descriptions are written from.',
    links: [
      {
        title: 'Qualifications',
        href: '/administration/hr/qualifications',
        icon: GraduationCap,
        description: 'The qualification catalogue.',
      },
      {
        // QualificationType is a category and cannot answer "is a Master's above a Diploma",
        // which is exactly what shortlisting and succession ask.
        title: 'Qualification Levels',
        href: '/administration/hr/qualification-levels',
        icon: ListOrdered,
        description: 'The rank a qualification sits on, so one can be compared against another.',
      },
      {
        title: 'Certifying Bodies',
        href: '/administration/hr/certifying-bodies',
        icon: BadgeCheck,
        description: 'Who certified a skill, as a catalogue rather than free text on every row.',
      },
      {
        // Round 2, lane C2. A credential is a compliance object — it expires, is renewed, is
        // revoked — so it is catalogued under the body that issues it, not as a qualification type.
        title: 'Certifications',
        href: '/administration/hr/certifications',
        icon: BadgeCheck,
        description: 'The credentials each certifying body issues — what skills, positions and people cite.',
      },
      {
        // Round 2, lane C3. The regulatory bundle a post must hold — "driver: licence class C,
        // defensive driving, first aid" — so that when the regulator changes it, it changes once.
        title: 'Certification Sets',
        href: '/administration/hr/certification-sets',
        icon: ListChecks,
        description: 'Named bundles of credentials a position must hold.',
      },
      {
        // Round 2, lane D1 (Q-4). Seeded with TDC's seven kinds in 2026 and reachable from nowhere
        // until now — and its duration is what dates a fixed-term contract's end, so it is not
        // decoration.
        title: 'Contract Types',
        href: '/administration/hr/contract-types',
        icon: FileSignature,
        description:
          'The kinds of engagement contracts are written under — and how long each normally runs.',
      },
      {
        // Round 2, lane D2. Four columns spelt this out in free text — the referee's, the
        // guarantor's, the next of kin's and the candidate referee's — and never agreed with each
        // other. The CATEGORY is what makes it useful: it is what decides which screens offer a
        // value, so a next-of-kin dropdown never offers "Former manager".
        title: 'Relationship Types',
        href: '/administration/hr/relationship-types',
        icon: HeartHandshake,
        description:
          'How one person is tied to another — what the referee, guarantor and next-of-kin screens pick from.',
      },
      {
        // Round 3, lane P2 (register row E-5). The disability was a tick and a free-text box on
        // the employee and on the dependant; now the tick opens onto this catalogue, and the text
        // stays beside it as notes.
        title: 'Disability Types',
        href: '/administration/hr/disability-types',
        icon: Accessibility,
        description:
          'What the employee and dependant forms pick from once the disability box is ticked.',
      },
      {
        title: 'Identification Types',
        href: '/administration/hr/identification-types',
        icon: IdCard,
        description: 'Identity document types.',
      },
      {
        // Round 3, lane C1. A candidate's languages were free text; the shortlisting engine
        // scored a Language criterion against exactly that text. The catalogue gives the form a
        // dropdown and the criterion something to match on.
        title: 'Languages',
        href: '/administration/hr/languages',
        icon: Languages,
        description: 'The languages a candidate can say they speak or write.',
      },
      {
        title: 'Document Types',
        href: '/administration/hr/document-types',
        icon: ClipboardCheck,
        description:
          "The vocabulary an employee's document file and a position's requirements both speak.",
      },
      {
        title: 'Reason Codes',
        href: '/administration/hr/reason-codes',
        icon: Tags,
        description: 'Standard reasons for HR actions.',
      },
      {
        title: 'Countries',
        href: '/administration/hr/countries',
        icon: Globe,
        description: 'Countries used across HR records.',
      },
      {
        title: 'Banks',
        href: '/administration/hr/banks',
        icon: Landmark,
        description: 'The banks salary accounts can be held at.',
      },
      {
        // Master data behind the job description's bargaining-unit clause.
        title: 'Unions',
        href: '/administration/hr/unions',
        icon: Users2,
        description: 'Trade unions and the collective agreements negotiated with each.',
      },
      {
        title: 'External Associates',
        href: '/administration/hr/external-associates',
        icon: UserRoundCheck,
        description: 'Panellists, assessors and advisers who act for you without an ERP login.',
      },
      {
        title: 'Asset Types',
        href: '/administration/hr/asset-types',
        icon: Boxes,
        description: 'The categories the company-asset register is built on.',
      },
    ],
  },
  {
    title: 'Time, Attendance & Leave',
    href: '/administration/hr/attendance',
    icon: Clock,
    description:
      'What counts as a working day, a shift, an overtime hour and a leave entitlement.',
    links: [
      {
        title: 'Work Schedules',
        href: '/administration/hr/attendance/work-schedules',
        icon: CalendarClock,
        description: 'The working patterns employees are assigned to.',
      },
      {
        title: 'Shift Rotations',
        href: '/administration/hr/attendance/shift-rotations',
        icon: Repeat2,
        description: 'Repeating shift cycles and the order crews move through them.',
      },
      {
        title: 'Holiday Calendars',
        href: '/administration/hr/attendance/holiday-calendars',
        icon: CalendarDays,
        description: 'Public and company holidays, per calendar.',
      },
      {
        title: 'Pay Periods',
        href: '/administration/hr/attendance/pay-periods',
        icon: DollarSign,
        description: 'The periods attendance is totalled and exported against.',
      },
      {
        title: 'Geofence Zones',
        href: '/administration/hr/attendance/geofence-zones',
        icon: MapPin,
        description: 'The map areas a clock-in must fall inside to be accepted.',
      },
      {
        title: 'Devices',
        href: '/administration/hr/attendance/devices',
        icon: Fingerprint,
        description: 'Biometric and terminal devices, and the site each is registered to.',
      },
      {
        title: 'Alert Rules',
        href: '/administration/hr/attendance/alert-rules',
        icon: BellRing,
        description: 'What an absence, a late arrival or a missed punch should raise.',
      },
      {
        title: 'Overtime Policies',
        href: '/administration/hr/attendance/overtime-policies',
        icon: Timer,
        description: 'When overtime is earned, at what multiplier, and up to what ceiling.',
      },
      {
        title: 'Leave Types',
        href: '/administration/hr/leave-types',
        icon: CalendarDays,
        description: 'Leave types with their sub-types, allocations and accrual policies.',
      },
    ],
  },
  {
    // Set up once, then referenced by the operational screens under /hr/company-schedule.
    // Closures sit here because the question they answer — "is this a working day?" — is the
    // same one holiday calendars answer, and that is setup.
    title: 'Company Schedule',
    href: '/administration/hr/company-schedule',
    icon: CalendarDays,
    description: 'The company calendar the booking and event screens are built on.',
    links: [
      {
        title: 'Meeting Rooms',
        href: '/administration/hr/company-schedule/rooms',
        icon: DoorOpen,
        description: 'Bookable rooms, their capacity and the site each sits at.',
      },
      {
        title: 'Business Closures',
        href: '/administration/hr/company-schedule/closures',
        icon: CalendarClock,
        description: 'Days the company is shut, over and above the holiday calendars.',
      },
      {
        title: 'Milestones',
        href: '/administration/hr/company-schedule/milestones',
        icon: Flag,
        description: 'Dates the organisation marks in its own calendar.',
      },
      {
        title: 'Fiscal Years',
        href: '/administration/hr/company-schedule/fiscal-years',
        icon: CalendarRange,
        description: 'The financial years budgets, plans and appraisal cycles are cut against.',
      },
    ],
  },
  {
    title: 'Pay & Benefits',
    href: '/administration/hr/compensation',
    icon: Coins,
    description: 'What a post is worth, what it attracts, and how payroll is parameterised.',
    links: [
      {
        // Lane G. Read-only while Payroll is the structure source (the default); the grade, level
        // and notch screens open when the source is HR — a policy setting.
        title: 'Salary Structure',
        href: '/administration/hr/compensation/salary-structure',
        icon: Coins,
        description: 'Grades, levels and notches — mirrored from Payroll, or maintained here.',
      },
      {
        title: 'Pay Components',
        href: '/administration/hr/compensation/pay-components',
        icon: Coins,
        description: 'Earnings and deductions mirrored from Payroll, with their HR-side rules.',
      },
      {
        title: 'Position Emoluments',
        href: '/administration/hr/compensation/position-emoluments',
        icon: Briefcase,
        description: 'What each position attracts, over and above the grade.',
      },
      {
        title: 'Benefit Policies',
        href: '/administration/hr/compensation/benefit-policies',
        icon: ShieldPlus,
        description: 'Who qualifies for which benefit, and on what terms.',
      },
      {
        // Round 2, lane C3. ⚠ Members carry no amount and no expiry: a post needing its own figure
        // for a benefit takes that benefit individually instead (plan Q-5).
        title: 'Benefit Groups',
        href: '/administration/hr/compensation/benefit-groups',
        icon: Boxes,
        description: 'Named bundles of benefits, attached to a position in one move.',
      },
      {
        title: 'Payroll Setup',
        href: '/administration/hr/payroll',
        icon: CreditCard,
        description: 'Grades, components, tax tables and payroll parameters.',
      },
    ],
  },
  {
    title: 'Recruitment',
    href: '/administration/hr/recruitment',
    icon: Workflow,
    description: 'The shape of the hiring process, before any vacancy runs through it.',
    links: [
      {
        title: 'Pipelines',
        href: '/administration/hr/recruitment/pipelines',
        icon: Workflow,
        description: 'The stages applications move through, and the rules governing those moves.',
      },
      {
        title: 'Question Bank',
        href: '/administration/hr/recruitment/question-bank',
        icon: HelpCircle,
        description:
          'The questions candidates are asked, with the weight and score band each is marked against.',
      },
      {
        title: 'Interview Presets',
        href: '/administration/hr/recruitment/question-presets',
        icon: LayoutList,
        description:
          'Reusable interview shapes — which question types a panel covers, and how many of each.',
      },
      {
        title: 'Check Templates',
        href: '/administration/hr/recruitment/check-templates',
        icon: ClipboardCheck,
        description:
          'Standard sets of pre-employment checks — medical, police clearance, references — applied to an offer in one step.',
      },
    ],
  },
  {
    title: 'Performance',
    href: '/administration/hr/performance',
    icon: Target,
    description:
      'The instruments an appraisal cycle is run with, and the goals it is scored against.',
    links: [
      {
        title: 'Appraisal Settings',
        href: '/administration/hr/performance/settings',
        icon: SlidersHorizontal,
        description: 'Weighting, rounding and the rules a cycle is scored under.',
      },
      {
        title: 'Appraisal Templates',
        href: '/administration/hr/performance/templates',
        icon: ClipboardCheck,
        description: 'The form each population is appraised on.',
      },
      {
        title: 'Appraisal Criteria',
        href: '/administration/hr/performance/criteria',
        icon: ListChecks,
        description: 'The individual lines a template is assembled from.',
      },
      {
        title: 'Grade Definitions',
        href: '/administration/hr/performance/grade-definitions',
        icon: Medal,
        description: 'The bands a final score is translated into.',
      },
      {
        title: 'Strategic Goals',
        href: '/administration/hr/performance/strategic-goals',
        icon: Target,
        description: 'The organisational goals unit and employee goals cascade from.',
      },
      {
        title: 'Goal Library',
        href: '/administration/hr/performance/goal-library',
        icon: Library,
        description: 'Reusable goal statements managers can draw on rather than retype.',
      },
      {
        title: 'KPI Definitions',
        href: '/administration/hr/performance/kpi-definitions',
        icon: Gauge,
        description:
          'What each measure means, its unit, and the direction that counts as better.',
      },
      {
        title: 'Goal Risk Thresholds',
        href: '/administration/hr/performance/goal-risk-settings',
        icon: TriangleAlert,
        description: 'How far behind a goal must fall before it is flagged at risk.',
      },
    ],
  },
  {
    title: 'Training & Learning',
    href: '/administration/hr/training',
    icon: GraduationCap,
    description:
      'The training catalogue — what can be delivered, by whom, and what it must renew.',
    // Needs Assessments, Training Plans and Training Budgets are deliberately absent: they are
    // cyclical casework, not catalogue, and now appear under HR → Training & Development via
    // hrOperationalTrainingLinks below.
    links: [
      {
        title: 'Categories',
        href: '/administration/hr/training/categories',
        icon: Tag,
        description: 'Classification used to group training programs.',
      },
      {
        title: 'Program Groups',
        href: '/administration/hr/training/program-groups',
        icon: Layers,
        description: 'Optional curriculum clusters above the individual program.',
      },
      {
        title: 'Vendors',
        href: '/administration/hr/training/vendors',
        icon: Building2,
        description: 'External training firms, consultants and institutions.',
      },
      {
        title: 'Trainers',
        href: '/administration/hr/training/trainers',
        icon: GraduationCap,
        description: 'Internal and external trainers, their skills and availability.',
      },
      {
        title: 'Programs',
        href: '/administration/hr/training/programs',
        icon: Library,
        description: 'The training program catalogue — materials, competencies and skills.',
      },
      {
        title: 'Compliance Requirements',
        href: '/administration/hr/training/compliance',
        icon: ShieldCheck,
        description: 'Training a population must hold, and how often it renews.',
      },
      {
        title: 'Learning Paths',
        href: '/administration/hr/training/learning-paths',
        icon: Route,
        description: 'Ordered curricula and the skills each one targets.',
      },
      {
        // Renamed from "Mentoring Programmes". The scheme is setup; the mentor/mentee PAIRS inside
        // it are casework, and already have their own screen at /hr/training/mentoring.
        title: 'Mentoring Schemes',
        href: '/administration/hr/training/mentoring',
        icon: Handshake,
        description: 'Mentoring schemes and the terms each one runs on.',
      },
    ],
  },
  {
    title: 'Orientation & Onboarding',
    href: '/administration/hr/orientation',
    icon: UserPlus,
    description: 'The induction catalogue and the reusable onboarding checklists.',
    links: [
      {
        title: 'Programmes',
        href: '/administration/hr/orientation/programs',
        icon: Library,
        description: 'Induction programmes with their modules, assessments and audience rules.',
      },
      {
        title: 'Categories',
        href: '/administration/hr/orientation/categories',
        icon: Tags,
        description: 'Classification used to group induction content.',
      },
      {
        title: 'Onboarding Templates',
        href: '/administration/hr/orientation/onboarding-templates',
        icon: ListChecks,
        description: 'Reusable task checklists a new hire is walked through.',
      },
    ],
  },
  {
    // This whole group had NO sidebar entry — the hub card was its only way in, so it was absent
    // from /settings/modules/human-resources entirely.
    title: 'Awards',
    href: '/administration/hr/awards',
    icon: Medal,
    description: 'The award scheme: what can be won, when it runs, and who decides.',
    links: [
      {
        title: 'Award Catalogue',
        href: '/administration/hr/awards/types',
        icon: Medal,
        description:
          'Award types with their levels, budgets and the long-service milestone ladder.',
      },
      {
        title: 'Award Cycles',
        href: '/administration/hr/awards/cycles',
        icon: CalendarRange,
        description: 'The periods nominations open and results are declared against.',
      },
      {
        title: 'Award Committees',
        href: '/administration/hr/awards/committees',
        icon: Trophy,
        description: 'The panels that adjudicate each award.',
      },
    ],
  },
  {
    title: 'Employee Lifecycle Setup',
    href: '/administration/hr/discipline/catalogue',
    icon: UserRoundCheck,
    description: 'The rules discipline, probation, travel and separation cases are run under.',
    links: [
      {
        // Sidebar-unreachable until now: the Discipline node pointed straight at its reminder
        // sweep and carried no children, so the catalogue had a hub card and nothing else.
        title: 'Discipline Catalogue',
        href: '/administration/hr/discipline/catalogue',
        icon: Gavel,
        description:
          'What counts as misconduct, the procedure each offence must follow, and the sanctions available.',
      },
      {
        title: 'Confirming Authorities',
        href: '/administration/hr/probation/confirming-authorities',
        icon: UserRoundCheck,
        description: 'Who may confirm an employee out of probation, by level.',
      },
      {
        title: 'Clearance Form',
        href: '/administration/hr/separation/clearance-form',
        icon: DoorOpen,
        description: 'The clearance an exiting employee is walked through, by department.',
      },
      {
        title: 'Travel Policies',
        href: '/administration/hr/travel/policies',
        icon: Plane,
        description: 'Entitlements, ceilings and approval rules per travel class.',
      },
      // Destination Alerts used to sit here. An advisory is raised against a destination, expires
      // and is re-issued as conditions change — that is casework on the same cadence as the trips
      // it warns about, not a policy set once. It moved to HR → Travel → Destination Alerts,
      // beside Visa Requirements, which it reads like.
    ],
  },
  {
    // Five reminder sweeps that were five unrelated-looking top-level entries. Each is the same
    // thing: a nightly engine with a run-now button, its run history and its dispatch log.
    title: 'Reminders & Sweeps',
    href: '/administration/hr/movements/reminders',
    icon: AlarmClock,
    description: 'The nightly HR engines — run one now, and read what it last dispatched.',
    links: [
      {
        title: 'Movement Reminders',
        href: '/administration/hr/movements/reminders',
        icon: ArrowRightLeft,
        description: 'Acting appointments and transfers falling due.',
      },
      {
        title: 'Discipline Reminders',
        href: '/administration/hr/discipline/reminders',
        icon: Gavel,
        description: 'Hearings, responses and sanctions falling due, with the dispatch log.',
      },
      {
        title: 'Probation Reminders',
        href: '/administration/hr/probation/reminders',
        icon: UserPlus,
        description: 'Probation reviews and confirmations falling due.',
      },
      {
        title: 'Travel Reminders',
        href: '/administration/hr/travel/reminders',
        icon: Plane,
        description: 'Visas, passports and trip approvals falling due.',
      },
      {
        // The retirement and contract-expiry sweeps RAISE separations. They run nightly on their
        // own; this is the manual run, and the only screen either endpoint has ever had.
        title: 'Separation Sweeps',
        href: '/administration/hr/separation/reminders',
        icon: DoorOpen,
        description: 'The retirement and contract-expiry sweeps that raise separations.',
      },
    ],
  },
];

/**
 * The same tree shaped for the sidebar's NavItem contract. Descriptions ride along so the
 * settings module page can print a real caption instead of a breadcrumb.
 */
export const hrSetupNavChildren = hrSetupGroups.map(group => ({
  title: group.title,
  href: group.href,
  icon: group.icon,
  children: group.links.map(link => ({
    title: link.title,
    href: link.href,
    icon: link.icon,
    description: link.description,
  })),
}));

/** Every leaf href in the tree. */
export const hrSetupLinkHrefs = hrSetupGroups.flatMap(group =>
  group.links.map(link => link.href),
);

/**
 * One group by name, for the sub-hub landing pages. Throws rather than rendering an empty grid:
 * a sub-hub silently losing its cards because a group was renamed is exactly the drift this
 * manifest exists to prevent.
 */
export function hrSetupGroup(title: string): HrSetupGroup {
  const group = hrSetupGroups.find(candidate => candidate.title === title);
  if (!group) {
    throw new Error(`No HR setup group named "${title}" in hr-setup-nav.ts`);
  }
  return group;
}

/**
 * Training screens that are cyclical casework rather than catalogue: the front of the chain whose
 * every later step (Requests, Nomination Approvals, Enrollments, Completions) already lived under
 * /hr/training. Listed here rather than inline in the sidebar so this file stays the one place
 * the HR setup boundary is drawn — these are the screens deliberately on the other side of it.
 *
 * Their pages moved from /administration/hr/training to /hr/training on 2026-09-06, which is what
 * let them drop the `admin.hr` permission the /administration route gate imposes: they now gate on
 * HR.Training.Read like the rest of the training desk. The old paths are gone rather than
 * redirected: nothing in the product, the docs or the stored workflow ActionUrls referenced them.
 */
export const hrOperationalTrainingLinks: HrSetupLink[] = [
  {
    title: 'Needs Assessments',
    href: '/hr/training/needs-assessments',
    icon: ClipboardPen,
    description: 'Training gaps identified for individual employees, with recommendations.',
  },
  {
    title: 'Training Plans',
    href: '/hr/training/plans',
    icon: CalendarRange,
    description: 'Annual or quarterly plans by organization scope, with items and budget lines.',
  },
  {
    title: 'Training Budgets',
    href: '/hr/training/budgets',
    icon: Coins,
    description: 'Allocated training spend, approvals and spend transactions.',
  },
];
