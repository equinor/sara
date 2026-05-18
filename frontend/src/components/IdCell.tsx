type Props = { id: string };

/**
 * Renders a full ID in a monospace font for easy identification of rows.
 */
export default function IdCell({ id }: Props) {
  return (
    <span title={id} style={{ fontFamily: "monospace", fontSize: "0.85em" }}>
      {id}
    </span>
  );
}
