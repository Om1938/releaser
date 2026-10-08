import { useMutation, useQueryClient } from "@tanstack/react-query";
import { MoreHorizontal } from "lucide-react";
import { toast } from "sonner";
import { api, errorMessage, unwrap, type Schemas } from "@/api/client";
import { applicationScope } from "@/api/queries";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { RolloutDialog } from "./rollout-dialog";

type Verb = "activate" | "pause" | "resume" | "complete" | "cancel";

const transitions: Record<Schemas["DeploymentState"], Verb[]> = {
  Draft: ["activate", "cancel"],
  Active: ["pause", "complete", "cancel"],
  Paused: ["resume", "cancel"],
  Completed: [],
  Cancelled: [],
};

const labels: Record<Verb, string> = {
  activate: "Activate",
  pause: "Pause offers",
  resume: "Resume",
  complete: "Mark completed",
  cancel: "Cancel deployment",
};

const paths = {
  activate: "/api/admin/v1/applications/{appId}/deployments/{deploymentId}/activate",
  pause: "/api/admin/v1/applications/{appId}/deployments/{deploymentId}/pause",
  resume: "/api/admin/v1/applications/{appId}/deployments/{deploymentId}/resume",
  complete: "/api/admin/v1/applications/{appId}/deployments/{deploymentId}/complete",
  cancel: "/api/admin/v1/applications/{appId}/deployments/{deploymentId}/cancel",
} as const;

/** Lifecycle actions allowed from the deployment's current state (ADR 0005). */
export function DeploymentActions({ appId, deployment }: { appId: string; deployment: Schemas["DeploymentResponse"] }) {
  const queryClient = useQueryClient();
  const transition = useMutation({
    mutationFn: (verb: Verb) => unwrap(api.POST(paths[verb], { params: { path: { appId, deploymentId: deployment.id } } })),
    onSuccess: async (updated) => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success(`“${updated.name}” is now ${updated.state.toLowerCase()}`);
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const verbs = transitions[deployment.state];
  const editable = deployment.state !== "Completed" && deployment.state !== "Cancelled";
  if (!editable) {
    return null;
  }
  return (
    <div className="flex justify-end gap-1">
      {deployment.state === "Active" && (
        <Button variant="outline" size="sm" onClick={() => transition.mutate("pause")} disabled={transition.isPending}>
          Pause
        </Button>
      )}
      {deployment.state === "Paused" && (
        <Button variant="outline" size="sm" onClick={() => transition.mutate("resume")} disabled={transition.isPending}>
          Resume
        </Button>
      )}
      {deployment.state === "Draft" && (
        <Button size="sm" onClick={() => transition.mutate("activate")} disabled={transition.isPending}>
          Activate
        </Button>
      )}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon-sm" aria-label={`More actions for ${deployment.name}`}>
            <MoreHorizontal />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <RolloutDialog appId={appId} deployment={deployment} trigger={<DropdownMenuItem onSelect={(event) => event.preventDefault()}>Adjust rollout…</DropdownMenuItem>} />
          <DropdownMenuSeparator />
          {verbs.map((verb) => (
            <DropdownMenuItem key={verb} variant={verb === "cancel" ? "destructive" : "default"} onSelect={() => transition.mutate(verb)}>
              {labels[verb]}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
