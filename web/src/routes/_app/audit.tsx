import { useQuery } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { History } from "lucide-react";
import { useState } from "react";
import { queries } from "@/api/queries";
import { PageHeader } from "@/components/page-header";
import { EmptyState, QueryState } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatDateTime } from "@/lib/format";

export const Route = createFileRoute("/_app/audit")({
  component: AuditPage,
});

function AuditPage() {
  const [action, setAction] = useState("");
  const [cursors, setCursors] = useState<number[]>([]);
  const before = cursors.at(-1);
  const page = useQuery(queries.audit({ action: action || undefined, before }));
  const applications = useQuery(queries.applications());
  const appName = (id: string | null) => applications.data?.find((a) => a.id === id)?.name ?? "—";

  return (
    <>
      <PageHeader title="Audit log" description="Every consequential administrative change: who, when, what and on which entity. Entries are immutable." />
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <Input
          aria-label="Filter by action prefix"
          placeholder="Filter by action, e.g. deployment."
          value={action}
          onChange={(event) => {
            setAction(event.target.value);
            setCursors([]);
          }}
          className="sm:max-w-xs"
        />
      </div>
      <QueryState query={page}>
        {(data) =>
          data.entries.length === 0 ? (
            <EmptyState title="No audit entries" description="Nothing matches this filter yet." icon={<History />} />
          ) : (
            <div className="flex flex-col gap-3">
              <div className="overflow-hidden rounded-lg border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>When</TableHead>
                      <TableHead>Actor</TableHead>
                      <TableHead>Action</TableHead>
                      <TableHead className="hidden md:table-cell">Application</TableHead>
                      <TableHead className="hidden lg:table-cell">Details</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.entries.map((entry) => (
                      <TableRow key={entry.id}>
                        <TableCell className="text-sm whitespace-nowrap">{formatDateTime(entry.occurredAt)}</TableCell>
                        <TableCell className="text-sm">{entry.actor}</TableCell>
                        <TableCell>
                          <Badge variant="outline">{entry.action}</Badge>
                          <div className="text-xs text-muted-foreground">
                            {entry.entityType} {entry.entityId.slice(0, 8)}
                          </div>
                        </TableCell>
                        <TableCell className="hidden text-sm md:table-cell">{appName(entry.appId)}</TableCell>
                        <TableCell className="hidden max-w-sm truncate text-xs text-muted-foreground lg:table-cell" title={entry.details ?? undefined}>
                          {entry.details}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" size="sm" disabled={cursors.length === 0} onClick={() => setCursors((c) => c.slice(0, -1))}>
                  Newer
                </Button>
                <Button variant="outline" size="sm" disabled={data.nextBefore == null} onClick={() => data.nextBefore != null && setCursors((c) => [...c, Number(data.nextBefore)])}>
                  Older
                </Button>
              </div>
            </div>
          )
        }
      </QueryState>
    </>
  );
}
