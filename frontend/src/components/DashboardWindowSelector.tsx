import { Chip } from "@equinor/eds-core-react";
import styled from "styled-components";

const WindowToggle = styled.div`
  display: flex;
  gap: 0.5rem;
  align-items: center;
`;

export default function DashboardWindowSelector({
  windows,
  windowHours,
  onChange,
  "aria-label": ariaLabel,
}: {
  windows: readonly { hours: number; label: string }[];
  windowHours: number;
  onChange: (hours: number) => void;
  "aria-label"?: string;
}) {
  return (
    <WindowToggle aria-label={ariaLabel}>
      {windows.map((item) => (
        <Chip
          key={item.hours}
          variant={windowHours === item.hours ? "active" : "default"}
          onClick={() => onChange(item.hours)}
          style={{ cursor: "pointer" }}
        >
          {item.label}
        </Chip>
      ))}
    </WindowToggle>
  );
}
