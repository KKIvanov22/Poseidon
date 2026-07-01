import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../auth/auth_notifier.dart';
import '../auth/auth_state.dart';
import '../features/admin/admin_screen.dart';
import '../features/auth/login_screen.dart';
import '../features/auth/signup_screen.dart';
import '../features/student/student_screen.dart';
import '../features/teacher/teacher_screen.dart';

GoRouter createAppRouter(AuthNotifier auth) {
  return GoRouter(
    initialLocation: '/login',
    refreshListenable: auth,
    redirect: (context, state) {
      final authState = auth.state;
      final loggingIn = state.matchedLocation == '/login';
      final signingUp = state.matchedLocation == '/signup';
      final publicRoute = loggingIn || signingUp;

      if (authState.status == AuthStatus.booting) return null;
      if (!authState.isAuthenticated) return publicRoute ? null : '/login';
      if (publicRoute) return _homeForRole(authState.role);
      return null;
    },
    routes: [
      GoRoute(
        path: '/login',
        builder: (context, state) => LoginScreen(auth: auth),
      ),
      GoRoute(
        path: '/signup',
        builder: (context, state) => SignupScreen(auth: auth),
      ),
      GoRoute(
        path: '/student',
        builder: (context, state) => StudentScreen(auth: auth),
      ),
      GoRoute(
        path: '/teacher',
        builder: (context, state) => TeacherScreen(auth: auth),
      ),
      GoRoute(
        path: '/admin',
        builder: (context, state) => AdminScreen(auth: auth),
      ),
    ],
    errorBuilder: (context, state) => Scaffold(
      body: Center(
        child: FilledButton(
          onPressed: () => context.go(_homeForRole(auth.state.role)),
          child: const Text('Return to Poseidon'),
        ),
      ),
    ),
  );
}

String _homeForRole(String role) => switch (role) {
  'admin' => '/admin',
  'teacher' => '/teacher',
  _ => '/student',
};
