import { redirect } from 'next/navigation';

export default function MaintenanceDashboardPage() {
  redirect('/maintenance?view=dashboard');
}
