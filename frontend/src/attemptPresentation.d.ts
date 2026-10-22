/* eslint-disable no-unused-vars */

export interface AttemptView {
  liftName?: string | null
  attemptNumber?: unknown
  weight?: unknown
  result?: unknown
}

export interface LifterAttemptView {
  attempts: AttemptView[]
}

export const getLiftType: (liftName?: string | null) => string | null
export const getAttemptNumber: (value?: unknown) => number | null
export const getAttemptForSlot: (
  lifter: LifterAttemptView,
  lift: string,
  number: number,
) => AttemptView | undefined
export const describeAttempt: (attempt?: AttemptView) => string
export const getAttemptStatus: (
  attempt?: AttemptView,
) => 'missing' | 'success' | 'failure' | 'pending' | 'incomplete'
