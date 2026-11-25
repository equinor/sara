import type {
  BenchmarkEvaluation,
  BenchmarkImageResult,
} from "../../../api/benchmarkClient";

export interface EvaluationSummary {
  tp: number;
  fn: number;
  fp: number;
  precision: number | null;
  recall: number | null;
}

export function summarizeBinaryEvaluation(
  evaluation: BenchmarkEvaluation
): EvaluationSummary | null {
  const matrix = evaluation.confusion_matrix;
  if (matrix.length !== 2 || matrix[0].length !== 2 || matrix[1].length !== 2) {
    return null;
  }
  const tp = matrix[0][0] ?? 0;
  const fn = matrix[0][1] ?? 0;
  const fp = matrix[1][0] ?? 0;
  return {
    tp,
    fn,
    fp,
    precision: tp + fp > 0 ? tp / (tp + fp) : null,
    recall: tp + fn > 0 ? tp / (tp + fn) : null,
  };
}

export function formatPercent(value: number | null): string {
  return value === null ? "–" : `${(value * 100).toFixed(1)}%`;
}

export function formatNumber(value: number | null | undefined, digits = 2): string {
  return value == null ? "–" : value.toFixed(digits);
}

export function shortArtifact(artifact: string): string {
  const lastSlash = artifact.lastIndexOf("/");
  return lastSlash >= 0 ? artifact.slice(lastSlash + 1) : artifact;
}

export function benchmarkRunDetailPath(
  datasetId: string,
  version: string,
  split: string,
  runId: string
): string {
  return `/benchmarks/${encodeURIComponent(datasetId)}/${encodeURIComponent(version)}/${encodeURIComponent(split)}/${encodeURIComponent(runId)}`;
}

export function isImagePassed(row: BenchmarkImageResult): boolean {
  return row.missed === 0 && row.false_positives === 0;
}
