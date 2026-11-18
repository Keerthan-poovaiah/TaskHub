import { formatTime } from "../utils/formatTime";
import StatusBadge from "./StatusBadge";

export default function RunsTable({ runs }) {
  if (runs.length === 0) {
    return <p className="muted">No runs yet. Create a task or press "Run now".</p>;
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Run</th>
            <th>Task</th>
            <th>Status</th>
            <th>Attempts</th>
            <th>Created</th>
            <th>Finished</th>
            <th>Error</th>
          </tr>
        </thead>
        <tbody>
          {runs.map((run) => (
            <tr key={run.id}>
              <td>#{run.id}</td>
              <td>{run.taskName}</td>
              <td>
                <StatusBadge status={run.status} />
              </td>
              <td>{run.attempts}</td>
              <td>{formatTime(run.createdAtUtc)}</td>
              <td>{formatTime(run.finishedAtUtc)}</td>
              <td className="error-cell" title={run.error ?? ""}>
                {run.error ?? ""}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}