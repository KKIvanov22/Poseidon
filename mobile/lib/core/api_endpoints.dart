import 'api_client.dart';

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

Future<Map<String, dynamic>> apiLogin(String email, String password) async {
  final data = await ApiClient.request(
    '/auth/login',
    method: 'POST',
    body: {'email': email, 'password': password},
  );
  return data as Map<String, dynamic>;
}

Future<Map<String, dynamic>> apiRegister(
  String email,
  String password,
  String displayName,
) async {
  final data = await ApiClient.request(
    '/auth/register',
    method: 'POST',
    body: {'email': email, 'password': password, 'displayName': displayName},
  );
  return data as Map<String, dynamic>;
}

Future<void> apiLogout(String token) async {
  await ApiClient.request('/auth/logout', method: 'POST', token: token);
}

// ---------------------------------------------------------------------------
// Events
// ---------------------------------------------------------------------------

Future<List<dynamic>> apiGetEvents(String token) async {
  final data = await ApiClient.request('/events', token: token);
  return data as List<dynamic>;
}

Future<List<dynamic>> apiGetMyEvents(String token) async {
  final data = await ApiClient.request('/events/mine', token: token);
  return data as List<dynamic>;
}

Future<Map<String, dynamic>> apiCreateEvent(
  String token,
  Map<String, dynamic> event,
) async {
  final data = await ApiClient.request(
    '/events',
    method: 'POST',
    token: token,
    body: event,
  );
  return data as Map<String, dynamic>;
}

Future<Map<String, dynamic>> apiUpdateEvent(
  String token,
  String eventId,
  Map<String, dynamic> event,
) async {
  final data = await ApiClient.request(
    '/events/$eventId',
    method: 'PUT',
    token: token,
    body: event,
  );
  return data as Map<String, dynamic>;
}

Future<Map<String, dynamic>> apiPublishEvent(
  String token,
  String eventId,
) async {
  final data = await ApiClient.request(
    '/events/$eventId/publish',
    method: 'POST',
    token: token,
  );
  return data as Map<String, dynamic>;
}

Future<Map<String, dynamic>> apiCancelEvent(
  String token,
  String eventId,
) async {
  final data = await ApiClient.request(
    '/events/$eventId/cancel',
    method: 'POST',
    token: token,
  );
  return data as Map<String, dynamic>;
}

Future<Map<String, dynamic>> apiRegisterForEvent(
  String token,
  String eventId,
) async {
  final data = await ApiClient.request(
    '/events/$eventId/register',
    method: 'POST',
    token: token,
  );
  return data as Map<String, dynamic>;
}

Future<Map<String, dynamic>> apiGetEventRegistrations(
  String token,
  String eventId,
) async {
  final data = await ApiClient.request(
    '/events/$eventId/registrations',
    token: token,
  );
  return data as Map<String, dynamic>;
}

// ---------------------------------------------------------------------------
// Registrations
// ---------------------------------------------------------------------------

Future<List<dynamic>> apiGetMyRegistrations(String token) async {
  final data = await ApiClient.request('/registrations/me', token: token);
  return data as List<dynamic>;
}

Future<dynamic> apiCancelRegistration(
  String token,
  String registrationId,
) async {
  return ApiClient.request(
    '/registrations/$registrationId',
    method: 'DELETE',
    token: token,
  );
}

// ---------------------------------------------------------------------------
// Users
// ---------------------------------------------------------------------------

Future<Map<String, dynamic>> apiGetCurrentUser(String token) async {
  final data = await ApiClient.request('/users/me', token: token);
  return data as Map<String, dynamic>;
}

Future<List<dynamic>> apiListUsers(String token) async {
  final data = await ApiClient.request('/users', token: token);
  return data as List<dynamic>;
}

Future<Map<String, dynamic>> apiUpdateUserRole(
  String token,
  String userId,
  String role,
) async {
  final data = await ApiClient.request(
    '/users/$userId/role',
    method: 'PATCH',
    token: token,
    body: {'role': role},
  );
  return data as Map<String, dynamic>;
}

// ---------------------------------------------------------------------------
// Notification Jobs
// ---------------------------------------------------------------------------

Future<List<dynamic>> apiGetPendingJobs(String token, {int limit = 25}) async {
  final data = await ApiClient.request(
    '/notifications/jobs/pending?limit=$limit',
    token: token,
  );
  return data as List<dynamic>;
}

Future<void> apiCompleteJob(String token, String jobId) async {
  await ApiClient.request(
    '/notifications/jobs/$jobId/complete',
    method: 'POST',
    token: token,
  );
}

Future<void> apiRetryJob(String token, String jobId) async {
  await ApiClient.request(
    '/notifications/jobs/$jobId/retry',
    method: 'POST',
    token: token,
  );
}
