// Turns a time from the API into a readable time in the viewer's own timezone.
// The API stores UTC. If a time arrives without a zone marker, we add "Z" so the
// browser doesn't mistake it for local time and show it hours off.
export function formatTime(value) {
  if (!value) return "-";

  const hasZone = /[zZ]$|[+-]\d{2}:?\d{2}$/.test(value);
  const date = new Date(hasZone ? value : `${value}Z`);

  return date.toLocaleString();
}