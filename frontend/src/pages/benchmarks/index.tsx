import { useEffect, useState } from "react";
import {
  useBenchmarkDatasets,
  useBenchmarkJobs,
  useBenchmarkResults,
  useTriggerBenchmark,
} from "../../api/benchmarkClient";
import PageHeader from "../../components/PageHeader";
import { ErrorText } from "../../components/Styles";
import { isParameterApplicable } from "./benchmarks-components/benchmarkFormatting";
import BenchmarkJobsTable from "./benchmarks-components/overview/BenchmarkJobsTable";
import BenchmarkResultsTable from "./benchmarks-components/overview/BenchmarkResultsTable";
import BenchmarkTriggerPanel from "./benchmarks-components/overview/BenchmarkTriggerPanel";

function parseOptionalNumber(raw: string, label: string): number | undefined {
  const trimmed = raw.trim();
  if (!trimmed) return undefined;
  const parsed = Number(trimmed);
  if (!Number.isFinite(parsed)) {
    throw new Error(`${label} must be a valid number.`);
  }
  return parsed;
}

export default function BenchmarksPage() {
  const datasetsQuery = useBenchmarkDatasets();
  const jobsQuery = useBenchmarkJobs();
  const triggerMutation = useTriggerBenchmark();

  const datasets = datasetsQuery.data ?? [];
  const [datasetId, setDatasetId] = useState("");
  const [version, setVersion] = useState("");
  const [split, setSplit] = useState("val");
  const [device, setDevice] = useState("gpu");
  const [method, setMethod] = useState("");
  const [parameterValues, setParameterValues] = useState<Record<string, string>>({});
  const [runId, setRunId] = useState("");
  const [triggerValidationError, setTriggerValidationError] = useState<string | null>(
    null
  );
  const [lastTriggeredWorkflow, setLastTriggeredWorkflow] = useState<string | null>(null);

  useEffect(() => {
    if (datasets.length === 0 || datasetId) return;
    const initial = datasets[0];
    setDatasetId(initial.id);
    setVersion(initial.versions[0] ?? "v001");
    setSplit(initial.defaultSplit || "val");
    setDevice(initial.devices.includes("gpu") ? "gpu" : (initial.devices[0] ?? "cpu"));
    setMethod(initial.methods[0] ?? "");
    setParameterValues({});
  }, [datasets, datasetId]);

  const resultsQuery = useBenchmarkResults(datasetId, version, split);
  const selectedDataset = datasets.find((d) => d.id === datasetId);

  const handleDatasetChange = (nextDatasetId: string) => {
    const selected = datasets.find((d) => d.id === nextDatasetId);
    setDatasetId(nextDatasetId);
    setVersion(selected?.versions[0] ?? "v001");
    setSplit(selected?.defaultSplit || "val");
    setDevice(
      selected?.devices.includes("gpu") ? "gpu" : (selected?.devices[0] ?? "cpu")
    );
    setMethod(selected?.methods[0] ?? "");
    setParameterValues({});
  };

  const handleMethodChange = (nextMethod: string) => {
    setMethod(nextMethod);
    const params = selectedDataset?.parameters ?? [];
    setParameterValues((prev) => {
      const next: Record<string, string> = {};
      for (const p of params) {
        if (isParameterApplicable(p, nextMethod) && prev[p.name]) {
          next[p.name] = prev[p.name];
        }
      }
      return next;
    });
  };

  const handleParameterChange = (name: string, value: string) => {
    setParameterValues((prev) => ({ ...prev, [name]: value }));
  };

  const handleTrigger = async () => {
    setLastTriggeredWorkflow(null);
    setTriggerValidationError(null);
    const parsedParameters: Record<string, number | null> = {};
    try {
      for (const param of selectedDataset?.parameters ?? []) {
        if (!isParameterApplicable(param, method)) continue;
        const parsed = parseOptionalNumber(parameterValues[param.name] ?? "", param.label);
        if (parsed !== undefined) {
          parsedParameters[param.name] = parsed;
        }
      }
    } catch (err) {
      setTriggerValidationError(err instanceof Error ? err.message : String(err));
      return;
    }

    try {
      const created = await triggerMutation.mutateAsync({
        datasetId,
        version: version.trim(),
        split: split.trim(),
        device,
        method: method || undefined,
        parameters:
          Object.keys(parsedParameters).length > 0 ? parsedParameters : undefined,
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
        method={method}
        parameterValues={parameterValues}
        runId={runId}
        submitting={triggerMutation.isPending}
        error={triggerValidationError ?? triggerMutation.error?.message ?? null}
        lastTriggeredWorkflow={lastTriggeredWorkflow}
        onDatasetChange={handleDatasetChange}
        onVersionChange={setVersion}
        onSplitChange={setSplit}
        onDeviceChange={setDevice}
        onMethodChange={handleMethodChange}
        onParameterChange={handleParameterChange}
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
