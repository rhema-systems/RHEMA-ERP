'use client';

// This layout allows public access to the /register page for new user self-registration.
// Authentication checks for sub-routes like /register/business-partner are handled
// in their own layout files.

export default function RegisterLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // Simply render children - no authentication check needed here
  // The main /register page is for public self-registration
  return <>{children}</>;
}

