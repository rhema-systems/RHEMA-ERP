import {
  Activity,
  BarChart3,
  Banknote,
  Building2,
  CalendarClock,
  CreditCard,
  FileText,
  Home,
  ListFilter,
  ShoppingCart,
  TrendingUp,
  UserCheck,
  Wrench,
  type LucideIcon,
} from 'lucide-react';

export interface ModuleReportLandingItem {
  title: string;
  href: string;
  icon: LucideIcon;
}

export interface ModuleReportLandingGroup {
  title: string;
  items: ModuleReportLandingItem[];
}

export interface ModuleReportLandingDefinition {
  code: string;
  title: string;
  icon: LucideIcon;
  groups: ModuleReportLandingGroup[];
}

/** Central extension point for module teams adding report landing entries. */
export const moduleReportLandings = {
  financial: {
    code: 'financial',
    title: 'Financial reports',
    icon: CreditCard,
    groups: [
      {
        title: 'Core accounting',
        items: [
          { title: 'Financial statements', href: '/finance/reports', icon: FileText },
          // FR-RP-012 is intentionally exposed from the shared Reports hierarchy. Unlike the
          // legacy administration builder, this route uses Finance-owned datasets and permissions.
          { title: 'Ad hoc report builder', href: '/reports/financial/ad-hoc', icon: ListFilter },
          { title: 'Cash reports', href: '/finance/cash/reports', icon: Banknote },
          { title: 'Fixed asset reports', href: '/finance/fixed-assets/reports', icon: Building2 },
          { title: 'Tax reports', href: '/finance/tax/reports', icon: FileText },
          { title: 'Report automation', href: '/reports/automation', icon: CalendarClock },
        ],
      },
      {
        title: 'Subledgers',
        items: [
          { title: 'Accounts payable reports', href: '/finance/ap/reports', icon: CreditCard },
          { title: 'Accounts receivable reports', href: '/finance/ar/reports', icon: CreditCard },
        ],
      },
      {
        title: 'Tax and compliance',
        items: [
          { title: 'WHT certificates', href: '/finance/tax/reports/wht-certificates', icon: FileText },
          // Remittance evidence is a Finance-owned statutory register, so expose it through the
          // shared Reports landing while retaining its existing canonical workspace route.
          { title: 'WHT remittances', href: '/finance/tax/reports/wht-remittances', icon: FileText },
        ],
      },
    ],
  },
  sales: {
    code: 'sales',
    title: 'Sales reports',
    icon: ShoppingCart,
    groups: [{
      title: 'Sales and customer performance',
      items: [
        { title: 'Sales report workspace', href: '/sales/reports', icon: BarChart3 },
        { title: 'CRM reports', href: '/crm/reports', icon: UserCheck },
      ],
    }],
  },
  humanResources: {
    code: 'human-resources',
    title: 'Human Resources reports',
    icon: UserCheck,
    groups: [{
      title: 'Workforce reporting',
      items: [{ title: 'HR report workspace', href: '/reports/hr', icon: FileText }],
    }],
  },
  estate: {
    code: 'estate',
    title: 'Estate reports',
    icon: Home,
    groups: [{
      title: 'Property and facilities',
      items: [
        { title: 'Property dashboard', href: '/estate/property-management/dashboard', icon: Home },
        { title: 'Facilities dashboard', href: '/estate/facilities/dashboard', icon: Building2 },
      ],
    }],
  },
  development: {
    code: 'development',
    title: 'Development reports',
    icon: TrendingUp,
    groups: [{
      title: 'Projects and delivery',
      items: [
        { title: 'Project reports', href: '/development/project-reports', icon: FileText },
        { title: 'Project analytics', href: '/development/project-analytics', icon: TrendingUp },
        { title: 'Civil Engineering reports', href: '/reports/civil-engineering', icon: Building2 },
      ],
    }],
  },
  operations: {
    code: 'operations',
    title: 'Operations reports',
    icon: Activity,
    groups: [{
      title: 'Maintenance and fleet',
      items: [
        { title: 'Maintenance reports', href: '/maintenance/reports', icon: Wrench },
        { title: 'Fleet fuel reports', href: '/maintenance/fleet/fuel', icon: Activity },
      ],
    }],
  },
} satisfies Record<string, ModuleReportLandingDefinition>;
