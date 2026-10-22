/* eslint-disable no-unused-vars */

export interface StrategyDivision {
  divisionId?: string | null
  weightClassId?: string | null
  gender?: string | null
  rawOrEquipped?: string | null
  lifts?: {
    squat?: boolean | null
    bench?: boolean | null
    dead?: boolean | null
  }
}

export interface StrategyLifter {
  documentId: string
  divisions: StrategyDivision[]
}

export const getEligibleStrategyOpponents: <T extends StrategyLifter>(
  lifters: readonly T[],
  primaryLifterId: string,
  selectedDivision: StrategyDivision | undefined,
  alreadySelectedIds?: readonly string[],
) => T[]

export const getCompatibleBestOverallDivision: <T extends StrategyLifter>(
  lifter: T,
  selectedDivision: StrategyDivision | undefined,
) => T['divisions'][number] | null

export const getEligibleBestOverallOpponents: <T extends StrategyLifter>(
  lifters: readonly T[],
  primaryLifterId: string,
  selectedDivision: StrategyDivision | undefined,
  alreadySelectedIds?: readonly string[],
) => T[]

export const updateScenario: <T>(
  scenarios: Record<string, T>,
  key: string,
  scenario: T | null,
) => Record<string, T>

export const resetScenarios: <T>() => Record<string, T>
