import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { Button, Checkbox, Table, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import {
  getBenchmarkImageUrl,
  useBenchmarkImageComparison,
  useBenchmarkRunDetail,
} from "../../api/benchmarkClient";
import DashboardPanel from "../../components/DashboardPanel";
import PageHeader from "../../components/PageHeader";
import SegmentedToggle from "../../components/SegmentedToggle";
import StatusChip from "../../components/StatusChip";
import { ErrorText, TableScroller } from "../../components/Styles";
import BenchmarkMaskCanvas, {
  GT_COLOR,
  PRED_COLOR,
  type OverlayLayer,
} from "./benchmarks-components/BenchmarkMaskCanvas";
import { isImagePassed } from "./benchmarks-components/BenchmarkPerImageTable";
import {
  benchmarkRunDetailPath,
  formatNumber,
} from "./benchmarks-components/BenchmarkResultsTable";

type ViewMode = "side-by-side" | "overlay";

const ControlsBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
`;

const ToggleGroup = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
`;

const SideBySideGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
  gap: 1rem;
`;

const LegendSwatch = styled.span<{ $color: string }>`
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 2px;
  background: ${(p) => p.$color};
  margin-right: 0.35rem;
  vertical-align: middle;
`;

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

  const [viewMode, setViewMode] = useState<ViewMode>("side-by-side");
  const [showGt, setShowGt] = useState(true);
  const [showPred, setShowPred] = useState(true);
  const [showMasks, setShowMasks] = useState(true);
  const [showBoxes, setShowBoxes] = useState(true);
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);

  const runDetailQuery = useBenchmarkRunDetail(datasetId, version, split, runId);
  const comparisonQuery = useBenchmarkImageComparison(
    datasetId,
    version,
    split,
    runId,
    imageId
  );

  useEffect(() => {
    if (imageId === null) return;
    let cancelled = false;
    let objectUrl: string | null = null;
    setImageError(null);
    setImageUrl(null);

    getBenchmarkImageUrl(datasetId, version, split, runId, imageId)
      .then((url) => {
        if (cancelled) {
          URL.revokeObjectURL(url);
          return;
        }
        objectUrl = url;
        setImageUrl(url);
      })
      .catch((err) => {
        if (!cancelled) {
          setImageError(err instanceof Error ? err.message : "Failed to load image");
        }
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [datasetId, version, split, runId, imageId]);

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

  const overlayLayers = useMemo<OverlayLayer[]>(() => {
    if (!comparison) return [];
    const layers: OverlayLayer[] = [];
    if (showGt) {
      layers.push({ annotations: comparison.groundTruth, color: GT_COLOR });
    }
    if (showPred) {
      layers.push({ annotations: comparison.predictions, color: PRED_COLOR });
    }
    return layers;
  }, [comparison, showGt, showPred]);

  return (
    <PageHeader
      title={comparison ? `${comparison.fileName} (#${comparison.imageId})` : `Image #${imageIdParam}`}
      loading={comparisonQuery.isFetching}
      onRefresh={() => comparisonQuery.refetch()}
    >
      <ControlsBar>
        <ToggleGroup>
          <Button variant="ghost" onClick={() => navigate(runPath)}>
            ← Back to {runId}
          </Button>
          {comparison?.summary && (
            <StatusChip
              status={isImagePassed(comparison.summary) ? "Succeeded" : "Failed"}
            />
          )}
          {comparison?.summary && (
            <Typography variant="body_short">
              GT: {comparison.summary.gt_count} · Pred: {comparison.summary.pred_count} ·
              Matched: {comparison.summary.matched} · Missed: {comparison.summary.missed} ·
              FP: {comparison.summary.false_positives} · Mean IoU:{" "}
              {formatNumber(comparison.summary.mean_iou, 3)}
            </Typography>
          )}
        </ToggleGroup>

        <ToggleGroup>
          <Button
            variant="outlined"
            disabled={prevImageId === null}
            onClick={() => prevImageId && navigate(`${runPath}/images/${prevImageId}`)}
          >
            ← Previous
          </Button>
          <Button
            variant="outlined"
            disabled={nextImageId === null}
            onClick={() => nextImageId && navigate(`${runPath}/images/${nextImageId}`)}
          >
            Next →
          </Button>
        </ToggleGroup>
      </ControlsBar>

      {(comparisonQuery.error || imageError) && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {comparisonQuery.error?.message ?? imageError}
        </ErrorText>
      )}

      {comparison && (
        <DashboardPanel title="Ground truth vs. prediction" style={{ marginBottom: "1rem" }}>
          <ControlsBar>
            <SegmentedToggle<ViewMode>
              value={viewMode}
              onChange={setViewMode}
              options={[
                { value: "side-by-side", label: "Side by side" },
                { value: "overlay", label: "Overlay" },
              ]}
            />
            <ToggleGroup>
              {viewMode === "overlay" && (
                <>
                  <span style={{ display: "inline-flex", alignItems: "center" }}>
                    <LegendSwatch $color={GT_COLOR.stroke} />
                    <Checkbox
                      label={`Ground truth (${comparison.groundTruth.length})`}
                      checked={showGt}
                      onChange={(e) => setShowGt(e.target.checked)}
                    />
                  </span>
                  <span style={{ display: "inline-flex", alignItems: "center" }}>
                    <LegendSwatch $color={PRED_COLOR.stroke} />
                    <Checkbox
                      label={`Predictions (${comparison.predictions.length})`}
                      checked={showPred}
                      onChange={(e) => setShowPred(e.target.checked)}
                    />
                  </span>
                </>
              )}
              <Checkbox
                label="Masks"
                checked={showMasks}
                onChange={(e) => setShowMasks(e.target.checked)}
              />
              <Checkbox
                label="Boxes"
                checked={showBoxes}
                onChange={(e) => setShowBoxes(e.target.checked)}
              />
            </ToggleGroup>
          </ControlsBar>

          {imageUrl ? (
            viewMode === "side-by-side" ? (
              <SideBySideGrid>
                <BenchmarkMaskCanvas
                  title={`Ground truth (${comparison.groundTruth.length})`}
                  imageUrl={imageUrl}
                  width={comparison.width}
                  height={comparison.height}
                  layers={[{ annotations: comparison.groundTruth, color: GT_COLOR }]}
                  showMasks={showMasks}
                  showBoxes={showBoxes}
                />
                <BenchmarkMaskCanvas
                  title={`Predictions (${comparison.predictions.length})`}
                  imageUrl={imageUrl}
                  width={comparison.width}
                  height={comparison.height}
                  layers={[{ annotations: comparison.predictions, color: PRED_COLOR }]}
                  showMasks={showMasks}
                  showBoxes={showBoxes}
                />
              </SideBySideGrid>
            ) : (
              <BenchmarkMaskCanvas
                imageUrl={imageUrl}
                width={comparison.width}
                height={comparison.height}
                layers={overlayLayers}
                showMasks={showMasks}
                showBoxes={showBoxes}
              />
            )
          ) : (
            <Typography variant="body_short">Loading image…</Typography>
          )}
        </DashboardPanel>
      )}

      {comparison && (
        <DashboardPanel title="Instance annotations">
          <TableScroller>
            <Table style={{ width: "100%" }}>
              <Table.Head>
                <Table.Row>
                  <Table.Cell>Source</Table.Cell>
                  <Table.Cell>ID</Table.Cell>
                  <Table.Cell>Class</Table.Cell>
                  <Table.Cell>BBox [x, y, w, h]</Table.Cell>
                  <Table.Cell>Area (px)</Table.Cell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {comparison.groundTruth.length === 0 && comparison.predictions.length === 0 ? (
                  <Table.Row>
                    <Table.Cell colSpan={5}>
                      No ground-truth or predicted instances (confirmed negative).
                    </Table.Cell>
                  </Table.Row>
                ) : (
                  [
                    ...comparison.groundTruth.map((ann) => ({
                      source: "Ground truth",
                      color: GT_COLOR.stroke,
                      ann,
                    })),
                    ...comparison.predictions.map((ann) => ({
                      source: "Prediction",
                      color: PRED_COLOR.stroke,
                      ann,
                    })),
                  ].map(({ source, color, ann }) => (
                    <Table.Row key={`${source}-${ann.id}`}>
                      <Table.Cell>
                        <LegendSwatch $color={color} />
                        {source}
                      </Table.Cell>
                      <Table.Cell>{ann.id}</Table.Cell>
                      <Table.Cell>{ann.categoryName}</Table.Cell>
                      <Table.Cell>
                        {ann.bbox
                          ? ann.bbox.map((v) => Math.round(v)).join(", ")
                          : "–"}
                      </Table.Cell>
                      <Table.Cell>
                        {ann.area != null ? Math.round(ann.area) : "–"}
                      </Table.Cell>
                    </Table.Row>
                  ))
                )}
              </Table.Body>
            </Table>
          </TableScroller>
        </DashboardPanel>
      )}
    </PageHeader>
  );
}
