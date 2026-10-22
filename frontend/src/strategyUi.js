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
          && normalizeGender(division.gender) === normalizeGender(selectedDivision.gender)
          && normalizeEquipment(division.rawOrEquipped) === normalizeEquipment(selectedDivision.rawOrEquipped)
          && division.lifts?.squat === selectedDivision.lifts?.squat
          && division.lifts?.bench === selectedDivision.lifts?.bench
          && division.lifts?.dead === selectedDivision.lifts?.dead,
      ),
  )
}

export function getCompatibleBestOverallDivision(lifter, selectedDivision) {
  if (!selectedDivision?.gender
    || !selectedDivision.rawOrEquipped
    || !selectedDivision.lifts
    || !hasCompleteLiftConfiguration(selectedDivision.lifts)) {
    return null
  }

  const compatible = lifter.divisions
    .filter((division) =>
      division.divisionId
        && division.weightClassId
        && normalizeGender(division.gender) === normalizeGender(selectedDivision.gender)
        && normalizeEquipment(division.rawOrEquipped) === normalizeEquipment(selectedDivision.rawOrEquipped)
        && hasSameLifts(division.lifts, selectedDivision.lifts),
    )
    .sort((left, right) =>
      Number(right.divisionId === selectedDivision.divisionId)
        - Number(left.divisionId === selectedDivision.divisionId)
        || String(left.weightClassId).localeCompare(String(right.weightClassId)),
    )

  return compatible[0] ?? null
}

export function getEligibleBestOverallOpponents(
  lifters,
  primaryLifterId,
  selectedDivision,
  alreadySelectedIds = [],
) {
  return lifters.filter((lifter) =>
    lifter.documentId !== primaryLifterId
      && !alreadySelectedIds.includes(lifter.documentId)
      && getCompatibleBestOverallDivision(lifter, selectedDivision) !== null,
  )
}

function normalizeGender(gender) {
  return typeof gender === 'string' ? gender.trim().toLowerCase() : ''
}

function normalizeEquipment(rawOrEquipped) {
  switch (typeof rawOrEquipped === 'string' ? rawOrEquipped.trim().toLowerCase() : '') {
    case 'raw':
    case 'classic':
      return 'classic'
    case 'equipped':
      return 'equipped'
    default:
      return ''
  }
}

function hasCompleteLiftConfiguration(lifts) {
  return lifts
    && typeof lifts.squat === 'boolean'
    && typeof lifts.bench === 'boolean'
    && typeof lifts.dead === 'boolean'
    && (lifts.squat || lifts.bench || lifts.dead)
}

function hasSameLifts(left, right) {
  return hasCompleteLiftConfiguration(left)
    && hasCompleteLiftConfiguration(right)
    && left.squat === right.squat
    && left.bench === right.bench
    && left.dead === right.dead
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
