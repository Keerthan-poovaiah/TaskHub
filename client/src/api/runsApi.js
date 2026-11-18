import { request } from "./httpClient";

export const getRuns = (token) => request("/runs", { token });