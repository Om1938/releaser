import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { Pencil, Plus, Trash2, UsersRound } from "lucide-react";
import { toast } from "sonner";
import { api, errorMessage, unwrap } from "@/api/client";
import { applicationScope, queries } from "@/api/queries";
import { ConfirmAction } from "@/components/confirm-action";
import { PageHeader } from "@/components/page-header";
import { QueryState } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AudienceDialog } from "@/features/audiences/audience-dialog";
import { describeRule } from "@/features/audiences/rule-model";
import { canManageReleases } from "@/lib/auth";

export const Route = createFileRoute("/_app/apps/$appId/audiences")({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(queries.audiences(params.appId)),
  component: AudiencesPage,
});

function AudiencesPage() {
  const { appId } = Route.useParams();
  const { user } = Route.useRouteContext();
  const audiences = useQuery(queries.audiences(appId));
  const queryClient = useQueryClient();
  const remove = useMutation({
    mutationFn: (audienceId: string) => unwrap(api.DELETE("/api/admin/v1/applications/{appId}/audiences/{audienceId}", { params: { path: { appId, audienceId } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: applicationScope(appId) });
      toast.success("Audience deleted");
    },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const canEdit = canManageReleases(user);
  const createButton = canEdit && (
    <AudienceDialog
      appId={appId}
      trigger={
        <Button>
          <Plus /> New audience
        </Button>
      }
    />
  );

  return (
    <>
      <PageHeader title="Audiences" description="Who a deployment or policy applies to. Specificity: installation > user > group > customer > attribute > everyone." actions={createButton} />
      <QueryState
        query={audiences}
        empty={{ title: "No audiences", description: "Start with an “Everyone” audience for your default release, then add customers and groups.", icon: <UsersRound />, action: createButton }}
      >
        {(rows) => (
          <div className="grid gap-4 md:grid-cols-2">
            {rows.map((audience) => (
              <Card key={audience.id}>
                <CardHeader>
                  <CardTitle>{audience.name}</CardTitle>
                  {audience.description && <CardDescription>{audience.description}</CardDescription>}
                  {canEdit && (
                    <CardAction className="flex gap-1">
                      <AudienceDialog
                        appId={appId}
                        audience={audience}
                        trigger={
                          <Button variant="ghost" size="icon-sm" aria-label={`Edit ${audience.name}`}>
                            <Pencil />
                          </Button>
                        }
                      />
                      <ConfirmAction
                        trigger={
                          <Button variant="ghost" size="icon-sm" aria-label={`Delete ${audience.name}`}>
                            <Trash2 />
                          </Button>
                        }
                        title={`Delete ${audience.name}?`}
                        description="Audiences used by deployments or policies cannot be deleted."
                        confirmLabel="Delete"
                        destructive
                        onConfirm={() => remove.mutate(audience.id)}
                      />
                    </CardAction>
                  )}
                </CardHeader>
                <CardContent className="flex flex-col gap-2 text-sm">
                  <ul className="flex flex-col gap-1">
                    {audience.includes.map((rule, index) => (
                      <li key={`i${index}`}>
                        <span className="text-muted-foreground">include</span> {describeRule(rule)}
                      </li>
                    ))}
                    {audience.excludes.map((rule, index) => (
                      <li key={`e${index}`}>
                        <span className="text-destructive">exclude</span> {describeRule(rule)}
                      </li>
                    ))}
                  </ul>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </QueryState>
    </>
  );
}
