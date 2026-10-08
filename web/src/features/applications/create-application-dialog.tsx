import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { Plus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap } from "@/api/client";
import { queries } from "@/api/queries";
import { TextField } from "@/components/form-fields";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { FieldGroup } from "@/components/ui/field";
import { Spinner } from "@/components/ui/spinner";
import { channelKeySchema, slugSchema } from "@/features/shared/schemas";

const schema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200),
  key: slugSchema,
  description: z.string().max(2000).optional(),
  defaultChannelKey: channelKeySchema,
  defaultChannelName: z.string().trim().min(1, "Channel name is required.").max(200),
});

type Values = z.infer<typeof schema>;

export function CreateApplicationDialog() {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", key: "", description: "", defaultChannelKey: "stable", defaultChannelName: "Stable" },
  });
  const create = useMutation({
    mutationFn: (values: Values) => unwrap(api.POST("/api/admin/v1/applications", { body: { ...values, description: values.description || null } })),
    onSuccess: async (app) => {
      await queryClient.invalidateQueries({ queryKey: queries.applications().queryKey });
      toast.success(`Created ${app.name}`);
      setOpen(false);
      form.reset();
      await navigate({ to: "/apps/$appId", params: { appId: app.id } });
    },
    onError: (error) => toast.error(errorMessage(error)),
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button>
          <Plus />
          New application
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={form.handleSubmit((values) => create.mutate(values))} noValidate>
          <DialogHeader>
            <DialogTitle>New application</DialogTitle>
            <DialogDescription>Each application has its own releases, audiences and update feed.</DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            <TextField control={form.control} name="name" label="Name" inputProps={{ placeholder: "Acme Desktop" }} />
            <TextField control={form.control} name="key" label="Key" description="Stable identifier used in feed URLs. Cannot be changed later." inputProps={{ placeholder: "acme-desktop" }} />
            <TextField control={form.control} name="description" label="Description" multiline rows={2} />
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField control={form.control} name="defaultChannelKey" label="Default channel key" description="Served to electron-updater's “latest” channel." />
              <TextField control={form.control} name="defaultChannelName" label="Default channel name" />
            </div>
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Spinner />}
              Create application
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
