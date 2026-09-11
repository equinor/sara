import { Button, Label, NativeSelect, Search, TextField, Typography } from "@equinor/eds-core-react";
import type { ComponentProps } from "react";
import { tokens } from "@equinor/eds-tokens";
import styled, { css } from "styled-components";

// Brand colors distinguish data series; semantic text colors retain readable contrast.
export const statusColors = {
  default: {
    accent: tokens.colors.text.static_icons__tertiary.hex,
    text: tokens.colors.text.static_icons__default.hex,
  },
  success: {
    accent: tokens.colors.infographic.primary__moss_green_100.hex,
    text: tokens.colors.interactive.success__text.hex,
  },
  error: {
    accent: tokens.colors.infographic.primary__energy_red_100.hex,
    text: tokens.colors.interactive.danger__text.hex,
  },
  warning: {
    accent: tokens.colors.interactive.warning__resting.hex,
    text: tokens.colors.interactive.warning__text.hex,
  },
  info: {
    accent: tokens.colors.infographic.substitute__blue_ocean.hex,
    text: tokens.colors.infographic.primary__slate_blue.hex,
  },
};

export const Surface = styled.div<{ $accent?: string }>`
  min-width: 0;
  border: 1px solid ${tokens.colors.ui.background__medium.hex};
  ${(p) => p.$accent && css`border-left: 3px solid ${p.$accent};`}
  border-radius: ${tokens.shape.corners.borderRadius};
  background: ${tokens.colors.ui.background__default.hex};
  color: ${tokens.colors.text.static_icons__default.hex};
`;

export const FilterField = styled.div`
  display: grid;
  min-width: 0;
`;

export const FilterBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  gap: 0.75rem;
  margin-bottom: 1rem;

  > * {
    flex: 0 1 240px;
    max-width: 100%;
    min-width: 0;
  }

  @media (max-width: 560px) {
    > * {
      flex-basis: 100%;
    }
  }
`;

export function FilterSearch({ label, id, ...props }: ComponentProps<typeof Search> & { label: string; id: string }) {
  return (
    <FilterField>
      <Label htmlFor={id} label={label} />
      <Search id={id} {...props} />
    </FilterField>
  );
}

export const ErrorText = styled(Typography)`
  color: ${tokens.colors.interactive.danger__text.hex};
`;

const filterControlSizing = css`
  min-width: 0;
  width: 100%;

  input,
  select {
    box-sizing: border-box;
    min-width: 0;
    width: 100%;
  }
`;

export const FilterSelect = styled(NativeSelect)`
  ${filterControlSizing}
`;

export const DateTimeInput = styled(TextField).attrs({ type: "datetime-local" })`
  ${filterControlSizing}
`;

export const ClearFiltersButton = styled(Button)`
  min-height: ${tokens.shape.button.minHeight};
  white-space: nowrap;
`;

export const TableScroller = styled.div`
  min-width: 0;
  overflow-x: auto;
`;
