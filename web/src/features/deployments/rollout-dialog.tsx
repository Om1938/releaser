import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { FieldGroup } from "@/components/ui/field";
import { PercentageField, percentageRule } from "./percentage-field";

const schema = z.object({ percentage: z.number().refine(percentageRule.test, percentageRule.message) });

export function RolloutDialog({ appId, deployment, trigger }: { appId: string; deployment: Schemas["DeploymentResponse"]; trigger: ReactNode }) {
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), values: { percentage: deployment.percentage } });
  const change = useMutation({
    mutationFn: ({ percentage }: z.infer<typeof schema>) =>
      unwrap(
        api.PUT("/api/admin/v1/applications/{appId}/deployments/{deploymentId}/rollout", {
          params: { path: { appId, deploymentId: deployment.id } },
          body: { percentage, priority: deployment.priority },
        }),
      ),
    onSuccess: async (updated) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`“${updated.name}” rollout set to ${updated.percentage}%`);
      setOpen(false);
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent>
        <form onSubmit={form.handleSubmit((values) => change.mutate(values))} noValidate>
          <DialogHeader>
            <DialogTitle>Adjust rollout — {deployment.name}</DialogTitle>
            <DialogDescription>
              Changes reach every node within the configured freshness bound. No binaries are rebuilt or re-hosted.
            </DialogDescription>
          </DialogHeader>
          <FieldGroup className="py-4">
            <PercentageField control={form.control} name="percentage" />
          </FieldGroup>
          <DialogFooter>
            <Button type="submit" disabled={change.isPending}>
              Save rollout
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
