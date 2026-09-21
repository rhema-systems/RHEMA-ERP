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

  /*
   * ⚠ Recording an employee who already exists is a DIFFERENT ACT from hiring one, not a variant
   * of it. A hire is given a number by whichever rule governs their register, and the normal
   * create refuses a supplied one so a hand-typed value cannot occupy a number the counter is
   * about to issue. Someone already on the payroll arrives with a number that is printed on their
   * ID card — it is not ours to reissue, so it goes down the import path, which keeps it and
   * moves the register's counter past it.
   */
  const [importMode, setImportMode] = useState(false);

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

  const handleSubmit = async (
    values: EmployeeFormValues,
    meta: { probationIsDerived: boolean },
  ) => {
    setSubmitting(true);
    try {
      const request = employeeFormToRequest(values, {
        probationIsDerived: meta.probationIsDerived,
        importMode,
      });
      if (importMode) {
        await employeeService.importExisting(request);
      } else {
        await employeeService.create(request);
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employees'] });
      // The import moves the register's counter, so anything showing where it stands is stale.
      await queryClient.invalidateQueries({ queryKey: ['hr', 'staff-number-counter'] });
      toast({
        title: 'Success',
        description: importMode
          ? 'Employee recorded with their existing staff number.'
          : 'Employee created.',
      });
      router.push('/hr/employees');
    } catch (error: any) {
      toast({
        title: 'Error',
        description:
          error?.message ||
          (importMode ? 'Failed to record the employee.' : 'Failed to create employee.'),
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
        description="Create an employee record, or record one who already has a staff number."
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
          submitLabel={importMode ? 'Record Employee' : 'Create Employee'}
          onCancel={() => router.push('/hr/employees')}
          showNumberingRule
          isCreate
          importMode={importMode}
          onImportModeChange={setImportMode}
        />
      )}
    </div>
  );
}
