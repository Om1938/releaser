import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, createFileRoute } from "@tanstack/react-router";
import { ArrowLeft } from "lucide-react";
import { toast } from "sonner";
import { api, errorMessage, unwrap } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { ConfirmAction } from "@/components/confirm-action";
import { QueryState } from "@/components/query-state";
import { ReleaseStateBadge } from "@/components/status-badge";
import { Button } from "@/components/ui/button";
import { ReleaseNotesEditor } from "@/features/release-notes/release-notes-editor";
import { ReleaseChannelsCard } from "@/features/releases/release-channels-card";
import { ObliterateReleaseCard } from "@/features/releases/obliterate-release-card";
import { ReleaseManifestsCard } from "@/features/releases/release-manifests-card";
import { canManageReleases, canObliterateReleases } from "@/lib/auth";
import { formatDateTime } from "@/lib/format";

export const Route = createFileRoute("/_app/apps/$appId/releases/$releaseId")({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.release(params.appId, params.releaseId)),
  component: ReleasePage,
});

function ReleasePage() {
  const { appId, releaseId } = Route.useParams();
  const { user } = Route.useRouteContext();
  const release = useQuery(queries.release(appId, releaseId));
  const app = useQuery(queries.application(appId));
  const queryClient = useQueryClient();
  const transition = useMutation({
    mutationFn: (action: "deprecate" | "withdraw") =>
      unwrap(
        action === "deprecate"
          ? api.POST("/api/admin/v1/applications/{appId}/releases/{releaseId}/deprecate", { params: { path: { appId, releaseId } } })
          : api.POST("/api/admin/v1/applications/{appId}/releases/{releaseId}/withdraw", { params: { path: { appId, releaseId } } }),
      ),
    onSuccess: async (updated) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`Release ${updated.version} is now ${updated.state.toLowerCase()}`);
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const canEdit = canManageReleases(user);

  return (
    <QueryState query={release}>
      {(r) => (
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="flex flex-col gap-1">
              <Link to="/apps/$appId/releases" params={{ appId }} className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
                <ArrowLeft className="size-4" /> Releases
              </Link>
              <h2 className="flex items-center gap-2 text-xl font-semibold">
                {r.version} <ReleaseStateBadge state={r.state} />
              </h2>
              <p className="text-sm text-muted-foreground">
                {r.title ?? "Untitled"} · registered {formatDateTime(r.registeredAt)}
              </p>
            </div>
            {canEdit && r.state !== "Withdrawn" && (
              <div className="flex flex-wrap gap-2">
                {r.state === "Available" && (
                  <ConfirmAction
                    trigger={<Button variant="outline">Deprecate</Button>}
                    title={`Deprecate ${r.version}?`}
                    description="Deprecated releases are no longer offered by any deployment or pin. Installed copies are unaffected."
                    confirmLabel="Deprecate"
                    onConfirm={() => transition.mutate("deprecate")}
                  />
                )}
                <ConfirmAction
                  trigger={<Button variant="destructive">Withdraw</Button>}
                  title={`Withdraw ${r.version}?`}
                  description="Withdrawal is permanent. No new update decisions will offer this release (within the freshness bound). It cannot undo installations that already happened — ship a forward fix."
                  confirmLabel="Withdraw permanently"
                  destructive
                  onConfirm={() => transition.mutate("withdraw")}
                />
              </div>
            )}
          </div>
          <div className="grid gap-6 lg:grid-cols-3">
            <div className="flex flex-col gap-6 lg:col-span-2">
              <ReleaseNotesEditor appId={appId} releaseId={releaseId} readOnly={!canEdit} />
              <ReleaseManifestsCard appId={appId} release={r} supported={app.data?.supportedPlatforms ?? []} readOnly={!canEdit} />
            </div>
            <div className="flex flex-col gap-6 lg:self-start">
              <ReleaseChannelsCard key={r.channels.join()} appId={appId} release={r} readOnly={!canEdit} />
              {canObliterateReleases(user) && <ObliterateReleaseCard appId={appId} release={r} />}
            </div>
          </div>
        </div>
      )}
    </QueryState>
  );
}
