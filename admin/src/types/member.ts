export const MemberRole = {
  Athlete: "Athlete",
  Coach: "Coach",
  Referee: "Referee",
  ClubAdmin: "ClubAdmin",
  FederationStaff: "FederationStaff",
  Visitor: "Visitor",
} as const;
export type MemberRole = (typeof MemberRole)[keyof typeof MemberRole];

export const RegistrationStatus = {
  Pending: "Pending",
  Approved: "Approved",
  Active: "Active",
} as const;
export type RegistrationStatus =
  (typeof RegistrationStatus)[keyof typeof RegistrationStatus];

export interface MemberListItem {
  id: string;
  fullName: string;
  dateOfBirth: string;
  role: MemberRole;
  smF_ID: string;
  registrationStatus: RegistrationStatus;
  guardianConsent: boolean;
  email: string;
  phoneNumber: string;
  nationalId: string;
  createdAtUtc: string;
  affiliatedClubId?: string | null;
  licenseLevel?: string | null;
  yearsOfExperience?: number | null;
  weightCategoryKg?: number | null;
  medicalCleared?: boolean | null;
  medicalClearedAtUtc?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
