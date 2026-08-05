'use client';

import { z } from 'zod';
import { useQueryClient } from '@tanstack/react-query';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { attendanceDeviceService } from '@/services/hr/attendance-setup.service';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { ATTENDANCE_DEVICE_TYPE_OPTIONS } from '@/types/hr/attendance';
import type { StaffAttendanceDeviceSummary } from '@/types/hr/attendance';

/**
 * Registered clocking hardware. `deviceId` is the vendor's own identifier — it is what
 * incoming punches carry, so it is set once at registration and never edited afterwards
 * (the update endpoint does not accept it).
 */
const deviceSchema = z.object({
  deviceId: z.string().min(1, 'Required').max(100),
  deviceName: z.string().min(1, 'Required').max(200),
  deviceModel: z.string().max(200).optional(),
  manufacturer: z.string().max(100).optional(),
  firmwareVersion: z.string().max(100).optional(),
  deviceType: z.enum([
    'Fingerprint',
    'FaceRecognition',
    'RFIDCard',
    'Iris',
    'Palm',
    'QRCode',
    'PINPad',
    'MobileApp',
    'WebPortal',
  ]),
  locationDescription: z.string().min(1, 'Required').max(500),
  ipAddress: z.string().max(50).optional(),
  port: z.coerce.number().min(0).max(65535).optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});

type DeviceForm = z.input<typeof deviceSchema>;

const emptyDevice: DeviceForm = {
  deviceId: '',
  deviceName: '',
  deviceModel: '',
  manufacturer: '',
  firmwareVersion: '',
  deviceType: 'Fingerprint',
  locationDescription: '',
  ipAddress: '',
  port: undefined,
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function AttendanceDevicesPage() {
  const queryClient = useQueryClient();

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Attendance Devices"
        description="Biometric readers, card terminals and kiosks that feed raw punches into attendance."
        backHref="/administration/hr/attendance"
      />

      <ResourceListPanel<StaffAttendanceDeviceSummary, DeviceForm>
        title="devices"
        singular="device"
        queryKey={['hr', 'attendance-devices']}
        dialogHint="The device ID must match the identifier the hardware reports with its punches."
        list={() => attendanceDeviceService.getAll()}
        create={(values) => {
          const v = deviceSchema.parse(values);
          return attendanceDeviceService.register({
            ...v,
            deviceModel: blank(v.deviceModel),
            manufacturer: blank(v.manufacturer),
            firmwareVersion: blank(v.firmwareVersion),
            ipAddress: blank(v.ipAddress),
            port: v.port ?? null,
            notes: blank(v.notes),
            locationId: null,
          } as any);
        }}
        update={(id, values) => {
          const v = deviceSchema.parse(values);
          // deviceId, manufacturer and deviceType are omitted — the update DTO rejects them.
          return attendanceDeviceService.update(id, {
            id,
            deviceName: v.deviceName,
            deviceModel: blank(v.deviceModel),
            firmwareVersion: blank(v.firmwareVersion),
            locationId: null,
            locationDescription: v.locationDescription,
            ipAddress: blank(v.ipAddress),
            port: v.port ?? null,
            isActive: v.isActive,
            notes: blank(v.notes),
          });
        }}
        remove={(id) => attendanceDeviceService.remove(id)}
        getId={(d) => d.id}
        actions={[
          {
            label: 'Record sync',
            run: async (d) => {
              await attendanceDeviceService.recordSync(d.id, 0);
              await queryClient.invalidateQueries({ queryKey: ['hr', 'attendance-devices'] });
            },
            confirm: {
              title: 'Record a sync?',
              description:
                'Stamps the device as synced now and clears its pending-punch count. Use after a manual pull.',
            },
          },
        ]}
        columns={[
          { header: 'Device', cell: (d) => <span className="font-medium">{d.deviceName}</span> },
          { header: 'ID', cell: (d) => <span className="text-muted-foreground">{d.deviceId}</span> },
          { header: 'Type', cell: (d) => d.deviceType },
          { header: 'Location', cell: (d) => d.locationName || d.locationDescription || '—' },
          { header: 'Last sync', cell: (d) => formatDateTime(d.lastSyncDate) },
          {
            header: 'Pending',
            cell: (d) => d.pendingSyncCount ?? '—',
            className: 'text-right',
          },
          { header: 'Status', cell: (d) => <StatusBadge active={d.isActive} /> },
        ]}
        schema={deviceSchema as any}
        emptyForm={emptyDevice}
        toForm={(d) => ({
          ...emptyDevice,
          deviceId: d.deviceId,
          deviceName: d.deviceName,
          deviceType: d.deviceType,
          locationDescription: d.locationDescription,
          isActive: d.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="deviceId" label="Device ID" required />
              <TextField form={form} name="deviceName" label="Device name" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="deviceType"
                label="Type"
                required
                options={ATTENDANCE_DEVICE_TYPE_OPTIONS}
              />
              <TextField form={form} name="manufacturer" label="Manufacturer" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="deviceModel" label="Model" />
              <TextField form={form} name="firmwareVersion" label="Firmware version" />
            </FieldRow>
            <TextField
              form={form}
              name="locationDescription"
              label="Location description"
              required
              placeholder="e.g. Main gate, ground floor"
            />
            <FieldRow>
              <TextField form={form} name="ipAddress" label="IP address" />
              <NumberField form={form} name="port" label="Port" />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
