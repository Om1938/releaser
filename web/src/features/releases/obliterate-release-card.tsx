import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { TriangleAlert } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { LoadingRows } from "@/components/query-state";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { platformLabels } from "@/lib/format";

function ImpactList({ impact }: { impact: Schemas["ObliterationImpact"] }) {
  const live = impact.deployments.filter((d) => d.isLive);
  return (
    <div className="flex flex-col gap-3 text-sm">
      {live.length > 0 && (
        <Alert variant="destructive">
          <TriangleAlert />
          <AlertTitle>{live.length === 1 ? "1 live deployment" : `${live.length} live deployments`} offering {impact.version} right now</AlertTitle>
          <AlertDescription>{live.map((d) => `${d.name} (${d.state.toLowerCase()}, ${d.audienceName})`).join(", ")}</AlertDescription>
        </Alert>
      )}
      <p className="font-medium">This permanently deletes:</p>
      <ul className="ml-5 list-disc space-y-1">
        <li>Release {impact.version} and its manifests for {impact.platforms.map((p) => platformLabels[p]).join(", ")}</li>
        <li>{impact.deployments.length === 1 ? "1 deployment" : `${impact.deployments.length} deployments`}</li>
        <li>
          {impact.pins} pin{impact.pins === 1 ? "" : "s"} and {impact.exclusions} exclusion{impact.exclusions === 1 ? "" : "s"} for this release
        </li>
        <li>{impact.hasReleaseNote ? `Release notes and ${impact.releaseNoteRevisions} revisions` : "No release notes"}</li>
      </ul>
      <p className="text-muted-foreground">
        The audit log keeps a record of what was deleted. Installations that already received {impact.version} keep it. If you register {impact.version}{" "}
        again with different files, they won't be offered it, because it's the same version to them. If anything was already shipped, prefer a new version.
      </p>
    </div>
  );
}

/** Admin-only, irreversible removal of a release so its version can be registered again (issue #12). */
export function ObliterateReleaseCard({ appId, release }: { appId: string; release: Schemas["ReleaseResponse"] }) {
  const [open, setOpen] = useState(false);
  const [typed, setTyped] = useState("");
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const impact = useQuery({ ...queries.obliterationImpact(appId, release.id), enabled: open });
  const obliterate = useMutation({
    mutationFn: () =>
      unwrap(
        api.DELETE("/api/admin/v1/applications/{appId}/releases/{releaseId}", {
          params: { path: { appId, releaseId: release.id }, query: { confirmVersion: typed.trim() } },
        }),
      ),
    onSuccess: async () => {
      setOpen(false);
      toast.success(`Release ${release.version} was obliterated`, { description: "The version can be registered again." });
      await navigate({ to: "/apps/$appId/releases", params: { appId } });
      queryClient.removeQueries({ queryKey: queries.release(appId, release.id).queryKey });
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
    },
  });
  const confirmed = typed.trim() === release.version;

  return (
    <Card className="border-destructive/40">
      <CardHeader>
        <CardTitle>Danger zone</CardTitle>
        <CardDescription>Made a mistake when registering? Obliterate the release to free its version. This cannot be undone.</CardDescription>
      </CardHeader>
      <CardContent>
        <Dialog
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            setTyped("");
            obliterate.reset();
          }}
        >
          <DialogTrigger asChild>
            <Button variant="destructive">Obliterate release…</Button>
          </DialogTrigger>
          <DialogContent className="max-h-[90svh] overflow-y-auto">
            <form
              onSubmit={(event) => {
                event.preventDefault();
                if (confirmed) obliterate.mutate();
              }}
            >
              <DialogHeader>
                <DialogTitle>Obliterate release {release.version}?</DialogTitle>
                <DialogDescription>Everything attached to this release is deleted permanently, and offers for it stop.</DialogDescription>
              </DialogHeader>
              <div className="flex flex-col gap-4 py-4">
                {impact.isPending && <LoadingRows rows={3} />}
                {impact.isError && (
                  <Alert variant="destructive">
                    <AlertDescription>{errorMessage(impact.error)}</AlertDescription>
                  </Alert>
                )}
                {impact.data && <ImpactList impact={impact.data} />}
                {obliterate.isError && (
                  <Alert variant="destructive">
                    <AlertDescription>{errorMessage(obliterate.error)}</AlertDescription>
                  </Alert>
                )}
                <Field>
                  <FieldLabel htmlFor="obliterate-confirm">Type {release.version} to confirm</FieldLabel>
                  <Input id="obliterate-confirm" autoComplete="off" value={typed} onChange={(event) => setTyped(event.target.value)} />
                  <FieldDescription>Only admins can do this.</FieldDescription>
                </Field>
              </div>
              <DialogFooter>
                <Button
                  type="submit"
                  variant="destructive"
                  // Solid, not the theme's tinted destructive: the final irreversible action must not look disabled when it is enabled.
                  className="bg-destructive text-white hover:bg-destructive/90 disabled:bg-destructive/40"
                  disabled={!confirmed || obliterate.isPending || !impact.data}
                >
                  {obliterate.isPending && <Spinner />}
                  Obliterate {release.version}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </CardContent>
    </Card>
  );
}
