import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Wand2 } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel, FieldLegend, FieldSet } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { semverSchema } from "@/features/shared/schemas";
import { platformLabels } from "@/lib/format";

const platforms = ["Windows", "MacOS", "LinuxX64", "LinuxArm64", "LinuxArmv7l"] as const satisfies readonly Schemas["PlatformTarget"][];
const feedFiles: Record<Schemas["PlatformTarget"], string> = {
  Windows: "latest.yml",
  MacOS: "latest-mac.yml",
  LinuxX64: "latest-linux.yml",
  LinuxArm64: "latest-linux-arm64.yml",
  LinuxArmv7l: "latest-linux-armv7l.yml",
};

const optionalUrl = z.union([z.literal(""), z.url({ protocol: /^https?$/, error: "Use an absolute http(s) URL." })]);

const schema = z
  .object({
    version: semverSchema,
    title: z.string().max(200).optional(),
    channels: z.array(z.string()),
    manifests: z.object(Object.fromEntries(platforms.map((p) => [p, optionalUrl])) as Record<Schemas["PlatformTarget"], typeof optionalUrl>),
  })
  .refine((values) => Object.values(values.manifests).some(Boolean), { path: ["manifests"], message: "Reference at least one platform manifest." });

type Values = z.infer<typeof schema>;

export function RegisterReleaseDialog({ appId }: { appId: string }) {
  const [open, setOpen] = useState(false);
  const [baseUrl, setBaseUrl] = useState("");
  const queryClient = useQueryClient();
  const channels = useQuery(queries.channels(appId));
  const app = useQuery(queries.application(appId));
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      version: "",
      title: "",
      channels: app.data ? [app.data.defaultChannel] : [],
      manifests: Object.fromEntries(platforms.map((p) => [p, ""])) as Values["manifests"],
    },
  });
  const register = useMutation({
    mutationFn: (values: Values) =>
      unwrap(
        api.POST("/api/admin/v1/applications/{appId}/releases", {
          params: { path: { appId } },
          body: {
            version: values.version,
            title: values.title || null,
            channels: values.channels,
            manifests: platforms.filter((p) => values.manifests[p]).map((platform) => ({ platform, url: values.manifests[platform] })),
          },
        }),
      ),
    onSuccess: async (release) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`Registered ${release.version}`, { description: "Nothing is offered until you deploy it." });
      setOpen(false);
      form.reset();
    },
  });

  const fillFromBase = () => {
    const base = baseUrl.replace(/\/+$/, "");
    for (const platform of platforms.slice(0, 4)) {
      form.setValue(`manifests.${platform}`, `${base}/${feedFiles[platform]}`, { shouldValidate: true });
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          Register release
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-2xl">
        <form onSubmit={form.handleSubmit((values) => register.mutate(values))} noValidate>
          <DialogHeader>
            <DialogTitle>Register an externally hosted release</DialogTitle>
            <DialogDescription>
              Reference the electron-builder update manifests you already publish. The platform snapshots the metadata once; your CDN keeps
              serving every binary.
            </DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            {register.isError && (
              <Alert variant="destructive">
                <AlertDescription>{errorMessage(register.error)}</AlertDescription>
              </Alert>
            )}
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField control={form.control} name="version" label="Version" inputProps={{ placeholder: "1.2.0" }} />
              <TextField control={form.control} name="title" label="Title (optional)" />
            </div>
            <Controller
              name="channels"
              control={form.control}
              render={({ field }) => (
                <FieldSet>
                  <FieldLegend variant="label">Channels</FieldLegend>
                  <FieldDescription>Assigning a channel makes the release deployable there. It is not offered yet.</FieldDescription>
                  <div className="flex flex-wrap gap-4">
                    {channels.data?.map((channel) => (
                      <Field key={channel.id} orientation="horizontal" className="w-auto">
                        <Checkbox
                          id={`channel-${channel.key}`}
                          checked={field.value.includes(channel.key)}
                          onCheckedChange={(checked) =>
                            field.onChange(checked ? [...field.value, channel.key] : field.value.filter((key) => key !== channel.key))
                          }
                        />
                        <FieldLabel htmlFor={`channel-${channel.key}`} className="font-normal">
                          {channel.name} <code className="text-xs text-muted-foreground">{channel.key}</code>
                        </FieldLabel>
                      </Field>
                    ))}
                  </div>
                </FieldSet>
              )}
            />
            <FieldSet>
              <FieldLegend variant="label">Update manifests</FieldLegend>
              <FieldDescription>Absolute URLs of each platform's latest*.yml. Relative file paths inside them are resolved against these URLs.</FieldDescription>
              <div className="flex flex-col gap-2 sm:flex-row">
                <Input
                  aria-label="Release folder URL"
                  placeholder="https://cdn.example.com/acme/1.2.0"
                  value={baseUrl}
                  onChange={(event) => setBaseUrl(event.target.value)}
                />
                <Button type="button" variant="outline" onClick={fillFromBase} disabled={!baseUrl}>
                  <Wand2 />
                  Fill standard file names
                </Button>
              </div>
              {platforms.map((platform) => (
                <TextField
                  key={platform}
                  control={form.control}
                  name={`manifests.${platform}`}
                  label={`${platformLabels[platform]} — ${feedFiles[platform]}`}
                  inputProps={{ placeholder: "https://…", inputMode: "url" }}
                />
              ))}
              {form.formState.errors.manifests?.root?.message && (
                <p className="text-sm text-destructive">{form.formState.errors.manifests.root.message}</p>
              )}
              {form.formState.errors.manifests?.message && <p className="text-sm text-destructive">{form.formState.errors.manifests.message}</p>}
            </FieldSet>
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={register.isPending}>
              {register.isPending && <Spinner />}
              Fetch manifests &amp; register
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
