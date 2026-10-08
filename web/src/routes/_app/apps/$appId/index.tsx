import { useQuery } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { queries } from "@/api/queries";
import { CopyButton } from "@/components/copy-button";
import { DeploymentStateBadge } from "@/components/status-badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatPercent } from "@/lib/format";

export const Route = createFileRoute("/_app/apps/$appId/")({
  component: OverviewPage,
});

function OverviewPage() {
  const { appId } = Route.useParams();
  const app = useQuery(queries.application(appId)).data;
  const system = useQuery(queries.system()).data;
  const releases = useQuery(queries.releases(appId)).data ?? [];
  const deployments = useQuery(queries.deployments(appId)).data ?? [];
  const live = deployments.filter((d) => d.state === "Active" || d.state === "Paused" || d.state === "Completed");
  const feedUrl = `${system?.publicBaseUrl ?? window.location.origin}/u/${app?.key ?? "<app-key>"}/\${installationId}/\${app.getVersion()}/`;
  const snippet = `import { autoUpdater } from "electron-updater";

autoUpdater.setFeedURL({
  provider: "generic",
  url: \`${feedUrl}\`,
});
// Optional: publisher-signed identity context for customer/user/group targeting
autoUpdater.requestHeaders = { Authorization: \`Bearer \${contextToken}\` };
await autoUpdater.checkForUpdatesAndNotify();`;

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Card>
        <CardHeader>
          <CardDescription>Registered releases</CardDescription>
          <CardTitle className="text-3xl tabular-nums">{releases.length}</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          {releases.filter((r) => r.state === "Available").length} available ·{" "}
          <Link to="/apps/$appId/releases" params={{ appId }} className="underline underline-offset-4">
            manage
          </Link>
        </CardContent>
      </Card>
      <Card className="lg:col-span-2">
        <CardHeader>
          <CardTitle>Live deployments</CardTitle>
          <CardDescription>Percentages are eligibility to be offered an update, not adoption.</CardDescription>
        </CardHeader>
        <CardContent>
          {live.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nothing is deployed yet. Every update check currently returns “no update”.</p>
          ) : (
            <ul className="flex flex-col divide-y">
              {live.map((d) => (
                <li key={d.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                  <span className="font-medium">{d.name}</span>
                  <span className="flex items-center gap-2 text-muted-foreground">
                    {d.releaseVersion} → {d.audienceName} · {formatPercent(d.percentage)} <DeploymentStateBadge state={d.state} />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
      <Card className="lg:col-span-3">
        <CardHeader>
          <CardTitle>Connect electron-updater</CardTitle>
          <CardDescription>
            Point the generic provider at this instance at runtime. The installation id and current version travel in the URL path, never to
            your artifact host. Downloads go straight from the app to your existing CDN.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div className="flex items-center gap-2 rounded-md border bg-muted/50 px-3 py-2">
            <code className="flex-1 overflow-x-auto text-xs whitespace-nowrap">{feedUrl}</code>
            <CopyButton value={feedUrl} label="Copy feed URL" />
          </div>
          <div className="relative">
            <pre className="overflow-x-auto rounded-md border bg-muted/50 p-3 text-xs">
              <code>{snippet}</code>
            </pre>
            <div className="absolute top-2 right-2">
              <CopyButton value={snippet} label="Copy snippet" />
            </div>
          </div>
          <p className="text-xs text-muted-foreground">
            See docs/integration-electron.md for identity-context tokens and supported platforms.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
