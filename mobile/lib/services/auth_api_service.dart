import 'dart:convert';
import 'package:http/http.dart' as http;

/// Issues a short-lived JWT for the referee tablet. Hits the dev-only
/// `POST /api/auth/dev-token` endpoint on SMF.Api (guarded to
/// Development / Testing environments).
///
/// In production this should be replaced with the real identity provider
/// (Keycloak / Entra ID) — the hub only cares that `Context.UserIdentifier`
/// matches the referee id the tablet sends.
class AuthApiService {
  final String baseUrl;
  final http.Client _client;

  AuthApiService({required this.baseUrl, http.Client? client})
      : _client = client ?? http.Client();

  Future<DevToken> issueDevToken({
    required String refereeId,
    required String displayName,
    List<String> roles = const [],
  }) async {
    final uri = Uri.parse("$baseUrl/api/auth/dev-token");
    final response = await _client.post(
      uri,
      headers: const {
        "Content-Type": "application/json",
        "Accept": "application/json",
      },
      body: jsonEncode({
        "refereeId": refereeId,
        "displayName": displayName,
        "roles": roles,
      }),
    );

    if (response.statusCode != 200) {
      throw AuthException(
        "Dev token endpoint returned ${response.statusCode}: ${response.body}",
      );
    }

    final map = jsonDecode(response.body) as Map<String, dynamic>;
    return DevToken(
      accessToken: map["accessToken"] as String,
      expiresAt: DateTime.parse(map["expiresAt"] as String).toUtc(),
    );
  }

  void dispose() => _client.close();
}

class DevToken {
  final String accessToken;
  final DateTime expiresAt;
  const DevToken({required this.accessToken, required this.expiresAt});
}

class AuthException implements Exception {
  final String message;
  const AuthException(this.message);
  @override
  String toString() => "AuthException: $message";
}
