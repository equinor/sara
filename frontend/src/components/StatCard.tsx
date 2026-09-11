import { ReactNode } from "react";
import { Typography } from "@equinor/eds-core-react";
import { tokens } from "@equinor/eds-tokens";
import styled from "styled-components";
import { statusColors, Surface } from "./Styles";

type Tone = keyof typeof statusColors;

const Card = styled(Surface)`
  padding: 0.5rem 0.75rem;
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
  const { accent, text } = statusColors[tone];
  return (
    <Card $accent={accent}>
      <Typography
        variant="caption"
        style={{ color: tokens.colors.text.static_icons__tertiary.hex, textTransform: "uppercase", letterSpacing: "0.04em" }}
      >
        {title}
      </Typography>
      <Value variant="h4" style={{ color: text }}>
        {value}
      </Value>
      {subtitle && (
        <Typography variant="caption" style={{ color: tokens.colors.text.static_icons__tertiary.hex, fontSize: "0.7rem" }}>
          {subtitle}
        </Typography>
      )}
    </Card>
  );
}
