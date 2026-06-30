import { httpClient } from "./httpClient";

export type CourseCategory =
  | "AthleteFundamentals"
  | "CoachingCertification"
  | "RefereeEducation"
  | "SafeguardingAndIntegrity"
  | "AntiDoping"
  | "StrengthAndConditioning"
  | "General";

export type CourseLevel = "Beginner" | "Intermediate" | "Advanced" | "Certification";

export type CourseEnrollmentStatus = "Active" | "Completed" | "Dropped";

export interface LessonSummary {
  id: string;
  order: number;
  title: string;
  summary: string;
  estimatedMinutes: number;
  hasVideo: boolean;
}

export interface LessonDetails {
  id: string;
  courseId: string;
  order: number;
  title: string;
  summary: string;
  content: string;
  videoUrl: string | null;
  estimatedMinutes: number;
}

export interface CourseSummary {
  id: string;
  slug: string;
  title: string;
  summary: string;
  category: CourseCategory;
  level: CourseLevel;
  instructorName: string;
  coverImageUrl: string | null;
  lessonCount: number;
  totalMinutes: number;
  enrollmentCount: number;
  isPublished: boolean;
  isArchived: boolean;
  createdAtUtc: string;
  publishedAtUtc: string | null;
}

export interface CourseDetails extends Omit<CourseSummary, "lessonCount"> {
  lessons: LessonSummary[];
}

export interface EnrollmentProgress {
  enrollmentId: string;
  courseId: string;
  memberId: string;
  status: CourseEnrollmentStatus;
  lessonsCompleted: number;
  lessonsTotal: number;
  progressPercent: number;
  enrolledAtUtc: string;
  completedAtUtc: string | null;
}

export async function listPublishedCourses(
  category?: CourseCategory,
  level?: CourseLevel,
  signal?: AbortSignal,
): Promise<CourseSummary[]> {
  const params: Record<string, string> = {};
  if (category) params.category = category;
  if (level) params.level = level;
  const { data } = await httpClient.get<CourseSummary[]>("/api/courses", {
    params,
    signal,
  });
  return data;
}

export async function getCourseBySlug(
  slug: string,
  signal?: AbortSignal,
): Promise<CourseDetails> {
  const { data } = await httpClient.get<CourseDetails>(
    `/api/courses/${encodeURIComponent(slug)}`,
    { signal },
  );
  return data;
}

export async function getLesson(
  courseId: string,
  lessonId: string,
  signal?: AbortSignal,
): Promise<LessonDetails> {
  const { data } = await httpClient.get<LessonDetails>(
    `/api/courses/${courseId}/lessons/${lessonId}`,
    { signal },
  );
  return data;
}

export async function enrollInCourse(
  courseId: string,
  memberId: string,
): Promise<EnrollmentProgress> {
  const { data } = await httpClient.post<EnrollmentProgress>(
    `/api/courses/${courseId}/enroll`,
    { memberId },
  );
  return data;
}

export async function completeLesson(
  enrollmentId: string,
  lessonId: string,
): Promise<EnrollmentProgress> {
  const { data } = await httpClient.post<EnrollmentProgress>(
    `/api/courses/enrollments/${enrollmentId}/lessons/${lessonId}/complete`,
  );
  return data;
}

export async function getEnrollment(
  courseId: string,
  memberId: string,
  signal?: AbortSignal,
): Promise<EnrollmentProgress | null> {
  const res = await httpClient.get<EnrollmentProgress>(
    `/api/courses/${courseId}/enrollments/by-member/${memberId}`,
    { signal, validateStatus: (s) => s === 200 || s === 204 },
  );
  return res.status === 204 ? null : res.data;
}
