import { Chip } from "@equinor/eds-core-react";
import type { ResultSeverity } from "../api/client";

type Variant = "default" | "active" | "error";

function variantFor(severity: ResultSeverity): Variant {
  switch (severity) {
    case "Ok":
      return "active";
    case "Warning":
    case "Alert":
      return "error";
    default:
      return "default";
  }
}

export default function SeverityChip({ severity }: { severity: ResultSeverity }) {
  return <Chip variant={variantFor(severity)}>{severity}</Chip>;
}
