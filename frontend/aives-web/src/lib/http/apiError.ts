import axios from 'axios'

export interface ApiError {
  status: number | null
  message: string
  /** Field-level messages from ASP.NET model validation, keyed by camelCase field name. */
  fieldErrors: Record<string, string>
  /** The backend's original message, for branching on a specific error without matching UI text. */
  rawMessage: string | null
}

// Backend messages are already English; these rewrite the terse or technical ones for end users.
const FRIENDLY_MESSAGES: Record<string, string> = {
  'Invalid username or password.': 'Incorrect email or password.',
  'Username already exists.': 'This username is already taken.',
  'Email already exists.': 'This email is already registered. Sign in or use another email.',
  'Email already in use.': 'This email is already used by another account.',
  'Invalid or expired Enroll Code.': 'This join code is not valid. Check the code your lecturer gave you.',
  'This session is not open for enrollment.': 'This exam session has ended or was cancelled, so you can no longer join.',
  'Session has reached maximum capacity.': 'This exam session is full. Contact your lecturer for help.',
  'You are already enrolled in this session.': 'You have already joined this exam session.',
  'StartTime must be earlier than EndTime.': 'The start time must be before the end time.',
  'StartTime cannot be in the past.': 'The start time cannot be in the past.',
  'You do not have permission to create a session for this exam.': 'You did not create this exam.',
  'This question is already in use. Create a new question instead.':
    'This question is already used in an exam. Create a new question to change its content.',
  'Only pending questions can be approved.': 'This question has already been reviewed. Refresh the list to see its status.',
  'Only pending questions can be rejected.': 'This question has already been reviewed. Refresh the list to see its status.',
  'A rejection reason is required.': 'Enter a reason for rejecting this question.',
  'You are not assigned to this course.': 'You are not assigned to this course, so you cannot review its questions.',
  'This course already has a document of this type. Replace the existing document instead.':
    'This course already has a document of this type. Replace the existing one instead.',
}

// Statuses whose meaning doesn't depend on the endpoint.
const STATUS_MESSAGES: Record<number, string> = {
  403: 'Your account does not have permission to do this.',
  429: 'Too many attempts. Please wait about 15 minutes and try again.',
}

const INVALID_INPUT = 'Some details are not valid. Check the fields below.'

export function toApiError(error: unknown, fallback = 'Something went wrong. Please try again.'): ApiError {
  if (!axios.isAxiosError(error)) {
    return { status: null, message: fallback, fieldErrors: {}, rawMessage: null }
  }

  // No response, or the dev proxy couldn't reach the backend.
  if (!error.response || [502, 503, 504].includes(error.response.status)) {
    return {
      status: null,
      message: 'Cannot reach the server. Check your connection or try again later.',
      fieldErrors: {},
      rawMessage: null,
    }
  }

  const { status, data } = error.response
  const fieldErrors: Record<string, string> = {}

  if (data && typeof data === 'object' && 'errors' in data && data.errors && typeof data.errors === 'object') {
    for (const [key, messages] of Object.entries(data.errors as Record<string, string[]>)) {
      const field = key.charAt(0).toLowerCase() + key.slice(1)
      if (Array.isArray(messages) && messages.length) fieldErrors[field] = FRIENDLY_MESSAGES[messages[0]] ?? messages[0]
    }
  }

  const rawMessage =
    data && typeof data === 'object' && 'message' in data && typeof data.message === 'string'
      ? data.message
      : null

  if (status === 429) return { status, message: STATUS_MESSAGES[status], fieldErrors, rawMessage }

  const message =
    (rawMessage && FRIENDLY_MESSAGES[rawMessage]) ??
    (status === 400 && Object.keys(fieldErrors).length > 0 ? INVALID_INPUT : rawMessage) ??
    STATUS_MESSAGES[status] ??
    (status === 400 ? INVALID_INPUT : fallback)

  return { status, message, fieldErrors, rawMessage }
}
