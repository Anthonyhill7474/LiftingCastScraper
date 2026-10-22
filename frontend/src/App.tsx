import { useEffect, useRef, useState } from 'react'
import {
  filterFollowedLifters,
  getMissingFollowedLifterIds,
  loadFollowedLifterIds,
  saveFollowedLifterIds,
  toggleFollowedLifter,
} from './followedLifters.js'
import {
  describeAttempt,
  getAttemptForSlot,
  getAttemptStatus,
  getLiftType,
} from './attemptPresentation.js'
import {
  DEFAULT_POINTS_SYSTEM,
  loadPointsSystem,
  POINTS_SYSTEMS,
  savePointsSystem,
} from './pointsSystems.js'
import {
  getCompatibleBestOverallDivision,
  getEligibleBestOverallOpponents,
  getEligibleStrategyOpponents,
  resetScenarios,
  updateScenario,
} from './strategyUi.js'
import {
  filterOverallCategories,
  getOverallResult,
  getVisibleStandingsCategories,
  getWeightClassResult,
  sortOverallStandings,
  sortWeightClassStandings,
} from './standingsUi.js'

interface Competition {
  id: string
  name: string
  date?: string
  dateFormat?: string
  createDate?: string
  showOnHomePage: boolean
  parsedDate?: string | null
}

type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }

interface CompetitionDetails {
  competitionId: string
  name: string
  date?: string | null
  dateFormat?: string | null
  units?: string | null
  lastSyncedAtUtc?: string | null
  platforms: Platform[]
  lifters: Lifter[]
}

interface WeightClassStanding {
  athleteId: string
  athleteName?: string | null
  bodyWeightKg?: number | null
  bestSquatKg?: number | null
  bestBenchKg?: number | null
  bestDeadliftKg?: number | null
  successfulLiftSumKg?: number | null
  completeThreeLiftTotalKg?: number | null
  confirmedResultKg?: number | null
  projectedResultKg?: number | null
  hasPendingAttempts: boolean
  confirmedRank?: number | null
  confirmedStatus: string
  projectedRank?: number | null
  projectedStatus: string
}

interface WeightClassLeaderboard {
  divisionId: string
  divisionName?: string | null
  weightClassId: string
  weightClassName?: string | null
  gender: string
  equipmentCategory: string
  eventType: string
  scoreBy?: string | null
  rankingBasis: string
  rankingLimitation?: string | null
  standings: WeightClassStanding[]
}

interface OverallStanding {
  athleteId: string
  athleteName?: string | null
  bodyWeightKg?: number | null
  divisionId: string
  divisionName?: string | null
  weightClassId: string
  weightClassName?: string | null
  scoreBy?: string | null
  hasPendingAttempts: boolean
  pointsScores: Record<string, DivisionPointsScore>
  rankings: Record<string, {
    confirmedRank: number | null
    confirmedStatus: string
    projectedRank: number | null
    projectedStatus: string
  }>
}

interface OverallLeaderboard {
  gender: string
  equipmentCategory: string
  eventType: string
  standings: OverallStanding[]
}

interface CompetitionStandings {
  competitionId: string
  name: string
  date?: string | null
  dateFormat?: string | null
  lastSyncedAtUtc?: string | null
  unresolvedMembershipCount: number
  unsupportedOverallMembershipCount: number
  weightClasses: WeightClassLeaderboard[]
  overallCategories: OverallLeaderboard[]
}

interface LifterPerformance {
  bestSquatKg: number | null
  bestBenchKg: number | null
  bestDeadliftKg: number | null
  confirmedSuccessfulLiftSumKg: number | null
  completeThreeLiftTotalKg: number | null
  eligibleCompleteTotalKg: number | null
  benchOnlyResultKg: number | null
  baselineProjectedLiftSumKg: number | null
  baselineProjectedThreeLiftTotalKg: number | null
  baselineProjectedEligibleTotalKg: number | null
}

interface CompetitionStrategyStanding {
  lifterId: string
  name?: string | null
  bodyWeight?: JsonValue
  weightClassId?: string | null
  weightClassName?: string | null
  currentPerformance: LifterPerformance
  scenarioPerformance: LifterPerformance
  currentConfirmedPoints?: number | null
  projectedPoints?: number | null
  provisionalPlace?: number | null
  isTied: boolean
}

interface CompetitionStrategyResult {
  divisionId: string
  weightClassId?: string | null
  mode: 'weight-class' | 'best-overall'
  pointsSystem: string
  standings: CompetitionStrategyStanding[]
}

interface DivisionPointsScore {
  confirmedScore: number | null
  projectedScore: number | null
}

interface SyncSummary {
  competitionId: string
  initialSync: boolean
  inserted: number
  updated: number
  unchanged: number
  deleted: number
  batches: number
  lastSequence: string
  hasMore: boolean
  syncedAtUtc: string
  fullRefresh: boolean
}

interface Platform {
  documentId: string
  name?: string | null
  clockState?: string | null
  clockTimerLength?: JsonValue
  currentAttemptId?: string | null
}

interface PlatformDashboard {
  competitionId: string
  lastSyncedAtUtc: string
  platforms: DashboardPlatform[]
}

interface DashboardPlatform {
  platformId: string
  name?: string | null
  clockState?: JsonValue
  clockTimerLength?: JsonValue
  currentAttemptId?: string | null
  currentAttempt?: PlatformCurrentAttempt | null
}

interface PlatformCurrentAttempt {
  attemptId: string
  liftName?: string | null
  attemptNumber?: JsonValue
  weight?: JsonValue
  lifter?: PlatformLifter | null
}

interface PlatformLifter {
  lifterId: string
  name?: string | null
  flight?: string | null
  session?: JsonValue
  lot?: JsonValue
  bodyWeight?: JsonValue
}

interface Division {
  divisionId?: string | null
  name?: string | null
  weightClassId?: string | null
  weightClassName?: string | null
  weightClassMaxWeight?: number | null
  gender?: string | null
  rawOrEquipped?: string | null
  scoreBy?: string | null
  pointsScores?: Record<string, DivisionPointsScore> | null
  weightClassResolved?: boolean
  lifts: {
    squat?: boolean | null
    bench?: boolean | null
    dead?: boolean | null
  }
}

interface Attempt {
  documentId: string
  liftName?: string | null
  attemptNumber?: JsonValue
  weight?: JsonValue
  result?: JsonValue
}

interface Lifter {
  documentId: string
  name?: string | null
  divisions: Division[]
  flight?: string | null
  session?: JsonValue
  lot?: JsonValue
  bodyWeight?: JsonValue
  platformId?: string | null
  performance: LifterPerformance
  attempts: Attempt[]
}

function getDateValue(date?: string | null): number | null {
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return null

  const value = Date.parse(`${date}T00:00:00.000Z`)
  if (Number.isNaN(value) || new Date(value).toISOString().slice(0, 10) !== date) return null
  return value
}

function formatDate(value?: string | null): string {
  if (!value) return 'Date unavailable'
  const parsedDate = new Date(`${value}T00:00:00.000Z`)
  if (Number.isNaN(parsedDate.getTime())) return value
  return parsedDate.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    timeZone: 'UTC',
  })
}

function formatValue(value?: JsonValue): string {
  if (value === undefined || value === null || value === '') return '—'
  return typeof value === 'object' ? JSON.stringify(value) : String(value)
}

function getLifterGroups(lifters: Lifter[]) {
  const groups = new Map<string, { label: string; lifters: Lifter[] }>()
  for (const lifter of lifters) {
    const flight = lifter.flight?.trim()
    const session = formatValue(lifter.session)
    const label = flight
      ? `Flight ${flight}${session !== '—' ? ` · Session ${session}` : ''}`
      : session !== '—'
        ? `Session ${session}`
        : 'Unassigned'
    const group = groups.get(label) ?? { label, lifters: [] }
    group.lifters.push(lifter)
    groups.set(label, group)
  }
  return [...groups.values()]
}

