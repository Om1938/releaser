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
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

const schema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  kind: z.enum(["Pin", "Exclusion"]),
  audienceId: z.string().min(1, "Choose an audience."),
  releaseId: z.string().min(1, "Choose a release."),
  priority: z.number().int().min(-1000).max(1000),
});

type Values = z.infer<typeof schema>;

export function CreatePolicyDialog({ appId }: { appId: string }) {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();
  const releases = useQuery(queries.releases(appId));
  const audiences = useQuery(queries.audiences(appId));
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { name: "", kind: "Pin", audienceId: "", releaseId: "", priority: 0 } });
  const create = useMutation({
    mutationFn: (values: Values) => unwrap(api.POST("/api/admin/v1/applications/{appId}/policies", { params: { path: { appId } }, body: values })),
    onSuccess: async (policy) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`Created ${policy.kind.toLowerCase()} “${policy.name}”`);
      setOpen(false);
      form.reset();
    },
  });
  const kind = useWatch({ control: form.control, name: "kind" });
  const select = (name: "kind" | "audienceId" | "releaseId", label: string, options: { value: string; label: string }[], description?: string) => (
    <Controller
      control={form.control}
      name={name}
      render={({ field, fieldState }) => (
        <Field data-invalid={fieldState.invalid}>
          <FieldLabel htmlFor={`policy-${name}`}>{label}</FieldLabel>
          <Select value={field.value} onValueChange={field.onChange}>
            <SelectTrigger id={`policy-${name}`} aria-invalid={fieldState.invalid}>
              <SelectValue placeholder={`Choose ${label.toLowerCase()}`} />
            </SelectTrigger>
            <SelectContent>
              {options.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {description && <FieldDescription>{description}</FieldDescription>}
          {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
        </Field>
      )}
    />
  );

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus /> New policy
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={form.handleSubmit((values) => create.mutate(values))} noValidate>
          <DialogHeader>
            <DialogTitle>New policy</DialogTitle>
            <DialogDescription>Pins hold an audience on one release; exclusions keep a problematic release away from an audience.</DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            {create.isError && (
              <Alert variant="destructive">
                <AlertDescription>{errorMessage(create.error)}</AlertDescription>
              </Alert>
            )}
            <TextField control={form.control} name="name" label="Name" />
            {select("kind", "Kind", [
              { value: "Pin", label: "Pin — hold on a specific release" },
              { value: "Exclusion", label: "Exclusion — never offer a release" },
            ], kind === "Pin" ? "Pins override deployments for matching installations. Downgrades are still never offered." : "Exclusions win over pins and deployments.")}
            {select("audienceId", "Audience", (audiences.data ?? []).map((a) => ({ value: a.id, label: a.name })))}
            {select("releaseId", "Release", (releases.data ?? []).map((r) => ({ value: r.id, label: `${r.version} (${r.state})` })))}
            {kind === "Pin" && <TextField control={form.control} name="priority" label="Priority" numeric description="Breaks ties between equally specific pins." />}
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              Create policy
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
