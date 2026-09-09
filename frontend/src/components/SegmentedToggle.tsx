import { Button, ButtonGroup } from "@equinor/eds-core-react"

export interface SegmentedToggleOption<T extends string> {
    value: T
    label: string
}

export interface SegmentedToggleProps<T extends string> {
    options: SegmentedToggleOption<T>[]
    value: T
    onChange: (value: T) => void
}

export default function SegmentedToggle<T extends string>({
    options,
    value,
    onChange,
}: SegmentedToggleProps<T>) {
    return (
        <ButtonGroup>
            {options.map((option) => (
                <Button
                    key={option.value}
                    variant={option.value === value ? "contained" : "outlined"}
                    onClick={() => onChange(option.value)}
                >
                    {option.label}
                </Button>
            ))}
        </ButtonGroup>
    )
}
