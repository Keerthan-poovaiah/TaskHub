// One colored label per run status. The CSS class decides the color.
export default function StatusBadge({ status }) {
  return <span className={`badge badge-${String(status).toLowerCase()}`}>{status}</span>;
}