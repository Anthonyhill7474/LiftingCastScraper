/* eslint-disable no-unused-vars */

export interface PointsSystemOption {
  id: string
  label: string
}

export const POINTS_SYSTEMS: PointsSystemOption[]
export const DEFAULT_POINTS_SYSTEM: string
export const loadPointsSystem: (storage: Pick<Storage, 'getItem'>) => string
export const savePointsSystem: (
  storage: Pick<Storage, 'setItem'>,
  pointsSystem: string,
) => string
