import { useEffect, useState } from "react";
import {
  useBenchmarkDatasets,
  useBenchmarkJobs,
  useBenchmarkResults,
  useTriggerBenchmark,
} from "../../api/benchmarkClient";
import PageHeader from "../../components/PageHeader";
import { ErrorText } from "../../components/Styles";
import BenchmarkJobsTable from "./benchmarks-components/overview/BenchmarkJobsTable";
import BenchmarkResultsTable from "./benchmarks-components/overview/BenchmarkResultsTable";
import BenchmarkTriggerPanel from "./benchmarks-components/overview/BenchmarkTriggerPanel";

export default function BenchmarksPage() {
  const datasetsQuery = useBenchmarkDatasets();
  const jobsQuery = useBenchmarkJobs();
  const triggerMutation = useTriggerBenchmark();

  const datasets = datasetsQuery.data ?? [];
  const [datasetId, setDatasetId] = useState("");
  const [version, setVersion] = useState("");
  const [split, setSplit] = useState("val");
  const [device, setDevice] = useState("gpu");
  const [runId, setRunId] = useState("");
  const [lastTriggeredWorkflow, setLastTriggeredWorkflow] = useState<string | null>(null);

  useEffect(() => {
    if (datasets.length === 0 || datasetId) return;
    const initial = datasets[0];
    setDatasetId(initial.id);
    setVersion(initial.versions[0] ?? "v001");
    setSplit(initial.defaultSplit || "val");
    setDevice(initial.devices.includes("gpu") ? "gpu" : (initial.devices[0] ?? "cpu"));
  }, [datasets, datasetId]);

  const resultsQuery = useBenchmarkResults(datasetId, version, split);

  const handleDatasetChange = (nextDatasetId: string) => {
    const selected = datasets.find((d) => d.id === nextDatasetId);
    setDatasetId(nextDatasetId);
    setVersion(selected?.versions[0] ?? "v001");
    setSplit(selected?.defaultSplit || "val");
    setDevice(
      selected?.devices.includes("gpu") ? "gpu" : (selected?.devices[0] ?? "cpu")
    );
  };

  const handleTrigger = async () => {
    setLastTriggeredWorkflow(null);
    try {
      const created = await triggerMutation.mutateAsync({
        datasetId,
        version: version.trim(),
        split: split.trim(),
        device,
        runId: runId.trim() || undefined,
      });
      setRunId("");
      setLastTriggeredWorkflow(created.workflowName);
    } catch {
      return;
    }
  };

  const handleRefresh = () => {
    datasetsQuery.refetch();
    jobsQuery.refetch();
    if (datasetId && version && split) {
      resultsQuery.refetch();
    }
  };

  const loading =
    datasetsQuery.isFetching || jobsQuery.isFetching || resultsQuery.isFetching;
  const pageError =
    datasetsQuery.error?.message ??
    jobsQuery.error?.message ??
    resultsQuery.error?.message ??
    null;

  return (
    <PageHeader title="Benchmarks" loading={loading} onRefresh={handleRefresh}>
      {pageError && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {pageError}
        </ErrorText>
      )}

      <BenchmarkTriggerPanel
        datasets={datasets}
        datasetId={datasetId}
        version={version}
        split={split}
        device={device}
        runId={runId}
        submitting={triggerMutation.isPending}
        error={triggerMutation.error?.message ?? null}
        lastTriggeredWorkflow={lastTriggeredWorkflow}
        onDatasetChange={handleDatasetChange}
        onVersionChange={setVersion}
        onSplitChange={setSplit}
        onDeviceChange={setDevice}
        onRunIdChange={setRunId}
        onSubmit={handleTrigger}
      />

      <BenchmarkJobsTable
        jobs={jobsQuery.data ?? []}
        loading={jobsQuery.isLoading}
      />

      <BenchmarkResultsTable
        datasetId={datasetId}
        version={version}
        split={split}
        results={resultsQuery.data ?? []}
        loading={resultsQuery.isLoading}
      />
    </PageHeader>
  );
}
