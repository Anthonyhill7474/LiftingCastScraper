export const POINTS_SYSTEMS = [
  { id: 'ipf-gl', label: 'IPF GL' },
  { id: 'dots', label: 'DOTS' },
  { id: 'wilks-1', label: 'Wilks 1' },
  { id: 'wilks-2', label: 'Wilks 2 (2020)' },
  { id: 'legacy-ipf', label: 'Legacy IPF Points' },
]

export const DEFAULT_POINTS_SYSTEM = 'ipf-gl'

const storageKey = 'liftingcast:points-system'

export function loadPointsSystem(storage) {
  const stored = storage.getItem(storageKey)
  return POINTS_SYSTEMS.some((system) => system.id === stored)
    ? stored
    : DEFAULT_POINTS_SYSTEM
}

export function savePointsSystem(storage, pointsSystem) {
  if (!POINTS_SYSTEMS.some((system) => system.id === pointsSystem)) {
    throw new TypeError('Unsupported points system.')
  }
  storage.setItem(storageKey, pointsSystem)
  return pointsSystem
}
