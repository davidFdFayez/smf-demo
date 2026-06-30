import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:smf_mobile/models/athlete_profile.dart';

void main() {
  group('AthleteProfile JSON round-trip', () {
    final sample = AthleteProfile(
      id: 'b6f9e2bd-7b8f-4b2d-8d2c-9af25f9a0111',
      fullName: 'Sara Al-Ahmed',
      smfId: '#SMF2026-00042',
      status: RegistrationStatus.approved,
      digitalIdToken: 'base64payload.base64sig',
      issuedAtUtc: DateTime.utc(2026, 4, 18, 12),
      validUntilUtc: DateTime.utc(2026, 4, 19, 12),
      cachedAtUtc: DateTime.utc(2026, 4, 18, 12, 1),
    );

    test('encode + decode produces an equal value', () {
      final encoded = sample.encode();
      final decoded = AthleteProfile.decode(encoded);
      expect(decoded, sample);
    });

    test('unknown schema version is rejected', () {
      final json = sample.toJson();
      json['version'] = 999;
      final raw = jsonEncode(json);
      expect(AthleteProfile.decode(raw), isNull);
    });

    test('corrupt payload returns null (no throw)', () {
      expect(AthleteProfile.decode('{not json'), isNull);
      expect(AthleteProfile.decode(''), isNull);
    });

    test('maps registrationStatus from backend camelCase payload', () {
      final wire = {
        'member': {
          'id': 'b6f9e2bd-7b8f-4b2d-8d2c-9af25f9a0111',
          'fullName': 'Sara',
          'dateOfBirth': '1998-03-12',
          'role': 'Athlete',
          'smF_ID': '#SMF2026-00042',
          'registrationStatus': 'Active',
          'guardianConsent': false,
          'createdAtUtc': '2026-01-01T00:00:00Z',
        },
        'token': 'abc.def',
        'issuedAtUtc': '2026-04-18T12:00:00Z',
        'validUntilUtc': '2026-04-19T12:00:00Z',
      };
      final profile = AthleteProfile.fromIssueResult(
        wire,
        cachedAtUtc: DateTime.utc(2026, 4, 18, 12, 5),
      );
      expect(profile.status, RegistrationStatus.active);
      expect(profile.smfId, '#SMF2026-00042');
      expect(profile.digitalIdToken, 'abc.def');
    });

    test('unknown status values fall back to pending (fail closed)', () {
      expect(RegistrationStatus.fromWire('Suspended'), RegistrationStatus.pending);
    });
  });

  group('AthleteProfile.validityAt', () {
    final profile = AthleteProfile(
      id: 'id',
      fullName: 'N',
      smfId: 'S',
      status: RegistrationStatus.approved,
      digitalIdToken: 'a.b',
      issuedAtUtc: DateTime.utc(2026, 4, 18, 12),
      validUntilUtc: DateTime.utc(2026, 4, 19, 12),
      cachedAtUtc: DateTime.utc(2026, 4, 18, 12),
    );

    test('returns valid well inside the window', () {
      expect(
        profile.validityAt(DateTime.utc(2026, 4, 18, 18)),
        DigitalIdValidity.valid,
      );
    });

    test('flips to expiringSoon within the last 20% of lifetime', () {
      // Lifetime = 24h. 20% = ~4h48m. 4h from end = well inside the
      // expiring-soon band.
      expect(
        profile.validityAt(DateTime.utc(2026, 4, 19, 8)),
        DigitalIdValidity.expiringSoon,
      );
    });

    test('returns expired at or after validUntilUtc', () {
      expect(
        profile.validityAt(DateTime.utc(2026, 4, 19, 12)),
        DigitalIdValidity.expired,
      );
      expect(
        profile.validityAt(DateTime.utc(2026, 4, 20)),
        DigitalIdValidity.expired,
      );
    });
  });
}
