import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../models/scoring_events.dart';
import '../services/scoring_hub_service.dart';

/// Full-screen two-button referee UI.
///
/// Layout priorities (in order):
///   1. Tap-target size – each button fills ~half of the available space so
///      a referee can hit it without looking.
///   2. High contrast – saturated red / blue on black, oversized labels.
///   3. Zero-delay feedback – tap triggers `HapticFeedback.heavyImpact()` and
///      increments a local counter immediately; the SignalR invoke runs in
///      the background via [ScoringHubService.fireStrike].
class RefereeScoringScreen extends StatefulWidget {
  const RefereeScoringScreen({
    super.key,
    required this.hub,
    required this.matchCode,
    required this.refereeId,
    required this.refereeLabel,
    required this.isHeadReferee,
  });

  final ScoringHubService hub;
  final String matchCode;
  final String refereeId;
  final String refereeLabel;
  final bool isHeadReferee;

  @override
  State<RefereeScoringScreen> createState() => _RefereeScoringScreenState();
}

class _RefereeScoringScreenState extends State<RefereeScoringScreen> {
  int _localRed = 0;
  int _localBlue = 0;
  Timer? _tickTimer;

  @override
  void initState() {
    super.initState();
    widget.hub.addListener(_onHubChanged);
    // Drive a 1s UI tick so the countdown ticks down smoothly even between
    // server broadcasts. The authoritative elapsed time still comes from
    // [ScoringHubService.timer] – we only extrapolate when the timer is
    // marked as running.
    _tickTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      if (widget.hub.timer?.isRunning ?? false) setState(() {});
    });
  }

  @override
  void dispose() {
    _tickTimer?.cancel();
    widget.hub.removeListener(_onHubChanged);
    super.dispose();
  }

  void _onHubChanged() {
    if (!mounted) return;
    setState(() {});
  }

  void _strike(FighterColor color) {
    // 1. Haptic is ALWAYS the first thing — it's the tactile confirmation
    //    that the referee's tap registered, independent of network health.
    HapticFeedback.heavyImpact();

    // 2. Bump the local counter so the UI moves with the tap.
    setState(() {
      if (color == FighterColor.red) {
        _localRed += 1;
      } else {
        _localBlue += 1;
      }
    });

    // 3. Fire-and-forget the hub invoke. Any failure surfaces on the banner
    //    through hub.lastError without blocking subsequent taps.
    widget.hub.fireStrike(
      matchCode: widget.matchCode,
      refereeId: widget.refereeId,
      fighterColor: color,
    );
  }

  Future<void> _openOverride() async {
    // Pre-fill with the current broadcast score so "accept as-is" keeps the
    // count stable. Round defaults to the current round from the timer.
    final result = await showDialog<_OverrideResult>(
      context: context,
      barrierDismissible: true,
      builder: (_) => _OverrideDialog(
        initialRed: widget.hub.remoteRed,
        initialBlue: widget.hub.remoteBlue,
        initialRound: widget.hub.timer?.currentRound,
      ),
    );
    if (result == null) return;
    try {
      await widget.hub.overrideScore(
        matchCode: widget.matchCode,
        headRefereeId: widget.refereeId,
        red: result.red,
        blue: result.blue,
        round: result.round,
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text("Override failed: $e"), backgroundColor: Colors.red.shade900),
      );
    }
  }

  Future<void> _nullifyLast() async {
    try {
      final ok = await widget.hub.nullifyLastStrike(
        matchCode: widget.matchCode,
        headRefereeId: widget.refereeId,
      );
      if (!mounted) return;
      if (!ok) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text("No strike to nullify yet.")),
        );
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text("Nullify failed: $e"), backgroundColor: Colors.red.shade900),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final hubState = widget.hub.state;
    final error = widget.hub.lastError;

    return Scaffold(
      backgroundColor: Colors.black,
      body: SafeArea(
        child: Column(
          children: [
            _StatusBar(
              matchCode: widget.matchCode,
              refereeLabel: widget.refereeLabel,
              isHead: widget.isHeadReferee,
              state: hubState,
              error: error,
              timer: widget.hub.timer,
              remoteRed: widget.hub.remoteRed,
              remoteBlue: widget.hub.remoteBlue,
            ),
            Expanded(
              child: LayoutBuilder(
                builder: (context, constraints) {
                  // Landscape (tablet held horizontally) → side-by-side.
                  // Portrait → stacked (red on top).
                  final isLandscape = constraints.maxWidth >= constraints.maxHeight;
                  final axis = isLandscape ? Axis.horizontal : Axis.vertical;

                  return Flex(
                    direction: axis,
                    children: [
                      Expanded(
                        child: _StrikeButton(
                          label: "RED",
                          sublabel: "FIGHTER STRIKE",
                          color: const Color(0xFFD32F2F),
                          accent: const Color(0xFFFF5252),
                          localCount: _localRed,
                          enabled: hubState == HubConnectionState.Connected,
                          onTap: () => _strike(FighterColor.red),
                        ),
                      ),
                      const SizedBox.square(dimension: 4),
                      Expanded(
                        child: _StrikeButton(
                          label: "BLUE",
                          sublabel: "FIGHTER STRIKE",
                          color: const Color(0xFF1565C0),
                          accent: const Color(0xFF448AFF),
                          localCount: _localBlue,
                          enabled: hubState == HubConnectionState.Connected,
                          onTap: () => _strike(FighterColor.blue),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
            if (widget.isHeadReferee)
              _HeadRefereeToolbar(
                connected: hubState == HubConnectionState.Connected,
                onOverride: _openOverride,
                onNullify: _nullifyLast,
              ),
          ],
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────── widgets ───────────────────────

class _StatusBar extends StatelessWidget {
  const _StatusBar({
    required this.matchCode,
    required this.refereeLabel,
    required this.isHead,
    required this.state,
    required this.error,
    required this.timer,
    required this.remoteRed,
    required this.remoteBlue,
  });

  final String matchCode;
  final String refereeLabel;
  final bool isHead;
  final HubConnectionState state;
  final String? error;
  final TimerStateUpdate? timer;
  final int remoteRed;
  final int remoteBlue;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: Colors.black,
      padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  "$refereeLabel${isHead ? ' · HEAD' : ''}",
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    letterSpacing: 0.4,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  "MATCH  $matchCode",
                  style: TextStyle(
                    color: Colors.white.withOpacity(0.65),
                    fontSize: 11,
                    fontFamily: "monospace",
                    letterSpacing: 0.8,
                  ),
                ),
                if (error != null) ...[
                  const SizedBox(height: 2),
                  Text(
                    error!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: Colors.red.shade300,
                      fontSize: 10,
                    ),
                  ),
                ],
              ],
            ),
          ),
          // Live running score (tiny) – surfaces what the head referee sees
          // on the big scoreboard so even a side ref has context.
          _RunningScoreChip(red: remoteRed, blue: remoteBlue),
          const SizedBox(width: 12),
          _RoundClockChip(timer: timer),
          const SizedBox(width: 12),
          _ConnectionPill(state: state),
          const SizedBox(width: 12),
          IconButton(
            tooltip: "Leave match",
            color: Colors.white70,
            icon: const Icon(Icons.logout),
            onPressed: () => Navigator.of(context).maybePop(),
          ),
        ],
      ),
    );
  }
}

class _RunningScoreChip extends StatelessWidget {
  const _RunningScoreChip({required this.red, required this.blue});
  final int red;
  final int blue;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF1F1F1F),
        borderRadius: BorderRadius.circular(8),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _ScoreDot(color: const Color(0xFFFF5252), value: red),
          const SizedBox(width: 8),
          Container(width: 1, height: 16, color: Colors.white.withOpacity(0.15)),
          const SizedBox(width: 8),
          _ScoreDot(color: const Color(0xFF448AFF), value: blue),
        ],
      ),
    );
  }
}

