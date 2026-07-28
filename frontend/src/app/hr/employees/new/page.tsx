'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  EmployeeForm,
  emptyEmployee,
  type EmployeeFormValues,
} from '@/components/hr/employee/EmployeeForm';
import { employeeFormToRequest } from '@/components/hr/employee/employeeFormMapper';
import { employeeService } from '@/services/hr/employee.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import { locationService } from '@/services/hr/location.service';
import { locationLevelService } from '@/services/hr/location-level.service';

export default function NewEmployeePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [submitting, setSubmitting] = useState(false);

  const { data: positions, isLoading: positionsLoading } = useQuery({
    queryKey: ['hr', 'employee-positions', 'active'],
    queryFn: () => employeePositionService.getActive(),
  });

  const { data: orgLevels } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
  });

  const { data: locations, isLoading: locationsLoading } = useQuery({
    queryKey: ['hr', 'locations', 'all'],
    queryFn: () => locationService.getAll(),
  });

  const { data: locationLevels } = useQuery({
    queryKey: ['hr', 'location-levels', 'all'],
    queryFn: () => locationLevelService.getAll(),
  });

  const handleSubmit = async (values: EmployeeFormValues) => {
    setSubmitting(true);
    try {
      await employeeService.create(employeeFormToRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employees'] });
      toast({ title: 'Success', description: 'Employee created.' });
      router.push('/hr/employees');
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create employee.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 p-6 max-w-6xl mx-auto">
      <PageHeader
        title="New Employee"
        description="Create an employee record."
        backHref="/hr/employees"
      />

      {positionsLoading || locationsLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <EmployeeForm
          positions={positions ?? []}
          orgLevels={orgLevels ?? []}
          locations={locations ?? []}
          locationLevels={locationLevels ?? []}
          defaultValues={emptyEmployee}
          onSubmit={handleSubmit}
          submitting={submitting}
          submitLabel="Create Employee"
          onCancel={() => router.push('/hr/employees')}
        />
      )}
    </div>
  );
}
