import 'package:flutter/foundation.dart';

import '../core/api_exception.dart';
import 'auth_service.dart';
import 'auth_state.dart';

class AuthNotifier extends ChangeNotifier {
  AuthNotifier({AuthService? service}) : _service = service ?? AuthService() {
    restoreSession();
  }

  final AuthService _service;
  AuthState _state = AuthState.booting;

  AuthState get state => _state;
  bool get isAuthenticated => _state.isAuthenticated;
  String? get token => _state.token;

  Future<void> restoreSession() async {
    try {
      final session = await _service.restore();
      _state = session == null
          ? AuthState.unauthenticated
          : AuthState(
              status: AuthStatus.authenticated,
              user: session.user,
              token: session.token,
              expiresAt: session.expiresAt,
            );
    } catch (_) {
      await _service.clear();
      _state = AuthState.unauthenticated;
    }
    notifyListeners();
  }

  Future<bool> login(String email, String password) async {
    return _authenticate(() => _service.login(email, password));
  }

  Future<bool> register(String email, String password, String displayName) {
    return _authenticate(() => _service.register(email, password, displayName));
  }

  Future<void> logout() async {
    final currentToken = _state.token;
    _state = AuthState.unauthenticated;
    notifyListeners();
    await _service.logout(currentToken);
  }

  Future<bool> _authenticate(Future<AuthSession> Function() action) async {
    _state = _state.copyWith(status: AuthStatus.busy, errorMessage: null);
    notifyListeners();
    try {
      final session = await action();
      _state = AuthState(
        status: AuthStatus.authenticated,
        user: session.user,
        token: session.token,
        expiresAt: session.expiresAt,
      );
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      _state = AuthState.unauthenticated.copyWith(errorMessage: error.message);
    } catch (_) {
      _state = AuthState.unauthenticated.copyWith(
        errorMessage: 'Something went wrong. Please try again.',
      );
    }
    notifyListeners();
    return false;
  }
}
