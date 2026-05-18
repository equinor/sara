import { ReactNode } from "react";
import { Button, Icon, Typography } from "@equinor/eds-core-react";
import { add, refresh } from "@equinor/eds-icons";
import styled from "styled-components";

Icon.add({ add, refresh });

const Header = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
`;

const Actions = styled.div`
  display: flex;
  gap: 0.5rem;
`;

interface Props {
  title: string;
  loading?: boolean;
  onRefresh?: () => void;
  primaryAction?: { label: string; onClick: () => void };
  children: ReactNode;
}

export default function PageHeader({
  title,
  loading,
  onRefresh,
  primaryAction,
  children,
}: Props) {
  return (
    <div style={{ paddingTop: "1rem" }}>
      <Header>
        <Typography variant="h3">{title}</Typography>
        <Actions>
          {onRefresh && (
            <Button
              variant="ghost_icon"
              onClick={onRefresh}
              aria-label="Refresh"
              disabled={loading}
            >
              <Icon name="refresh" />
            </Button>
          )}
          {primaryAction && (
            <Button onClick={primaryAction.onClick}>
              <Icon name="add" />
              {primaryAction.label}
            </Button>
          )}
        </Actions>
      </Header>
      {children}
    </div>
  );
}
