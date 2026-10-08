import type { Schemas } from "@/api/client";
import { CopyButton } from "@/components/copy-button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { formatDateTime, platformLabels } from "@/lib/format";

export function ReleaseManifestsCard({ release }: { release: Schemas["ReleaseResponse"] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>External manifests</CardTitle>
        <CardDescription>
          Immutable snapshots. Download URLs point at your artifact host; checksums are served exactly as published.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {release.manifests.map((manifest) => (
          <section key={manifest.platform} className="flex flex-col gap-2 rounded-lg border p-3" aria-label={`${platformLabels[manifest.platform]} manifest`}>
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h3 className="font-medium">
                {platformLabels[manifest.platform]} <code className="text-xs text-muted-foreground">{manifest.feedFileName}</code>
              </h3>
              <span className="text-xs text-muted-foreground">fetched {formatDateTime(manifest.fetchedAt)}</span>
            </div>
            <div className="flex items-center gap-1 text-xs text-muted-foreground">
              <span className="truncate">Source: {manifest.sourceUrl}</span>
              <CopyButton value={manifest.sourceUrl} label="Copy source URL" />
            </div>
            <div className="text-xs text-muted-foreground">
              SHA-256 of snapshot: <code className="break-all">{manifest.sourceSha256}</code>
            </div>
            <ul className="flex flex-col gap-1">
              {manifest.files.map((file) => (
                <li key={file.url} className="rounded bg-muted/50 px-2 py-1 text-xs">
                  <div className="break-all">{file.url}</div>
                  <div className="text-muted-foreground">
                    sha512 <code className="break-all">{file.sha512}</code>
                    {file.size != null && <> · {file.size.toLocaleString()} bytes</>}
                  </div>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </CardContent>
    </Card>
  );
}
