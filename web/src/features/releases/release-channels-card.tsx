import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldLabel } from "@/components/ui/field";

/** Promotes or demotes a release between channels without rebuilding anything. */
export function ReleaseChannelsCard({ appId, release, readOnly }: { appId: string; release: Schemas["ReleaseResponse"]; readOnly: boolean }) {
  const channels = useQuery(queries.channels(appId));
  const [selected, setSelected] = useState<string[]>(release.channels);
  const queryClient = useQueryClient();
  const save = useMutation({
    mutationFn: () =>
      unwrap(
        api.PUT("/api/admin/v1/applications/{appId}/releases/{releaseId}/channels", {
          params: { path: { appId, releaseId: release.id } },
          body: { channels: selected },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success("Channels updated");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const changed = [...selected].sort().join() !== [...release.channels].sort().join();
  const disabled = readOnly || release.state === "Withdrawn";

  return (
    <Card>
      <CardHeader>
        <CardTitle>Channels</CardTitle>
        <CardDescription>Promotion reuses the same external artifacts — no rebuild or re-upload.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {channels.data?.map((channel) => (
          <Field key={channel.id} orientation="horizontal">
            <Checkbox
              id={`release-channel-${channel.key}`}
              disabled={disabled}
              checked={selected.includes(channel.key)}
              onCheckedChange={(checked) => setSelected((current) => (checked ? [...current, channel.key] : current.filter((key) => key !== channel.key)))}
            />
            <FieldLabel htmlFor={`release-channel-${channel.key}`} className="font-normal">
              {channel.name} <code className="text-xs text-muted-foreground">{channel.key}</code>
            </FieldLabel>
          </Field>
        ))}
      </CardContent>
      {!disabled && (
        <CardFooter>
          <Button size="sm" disabled={!changed || save.isPending} onClick={() => save.mutate()}>
            Save channels
          </Button>
        </CardFooter>
      )}
    </Card>
  );
}
