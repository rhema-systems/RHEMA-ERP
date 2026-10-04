import { redirect } from 'next/navigation';

export default function LegacySecuritySettingsPage() {
  redirect('/administration/security/dashboard?tab=settings&section=appearance');
}
