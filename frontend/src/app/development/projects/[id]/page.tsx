import { redirect } from 'next/navigation';

export default async function ProjectDetailRedirectPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  redirect(`/development/projects/${id}/overview`);
}
