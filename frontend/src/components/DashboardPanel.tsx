import type { CSSProperties, ReactNode } from "react";
import { Typography } from "@equinor/eds-core-react";
import { tokens } from "@equinor/eds-tokens";
import styled from "styled-components";
import { Surface } from "./Styles";

const Panel = styled(Surface)<{ $feedback: boolean }>`
  padding: ${(p) => p.$feedback ? "0.75rem" : "0.6rem 0.8rem"};
  overflow-x: auto;
`;

const SectionTitle = styled(Typography).attrs({ variant: "caption" })<{ $feedback: boolean }>`
  display: block;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: ${(p) => p.$feedback
    ? tokens.colors.text.static_icons__secondary.hex
    : tokens.colors.text.static_icons__tertiary.hex};
  margin-bottom: ${(p) => p.$feedback ? "0.5rem" : "0.4rem"};
  ${(p) => p.$feedback && "font-weight: 600;"}
`;

export default function DashboardPanel({
  title,
  accent,
  variant = "overview",
  style,
  children,
}: {
  title: ReactNode;
  accent?: string;
  variant?: "overview" | "feedback";
  style?: CSSProperties;
  children: ReactNode;
}) {
  const feedback = variant === "feedback";
  return (
    <Panel as={feedback ? "section" : "div"} $feedback={feedback} $accent={accent} style={style}>
      <SectionTitle $feedback={feedback}>{title}</SectionTitle>
      {children}
    </Panel>
  );
}
