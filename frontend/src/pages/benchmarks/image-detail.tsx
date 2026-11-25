import { useMemo } from "react";
import { useNavigate, useParams } from "react-router";
import {
  useBenchmarkImageComparison,
  useBenchmarkRunDetail,
} from "../../api/benchmarkClient";
import PageHeader from "../../components/PageHeader";
import { ErrorText } from "../../components/Styles";
import { benchmarkRunDetailPath } from "./benchmarks-components/benchmarkFormatting";
import BenchmarkAnnotationsTable from "./benchmarks-components/image-comparison/BenchmarkAnnotationsTable";
import BenchmarkComparisonViewer from "./benchmarks-components/image-comparison/BenchmarkComparisonViewer";
import BenchmarkImageHeaderBar from "./benchmarks-components/image-comparison/BenchmarkImageHeaderBar";
import { useBenchmarkImageObjectUrl } from "./benchmarks-components/image-comparison/useBenchmarkImageObjectUrl";

export default function BenchmarkImageDetailPage() {
  const navigate = useNavigate();
  const {
    datasetId = "",
    version = "",
    split = "",
    runId = "",
    imageId: imageIdParam = "",
  } = useParams();
  const parsedId = Number(imageIdParam);
  const imageId = Number.isInteger(parsedId) && parsedId > 0 ? parsedId : null;

  const runDetailQuery = useBenchmarkRunDetail(datasetId, version, split, runId);
  const comparisonQuery = useBenchmarkImageComparison(
    datasetId,
    version,
    split,
    runId,
    imageId
  );
  const { imageUrl, imageError } = useBenchmarkImageObjectUrl(
    datasetId,
    version,
    split,
    runId,
    imageId
  );

  const orderedImageIds = useMemo(
    () => (runDetailQuery.data?.perImage ?? []).map((item) => item.image_id),
    [runDetailQuery.data]
  );
  const currentIndex = imageId === null ? -1 : orderedImageIds.indexOf(imageId);
  const prevImageId = currentIndex > 0 ? orderedImageIds[currentIndex - 1] : null;
  const nextImageId =
    currentIndex >= 0 && currentIndex < orderedImageIds.length - 1
      ? orderedImageIds[currentIndex + 1]
      : null;

  const runPath = benchmarkRunDetailPath(datasetId, version, split, runId);
  const comparison = comparisonQuery.data;

  return (
    <PageHeader
      title={
        comparison
          ? `${comparison.fileName} (#${comparison.imageId})`
          : `Image #${imageIdParam}`
      }
      loading={comparisonQuery.isFetching}
      onRefresh={() => comparisonQuery.refetch()}
    >
      <BenchmarkImageHeaderBar
        runId={runId}
        summary={comparison?.summary}
        hasPrev={prevImageId !== null}
        hasNext={nextImageId !== null}
        onBack={() => navigate(runPath)}
        onPrev={() => prevImageId && navigate(`${runPath}/images/${prevImageId}`)}
        onNext={() => nextImageId && navigate(`${runPath}/images/${nextImageId}`)}
      />

      {(comparisonQuery.error || imageError) && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {comparisonQuery.error?.message ?? imageError}
        </ErrorText>
      )}

      <BenchmarkComparisonViewer comparison={comparison} imageUrl={imageUrl} />

      {comparison && (
        <BenchmarkAnnotationsTable
          groundTruth={comparison.groundTruth}
          predictions={comparison.predictions}
        />
      )}
    </PageHeader>
  );
}
