/**
 * Message catalog. Kept as a plain TypeScript object so the compiler can
 * cross-check every call site — missing keys fail the build, which keeps
 * translations from silently drifting apart across locales.
 */

export type Locale = "en" | "ar";

export const strings = {
  en: {
    "app.title": "Saudi MuayThai Federation",
    "app.subtitle": "Member registration, events, and live scoring",
    "app.brand": "SMF",

    "nav.home": "Home",
    "nav.register": "Register",
    "nav.news": "News",
    "nav.rankings": "Rankings",
    "nav.events": "Events",
    "nav.clubs": "Clubs",
    "nav.scoreboard": "Watch match",
    "nav.safeguarding": "Report a concern",
    "nav.about": "About",
    "nav.learn": "Learn",
    "nav.store": "Store",

    "home.eyebrow": "Official federation platform",
    "home.headline_1": "Elevating MuayThai",
    "home.headline_2": "in the Kingdom.",
    "home.subheadline":
      "Register as an athlete, coach, or referee. Follow federation news, national rankings, and live tournament action — all in one place.",
    "home.cta.primary": "Register now",
    "home.cta.secondary": "Watch live match",
    "home.kpi.members": "Federation members",
    "home.kpi.clubs": "Accredited clubs",
    "home.kpi.events": "Events this year",
    "home.section.news": "Latest news & announcements",
    "home.section.news.cta": "All news →",
    "home.section.events": "Upcoming events",
    "home.section.events.cta": "All events →",
    "home.section.pillars": "Built for every member of the federation",
    "home.pillar.athlete.title": "Athletes",
    "home.pillar.athlete.desc":
      "Online registration, digital ID, medical clearance, and live rankings.",
    "home.pillar.club.title": "Clubs",
    "home.pillar.club.desc":
      "Accredited directory, event entries, and member management tools.",
    "home.pillar.referee.title": "Referees",
    "home.pillar.referee.desc":
      "Real-time scoring consoles with SignalR streaming and head-referee override.",

    "register.title": "Register with the federation",
    "register.subtitle":
      "Fill in the details below. If the athlete is under 18, a guardian consent field will appear automatically.",

    "news.title": "News & announcements",
    "news.subtitle":
      "Official updates from the federation — championships, education, press releases.",
    "news.empty": "No articles published yet — check back soon.",
    "news.all_categories": "All categories",
    "news.read_more": "Read article →",
    "news.back": "← Back to news",

    "rankings.title": "National rankings",
    "rankings.subtitle":
      "Athlete standings derived from completed federation tournaments. Gold → Silver → Bronze → wins is the tie-break order.",
    "rankings.empty":
      "No completed tournaments yet — rankings will appear here after the first event concludes.",

    "events.title": "Events & tournaments",
    "events.subtitle":
      "Upcoming and recent tournaments, camps, and championships organised by the federation.",
    "events.empty": "No events scheduled yet.",
    "events.location": "Location",
    "events.fee": "Entry fee",
    "events.seats": "Seats",
    "events.unlimited": "Unlimited",
    "events.view_details": "View details",

    "clubs.title": "Accredited clubs",
    "clubs.subtitle":
      "Find an officially recognised MuayThai club in your city. All listed clubs are active members in good standing.",
    "clubs.empty": "No active clubs yet.",
    "clubs.member_count": "members",
    "clubs.contact": "Contact",
    "clubs.website": "Website",

    "safeguarding.title": "Report a concern",
    "safeguarding.subtitle":
      "Confidentially report anti-doping violations, safeguarding concerns, harassment, or integrity issues. Anonymous submissions supported.",

    "watch.title": "Watch a live match",
    "watch.subtitle":
      "Public, read-only view of the live scoring feed. Enter the match code to connect to the broadcast.",

    "about.title": "About the federation",
    "about.subtitle":
      "The Saudi MuayThai Federation governs the practice, competition, and development of MuayThai across the Kingdom.",

    "language.switch": "Switch language",

    "common.loading": "Loading…",
    "common.refresh": "Refresh",
    "common.error_generic": "Something went wrong.",

    "footer.tagline":
      "Governing MuayThai in Saudi Arabia — registration, competitions, and integrity.",
    "footer.explore": "Explore",
    "footer.integrity": "Integrity & support",
  },

  ar: {
    "app.title": "الاتحاد السعودي للملاكمة التايلاندية",
    "app.subtitle": "تسجيل الأعضاء، الفعاليات، والتحكيم المباشر",
    "app.brand": "الاتحاد",

    "nav.home": "الرئيسية",
    "nav.register": "التسجيل",
    "nav.news": "الأخبار",
    "nav.rankings": "الترتيب",
    "nav.events": "الفعاليات",
    "nav.clubs": "الأندية",
    "nav.scoreboard": "شاشة البث",
    "nav.safeguarding": "الإبلاغ عن مخالفة",
    "nav.about": "عن الاتحاد",
    "nav.learn": "التعلم",
    "nav.store": "المتجر",

    "home.eyebrow": "المنصة الرسمية للاتحاد",
    "home.headline_1": "للارتقاء بالملاكمة التايلاندية",
    "home.headline_2": "في المملكة.",
    "home.subheadline":
      "سجّل كلاعب أو مدرّب أو حكم. تابع أخبار الاتحاد، الترتيب الوطني، والمباريات الحية — في مكان واحد.",
    "home.cta.primary": "سجّل الآن",
    "home.cta.secondary": "شاهد مباراة حية",
    "home.kpi.members": "أعضاء الاتحاد",
    "home.kpi.clubs": "أندية معتمدة",
    "home.kpi.events": "فعاليات هذا العام",
    "home.section.news": "أحدث الأخبار والإعلانات",
    "home.section.news.cta": "جميع الأخبار ←",
    "home.section.events": "الفعاليات القادمة",
    "home.section.events.cta": "جميع الفعاليات ←",
    "home.section.pillars": "مصمم لكل عضو في الاتحاد",
    "home.pillar.athlete.title": "اللاعبون",
    "home.pillar.athlete.desc":
      "التسجيل الإلكتروني، الهوية الرقمية، الفحص الطبي، والترتيب المباشر.",
    "home.pillar.club.title": "الأندية",
    "home.pillar.club.desc":
      "دليل الأندية المعتمدة، تسجيل الفعاليات، وإدارة الأعضاء.",
    "home.pillar.referee.title": "الحكام",
    "home.pillar.referee.desc":
      "لوحات تحكيم لحظية عبر SignalR مع صلاحية تعديل الحكم الأول.",

    "register.title": "التسجيل في الاتحاد",
    "register.subtitle":
      "أدخل البيانات التالية. إذا كان اللاعب دون الثامنة عشرة، سيظهر حقل موافقة ولي الأمر تلقائياً.",

    "news.title": "الأخبار والإعلانات",
    "news.subtitle": "أحدث التحديثات الرسمية من الاتحاد.",
    "news.empty": "لا توجد مقالات منشورة بعد.",
    "news.all_categories": "جميع التصنيفات",
    "news.read_more": "اقرأ المقال ←",
    "news.back": "→ العودة إلى الأخبار",

    "rankings.title": "الترتيب الوطني",
    "rankings.subtitle":
      "ترتيب اللاعبين مستخلص من البطولات المكتملة. ذهبية ← فضية ← برونزية ← الانتصارات.",
    "rankings.empty": "لا توجد بطولات مكتملة بعد.",

    "events.title": "الفعاليات والبطولات",
    "events.subtitle":
      "الفعاليات القادمة والحديثة من بطولات وتجمعات ومعسكرات.",
    "events.empty": "لا توجد فعاليات مجدولة.",
    "events.location": "المكان",
    "events.fee": "رسوم المشاركة",
    "events.seats": "المقاعد",
    "events.unlimited": "مفتوح",
    "events.view_details": "عرض التفاصيل",

    "clubs.title": "الأندية المعتمدة",
    "clubs.subtitle":
      "ابحث عن ناد رسمي للملاكمة التايلاندية في مدينتك.",
    "clubs.empty": "لا توجد أندية مفعّلة بعد.",
    "clubs.member_count": "أعضاء",
    "clubs.contact": "التواصل",
    "clubs.website": "الموقع",

    "safeguarding.title": "الإبلاغ عن مخالفة",
    "safeguarding.subtitle":
      "الإبلاغ السري عن مخالفات المنشطات، مخاوف حماية اللاعبين، التحرش، أو قضايا النزاهة.",

    "watch.title": "مشاهدة مباراة حية",
    "watch.subtitle":
      "شاشة عامة للقراءة فقط تعرض النتائج الحية للمباراة. أدخل رمز المباراة للاتصال بالبث.",

    "about.title": "عن الاتحاد",
    "about.subtitle":
      "الاتحاد السعودي للملاكمة التايلاندية هو الجهة المسؤولة عن تنظيم الممارسة والمنافسات والتطوير في المملكة.",

    "language.switch": "تغيير اللغة",

    "common.loading": "جارٍ التحميل…",
    "common.refresh": "تحديث",
    "common.error_generic": "حدث خطأ ما.",

    "footer.tagline":
      "تنظيم الملاكمة التايلاندية في المملكة — التسجيل والمنافسات والنزاهة.",
    "footer.explore": "استكشف",
    "footer.integrity": "النزاهة والدعم",
  },
} as const;

export type MessageKey = keyof typeof strings.en;
