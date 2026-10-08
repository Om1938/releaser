import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { platformLabels } from "@/lib/format";
import { allPlatforms, feedFiles, manifestUrlIn, manifestUrlSchema, type Platform } from "./platforms";

const schema = z.object({ platform: z.enum(allPlatforms), url: manifestUrlSchema });
type Values = z.infer<typeof schema>;

/** Platforms the release does not ship yet, in the standard order. */
export function missingPlatforms(release: Schemas["ReleaseResponse"]): Platform[] {
  return allPlatforms.filter((platform) => !release.platforms.includes(platform));
}

/** Folder of the release's first manifest, used to suggest where the next platform's manifest lives. */
function releaseFolder(release: Schemas["ReleaseResponse"]): string | undefined {
  const source = release.manifests[0]?.sourceUrl;
  return source ? source.slice(0, source.lastIndexOf("/")) : undefined;
}

/** Adds one more platform's manifest to an existing release (issue #8). Existing manifests stay immutable. */
export function AddPlatformDialog({ appId, release }: { appId: string; release: Schemas["ReleaseResponse"] }) {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();
  const missing = missingPlatforms(release);
  const folder = releaseFolder(release);
  const suggest = (platform: Platform) => (folder ? manifestUrlIn(folder, platform) : "");
  const initial = (): Values => ({ platform: missing[0] ?? "MacOS", url: missing[0] ? suggest(missing[0]) : "" });
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: initial() });
  const platform = useWatch({ control: form.control, name: "platform" });
  const add = useMutation({
    mutationFn: (values: Values) =>
      unwrap(
        api.POST("/api/admin/v1/applications/{appId}/releases/{releaseId}/manifests", {
          params: { path: { appId, releaseId: release.id } },
          body: values,
        }),
      ),
    onSuccess: async (_, values) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`${platformLabels[values.platform]} added to ${release.version}`, {
        description: "Matching installations on that platform are offered it within the freshness bound.",
      });
      setOpen(false);
    },
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) {
          add.reset();
          form.reset(initial());
        }
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          <Plus /> Add platform manifest
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={form.handleSubmit((values) => add.mutate(values))} noValidate>
          <DialogHeader>
            <DialogTitle>Add a platform to {release.version}</DialogTitle>
            <DialogDescription>
              Fetches and snapshots this platform's manifest. Its version must be {release.version}. Manifests already registered cannot be
              replaced.
            </DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            {add.isError && (
              <Alert variant="destructive">
                <AlertDescription>{errorMessage(add.error)}</AlertDescription>
              </Alert>
            )}
            <Controller
              control={form.control}
              name="platform"
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="add-platform">Platform</FieldLabel>
                  <Select
                    value={field.value}
                    onValueChange={(value) => {
                      field.onChange(value);
                      form.setValue("url", suggest(value as Platform), { shouldValidate: true });
                    }}
                  >
                    <SelectTrigger id="add-platform">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {missing.map((p) => (
                        <SelectItem key={p} value={p}>
                          {platformLabels[p]}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FieldDescription>Only platforms this release does not have yet.</FieldDescription>
                </Field>
              )}
            />
            <TextField control={form.control} name="url" label={`Manifest URL — ${feedFiles[platform]}`} inputProps={{ inputMode: "url" }} />
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={add.isPending}>
              {add.isPending && <Spinner />}
              Fetch manifest &amp; add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
