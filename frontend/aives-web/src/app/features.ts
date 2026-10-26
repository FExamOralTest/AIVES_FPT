import {
  BookOpen,
  CalendarClock,
  ChartColumn,
  ClipboardCheck,
  FileText,
  KeyRound,
  LibraryBig,
  ListChecks,
  Mic,
  Trophy,
  Users,
  type LucideIcon,
} from 'lucide-react'
import type { UserRole } from '@/features/auth'

/**
 * - done:    the page is built and calls the real API (register it in PAGES in router.tsx).
 * - pending: waiting for backend APIs; renders FeaturePlaceholderPage with the endpoints below.
 */
export type FeatureStatus = 'done' | 'pending'

/** ok = callable now · missing = not implemented by the backend yet. */
export type EndpointState = 'ok' | 'missing'

export interface FeatureEndpoint {
  method: 'GET' | 'POST' | 'PUT' | 'DELETE'
  path: string
  state: EndpointState
  note: string
}

export interface FeatureDef {
  role: UserRole
  /** Path segment under the role's home, e.g. "users" → /admin/users. */
  path: string
  title: string
  icon: LucideIcon
  summary: string
  status: FeatureStatus
  /** `flush` pages fill the content area edge to edge and draw their own bars; default is a padded column. */
  layout?: 'flush'
  /** Only for pending features: what the placeholder page lists as still needed. */
  endpoints?: FeatureEndpoint[]
  blockers?: string[]
}

export const FEATURES: FeatureDef[] = [
  // ── Admin ──────────────────────────────────────────────────────────
  {
    role: 'ADMIN',
    path: 'users',
    title: 'Users',
    icon: Users,
    summary: 'Create staff, examiner and student accounts, edit details and change roles.',
    status: 'done',
    layout: 'flush',
  },
  {
    role: 'ADMIN',
    path: 'courses',
    title: 'Courses',
    icon: BookOpen,
    summary: 'Create courses and assign the staff member in charge.',
    status: 'done',
    layout: 'flush',
  },
  {
    role: 'ADMIN',
    path: 'statistics',
    title: 'Statistics',
    icon: ChartColumn,
    summary: 'Sessions, attempts, scores and review progress for the whole system or one course.',
    status: 'done',
  },
  {
    role: 'ADMIN',
    path: 'reports',
    title: 'Exams & results',
    icon: ListChecks,
    summary: 'Browse every exam session and open it to see each student’s result.',
    status: 'done',
    layout: 'flush',
  },

  // ── Staff ──────────────────────────────────────────────────────────
  {
    role: 'STAFF',
    path: 'courses',
    title: 'Courses & documents',
    icon: BookOpen,
    summary: 'See your assigned courses and upload the two .md files: course content and learning outcomes.',
    status: 'done',
  },
  {
    role: 'STAFF',
    path: 'questions',
    title: 'Question bank',
    icon: LibraryBig,
    summary: 'Generate questions with AI from course documents, then review and edit them.',
    status: 'done',
  },
  {
    role: 'STAFF',
    path: 'exams',
    title: 'Exams',
    icon: FileText,
    summary: 'Build exams from the question bank and set the duration and number of questions.',
    status: 'done',
  },
  {
    role: 'STAFF',
    path: 'sessions',
    title: 'Exam sessions',
    icon: CalendarClock,
    summary: 'Schedule sessions, share the join code, start or close a session and monitor it live.',
    status: 'done',
    layout: 'flush',
  },

  // ── Examiner ───────────────────────────────────────────────────────
  {
    role: 'EXAMINER',
    path: 'results',
    title: 'Review & publish',
    icon: ClipboardCheck,
    summary: 'Check AI-suggested scores, replay answers, adjust scores and publish results.',
    status: 'done',
  },

  // ── Student ────────────────────────────────────────────────────────
  {
    role: 'STUDENT',
    path: 'enroll',
    title: 'Join a session',
    icon: KeyRound,
    summary: 'Enter the join code from your lecturer to register for an exam session.',
    status: 'done',
  },
  {
    role: 'STUDENT',
    path: 'sessions',
    title: 'My sessions',
    icon: CalendarClock,
    summary: 'Your registered sessions, their start times and status.',
    status: 'done',
  },
  {
    role: 'STUDENT',
    path: 'viva',
    title: 'Viva room',
    icon: Mic,
    summary: 'Listen to the AI examiner, answer by voice and get follow-up questions when needed.',
    status: 'done',
  },
  {
    role: 'STUDENT',
    path: 'results',
    title: 'Results',
    icon: Trophy,
    summary: 'See your scores, feedback and transcript once they are published.',
    status: 'done',
  },
]

export function featuresFor(role: UserRole) {
  return FEATURES.filter((f) => f.role === role)
}

export const STATUS_LABEL: Record<FeatureStatus, string> = {
  done: 'Live',
  pending: 'Coming soon',
}

export const ENDPOINT_STATE_LABEL: Record<EndpointState, string> = {
  ok: 'Available',
  missing: 'Missing',
}
