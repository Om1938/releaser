import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { channelKeySchema } from "@/features/shared/schemas";

const schema = z.object({ key: channelKeySchema, name: z.string().trim().min(1, "Name is required.").max(200) });

export function ChannelsCard({ appId, readOnly }: { appId: string; readOnly: boolean }) {
  const channels = useQuery(queries.channels(appId));
  const app = useQuery(queries.application(appId));
  const queryClient = useQueryClient();
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { key: "", name: "" } });
  const create = useMutation({
    mutationFn: (values: z.infer<typeof schema>) =>
      unwrap(api.POST("/api/admin/v1/applications/{appId}/channels", { params: { path: { appId } }, body: { ...values, description: null } })),
    onSuccess: async (channel) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`Added channel ${channel.key}`);
      form.reset();
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  return (
    <Card>
      <CardHeader>
        <CardTitle>Channels</CardTitle>
        <CardDescription>
          Logical release streams. electron-updater's default <code>latest</code> channel maps to the application's default channel; set{" "}
          <code>autoUpdater.channel</code> to use another one.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <ul className="flex flex-col divide-y">
          {channels.data?.map((channel) => (
            <li key={channel.id} className="flex items-center justify-between py-2 text-sm">
              <span>
                {channel.name} <code className="text-xs text-muted-foreground">{channel.key}</code>
              </span>
              {app.data?.defaultChannel === channel.key && <Badge variant="secondary">default</Badge>}
            </li>
          ))}
        </ul>
        {!readOnly && (
          <form onSubmit={form.handleSubmit((values) => create.mutate(values))} className="grid items-end gap-3 sm:grid-cols-[1fr_1fr_auto]" noValidate>
            <TextField control={form.control} name="key" label="Key" inputProps={{ placeholder: "beta" }} />
            <TextField control={form.control} name="name" label="Name" inputProps={{ placeholder: "Beta" }} />
            <Button type="submit" disabled={create.isPending}>
              Add channel
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  );
}
