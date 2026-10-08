import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Wand2 } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap } from "@/api/client";
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
import { allPlatforms, feedFiles, manifestUrlIn, manifestUrlSchema, type Platform } from "@/lib/platforms";
import { PlatformCheckboxes } from "@/features/shared/platform-checkboxes";

const schema = z
  .object({
    version: semverSchema,
    title: z.string().max(200).optional(),
    channels: z.array(z.string()),
    included: z.array(z.enum(allPlatforms)),
    manifests: z.record(z.enum(allPlatforms), z.string()),
  })
  .superRefine((values, ctx) => {
    if (values.included.length === 0) {
      ctx.addIssue({ code: "custom", path: ["included"], message: "Include at least one platform. The others can be added later." });
    }
    for (const platform of values.included) {
      const result = manifestUrlSchema.safeParse(values.manifests[platform]);
      if (!result.success) {
        ctx.addIssue({ code: "custom", path: ["manifests", platform], message: result.error.issues[0]?.message ?? "Invalid URL." });
      }
    }
  });

type Values = z.infer<typeof schema>;

const emptyManifests = Object.fromEntries(allPlatforms.map((p) => [p, ""])) as Record<Platform, string>;

export function RegisterReleaseDialog({ appId }: { appId: string }) {
  const [open, setOpen] = useState(false);
  const [folderUrl, setFolderUrl] = useState("");
  const queryClient = useQueryClient();
  const channels = useQuery(queries.channels(appId));
  const app = useQuery(queries.application(appId));
  // Only the application's supported platforms are offered, all ticked by default (issue #10).
  const supported: Platform[] = app.data?.supportedPlatforms ?? [];
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    values: {
      version: "",
      title: "",
      channels: app.data ? [app.data.defaultChannel] : [],
      included: supported,
      manifests: emptyManifests,
    },
    resetOptions: { keepDirtyValues: true },
  });
  const included = useWatch({ control: form.control, name: "included" });
  const register = useMutation({
    mutationFn: (values: Values) =>
      unwrap(
        api.POST("/api/admin/v1/applications/{appId}/releases", {
          params: { path: { appId } },
          body: {
            version: values.version,
            title: values.title || null,
            channels: values.channels,
            manifests: allPlatforms.filter((p) => values.included.includes(p)).map((platform) => ({ platform, url: values.manifests[platform] ?? "" })),
          },
        }),
      ),
    onSuccess: async (release) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`Registered ${release.version}`, { description: "Nothing is offered until you deploy it. Missing platforms can be added from the release page." });
      setOpen(false);
      form.reset();
      setFolderUrl("");
    },
  });

  const fillFromFolder = () => {
    for (const platform of included) {
      form.setValue(`manifests.${platform}`, manifestUrlIn(folderUrl, platform), { shouldValidate: true, shouldDirty: true });
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
            <Controller
              name="included"
              control={form.control}
              render={({ field, fieldState }) => (
                <PlatformCheckboxes
                  legend="Platforms to register now"
                  description="Only these manifests are fetched now. Skip platforms you haven't published yet and add them to this release later from its page. The list comes from the application's supported platforms (Settings)."
                  options={supported}
                  value={field.value}
                  onChange={field.onChange}
                  idPrefix="include"
                  error={fieldState.error?.message}
                />
              )}
            />
            {included.length > 0 && (
              <FieldSet>
                <FieldLegend variant="label">Update manifests</FieldLegend>
                <FieldDescription>
                  Absolute URLs of each selected platform's latest*.yml. Relative file paths inside them are resolved against these URLs, so
                  downloads keep coming from your host.
                </FieldDescription>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Input
                    aria-label="Release folder URL"
                    placeholder="https://cdn.example.com/acme/1.2.0"
                    value={folderUrl}
                    onChange={(event) => setFolderUrl(event.target.value)}
                  />
                  <Button type="button" variant="outline" onClick={fillFromFolder} disabled={!folderUrl}>
                    <Wand2 />
                    Fill selected platforms
                  </Button>
                </div>
                {allPlatforms
                  .filter((platform) => included.includes(platform))
                  .map((platform) => (
                    <TextField
                      key={platform}
                      control={form.control}
                      name={`manifests.${platform}`}
                      label={`${platformLabels[platform]} — ${feedFiles[platform]}`}
                      inputProps={{ placeholder: "https://…", inputMode: "url" }}
                    />
                  ))}
              </FieldSet>
            )}
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
