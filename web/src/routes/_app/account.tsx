import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap } from "@/api/client";
import { queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { FieldGroup } from "@/components/ui/field";

const schema = z
  .object({
    currentPassword: z.string().min(1, "Enter your current password."),
    newPassword: z.string().min(12, "Use at least 12 characters."),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ["confirmPassword"], message: "Passwords do not match." });

export const Route = createFileRoute("/_app/account")({
  component: AccountPage,
});

function AccountPage() {
  const system = useQuery(queries.system());
  const me = useQuery(queries.me());
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" } });
  const change = useMutation({
    mutationFn: (values: z.infer<typeof schema>) =>
      unwrap(api.POST("/api/admin/v1/auth/password", { body: { currentPassword: values.currentPassword, newPassword: values.newPassword } })),
    onSuccess: () => {
      toast.success("Password changed");
      form.reset();
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const facts = [
    ["Version", system.data?.version],
    ["Public base URL", system.data?.publicBaseUrl],
    ["Pause/withdraw freshness bound", system.data ? `${system.data.freshnessSeconds} s` : undefined],
    ["Distributed cache (Redis L2)", system.data ? (system.data.distributedCacheEnabled ? "Enabled" : "Not configured — local memory only") : undefined],
    ["Signed in as", me.data ? `${me.data.email} (${me.data.roles.join(", ")})` : undefined],
  ] as const;

  return (
    <>
      <PageHeader title="Account & system" description="Instance facts for operators and your own credentials." />
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>This instance</CardTitle>
            <CardDescription>PostgreSQL is the source of truth; caches never broaden eligibility.</CardDescription>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 text-sm">
              {facts.map(([label, value]) => (
                <div key={label} className="grid gap-1 sm:grid-cols-2">
                  <dt className="text-muted-foreground">{label}</dt>
                  <dd className="break-all">{value ?? "…"}</dd>
                </div>
              ))}
            </dl>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Change password</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={form.handleSubmit((values) => change.mutate(values))} noValidate>
              <FieldGroup>
                <TextField control={form.control} name="currentPassword" label="Current password" inputProps={{ type: "password", autoComplete: "current-password" }} />
                <TextField control={form.control} name="newPassword" label="New password" inputProps={{ type: "password", autoComplete: "new-password" }} />
                <TextField control={form.control} name="confirmPassword" label="Confirm new password" inputProps={{ type: "password", autoComplete: "new-password" }} />
                <Button type="submit" className="self-start" disabled={change.isPending}>
                  Change password
                </Button>
              </FieldGroup>
            </form>
          </CardContent>
        </Card>
      </div>
    </>
  );
}
