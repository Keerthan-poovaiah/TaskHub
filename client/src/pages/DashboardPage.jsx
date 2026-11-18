import { useCallback, useEffect, useState } from "react";
import { ApiError } from "../api/httpClient";
import { getRuns } from "../api/runsApi";
import { createTask, getTasks, pauseTask, resumeTask, runTask } from "../api/tasksApi";
import CreateTaskForm from "../components/CreateTaskForm";
import RunsTable from "../components/RunsTable";
import TasksTable from "../components/TasksTable";

const REFRESH_MS = 5000; // ask the server for fresh data every 5 seconds

export default function DashboardPage({ token, email, onLogout }) {
  const [tasks, setTasks] = useState([]);
  const [runs, setRuns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busyId, setBusyId] = useState(null); // which task's button is working right now

  // A 401 means the ticket expired or is invalid: go back to the login page.
  // Anything else is shown as a message.
  const handleError = useCallback(
    (err) => {
      if (err instanceof ApiError && err.status === 401) {
        onLogout();
        return;
      }
      setError(err.message);
    },
    [onLogout]
  );

  // Fetch tasks and runs together
  const load = useCallback(async () => {
    try {
      const [taskList, runList] = await Promise.all([getTasks(token), getRuns(token)]);
      setTasks(taskList);
      setRuns(runList);
      setError("");
    } catch (err) {
      handleError(err);
    } finally {
      setLoading(false);
    }
  }, [token, handleError]);

  // Load once, then keep refreshing. The cleanup stops the timer when the page closes.
  useEffect(() => {
    load();

    const timer = setInterval(() => {
      if (!document.hidden) load(); // don't bother while the tab is in the background
    }, REFRESH_MS);

    return () => clearInterval(timer);
  }, [load]);

  // Used by the form. Errors are passed back to the form, except 401.
  async function handleCreate(payload) {
    try {
      await createTask(token, payload);
      await load();
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        onLogout();
        return;
      }
      throw err;
    }
  }

  // Used by the row buttons: disable that row while working, then refresh
  async function act(taskId, action) {
    setBusyId(taskId);
    setError("");

    try {
      await action(token, taskId);
      await load();
    } catch (err) {
      handleError(err);
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="page">
      <header className="topbar">
        <h1>TaskHub</h1>
        <div className="topbar-right">
          <span className="muted">{email}</span>
          <button className="secondary" onClick={onLogout}>
            Log out
          </button>
        </div>
      </header>

      {error && <p className="banner-error">{error}</p>}

      <CreateTaskForm onCreate={handleCreate} />

      <section className="card">
        <div className="card-head">
          <h2>Your tasks</h2>
        </div>
        {loading ? (
          <p className="muted">Loading...</p>
        ) : (
          <TasksTable
            tasks={tasks}
            busyId={busyId}
            onRun={(id) => act(id, runTask)}
            onPause={(id) => act(id, pauseTask)}
            onResume={(id) => act(id, resumeTask)}
          />
        )}
      </section>

      <section className="card">
        <div className="card-head">
          <h2>Recent runs</h2>
          <span className="muted">Updates every 5 seconds</span>
        </div>
        {loading ? <p className="muted">Loading...</p> : <RunsTable runs={runs} />}
      </section>
    </div>
  );
}