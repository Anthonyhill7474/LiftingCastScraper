/* eslint-disable no-unused-vars */

export interface LifterDocument {
  documentId: string
}

export const loadFollowedLifterIds: (
  storage: Pick<Storage, 'getItem'>,
  competitionId: string,
) => string[]

export const saveFollowedLifterIds: (
  storage: Pick<Storage, 'setItem'>,
  competitionId: string,
  lifterIds: readonly string[],
) => string[]

export const toggleFollowedLifter: (
  lifterIds: readonly string[],
  lifterId: string,
) => string[]

export const filterFollowedLifters: <T extends LifterDocument>(
  lifters: readonly T[],
  lifterIds: readonly string[],
) => T[]

export const getMissingFollowedLifterIds: <T extends LifterDocument>(
  lifterIds: readonly string[],
  lifters: readonly T[],
) => string[]
