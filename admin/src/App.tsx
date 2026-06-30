import { Navigate, Route, Routes } from "react-router-dom";
import { AdminLayout } from "./layouts/AdminLayout";
import { DashboardPage } from "./pages/DashboardPage";
import { MembersPage } from "./pages/MembersPage";
import { MemberDetailPage } from "./pages/MemberDetailPage";
import { ClubsPage } from "./pages/ClubsPage";
import { EventsPage } from "./pages/EventsPage";
import { NewsPage } from "./pages/NewsPage";
import { SafeguardingPage } from "./pages/SafeguardingPage";
import { RefereePage } from "./pages/RefereePage";
import { HeadRefereePage } from "./pages/HeadRefereePage";
import { TimekeeperPage } from "./pages/TimekeeperPage";
import { BracketsPage } from "./pages/BracketsPage";
import { CertificatesPage } from "./pages/CertificatesPage";
import { ExportsPage } from "./pages/ExportsPage";
import { LearnPage } from "./pages/LearnPage";
import { TenantsPage } from "./pages/TenantsPage";
import { AnalyticsPage } from "./pages/AnalyticsPage";
import { StoreProductsPage } from "./pages/StoreProductsPage";
import { StoreOrdersPage } from "./pages/StoreOrdersPage";
import { StoreOrderDetailPage } from "./pages/StoreOrderDetailPage";
import { BroadcastsPage } from "./pages/BroadcastsPage";
import { FeedbackPage } from "./pages/FeedbackPage";
import { SocialHighlightsPage } from "./pages/SocialHighlightsPage";
import { GovernanceDocumentsPage } from "./pages/GovernanceDocumentsPage";
import { PolicyVersionsPage } from "./pages/PolicyVersionsPage";
import { ConsentLogPage } from "./pages/ConsentLogPage";
import { NotFoundPage } from "./pages/NotFoundPage";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<AdminLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="members" element={<MembersPage />} />
        <Route path="members/:id" element={<MemberDetailPage />} />
        <Route path="clubs" element={<ClubsPage />} />
        <Route path="events" element={<EventsPage />} />
        <Route path="news" element={<NewsPage />} />
        <Route path="safeguarding" element={<SafeguardingPage />} />
        <Route path="referee" element={<RefereePage />} />
        <Route path="head-referee" element={<HeadRefereePage />} />
        <Route path="timekeeper" element={<TimekeeperPage />} />
        <Route path="brackets" element={<BracketsPage />} />
        <Route path="certificates" element={<CertificatesPage />} />
        <Route path="learn" element={<LearnPage />} />
        <Route path="tenants" element={<TenantsPage />} />
        <Route path="analytics" element={<AnalyticsPage />} />
        <Route path="exports" element={<ExportsPage />} />
        <Route path="store/products" element={<StoreProductsPage />} />
        <Route path="store/orders" element={<StoreOrdersPage />} />
        <Route path="store/orders/:id" element={<StoreOrderDetailPage />} />
        <Route path="broadcasts" element={<BroadcastsPage />} />
        <Route path="feedback" element={<FeedbackPage />} />
        <Route path="social-highlights" element={<SocialHighlightsPage />} />
        <Route path="governance" element={<GovernanceDocumentsPage />} />
        <Route path="policies" element={<PolicyVersionsPage />} />
        <Route path="consents" element={<ConsentLogPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
