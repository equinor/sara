import type { BenchmarkEvaluation } from "../../../../api/benchmarkClient";

export interface MatrixTotals {
  rowTotals: number[];
  colTotals: number[];
  grandTotal: number;
}

export interface BinaryClassificationStats {
  positiveLabel: string;
  negativeLabel: string;
  tp: number;
  fn: number;
  fp: number;
  tn: number;
  positives: number;
  negatives: number;
  total: number;
  positiveShare: number | null;
  negativeShare: number | null;
  accuracy: number | null;
  precision: number | null;
  recall: number | null;
  specificity: number | null;
  f1: number | null;
  falseNegativeRate: number | null;
  falsePositiveRate: number | null;
}

export function formatEvaluationLabel(label: string): string {
  if (label === "__background__") return "background";
  return label.replace(/_/g, " ");
}

export function safeRatio(numerator: number, denominator: number): number | null {
  return denominator > 0 ? numerator / denominator : null;
}

function computeF1(precision: number | null, recall: number | null): number | null {
  if (precision === null || recall === null) return null;
  if (precision + recall === 0) return 0;
  return (2 * precision * recall) / (precision + recall);
}

export function computeMatrixTotals(
  matrix: (number | null)[][],
  labelCount: number
): MatrixTotals {
  const rowTotals = Array<number>(labelCount).fill(0);
  const colTotals = Array<number>(labelCount).fill(0);
  let grandTotal = 0;

  for (let rowIdx = 0; rowIdx < labelCount; rowIdx += 1) {
    const row = matrix[rowIdx];
    if (!row) continue;
    for (let colIdx = 0; colIdx < labelCount; colIdx += 1) {
      const cell = row[colIdx];
      if (typeof cell !== "number" || !Number.isFinite(cell)) continue;
      rowTotals[rowIdx] += cell;
      colTotals[colIdx] += cell;
      grandTotal += cell;
    }
  }

  return { rowTotals, colTotals, grandTotal };
}

function isValidCount(value: number | null | undefined): value is number {
  return typeof value === "number" && Number.isFinite(value) && value >= 0;
}

export function computeBinaryClassificationStats(
  evaluation: BenchmarkEvaluation
): BinaryClassificationStats | null {
  const { labels, confusion_matrix: matrix } = evaluation;
  if (labels.length !== 2 || matrix.length !== 2) return null;

  const tp = matrix[0]?.[0];
  const fn = matrix[0]?.[1];
  const fp = matrix[1]?.[0];
  const tn = matrix[1]?.[1];
  if (!isValidCount(tp) || !isValidCount(fn) || !isValidCount(fp) || !isValidCount(tn)) {
    return null;
  }

  const positives = tp + fn;
  const negatives = fp + tn;
  const total = positives + negatives;
  const precision = safeRatio(tp, tp + fp);
  const recall = safeRatio(tp, positives);

  return {
    positiveLabel: formatEvaluationLabel(labels[0]),
    negativeLabel: formatEvaluationLabel(labels[1]),
    tp,
    fn,
    fp,
    tn,
    positives,
    negatives,
    total,
    positiveShare: safeRatio(positives, total),
    negativeShare: safeRatio(negatives, total),
    accuracy: safeRatio(tp + tn, total),
    precision,
    recall,
    specificity: safeRatio(tn, negatives),
    f1: computeF1(precision, recall),
    falseNegativeRate: safeRatio(fn, positives),
    falsePositiveRate: safeRatio(fp, negatives),
  };
}
