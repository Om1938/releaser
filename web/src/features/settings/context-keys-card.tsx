import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { KeyRound } from "lucide-react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { ConfirmAction } from "@/components/confirm-action";
import { TextField } from "@/components/form-fields";
import { EmptyState } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatDateTime } from "@/lib/format";

const schema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  publicKeyPem: z
    .string()
    .trim()
    .startsWith("-----BEGIN PUBLIC KEY-----", "Paste a PEM public key (BEGIN PUBLIC KEY).")
    .refine((pem) => !pem.includes("PRIVATE KEY"), "Never paste a private key."),
});

/** ES256 public keys used to verify publisher-signed identity-context tokens (ADR 0004). */
export function ContextKeysCard({ appId, readOnly }: { appId: string; readOnly: boolean }) {
  const keys = useQuery(queries.contextKeys(appId));
  const queryClient = useQueryClient();
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { name: "", publicKeyPem: "" } });
  const refresh = () => queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
  const register = useMutation({
    mutationFn: (values: z.infer<typeof schema>) => unwrap(api.POST("/api/admin/v1/applications/{appId}/context-keys", { params: { path: { appId } }, body: values })),
    onSuccess: async () => {
      await refresh();
      toast.success("Context key registered");
      form.reset();
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const revoke = useMutation({
    mutationFn: (keyId: string) => unwrap(api.POST("/api/admin/v1/applications/{appId}/context-keys/{keyId}/revoke", { params: { path: { appId, keyId } } })),
    onSuccess: async () => {
      await refresh();
      toast.success("Key revoked");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Identity context keys</CardTitle>
        <CardDescription>
          Your backend signs short-lived ES256 tokens (claims <code>tid</code>, <code>sub</code>, <code>grp</code>, <code>aud</code> = application key). Only
          public keys are stored here. Without a valid token, only installation and default targeting apply.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {keys.data?.length === 0 && <EmptyState title="No keys" description="Customer, user and group targeting needs a registered key." icon={<KeyRound />} />}
        <ul className="flex flex-col divide-y">
          {keys.data?.map((key) => (
            <li key={key.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
              <span>
                <span className="font-medium">{key.name}</span> <span className="text-muted-foreground">· added {formatDateTime(key.createdAt)}</span>
              </span>
              {key.revokedAt ? (
                <Badge variant="destructive">revoked</Badge>
              ) : (
                !readOnly && (
                  <ConfirmAction
                    trigger={
                      <Button variant="outline" size="sm">
                        Revoke
                      </Button>
                    }
                    title={`Revoke ${key.name}?`}
                    description="Tokens signed with this key stop being trusted within the freshness bound."
                    confirmLabel="Revoke"
                    destructive
                    onConfirm={() => revoke.mutate(key.id)}
                  />
                )
              )}
            </li>
          ))}
        </ul>
        {!readOnly && (
          <form onSubmit={form.handleSubmit((values) => register.mutate(values))} className="flex flex-col gap-3" noValidate>
            <TextField control={form.control} name="name" label="Key name" inputProps={{ placeholder: "Production backend" }} />
            <TextField control={form.control} name="publicKeyPem" label="Public key (PEM)" multiline rows={5} />
            <Button type="submit" className="self-start" disabled={register.isPending}>
              Register key
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  );
}
