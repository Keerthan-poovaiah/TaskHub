import { formatTime } from "../utils/formatTime";

export default function TasksTable({ tasks, busyId, onRun, onPause, onResume }) {
  if (tasks.length === 0) {
    return <p className="muted">No tasks yet. Create one above.</p>;
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Type</th>
            <th>Target</th>
            <th>Schedule (UTC)</th>
            <th>State</th>
            <th>Next run (your time)</th>
            <th>Runs</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {tasks.map((task) => {
            const scheduled = Boolean(task.cronExpression);
            const busy = busyId === task.id;

            return (
              <tr key={task.id}>
                <td>{task.name}</td>
                <td>{task.type}</td>
                <td className="break">{task.target}</td>
                <td>{task.cronExpression ?? "one-off"}</td>
                <td>{scheduled ? (task.isActive ? "Active" : "Paused") : "One-off"}</td>
                <td>{scheduled && task.isActive ? formatTime(task.nextRunAtUtc) : "-"}</td>
                <td>{task.runCount}</td>
                <td>
                  <div className="actions">
                    <button
                      className="small secondary"
                      disabled={busy}
                      onClick={() => onRun(task.id)}
                    >
                      Run now
                    </button>

                    {scheduled &&
                      (task.isActive ? (
                        <button
                          className="small secondary"
                          disabled={busy}
                          onClick={() => onPause(task.id)}
                        >
                          Pause
                        </button>
                      ) : (
                        <button
                          className="small"
                          disabled={busy}
                          onClick={() => onResume(task.id)}
                        >
                          Resume
                        </button>
                      ))}
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}