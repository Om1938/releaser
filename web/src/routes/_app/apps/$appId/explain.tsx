import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { CheckCircle2, CircleSlash } from "lucide-react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { PageHeader } from "@/components/page-header";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { semverSchema, splitList } from "@/features/shared/schemas";
import { platformLabels } from "@/lib/format";

const schema = z.object({
  channel: z.string(),
  platform: z.enum(["Windows", "MacOS", "LinuxX64", "LinuxArm64", "LinuxArmv7l"]),
  currentVersion: semverSchema,
  installationId: z.union([z.literal(""), z.string().regex(/^[A-Za-z0-9._-]{8,128}$/, "8-128 letters, digits, '.', '_' or '-'.")]),
  customerId: z.string(),
  userId: z.string(),
  groups: z.string(),
});

type Values = z.infer<typeof schema>;

export const Route = createFileRoute("/_app/apps/$appId/explain")({
  component: ExplainPage,
});

function ExplainPage() {
  const { appId } = Route.useParams();
  const channels = useQuery(queries.channels(appId));
  const app = useQuery(queries.application(appId));
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { channel: "", platform: "Windows", currentVersion: "1.0.0", installationId: "", customerId: "", userId: "", groups: "" },
  });
  const explain = useMutation({
    mutationFn: (values: Values) =>
      unwrap(
        api.POST("/api/admin/v1/applications/{appId}/decisions/explain", {
          params: { path: { appId } },
          body: {
            channel: values.channel || null,
            platform: values.platform,
            currentVersion: values.currentVersion,
            installationId: values.installationId || null,
            customerId: values.customerId || null,
            userId: values.userId || null,
            groups: splitList(values.groups),
            attributes: null,
          },
        }),
      ),
  });

  return (
    <>
      <PageHeader
        title="Decision explainer"
        description="Ask what a hypothetical installation would receive and why — no device inventory required. Identity fields are treated as verified."
      />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Installation context</CardTitle>
            <CardDescription>Uses the same cached snapshot and resolver as the live update feed.</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={form.handleSubmit((values) => explain.mutate(values))} noValidate>
              <FieldGroup>
                <div className="grid gap-4 sm:grid-cols-2">
                  <Controller
                    control={form.control}
                    name="channel"
                    render={({ field }) => (
                      <Field>
                        <FieldLabel htmlFor="explain-channel">Channel</FieldLabel>
                        <Select value={field.value || "__default"} onValueChange={(v) => field.onChange(v === "__default" ? "" : v)}>
                          <SelectTrigger id="explain-channel">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="__default">Default ({app.data?.defaultChannel})</SelectItem>
                            {channels.data?.map((c) => (
                              <SelectItem key={c.id} value={c.key}>
                                {c.name}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </Field>
                    )}
                  />
                  <Controller
                    control={form.control}
                    name="platform"
                    render={({ field }) => (
                      <Field>
                        <FieldLabel htmlFor="explain-platform">Platform</FieldLabel>
                        <Select value={field.value} onValueChange={field.onChange}>
                          <SelectTrigger id="explain-platform">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {(Object.keys(platformLabels) as Schemas["PlatformTarget"][]).map((p) => (
                              <SelectItem key={p} value={p}>
                                {platformLabels[p]}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </Field>
                    )}
                  />
                </div>
                <div className="grid gap-4 sm:grid-cols-2">
                  <TextField control={form.control} name="currentVersion" label="Current version" />
                  <TextField control={form.control} name="installationId" label="Installation id" description="Needed for partial rollouts." />
                </div>
                <div className="grid gap-4 sm:grid-cols-2">
                  <TextField control={form.control} name="customerId" label="Customer id" />
                  <TextField control={form.control} name="userId" label="User id" />
                </div>
                <TextField control={form.control} name="groups" label="Groups" description="Comma separated." />
                <Button type="submit" disabled={explain.isPending} className="self-start">
                  Explain decision
                </Button>
              </FieldGroup>
            </form>
          </CardContent>
        </Card>
        <div aria-live="polite">
          {explain.isError && (
            <Alert variant="destructive">
              <AlertDescription>{errorMessage(explain.error)}</AlertDescription>
            </Alert>
          )}
          {explain.data && <DecisionResult decision={explain.data} />}
          {!explain.data && !explain.isError && (
            <Card className="h-full border-dashed">
              <CardContent className="flex h-full items-center justify-center py-12 text-sm text-muted-foreground">The decision and its reasoning appear here.</CardContent>
            </Card>
          )}
        </div>
      </div>
    </>
  );
}

function DecisionResult({ decision }: { decision: Schemas["ExplainDecisionResponse"] }) {
  return (
    <Card>
      <CardHeader>
        <Alert className="border-none p-0">
          {decision.updateOffered ? <CheckCircle2 className="text-emerald-600 dark:text-emerald-400" /> : <CircleSlash className="text-muted-foreground" />}
          <AlertTitle className="text-base">{decision.updateOffered ? `Offered ${decision.offeredVersion}` : "No update offered"}</AlertTitle>
          <AlertDescription>
            Reason: <code>{decision.reason}</code> · channel {decision.channel} · config v{decision.configVersion}
            {decision.deploymentName && <> · deployment “{decision.deploymentName}”</>}
            {decision.pinName && <> · pin “{decision.pinName}”</>}
          </AlertDescription>
        </Alert>
      </CardHeader>
      <CardContent>
        <ol className="flex list-decimal flex-col gap-2 pl-5 text-sm">
          {decision.trace.map((step, index) => (
            <li key={index}>{step}</li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}
