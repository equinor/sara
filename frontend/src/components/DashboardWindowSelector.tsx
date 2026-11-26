import { useState, type FormEvent } from "react";
import { Button, Chip, Dialog, TextField } from "@equinor/eds-core-react";
import styled from "styled-components";
import {
  MAX_DASHBOARD_WINDOW_DAYS,
  MIN_DASHBOARD_WINDOW_DAYS,
  type DashboardWindowPreset,
} from "../api/client";

const WindowToggle = styled.div`
  display: flex;
  gap: 0.5rem;
  align-items: center;
`;

const DialogActions = styled(Dialog.Actions)`
  display: flex;
  gap: 0.5rem;
`;

// EDS puts its labels on an inner element; name the native <dialog> after the EDS title.
const labelNativeDialog = (element: HTMLDialogElement | null) =>
  element?.setAttribute("aria-labelledby", "eds-dialog-title");

function validateCustomDays(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === "") return "Enter a number of days.";
  if (!/^\d+$/.test(trimmed)) return "Enter a whole number of days.";
  const days = Number(trimmed);
  if (days < MIN_DASHBOARD_WINDOW_DAYS || days > MAX_DASHBOARD_WINDOW_DAYS) {
    return `Enter a number between ${MIN_DASHBOARD_WINDOW_DAYS} and ${MAX_DASHBOARD_WINDOW_DAYS}.`;
  }
  return null;
}

export default function DashboardWindowSelector({
  windows,
  windowHours,
  onChange,
  "aria-label": ariaLabel,
}: {
  windows: readonly DashboardWindowPreset[];
  windowHours: number;
  onChange: (hours: number) => void;
  "aria-label"?: string;
}) {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [daysInput, setDaysInput] = useState("");
  const [touched, setTouched] = useState(false);
  const isCustom = !windows.some((item) => item.hours === windowHours);
  const validationError = validateCustomDays(daysInput);
  const showError = touched && validationError !== null;

  const openDialog = () => {
    setDaysInput(String(windowHours / 24));
    setTouched(false);
    setDialogOpen(true);
  };

  const closeDialog = () => setDialogOpen(false);

  const apply = (event?: FormEvent) => {
    event?.preventDefault();
    setTouched(true);
    if (validationError !== null) return;
    onChange(Number(daysInput.trim()) * 24);
    setDialogOpen(false);
  };

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
      <Chip variant={isCustom ? "active" : "default"} onClick={openDialog} style={{ cursor: "pointer" }}>
        {isCustom ? `Custom (${windowHours / 24}d)` : "Custom"}
      </Chip>
      <Dialog open={dialogOpen} onClose={closeDialog} isDismissable dialogRef={labelNativeDialog}>
        <form onSubmit={apply} noValidate>
          <Dialog.Header>
            <Dialog.Title>Custom time window</Dialog.Title>
          </Dialog.Header>
          <Dialog.CustomContent>
            <TextField
              id="custom-window-days"
              label="Number of days"
              type="number"
              inputMode="numeric"
              min={MIN_DASHBOARD_WINDOW_DAYS}
              max={MAX_DASHBOARD_WINDOW_DAYS}
              step={1}
              value={daysInput}
              autoFocus
              onChange={(event: React.ChangeEvent<HTMLInputElement>) => setDaysInput(event.target.value)}
              onBlur={() => setTouched(true)}
              variant={showError ? "error" : undefined}
              helperText={
                showError
                  ? validationError
                  : `Whole number from ${MIN_DASHBOARD_WINDOW_DAYS} to ${MAX_DASHBOARD_WINDOW_DAYS}.`
              }
            />
          </Dialog.CustomContent>
          <DialogActions>
            <Button type="submit" disabled={showError}>
              Apply
            </Button>
            <Button type="button" variant="ghost" onClick={closeDialog}>
              Cancel
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </WindowToggle>
  );
}
