const TOKEN_KEY = "taskhub_token";
const EMAIL_KEY = "taskhub_email";

// Reads the saved login from the browser. Storage can be blocked, so every call is guarded.
export function loadSession() {
  try {
    return {
      token: localStorage.getItem(TOKEN_KEY),
      email: localStorage.getItem(EMAIL_KEY) ?? "",
    };
  } catch {
    return { token: null, email: "" };
  }
}

export function saveSession(token, email) {
  try {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(EMAIL_KEY, email);
  } catch {
    // storage blocked: the login simply won't survive a page refresh
  }
}

export function clearSession() {
  try {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EMAIL_KEY);
  } catch {
    // nothing to clear
  }
}