import { Chip } from "@equinor/eds-core-react";

type Variant = "default" | "active" | "error";

function variantFor(status: string): Variant {
  switch (status) {
    case "Succeeded":
    case "Complete":
      return "active";
    case "InProgress":
    case "Started":
    case "Pending":
      return "default";
    case "Failed":
    case "TimedOut":
      return "error";
    default:
      return "default";
  }
}

export default function StatusChip({ status }: { status: string }) {
  return <Chip variant={variantFor(status)}>{status}</Chip>;
}
