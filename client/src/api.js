// Base URL of the Poseidon API. Override by setting REACT_APP_API_BASE_URL
// in a .env file at the client root if your server runs somewhere else.
const API_BASE_URL = (process.env.REACT_APP_API_BASE_URL || 'http://localhost:8080').replace(/\/$/, '');

class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

async function request(path, { method = 'GET', token, body } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;

  let response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch (networkError) {
    throw new ApiError(
      'Could not reach the Poseidon API. Is the server running?',
      0
    );
  }

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const problem = await response.json();
      message = problem.detail || problem.title || problem.message || message;
    } catch {
      // response had no JSON body; fall back to the generic message
    }
    throw new ApiError(message, response.status);
  }

  if (response.status === 204) return null;

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

export const login = (email, password) =>
  request('/auth/login', { method: 'POST', body: { email, password } });

export const register = (email, password, displayName) =>
  request('/auth/register', {
    method: 'POST',
    body: { email, password, displayName },
  });

export const logout = (token) =>
  request('/auth/logout', { method: 'POST', token });

export const getEvents = (token) => request('/events', { token });

export { ApiError };
