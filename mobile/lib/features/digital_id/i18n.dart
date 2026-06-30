import 'package:flutter/widgets.dart';

import '../../models/athlete_profile.dart';

/// Minimal, dependency-free bilingual string table for the Digital ID
/// feature. We don't pull in `intl` or `flutter_localizations` here —
/// the feature surfaces ~15 strings, all known at compile time, and the
/// project is already shipping a similar hand-rolled approach on the
/// web checkout page for consistency.
enum Locale { en, ar }

TextDirection directionFor(Locale locale) =>
    locale == Locale.ar ? TextDirection.rtl : TextDirection.ltr;

class DigitalIdStrings {
  final String appTitle;
  final String screenTitle;
  final String loginTitle;
  final String loginBlurb;
  final String memberIdLabel;
  final String memberIdHint;
  final String memberIdRequired;
  final String memberIdInvalid;
  final String signInCta;
  final String signOut;
  final String offlineBanner;
  final String presentQrCaption;
  final String smfIdPrefix;
  final String lastSynced;
  final String refresh;
  final String refreshing;
  final String refreshSuccess;
  final String refreshFailed;
  final String continueAs;
  final String validPill;
  final String expiringSoonPill;
  final String expiredPill;
  final String tokenExpiredInstruction;
  final String cacheError;

  const DigitalIdStrings({
    required this.appTitle,
    required this.screenTitle,
    required this.loginTitle,
    required this.loginBlurb,
    required this.memberIdLabel,
    required this.memberIdHint,
    required this.memberIdRequired,
    required this.memberIdInvalid,
    required this.signInCta,
    required this.signOut,
    required this.offlineBanner,
    required this.presentQrCaption,
    required this.smfIdPrefix,
    required this.lastSynced,
    required this.refresh,
    required this.refreshing,
    required this.refreshSuccess,
    required this.refreshFailed,
    required this.continueAs,
    required this.validPill,
    required this.expiringSoonPill,
    required this.expiredPill,
    required this.tokenExpiredInstruction,
    required this.cacheError,
  });

  String statusLabel(RegistrationStatus status) =>
      this == _en ? status.labelEn : status.labelAr;

  static const _en = DigitalIdStrings(
    appTitle: 'Saudi MuayThai Federation',
    screenTitle: 'Digital ID',
    loginTitle: 'Digital ID',
    loginBlurb: 'Sign in once on the network to cache your profile. '
        'Your Digital ID will stay available offline at the venue gate.',
    memberIdLabel: 'Member id',
    memberIdHint: '00000000-0000-0000-0000-000000000000',
    memberIdRequired: 'Required',
    memberIdInvalid: 'Enter a valid member id (GUID).',
    signInCta: 'Sign in & cache profile',
    signOut: 'Sign out',
    offlineBanner: 'Offline Mode — showing cached Digital ID. '
        'The QR code below is still valid.',
    presentQrCaption: 'Present this QR at the venue gate',
    smfIdPrefix: 'SMF ID: ',
    lastSynced: 'Last synced',
    refresh: 'Refresh',
    refreshing: 'Refreshing…',
    refreshSuccess: 'Profile refreshed.',
    refreshFailed: 'Refresh failed',
    continueAs: 'Continue as',
    validPill: 'Valid',
    expiringSoonPill: 'Expires soon',
    expiredPill: 'Expired — reconnect to renew',
    tokenExpiredInstruction: 'Your Digital ID token has expired. Connect '
        'to the internet and tap Refresh to get a new one.',
    cacheError: 'Cache error',
  );

  static const _ar = DigitalIdStrings(
    appTitle: 'الاتحاد السعودي للمواي تاي',
    screenTitle: 'الهوية الرقمية',
    loginTitle: 'الهوية الرقمية',
    loginBlurb: 'سجّل الدخول مرة واحدة عبر الإنترنت لحفظ بيانات ملفك. '
        'ستتوفر هويتك الرقمية بدون إنترنت عند بوابة المنشأة.',
    memberIdLabel: 'رقم العضو',
    memberIdHint: '00000000-0000-0000-0000-000000000000',
    memberIdRequired: 'مطلوب',
    memberIdInvalid: 'الرجاء إدخال رقم عضو صالح (GUID).',
    signInCta: 'تسجيل الدخول وحفظ البيانات',
    signOut: 'تسجيل الخروج',
    offlineBanner: 'وضع عدم الاتصال — يتم عرض الهوية المحفوظة. '
        'رمز QR أدناه لا يزال صالحًا.',
    presentQrCaption: 'اعرض رمز QR هذا عند بوابة المنشأة',
    smfIdPrefix: 'رقم الاتحاد: ',
    lastSynced: 'آخر مزامنة',
    refresh: 'تحديث',
    refreshing: 'جارٍ التحديث…',
    refreshSuccess: 'تم تحديث الملف.',
    refreshFailed: 'فشل التحديث',
    continueAs: 'المتابعة باسم',
    validPill: 'صالح',
    expiringSoonPill: 'ينتهي قريبًا',
    expiredPill: 'منتهي — يرجى إعادة الاتصال للتحديث',
    tokenExpiredInstruction: 'انتهت صلاحية رمز هويتك الرقمية. اتصل بالإنترنت '
        'ثم اضغط تحديث للحصول على رمز جديد.',
    cacheError: 'خطأ في التخزين',
  );
}

DigitalIdStrings stringsFor(Locale locale) =>
    locale == Locale.ar ? DigitalIdStrings._ar : DigitalIdStrings._en;
