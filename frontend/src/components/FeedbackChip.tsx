import { Chip } from "@equinor/eds-core-react";

export default function FeedbackChip({ isCorrect }: { isCorrect: boolean }) {
  return (
    <Chip variant={isCorrect ? "active" : "error"}>
      {isCorrect ? "Correct" : "Incorrect"}
    </Chip>
  );
}
