import type {
  BenchmarkEvaluation,
  BenchmarkImageResult,
  BenchmarkJob,
  BenchmarkRunConfiguration,
} from "../../../api/benchmarkClient";

export interface EvaluationSummary {
  tp: number;
  fn: number;
  fp: number;
  tn: number;
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
  const tn = matrix[1][1] ?? 0;
  return {
    tp,
    fn,
    fp,
    tn,
    precision: tp + fp > 0 ? tp / (tp + fp) : null,
    recall: tp + fn > 0 ? tp / (tp + fn) : null,
  };
}

export function formatPercent(value: number | null | undefined): string {
  return value == null ? "–" : `${(value * 100).toFixed(1)}%`;
}

export function formatNumber(value: number | null | undefined, digits = 2): string {
  return value == null ? "–" : value.toFixed(digits);
}

export function formatMethodLabel(method?: string | null): string {
  if (!method) return "–";
  switch (method) {
    case "segmentation":
      return "Segmentation";
    case "bounding_box":
      return "Bounding box";
    default:
      return method;
  }
}

export function formatConfigurationDetails(
  config?: BenchmarkRunConfiguration | null
): string {
  if (!config) return "";
  const parts: string[] = [config.model];
  if (config.confidence_threshold != null) {
    parts.push(`conf ${config.confidence_threshold}`);
  }
  if (config.box_padding != null) {
    parts.push(`pad ${(config.box_padding * 100).toFixed(0)}%`);
  }
  if (config.compiled) {
    parts.push("compiled");
  }
  return parts.join(" · ");
}

export function formatJobMethodSummary(job: BenchmarkJob): string {
  if (!job.method) return "–";
  const parts: string[] = [formatMethodLabel(job.method)];
  if (job.confidenceThreshold != null) {
    parts.push(`conf ${job.confidenceThreshold}`);
  }
  if (job.boxPadding != null) {
    parts.push(`pad ${(job.boxPadding * 100).toFixed(0)}%`);
  }
  return parts.join(" · ");
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
  return row.missed_people === 0 && !row.false_positive;
}
