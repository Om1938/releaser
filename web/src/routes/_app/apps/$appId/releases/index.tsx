import { useQuery } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { Package } from "lucide-react";
import { queries } from "@/api/queries";
import { PageHeader } from "@/components/page-header";
import { QueryState } from "@/components/query-state";
import { ReleaseStateBadge } from "@/components/status-badge";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { RegisterReleaseDialog } from "@/features/releases/register-release-dialog";
import { canManageReleases } from "@/lib/auth";
import { formatDateTime, platformLabels } from "@/lib/format";

export const Route = createFileRoute("/_app/apps/$appId/releases/")({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.releases(params.appId)),
  component: ReleasesPage,
});

function ReleasesPage() {
  const { appId } = Route.useParams();
  const { user } = Route.useRouteContext();
  const releases = useQuery(queries.releases(appId));
  const canEdit = canManageReleases(user);
  return (
    <>
      <PageHeader
        title="Releases"
        description="Versions built, signed and hosted elsewhere. Registering a release never offers it — deployments do."
        actions={canEdit && <RegisterReleaseDialog appId={appId} />}
      />
      <QueryState
        query={releases}
        empty={{
          title: "No releases registered",
          description: "Reference the latest*.yml manifests your electron-builder pipeline already publishes.",
          icon: <Package />,
          action: canEdit && <RegisterReleaseDialog appId={appId} />,
        }}
      >
        {(rows) => (
          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Version</TableHead>
                  <TableHead>State</TableHead>
                  <TableHead className="hidden md:table-cell">Channels</TableHead>
                  <TableHead className="hidden lg:table-cell">Platforms</TableHead>
                  <TableHead className="hidden sm:table-cell">Notes</TableHead>
                  <TableHead className="hidden md:table-cell">Registered</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((release) => (
                  <TableRow key={release.id}>
                    <TableCell>
                      <Link to="/apps/$appId/releases/$releaseId" params={{ appId, releaseId: release.id }} className="font-medium underline-offset-4 hover:underline">
                        {release.version}
                      </Link>
                      {release.title && <div className="text-xs text-muted-foreground">{release.title}</div>}
                    </TableCell>
                    <TableCell>
                      <ReleaseStateBadge state={release.state} />
                    </TableCell>
                    <TableCell className="hidden md:table-cell">
                      <div className="flex flex-wrap gap-1">
                        {release.channels.map((channel) => (
                          <Badge key={channel} variant="outline">
                            {channel}
                          </Badge>
                        ))}
                      </div>
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground lg:table-cell">
                      {release.platforms.map((p) => platformLabels[p]).join(", ")}
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">
                      {release.hasPublishedNotes ? <Badge variant="secondary">Published</Badge> : <span className="text-sm text-muted-foreground">—</span>}
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground md:table-cell">{formatDateTime(release.registeredAt)}</TableCell>
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