class _ScoreDot extends StatelessWidget {
  const _ScoreDot({required this.color, required this.value});
  final Color color;
  final int value;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 6),
        Text(
          "$value",
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w700,
            fontSize: 14,
            fontFamily: "monospace",
          ),
        ),
      ],
    );
  }
}

class _RoundClockChip extends StatelessWidget {
  const _RoundClockChip({required this.timer});
  final TimerStateUpdate? timer;

  @override
  Widget build(BuildContext context) {
    final t = timer;
    if (t == null) {
      return _clockContainer(
        color: const Color(0xFF2E2E2E),
        label: "ROUND --",
        time: "--:--",
        dim: true,
      );
    }

    // Extrapolate elapsed time since the last server broadcast, so the
    // countdown ticks down smoothly between updates.
    final extrapolated = t.isRunning
        ? t.elapsedSeconds +
            DateTime.now().toUtc().difference(t.occurredAtUtc).inSeconds
        : t.elapsedSeconds;
    final remaining = (t.roundDurationSeconds - extrapolated).clamp(0, 1 << 31);

    final Color color;
    if (!t.isRunning) {
      color = const Color(0xFFB26A00); // paused/idle
    } else if (remaining <= 10) {
      color = const Color(0xFFD32F2F); // final countdown
    } else {
      color = const Color(0xFF1B5E20); // running
    }

    return _clockContainer(
      color: color,
      label: "ROUND ${t.currentRound}",
      time: _formatMmSs(remaining),
      dim: false,
    );
  }

