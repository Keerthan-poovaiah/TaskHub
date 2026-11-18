import { useState } from "react";
import { login, register } from "../api/authApi";

export default function AuthPage({ onLoggedIn }) {
  const [mode, setMode] = useState("login"); // "login" or "register"
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const isRegister = mode === "register";

  async function handleSubmit(event) {
    event.preventDefault(); // stop the browser from reloading the page
    setError("");
    setBusy(true);

    try {
      if (isRegister) {
        await register(email, password);
      }
      // After signing up we log straight in, so the user doesn't type everything twice
      const result = await login(email, password);
      onLoggedIn(result.token, email.trim().toLowerCase());
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  function switchMode() {
    setMode(isRegister ? "login" : "register");
    setError("");
  }

  return (
    <div className="auth-wrap">
      <form className="card auth-card" onSubmit={handleSubmit}>
        <h1>TaskHub</h1>
        <p className="muted">
          {isRegister ? "Create your account" : "Log in to your dashboard"}
        </p>

        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            autoComplete="email"
            required
          />
        </label>

        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={isRegister ? "new-password" : "current-password"}
            minLength={8}
            required
          />
        </label>

        {error && <p className="error">{error}</p>}

        <button type="submit" disabled={busy}>
          {busy ? "Please wait..." : isRegister ? "Sign up" : "Log in"}
        </button>

        <button type="button" className="link" onClick={switchMode}>
          {isRegister ? "I already have an account" : "Create an account"}
        </button>
      </form>
    </div>
  );
}