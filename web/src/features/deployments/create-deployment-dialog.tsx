import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { PercentageField, percentageRule } from "./percentage-field";

const schema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  releaseId: z.string().min(1, "Choose a release."),
  channel: z.string().min(1, "Choose a channel."),
  audienceId: z.string().min(1, "Choose an audience."),
  percentage: z.number({ error: "Enter a percentage." }).refine(percentageRule.test, percentageRule.message),
  priority: z.number().int().min(-1000).max(1000),
});

type Values = z.infer<typeof schema>;

function SelectField({ id, label, value, onChange, error, options, placeholder }: {
  id: string; label: string; value: string; onChange: (value: string) => void; error?: string;
  options: { value: string; label: string }[]; placeholder: string;
}) {
  return (
    <Field data-invalid={!!error}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id={id} aria-invalid={!!error}>
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {error && <FieldError>{error}</FieldError>}
    </Field>
  );
}

export function CreateDeploymentDialog({ appId }: { appId: string }) {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();
  const releases = useQuery(queries.releases(appId));
  const audiences = useQuery(queries.audiences(appId));
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", releaseId: "", channel: "", audienceId: "", percentage: 100, priority: 0 },
  });
  const releaseId = useWatch({ control: form.control, name: "releaseId" });
  const release = releases.data?.find((r) => r.id === releaseId);
  const create = useMutation({
    mutationFn: async (values: Values) => {
      const created = await unwrap(api.POST("/api/admin/v1/applications/{appId}/deployments", { params: { path: { appId } }, body: values }));
      return created;
    },
    onSuccess: async (deployment) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`Created draft deployment “${deployment.name}”`, { description: "Activate it to start offering the release." });
      setOpen(false);
      form.reset();
    },
  });
  const errors = form.formState.errors;

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          New deployment
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-lg">
        <form onSubmit={form.handleSubmit((values) => create.mutate(values))} noValidate>
          <DialogHeader>
            <DialogTitle>New deployment</DialogTitle>
            <DialogDescription>Makes one release eligible for one audience on one channel. It starts as a draft.</DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            {create.isError && (
              <Alert variant="destructive">
                <AlertDescription>{errorMessage(create.error)}</AlertDescription>
              </Alert>
            )}
            <TextField control={form.control} name="name" label="Name" inputProps={{ placeholder: "Customer B — 1.2.0" }} />
            <Controller
              control={form.control}
              name="releaseId"
              render={({ field }) => (
                <SelectField
                  id="deployment-release"
                  label="Release"
                  placeholder="Choose an available release"
                  value={field.value}
                  onChange={(value) => {
                    field.onChange(value);
                    form.setValue("channel", releases.data?.find((r) => r.id === value)?.channels[0] ?? "");
                  }}
                  error={errors.releaseId?.message}
                  options={(releases.data ?? []).filter((r) => r.state === "Available").map((r) => ({ value: r.id, label: r.version }))}
                />
              )}
            />
            <Controller
              control={form.control}
              name="channel"
              render={({ field }) => (
                <SelectField
                  id="deployment-channel"
                  label="Channel"
                  placeholder={release ? "Choose a channel" : "Choose a release first"}
                  value={field.value}
                  onChange={field.onChange}
                  error={errors.channel?.message}
                  options={(release?.channels ?? []).map((c) => ({ value: c, label: c }))}
                />
              )}
            />
            <Controller
              control={form.control}
              name="audienceId"
              render={({ field }) => (
                <SelectField
                  id="deployment-audience"
                  label="Audience"
                  placeholder="Choose an audience"
                  value={field.value}
                  onChange={field.onChange}
                  error={errors.audienceId?.message}
                  options={(audiences.data ?? []).map((a) => ({ value: a.id, label: a.name }))}
                />
              )}
            />
            <PercentageField control={form.control} name="percentage" />
            <TextField
              control={form.control}
              name="priority"
              label="Priority"
              description="Breaks ties between deployments with equally specific audiences (higher wins)."
              numeric
            />
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Spinner />}
              Create draft
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
