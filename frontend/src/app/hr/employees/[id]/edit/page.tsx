'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  EmployeeForm,
  type EmployeeFormValues,
} from '@/components/hr/employee/EmployeeForm';
import { employeeFormToRequest } from '@/components/hr/employee/employeeFormMapper';
import { employeeService } from '@/services/hr/employee.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { locationService } from '@/services/hr/location.service';
import { locationLevelService } from '@/services/hr/location-level.service';
import type { EmployeeDetail } from '@/types/hr/employee';

function toFormValues(d: EmployeeDetail): EmployeeFormValues {
  return {
    employeeNumber: d.employeeNumber ?? '',
    firstName: d.firstName,
    middleName: d.middleName ?? '',
    lastName: d.lastName,
    title: d.title ?? '',
    gender: d.gender ?? '',
    dateOfBirth: d.dateOfBirth ?? '',
    maritalStatus: d.maritalStatus ?? '',
    religion: d.religion ?? '',
    // ⚠ Read from /details, never the summary: GET api/hr/Employees/{id} is
    // GetEmployeeSummaryByIdAsync and carries none of these, so binding the form to it would
    // render every one blank and blank them on save.
    genderDescription: d.genderDescription ?? '',
    hometown: d.hometown ?? '',
    hasDisability: d.hasDisability ?? false,
    disabilityDescription: d.disabilityDescription ?? '',
    bloodType: d.bloodType ?? '',
    isExpatriate: d.isExpatriate,
    emailAddress: d.emailAddress,
    mobileNumber: d.mobileNumber ?? '',
    telephoneNumber: d.telephoneNumber ?? '',
    address: d.address ?? '',
    city: d.city ?? '',
    state: d.state ?? '',
    postalCode: d.postalCode ?? '',
    digitalAddress: d.digitalAddress ?? '',
    positionId: d.positionId,
    organizationUnitId: d.organizationUnitId ?? '',
    locationId: d.locationId ?? '',
    managerId: d.managerId ?? '',
    employmentType: d.employmentType,
    staffStatus: d.staffStatus,
    dateEmployed: d.dateEmployed ?? '',
    probationPeriodDays: d.probationPeriodDays,
    isFullTime: d.isFullTime,
    salary: d.salary != null ? String(d.salary) : '',
    taxNumber: d.taxNumber ?? '',
    socialSecurityNumber: d.socialSecurityNumber ?? '',
    tinNumber: d.tinNumber ?? '',
    payTax: d.payTax,
    ssFund: d.ssFund,
    grossUp: d.grossUp,
    tier2Only: d.tier2Only,
    overtime: d.overtime,
    badgeNumber: d.badgeNumber ?? '',
    notes: d.notes ?? '',
  };
}

export default function EditEmployeePage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: employee, isLoading, isError } = useQuery({
    queryKey: ['hr', 'employees', id, 'details'],
    queryFn: () => employeeService.getDetails(id),
    enabled: !!id,
  });

  const { data: positions } = useQuery({
    queryKey: ['hr', 'employee-positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });

  const { data: orgLevels } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const { data: locations } = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });

  const { data: locationLevels } = useQuery({
    queryKey: ['hr', 'location-levels', 'all'],
    queryFn: () => locationLevelService.getAll(),
  });

  // Seed the manager picker label from the current manager, if any.
  const { data: manager } = useQuery({
    queryKey: ['hr', 'employees', employee?.managerId, 'lookup'],
    queryFn: () => employeeService.getById(employee?.managerId as string),
    enabled: !!employee?.managerId,
  });

  const handleSubmit = async (values: EmployeeFormValues) => {
    setSubmitting(true);
    try {
      await employeeService.update(id, employeeFormToRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employees'] });
      toast({ title: 'Success', description: 'Employee updated.' });
      router.push(`/hr/employees/${id}`);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update employee.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-6xl mx-auto">
      <PageHeader
        title="Edit Employee"
        description={employee ? employee.fullName : 'Update this employee record.'}
        backHref={id ? `/hr/employees/${id}` : '/hr/employees'}
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : isError || !employee ? (
        <EmptyState title="Employee not found" description="This employee may have been deleted." />
      ) : (
        <EmployeeForm
          positions={positions ?? []}
          orgLevels={orgLevels ?? []}
          locations={locations ?? []}
          locationLevels={locationLevels ?? []}
          defaultValues={toFormValues(employee)}
          initialManagerLabel={manager?.fullName ?? null}
          initialLocationLevelId={employee.locationLevelId ?? null}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Save Changes"
          onCancel={() => router.push(`/hr/employees/${id}`)}
        />
      )}
    </div>
  );
}
