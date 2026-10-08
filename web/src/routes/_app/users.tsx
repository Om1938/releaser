import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute, redirect } from "@tanstack/react-router";
import { Trash2, UserPlus } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { queries } from "@/api/queries";
import { ConfirmAction } from "@/components/confirm-action";
import { TextField } from "@/components/form-fields";
import { PageHeader } from "@/components/page-header";
import { QueryState } from "@/components/query-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { canManageUsers } from "@/lib/auth";

const roles = ["Admin", "ReleaseManager", "Viewer"] as const;
const roleDescriptions: Record<(typeof roles)[number], string> = {
  Admin: "Everything, including administrator accounts.",
  ReleaseManager: "Manage applications, releases, audiences, deployments, policies and notes.",
  Viewer: "Read-only access to the dashboard and audit log.",
};

export const Route = createFileRoute("/_app/users")({
  beforeLoad: ({ context }) => {
    if (!canManageUsers((context as { user?: Schemas["CurrentUserResponse"] }).user)) {
      throw redirect({ to: "/" });
    }
  },
  component: UsersPage,
});

function RoleSelect({ value, onChange, id }: { value: string; onChange: (value: string) => void; id: string }) {
  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger id={id} className="w-44">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {roles.map((role) => (
          <SelectItem key={role} value={role}>
            {role}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

const createSchema = z.object({
  email: z.email("Enter a valid email."),
  displayName: z.string().trim().min(1, "Name is required.").max(200),
  password: z.string().min(12, "Use at least 12 characters."),
  role: z.enum(roles),
});

function CreateUserDialog() {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();
  const form = useForm<z.infer<typeof createSchema>>({ resolver: zodResolver(createSchema), defaultValues: { email: "", displayName: "", password: "", role: "Viewer" } });
  const create = useMutation({
    mutationFn: (values: z.infer<typeof createSchema>) => unwrap(api.POST("/api/admin/v1/users", { body: values })),
    onSuccess: async (user) => {
      await queryClient.invalidateQueries({ queryKey: queries.users().queryKey });
      toast.success(`Added ${user.email}`);
      setOpen(false);
      form.reset();
    },
  });
  const role = useWatch({ control: form.control, name: "role" });
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <UserPlus /> Add administrator
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={form.handleSubmit((values) => create.mutate(values))} noValidate>
          <DialogHeader>
            <DialogTitle>Add administrator</DialogTitle>
            <DialogDescription>Publisher staff only. Share the initial password through a secure channel.</DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            {create.isError && (
              <Alert variant="destructive">
                <AlertDescription>{errorMessage(create.error)}</AlertDescription>
              </Alert>
            )}
            <TextField control={form.control} name="email" label="Email" inputProps={{ type: "email", autoComplete: "off" }} />
            <TextField control={form.control} name="displayName" label="Display name" />
            <TextField control={form.control} name="password" label="Initial password" inputProps={{ type: "password", autoComplete: "new-password" }} />
            <Controller
              control={form.control}
              name="role"
              render={({ field }) => (
                <Field>
                  <FieldLabel htmlFor="new-user-role">Role</FieldLabel>
                  <RoleSelect id="new-user-role" value={field.value} onChange={field.onChange} />
                  <FieldDescription>{roleDescriptions[role]}</FieldDescription>
                </Field>
              )}
            />
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              Add administrator
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function UsersPage() {
  const users = useQuery(queries.users());
  const me = useQuery(queries.me());
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: queries.users().queryKey });
  const changeRole = useMutation({
    mutationFn: ({ user, role }: { user: Schemas["CurrentUserResponse"]; role: string }) =>
      unwrap(api.PUT("/api/admin/v1/users/{userId}", { params: { path: { userId: user.id } }, body: { displayName: user.displayName, role } })),
    onSuccess: async () => {
      await refresh();
      toast.success("Role updated");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const remove = useMutation({
    mutationFn: (userId: string) => unwrap(api.DELETE("/api/admin/v1/users/{userId}", { params: { path: { userId } } })),
    onSuccess: async () => {
      await refresh();
      toast.success("Administrator removed");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });

  return (
    <>
      <PageHeader title="Administrators" description="Accounts for publisher staff. Customers and end users never sign in here." actions={<CreateUserDialog />} />
      <QueryState query={users}>
        {(rows) => (
          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Administrator</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead className="sr-only">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((user) => (
                  <TableRow key={user.id}>
                    <TableCell>
                      <div className="font-medium">{user.displayName}</div>
                      <div className="text-xs text-muted-foreground">{user.email}</div>
                    </TableCell>
                    <TableCell>
                      <RoleSelect id={`role-${user.id}`} value={user.roles[0] ?? "Viewer"} onChange={(role) => changeRole.mutate({ user, role })} />
                    </TableCell>
                    <TableCell className="text-right">
                      {user.id !== me.data?.id && (
                        <ConfirmAction
                          trigger={
                            <Button variant="ghost" size="icon-sm" aria-label={`Remove ${user.email}`}>
                              <Trash2 />
                            </Button>
                          }
                          title={`Remove ${user.email}?`}
                          description="They will lose access immediately. Their past actions stay in the audit log."
                          confirmLabel="Remove"
                          destructive
                          onConfirm={() => remove.mutate(user.id)}
                        />
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </QueryState>
    </>
  );
}
