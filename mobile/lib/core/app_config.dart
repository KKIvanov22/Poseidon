/// Single source of truth for the API base URL.
///
/// Set at build / run time via --dart-define:
///   flutter run --dart-define=API_BASE_URL=http://192.168.1.10:8080
///
/// Defaults to the Android-emulator loopback alias (10.0.2.2) so the app
/// works against a locally running docker-compose stack out of the box.
class AppConfig {
  AppConfig._();

  static const String baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080',
  );
}
