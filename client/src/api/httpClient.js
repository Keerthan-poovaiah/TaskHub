// Where the API lives. Only public values may go in VITE_ variables,
// because everything in them is shipped to the browser.
const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5080";

// A clearer kind of error that remembers the HTTP status (401, 404, ...)
export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

// The API answers errors in a few shapes: a JSON string, {"error": "..."}, or plain text.
// This turns any of them into one readable message.
async function readError(response) {
  const text = await response.text();
  if (!text) return `Request failed (${response.status}).`;

  try {
    const data = JSON.parse(text);
    if (typeof data === "string") return data;
    if (data.error) return data.error;
    if (data.title) return data.title;
  } catch {
    // not JSON: fall through and use the raw text
  }
  return text;
}

// Every call goes through here, so the ticket and the error handling live in one place.
export async function request(path, { method = "GET", body, token } = {}) {
  const headers = {};
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (token) headers["Authorization"] = `Bearer ${token}`;

  let response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    // The request never got an answer: API is off, or the browser blocked it (CORS)
    throw new ApiError("Cannot reach the server. Is the API running?", 0);
  }

  if (!response.ok) {
    throw new ApiError(await readError(response), response.status);
  }

  const contentType = response.headers.get("content-type") ?? "";
  return contentType.includes("json") ? response.json() : null;
}