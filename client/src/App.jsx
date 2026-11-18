import { useCallback, useState } from "react";
import AuthPage from "./pages/AuthPage";
import DashboardPage from "./pages/DashboardPage";
import { clearSession, loadSession, saveSession } from "./utils/session";

export default function App() {
  // Start from whatever the browser remembered, so a page refresh keeps you logged in
  const [session, setSession] = useState(loadSession);

  function handleLoggedIn(token, email) {
    saveSession(token, email);
    setSession({ token, email });
  }

  // useCallback keeps this function the same object between renders,
  // so the dashboard doesn't reload itself in a loop
  const handleLogout = useCallback(() => {
    clearSession();
    setSession({ token: null, email: "" });
  }, []);

  if (!session.token) {
    return <AuthPage onLoggedIn={handleLoggedIn} />;
  }

  return (
    <DashboardPage token={session.token} email={session.email} onLogout={handleLogout} />
  );
}