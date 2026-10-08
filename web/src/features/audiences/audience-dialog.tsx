import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2 } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Controller, useFieldArray, useForm, type Control, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel, FieldLegend, FieldSet } from "@/components/ui/field";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { platformLabels } from "@/lib/format";
import { emptyRule, ruleDraftSchema, ruleKindHints, ruleKindLabels, ruleKinds, toApiRule, toDraft, type RuleDraft } from "./rule-model";

const schema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  description: z.string().max(2000).optional(),
  includes: z.array(ruleDraftSchema).min(1, "Add at least one include rule."),
  excludes: z.array(ruleDraftSchema),
});

type Values = z.infer<typeof schema>;
type RuleListName = "includes" | "excludes";

function RuleEditor({ control, list, index, rule, onRemove }: { control: Control<Values>; list: RuleListName; index: number; rule: RuleDraft; onRemove: () => void }) {
  const prefix = `${list}.${index}` as const;
  return (
    <div className="flex flex-col gap-3 rounded-lg border p-3">
      <div className="flex items-start gap-2">
        <Controller
          control={control}
          name={`${prefix}.kind`}
          render={({ field }) => (
            <Field className="flex-1">
              <FieldLabel htmlFor={`${prefix}-kind`}>Match</FieldLabel>
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id={`${prefix}-kind`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ruleKinds.map((kind) => (
                    <SelectItem key={kind} value={kind}>
                      {ruleKindLabels[kind]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FieldDescription>{ruleKindHints[rule.kind]}</FieldDescription>
            </Field>
          )}
        />
        <Button type="button" variant="ghost" size="icon" className="mt-6" onClick={onRemove} aria-label={`Remove ${list === "includes" ? "include" : "exclude"} rule ${index + 1}`}>
          <Trash2 />
        </Button>
      </div>
      {rule.kind === "attribute" && <TextField control={control} name={`${prefix}.name`} label="Attribute name" inputProps={{ placeholder: "plan" }} />}
      {["customer", "group", "user", "installation", "attribute"].includes(rule.kind) && (
        <TextField control={control} name={`${prefix}.values`} label="Values" description="One per line or comma separated." multiline rows={3} />
      )}
      {rule.kind === "platform" && (
        <Controller
          control={control}
          name={`${prefix}.platforms`}
          render={({ field, fieldState }) => (
            <FieldSet>
              <FieldLegend variant="label">Platforms</FieldLegend>
              <div className="flex flex-wrap gap-4">
                {(Object.keys(platformLabels) as Schemas["PlatformTarget"][]).map((platform) => (
                  <Field key={platform} orientation="horizontal" className="w-auto">
                    <Checkbox
                      id={`${prefix}-${platform}`}
                      checked={field.value.includes(platform)}
                      onCheckedChange={(checked) => field.onChange(checked ? [...field.value, platform] : field.value.filter((p) => p !== platform))}
                    />
                    <FieldLabel htmlFor={`${prefix}-${platform}`} className="font-normal">
                      {platformLabels[platform]}
                    </FieldLabel>
                  </Field>
                ))}
              </div>
              {fieldState.error && <p className="text-sm text-destructive">{fieldState.error.message}</p>}
            </FieldSet>
          )}
        />
      )}
      {rule.kind === "currentVersion" && (
        <div className="grid gap-3 sm:grid-cols-2">
          <TextField control={control} name={`${prefix}.minimum`} label="Minimum (inclusive)" inputProps={{ placeholder: "1.0.0" }} />
          <TextField control={control} name={`${prefix}.maximumExclusive`} label="Maximum (exclusive)" inputProps={{ placeholder: "1.2.0" }} />
        </div>
      )}
    </div>
  );
}

function RuleList({ control, list, title, description, rules }: { control: Control<Values>; list: RuleListName; title: string; description: string; rules: RuleDraft[] }) {
  const array = useFieldArray({ control, name: list });
  return (
    <FieldSet>
      <FieldLegend variant="label">{title}</FieldLegend>
      <FieldDescription>{description}</FieldDescription>
      {array.fields.map((item, index) => (
        <RuleEditor key={item.id} control={control} list={list} index={index} rule={rules[index] ?? emptyRule()} onRemove={() => array.remove(index)} />
      ))}
      <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => array.append(emptyRule(list === "includes" ? "customer" : "installation"))}>
        <Plus /> Add {list === "includes" ? "include" : "exclude"} rule
      </Button>
    </FieldSet>
  );
}

export function AudienceDialog({ appId, audience, trigger }: { appId: string; audience?: Schemas["AudienceResponse"]; trigger: ReactNode }) {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();
  const initial: Values = {
    name: audience?.name ?? "",
    description: audience?.description ?? "",
    includes: audience?.includes.map(toDraft) ?? [emptyRule()],
    excludes: audience?.excludes.map(toDraft) ?? [],
  };
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: initial });
  const save = useMutation({
    mutationFn: (values: Values) => {
      const body = { name: values.name, description: values.description || null, includes: values.includes.map(toApiRule), excludes: values.excludes.map(toApiRule) };
      return unwrap(
        audience
          ? api.PUT("/api/admin/v1/applications/{appId}/audiences/{audienceId}", { params: { path: { appId, audienceId: audience.id } }, body })
          : api.POST("/api/admin/v1/applications/{appId}/audiences", { params: { path: { appId } }, body }),
      );
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`Saved audience “${saved.name}”`);
      setOpen(false);
      if (!audience) {
        form.reset(initial);
      }
    },
  });
  const values = useWatch({ control: form.control }) as Values;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) form.reset(initial);
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-2xl">
        <form onSubmit={form.handleSubmit((v) => save.mutate(v))} noValidate>
          <DialogHeader>
            <DialogTitle>{audience ? `Edit ${audience.name}` : "New audience"}</DialogTitle>
            <DialogDescription>
              Release targets are your customers, users, groups and installations — they never get accounts here. Identity rules only match
              verified context tokens.
            </DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            {save.isError && (
              <Alert variant="destructive">
                <AlertDescription>{errorMessage(save.error)}</AlertDescription>
              </Alert>
            )}
            <TextField control={form.control} name="name" label="Name" inputProps={{ placeholder: "Customer B" }} />
            <TextField control={form.control} name="description" label="Description" multiline rows={2} />
            <RuleList control={form.control} list="includes" title="Include when any of these match" description="The most specific matching rule decides precedence between overlapping deployments." rules={values.includes} />
            {form.formState.errors.includes?.message && <p className="text-sm text-destructive">{form.formState.errors.includes.message}</p>}
            <RuleList control={form.control} list="excludes" title="Exclude when any of these match" description="Exclusions always win over includes." rules={values.excludes} />
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Spinner />}
              Save audience
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
