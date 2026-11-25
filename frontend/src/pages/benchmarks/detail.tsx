import { useNavigate, useParams } from "react-router";
import { Button, Typography } from "@equinor/eds-core-react";
import { useBenchmarkRunDetail } from "../../api/benchmarkClient";
import DashboardPanel from "../../components/DashboardPanel";
import PageHeader from "../../components/PageHeader";
import { ErrorText } from "../../components/Styles";
import { benchmarkRunDetailPath } from "./benchmarks-components/benchmarkFormatting";
import BenchmarkConfusionMatrixPanel from "./benchmarks-components/run-detail/BenchmarkConfusionMatrixPanel";
import BenchmarkPerImageTable from "./benchmarks-components/run-detail/BenchmarkPerImageTable";
import BenchmarkRunMetadataPanel from "./benchmarks-components/run-detail/BenchmarkRunMetadataPanel";
import BenchmarkRunSummaryCards from "./benchmarks-components/run-detail/BenchmarkRunSummaryCards";

export default function BenchmarkDetailPage() {
  const navigate = useNavigate();
  const { datasetId = "", version = "", split = "", runId = "" } = useParams();

  const detailQuery = useBenchmarkRunDetail(datasetId, version, split, runId);
  const detail = detailQuery.data;

  const evalEntries = Object.entries(detail?.metrics?.evaluations ?? {});
  const primaryEvaluation = evalEntries[0]?.[1];

  const handleSelectImage = (imageId: number) => {
    navigate(
      `${benchmarkRunDetailPath(datasetId, version, split, runId)}/images/${imageId}`
    );
  };

  return (
    <PageHeader
      title={`Benchmark: ${runId}`}
      loading={detailQuery.isFetching}
      onRefresh={() => detailQuery.refetch()}
    >
      <div style={{ marginBottom: "1rem" }}>
        <Button variant="ghost" onClick={() => navigate("/benchmarks")}>
          ← Back to Benchmarks
        </Button>
      </div>

      {detailQuery.error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {detailQuery.error.message}
        </ErrorText>
      )}
      {detail?.error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {detail.error}
        </ErrorText>
      )}

      {detail?.run && detail?.metrics && (
        <>
          <BenchmarkRunSummaryCards
            metrics={detail.metrics}
            primaryEvaluation={primaryEvaluation}
          />
          <BenchmarkRunMetadataPanel run={detail.run} />
          {evalEntries.map(([name, evaluation]) => (
            <BenchmarkConfusionMatrixPanel
              key={name}
              name={name}
              evaluation={evaluation}
            />
          ))}
        </>
      )}

      {detail?.perImage ? (
        <BenchmarkPerImageTable
          items={detail.perImage}
          onSelectImage={handleSelectImage}
        />
      ) : (
        detail && (
          <DashboardPanel title="Per-image results">
            <Typography variant="body_short">
              No per-image breakdown was recorded for this run.
            </Typography>
          </DashboardPanel>
        )
      )}
    </PageHeader>
  );
}
