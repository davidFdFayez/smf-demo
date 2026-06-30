/// Shared scoring event types for the referee tablet.
///
/// The server exposes two corner colours in `FighterColor`. The hub method
/// `SubmitStrike` accepts a string (e.g. "Red") and parses it internally —
/// so on the wire we always send the name, never the enum ordinal.
enum FighterColor {
  red,
  blue;

  /// Server-compatible wire value ("Red" / "Blue").
  String get wireValue {
    switch (this) {
      case FighterColor.red:
        return "Red";
      case FighterColor.blue:
        return "Blue";
    }
  }

  static FighterColor fromJson(dynamic value) {
    // JsonHubProtocol may emit the enum name ("Red") or the ordinal (1/2),
    // depending on server-side converters. Accept both.
    if (value is String) {
      final normalised = value.toLowerCase();
      if (normalised == "red" || normalised == "1") return FighterColor.red;
      if (normalised == "blue" || normalised == "2") return FighterColor.blue;
    }
    if (value is num) {
      return value.toInt() == 1 ? FighterColor.red : FighterColor.blue;
    }
    return FighterColor.red;
  }
}

/// Payload broadcast by `MatchScoringHub.SubmitStrike` to every client in
/// the match's group (head referee dashboard + public scoreboard + other
/// referees).
class StrikeUpdate {
  final String eventId;
  final String matchId;
  final String refereeId;
  final FighterColor fighterColor;
  final DateTime occurredAtUtc;

  const StrikeUpdate({
    required this.eventId,
    required this.matchId,
    required this.refereeId,
    required this.fighterColor,
    required this.occurredAtUtc,
  });

  factory StrikeUpdate.fromJson(Map<String, dynamic> json) {
    return StrikeUpdate(
      eventId: (json["eventId"] ?? json["EventId"] ?? "").toString(),
      matchId: (json["matchId"] ?? json["MatchId"] ?? "").toString(),
      refereeId: (json["refereeId"] ?? json["RefereeId"] ?? "").toString(),
      fighterColor: FighterColor.fromJson(json["fighterColor"] ?? json["FighterColor"]),
      occurredAtUtc: _parseDate(json["occurredAtUtc"] ?? json["OccurredAtUtc"]),
    );
  }
}

/// Payload broadcast by `MatchScoringHub.OverrideScore` – head-referee only.
class ScoreOverrideUpdate {
  final String eventId;
  final String matchId;
  final String headRefereeId;
  final int red;
  final int blue;
  final int? round;
  final DateTime occurredAtUtc;

  const ScoreOverrideUpdate({
    required this.eventId,
    required this.matchId,
    required this.headRefereeId,
    required this.red,
    required this.blue,
    required this.round,
    required this.occurredAtUtc,
  });

  factory ScoreOverrideUpdate.fromJson(Map<String, dynamic> json) {
    final newScore = (json["newScore"] ?? json["NewScore"] ?? const {}) as Map;
    return ScoreOverrideUpdate(
      eventId: (json["eventId"] ?? json["EventId"] ?? "").toString(),
      matchId: (json["matchId"] ?? json["MatchId"] ?? "").toString(),
      headRefereeId: (json["headRefereeId"] ?? json["HeadRefereeId"] ?? "").toString(),
      red: (newScore["red"] ?? newScore["Red"] ?? 0) as int,
      blue: (newScore["blue"] ?? newScore["Blue"] ?? 0) as int,
      round: (newScore["round"] ?? newScore["Round"]) as int?,
      occurredAtUtc: _parseDate(json["occurredAtUtc"] ?? json["OccurredAtUtc"]),
    );
  }
}

/// Mirror of `SMF.Application.Features.Scoring.TimerActionKind`.
/// Broadcast by [ScoreOverrideUpdate]'s sibling, `ReceiveTimerUpdate`.
enum TimerActionKind {
  roundStarted,
  paused,
  resumed,
  ended,
  reset,
  tick;

  static TimerActionKind fromJson(dynamic value) {
    if (value is num) {
      switch (value.toInt()) {
        case 1:
          return TimerActionKind.roundStarted;
        case 2:
          return TimerActionKind.paused;
        case 3:
          return TimerActionKind.resumed;
        case 4:
          return TimerActionKind.ended;
        case 5:
          return TimerActionKind.reset;
        case 6:
          return TimerActionKind.tick;
      }
    }
    if (value is String) {
      switch (value.toLowerCase()) {
        case "roundstarted":
          return TimerActionKind.roundStarted;
        case "paused":
          return TimerActionKind.paused;
        case "resumed":
          return TimerActionKind.resumed;
        case "ended":
          return TimerActionKind.ended;
        case "reset":
          return TimerActionKind.reset;
        case "tick":
          return TimerActionKind.tick;
      }
    }
    return TimerActionKind.tick;
  }
}

/// Payload broadcast by `MatchScoringHub` whenever the round clock changes.
/// Mirror of `SMF.Application.Features.Scoring.TimerStatePayload`.
///
/// The hub sends a snapshot on every round transition (start/pause/resume/
/// end/reset), and also answers `RequestTimerState` with a `tick` payload
/// — that's how a late joiner gets the current elapsed time without waiting
/// for the next transition.
class TimerStateUpdate {
  final String matchId;
  final int currentRound;
  final int roundDurationSeconds;
  final int elapsedSeconds;
  final bool isRunning;
  final TimerActionKind lastAction;
  final DateTime occurredAtUtc;

  const TimerStateUpdate({
    required this.matchId,
    required this.currentRound,
    required this.roundDurationSeconds,
    required this.elapsedSeconds,
    required this.isRunning,
    required this.lastAction,
    required this.occurredAtUtc,
  });

  factory TimerStateUpdate.fromJson(Map<String, dynamic> json) {
    return TimerStateUpdate(
      matchId: (json["matchId"] ?? json["MatchId"] ?? "").toString(),
      currentRound: (json["currentRound"] ?? json["CurrentRound"] ?? 1) as int,
      roundDurationSeconds:
          (json["roundDurationSeconds"] ?? json["RoundDurationSeconds"] ?? 0) as int,
      elapsedSeconds:
          (json["elapsedSeconds"] ?? json["ElapsedSeconds"] ?? 0) as int,
      isRunning:
          (json["isRunning"] ?? json["IsRunning"] ?? false) as bool,
      lastAction:
          TimerActionKind.fromJson(json["lastAction"] ?? json["LastAction"]),
      occurredAtUtc:
          _parseDate(json["occurredAtUtc"] ?? json["OccurredAtUtc"]),
    );
  }

  /// Seconds remaining in the current round, clamped to zero.
  int get remainingSeconds {
    final remaining = roundDurationSeconds - elapsedSeconds;
    return remaining < 0 ? 0 : remaining;
  }
}

DateTime _parseDate(dynamic value) {
  if (value is DateTime) return value;
  if (value is String) {
    return DateTime.tryParse(value)?.toUtc() ?? DateTime.now().toUtc();
  }
  return DateTime.now().toUtc();
}