  Widget _clockContainer({
    required Color color,
    required String label,
    required String time,
    required bool dim,
  }) {
    return Container(
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(8),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: TextStyle(
              color: Colors.white.withOpacity(dim ? 0.55 : 0.85),
              fontSize: 9,
              fontWeight: FontWeight.w700,
              letterSpacing: 1.0,
            ),
          ),
          Text(
            time,
            style: TextStyle(
              color: Colors.white.withOpacity(dim ? 0.5 : 1.0),
              fontSize: 16,
              fontWeight: FontWeight.w800,
              fontFamily: "monospace",
              letterSpacing: 1.1,
            ),
          ),
        ],
      ),
    );
  }
}

String _formatMmSs(int totalSeconds) {
  final m = (totalSeconds ~/ 60).toString().padLeft(2, "0");
  final s = (totalSeconds % 60).toString().padLeft(2, "0");
  return "$m:$s";
}

class _HeadRefereeToolbar extends StatelessWidget {
  const _HeadRefereeToolbar({
    required this.connected,
    required this.onOverride,
    required this.onNullify,
  });

  final bool connected;
  final VoidCallback onOverride;
  final VoidCallback onNullify;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: const Color(0xFF0F0F0F),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        children: [
          Expanded(
            child: _ToolbarButton(
              icon: Icons.backspace,
              label: "Nullify last strike",
              tone: const Color(0xFFB26A00),
              enabled: connected,
              onPressed: onNullify,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            flex: 2,
            child: _ToolbarButton(
              icon: Icons.edit_note,
              label: "Override score",
              tone: const Color(0xFF1565C0),
              enabled: connected,
              onPressed: onOverride,
            ),
          ),
        ],
      ),
    );
  }
}

class _ToolbarButton extends StatelessWidget {
  const _ToolbarButton({
    required this.icon,
    required this.label,
    required this.tone,
    required this.enabled,
    required this.onPressed,
  });

  final IconData icon;
  final String label;
  final Color tone;
  final bool enabled;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: enabled ? tone : tone.withOpacity(0.35),
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: enabled ? onPressed : null,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: Colors.white, size: 20),
              const SizedBox(width: 10),
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                    letterSpacing: 0.6,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _OverrideResult {
  final int red;
  final int blue;
  final int? round;
  const _OverrideResult({required this.red, required this.blue, required this.round});
}

class _OverrideDialog extends StatefulWidget {
  const _OverrideDialog({
    required this.initialRed,
    required this.initialBlue,
    required this.initialRound,
  });

  final int initialRed;
  final int initialBlue;
  final int? initialRound;

  @override
  State<_OverrideDialog> createState() => _OverrideDialogState();
}

class _OverrideDialogState extends State<_OverrideDialog> {
  late int _red = widget.initialRed;
  late int _blue = widget.initialBlue;
  late int? _round = widget.initialRound;

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: const Color(0xFF161616),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              "Score override",
              style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 4),
            Text(
              "Head referee only · round ${_round ?? '—'}",
              style: TextStyle(color: Colors.white.withOpacity(0.6), fontSize: 12),
            ),
            const SizedBox(height: 18),
            Row(
              children: [
                Expanded(
                  child: _ColorCounter(
                    label: "RED",
                    color: const Color(0xFFD32F2F),
                    value: _red,
                    onChanged: (v) => setState(() => _red = v),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _ColorCounter(
                    label: "BLUE",
                    color: const Color(0xFF1565C0),
                    value: _blue,
                    onChanged: (v) => setState(() => _blue = v),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                const Text(
                  "Round:",
                  style: TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.w600),
                ),
                const SizedBox(width: 10),
                _RoundChip(
                  label: "Match total",
                  selected: _round == null,
                  onSelected: () => setState(() => _round = null),
                ),
                const SizedBox(width: 6),
                for (var n = 1; n <= 5; n++) ...[
                  _RoundChip(
                    label: "R$n",
                    selected: _round == n,
                    onSelected: () => setState(() => _round = n),
                  ),
                  const SizedBox(width: 6),
                ],
              ],
            ),
            const SizedBox(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text("Cancel", style: TextStyle(color: Colors.white70)),
                ),
                const SizedBox(width: 8),
                FilledButton(
                  onPressed: () => Navigator.of(context).pop(
                    _OverrideResult(red: _red, blue: _blue, round: _round),
                  ),
                  style: FilledButton.styleFrom(backgroundColor: const Color(0xFF2E7D32)),
                  child: const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 8),
                    child: Text("Apply override"),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _ColorCounter extends StatelessWidget {
  const _ColorCounter({
    required this.label,
    required this.color,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final Color color;
  final int value;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 14),
      decoration: BoxDecoration(
        color: color.withOpacity(0.15),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: color.withOpacity(0.6)),
      ),
      child: Column(
        children: [
          Text(
            label,
            style: TextStyle(
              color: color,
              fontSize: 12,
              fontWeight: FontWeight.w800,
              letterSpacing: 2,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            "$value",
            style: const TextStyle(
              color: Colors.white,
              fontSize: 40,
              fontWeight: FontWeight.w800,
              fontFamily: "monospace",
            ),
          ),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              _StepButton(
                icon: Icons.remove,
                onTap: () => onChanged(value > 0 ? value - 1 : 0),
              ),
              const SizedBox(width: 12),
              _StepButton(icon: Icons.add, onTap: () => onChanged(value + 1)),
            ],
          ),
        ],
      ),
    );
  }
}