function formatSyncedAt(value?: string | null): string {
  if (!value) return 'Not available'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

function formatKg(value?: number | null): string {
  return value === undefined || value === null || !Number.isFinite(value)
    ? '—'
    : `${value.toFixed(2)} kg`
}

function formatPoints(value?: number | null): string {
  return value === undefined || value === null || !Number.isFinite(value)
    ? 'Unavailable'
    : value.toFixed(2)
}

function getWeightClassCategoryKey(category: WeightClassLeaderboard): string {
  return [
    category.divisionId,
    category.weightClassId,
    category.gender,
    category.equipmentCategory,
    category.eventType,
    category.scoreBy ?? '',
  ].join('|')
}

function getOverallCategoryKey(category: OverallLeaderboard): string {
  return [category.gender, category.equipmentCategory, category.eventType].join('|')
}

function formatEventType(eventType: string): string {
  switch (eventType) {
    case 'full-power':
      return 'Full power'
    case 'bench-only':
      return 'Bench only'
    default:
      return eventType.split('-').map((part) => part[0]?.toUpperCase() + part.slice(1)).join(' + ')
  }
}

function formatStandingStatus(status: string): string {
  switch (status) {
    case 'tied':
      return 'Tied · tie-break unresolved'
    case 'incomplete':
      return 'Incomplete result'
    case 'unrankable':
      return 'Ranking unavailable'
    case 'no-projection':
      return 'No pending-attempt projection'
    case 'projection-incomplete':
      return 'Projection incomplete'
    default:
      return 'Unavailable'
  }
}

function canEditScenarioAttempt(attempt: Attempt, units?: string | null): boolean {
  const result = typeof attempt.result === 'string' ? attempt.result.trim().toLowerCase() : ''
  const weight = typeof attempt.weight === 'string' || typeof attempt.weight === 'number'
    ? Number(attempt.weight)
    : Number.NaN
  return result !== 'good'
    && result !== 'bad'
    && getLiftType(attempt.liftName) !== null
    && Number.isFinite(weight)
    && weight > 0
    && ['kg', 'kgs', 'kilogram', 'kilograms', 'lb', 'lbs', 'pound', 'pounds']
      .includes(units?.trim().toLowerCase() ?? '')
}

function getScenarioKey(lifterId: string, attemptId: string): string {
  return `${lifterId}:${attemptId}`
}

function getStrategyDivision(
  lifter: Lifter,
  selectedDivision: Division | undefined,
  mode: 'weight-class' | 'best-overall',
): Division | undefined {
  if (!selectedDivision?.divisionId || !selectedDivision.weightClassId) return undefined
  if (mode === 'best-overall') {
    return getCompatibleBestOverallDivision(lifter, selectedDivision) ?? undefined
  }
  return lifter.divisions.find((division) =>
    division.divisionId === selectedDivision.divisionId
      && division.weightClassId === selectedDivision.weightClassId,
  )
}

export default function App() {
  const [competitions, setCompetitions] = useState<Competition[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedCompetition, setSelectedCompetition] = useState<Competition | null>(null)
  const [details, setDetails] = useState<CompetitionDetails | null>(null)
  const [standings, setStandings] = useState<CompetitionStandings | null>(null)
  const [standingsLoading, setStandingsLoading] = useState(false)
  const [standingsError, setStandingsError] = useState<string | null>(null)
  const [standingsView, setStandingsView] = useState<'my-classes' | 'all-classes' | 'overall'>('my-classes')
  const [standingsResultMode, setStandingsResultMode] = useState<'confirmed' | 'projected'>('confirmed')
  const [standingsCategoryKey, setStandingsCategoryKey] = useState('')
  const [standingsGenderFilter, setStandingsGenderFilter] = useState('all')
  const [standingsDivisionFilter, setStandingsDivisionFilter] = useState('all')
  const [standingsWeightClassFilter, setStandingsWeightClassFilter] = useState('all')
  const [overallGender, setOverallGender] = useState<'male' | 'female'>('male')
  const [overallEquipmentFilter, setOverallEquipmentFilter] = useState('all')
  const [overallEventFilter, setOverallEventFilter] = useState('all')
  const [overallCategoryKey, setOverallCategoryKey] = useState('')
  const focusLifterId = useRef('')
  const [platformDashboard, setPlatformDashboard] = useState<PlatformDashboard | null>(null)
  const [detailsLoading, setDetailsLoading] = useState(false)
  const [platformDashboardLoading, setPlatformDashboardLoading] = useState(false)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [platformDashboardError, setPlatformDashboardError] = useState<string | null>(null)
  const [followedSelection, setFollowedSelection] = useState<{
    competitionId: string
    lifterIds: string[]
  }>({ competitionId: '', lifterIds: [] })
  const [lifterView, setLifterView] = useState<'all' | 'followed'>('all')
  const [followStorageError, setFollowStorageError] = useState<string | null>(null)
  const [initialPointsPreference] = useState(() => {
    try {
      return { system: loadPointsSystem(window.localStorage), error: null }
    } catch {
      return {
        system: DEFAULT_POINTS_SYSTEM,
        error: 'The points-system preference could not be read from this browser.',
      }
    }
  })
  const [pointsSystem, setPointsSystem] = useState(initialPointsPreference.system)
  const [pointsSystemError, setPointsSystemError] = useState<string | null>(
    initialPointsPreference.error,
  )
  const [strategyMode, setStrategyMode] = useState<'weight-class' | 'best-overall'>('weight-class')
  const [selectedStrategistId, setSelectedStrategistId] = useState('')
  const [selectedStrategyDivisionKey, setSelectedStrategyDivisionKey] = useState('')
  const [selectedOpponentId, setSelectedOpponentId] = useState('')
  const [strategyOpponentIds, setStrategyOpponentIds] = useState<string[]>([])
  const [strategyScenarios, setStrategyScenarios] = useState<
    Record<string, { weight: string; result: 'good' | 'bad' }>
  >({})
  const [strategyResultState, setStrategyResultState] = useState<{
    requestKey: string
    result: CompetitionStrategyResult
  } | null>(null)
  const [strategyLoading, setStrategyLoading] = useState(false)
  const [strategyError, setStrategyError] = useState<string | null>(null)
  const [notSynchronized, setNotSynchronized] = useState(false)
  const [backendDevelopment, setBackendDevelopment] = useState(false)
  const [refreshing, setRefreshing] = useState<'changes' | 'full' | null>(null)
  const [refreshError, setRefreshError] = useState<string | null>(null)
  const [refreshSummary, setRefreshSummary] = useState<SyncSummary | null>(null)
  const [detailsReloadKey, setDetailsReloadKey] = useState(0)
  const followedLifterIds = selectedCompetition
    && followedSelection.competitionId === selectedCompetition.id
    ? followedSelection.lifterIds
    : []
  const followedLifterIdSet = new Set(followedLifterIds)
  const availableLifterIds = new Set(details?.lifters.map((lifter) => lifter.documentId) ?? [])
  const missingFollowedLifterIds = details
    ? getMissingFollowedLifterIds(followedLifterIds, details.lifters)
    : []
  const liftersToDisplay = details
    ? lifterView === 'all'
      ? details.lifters
      : filterFollowedLifters(details.lifters, followedLifterIds)
    : []
  const followedInCompetition = details?.lifters.filter(
    (lifter) => followedLifterIdSet.has(lifter.documentId),
  ) ?? []
  const activeStrategistId = followedInCompetition.some(
    (lifter) => lifter.documentId === selectedStrategistId,
  )
    ? selectedStrategistId
    : followedInCompetition[0]?.documentId ?? ''
  const strategyLifter = details?.lifters.find((lifter) => lifter.documentId === activeStrategistId)
  const strategyDivisions = strategyLifter?.divisions.filter((division) =>
    division.divisionId
      && division.weightClassId
      && division.gender
      && division.rawOrEquipped
      && division.lifts?.squat !== null
      && division.lifts?.squat !== undefined
      && division.lifts?.bench !== null
      && division.lifts?.bench !== undefined
      && division.lifts?.dead !== null
      && division.lifts?.dead !== undefined,
  ) ?? []
  const selectedStrategyDivision = strategyDivisions.find(
    (division) => `${division.divisionId}|${division.weightClassId}` === selectedStrategyDivisionKey,
  ) ?? strategyDivisions[0]
  const activeStrategyDivisionKey = selectedStrategyDivision
    ? `${selectedStrategyDivision.divisionId}|${selectedStrategyDivision.weightClassId}`
    : ''
  const availableStrategyOpponents = details
    ? strategyMode === 'best-overall'
      ? getEligibleBestOverallOpponents(
          details.lifters,
          activeStrategistId,
          selectedStrategyDivision,
        )
      : getEligibleStrategyOpponents(
          details.lifters,
          activeStrategistId,
          selectedStrategyDivision,
        )
    : []
  const activeStrategyOpponentIds = strategyOpponentIds.filter((id) =>
    id !== activeStrategistId
      && availableStrategyOpponents.some((lifter) => lifter.documentId === id),
  )
  const activeSelectedOpponentId = availableStrategyOpponents.some(
    (lifter) => lifter.documentId === selectedOpponentId
      && !activeStrategyOpponentIds.includes(lifter.documentId),
  )
    ? selectedOpponentId
    : ''
  const strategyLifters = details?.lifters.filter((lifter) =>
    lifter.documentId === activeStrategistId
      || activeStrategyOpponentIds.includes(lifter.documentId),
  ) ?? []
  const eligibleStrategyOpponents = availableStrategyOpponents.filter(
    (lifter) => !activeStrategyOpponentIds.includes(lifter.documentId),
  )
  const strategyRequestPayload = selectedCompetition
    && details
    && selectedStrategyDivision
    && activeStrategistId
    ? {
        mode: strategyMode,
        pointsSystem,
        divisionId: selectedStrategyDivision.divisionId,
        weightClassId: selectedStrategyDivision.weightClassId,
        lifters: [activeStrategistId, ...activeStrategyOpponentIds].map((lifterId) => {
          const lifter = details.lifters.find((item) => item.documentId === lifterId)
          const division = lifter
            ? getStrategyDivision(lifter, selectedStrategyDivision, strategyMode)
            : undefined
          const attempts = lifter?.attempts.flatMap((attempt) => {
            const scenario = strategyScenarios[getScenarioKey(lifterId, attempt.documentId)]
            const weight = Number(scenario?.weight)
            return scenario
              && canEditScenarioAttempt(attempt, details.units)
              && Number.isFinite(weight)
              && weight > 0
              ? [{ attemptId: attempt.documentId, weight, result: scenario.result }]
              : []
          }) ?? []
          return {
            lifterId,
            divisionId: division?.divisionId,
            weightClassId: division?.weightClassId,
            attempts,
          }
        }),
      }
    : null
  const strategyRequestKey = strategyRequestPayload
    ? `${selectedCompetition?.id}:${details?.lastSyncedAtUtc}:${JSON.stringify(strategyRequestPayload)}`
    : ''
  const strategyRequestBody = strategyRequestPayload
    ? JSON.stringify(strategyRequestPayload)
    : ''
  const strategyResult = strategyResultState?.requestKey === strategyRequestKey
    ? strategyResultState.result
    : null
  const today = new Date()
  const todayValue = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate())
  const sortedCompetitions = [...competitions].sort((left, right) => {
    const leftDate = getDateValue(left.parsedDate)
    const rightDate = getDateValue(right.parsedDate)

    if (leftDate === null) return rightDate === null ? 0 : 1
    if (rightDate === null) return -1

    const leftIsUpcoming = leftDate >= todayValue
    const rightIsUpcoming = rightDate >= todayValue
    if (leftIsUpcoming !== rightIsUpcoming) return leftIsUpcoming ? -1 : 1
    return leftIsUpcoming ? leftDate - rightDate : rightDate - leftDate
  })
  const visibleWeightClasses = standings
    ? getVisibleStandingsCategories(
        standings,
        standingsView === 'my-classes' ? 'my-classes' : 'all-classes',
        followedLifterIds,
      ) as WeightClassLeaderboard[]
    : []
  const filteredWeightClasses = visibleWeightClasses.filter((category) =>
    (standingsGenderFilter === 'all' || category.gender === standingsGenderFilter)
      && (standingsDivisionFilter === 'all' || category.divisionId === standingsDivisionFilter)
      && (standingsWeightClassFilter === 'all' || category.weightClassId === standingsWeightClassFilter),
  )
  const activeWeightClass = filteredWeightClasses.find(
    (category) => getWeightClassCategoryKey(category) === standingsCategoryKey,
  ) ?? filteredWeightClasses[0]
  const visibleOverallCategories = standings
    ? filterOverallCategories(
        standings.overallCategories,
        overallGender,
        overallEquipmentFilter === 'all' ? '' : overallEquipmentFilter,
        overallEventFilter === 'all' ? '' : overallEventFilter,
      )
    : []
  const activeOverallCategory = visibleOverallCategories.find(
    (category) => getOverallCategoryKey(category) === overallCategoryKey,
  ) ?? visibleOverallCategories[0]

  const persistFollowedLifterIds = (lifterIds: string[]) => {
    if (!selectedCompetition) return

    const uniqueIds = [...new Set(lifterIds)]
    try {
      const savedIds = saveFollowedLifterIds(
        window.localStorage,
        selectedCompetition.id,
        uniqueIds,
      )
      setFollowedSelection({ competitionId: selectedCompetition.id, lifterIds: savedIds })
      setFollowStorageError(null)
    } catch {
      setFollowedSelection({ competitionId: selectedCompetition.id, lifterIds: uniqueIds })
      setFollowStorageError('Followed lifters could not be saved in this browser.')
    }
  }

  const toggleFollowedLifterId = (lifterId: string) => {
    persistFollowedLifterIds(
      toggleFollowedLifter(followedLifterIds, lifterId),
    )
  }

  useEffect(() => {
    const fetchCompetitions = async () => {
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7251'
        const response = await fetch(`${apiBase}/api/competitions`)
        if (!response.ok) throw new Error(`API error: ${response.status}`)
        setCompetitions(await response.json())
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to fetch competitions')
      } finally {
        setLoading(false)
      }
    }

    fetchCompetitions()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const checkDevelopmentMode = async () => {
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7251'
        const response = await fetch(`${apiBase}/api/development/status`, {
          signal: controller.signal,
        })
        if (!response.ok) return
        const status = await response.json()
        setBackendDevelopment(
          status.environment === 'Development' && status.isDevelopment === true,
        )
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return
        setBackendDevelopment(false)
      }
    }

    checkDevelopmentMode()
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!selectedCompetition) {
      return
    }

    const controller = new AbortController()
    const fetchDetails = async () => {
      setDetailsError(null)
      setNotSynchronized(false)
      setDetailsLoading(true)
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7251'
        const response = await fetch(
          `${apiBase}/api/competitions/${encodeURIComponent(selectedCompetition.id)}/details`,
          { signal: controller.signal },
        )
        if (response.status === 409) {
          setNotSynchronized(true)
          return
        }
        if (!response.ok) throw new Error(`API error: ${response.status}`)
        setDetails(await response.json())
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return
        setDetailsError(err instanceof Error ? err.message : 'Failed to fetch competition details')
      } finally {
        if (!controller.signal.aborted) setDetailsLoading(false)
      }
    }

    fetchDetails()
    return () => controller.abort()
  }, [selectedCompetition, detailsReloadKey])

  useEffect(() => {
    if (!selectedCompetition) {
      return
    }

    const controller = new AbortController()
    const fetchPlatformDashboard = async () => {
      setPlatformDashboard(null)
      setPlatformDashboardError(null)
      setPlatformDashboardLoading(true)
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7251'
        const response = await fetch(
          `${apiBase}/api/competitions/${encodeURIComponent(selectedCompetition.id)}/platforms`,
          { signal: controller.signal },
        )
        if (response.status === 409) return
        if (!response.ok) throw new Error(`API error: ${response.status}`)
        setPlatformDashboard(await response.json())
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return
        setPlatformDashboardError(
          err instanceof Error ? err.message : 'Failed to fetch platform state',
        )
      } finally {
        if (!controller.signal.aborted) setPlatformDashboardLoading(false)
      }
    }

    fetchPlatformDashboard()
    return () => controller.abort()
  }, [selectedCompetition, detailsReloadKey])

  useEffect(() => {
    if (!selectedCompetition) return

    const controller = new AbortController()
    const fetchStandings = async () => {
      setStandings(null)
      setStandingsError(null)
      setStandingsLoading(true)
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7251'
        const response = await fetch(
          `${apiBase}/api/competitions/${encodeURIComponent(selectedCompetition.id)}/standings`,
          { signal: controller.signal },
        )
        if (response.status === 409) return
        if (!response.ok) throw new Error(`API error: ${response.status}`)
        setStandings(await response.json())
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return
        setStandingsError(err instanceof Error ? err.message : 'Failed to fetch competition standings')
      } finally {
        if (!controller.signal.aborted) setStandingsLoading(false)
      }
    }

    fetchStandings()
    return () => controller.abort()
  }, [selectedCompetition, detailsReloadKey])

  useEffect(() => {
    if (!selectedCompetition || !strategyRequestKey || !strategyRequestBody) return

    const controller = new AbortController()
    const calculateStrategy = async () => {
      setStrategyLoading(true)
      setStrategyError(null)
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7251'
        const response = await fetch(
          `${apiBase}/api/competitions/${encodeURIComponent(selectedCompetition.id)}/strategy`,
          {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: strategyRequestBody,
            signal: controller.signal,
          },
        )
        const payload = await response.json()
        if (!response.ok) {
          throw new Error(payload.error || payload.detail || `Strategy calculation failed: HTTP ${response.status}`)
        }
        setStrategyResultState({ requestKey: strategyRequestKey, result: payload })
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return
        setStrategyError(err instanceof Error ? err.message : 'Strategy calculation failed')
      } finally {
        if (!controller.signal.aborted) setStrategyLoading(false)
      }
    }

    calculateStrategy()
    return () => controller.abort()
  }, [
    selectedCompetition,
    strategyRequestKey,
    strategyRequestBody,
  ])

  useEffect(() => {
    const athleteId = focusLifterId.current
    if (!athleteId || lifterView !== 'all') return
    const target = document.getElementById(`lifter-${encodeURIComponent(athleteId)}`)
    if (!target) return
    target.scrollIntoView({ behavior: 'smooth', block: 'start' })
    focusLifterId.current = ''
  }, [lifterView, details])

  const changePointsSystem = (nextSystem: string) => {
    try {
      setPointsSystem(savePointsSystem(window.localStorage, nextSystem))
      setPointsSystemError(null)
    } catch {
      setPointsSystemError('The points-system preference could not be saved in this browser.')
    }
  }

  const changeScenario = (
    lifterId: string,
    attempt: Attempt,
    nextResult: '' | 'good' | 'bad',
    nextWeight?: string,
  ) => {
    const key = getScenarioKey(lifterId, attempt.documentId)
    setStrategyScenarios((current) => {
      if (nextResult === '') {
        return updateScenario(current, key, null)
      }
      return updateScenario(current, key, {
        weight: nextWeight ?? current[key]?.weight ?? formatValue(attempt.weight),
        result: nextResult,
      })
    })
  }

  const refreshCompetition = async (fullRefresh: boolean) => {
    if (!selectedCompetition || refreshing) return
    if (fullRefresh && !window.confirm(
      'Full refresh replaces all locally cached documents for this competition with a fresh CouchDB snapshot. Continue?',
    )) {
      return
    }

    setRefreshing(fullRefresh ? 'full' : 'changes')
    setRefreshError(null)
    setRefreshSummary(null)
    try {
      const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7251'
      const url = `${apiBase}/api/development/competitions/${encodeURIComponent(selectedCompetition.id)}/sync${fullRefresh ? '?full=true' : ''}`
      const response = await fetch(url, { method: 'POST' })
      const summary = await response.json()
      if (!response.ok) {
        throw new Error(summary.detail || summary.error || `Synchronization failed: HTTP ${response.status}`)
      }

      setRefreshSummary(summary)
      setNotSynchronized(false)
      setDetailsReloadKey((key) => key + 1)
    } catch (err) {
      setRefreshError(err instanceof Error ? err.message : 'Synchronization failed')
    } finally {
      setRefreshing(null)
    }
  }

  const getStrategyPreset = (
    athleteId: string,
    divisionId: string,
    weightClassId: string,
  ) => {
    if (!details) return null
    const targetLifter = details.lifters.find((lifter) => lifter.documentId === athleteId)
    const targetDivision = targetLifter?.divisions.find((division) =>
      division.divisionId === divisionId && division.weightClassId === weightClassId,
    )
    if (!targetLifter || !targetDivision) return null

    if (followedLifterIdSet.has(targetLifter.documentId)) {
      const opponent = getEligibleBestOverallOpponents(
        details.lifters,
        targetLifter.documentId,
        targetDivision,
      )[0]
      return opponent
        ? { strategist: targetLifter, division: targetDivision, opponentId: opponent.documentId }
        : null
    }

    for (const strategist of followedInCompetition) {
      const compatibleDivision = getCompatibleBestOverallDivision(strategist, targetDivision)
      if (compatibleDivision) {
        return {
          strategist,
          division: compatibleDivision,
          opponentId: targetLifter.documentId,
        }
      }
    }

    return null
  }

  const compareInStrategy = (
    athleteId: string,
    divisionId: string,
    weightClassId: string,
  ) => {
    const preset = getStrategyPreset(athleteId, divisionId, weightClassId)
    if (!preset) return
    setSelectedStrategistId(preset.strategist.documentId)
    setSelectedStrategyDivisionKey(
      `${preset.division.divisionId}|${preset.division.weightClassId}`,
    )
    setStrategyMode('best-overall')
    setSelectedOpponentId('')
    setStrategyOpponentIds([preset.opponentId])
    setStrategyScenarios(resetScenarios())
    document.getElementById('competition-strategy')?.scrollIntoView({
      behavior: 'smooth',
      block: 'start',
    })
  }

  const showLifterDetails = (athleteId: string) => {
    focusLifterId.current = athleteId
    setLifterView('all')
    if (lifterView === 'all' && athleteId) {
      document.getElementById(`lifter-${encodeURIComponent(athleteId)}`)?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      })
      focusLifterId.current = ''
    }
    document.getElementById('competition-lifters')?.scrollIntoView({
      behavior: 'smooth',
      block: 'start',
    })
  }

  return (
    <div className="app">
      <header className="header">
        <h1>LiftingCast Tracker</h1>
        <p>Discover and track powerlifting competitions</p>
      </header>

      <main className="main">
        {selectedCompetition ? (
          <section className="competition-details">
            <button
              className="back-button"
              disabled={refreshing !== null}
              onClick={() => {
                setDetails(null)
                setSelectedCompetition(null)
                setPlatformDashboard(null)
                setFollowedSelection({ competitionId: '', lifterIds: [] })
                setFollowStorageError(null)
                setLifterView('all')
                setRefreshSummary(null)
                setRefreshError(null)
                setSelectedStrategistId('')
                setSelectedStrategyDivisionKey('')
                setSelectedOpponentId('')
                setStrategyOpponentIds([])
                setStrategyScenarios({})
              }}
            >
              ← All competitions
            </button>
            <div className="details-heading">
              <div>
                <h2>{selectedCompetition.name || 'Unnamed competition'}</h2>
                <p className="date">{formatDate(selectedCompetition.parsedDate || selectedCompetition.date)}</p>
              </div>
              {details?.lastSyncedAtUtc && (
                <p className="sync-time">Last synchronized: {formatSyncedAt(details.lastSyncedAtUtc)}</p>
              )}
            </div>

            {backendDevelopment && (
              <div className="refresh-panel">
                <div className="refresh-actions">
                  <button
                    className="refresh-button"
                    disabled={refreshing !== null}
                    onClick={() => refreshCompetition(false)}
                  >
                    {refreshing === 'changes' ? 'Refreshing changes…' : 'Refresh changes'}
                  </button>
                  <button
                    className="refresh-button secondary"
                    disabled={refreshing !== null}
                    onClick={() => refreshCompetition(true)}
                  >
                    {refreshing === 'full' ? 'Full refresh in progress…' : 'Full refresh'}
                  </button>
                </div>
                <p className="refresh-help">
                  Full refresh replaces locally cached competition documents with a fresh snapshot.
                </p>
              </div>
            )}
            {refreshing && <div className="state">Synchronizing competition data...</div>}
            {refreshError && <div className="state error">Synchronization failed: {refreshError}</div>}
            {refreshSummary && (
              <div className="sync-summary" role="status">
                <strong>
                  {refreshSummary.fullRefresh
                    ? 'Full refresh completed'
                    : refreshSummary.initialSync
                      ? 'Initial synchronization completed'
                      : 'Changes refresh completed'}
                </strong>
                <span>
                  Added {refreshSummary.inserted}, updated {refreshSummary.updated}, unchanged {refreshSummary.unchanged}, removed {refreshSummary.deleted}
                  {' · '}batches {refreshSummary.batches}
                </span>
                {refreshSummary.hasMore && (
                  <span>More changes remain. Select Refresh changes again to continue.</span>
                )}
              </div>
            )}

            {details && (
              <nav className="competition-view-nav" aria-label="Competition views">
                <a href="#platform-dashboard">Platforms</a>
                <a href="#competition-standings">Standings</a>
                <a href="#competition-strategy">Strategy</a>
                <a href="#competition-lifters">Lifters</a>
              </nav>
            )}

            {detailsLoading && <div className="state">Loading competition details...</div>}
            {detailsError && <div className="state error">Error: {detailsError}</div>}
            {notSynchronized && (
              <div className="state empty">
                <strong>No synchronized data yet.</strong>
                <span>Run a manual Development synchronization for this competition, then reload this page.</span>
              </div>
            )}
            <section
              className="platform-dashboard"
              id="platform-dashboard"
              aria-labelledby="platform-dashboard-heading"
            >
              <div className="platform-dashboard-heading">
                <div>
                  <h3 id="platform-dashboard-heading">Platform dashboard</h3>
                  <p>Latest synchronized platform state; clock values are shown as supplied.</p>
                </div>
                {platformDashboard && (
                  <p className="sync-time">
                    Platform data last synchronized: {formatSyncedAt(platformDashboard.lastSyncedAtUtc)}
                  </p>
                )}
              </div>
              <div className="platform-next-up" role="status">
                <strong>Next Up</strong>
                <span>Order unavailable — synchronized fields do not establish a verified queue. No wait estimate is shown.</span>
              </div>
              {platformDashboardLoading && <div className="state">Loading platform state...</div>}
              {platformDashboardError && (
                <div className="state error">Platform dashboard error: {platformDashboardError}</div>
              )}
              {platformDashboard && platformDashboard.platforms.length === 0 && (
                <div className="state empty">No platform runtime documents were found in the synchronized data.</div>
              )}
              {platformDashboard && platformDashboard.platforms.length > 0 && (
                <div className="platform-grid">
                  {platformDashboard.platforms.map((platform) => (
                    <article className="platform-card" key={platform.platformId}>
                      <div className="platform-card-heading">
                        <h4>{platform.name || 'Unnamed platform'}</h4>
                        <span className="platform-id">ID: {platform.platformId}</span>
                      </div>
                      <div className="platform-runtime-values">
                        <span>
                          <strong>Clock state</strong>
                          <span>{formatValue(platform.clockState)}</span>
                        </span>
                        <span>
                          <strong>Clock timer length (raw)</strong>
                          <span>{formatValue(platform.clockTimerLength)}</span>
                        </span>
                      </div>
                      <div className="current-attempt">
                        <h5>Current attempt</h5>
                        {platform.currentAttempt ? (
                          <>
                            <strong>
                              {platform.currentAttempt.liftName || 'Lift unknown'}
                              {platform.currentAttempt.attemptNumber !== undefined
                                && platform.currentAttempt.attemptNumber !== null
                                ? ` · Attempt ${formatValue(platform.currentAttempt.attemptNumber)}`
                                : ''}
                            </strong>
                            <span>Weight (source): {formatValue(platform.currentAttempt.weight)}</span>
                            <span>
                              Lifter: {platform.currentAttempt.lifter?.name || 'Unavailable in synchronized records'}
                            </span>
                            {platform.currentAttempt.lifter?.flight && (
                              <span>Flight {platform.currentAttempt.lifter.flight}</span>
                            )}
                          </>
                        ) : platform.currentAttemptId ? (
                          <span>
                            Attempt reference {platform.currentAttemptId} is not present as a stored attempt.
                          </span>
                        ) : (
                          <span>No current attempt is reported (idle or unknown).</span>
                        )}
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </section>
            {details && (
              <section
                className="standings-dashboard"
                id="competition-standings"
                aria-labelledby="competition-standings-heading"
              >
                <div className="standings-heading">
                  <div>
                    <h3 id="competition-standings-heading">Competition standings</h3>
                    <p>
                      Calculated coaching comparisons, not federation-certified placings.
                      Unverified tie-break rules remain unresolved.
                    </p>
                  </div>
                  <p className="sync-time">
                    Last synchronized: {formatSyncedAt(standings?.lastSyncedAtUtc)}
                  </p>
                </div>

                <div className="standings-view-switch" role="group" aria-label="Standings view">
                  <button
                    type="button"
                    aria-pressed={standingsView === 'my-classes'}
                    onClick={() => {
                      setStandingsView('my-classes')
                      setStandingsGenderFilter('all')
                      setStandingsDivisionFilter('all')
                      setStandingsWeightClassFilter('all')
                    }}
                  >
                    My Classes
                  </button>
                  <button
                    type="button"
                    aria-pressed={standingsView === 'all-classes'}
                    onClick={() => {
                      setStandingsView('all-classes')
                      setStandingsGenderFilter('all')
                      setStandingsDivisionFilter('all')
                      setStandingsWeightClassFilter('all')
                    }}
                  >
                    All Classes
                  </button>
                  <button
                    type="button"
                    aria-pressed={standingsView === 'overall'}
                    onClick={() => setStandingsView('overall')}
                  >
                    Overall
                  </button>
                </div>

                <div className="standings-result-switch" role="group" aria-label="Standings result type">
                  <button
                    type="button"
                    aria-pressed={standingsResultMode === 'confirmed'}
                    onClick={() => setStandingsResultMode('confirmed')}
                  >
                    Confirmed
                  </button>
                  <button
                    type="button"
                    aria-pressed={standingsResultMode === 'projected'}
                    onClick={() => setStandingsResultMode('projected')}
                  >
                    Projected
                  </button>
                </div>

                {standingsLoading && <div className="state">Loading standings...</div>}
                {standingsError && (
                  <div className="state error">Standings error: {standingsError}</div>
                )}
                {notSynchronized && (
                  <div className="state empty">Synchronize this competition to calculate standings.</div>
                )}
                {standings && (
                  <>
                    {standings.unresolvedMembershipCount > 0 && (
                      <p className="standings-note" role="status">
                        {standings.unresolvedMembershipCount} division membership
                        {standings.unresolvedMembershipCount === 1 ? '' : 's'} lack a usable division
                        or declared weight-class ID and are omitted from class groups.
                      </p>
                    )}
                    {standings.unsupportedOverallMembershipCount > 0 && (
                      <p className="standings-note" role="status">
                        {standings.unsupportedOverallMembershipCount} membership
                        {standings.unsupportedOverallMembershipCount === 1 ? '' : 's'} cannot be placed in
                        an overall category because gender, equipment, or event type is unresolved or unsupported.
                      </p>
                    )}

                    {standingsView !== 'overall' ? (
                      <>
                        {standingsView === 'all-classes' && (
                          <div className="standings-filters">
                            <label>
                              <span>Gender</span>
                              <select
                                value={standingsGenderFilter}
                                onChange={(event) => setStandingsGenderFilter(event.target.value)}
                              >
                                <option value="all">All genders</option>
                                {[...new Set(visibleWeightClasses.map((category) => category.gender))]
                                  .sort()
                                  .map((gender) => (
                                    <option key={gender} value={gender}>
                                      {gender.charAt(0).toUpperCase() + gender.slice(1)}
                                    </option>
                                  ))}
                              </select>
                            </label>
                            <label>
                              <span>Division</span>
                              <select
                                value={standingsDivisionFilter}
                                onChange={(event) => setStandingsDivisionFilter(event.target.value)}
                              >
                                <option value="all">All divisions</option>
                                {[...new Map(visibleWeightClasses.map((category) => [
                                  category.divisionId,
                                  category.divisionName || category.divisionId,
                                ])).entries()]
                                  .sort((left, right) => String(left[1]).localeCompare(String(right[1])))
                                  .map(([divisionId, label]) => (
                                    <option key={divisionId} value={divisionId}>{label}</option>
                                  ))}
                              </select>
                            </label>
                            <label>
                              <span>Weight class</span>
                              <select
                                value={standingsWeightClassFilter}
                                onChange={(event) => setStandingsWeightClassFilter(event.target.value)}
                              >
                                <option value="all">All weight classes</option>
                                {[...new Map(visibleWeightClasses
                                  .filter((category) => standingsDivisionFilter === 'all'
                                    || category.divisionId === standingsDivisionFilter)
                                  .map((category) => [
                                    category.weightClassId,
                                    category.weightClassName || category.weightClassId,
                                  ])).entries()]
                                  .sort((left, right) => String(left[1]).localeCompare(String(right[1])))
                                  .map(([weightClassId, label]) => (
                                    <option key={weightClassId} value={weightClassId}>{label}</option>
                                  ))}
                              </select>
                            </label>
                          </div>
                        )}
                        {standingsView === 'my-classes' && followedLifterIds.length === 0 ? (
                          <div className="state empty">
                            <strong>Follow lifters to see My Classes.</strong>
                            <button
                              type="button"
                              className="lifter-view-button"
                              onClick={() => showLifterDetails('')}
                            >
                              Browse lifters
                            </button>
                          </div>
                        ) : filteredWeightClasses.length === 0 ? (
                          <div className="state empty">
                            {standingsView === 'my-classes'
                              ? 'No followed lifters have a resolved division and weight-class membership.'
                              : 'No weight classes match these filters.'}
                          </div>
                        ) : (
                          <>
                            <label className="standings-category-select">
                              <span>{standingsView === 'my-classes' ? 'My weight classes' : 'Leaderboard'}</span>
                              <select
                                value={activeWeightClass
                                  ? getWeightClassCategoryKey(activeWeightClass)
                                  : ''}
                                onChange={(event) => setStandingsCategoryKey(event.target.value)}
                              >
                                {filteredWeightClasses.map((category) => (
                                  <option
                                    key={getWeightClassCategoryKey(category)}
                                    value={getWeightClassCategoryKey(category)}
                                  >
                                    {[
                                      category.divisionName || category.divisionId,
                                      category.weightClassName || category.weightClassId,
                                      category.gender,
                                      category.equipmentCategory,
                                      formatEventType(category.eventType),
                                    ].join(' · ')}
                                  </option>
                                ))}
                              </select>
                            </label>
                            {activeWeightClass && (
                              <>
                                <div className="standings-category-heading">
                                  <div>
                                    <h4>
                                      {activeWeightClass.divisionName || activeWeightClass.divisionId}
                                      {' · '}
                                      {activeWeightClass.weightClassName || activeWeightClass.weightClassId}
                                    </h4>
                                    <p>
                                      {activeWeightClass.gender} · {activeWeightClass.equipmentCategory}
                                      {' · '}{formatEventType(activeWeightClass.eventType)}
                                      {activeWeightClass.scoreBy ? ` · configured scoreBy ${activeWeightClass.scoreBy}` : ''}
                                    </p>
                                  </div>
                                  {standingsResultMode === 'projected' && (
                                    <span className="standings-hypothetical-label">
                                      Hypothetical baseline · selected unresolved weights assume success
                                    </span>
                                  )}
                                </div>
                                {activeWeightClass.rankingLimitation && (
                                  <p className="standings-note" role="status">
                                    {activeWeightClass.rankingLimitation} Results are shown without a rank.
                                  </p>
                                )}
                                {activeWeightClass.standings.length === 0 ? (
                                  <div className="state empty">No athletes are entered in this category.</div>
                                ) : (
                                  <div className="standings-table-wrap">
                                    <table className="standings-table">
                                      <thead>
                                        <tr>
                                          <th scope="col">Rank</th>
                                          <th scope="col">Athlete</th>
                                          <th scope="col">Bodyweight</th>
                                          <th scope="col">Squat</th>
                                          <th scope="col">Bench</th>
                                          <th scope="col">Deadlift</th>
                                          <th scope="col">Successful-lift sum</th>
                                          <th scope="col">Complete 3-lift total</th>
                                          <th scope="col">
                                            {activeWeightClass.eventType === 'bench-only'
                                              ? 'Bench result'
                                              : standingsResultMode === 'projected'
                                                ? 'Projected result'
                                                : 'Confirmed result'}
                                          </th>
                                          <th scope="col">Actions</th>
                                        </tr>
                                      </thead>
                                      <tbody>
                                        {sortWeightClassStandings(
                                          activeWeightClass.standings,
                                          standingsResultMode,
                                        ).map((standing) => {
                                          const result = getWeightClassResult(standing, standingsResultMode)
                                          const isFollowed = followedLifterIdSet.has(standing.athleteId)
                                          const strategyPreset = getStrategyPreset(
                                            standing.athleteId,
                                            activeWeightClass.divisionId,
                                            activeWeightClass.weightClassId,
                                          )
                                          return (
                                            <tr
                                              className={isFollowed ? 'is-followed' : ''}
                                              key={standing.athleteId}
                                            >
                                              <td>
                                                {result.rank ? `#${result.rank}` : formatStandingStatus(result.status)}
                                              </td>
                                              <th scope="row">
                                                {standing.athleteName || standing.athleteId}
                                                {isFollowed && <span className="followed-marker">Followed</span>}
                                              </th>
                                              <td>{formatKg(standing.bodyWeightKg)}</td>
                                              <td>{formatKg(standing.bestSquatKg)}</td>
                                              <td>{formatKg(standing.bestBenchKg)}</td>
                                              <td>{formatKg(standing.bestDeadliftKg)}</td>
                                              <td>{formatKg(standing.successfulLiftSumKg)}</td>
                                              <td>{formatKg(standing.completeThreeLiftTotalKg)}</td>
                                              <td>
                                                <strong>{formatKg(result.value)}</strong>
                                                {standingsResultMode === 'projected' && result.value !== null && (
                                                  <span className="standings-value-source">Hypothetical</span>
                                                )}
                                              </td>
                                              <td>
                                                <div className="standings-row-actions">
                                                  <button
                                                    type="button"
                                                    onClick={() => showLifterDetails(standing.athleteId)}
                                                  >
                                                    View details
                                                  </button>
                                                  {strategyPreset && (
                                                    <button
                                                      type="button"
                                                      onClick={() => compareInStrategy(
                                                        standing.athleteId,
                                                        activeWeightClass.divisionId,
                                                        activeWeightClass.weightClassId,
                                                      )}
                                                    >
                                                      Compare in Strategy
                                                    </button>
                                                  )}
                                                </div>
                                              </td>
                                            </tr>
                                          )
                                        })}
                                      </tbody>
                                    </table>
                                  </div>
                                )}
                              </>
                            )}
                          </>
                        )}
                      </>
                    ) : (
                      <>
                        <div className="standings-filters overall-filters">
                          <div className="overall-gender-switch" role="group" aria-label="Overall gender">
                            <button
                              type="button"
                              aria-pressed={overallGender === 'male'}
                              onClick={() => setOverallGender('male')}
                            >
                              Male
                            </button>
                            <button
                              type="button"
                              aria-pressed={overallGender === 'female'}
                              onClick={() => setOverallGender('female')}
                            >
                              Female
                            </button>
                          </div>
                          <label className="points-system-control">
                            <span>Points system</span>
                            <select
                              value={pointsSystem}
                              onChange={(event) => changePointsSystem(event.target.value)}
                            >
                              {POINTS_SYSTEMS.map((system) => (
                                <option key={system.id} value={system.id}>{system.label}</option>
                              ))}
                            </select>
                          </label>
                          <label>
                            <span>Equipment</span>
                            <select
                              value={overallEquipmentFilter}
                              onChange={(event) => setOverallEquipmentFilter(event.target.value)}
                            >
                              <option value="all">All equipment</option>
                              <option value="classic">Classic/raw</option>
                              <option value="equipped">Equipped</option>
                            </select>
                          </label>
                          <label>
                            <span>Event</span>
                            <select
                              value={overallEventFilter}
                              onChange={(event) => setOverallEventFilter(event.target.value)}
                            >
                              <option value="all">All events</option>
                              <option value="full-power">Full power</option>
                              <option value="bench-only">Bench only</option>
                            </select>
                          </label>
                        </div>
                        {visibleOverallCategories.length === 0 ? (
                          <div className="state empty">
                            No compatible {overallGender} overall scoring categories are available.
                          </div>
                        ) : (
                          <>
                            <label className="standings-category-select">
                              <span>Overall category</span>
                              <select
                                value={activeOverallCategory
                                  ? getOverallCategoryKey(activeOverallCategory)
                                  : ''}
                                onChange={(event) => setOverallCategoryKey(event.target.value)}
                              >
                                {visibleOverallCategories.map((category) => (
                                  <option
                                    key={getOverallCategoryKey(category)}
                                    value={getOverallCategoryKey(category)}
                                  >
                                    {category.equipmentCategory} · {formatEventType(category.eventType)}
                                  </option>
                                ))}
                              </select>
                            </label>
                            {activeOverallCategory && (
                              <>
                                {standingsResultMode === 'projected' && (
                                  <p className="standings-hypothetical-label">
                                    Projected view includes only scores based on recorded pending attempts;
                                    confirmed scores are not substituted.
                                  </p>
                                )}
                                <div className="standings-category-heading">
                                  <h4>
                                    {overallGender} overall · {activeOverallCategory.equipmentCategory}
                                    {' · '}{formatEventType(activeOverallCategory.eventType)}
                                  </h4>
                                  <p>
                                    Selected points are a coaching comparison and do not override competition
                                    award rules.
                                  </p>
                                </div>
                                {activeOverallCategory.standings.length === 0 ? (
                                  <div className="state empty">No athletes are available for this category.</div>
                                ) : (
                                  <div className="standings-table-wrap">
                                    <table className="standings-table">
                                      <thead>
                                        <tr>
                                          <th scope="col">Rank</th>
                                          <th scope="col">Athlete</th>
                                          <th scope="col">Division / weight class</th>
                                          <th scope="col">Bodyweight</th>
                                          <th scope="col">Points</th>
                                          <th scope="col">Source</th>
                                          <th scope="col">Actions</th>
                                        </tr>
                                      </thead>
                                      <tbody>
                                        {sortOverallStandings(
                                          activeOverallCategory.standings,
                                          standingsResultMode,
                                          pointsSystem,
                                        ).map((standing) => {
                                          const result = getOverallResult(
                                            standing,
                                            standingsResultMode,
                                            pointsSystem,
                                          )
                                          const isFollowed = followedLifterIdSet.has(standing.athleteId)
                                          const strategyPreset = getStrategyPreset(
                                            standing.athleteId,
                                            standing.divisionId,
                                            standing.weightClassId,
                                          )
                                          return (
                                            <tr
                                              className={isFollowed ? 'is-followed' : ''}
                                              key={standing.athleteId}
                                            >
                                              <td>
                                                {result.rank ? `#${result.rank}` : formatStandingStatus(result.status)}
                                              </td>
                                              <th scope="row">
                                                {standing.athleteName || standing.athleteId}
                                                {isFollowed && <span className="followed-marker">Followed</span>}
                                              </th>
                                              <td>
                                                {standing.divisionName || standing.divisionId}
                                                {' · '}
                                                {standing.weightClassName || standing.weightClassId}
                                              </td>
                                              <td>{formatKg(standing.bodyWeightKg)}</td>
                                              <td><strong>{formatPoints(result.value)}</strong></td>
                                              <td>
                                                {result.source === 'hypothetical'
                                                  ? 'Hypothetical'
                                                  : standingsResultMode === 'projected'
                                                    ? 'No projection'
                                                    : formatPoints(result.value) === 'Unavailable'
                                                      ? 'Unavailable'
                                                      : 'Confirmed'}
                                              </td>
                                              <td>
                                                <div className="standings-row-actions">
                                                  <button
                                                    type="button"
                                                    onClick={() => showLifterDetails(standing.athleteId)}
                                                  >
                                                    View details
                                                  </button>
                                                  {strategyPreset && (
                                                    <button
                                                      type="button"
                                                      onClick={() => compareInStrategy(
                                                        standing.athleteId,
                                                        standing.divisionId,
                                                        standing.weightClassId,
                                                      )}
                                                    >
                                                      Compare in Strategy
                                                    </button>
                                                  )}
                                                </div>
                                              </td>
                                            </tr>
                                          )
                                        })}
                                      </tbody>
                                    </table>
                                  </div>
                                )}
                              </>
                            )}
                          </>
                        )}
                      </>
                    )}
                  </>
                )}
              </section>
            )}
            {details && (
              <section
                className="strategy-dashboard"
                id="competition-strategy"
                aria-labelledby="strategy-heading"
              >
                <div className="strategy-header">
                  <div>
                    <h3 id="strategy-heading">Attempt Strategy</h3>
                    <p>
                      Scenarios are browser-only and never update synchronized competition results.
                    </p>
                  </div>
                  <label className="points-system-control">
                    <span>Points system</span>
                    <select
                      value={pointsSystem}
                      onChange={(event) => changePointsSystem(event.target.value)}
                    >
                      {POINTS_SYSTEMS.map((system) => (
                        <option key={system.id} value={system.id}>{system.label}</option>
                      ))}
                    </select>
                  </label>
                </div>
                {pointsSystemError && <p className="follow-storage-error" role="alert">{pointsSystemError}</p>}
                <p className="strategy-limitation">
                  Scores use the supplied legacy coefficient reference and are not a claim about current federation rules. Live lifting order remains unverified.
                </p>
                <div className="strategy-mode-switch" role="group" aria-label="Strategy comparison mode">
                  <button
                    type="button"
                    className="lifter-view-button"
                    aria-pressed={strategyMode === 'weight-class'}
                    onClick={() => {
                      setStrategyMode('weight-class')
                      setSelectedOpponentId('')
                      setStrategyOpponentIds([])
                      setStrategyScenarios({})
                    }}
                  >
                    Weight Class
                  </button>
                  <button
                    type="button"
                    className="lifter-view-button"
                    aria-pressed={strategyMode === 'best-overall'}
                    onClick={() => {
                      setStrategyMode('best-overall')
                      setSelectedOpponentId('')
                      setStrategyOpponentIds([])
                      setStrategyScenarios({})
                    }}
                  >
                    Best Overall
                  </button>
                </div>
                {followedInCompetition.length === 0 ? (
                  <div className="state empty">
                    <strong>
                      {followedLifterIds.length === 0
                        ? 'Follow a lifter to start a strategy comparison.'
                        : 'No followed lifters are present in the synchronized data.'}
                    </strong>
                    <button
                      type="button"
                      className="lifter-view-button"
                      onClick={() => setLifterView('all')}
                    >
                      Browse all lifters
                    </button>
                  </div>
                ) : (
                  <>
                    <div className="strategy-selection">
                      <label>
                        <span>Followed athlete</span>
                        <select
                          value={activeStrategistId}
                          onChange={(event) => {
                            setSelectedStrategistId(event.target.value)
                            setSelectedOpponentId('')
                            setStrategyOpponentIds([])
                            setStrategyScenarios({})
                            setStrategyError(null)
                          }}
                        >
                          {followedInCompetition.map((lifter) => {
                            const lifterId = lifter.documentId
                            return (
                              <option key={lifterId} value={lifterId}>
                                {lifter.name || 'Unavailable lifter'}
                              </option>
                            )
                          })}
                        </select>
                      </label>
                      {strategyLifter && (
                        <label>
                          <span>
                            {strategyMode === 'best-overall'
                              ? 'Scoring category and primary weight class'
                              : 'Eligible division and weight class'}
                          </span>
                          <select
                            value={activeStrategyDivisionKey}
                            onChange={(event) => {
                              setSelectedStrategyDivisionKey(event.target.value)
                              setSelectedOpponentId('')
                              setStrategyOpponentIds([])
                              setStrategyScenarios({})
                            }}
                          >
                            {strategyDivisions.map((division) => (
                              <option
                                key={`${division.divisionId}|${division.weightClassId}`}
                                value={`${division.divisionId}|${division.weightClassId}`}
                              >
                                {[
                                  division.name || division.divisionId,
                                  division.weightClassName || division.weightClassId,
                                  division.gender,
                                  division.rawOrEquipped,
                                ].filter(Boolean).join(' · ')}
                              </option>
                            ))}
                          </select>
                        </label>
                      )}
                    </div>
                    {!strategyLifter && (
                      <div className="state empty">
                        A followed lifter is not present in the synchronized data. Refresh the competition or select another followed athlete.
                      </div>
                    )}
                    {strategyLifter && strategyDivisions.length === 0 && (
                      <div className="state empty">
                        No division has enough confirmed gender, lift, and weight-class data for a comparison.
                      </div>
                    )}
                    {selectedStrategyDivision && (
                      <>
                        {strategyMode === 'best-overall' && (
                          <p className="strategy-limitation">
                            Opponents may be in other weight classes, but must match gender, equipment, and event category. This is a provisional points comparison, not an official placing.
                          </p>
                        )}
                        <div className="strategy-opponent-controls">
                          <label>
                            <span>
                              {strategyMode === 'best-overall'
                                ? 'Compatible opponent across weight classes'
                                : 'Opponent in the same division and weight class'}
                            </span>
                            <select
                              value={activeSelectedOpponentId}
                              onChange={(event) => setSelectedOpponentId(event.target.value)}
                            >
                              <option value="">Select an opponent</option>
                              {eligibleStrategyOpponents.map((lifter) => {
                                const division = getStrategyDivision(
                                  lifter,
                                  selectedStrategyDivision,
                                  strategyMode,
                                )
                                const weightClass = division?.weightClassName
                                  || division?.weightClassId
                                return (
                                  <option key={lifter.documentId} value={lifter.documentId}>
                                    {lifter.name || lifter.documentId}
                                    {strategyMode === 'best-overall' && weightClass
                                      ? ` · ${weightClass}`
                                      : ''}
                                  </option>
                                )
                              })}
                            </select>
                          </label>
                          <button
                            type="button"
                            className="lifter-view-button"
                            disabled={!activeSelectedOpponentId || activeStrategyOpponentIds.length >= 19}
                            onClick={() => {
                              if (!activeSelectedOpponentId) return
                              setStrategyOpponentIds((current) => [...current, activeSelectedOpponentId])
                              setSelectedOpponentId('')
                            }}
                          >
                            Add opponent
                          </button>
                        </div>
                        {activeStrategyOpponentIds.length > 0 && (
                          <div className="strategy-opponent-list" aria-label="Selected opponents">
                            {activeStrategyOpponentIds.map((lifterId) => {
                              const opponent = details.lifters.find((lifter) => lifter.documentId === lifterId)
                              return (
                                <span className="strategy-opponent-chip" key={lifterId}>
                                  {opponent?.name || 'Opponent'}
                                  <button
                                    type="button"
                                    aria-label={`Remove ${opponent?.name || 'opponent'}`}
                                    onClick={() => {
                                      setStrategyOpponentIds((current) => current.filter((id) => id !== lifterId))
                                      setStrategyScenarios((current) =>
                                        Object.fromEntries(
                                          Object.entries(current).filter(([key]) => !key.startsWith(`${lifterId}:`)),
                                        ),
                                      )
                                    }}
                                  >
                                    ×
                                  </button>
                                </span>
                              )
                            })}
                          </div>
                        )}
                        <p className="strategy-limitation">
                          {strategyMode === 'best-overall'
                            ? 'Best Overall compares compatible gender, equipment, and event categories across weight classes. Rankings are provisional score comparisons, not official award placings.'
                            : 'Weight-class comparisons require the same division document, weight-class ID, gender, equipment category, and lift configuration. Rankings are provisional by projected total.'}
                        </p>
                        <div className="scenario-actions">
                          <h4>Hypothetical remaining attempts</h4>
                          <button
                            type="button"
                            className="text-button"
                            disabled={Object.keys(strategyScenarios).length === 0}
                            onClick={() => setStrategyScenarios(
                              resetScenarios<{ weight: string; result: 'good' | 'bad' }>(),
                            )}
                          >
                            Reset scenario
                          </button>
                        </div>
                        <p className="follow-storage-note">
                          Only existing unresolved attempts with a selected source weight can be simulated. Entered weights and good/bad outcomes remain in this page only.
                        </p>
                        <div className="scenario-athletes">
                          {strategyLifters.map((lifter) => {
                            const name = lifter.name || 'Unnamed lifter'
                            const editableAttempts = lifter.attempts.filter((attempt) =>
                              canEditScenarioAttempt(attempt, details.units),
                            )
                            return (
                              <fieldset className="scenario-athlete" key={lifter.documentId}>
                                <legend>{name}</legend>
                                {editableAttempts.length === 0 ? (
                                  <p>No selected unresolved attempts are available to simulate.</p>
                                ) : (
                                  editableAttempts.map((attempt) => {
                                    const key = getScenarioKey(lifter.documentId, attempt.documentId)
                                    const scenario = strategyScenarios[key]
                                    const slot = formatValue(attempt.attemptNumber)
                                    const label = `${attempt.liftName || 'Lift'} attempt ${slot}`
                                    return (
                                      <div className="scenario-attempt" key={attempt.documentId}>
                                        <label>
                                          <span>{label} · source weight {formatValue(attempt.weight)} {details.units || ''}</span>
                                          <select
                                            value={scenario?.result ?? ''}
                                            onChange={(event) => changeScenario(
                                              lifter.documentId,
                                              attempt,
                                              event.target.value as '' | 'good' | 'bad',
                                            )}
                                          >
                                            <option value="">Baseline: assume selected weight succeeds</option>
                                            <option value="good">Simulate good</option>
                                            <option value="bad">Simulate bad</option>
                                          </select>
                                        </label>
                                        {scenario && (
                                          <label>
                                            <span>Simulated weight ({details.units || 'source units'})</span>
                                            <input
                                              type="number"
                                              min="0.01"
                                              step="any"
                                              value={scenario.weight}
                                              onChange={(event) => changeScenario(
                                                lifter.documentId,
                                                attempt,
                                                scenario.result,
                                                event.target.value,
                                              )}
                                            />
                                          </label>
                                        )}
                                      </div>
                                    )
                                  })
                                )}
                              </fieldset>
                            )
                          })}
                        </div>
                        {strategyRequestKey && strategyLoading && (
                          <div className="state">Recalculating strategy…</div>
                        )}
                        {strategyError && <div className="state error">{strategyError}</div>}
                        {strategyResult && (
                          <div className="strategy-results">
                            <h4>
                              {strategyResult.mode === 'best-overall'
                                ? 'Best Overall · compatible categories'
                                : `${selectedStrategyDivision.name || 'Selected division'} · ${selectedStrategyDivision.weightClassName || selectedStrategyDivision.weightClassId}`}
                            </h4>
                            <p>
                              {strategyResult.mode === 'best-overall'
                                ? 'Provisional points ranking across weight classes; this is not an official award placing.'
                                : 'Provisional projected-total ranking; values in kilograms and not an official placing.'}
                              {' '}Scoring system: {POINTS_SYSTEMS.find((system) => system.id === strategyResult.pointsSystem)?.label}.
                            </p>
                            <div className="attempt-table-wrap">
                              <table className="strategy-table">
                                <thead>
                                  <tr>
                                    <th scope="col">Athlete</th>
                                    {strategyResult.mode === 'best-overall' && (
                                      <th scope="col">Weight class</th>
                                    )}
                                    <th scope="col">Bodyweight</th>
                                    <th scope="col">Best squat</th>
                                    <th scope="col">Best bench</th>
                                    <th scope="col">Best deadlift</th>
                                    <th scope="col">Confirmed sum</th>
                                    <th scope="col">Current eligible total</th>
                                    <th scope="col">Baseline projection</th>
                                    <th scope="col">Scenario projection</th>
                                    <th scope="col">Confirmed points</th>
                                    <th scope="col">Projected points</th>
                                    <th scope="col">Provisional place</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {strategyResult.standings.map((standing) => (
                                    <tr key={standing.lifterId}>
                                      <th scope="row">{standing.name || standing.lifterId}</th>
                                      {strategyResult.mode === 'best-overall' && (
                                        <td>{standing.weightClassName || standing.weightClassId || 'Unavailable'}</td>
                                      )}
                                      <td>{formatValue(standing.bodyWeight)} {details.units || ''}</td>
                                      <td>{formatKg(standing.currentPerformance.bestSquatKg)}</td>
                                      <td>{formatKg(standing.currentPerformance.bestBenchKg)}</td>
                                      <td>{formatKg(standing.currentPerformance.bestDeadliftKg)}</td>
                                      <td>{formatKg(standing.currentPerformance.confirmedSuccessfulLiftSumKg)}</td>
                                      <td>{formatKg(standing.currentPerformance.eligibleCompleteTotalKg)}</td>
                                      <td>{formatKg(standing.currentPerformance.baselineProjectedEligibleTotalKg)}</td>
                                      <td>{formatKg(standing.scenarioPerformance.baselineProjectedEligibleTotalKg)}</td>
                                      <td>{formatPoints(standing.currentConfirmedPoints)}</td>
                                      <td>{formatPoints(standing.projectedPoints)}</td>
                                      <td>
                                        {standing.provisionalPlace
                                          ? `${standing.provisionalPlace}${standing.isTied ? ' · tied' : ''}`
                                          : 'Unavailable'}
                                      </td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          </div>
                        )}
                      </>
                    )}
                  </>
                )}
              </section>
            )}
            {details && (
              <section
                className="lifter-dashboard"
                id="competition-lifters"
                aria-labelledby="lifter-dashboard-heading"
              >
                <div className="lifter-dashboard-heading">
                  <div>
                    <h3 id="lifter-dashboard-heading">
                      {lifterView === 'followed' ? 'My Lifters' : 'Competition lifters'}
                    </h3>
                    <p>Last successful sync: {formatSyncedAt(details.lastSyncedAtUtc)}</p>
                  </div>
                  <div className="lifter-view-switch" role="group" aria-label="Lifter list view">
                    <button
                      type="button"
                      className="lifter-view-button"
                      aria-pressed={lifterView === 'all'}
                      onClick={() => setLifterView('all')}
                    >
                      All lifters ({details.lifters.length})
                    </button>
                    <button
                      type="button"
                      className="lifter-view-button"
                      aria-pressed={lifterView === 'followed'}
                      onClick={() => setLifterView('followed')}
                    >
                      My Lifters ({followedLifterIds.length})
                    </button>
                  </div>
                </div>
                <p className="follow-storage-note">
                  Followed lifters are saved in this browser for this competition only.
                </p>
                {followStorageError && (
                  <p className="follow-storage-error" role="alert">{followStorageError}</p>
                )}
                {lifterView === 'followed' && missingFollowedLifterIds.length > 0 && (
                  <div className="followed-missing" role="status">
                    <span>
                      {missingFollowedLifterIds.length} saved {missingFollowedLifterIds.length === 1 ? 'lifter is' : 'lifters are'} not in the current synchronized data.
                    </span>
                    <button
                      type="button"
                      className="text-button"
                      onClick={() => persistFollowedLifterIds(
                        followedLifterIds.filter((id) => availableLifterIds.has(id)),
                      )}
                    >
                      Remove missing selections
                    </button>
                  </div>
                )}
                {lifterView === 'followed' && liftersToDisplay.length === 0 && (
                  <div className="state empty">
                    <strong>
                      {followedLifterIds.length === 0
                        ? 'No lifters followed in this competition.'
                        : 'No followed lifters are available in synchronized data.'}
                    </strong>
                    <span>
                      {followedLifterIds.length === 0
                        ? 'Browse all lifters and select Follow beside each lifter you coach.'
                        : 'A saved lifter may have been removed or may not be present in the current snapshot.'}
                    </span>
                    <button
                      type="button"
                      className="lifter-view-button"
                      onClick={() => setLifterView('all')}
                    >
                      Browse all lifters
                    </button>
                  </div>
                )}
                {lifterView === 'all' && details.lifters.length === 0 && (
                  <div className="state empty">No lifter records were found in the synchronized data.</div>
                )}
                {liftersToDisplay.length > 0 && (
                  <div className="lifter-groups">
                    {getLifterGroups(liftersToDisplay).map((group) => (
                      <section className="flight-group" key={group.label}>
                        <h3>{group.label}</h3>
                        <div className="lifter-list">
                          {group.lifters.map((lifter) => {
                            const isFollowed = followedLifterIdSet.has(lifter.documentId)
                            const platformLabel = lifter.platformId
                              ? details.platforms.find(
                                  (platform) => platform.documentId === lifter.platformId,
                                )?.name || lifter.platformId
                              : '—'
                            const lifterName = lifter.name || 'Unnamed lifter'

                            return (
                              <article
                                className={`lifter-card ${isFollowed ? 'is-followed' : ''}`}
                                id={`lifter-${encodeURIComponent(lifter.documentId)}`}
                                key={lifter.documentId}
                              >
                                <div className="lifter-heading">
                                  <div>
                                    <h4>{lifterName}</h4>
                                    <p className="division-label">
                                      {lifter.divisions.length > 0
                                        ? lifter.divisions.map((division) => (
                                            [
                                              division.name || division.divisionId || 'Division',
                                              division.weightClassName,
                                              division.rawOrEquipped,
                                            ].filter(Boolean).join(' · ')
                                          )).join(', ')
                                        : 'Division unavailable'}
                                    </p>
                                  </div>
                                  <div className="lifter-card-controls">
                                    <div className="lifter-meta">
                                      <span>Flight {lifter.flight || '—'}</span>
                                      <span>Session {formatValue(lifter.session)}</span>
                                      <span>Platform {platformLabel}</span>
                                      <span>Lot {formatValue(lifter.lot)}</span>
                                      <span>Bodyweight {formatValue(lifter.bodyWeight)}</span>
                                    </div>
                                    <button
                                      type="button"
                                      className={`follow-button ${isFollowed ? 'following' : ''}`}
                                      aria-pressed={isFollowed}
                                      aria-label={`${isFollowed ? 'Unfollow' : 'Follow'} ${lifterName}`}
                                      onClick={() => toggleFollowedLifterId(lifter.documentId)}
                                    >
                                      {isFollowed ? 'Unfollow' : 'Follow'}
                                    </button>
                                  </div>
                                </div>
                                <div className="division-points-list">
                                  <strong>
                                    {POINTS_SYSTEMS.find((system) => system.id === pointsSystem)?.label} points
                                  </strong>
                                  <span className="follow-storage-note">
                                    Projected scores assume unresolved selected weights succeed.
                                  </span>
                                  {lifter.divisions.length === 0 ? (
                                    <span>Score unavailable: division category is missing.</span>
                                  ) : (
                                    lifter.divisions.map((division, index) => {
                                      const score = division.pointsScores?.[pointsSystem]
                                      const divisionLabel = [
                                        division.name || division.divisionId || 'Division',
                                        division.weightClassName || division.weightClassId,
                                      ].filter(Boolean).join(' · ')
                                      return (
                                        <div
                                          className="division-points-row"
                                          key={`${division.divisionId}|${division.weightClassId}|${index}`}
                                        >
                                          <strong>{divisionLabel}</strong>
                                          <span>Confirmed: {formatPoints(score?.confirmedScore)}</span>
                                          <span>Projected: {formatPoints(score?.projectedScore)}</span>
                                        </div>
                                      )
                                    })
                                  )}
                                </div>
                                {lifterView === 'followed' && (
                                  <div className="performance-summary">
                                    <strong>Performance estimates (kg)</strong>
                                    <span>
                                      Best S/B/D: {formatKg(lifter.performance.bestSquatKg)} / {formatKg(lifter.performance.bestBenchKg)} / {formatKg(lifter.performance.bestDeadliftKg)}
                                    </span>
                                    <span>Confirmed successful-lift sum: {formatKg(lifter.performance.confirmedSuccessfulLiftSumKg)}</span>
                                    <span>Complete three-lift total: {formatKg(lifter.performance.completeThreeLiftTotalKg)}</span>
                                    <span>
                                      Baseline projected three-lift total (pending selected weights assumed good): {formatKg(lifter.performance.baselineProjectedThreeLiftTotalKg)}
                                    </span>
                                    <span>Next known attempt: order unavailable; live queue order is unverified.</span>
                                  </div>
                                )}
                                <div className="attempt-table-wrap">
                                  <table className="attempt-table">
                                    <thead>
                                      <tr>
                                        <th scope="col">Lift</th>
                                        <th scope="col">Attempt 1</th>
                                        <th scope="col">Attempt 2</th>
                                        <th scope="col">Attempt 3</th>
                                      </tr>
                                    </thead>
                                    <tbody>
                                      {([
                                        ['squat', 'Squat'],
                                        ['bench', 'Bench'],
                                        ['deadlift', 'Deadlift'],
                                      ] as const).map(([liftType, label]) => (
                                        <tr key={liftType}>
                                          <th scope="row">{label}</th>
                                          {[1, 2, 3].map((attemptNumber) => {
                                            const attempt = getAttemptForSlot(lifter, liftType, attemptNumber)
                                            return (
                                              <td
                                                key={attemptNumber}
                                                className={`attempt-result ${getAttemptStatus(attempt)}`}
                                              >
                                                {describeAttempt(attempt)}
                                              </td>
                                            )
                                          })}
                                        </tr>
                                      ))}
                                    </tbody>
                                  </table>
                                </div>
                              </article>
                            )
                          })}
                        </div>
                      </section>
                    ))}
                  </div>
                )}
              </section>
            )}
          </section>
        ) : (
          <>
            {loading && <div className="state">Loading competitions...</div>}

            {error && <div className="state error">Error: {error}</div>}

            {!loading && !error && competitions.length === 0 && (
              <div className="state">No competitions found</div>
            )}

            {competitions.length > 0 && (
              <div className="competitions">
                <h2>Competitions ({competitions.length})</h2>
                <div className="grid">
                  {sortedCompetitions.map((competition) => {
                    const dateValue = getDateValue(competition.parsedDate)
                    const category = dateValue === null
                      ? 'Date unavailable'
                      : dateValue >= todayValue
                        ? 'Upcoming'
                        : 'Past'
                    const dateLabel = dateValue === null
                      ? competition.date?.trim() || 'Date unavailable'
                      : formatDate(competition.parsedDate)

                    return (
                      <button
                        key={competition.id}
                        className="card card-button"
                        onClick={() => {
                          setDetails(null)
                          setPlatformDashboard(null)
                          let savedIds: string[] = []
                          try {
                            savedIds = loadFollowedLifterIds(window.localStorage, competition.id)
                            setFollowStorageError(null)
                          } catch {
                            setFollowStorageError(
                              'Saved followed lifters could not be read from this browser.',
                            )
                          }
                          setFollowedSelection({
                            competitionId: competition.id,
                            lifterIds: savedIds,
                          })
                          setLifterView(savedIds.length > 0 ? 'followed' : 'all')
                          setRefreshSummary(null)
                          setRefreshError(null)
                          setSelectedCompetition(competition)
                        }}
                      >
                        <h3>{competition.name || 'Unnamed competition'}</h3>
                        <p className="date">{dateLabel}</p>
                        <span className={`badge ${category === 'Upcoming' ? 'upcoming' : category === 'Past' ? 'past' : 'unavailable'}`}>
                          {category}
                        </span>
                        {competition.showOnHomePage && <span className="badge">Featured</span>}
                        <span className="open-details">View synchronized details →</span>
                      </button>
                    )
                  })}
                </div>
              </div>
            )}
          </>
        )}
      </main>
    </div>
  )
}
