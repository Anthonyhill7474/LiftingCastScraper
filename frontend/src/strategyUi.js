export function getEligibleStrategyOpponents(
  lifters,
  primaryLifterId,
  selectedDivision,
  alreadySelectedIds = [],
) {
  if (!selectedDivision?.divisionId || !selectedDivision.weightClassId) return []

  return lifters.filter((lifter) =>
    lifter.documentId !== primaryLifterId
      && !alreadySelectedIds.includes(lifter.documentId)
      && lifter.divisions.some((division) =>
        division.divisionId === selectedDivision.divisionId
          && division.weightClassId === selectedDivision.weightClassId
          && division.gender === selectedDivision.gender
          && division.rawOrEquipped === selectedDivision.rawOrEquipped
          && division.lifts?.squat === selectedDivision.lifts?.squat
          && division.lifts?.bench === selectedDivision.lifts?.bench
          && division.lifts?.dead === selectedDivision.lifts?.dead,
      ),
  )
}

export function updateScenario(scenarios, key, scenario) {
  if (scenario === null) {
    const { [key]: removed, ...remaining } = scenarios
    void removed
    return remaining
  }
  return { ...scenarios, [key]: scenario }
}

export function resetScenarios() {
  return {}
}
