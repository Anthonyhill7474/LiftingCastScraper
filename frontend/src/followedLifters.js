export function loadFollowedLifterIds(storage, competitionId) {
  const stored = storage.getItem(`liftingcast:followed-lifters:${competitionId}`)
  if (stored === null) return []

  const parsed = JSON.parse(stored)
  if (!Array.isArray(parsed)) {
    throw new TypeError('Saved followed lifters must be an array.')
  }

  return [...new Set(parsed.filter((id) => typeof id === 'string'))]
}

export function saveFollowedLifterIds(storage, competitionId, lifterIds) {
  const uniqueIds = [...new Set(lifterIds)]
  storage.setItem(
    `liftingcast:followed-lifters:${competitionId}`,
    JSON.stringify(uniqueIds),
  )
  return uniqueIds
}

export function toggleFollowedLifter(lifterIds, lifterId) {
  return lifterIds.includes(lifterId)
    ? lifterIds.filter((id) => id !== lifterId)
    : [...lifterIds, lifterId]
}

export function filterFollowedLifters(lifters, lifterIds) {
  const followedIds = new Set(lifterIds)
  return lifters.filter((lifter) => followedIds.has(lifter.documentId))
}

export function getMissingFollowedLifterIds(lifterIds, lifters) {
  const availableIds = new Set(lifters.map((lifter) => lifter.documentId))
  return lifterIds.filter((id) => !availableIds.has(id))
}
