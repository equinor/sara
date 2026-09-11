import { QueryClient, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getConfiguredAnalyses } from "./client";

export type PagedResource =
  | "inspection-records"
  | "analyses"
  | "analysis-groups"
  | "analysis-runs"
  | "workflows"
  | "feedback";

export const pagedListsKey = ["sara", "lists"] as const;

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 0,
        gcTime: 5 * 60 * 1000,
        retry: false,
        refetchOnWindowFocus: false,
      },
    },
  });
}

export function useConfiguredAnalyses() {
  return useQuery({
    queryKey: ["sara", "config", "analyses"],
    queryFn: ({ signal }) => getConfiguredAnalyses(signal),
    staleTime: 5 * 60 * 1000,
    gcTime: 30 * 60 * 1000,
  });
}

export function useResourceDetail<T>(
  resource: PagedResource | "reference-images",
  id: string | undefined,
  fetcher: (id: string, signal?: AbortSignal) => Promise<T>,
) {
  return useQuery<T | null>({
    queryKey: ["sara", "details", resource, id],
    queryFn: ({ signal }) => {
      if (!id) throw new Error("Missing resource ID");
      return fetcher(id, signal);
    },
    enabled: Boolean(id),
  });
}

export function useResourceMutation<TData, TVariables>(
  mutationFn: (variables: TVariables) => Promise<TData>,
  operation: "write" | "delete" = "write",
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    // Match direct fetch behavior: fail offline rather than queue a write for later.
    networkMode: "always",
    retry: false,
    // Writes can change embedded runs, parent counts, summaries and chart details.
    onSuccess: async () => {
      // Deletions can cascade. Clear detail data without removing active query
      // observers, which would recreate and fetch the deleted resource on render.
      if (operation === "delete") {
        const details = { queryKey: ["sara", "details"] };
        await queryClient.cancelQueries(details);
        queryClient.setQueriesData(details, null);
        await queryClient.invalidateQueries({ ...details, refetchType: "none" });
      }
      return queryClient.invalidateQueries({
        queryKey: ["sara"],
        predicate: (query) => query.queryKey[1] !== "config" &&
          (operation !== "delete" || query.queryKey[1] !== "details"),
      });
    },
  });
}
