import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { ReactQueryProvider } from "../lib/react-query";
import { TenantProvider } from "../contexts/TenantContext";
import { SessionBlacklistProvider } from "../contexts/SessionBlacklistContext";
import { ThemeProvider } from "../contexts/ThemeContext";
import { Toaster } from "../components/ui/toaster";
import { PWAInit } from "../components/PWAInit";
import { NotificationProvider } from "../contexts/NotificationContext";
import { NotificationToast } from "../components/notifications/NotificationToast";

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "ERP System - Enterprise Resource Planning",
  description: "Modern enterprise resource planning system",
  manifest: "/manifest.json",
  themeColor: "#000000",
  viewport: "width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no",
  appleWebApp: {
    capable: true,
    statusBarStyle: "default",
    title: "ERP System"
  },
  icons: {
    icon: "/favicon.ico",
    apple: "/icon-192.png"
  }
};

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
