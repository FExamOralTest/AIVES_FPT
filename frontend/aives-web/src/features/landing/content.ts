// Landing copy, taken from the AIVES SRS (business rules BR-01…BR-13, user stories US-01…US-12),
// updated for the four roles in use: ADMIN, STAFF, EXAMINER, STUDENT.

export const NAV_LINKS = [
  { href: '#how', label: 'How it works' },
  { href: '#features', label: 'Features' },
  { href: '#roles', label: 'Roles' },
  { href: '#faq', label: 'FAQ' },
]

export const FACTS = [
  { value: '4', label: 'Roles in the system' },
  { value: '4', label: 'Steps per viva exam' },
  { value: '2', label: '.md files per course' },
  { value: '100%', label: 'Scores reviewed by examiners' },
]

export const STEPS = [
  { title: 'Enter the join code', body: 'The code from your lecturer opens the right viva session for you.', tone: 'dark' },
  { title: 'Answer by voice', body: 'The AI reads each question aloud, you answer through your microphone and the system records a transcript.', tone: 'brand' },
  { title: 'AI scores and comments', body: 'The AI scores every answer against the course content and drafts feedback.', tone: 'sun' },
  { title: 'Examiners publish', body: 'An examiner reviews the scores, adjusts them if needed, then publishes the official result.', tone: 'light' },
] as const

export const COURSE_FILES = [
  { name: 'course-content.md', kind: 'Course content' },
  { name: 'learning-outcomes.md', kind: 'Learning outcomes' },
]

export const FEATURES = [
  { title: 'Questions stay on the syllabus', body: 'Staff upload the course content and learning outcomes; the AI only asks within that scope.' },
  { title: 'Follow-up when an answer is unclear', body: 'The AI asks follow-up questions so you can explain further, within the question limit.' },
  { title: 'A transcript for every answer', body: 'Every answer is recorded so examiners and students can review it later.' },
  { title: 'Reviewed before it is published', body: 'AI scores are only suggestions until an examiner approves them.' },
]

export const ROLES = [
  {
    key: 'student',
    name: 'Students',
    items: ['Join an exam with a code', 'Answer by voice', 'See scores, feedback and transcripts'],
  },
  {
    key: 'teacher',
    name: 'Staff & examiners',
    items: ['Upload course content and learning outcomes', 'Create exams and schedule sessions', 'Review AI results and publish scores'],
  },
  {
    key: 'admin',
    name: 'Administrators',
    items: ['Create and manage courses', 'Issue staff and examiner accounts', 'Control access by role'],
  },
] as const

export type RoleKey = (typeof ROLES)[number]['key']

export const FAQS = [
  {
    q: 'Who can create an account?',
    a: 'Students sign up with their email. Staff, examiner and admin accounts are issued by the school.',
  },
  {
    q: 'Is the AI score the official score?',
    a: 'No. The AI only suggests a score. The official score is the one an examiner has reviewed and published.',
  },
  {
    q: 'What do I need to take an exam?',
    a: 'A computer with a microphone, a web browser and the join code from your lecturer.',
  },
  {
    q: 'Who can see my answers?',
    a: 'Only you and the staff and examiners in charge of the exam. Data is protected under Decree 13/2023/ND-CP.',
  },
]
