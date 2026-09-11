import type { ComponentProps } from "react";
import { DateTimeInput } from "./Styles";

type DateTimeFilterInputProps = Omit<ComponentProps<typeof DateTimeInput>, "value" | "onChange"> & {
  value: string | undefined;
  onChange: (value: string | undefined) => void;
};

export default function DateTimeFilterInput({ value, onChange, ...props }: DateTimeFilterInputProps) {
  const date = value ? new Date(value) : null;
  const localValue = date && !Number.isNaN(date.getTime())
    ? new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
    : "";

  return (
    <DateTimeInput
      {...props}
      value={localValue}
      onChange={(event) => onChange(event.target.value ? new Date(event.target.value).toISOString() : undefined)}
    />
  );
}
