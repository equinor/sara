import { Chip } from "@equinor/eds-core-react";

function statusVariant(
  status: string
): "default" | "active" | "error" | undefined {
  switch (status) {
    case "ExitSuccess":
      return "active";
    case "ExitFailure":
      return "error";
    case "Started":
      return "active";
    default:
      return "default";
  }
}

export default function StatusChip({ status }: { status: string }) {
  return <Chip variant={statusVariant(status)}>{status}</Chip>;
}
