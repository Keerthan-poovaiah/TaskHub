import { request } from "./httpClient";

export const getTasks = (token) => request("/tasks", { token });

// payload looks like { name, type, target, cron? } ("cron" is optional)
export const createTask = (token, payload) =>
  request("/tasks", { method: "POST", body: payload, token });

export const runTask = (token, taskId) =>
  request(`/tasks/${taskId}/run`, { method: "POST", token });

export const pauseTask = (token, taskId) =>
  request(`/tasks/${taskId}/pause`, { method: "POST", token });

export const resumeTask = (token, taskId) =>
  request(`/tasks/${taskId}/resume`, { method: "POST", token });