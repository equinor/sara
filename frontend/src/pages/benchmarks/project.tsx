import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { Button, Typography } from "@equinor/eds-core-react";
import { tokens } from "@equinor/eds-tokens";
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

export default function BenchmarkProjectPage() {
  const navigate = useNavigate();
  const { datasetId = "" } = useParams();

  const datasetsQuery = useBenchmarkDatasets();
  const jobsQuery = useBenchmarkJobs();
  const triggerMutation = useTriggerBenchmark();

  const datasets = datasetsQuery.data ?? [];
  const selectedDataset = datasets.find(
    (d) => d.id.toLowerCase() === datasetId.toLowerCase()
  );
  const resolvedDatasetId = selectedDataset?.id ?? "";

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
    if (!selectedDataset) return;
    setVersion(selectedDataset.versions[0] ?? "v001");
    setSplit(selectedDataset.defaultSplit || "val");
    setDevice(
      selectedDataset.devices.includes("gpu")
        ? "gpu"
        : (selectedDataset.devices[0] ?? "cpu")
    );
    setMethod(selectedDataset.methods[0] ?? "");
    setParameterValues({});
    setRunId("");
    setTriggerValidationError(null);
    setLastTriggeredWorkflow(null);
  }, [selectedDataset?.id]);

  const resultsQuery = useBenchmarkResults(resolvedDatasetId, version, split);
  const projectJobs = (jobsQuery.data ?? []).filter(
    (job) => job.datasetId.toLowerCase() === datasetId.toLowerCase()
  );

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
    if (!selectedDataset) return;
    setLastTriggeredWorkflow(null);
    setTriggerValidationError(null);
    const parsedParameters: Record<string, number | null> = {};
    try {
      for (const param of selectedDataset.parameters) {
        if (!isParameterApplicable(param, method)) continue;
        const parsed = parseOptionalNumber(
          parameterValues[param.name] ?? "",
          param.label
        );
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
        datasetId: selectedDataset.id,
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
    if (resolvedDatasetId && version && split) {
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
  const projectNotFound = !datasetsQuery.isLoading && !datasetsQuery.error && !selectedDataset;

  return (
    <PageHeader
      title={selectedDataset?.label ?? datasetId}
      loading={loading}
      onRefresh={handleRefresh}
    >
      <div style={{ marginBottom: "1rem" }}>
        <Button variant="ghost" onClick={() => navigate("/benchmarks")}>
          ← Back to Benchmarks
        </Button>
      </div>

      {selectedDataset?.description && (
        <Typography
          variant="body_short"
          style={{
            color: tokens.colors.text.static_icons__secondary.hex,
            marginBottom: "1rem",
          }}
        >
          {selectedDataset.description}
        </Typography>
      )}

      {pageError && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {pageError}
        </ErrorText>
      )}

      {projectNotFound ? (
        <ErrorText variant="body_short">
          Unknown benchmark project "{datasetId}".
        </ErrorText>
      ) : (
        <>
          <BenchmarkTriggerPanel
            dataset={selectedDataset}
            version={version}
            split={split}
            device={device}
            method={method}
            parameterValues={parameterValues}
            runId={runId}
            submitting={triggerMutation.isPending}
            error={triggerValidationError ?? triggerMutation.error?.message ?? null}
            lastTriggeredWorkflow={lastTriggeredWorkflow}
            onVersionChange={setVersion}
            onSplitChange={setSplit}
            onDeviceChange={setDevice}
            onMethodChange={handleMethodChange}
            onParameterChange={handleParameterChange}
            onRunIdChange={setRunId}
            onSubmit={handleTrigger}
          />

          <BenchmarkJobsTable jobs={projectJobs} loading={jobsQuery.isLoading} />

          <BenchmarkResultsTable
            datasetId={resolvedDatasetId}
            version={version}
            split={split}
            results={resultsQuery.data ?? []}
            loading={resultsQuery.isLoading}
          />
        </>
      )}
    </PageHeader>
  );
}
