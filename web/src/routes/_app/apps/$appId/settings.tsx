import { createFileRoute } from "@tanstack/react-router";
import { ChannelsCard } from "@/features/settings/channels-card";
import { ContextKeysCard } from "@/features/settings/context-keys-card";
import { canManageReleases } from "@/lib/auth";

export const Route = createFileRoute("/_app/apps/$appId/settings")({
  component: SettingsPage,
});

function SettingsPage() {
  const { appId } = Route.useParams();
  const { user } = Route.useRouteContext();
  const readOnly = !canManageReleases(user);
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <ChannelsCard appId={appId} readOnly={readOnly} />
      <ContextKeysCard appId={appId} readOnly={readOnly} />
    </div>
  );
}
