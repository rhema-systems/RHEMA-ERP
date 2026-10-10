'use client';

import { usePathname } from 'next/navigation';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { AuthGuard } from '../../components/auth/auth-guard';

interface AdministrationLayoutProps {
  children: React.ReactNode;
}

export default function AdministrationLayout({
  children,
}: AdministrationLayoutProps) {
  const pathname = usePathname() ?? '';

  let requiredPermissions: string[] | undefined;
  let requiredRoles: string[] | undefined;
  let accessMode: 'all' | 'any' = 'all';
  if (pathname.startsWith('/administration/mobile-pos')) {
    requiredPermissions = [
      'MobilePOS.Store.View',
      'MobilePOS.Store.Manage',
      'MobilePOS.Device.Approve',
      'MobilePOS.Till.Review',
    ];
    accessMode = 'any';
  } else if (pathname.startsWith('/administration/finance')) {
    requiredPermissions = ['Finance.Admin'];
  } else if (pathname.startsWith('/administration/document-management')) {
    requiredPermissions = ['Finance.Admin'];
    requiredRoles = [
      'admin',
      'Admin',
      'SystemAdmin',
      'SuperAdmin',
      'TenantAdmin',
      'Document Control Officer',
      'Records Officer',
    ];
    accessMode = 'any';
  } else if (pathname.startsWith('/administration/estate')) {
    requiredRoles = [
      'admin',
      'Admin',
      'SystemAdmin',
      'SuperAdmin',
      'TenantAdmin',
    ];
  } else if (pathname.startsWith('/administration/legal')) {
    requiredRoles = [
      'admin',
      'Admin',
      'SystemAdmin',
      'SuperAdmin',
      'TenantAdmin',
    ];
  } else if (pathname.startsWith('/administration/workflow')) {
    requiredPermissions = ['procurement.workflow.configure'];
    requiredRoles = [
      'admin',
      'Admin',
      'SystemAdmin',
      'SuperAdmin',
      'TenantAdmin',
      'WorkflowAdmin',
    ];
    accessMode = 'any';
  } else if (
    /^\/administration\/project-management\/(quantity-survey-config|quantity-survey-catalogues)(\/|$)/.test(
      pathname
    )
  ) {
    requiredPermissions = ['quantity-survey.configuration.read'];
  } else if (
    /^\/administration\/project-management\/quantity-survey-rate-library(\/|$)/.test(
      pathname
    )
  ) {
    requiredPermissions = ['quantity-survey.workspace.read'];
  } else if (pathname.startsWith('/administration/project-management')) {
    requiredPermissions = ['admin.project-management'];
  } else if (
    pathname.startsWith('/administration/fleet-management') ||
    pathname.startsWith(
      '/administration/maintenance/fleet-trip-destinations'
    ) ||
    pathname.startsWith(
      '/administration/maintenance/fleet-compliance-templates'
    )
  ) {
    requiredPermissions = ['admin.fleet-management'];
  } else if (pathname.startsWith('/administration/maintenance')) {
    requiredPermissions = ['admin.maintenance'];
  } else if (pathname.startsWith('/administration/safety')) {
    // The SHE settings tree has its own admin gate (SHE Manager and the
    // administrators), separate from admin.hr — the one-admin-gate-per-module convention.
    requiredPermissions = ['admin.she'];
  } else if (pathname.startsWith('/administration/reference')) {
    // Shared cross-module reference data (administrative geography). Gated on the reference-data
    // family rather than admin.hr: the tree is not HR's, and an Estate or Sales administrator
    // curating districts should not need an HR grant. Either tier admits — admins hold both,
    // the HR role holds Write.
    requiredPermissions = [
      'Reference.Geography.Write',
      'Reference.Geography.Admin',
    ];
  } else if (pathname.startsWith('/administration/hr')) {
    // Seeded to SuperAdmin/TenantAdmin/Admin and to HR (+ legacy "HR User") — HR
    // practitioners maintain their own reference data (leave types, org structures).
    // Previously this path had no route gate at all; the sidebar was the only thing
    // hiding it, and any authenticated user could reach it by URL.
    requiredPermissions = ['admin.hr'];
  }

  return (
    <AuthGuard
      requiredPermissions={requiredPermissions}
      requiredRoles={requiredRoles}
      accessMode={accessMode}
    >
      <DashboardLayout>{children}</DashboardLayout>
    </AuthGuard>
  );
}