class _StepButton extends StatelessWidget {
  const _StepButton({required this.icon, required this.onTap});
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white.withOpacity(0.12),
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(10),
          child: Icon(icon, color: Colors.white, size: 22),
        ),
      ),
    );
  }
}

class _RoundChip extends StatelessWidget {
  const _RoundChip({
    required this.label,
    required this.selected,
    required this.onSelected,
  });

  final String label;
  final bool selected;
  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context) {
    return ChoiceChip(
      label: Text(label, style: const TextStyle(fontSize: 12)),
      selected: selected,
      onSelected: (_) => onSelected(),
      selectedColor: const Color(0xFF2E7D32),
      backgroundColor: Colors.white10,
      labelStyle: TextStyle(
        color: selected ? Colors.white : Colors.white70,
        fontWeight: FontWeight.w600,
      ),
      side: BorderSide(color: selected ? const Color(0xFF2E7D32) : Colors.white24),
    );
  }
}

class _ConnectionPill extends StatelessWidget {
  const _ConnectionPill({required this.state});
  final HubConnectionState state;

  @override
  Widget build(BuildContext context) {
    late final Color bg;
    late final String label;
    switch (state) {
      case HubConnectionState.Connected:
        bg = const Color(0xFF1B5E20);
        label = "LIVE";
        break;
      case HubConnectionState.Connecting:
      case HubConnectionState.Reconnecting:
        bg = const Color(0xFFB26A00);
        label = state == HubConnectionState.Reconnecting ? "RECONNECTING" : "CONNECTING";
        break;
      case HubConnectionState.Disconnected:
      case HubConnectionState.Disconnecting:
        bg = const Color(0xFF4E342E);
        label = "OFFLINE";
        break;
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: Colors.white,
          fontSize: 11,
          fontWeight: FontWeight.w700,
          letterSpacing: 1.2,
        ),
      ),
    );
  }
}

class _StrikeButton extends StatefulWidget {
  const _StrikeButton({
    required this.label,
    required this.sublabel,
    required this.color,
    required this.accent,
    required this.localCount,
    required this.enabled,
    required this.onTap,
  });

  final String label;
  final String sublabel;
  final Color color;
  final Color accent;
  final int localCount;
  final bool enabled;
  final VoidCallback onTap;

  @override
  State<_StrikeButton> createState() => _StrikeButtonState();
}

class _StrikeButtonState extends State<_StrikeButton> {
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final opacity = widget.enabled ? 1.0 : 0.35;
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTapDown: widget.enabled ? (_) => setState(() => _pressed = true) : null,
      onTapCancel: widget.enabled ? () => setState(() => _pressed = false) : null,
      onTapUp: widget.enabled
          ? (_) {
              setState(() => _pressed = false);
              widget.onTap();
            }
          : null,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 60),
        curve: Curves.easeOut,
        margin: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(20),
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [
              widget.color.withOpacity(opacity),
              widget.accent.withOpacity(opacity),
            ],
          ),
          boxShadow: _pressed
              ? const []
              : [
                  BoxShadow(
                    color: widget.accent.withOpacity(0.55 * opacity),
                    blurRadius: 22,
                    offset: const Offset(0, 6),
                  ),
                ],
        ),
        transform: Matrix4.identity()..scale(_pressed ? 0.985 : 1.0),
        child: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(
                widget.label,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 72,
                  fontWeight: FontWeight.w900,
                  letterSpacing: 4,
                  shadows: [Shadow(color: Colors.black45, blurRadius: 8)],
                ),
              ),
              const SizedBox(height: 4),
              Text(
                widget.sublabel,
                style: TextStyle(
                  color: Colors.white.withOpacity(0.9),
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                  letterSpacing: 6,
                ),
              ),
              const SizedBox(height: 24),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 6),
                decoration: BoxDecoration(
                  color: Colors.black.withOpacity(0.25),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  "TAPS  ${widget.localCount}",
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 14,
                    fontFamily: "monospace",
                    letterSpacing: 2,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
