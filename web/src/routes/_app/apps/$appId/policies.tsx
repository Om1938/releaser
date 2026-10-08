import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { ShieldCheck, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { api, errorMessage, unwrap } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { ConfirmAction } from "@/components/confirm-action";
import { PageHeader } from "@/components/page-header";
import { QueryState } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { CreatePolicyDialog } from "@/features/policies/create-policy-dialog";
import { canManageReleases } from "@/lib/auth";
import { formatDateTime } from "@/lib/format";

export const Route = createFileRoute("/_app/apps/$appId/policies")({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.policies(params.appId)),
  component: PoliciesPage,
});

function PoliciesPage() {
  const { appId } = Route.useParams();
  const { user } = Route.useRouteContext();
  const policies = useQuery(queries.policies(appId));
  const audiences = useQuery(queries.audiences(appId));
  const queryClient = useQueryClient();
  const remove = useMutation({
    mutationFn: (policyId: string) => unwrap(api.DELETE("/api/admin/v1/applications/{appId}/policies/{policyId}", { params: { path: { appId, policyId } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success("Policy removed");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const canEdit = canManageReleases(user);
  const audienceName = (id: string) => audiences.data?.find((a) => a.id === id)?.name ?? "—";

  return (
    <>
      <PageHeader
        title="Policies"
        description="Precedence: exclusions block a release; pins override deployments; otherwise the most specific deployment wins."
        actions={canEdit && <CreatePolicyDialog appId={appId} />}
      />
      <QueryState query={policies} empty={{ title: "No policies", description: "Use a pin to hold an audience on a version, or an exclusion to keep a problematic version away.", icon: <ShieldCheck /> }}>
        {(rows) => (
          <div className="overflow-hidden rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Policy</TableHead>
                  <TableHead>Kind</TableHead>
                  <TableHead>Audience</TableHead>
                  <TableHead>Release</TableHead>
                  <TableHead className="hidden md:table-cell">Created</TableHead>
                  {canEdit && <TableHead className="sr-only">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((policy) => (
                  <TableRow key={policy.id}>
                    <TableCell className="font-medium">{policy.name}</TableCell>
                    <TableCell>
                      <Badge variant={policy.kind === "Exclusion" ? "destructive" : "secondary"}>{policy.kind}</Badge>
                    </TableCell>
                    <TableCell>{audienceName(policy.audienceId)}</TableCell>
                    <TableCell>{policy.releaseVersion}</TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground md:table-cell">{formatDateTime(policy.createdAt)}</TableCell>
                    {canEdit && (
                      <TableCell className="text-right">
                        <ConfirmAction
                          trigger={
                            <Button variant="ghost" size="icon-sm" aria-label={`Remove ${policy.name}`}>
                              <Trash2 />
                            </Button>
                          }
                          title={`Remove ${policy.name}?`}
                          description="Affected installations will be resolved by deployments again within the freshness bound."
                          confirmLabel="Remove"
                          destructive
                          onConfirm={() => remove.mutate(policy.id)}
                        />
                      </TableCell>
                    )}
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
