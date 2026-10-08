import { useQuery } from "@tanstack/react-query";
import { Link, Outlet, createFileRoute } from "@tanstack/react-router";
import { queries } from "@/api/queries";
import { ErrorState, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";

const sections = [
  { to: "/apps/$appId", label: "Overview", exact: true },
  { to: "/apps/$appId/releases", label: "Releases", exact: false },
  { to: "/apps/$appId/deployments", label: "Deployments", exact: false },
  { to: "/apps/$appId/audiences", label: "Audiences", exact: false },
  { to: "/apps/$appId/policies", label: "Policies", exact: false },
  { to: "/apps/$appId/explain", label: "Decision explainer", exact: false },
  { to: "/apps/$appId/settings", label: "Settings", exact: false },
] as const;

export const Route = createFileRoute("/_app/apps/$appId")({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.application(params.appId)),
  pendingComponent: () => <LoadingRows rows={6} />,
  errorComponent: ({ error }) => <ErrorState error={error} />,
  component: ApplicationLayout,
});

function ApplicationLayout() {
  const { appId } = Route.useParams();
  const application = useQuery(queries.application(appId));
  const app = application.data;
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-2xl font-semibold tracking-tight">{app?.name}</h1>
          <code className="rounded bg-muted px-1.5 py-0.5 text-xs">{app?.key}</code>
          <Badge variant="outline">config v{app?.configVersion}</Badge>
        </div>
        <nav aria-label="Application sections" className="-mx-1 flex gap-1 overflow-x-auto border-b">
          {sections.map((section) => (
            <Link
              key={section.to}
              to={section.to}
              params={{ appId }}
              activeOptions={{ exact: section.exact }}
              className="border-b-2 border-transparent px-3 py-2 text-sm whitespace-nowrap text-muted-foreground transition-colors hover:text-foreground"
              activeProps={{ className: "border-primary! text-foreground! font-medium", "aria-current": "page" }}
            >
              {section.label}
            </Link>
          ))}
        </nav>
      </div>
      <Outlet />
    </div>
  );
}
