import { useQuery } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { AppWindow, ChevronRight } from "lucide-react";
import { queries } from "@/api/queries";
import { PageHeader } from "@/components/page-header";
import { QueryState } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { CreateApplicationDialog } from "@/features/applications/create-application-dialog";
import { canManageReleases } from "@/lib/auth";

export const Route = createFileRoute("/_app/")({
  loader: ({ context }) => context.queryClient.ensureQueryData(queries.applications()),
  component: ApplicationsPage,
});

function ApplicationsPage() {
  const applications = useQuery(queries.applications());
  const { user } = Route.useRouteContext();
  const canCreate = canManageReleases(user);
  return (
    <>
      <PageHeader
        title="Applications"
        description="Externally built Electron applications whose updates this instance controls. Builds and binaries stay on your own hosting."
        actions={canCreate && <CreateApplicationDialog />}
      />
      <QueryState
        query={applications}
        empty={{
          title: "No applications yet",
          description: "Register your first application, then reference its externally hosted electron-builder releases.",
          icon: <AppWindow />,
          action: canCreate && <CreateApplicationDialog />,
        }}
      >
        {(apps) => (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {apps.map((app) => (
              <Link key={app.id} to="/apps/$appId" params={{ appId: app.id }} className="group rounded-xl focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none">
                <Card className="h-full transition-colors group-hover:bg-muted/50">
                  <CardHeader>
                    <CardTitle className="flex items-center justify-between gap-2">
                      <span className="truncate">{app.name}</span>
                      <ChevronRight className="size-4 text-muted-foreground" />
                    </CardTitle>
                    <CardDescription className="flex flex-wrap items-center gap-2">
                      <code className="text-xs">{app.key}</code>
                      <Badge variant="outline">default: {app.defaultChannel}</Badge>
                    </CardDescription>
                    {app.description && <p className="line-clamp-2 text-sm text-muted-foreground">{app.description}</p>}
                  </CardHeader>
                </Card>
              </Link>
            ))}
          </div>
        )}
      </QueryState>
    </>
  );
}
