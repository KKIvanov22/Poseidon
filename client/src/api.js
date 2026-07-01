// Base URL of the Poseidon API. Override by setting REACT_APP_API_BASE_URL
// in a .env file at the client root if your server runs somewhere else.
const API_BASE_URL = (process.env.REACT_APP_API_BASE_URL || 'http://localhost:8080').replace(/\/$/, '');

class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
    this.name = 'ApiError';
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
  } catch {
    throw new ApiError('Could not reach the Poseidon API. Is the server running?', 0);
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

// Auth
export const login = (email, password) =>
  request('/auth/login', { method: 'POST', body: { email, password } });

export const register = (email, password, displayName) =>
  request('/auth/register', { method: 'POST', body: { email, password, displayName } });

export const logout = (token) =>
  request('/auth/logout', { method: 'POST', token });

// Events
export const getEvents = (token) => request('/events', { token });

export const getMyEvents = (token) => request('/events/mine', { token });

export const createEvent = (token, event) =>
  request('/events', { method: 'POST', token, body: event });

export const updateEvent = (token, eventId, event) =>
  request(`/events/${eventId}`, { method: 'PUT', token, body: event });

export const publishEvent = (token, eventId) =>
  request(`/events/${eventId}/publish`, { method: 'POST', token });

export const cancelEvent = (token, eventId) =>
  request(`/events/${eventId}/cancel`, { method: 'POST', token });

export const registerForEvent = (token, eventId) =>
  request(`/events/${eventId}/register`, { method: 'POST', token });

export const getEventRegistrations = (token, eventId) =>
  request(`/events/${eventId}/registrations`, { token });

// Registrations
export const getMyRegistrations = (token) => request('/registrations/me', { token });

export const cancelRegistration = (token, registrationId) =>
  request(`/registrations/${registrationId}`, { method: 'DELETE', token });

// Users
export const getCurrentUser = (token) => request('/users/me', { token });

export const listUsers = (token) => request('/users', { token });

export const updateUserRole = (token, userId, role) =>
  request(`/users/${userId}/role`, { method: 'PATCH', token, body: { role } });

// Admin notification jobs
export const getPendingNotificationJobs = (token, limit = 25) =>
  request(`/notifications/jobs/pending?limit=${limit}`, { token });

export const completeNotificationJob = (token, jobId) =>
  request(`/notifications/jobs/${jobId}/complete`, { method: 'POST', token });

export const retryNotificationJob = (token, jobId) =>
  request(`/notifications/jobs/${jobId}/retry`, { method: 'POST', token });

export { ApiError };
