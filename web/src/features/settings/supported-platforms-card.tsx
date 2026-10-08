import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { PlatformCheckboxes } from "@/features/shared/platform-checkboxes";
import { platformLabels } from "@/lib/format";
import type { Platform } from "@/lib/platforms";

/** Edits which platforms the application ships to (issue #10). Dropping one stops new offers to it. */
export function SupportedPlatformsCard({ application, readOnly }: { application: Schemas["ApplicationResponse"]; readOnly: boolean }) {
  const [selected, setSelected] = useState<Platform[]>(application.supportedPlatforms);
  const releases = useQuery(queries.releases(application.id));
  const queryClient = useQueryClient();
  const save = useMutation({
    mutationFn: () =>
      unwrap(
        // Dedicated endpoint: only the platform set is sent, so concurrent edits of name/description/channel are never overwritten.
        api.PUT("/api/admin/v1/applications/{appId}/supported-platforms", {
          params: { path: { appId: application.id } },
          body: { supportedPlatforms: selected },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(application.id) });
      await queryClient.invalidateQueries({ queryKey: queries.applications().queryKey });
      toast.success("Supported platforms updated");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const removed = application.supportedPlatforms.filter((platform) => !selected.includes(platform));
  const stillShipped = removed.filter((platform) =>
    (releases.data ?? []).some((release) => release.state !== "Withdrawn" && release.platforms.includes(platform)),
  );
  const changed = selected.join() !== application.supportedPlatforms.join();

  return (
    <Card>
      <CardHeader>
        <CardTitle>Supported platforms</CardTitle>
        <CardDescription>Only these platforms can be registered on releases and offered to installations.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <fieldset disabled={readOnly} className="contents">
          <PlatformCheckboxes
            legend="Platforms"
            value={selected}
            onChange={setSelected}
            idPrefix="supported-platform"
            error={selected.length === 0 ? "Choose at least one platform." : undefined}
          />
        </fieldset>
        {stillShipped.length > 0 && (
          <Alert>
            <AlertDescription>
              Releases still carry {stillShipped.map((p) => platformLabels[p]).join(", ")} manifests. After saving, those installations stop
              receiving new offers within the freshness bound. Installed versions are not affected.
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
      {!readOnly && (
        <CardFooter>
          <Button size="sm" disabled={!changed || selected.length === 0 || save.isPending} onClick={() => save.mutate()}>
            Save platforms
          </Button>
        </CardFooter>
      )}
    </Card>
  );
}
