import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { AppWindow, History, LogOut, Rocket, UserCog, Users } from "lucide-react";
import { api, unwrap } from "@/api/client";
import { queries } from "@/api/queries";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSkeleton,
} from "@/components/ui/sidebar";
import { canManageUsers } from "@/lib/auth";

export function AppSidebar() {
  const me = useQuery(queries.me());
  const applications = useQuery(queries.applications());
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const logout = useMutation({
    mutationFn: () => unwrap(api.POST("/api/admin/v1/auth/logout")),
    onSettled: async () => {
      queryClient.clear();
      await navigate({ to: "/login" });
    },
  });

  return (
    <Sidebar>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to="/">
                <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                  <Rocket className="size-4" />
                </div>
                <div className="flex flex-col leading-tight">
                  <span className="font-semibold">Releaser</span>
                  <span className="text-xs text-muted-foreground">Release control plane</span>
                </div>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>Applications</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton asChild>
                  <Link to="/" activeOptions={{ exact: true }} activeProps={{ "data-active": true }}>
                    <AppWindow />
                    All applications
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
              {applications.isPending && <SidebarMenuSkeleton />}
              {applications.data?.map((app) => (
                <SidebarMenuItem key={app.id}>
                  <SidebarMenuButton asChild>
                    <Link to="/apps/$appId" params={{ appId: app.id }} activeProps={{ "data-active": true }} title={app.key}>
                      <span className="ml-6 truncate">{app.name}</span>
                      <span className="ml-auto truncate font-mono text-[0.7rem] text-sidebar-foreground">{app.key}</span>
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
        <SidebarGroup>
          <SidebarGroupLabel>Administration</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton asChild>
                  <Link to="/audit" activeProps={{ "data-active": true }}>
                    <History />
                    Audit log
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
              {canManageUsers(me.data) && (
                <SidebarMenuItem>
                  <SidebarMenuButton asChild>
                    <Link to="/users" activeProps={{ "data-active": true }}>
                      <Users />
                      Administrators
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              )}
              <SidebarMenuItem>
                <SidebarMenuButton asChild>
                  <Link to="/account" activeProps={{ "data-active": true }}>
                    <UserCog />
                    Account &amp; system
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <div className="flex flex-col px-2 py-1 text-xs">
              <span className="truncate font-medium">{me.data?.displayName}</span>
              <span className="truncate text-muted-foreground">{me.data?.email}</span>
            </div>
          </SidebarMenuItem>
          <SidebarMenuItem>
            <SidebarMenuButton onClick={() => logout.mutate()} disabled={logout.isPending}>
              <LogOut />
              Sign out
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
    </Sidebar>
  );
}
