import 'dart:convert';

/// Registration states used by the venue gate. Mirrors the backend
/// `RegistrationStatus` enum; values are the exact strings the API
/// returns so round-tripping through JSON is loss-less.
enum RegistrationStatus {
  pending('Pending'),
  approved('Approved'),
  active('Active');

  final String wire;
  const RegistrationStatus(this.wire);

  static RegistrationStatus fromWire(String value) {
    for (final s in RegistrationStatus.values) {
      if (s.wire == value) return s;
    }
    // Unknown status from a newer backend — default to pending so the
    // gate doesn't wrongly admit on a value we don't understand.
    return RegistrationStatus.pending;
  }

  /// Human-friendly label shown on the digital ID card (English).
  String get labelEn {
    switch (this) {
      case RegistrationStatus.pending:
        return 'Pending review';
      case RegistrationStatus.approved:
        return 'Approved';
      case RegistrationStatus.active:
        return 'Active';
    }
  }

  /// Arabic label — displayed when the user flips the card to العربية.
  String get labelAr {
    switch (this) {
      case RegistrationStatus.pending:
        return 'قيد المراجعة';
      case RegistrationStatus.approved:
        return 'معتمد';
      case RegistrationStatus.active:
        return 'نشط';
    }
  }
}

/// Validity tier derived from the cached token's `validUntilUtc`.
/// Used to colour the expiry pill on the Digital ID screen.
enum DigitalIdValidity {
  valid,
  expiringSoon,
  expired,
}

/// Cached view of a member's identity card, plus the signed Digital ID
/// token that the gate scanner will verify against the API.
///
/// Tamper-resistance story:
///   * The token itself is HMAC-signed by the server. Even though the
///     on-disk cache is plaintext JSON, mutating `fullName` or `status`
///     here has no effect on admission — the gate re-reads those from
///     the signed payload via `/api/admissions/verify`.
///   * If an attacker edits the token, the signature check on the gate
///     side fails and the app surfaces "signature mismatch".
///
/// Deliberately immutable: every refresh produces a fresh instance, and
/// the storage repository writes the full JSON each time rather than
/// patching fields (simpler migration story, atomic writes).
class AthleteProfile {
  final String id;
  final String fullName;
  final String smfId;
  final RegistrationStatus status;

  /// Server-issued, HMAC-signed Digital ID token. This is what the QR
  /// encodes; `smfId` alone is never presented as a credential.
  final String digitalIdToken;
  final DateTime issuedAtUtc;
  final DateTime validUntilUtc;

  final DateTime cachedAtUtc;

  const AthleteProfile({
    required this.id,
    required this.fullName,
    required this.smfId,
    required this.status,
    required this.digitalIdToken,
    required this.issuedAtUtc,
    required this.validUntilUtc,
    required this.cachedAtUtc,
  });

  AthleteProfile copyWith({
    String? id,
    String? fullName,
    String? smfId,
    RegistrationStatus? status,
    String? digitalIdToken,
    DateTime? issuedAtUtc,
    DateTime? validUntilUtc,
    DateTime? cachedAtUtc,
  }) =>
      AthleteProfile(
        id: id ?? this.id,
        fullName: fullName ?? this.fullName,
        smfId: smfId ?? this.smfId,
        status: status ?? this.status,
        digitalIdToken: digitalIdToken ?? this.digitalIdToken,
        issuedAtUtc: issuedAtUtc ?? this.issuedAtUtc,
        validUntilUtc: validUntilUtc ?? this.validUntilUtc,
        cachedAtUtc: cachedAtUtc ?? this.cachedAtUtc,
      );

  /// Builds a profile from the backend `IssueDigitalIdResult` shape
  /// (`POST /api/members/{id}/digital-id`). The server uses camelCase
  /// keys and `JsonStringEnumConverter`, so `registrationStatus` comes
  /// as a string ("Approved"), not an int.
  factory AthleteProfile.fromIssueResult(
    Map<String, dynamic> json, {
    required DateTime cachedAtUtc,
  }) {
    final member = json['member'] as Map<String, dynamic>;
    return AthleteProfile(
      id: member['id'] as String,
      fullName: member['fullName'] as String,
      smfId: member['smF_ID'] as String,
      status: RegistrationStatus.fromWire(
        member['registrationStatus'] as String,
      ),
      digitalIdToken: json['token'] as String,
      issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String).toUtc(),
      validUntilUtc: DateTime.parse(json['validUntilUtc'] as String).toUtc(),
      cachedAtUtc: cachedAtUtc,
    );
  }

  /// Storage-format JSON. Versioned so we can evolve the schema without
  /// blowing up old cached rows — unknown versions get ignored and the
  /// user sees the login screen instead of a cryptic crash.
  Map<String, dynamic> toJson() => {
        'version': _schemaVersion,
        'id': id,
        'fullName': fullName,
        'smfId': smfId,
        'status': status.wire,
        'digitalIdToken': digitalIdToken,
        'issuedAtUtc': issuedAtUtc.toUtc().toIso8601String(),
        'validUntilUtc': validUntilUtc.toUtc().toIso8601String(),
        'cachedAtUtc': cachedAtUtc.toUtc().toIso8601String(),
      };

  static const int _schemaVersion = 2;

  static AthleteProfile? fromJson(Map<String, dynamic> json) {
    final version = json['version'];
    if (version is! int || version != _schemaVersion) return null;
    try {
      return AthleteProfile(
        id: json['id'] as String,
        fullName: json['fullName'] as String,
        smfId: json['smfId'] as String,
        status: RegistrationStatus.fromWire(json['status'] as String),
        digitalIdToken: json['digitalIdToken'] as String,
        issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String).toUtc(),
        validUntilUtc: DateTime.parse(json['validUntilUtc'] as String).toUtc(),
        cachedAtUtc: DateTime.parse(json['cachedAtUtc'] as String).toUtc(),
      );
    } catch (_) {
      return null;
    }
  }

  String encode() => jsonEncode(toJson());

  static AthleteProfile? decode(String raw) {
    try {
      final map = jsonDecode(raw);
      if (map is Map<String, dynamic>) {
        return AthleteProfile.fromJson(map);
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  /// Classify the token against the given reference time.
  ///
  /// `expiringSoon` fires at 20% of the total lifetime remaining —
  /// typically the last ~5 hours of a 24-hour token. Tuned to give
  /// athletes enough time to find wifi before the card goes red.
  DigitalIdValidity validityAt(DateTime now) {
    final nowUtc = now.toUtc();
    if (!nowUtc.isBefore(validUntilUtc)) {
      return DigitalIdValidity.expired;
    }
    final remaining = validUntilUtc.difference(nowUtc);
    final total = validUntilUtc.difference(issuedAtUtc);
    if (total.inSeconds <= 0) return DigitalIdValidity.valid;
    final ratio = remaining.inSeconds / total.inSeconds;
    if (ratio < 0.2) return DigitalIdValidity.expiringSoon;
    return DigitalIdValidity.valid;
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AthleteProfile &&
          other.id == id &&
          other.fullName == fullName &&
          other.smfId == smfId &&
          other.status == status &&
          other.digitalIdToken == digitalIdToken &&
          other.issuedAtUtc == issuedAtUtc &&
          other.validUntilUtc == validUntilUtc &&
          other.cachedAtUtc == cachedAtUtc;

  @override
  int get hashCode => Object.hash(
        id,
        fullName,
        smfId,
        status,
        digitalIdToken,
        issuedAtUtc,
        validUntilUtc,
        cachedAtUtc,
      );
}
