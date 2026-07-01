import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../core/api_endpoints.dart';
import '../models/user.dart';

class AuthSession {
  const AuthSession({
    required this.user,
    required this.token,
    required this.expiresAt,
  });

  final AppUser user;
  final String token;
  final DateTime expiresAt;
}

class AuthService {
  AuthService({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  static const _tokenKey = 'poseidon.accessToken';
  static const _expiresKey = 'poseidon.expiresAt';

  Future<AuthSession?> restore() async {
    final token = await _storage.read(key: _tokenKey);
    final expiresRaw = await _storage.read(key: _expiresKey);
    if (token == null || expiresRaw == null) return null;

    final expiresAt = DateTime.tryParse(expiresRaw)?.toLocal();
    if (expiresAt == null || expiresAt.isBefore(DateTime.now())) {
      await clear();
      return null;
    }

    final user = AppUser.fromJson(await apiGetCurrentUser(token));
    return AuthSession(user: user, token: token, expiresAt: expiresAt);
  }

  Future<AuthSession> login(String email, String password) async {
    final data = await apiLogin(email, password);
    return _persistAuth(data);
  }

  Future<AuthSession> register(
    String email,
    String password,
    String displayName,
  ) async {
    final data = await apiRegister(email, password, displayName);
    return _persistAuth(data);
  }

  Future<void> logout(String? token) async {
    if (token != null) {
      try {
        await apiLogout(token);
      } catch (_) {
        // Logout remains a local session clear if the network call fails.
      }
    }
    await clear();
  }

  Future<void> clear() async {
    await _storage.delete(key: _tokenKey);
    await _storage.delete(key: _expiresKey);
  }

  Future<AuthSession> _persistAuth(Map<String, dynamic> data) async {
    final token = data['accessToken'] as String;
    final expiresAt = DateTime.parse(data['expiresAt'] as String).toLocal();
    final user = AppUser.fromJson(data);
    await _storage.write(key: _tokenKey, value: token);
    await _storage.write(key: _expiresKey, value: expiresAt.toIso8601String());
    return AuthSession(user: user, token: token, expiresAt: expiresAt);
  }
}
