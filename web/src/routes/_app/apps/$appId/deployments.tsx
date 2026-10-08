import { useQuery } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { Info, Rocket } from "lucide-react";
import { queries } from "@/api/queries";
import { PageHeader } from "@/components/page-header";
import { QueryState } from "@/components/query-state";
import { DeploymentStateBadge } from "@/components/status-badge";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { CreateDeploymentDialog } from "@/features/deployments/create-deployment-dialog";
import { DeploymentActions } from "@/features/deployments/deployment-actions";
import { canManageReleases } from "@/lib/auth";
import { formatPercent } from "@/lib/format";

export const Route = createFileRoute("/_app/apps/$appId/deployments")({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.deployments(params.appId)),
  component: DeploymentsPage,
});

function DeploymentsPage() {
  const { appId } = Route.useParams();
  const { user } = Route.useRouteContext();
  const deployments = useQuery(queries.deployments(appId));
  const system = useQuery(queries.system());
  const canEdit = canManageReleases(user);
  return (
    <>
      <PageHeader
        title="Deployments"
        description="The most specific matching audience wins; a paused deployment holds its in-cohort installations without falling through."
        actions={canEdit && <CreateDeploymentDialog appId={appId} />}
      />
      <Alert>
        <Info />
        <AlertDescription>
          Rollout percentages control <strong>eligibility</strong>, not adoption. Pause and withdrawal stop new offers on every node within{" "}
          {system.data?.freshnessSeconds ?? "a few"} seconds; they cannot undo installs that already happened.
        </AlertDescription>
      </Alert>
      <QueryState
        query={deployments}
        empty={{
          title: "No deployments",
          description: "Create an audience and register a release, then deploy it. Until then every check returns “no update”.",
          icon: <Rocket />,
          action: canEdit && <CreateDeploymentDialog appId={appId} />,
        }}
      >
        {(rows) => (
          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Deployment</TableHead>
                  <TableHead className="hidden sm:table-cell">State</TableHead>
                  <TableHead className="hidden sm:table-cell">Eligible share</TableHead>
                  <TableHead className="hidden md:table-cell">Priority</TableHead>
                  {canEdit && <TableHead className="text-right">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell>
                      <div className="font-medium">{d.name}</div>
                      <div className="text-xs text-muted-foreground">
                        {d.releaseVersion} → {d.audienceName} on <Badge variant="outline">{d.channel}</Badge>
                      </div>
                      <div className="mt-1 flex items-center gap-2 sm:hidden">
                        <DeploymentStateBadge state={d.state} />
                        <span className="text-xs tabular-nums">{formatPercent(d.percentage)} eligible</span>
                      </div>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">
                      <DeploymentStateBadge state={d.state} />
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">
                      <div className="flex items-center gap-2">
                        <Progress value={d.percentage} className="w-24" aria-label={`${d.name} eligible share`} />
                        <span className="text-sm tabular-nums">{formatPercent(d.percentage)}</span>
                      </div>
                    </TableCell>
                    <TableCell className="hidden tabular-nums md:table-cell">{d.priority}</TableCell>
                    {canEdit && (
                      <TableCell>
                        <DeploymentActions appId={appId} deployment={d} />
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </QueryState>
    </>
  );
}
