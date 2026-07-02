import 'dart:io';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

import '../auth/auth_notifier.dart';
import 'api_endpoints.dart';

@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp();
}

class PushNotificationService {
  PushNotificationService({
    FirebaseMessaging? messaging,
    FlutterLocalNotificationsPlugin? localNotifications,
  }) : _localNotifications =
           localNotifications ?? FlutterLocalNotificationsPlugin() {
    _messaging = messaging;
  }

  FirebaseMessaging? _messaging;
  final FlutterLocalNotificationsPlugin _localNotifications;

  AuthNotifier? _auth;
  bool _ready = false;
  String? _registeredApiToken;
  String? _registeredPushToken;

  static const AndroidNotificationChannel _androidChannel =
      AndroidNotificationChannel(
        'poseidon_events',
        'Poseidon events',
        description: 'Event registration and status updates from Poseidon.',
        importance: Importance.high,
      );

  Future<void> initialize(AuthNotifier auth) async {
    if (!Platform.isAndroid) return;
    _auth = auth;

    try {
      await Firebase.initializeApp();
      final messaging = _messaging ??= FirebaseMessaging.instance;
      FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
      await _configureLocalNotifications();
      await messaging.requestPermission();
      await messaging.setForegroundNotificationPresentationOptions(
        alert: true,
        badge: true,
        sound: true,
      );

      FirebaseMessaging.onMessage.listen(_showForegroundNotification);
      messaging.onTokenRefresh.listen(_registerToken);
      auth.addListener(_syncWithAuth);
      _ready = true;
      await _syncWithAuth();
    } catch (error) {
      debugPrint('Push notifications disabled: $error');
    }
  }

  Future<void> dispose() async {
    _auth?.removeListener(_syncWithAuth);
  }

  Future<void> _configureLocalNotifications() async {
    await _localNotifications
        .resolvePlatformSpecificImplementation<
          AndroidFlutterLocalNotificationsPlugin
        >()
        ?.createNotificationChannel(_androidChannel);

    await _localNotifications.initialize(
      const InitializationSettings(
        android: AndroidInitializationSettings('@mipmap/ic_launcher'),
      ),
    );
  }

  Future<void> _syncWithAuth() async {
    if (!_ready) return;

    final auth = _auth;
    if (auth == null) return;

    if (!auth.isAuthenticated) {
      await _revokeRegisteredToken();
      return;
    }

    final messaging = _messaging;
    if (messaging == null) return;

    final pushToken = await messaging.getToken();
    if (pushToken != null) {
      await _registerToken(pushToken);
    }
  }

  Future<void> _registerToken(String pushToken) async {
    final auth = _auth;
    final apiToken = auth?.token;
    if (auth == null || !auth.isAuthenticated || apiToken == null) return;

    try {
      await apiRegisterPushToken(apiToken, pushToken);
      _registeredApiToken = apiToken;
      _registeredPushToken = pushToken;
    } catch (error) {
      debugPrint('Push token registration failed: $error');
    }
  }

  Future<void> _revokeRegisteredToken() async {
    final apiToken = _registeredApiToken;
    final pushToken = _registeredPushToken;
    _registeredApiToken = null;
    _registeredPushToken = null;

    if (apiToken == null || pushToken == null) return;

    try {
      await apiRevokePushToken(apiToken, pushToken);
    } catch (error) {
      debugPrint('Push token revocation failed: $error');
    }
  }

  Future<void> _showForegroundNotification(RemoteMessage message) async {
    final notification = message.notification;
    final android = notification?.android;
    if (notification == null || android == null) return;

    await _localNotifications.show(
      notification.hashCode,
      notification.title,
      notification.body,
      NotificationDetails(
        android: AndroidNotificationDetails(
          _androidChannel.id,
          _androidChannel.name,
          channelDescription: _androidChannel.description,
          icon: android.smallIcon ?? '@mipmap/ic_launcher',
          importance: Importance.high,
          priority: Priority.high,
        ),
      ),
      payload: message.data['eventId'],
    );
  }
}
