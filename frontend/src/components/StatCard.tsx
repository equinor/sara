import { ReactNode } from "react";
import { Typography } from "@equinor/eds-core-react";
import styled from "styled-components";

type Tone = "default" | "success" | "error" | "warning" | "info";

const TONE_COLORS: Record<Tone, string> = {
  default: "#6f6f6f",
  success: "#4bb748",
  error: "#eb0000",
  warning: "#ff9200",
  info: "#0084c4",
};

const Card = styled.div<{ $accent: string }>`
  border: 1px solid #dcdcdc;
  border-left: 3px solid ${(p) => p.$accent};
  border-radius: 4px;
  padding: 0.5rem 0.75rem;
  background: #ffffff;
  min-width: 120px;
  flex: 1 1 120px;
`;

const Value = styled(Typography)`
  font-size: 1.4rem;
  font-weight: 600;
  line-height: 1.15;
`;

interface Props {
  title: string;
  value: ReactNode;
  tone?: Tone;
  subtitle?: ReactNode;
}

export default function StatCard({ title, value, tone = "default", subtitle }: Props) {
  const accent = TONE_COLORS[tone];
  return (
    <Card $accent={accent}>
      <Typography
        variant="caption"
        style={{ color: "#6f6f6f", textTransform: "uppercase", letterSpacing: "0.04em" }}
      >
        {title}
      </Typography>
      <Value variant="h4" style={{ color: tone === "default" ? undefined : accent }}>
        {value}
      </Value>
      {subtitle && (
        <Typography variant="caption" style={{ color: "#6f6f6f", fontSize: "0.7rem" }}>
          {subtitle}
        </Typography>
      )}
    </Card>
  );
}
