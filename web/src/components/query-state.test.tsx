import type { UseQueryResult } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/api/client";
import { QueryState } from "./query-state";

function query<T>(state: Partial<UseQueryResult<T>>): UseQueryResult<T> {
  return { isPending: false, isError: false, data: undefined, error: null, refetch: vi.fn(), ...state } as unknown as UseQueryResult<T>;
}

describe("QueryState", () => {
  it("announces loading", () => {
    render(<QueryState query={query({ isPending: true })}>{() => "data"}</QueryState>);
    expect(screen.getByRole("status", { name: "Loading" })).toBeInTheDocument();
  });

  it("shows the API problem message and retries", async () => {
    const refetch = vi.fn();
    render(
      <QueryState query={query({ isError: true, error: new ApiError(409, "Release 1.2.0 is withdrawn."), refetch })}>{() => "data"}</QueryState>,
    );
    expect(screen.getByText("Release 1.2.0 is withdrawn.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(refetch).toHaveBeenCalledOnce();
  });

  it("renders the empty state for an empty list", () => {
    render(
      <QueryState query={query({ data: [] })} empty={{ title: "No deployments", description: "Nothing is offered." }}>
        {() => "rows"}
      </QueryState>,
    );
    expect(screen.getByText("No deployments")).toBeInTheDocument();
    expect(screen.queryByText("rows")).not.toBeInTheDocument();
  });

  it("renders children with data", () => {
    render(<QueryState query={query({ data: ["a"] })}>{(rows) => `rows: ${rows.length}`}</QueryState>);
    expect(screen.getByText("rows: 1")).toBeInTheDocument();
  });
});
