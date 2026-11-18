import { useState } from "react";

// Friendly choices that turn into cron expressions. All cron times are UTC.
// India is UTC+5:30, so 8:00 AM India time is 02:30 UTC.
const SCHEDULES = [
  { id: "once", label: "Run once, right now", cron: null },
  { id: "minute", label: "Every minute", cron: "* * * * *" },
  { id: "five", label: "Every 5 minutes", cron: "*/5 * * * *" },
  { id: "hour", label: "Every hour", cron: "0 * * * *" },
  { id: "daily-india", label: "Every day at 8:00 AM India time", cron: "30 2 * * *" },
  { id: "custom", label: "Custom cron expression (UTC)...", cron: "" },
];

export default function CreateTaskForm({ onCreate }) {
  const [name, setName] = useState("");
  const [type, setType] = useState("CheckUrl");
  const [target, setTarget] = useState("");
  const [scheduleId, setScheduleId] = useState("once");
  const [customCron, setCustomCron] = useState("");
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  const isEmail = type === "SendEmail";

  async function handleSubmit(event) {
    event.preventDefault();
    setError("");
    setMessage("");

    const chosen = SCHEDULES.find((s) => s.id === scheduleId);
    const cron = scheduleId === "custom" ? customCron.trim() : chosen.cron;

    if (scheduleId === "custom" && !cron) {
      setError("Enter a cron expression, for example 0 8 * * MON");
      return;
    }

    setBusy(true);
    try {
      // "cron" is only sent when there is one
      await onCreate({
        name: name.trim(),
        type,
        target: target.trim(),
        ...(cron ? { cron } : {}),
      });

      setName("");
      setTarget("");
      setMessage(cron ? "Task scheduled." : "Task created and queued.");
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="card" onSubmit={handleSubmit}>
      <h2>Create a task</h2>

      <div className="form-grid">
        <label>
          Name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Monitor my website"
            maxLength={100}
            required
          />
        </label>

        <label>
          What should it do?
          <select
            value={type}
            onChange={(e) => {
              setType(e.target.value);
              setTarget("");
            }}
          >
            <option value="CheckUrl">Check that a website is up</option>
            <option value="SendEmail">Send me an email</option>
          </select>
        </label>

        <label>
          {isEmail ? "Email address" : "Website address"}
          <input
            // The browser checks the format for us. This is only for convenience:
            // the API repeats the check, because a browser can be bypassed.
            type={isEmail ? "email" : "url"}
            value={target}
            onChange={(e) => setTarget(e.target.value)}
            placeholder={isEmail ? "you@example.com" : "https://example.com"}
            required
          />
        </label>

        <label>
          When?
          <select value={scheduleId} onChange={(e) => setScheduleId(e.target.value)}>
            {SCHEDULES.map((s) => (
              <option key={s.id} value={s.id}>
                {s.label}
              </option>
            ))}
          </select>
        </label>

        {scheduleId === "custom" && (
          <label className="wide">
            Cron expression (minute hour day month weekday, in UTC)
            <input
              value={customCron}
              onChange={(e) => setCustomCron(e.target.value)}
              placeholder="0 8 * * MON"
            />
          </label>
        )}
      </div>

      {error && <p className="error">{error}</p>}
      {message && <p className="success">{message}</p>}

      <div>
        <button type="submit" disabled={busy}>
          {busy ? "Creating..." : "Create task"}
        </button>
      </div>
    </form>
  );
}