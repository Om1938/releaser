import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { api, errorMessage, unwrap } from "@/api/client";
import { queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { FieldGroup } from "@/components/ui/field";
import { Spinner } from "@/components/ui/spinner";

const schema = z.object({
  email: z.email("Enter a valid email address."),
  password: z.string().min(1, "Enter your password."),
});

export const Route = createFileRoute("/login")({
  validateSearch: z.object({ redirect: z.string().optional() }),
  component: LoginPage,
});

function LoginPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { redirect } = Route.useSearch();
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { email: "", password: "" } });
  const login = useMutation({
    mutationFn: (values: z.infer<typeof schema>) => unwrap(api.POST("/api/admin/v1/auth/login", { body: values })),
    onSuccess: async (user) => {
      queryClient.setQueryData(queries.me().queryKey, user);
      await navigate({ to: redirect?.startsWith("/") ? redirect : "/" });
    },
  });

  return (
    <main className="flex min-h-svh items-center justify-center bg-muted p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-xl">Sign in to Releaser</CardTitle>
          <CardDescription>Publisher administrators only. Release targets never need an account.</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={form.handleSubmit((values) => login.mutate(values))} noValidate>
            <FieldGroup>
              {login.isError && (
                <Alert variant="destructive">
                  <AlertDescription>{errorMessage(login.error)}</AlertDescription>
                </Alert>
              )}
              <TextField control={form.control} name="email" label="Email" inputProps={{ type: "email", autoComplete: "username", autoFocus: true }} />
              <TextField control={form.control} name="password" label="Password" inputProps={{ type: "password", autoComplete: "current-password" }} />
              <Button type="submit" disabled={login.isPending}>
                {login.isPending && <Spinner />}
                Sign in
              </Button>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>
    </main>
  );
}
