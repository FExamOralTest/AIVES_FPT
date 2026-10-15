export type SessionStatus = 'NOT_STARTED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED'

export const SESSION_STATUS_LABEL: Record<SessionStatus, string> = {
  NOT_STARTED: 'Chưa bắt đầu',
  IN_PROGRESS: 'Đang diễn ra',
  COMPLETED: 'Đã kết thúc',
  CANCELLED: 'Đã huỷ',
}

export const SESSION_STATUS_TONE: Record<SessionStatus, string> = {
  NOT_STARTED: 'bg-muted text-foreground',
  IN_PROGRESS: 'bg-success-soft text-success',
  COMPLETED: 'bg-brand-soft text-brand',
  CANCELLED: 'bg-[#fef1f0] text-destructive',
}

/** Backend state machine (BR-16): completed and cancelled are final. */
export const SESSION_TRANSITIONS: Record<SessionStatus, SessionStatus[]> = {
  NOT_STARTED: ['IN_PROGRESS', 'CANCELLED'],
  IN_PROGRESS: ['COMPLETED', 'CANCELLED'],
  COMPLETED: [],
  CANCELLED: [],
}

// Request bodies need the enum number: the backend has no JsonStringEnumConverter yet.
export const SESSION_STATUS_CODE: Record<SessionStatus, number> = {
  NOT_STARTED: 0,
  IN_PROGRESS: 1,
  COMPLETED: 2,
  CANCELLED: 3,
}
