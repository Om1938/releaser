import { Outlet, createFileRoute, redirect } from "@tanstack/react-router";
import { ApiError } from "@/api/client";
import { queries } from "@/api/queries";
import { AppSidebar } from "@/components/layout/app-sidebar";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";

/** Authenticated shell: every child route requires a signed-in administrator. */
export const Route = createFileRoute("/_app")({
  beforeLoad: async ({ context, location }) => {
    try {
      return { user: await context.queryClient.ensureQueryData(queries.me()) };
    } catch (error) {
      if (error instanceof ApiError && error.isUnauthorized) {
        throw redirect({ to: "/login", search: { redirect: location.href } });
      }
      throw error;
    }
  },
  component: AppLayout,
});

function AppLayout() {
  return (
    <SidebarProvider>
      <AppSidebar />
      <SidebarInset>
        <header className="flex h-12 items-center gap-2 border-b px-4 md:hidden">
          <SidebarTrigger />
          <span className="font-semibold">Releaser</span>
        </header>
        <main id="main" className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-6 p-4 md:p-8">
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
