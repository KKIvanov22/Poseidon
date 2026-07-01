import '../models/user.dart';

class AuthState {
  const AuthState({
    required this.status,
    this.user,
    this.token,
    this.expiresAt,
    this.errorMessage,
  });

  final AuthStatus status;
  final AppUser? user;
  final String? token;
  final DateTime? expiresAt;
  final String? errorMessage;

  bool get isAuthenticated =>
      status == AuthStatus.authenticated &&
      token != null &&
      user != null &&
      (expiresAt == null || expiresAt!.isAfter(DateTime.now()));

  String get role => user?.normalizedRole ?? '';

  AuthState copyWith({
    AuthStatus? status,
    AppUser? user,
    String? token,
    DateTime? expiresAt,
    String? errorMessage,
    bool clearSession = false,
  }) => AuthState(
    status: status ?? this.status,
    user: clearSession ? null : user ?? this.user,
    token: clearSession ? null : token ?? this.token,
    expiresAt: clearSession ? null : expiresAt ?? this.expiresAt,
    errorMessage: errorMessage,
  );

  static const booting = AuthState(status: AuthStatus.booting);
  static const unauthenticated = AuthState(status: AuthStatus.unauthenticated);
}

enum AuthStatus { booting, unauthenticated, authenticated, busy }
