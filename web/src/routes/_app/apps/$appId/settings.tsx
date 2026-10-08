import { useQuery } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { queries } from "@/api/queries";
import { ChannelsCard } from "@/features/settings/channels-card";
import { ContextKeysCard } from "@/features/settings/context-keys-card";
import { SupportedPlatformsCard } from "@/features/settings/supported-platforms-card";
import { canManageReleases } from "@/lib/auth";

export const Route = createFileRoute("/_app/apps/$appId/settings")({
  component: SettingsPage,
});

function SettingsPage() {
  const { appId } = Route.useParams();
  const { user } = Route.useRouteContext();
  const readOnly = !canManageReleases(user);
  const application = useQuery(queries.application(appId)).data;
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      {application && <SupportedPlatformsCard key={application.supportedPlatforms.join()} application={application} readOnly={readOnly} />}
      <ChannelsCard appId={appId} readOnly={readOnly} />
      <ContextKeysCard appId={appId} readOnly={readOnly} />
    </div>
  );
}
