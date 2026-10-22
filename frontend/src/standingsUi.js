export function getVisibleStandingsCategories(standings, view, followedLifterIds = []) {
  if (!standings) return []
  if (view === 'overall') return standings.overallCategories
  if (view === 'my-classes') {
    const followed = new Set(followedLifterIds)
    return standings.weightClasses.filter((category) =>
      category.standings.some((standing) => followed.has(standing.athleteId)),
    )
  }
  return standings.weightClasses
}

export function filterOverallCategories(categories, gender, equipment, eventType) {
  return categories.filter((category) =>
    (!gender || category.gender === gender)
      && (!equipment || category.equipmentCategory === equipment)
      && (!eventType || category.eventType === eventType),
  )
}

export function getWeightClassResult(standing, mode) {
  if (mode === 'projected') {
    return {
      value: standing.projectedResultKg,
      rank: standing.projectedRank,
      status: standing.projectedStatus,
      source: standing.hasPendingAttempts && standing.projectedResultKg !== null
        ? 'hypothetical'
        : 'unavailable',
    }
  }

  return {
    value: standing.confirmedResultKg,
    rank: standing.confirmedRank,
    status: standing.confirmedStatus,
    source: 'confirmed',
  }
}

export function getOverallResult(standing, mode, pointsSystem) {
  const scores = standing.pointsScores?.[pointsSystem]
  const ranking = standing.rankings?.[pointsSystem]
  if (mode === 'projected') {
    return {
      value: scores?.projectedScore ?? null,
      rank: ranking?.projectedRank ?? null,
      status: ranking?.projectedStatus ?? 'unavailable',
      source: scores?.projectedScore !== null && scores?.projectedScore !== undefined
        ? 'hypothetical'
        : 'unavailable',
    }
  }

  return {
    value: scores?.confirmedScore ?? null,
    rank: ranking?.confirmedRank ?? null,
    status: ranking?.confirmedStatus ?? 'unavailable',
    source: 'confirmed',
  }
}

export function sortOverallStandings(standings, mode, pointsSystem) {
  return [...standings].sort((left, right) => {
    const leftResult = getOverallResult(left, mode, pointsSystem)
    const rightResult = getOverallResult(right, mode, pointsSystem)
    const leftRank = leftResult.rank ?? Number.MAX_SAFE_INTEGER
    const rightRank = rightResult.rank ?? Number.MAX_SAFE_INTEGER
    if (leftRank !== rightRank) return leftRank - rightRank

    const leftValue = leftResult.value ?? Number.NEGATIVE_INFINITY
    const rightValue = rightResult.value ?? Number.NEGATIVE_INFINITY
    if (leftValue !== rightValue) return rightValue - leftValue
    return String(left.athleteName || left.athleteId)
      .localeCompare(String(right.athleteName || right.athleteId))
  })
}

export function sortWeightClassStandings(standings, mode) {
  return [...standings].sort((left, right) => {
    const leftResult = getWeightClassResult(left, mode)
    const rightResult = getWeightClassResult(right, mode)
    const leftRank = leftResult.rank ?? Number.MAX_SAFE_INTEGER
    const rightRank = rightResult.rank ?? Number.MAX_SAFE_INTEGER
    if (leftRank !== rightRank) return leftRank - rightRank

    const leftValue = leftResult.value ?? Number.NEGATIVE_INFINITY
    const rightValue = rightResult.value ?? Number.NEGATIVE_INFINITY
    if (leftValue !== rightValue) return rightValue - leftValue
    return String(left.athleteName || left.athleteId)
      .localeCompare(String(right.athleteName || right.athleteId))
  })
}
