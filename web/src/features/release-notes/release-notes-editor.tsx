import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Eye, Plus, Trash2 } from "lucide-react";
import { useEffect } from "react";
import { Controller, useFieldArray, useForm, useWatch } from "react-hook-form";
import Markdown from "react-markdown";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { FieldGroup, FieldLegend, FieldSet } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { formatDateTime } from "@/lib/format";

const categories = ["Added", "Changed", "Fixed", "Removed", "Deprecated", "Security"] as const satisfies readonly Schemas["ChangeCategory"][];

const schema = z.object({
  title: z.string().trim().min(1, "A title is required.").max(200),
  summary: z.string().max(1000).optional(),
  bodyMarkdown: z.string().max(50_000),
  changes: z.array(z.object({ category: z.enum(categories), text: z.string().trim().min(1, "Describe the change.").max(1000) })).max(200),
});

type Values = z.infer<typeof schema>;

function toValues(note: Schemas["ReleaseNoteResponse"] | null | undefined): Values {
  return {
    title: note?.title ?? "",
    summary: note?.summary ?? "",
    bodyMarkdown: note?.bodyMarkdown ?? "",
    changes: note?.changes.map((c) => ({ category: c.category, text: c.text })) ?? [],
  };
}

/** Drafts, publishes and reviews revisions of one release's notes. Publication is independent of deployment. */
export function ReleaseNotesEditor({ appId, releaseId, readOnly }: { appId: string; releaseId: string; readOnly: boolean }) {
  const queryClient = useQueryClient();
  const note = useQuery(queries.releaseNote(appId, releaseId));
  const revisions = useQuery(queries.releaseNoteRevisions(appId, releaseId));
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: toValues(null) });
  const changes = useFieldArray({ control: form.control, name: "changes" });
  useEffect(() => form.reset(toValues(note.data)), [note.data, form]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
  const save = useMutation({
    mutationFn: (values: Values) =>
      unwrap(
        api.PUT("/api/admin/v1/applications/{appId}/releases/{releaseId}/notes", {
          params: { path: { appId, releaseId } },
          body: { ...values, summary: values.summary || null },
        }),
      ),
    onSuccess: async () => {
      await refresh();
      toast.success("Release notes saved");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const changeState = useMutation({
    mutationFn: (action: "publish" | "unpublish") =>
      unwrap(
        action === "publish"
          ? api.POST("/api/admin/v1/applications/{appId}/releases/{releaseId}/notes/publish", { params: { path: { appId, releaseId } } })
          : api.POST("/api/admin/v1/applications/{appId}/releases/{releaseId}/notes/unpublish", { params: { path: { appId, releaseId } } }),
      ),
    onSuccess: async (result) => {
      await refresh();
      toast.success(result.state === "Published" ? "Notes published" : "Notes moved back to draft");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const preview = useWatch({ control: form.control }) as Values;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          Release notes
          {note.data ? <Badge variant={note.data.state === "Published" ? "default" : "outline"}>{note.data.state}</Badge> : <Badge variant="outline">Not started</Badge>}
          {note.data && <span className="text-xs font-normal text-muted-foreground">revision {note.data.revision}</span>}
        </CardTitle>
        <CardDescription>Applications fetch published notes from the client API; published notes also appear in electron-updater's releaseNotes.</CardDescription>
      </CardHeader>
      <CardContent>
        <Tabs defaultValue="edit">
          <TabsList>
            <TabsTrigger value="edit">Edit</TabsTrigger>
            <TabsTrigger value="preview">
              <Eye /> Preview
            </TabsTrigger>
            <TabsTrigger value="history">History</TabsTrigger>
          </TabsList>
          <TabsContent value="edit">
            <form id="release-notes-form" onSubmit={form.handleSubmit((values) => save.mutate(values))} noValidate>
              <fieldset disabled={readOnly} className="contents">
                <FieldGroup className="pt-4">
                  <TextField control={form.control} name="title" label="Title" />
                  <TextField control={form.control} name="summary" label="Summary" multiline rows={2} />
                  <TextField control={form.control} name="bodyMarkdown" label="Body (Markdown)" multiline rows={8} />
                  <FieldSet>
                    <FieldLegend variant="label">Categorized changes</FieldLegend>
                    {changes.fields.map((item, index) => (
                      <div key={item.id} className="flex flex-col gap-2 sm:flex-row sm:items-start">
                        <Controller
                          control={form.control}
                          name={`changes.${index}.category`}
                          render={({ field }) => (
                            <Select value={field.value} onValueChange={field.onChange}>
                              <SelectTrigger className="sm:w-36" aria-label={`Change ${index + 1} category`}>
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {categories.map((category) => (
                                  <SelectItem key={category} value={category}>
                                    {category}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          )}
                        />
                        <Controller
                          control={form.control}
                          name={`changes.${index}.text`}
                          render={({ field, fieldState }) => (
                            <div className="flex-1">
                              <Input {...field} aria-label={`Change ${index + 1} description`} aria-invalid={fieldState.invalid} />
                              {fieldState.error && <p className="mt-1 text-sm text-destructive">{fieldState.error.message}</p>}
                            </div>
                          )}
                        />
                        <Button type="button" variant="ghost" size="icon" onClick={() => changes.remove(index)} aria-label={`Remove change ${index + 1}`}>
                          <Trash2 />
                        </Button>
                      </div>
                    ))}
                    <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => changes.append({ category: "Fixed", text: "" })}>
                      <Plus /> Add change
                    </Button>
                  </FieldSet>
                </FieldGroup>
              </fieldset>
            </form>
          </TabsContent>
          <TabsContent value="preview">
            <article className="prose prose-sm flex max-w-none flex-col gap-3 pt-4 dark:prose-invert">
              <h2 className="text-lg font-semibold">{preview.title || "Untitled"}</h2>
              {preview.summary && <p className="text-muted-foreground">{preview.summary}</p>}
              <div className="[&_a]:underline [&_li]:ml-4 [&_ul]:list-disc">
                <Markdown>{preview.bodyMarkdown}</Markdown>
              </div>
              {categories
                .filter((category) => preview.changes.some((c) => c.category === category))
                .map((category) => (
                  <section key={category}>
                    <h3 className="font-medium">{category}</h3>
                    <ul className="ml-4 list-disc text-sm">
                      {preview.changes
                        .filter((c) => c.category === category)
                        .map((c, index) => (
                          <li key={index}>{c.text}</li>
                        ))}
                    </ul>
                  </section>
                ))}
            </article>
          </TabsContent>
          <TabsContent value="history">
            {revisions.data?.length ? (
              <ol className="flex flex-col divide-y pt-2">
                {revisions.data.map((revision) => (
                  <li key={revision.revision} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                    <span>
                      Revision {revision.revision} · <span className="font-medium">{revision.title}</span>
                    </span>
                    <span className="flex items-center gap-2 text-muted-foreground">
                      <Badge variant="outline">{revision.state}</Badge>
                      {revision.actor ?? "system"} · {formatDateTime(revision.recordedAt)}
                    </span>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="pt-4 text-sm text-muted-foreground">No revisions yet.</p>
            )}
          </TabsContent>
        </Tabs>
      </CardContent>
      {!readOnly && (
        <CardFooter className="flex flex-wrap gap-2">
          <Button type="submit" form="release-notes-form" disabled={save.isPending}>
            Save draft
          </Button>
          {note.data?.state === "Published" ? (
            <Button variant="outline" onClick={() => changeState.mutate("unpublish")} disabled={changeState.isPending}>
              Unpublish
            </Button>
          ) : (
            <Button variant="outline" onClick={() => changeState.mutate("publish")} disabled={!note.data || changeState.isPending || form.formState.isDirty}>
              Publish
            </Button>
          )}
        </CardFooter>
      )}
    </Card>
  );
}
