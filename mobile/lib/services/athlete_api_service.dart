import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../models/athlete_profile.dart';

/// Thin HTTP client for the athlete-facing slice of SMF.Api.
///
/// This file intentionally stays very small — no bearer token handling,
/// no retry ladder. The broader identity story (real IdP, refresh
/// tokens, etc.) lives in [AuthApiService] and will be layered on top.
class AthleteApiService {
  final String baseUrl;
  final http.Client _client;

  AthleteApiService({required this.baseUrl, http.Client? client})
      : _client = client ?? http.Client();

  /// POST /api/members/{id}/digital-id. Asks the server to issue a
  /// signed Digital ID token for the athlete; returns both the profile
  /// and the token stamped with `cachedAtUtc = now`.
  ///
  /// Maps server errors onto typed exceptions so callers can show
  /// accurate UI without parsing strings:
  ///   * 404 → [AthleteNotFoundException]
  ///   * any other non-2xx → [AthleteApiException]
  ///   * IO / socket failure → [AthleteNetworkException]
  Future<AthleteProfile> issueDigitalId(String memberId) async {
    final uri = Uri.parse('$baseUrl/api/members/$memberId/digital-id');
    http.Response response;
    try {
      response = await _client
          .post(uri, headers: const {
            'Accept': 'application/json',
            'Content-Length': '0',
          })
          .timeout(const Duration(seconds: 8));
    } on SocketException catch (e) {
      throw AthleteNetworkException(e.message);
    } on HttpException catch (e) {
      throw AthleteNetworkException(e.message);
    } catch (e) {
      // TimeoutException, TLS errors, etc. — treat as recoverable
      // network issues so the UI can suggest "you appear to be offline"
      // rather than a hard error.
      throw AthleteNetworkException(e.toString());
    }

    if (response.statusCode == 404) {
      throw AthleteNotFoundException(memberId);
    }
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw AthleteApiException(response.statusCode, response.body);
    }

    final decoded = jsonDecode(response.body);
    if (decoded is! Map<String, dynamic>) {
      throw AthleteApiException(
        response.statusCode,
        'Unexpected response shape: ${response.body}',
      );
    }

    return AthleteProfile.fromIssueResult(
      decoded,
      cachedAtUtc: DateTime.now().toUtc(),
    );
  }

  void dispose() => _client.close();
}

/// Raised when the server rejects the id — don't silently cache an
/// empty profile, ask the user to log in again.
class AthleteNotFoundException implements Exception {
  final String memberId;
  const AthleteNotFoundException(this.memberId);
  @override
  String toString() => 'Member $memberId was not found.';
}

/// Generic non-2xx (that isn't 404).
class AthleteApiException implements Exception {
  final int statusCode;
  final String body;
  const AthleteApiException(this.statusCode, this.body);
  @override
  String toString() => 'AthleteApi error $statusCode: $body';
}

/// Socket / DNS / TLS / timeout — the caller should fall back to cache
/// and surface the offline banner rather than treat this as fatal.
class AthleteNetworkException implements Exception {
  final String details;
  const AthleteNetworkException(this.details);
  @override
  String toString() => 'Network error: $details';
}
