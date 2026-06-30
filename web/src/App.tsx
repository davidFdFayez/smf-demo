import { Route, Routes } from "react-router-dom";
import { PublicLayout } from "./layouts/PublicLayout";
import { HomePage } from "./pages/HomePage";
import { RegisterPage } from "./pages/RegisterPage";
import { NewsListPage } from "./pages/NewsListPage";
import { NewsDetailPage } from "./pages/NewsDetailPage";
import { RankingsPage } from "./pages/RankingsPage";
import { EventsPage } from "./pages/EventsPage";
import { EventDetailPage } from "./pages/EventDetailPage";
import { ClubsPage } from "./pages/ClubsPage";
import { ClubMicrositePage } from "./pages/ClubMicrositePage";
import { SafeguardingPage } from "./pages/SafeguardingPage";
import { WatchMatchPage } from "./pages/WatchMatchPage";
import { ScoreboardOverlayPage } from "./pages/ScoreboardOverlayPage";
import { BracketViewerPage } from "./pages/BracketViewerPage";
import { CertificateVerifyPage } from "./pages/CertificateVerifyPage";
import { CheckoutPage as CheckoutPageFeature } from "./features/payments/CheckoutPage";
import { StorePage } from "./pages/StorePage";
import { StoreProductPage } from "./pages/StoreProductPage";
import { StoreCartPage } from "./pages/StoreCartPage";
import { StoreCheckoutPage } from "./pages/StoreCheckoutPage";
import { StoreOrderPage } from "./pages/StoreOrderPage";
import { AboutPage } from "./pages/AboutPage";
import { GovernancePage } from "./pages/GovernancePage";
import { GuardianSignPage } from "./pages/GuardianSignPage";
import { CoursesListPage } from "./pages/CoursesListPage";
import { CourseDetailPage } from "./pages/CourseDetailPage";
import { LessonReaderPage } from "./pages/LessonReaderPage";
import { NotFoundPage } from "./pages/NotFoundPage";

export default function App() {
  return (
    <Routes>
      {/* Overlay route is deliberately outside the PublicLayout so the
       * broadcaster gets just the scoreboard + timer on a transparent
       * background with no chrome. */}
      <Route path="overlay" element={<ScoreboardOverlayPage />} />

      <Route path="/" element={<PublicLayout />}>
        <Route index element={<HomePage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="news" element={<NewsListPage />} />
        <Route path="news/:slug" element={<NewsDetailPage />} />
        <Route path="rankings" element={<RankingsPage />} />
        <Route path="events" element={<EventsPage />} />
        <Route path="events/:id" element={<EventDetailPage />} />
        <Route path="tournaments/:id" element={<BracketViewerPage />} />
        <Route path="certificates/verify" element={<CertificateVerifyPage />} />
        <Route path="certificates/verify/:code" element={<CertificateVerifyPage />} />
        <Route path="clubs" element={<ClubsPage />} />
        <Route path="clubs/:slug" element={<ClubMicrositePage />} />
        <Route path="learn" element={<CoursesListPage />} />
        <Route path="learn/:slug" element={<CourseDetailPage />} />
        <Route path="learn/:slug/lessons/:lessonId" element={<LessonReaderPage />} />
        <Route path="safeguarding" element={<SafeguardingPage />} />
        <Route path="watch" element={<WatchMatchPage />} />
        <Route path="checkout" element={<CheckoutPageFeature />} />
        <Route path="store" element={<StorePage />} />
        <Route path="store/p/:id" element={<StoreProductPage />} />
        <Route path="store/cart" element={<StoreCartPage />} />
        <Route path="store/checkout" element={<StoreCheckoutPage />} />
        <Route path="store/orders/:id" element={<StoreOrderPage />} />
        <Route path="about" element={<AboutPage />} />
        <Route path="governance" element={<GovernancePage />} />
        <Route path="parental-consent/:consentId/:token" element={<GuardianSignPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
