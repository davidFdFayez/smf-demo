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

export interface CreateCourseInput {
  title: string;
  summary: string;
  category: CourseCategory;
  level: CourseLevel;
  instructorName: string;
  coverImageUrl?: string;
}

export interface AddLessonInput {
  title: string;
  summary: string;
  content: string;
  videoUrl?: string;
  estimatedMinutes: number;
}

export async function listAllCourses(signal?: AbortSignal): Promise<CourseSummary[]> {
  const { data } = await httpClient.get<CourseSummary[]>("/api/courses/admin", { signal });
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

export async function createCourse(input: CreateCourseInput): Promise<CourseDetails> {
  const { data } = await httpClient.post<CourseDetails>("/api/courses", input);
  return data;
}

export async function addLesson(
  courseId: string,
  input: AddLessonInput,
): Promise<LessonDetails> {
  const { data } = await httpClient.post<LessonDetails>(
    `/api/courses/${courseId}/lessons`,
    input,
  );
  return data;
}

export async function publishCourse(id: string): Promise<CourseDetails> {
  const { data } = await httpClient.post<CourseDetails>(
    `/api/courses/${encodeURIComponent(id)}/publish`,
  );
  return data;
}

export async function archiveCourse(id: string): Promise<void> {
  await httpClient.post(`/api/courses/${encodeURIComponent(id)}/archive`);
}
