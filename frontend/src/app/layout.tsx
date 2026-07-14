import type { Metadata, Viewport } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import "@syncfusion/ej2-react-pdfviewer/styles/material.css";
import "leaflet/dist/leaflet.css";
import { ReactQueryProvider } from "../lib/react-query";
import { TenantProvider } from "../contexts/TenantContext";
import { SessionBlacklistProvider } from "../contexts/SessionBlacklistContext";
import { ThemeProvider } from "../contexts/ThemeContext";
import { Toaster } from "../components/ui/toaster";
import { Toaster as SonnerToaster } from "sonner";
import { PWAInit } from "../components/PWAInit";
import { NotificationProvider } from "../contexts/NotificationContext";
import { NotificationToast } from "../components/notifications/NotificationToast";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  applicationName: "Rhema ERP",
  title: "Rhema ERP - Enterprise Resource Planning",
  description: "Enterprise resource planning with mobile fleet and maintenance inspections",
  manifest: "/manifest.json",
  appleWebApp: {
    capable: true,
    statusBarStyle: "default",
    title: "Rhema Mobile"
  },
  icons: {
    icon: "/favicon.ico",
    apple: "/icon-192.png"
  }
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
  themeColor: "#f8fafc",
};

// Force dynamic rendering for all pages — this ERP app requires authentication
// and cannot be statically pre-rendered at build time.
export const dynamic = 'force-dynamic';

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className={inter.className} suppressHydrationWarning>
        <ThemeProvider
          attribute="class"
          defaultTheme="system"
          enableSystem
          disableTransitionOnChange
        >
          <ReactQueryProvider>
            <SessionBlacklistProvider>
              <TenantProvider>
                <NotificationProvider>
                  {children}
                  <Toaster />
                  <SonnerToaster position="top-right" richColors />
                  <NotificationToast position="top-right" />
                  <PWAInit />
                </NotificationProvider>
              </TenantProvider>
            </SessionBlacklistProvider>
          </ReactQueryProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
