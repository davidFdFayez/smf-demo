import { z } from "zod";

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

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** HTML selects submit "" for the placeholder option — treat as missing. */
function emptyToUndefined(val: unknown): unknown {
  if (val === "" || val === null || val === undefined) return undefined;
  return val;
}

const GUARDIAN_RELATIONS = [
  "Mother",
  "Father",
  "LegalGuardian",
  "Grandparent",
  "Sibling",
  "Other",
] as const;

/** Age in whole years on `onDate` (local, no timezone drift). */
export function calculateAge(dateOfBirthIso: string, onDate = new Date()): number {
  const dob = new Date(`${dateOfBirthIso}T00:00:00`);
  if (Number.isNaN(dob.getTime())) return NaN;

  let age = onDate.getFullYear() - dob.getFullYear();
  const m = onDate.getMonth() - dob.getMonth();
  if (m < 0 || (m === 0 && onDate.getDate() < dob.getDate())) {
    age -= 1;
  }
  return age;
}

export function isMinor(dateOfBirthIso: string, onDate = new Date()): boolean {
  const age = calculateAge(dateOfBirthIso, onDate);
  return Number.isFinite(age) && age < 18;
}

// Matches the FluentValidation regex on the server (RegisterMemberCommandValidator).
const PHONE_PATTERN = /^\+?[0-9][0-9\s\-]{6,20}$/;
const NATIONAL_ID_PATTERN = /^[A-Za-z0-9\-]{5,30}$/;

/**
 * Mirrors the backend `RegisterMemberCommand`:
 *   FullName: string
 *   DateOfBirth: DateOnly (yyyy-MM-dd)
 *   Role: MemberRole (string enum)
 *   GuardianConsent: bool
 *   Email / PhoneNumber / NationalId: string (per PDF §3 Role-Specific Fields)
 *   AcceptTerms / AcceptPrivacyPolicy / AcceptCodeOfConduct: bool
 *     (per PDF §3 "Compliance at Signup" + §8 "Privacy & Terms")
 *
 * The superRefine enforces the same business rule as the FluentValidation validator:
 * Athletes under 18 must have GuardianConsent === true.
 */
export const registerMemberSchema = z
  .object({
    fullName: z
      .string()
      .trim()
      .min(1, "Full name is required.")
      .max(200, "Full name must be at most 200 characters."),

    dateOfBirth: z
      .string()
      .regex(ISO_DATE, "Date of birth must be a valid date.")
      .refine((value) => {
        const d = new Date(`${value}T00:00:00`);
        return !Number.isNaN(d.getTime()) && d <= new Date();
      }, "Date of birth must be in the past."),

    role: z.nativeEnum(MemberRole, {
      errorMap: () => ({ message: "Please select a role." }),
    }),

    guardianConsent: z.boolean(),

    email: z
      .string()
      .trim()
      .min(1, "Email is required.")
      .max(200, "Email must be at most 200 characters.")
      .email("Email must be a valid email address."),

    phoneNumber: z
      .string()
      .trim()
      .min(1, "Phone number is required.")
      .max(32, "Phone number must be at most 32 characters.")
      .regex(PHONE_PATTERN, "Phone number format is invalid."),

    nationalId: z
      .string()
      .trim()
      .min(1, "National ID is required.")
      .max(32, "National ID must be at most 32 characters.")
      .regex(
        NATIONAL_ID_PATTERN,
        "National ID must be 5–30 letters, digits, or dashes.",
      ),

    acceptTerms: z.boolean(),
    acceptPrivacyPolicy: z.boolean(),
    acceptCodeOfConduct: z.boolean(),

    // Minor-athlete digital consent fields. Optional unless the superRefine
    // block enforces them for athletes under 18 without in-person consent.
    guardianFullName: z.preprocess(
      emptyToUndefined,
      z.string().trim().max(200).optional(),
    ),
    guardianRelation: z.preprocess(
      emptyToUndefined,
      z.enum(GUARDIAN_RELATIONS).optional(),
    ),
    guardianEmail: z.preprocess(
      emptyToUndefined,
      z
        .string()
        .trim()
        .max(200)
        .email("Guardian email must be valid.")
        .optional(),
    ),
    guardianPhone: z.preprocess(
      emptyToUndefined,
      z.string().trim().max(32).optional(),
    ),
    guardianNationalId: z.preprocess(
      emptyToUndefined,
      z.string().trim().max(64).optional(),
    ),
  })
  .superRefine((data, ctx) => {
    const minorAthlete = data.role === MemberRole.Athlete && isMinor(data.dateOfBirth);

    // Either the legacy in-person bool, OR all four guardian-detail fields.
    if (minorAthlete && !data.guardianConsent) {
      const fields: Array<[keyof RegisterMemberInput, string]> = [
        ["guardianFullName", "Guardian full name is required."],
        ["guardianEmail",    "Guardian email is required."],
        ["guardianPhone",    "Guardian phone is required."],
        ["guardianRelation", "Guardian relation is required."],
      ];
      for (const [path, message] of fields) {
        const value = data[path];
        if (!value || (typeof value === "string" && !value.trim())) {
          ctx.addIssue({ code: z.ZodIssueCode.custom, path: [path as string], message });
        }
      }
    }

    if (!data.acceptTerms) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["acceptTerms"],
        message: "You must accept the Terms of Service.",
      });
    }
    if (!data.acceptPrivacyPolicy) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["acceptPrivacyPolicy"],
        message: "You must accept the Privacy Policy.",
      });
    }
    if (!data.acceptCodeOfConduct) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["acceptCodeOfConduct"],
        message: "You must accept the Code of Conduct.",
      });
    }
  });

export type RegisterMemberInput = z.infer<typeof registerMemberSchema>;

export interface RegisterMemberResponse {
  id: string;
  smF_ID: string;
  registrationStatus: RegistrationStatus;
  parentalConsentId: string | null;
  parentalConsentExpiresAtUtc: string | null;
  guardianSignUrl: string | null;
}

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
