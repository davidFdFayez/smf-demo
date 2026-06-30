import { httpClient } from "./httpClient";

export interface RecentMember {
  id: string;
  fullName: string;
  smF_ID: string;
  role: string;
  status: string;
  createdAtUtc: string;
}

export interface AdminStats {
  totalMembers: number;
  membersByRole: Record<string, number>;
  membersByStatus: Record<string, number>;
  pendingMemberApprovals: number;
  totalClubs: number;
  pendingClubApprovals: number;
  totalEvents: number;
  upcomingEvents: number;
  membershipRevenueThisMonthMinor: number;
  eventRevenueThisMonthMinor: number;
  recentRegistrations: RecentMember[];
}

export async function getAdminStats(signal?: AbortSignal): Promise<AdminStats> {
  const { data } = await httpClient.get<AdminStats>("/api/admin/stats", { signal });
  return data;
}
