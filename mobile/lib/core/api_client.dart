import 'dart:convert';
import 'package:http/http.dart' as http;
import 'app_config.dart';
import 'api_exception.dart';

/// Low-level HTTP client. All API calls should go through [ApiClient.request].
class ApiClient {
  ApiClient._();

  static final _base = Uri.parse(
    AppConfig.baseUrl.replaceFirst(RegExp(r'/$'), ''),
  );

  /// Makes a request to [path] with optional [method], [token], and [body].
  ///
  /// Throws [ApiException] on non-2xx responses or network failures.
  static Future<dynamic> request(
    String path, {
    String method = 'GET',
    String? token,
    Map<String, dynamic>? body,
  }) async {
    final uri = _buildUri(path);
    final headers = <String, String>{
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    };
    if (token != null) {
      headers['Authorization'] = 'Bearer $token';
    }

    final encoded = body != null ? jsonEncode(body) : null;

    http.Response response;
    try {
      response = await _send(method, uri, headers, encoded);
    } catch (_) {
      throw const ApiException(
        'Could not reach the Poseidon API. Is the server running?',
      );
    }

    if (response.statusCode == 204) return null;

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final text = response.body;
      if (text.isEmpty) return null;
      return jsonDecode(text);
    }

    // Non-success response: try to parse problem-detail
    String message = 'Request failed with status ${response.statusCode}';
    try {
      final problem = jsonDecode(response.body) as Map<String, dynamic>;
      message =
          (problem['detail'] ??
                  problem['title'] ??
                  problem['message'] ??
                  message)
              as String;
    } catch (_) {
      // no JSON body — use generic message
    }
    throw ApiException(message, statusCode: response.statusCode);
  }

  static Future<http.Response> _send(
    String method,
    Uri uri,
    Map<String, String> headers,
    String? body,
  ) {
    switch (method.toUpperCase()) {
      case 'POST':
        return http.post(uri, headers: headers, body: body);
      case 'PUT':
        return http.put(uri, headers: headers, body: body);
      case 'PATCH':
        return http.patch(uri, headers: headers, body: body);
      case 'DELETE':
        return http.delete(uri, headers: headers, body: body);
      default:
        return http.get(uri, headers: headers);
    }
  }

  static Uri _buildUri(String path) {
    final relative = Uri.parse(path.startsWith('/') ? path.substring(1) : path);
    return _base.resolveUri(relative);
  }
}
