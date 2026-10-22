export interface WeightClassStanding {
  athleteId: string
  athleteName?: string | null
  confirmedResultKg?: number | null
  projectedResultKg?: number | null
  confirmedRank?: number | null
  confirmedStatus: string
  projectedRank?: number | null
  projectedStatus: string
  hasPendingAttempts: boolean
}

export interface OverallStanding {
  athleteId: string
  athleteName?: string | null
  hasPendingAttempts: boolean
  pointsScores: Record<string, {
    confirmedScore: number | null
    projectedScore: number | null
  }>
  rankings: Record<string, {
    confirmedRank: number | null
    confirmedStatus: string
    projectedRank: number | null
    projectedStatus: string
  }>
}

/* eslint-disable no-unused-vars */

export const getVisibleStandingsCategories: <T, U>(
  standings: {
    weightClasses: T[]
    overallCategories: U[]
  } | null,
  view: 'all-classes' | 'my-classes' | 'overall',
  followedLifterIds?: readonly string[],
) => T[] | U[]

export const filterOverallCategories: <T extends {
  gender: string
  equipmentCategory: string
  eventType: string
}>(
  categories: readonly T[],
  gender?: string,
  equipment?: string,
  eventType?: string,
) => T[]

export const getWeightClassResult: (
  standing: WeightClassStanding,
  mode: 'confirmed' | 'projected',
) => {
  value: number | null
  rank: number | null
  status: string
  source: 'confirmed' | 'hypothetical' | 'unavailable'
}

export const getOverallResult: (
  standing: OverallStanding,
  mode: 'confirmed' | 'projected',
  pointsSystem: string,
) => {
  value: number | null
  rank: number | null
  status: string
  source: 'confirmed' | 'hypothetical' | 'unavailable'
}

export const sortOverallStandings: <T extends OverallStanding>(
  standings: readonly T[],
  mode: 'confirmed' | 'projected',
  pointsSystem: string,
) => T[]

export const sortWeightClassStandings: <T extends WeightClassStanding>(
  standings: readonly T[],
  mode: 'confirmed' | 'projected',
) => T[]
