import { useEffect, useState } from 'react'

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
  lastSyncedAtUtc?: string | null
  platforms: Platform[]
  lifters: Lifter[]
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
  rawOrEquipped?: string | null
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

function getLiftType(liftName?: string | null): 'squat' | 'bench' | 'deadlift' | null {
  const name = liftName?.toLocaleLowerCase()
  if (!name) return null
  if (name.includes('squat')) return 'squat'
  if (name.includes('bench')) return 'bench'
  if (name.includes('dead')) return 'deadlift'
  return null
}

function getAttemptNumber(value?: JsonValue): number | null {
  if (typeof value !== 'string' && typeof value !== 'number') return null
  const number = Number(value)
  return Number.isInteger(number) ? number : null
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

function getAttemptForSlot(lifter: Lifter, lift: string, number: number): Attempt | undefined {
  return lifter.attempts.find((attempt) =>
    getLiftType(attempt.liftName) === lift && getAttemptNumber(attempt.attemptNumber) === number,
  )
}

function describeAttempt(attempt?: Attempt): string {
  if (!attempt) return '—'
  const weight = formatValue(attempt.weight)
  const result = formatValue(attempt.result)
  if (result === '—') return weight === '—' ? 'No result' : `${weight} · No result`
  if (weight === '—') return result
  return `${weight} · ${result}`
}

function getAttemptStatus(attempt?: Attempt): 'missing' | 'success' | 'failure' | 'pending' | 'incomplete' {
  if (!attempt) return 'missing'
  if (typeof attempt.result !== 'string' || attempt.result.trim() === '') return 'incomplete'
  const result = attempt.result.trim().toLocaleLowerCase()
  if (result === 'good') return 'success'
  if (result === 'bad') return 'failure'
  if (result === 'pending') return 'pending'
  return 'incomplete'
}

function formatSyncedAt(value?: string | null): string {
  if (!value) return 'Not available'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

export default function App() {
  const [competitions, setCompetitions] = useState<Competition[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedCompetition, setSelectedCompetition] = useState<Competition | null>(null)
  const [details, setDetails] = useState<CompetitionDetails | null>(null)
  const [platformDashboard, setPlatformDashboard] = useState<PlatformDashboard | null>(null)
  const [detailsLoading, setDetailsLoading] = useState(false)
  const [platformDashboardLoading, setPlatformDashboardLoading] = useState(false)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [platformDashboardError, setPlatformDashboardError] = useState<string | null>(null)
  const [notSynchronized, setNotSynchronized] = useState(false)
  const [backendDevelopment, setBackendDevelopment] = useState(false)
  const [refreshing, setRefreshing] = useState<'changes' | 'full' | null>(null)
  const [refreshError, setRefreshError] = useState<string | null>(null)
  const [refreshSummary, setRefreshSummary] = useState<SyncSummary | null>(null)
  const [detailsReloadKey, setDetailsReloadKey] = useState(0)
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
                setRefreshSummary(null)
                setRefreshError(null)
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

            {detailsLoading && <div className="state">Loading competition details...</div>}
            {detailsError && <div className="state error">Error: {detailsError}</div>}
            {notSynchronized && (
              <div className="state empty">
                <strong>No synchronized data yet.</strong>
                <span>Run a manual Development synchronization for this competition, then reload this page.</span>
              </div>
            )}
            <section className="platform-dashboard" aria-labelledby="platform-dashboard-heading">
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
            {details && details.lifters.length === 0 && (
              <div className="state empty">No lifter records were found in the synchronized data.</div>
            )}
            {details && details.lifters.length > 0 && (
              <div className="lifter-groups">
                {getLifterGroups(details.lifters).map((group) => (
                  <section className="flight-group" key={group.label}>
                    <h3>{group.label}</h3>
                    <div className="lifter-list">
                      {group.lifters.map((lifter) => (
                        <article className="lifter-card" key={lifter.documentId}>
                          <div className="lifter-heading">
                            <div>
                              <h4>{lifter.name || 'Unnamed lifter'}</h4>
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
                            <div className="lifter-meta">
                              <span>Lot {formatValue(lifter.lot)}</span>
                              <span>Bodyweight {formatValue(lifter.bodyWeight)}</span>
                            </div>
                          </div>
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
                      ))}
                    </div>
                  </section>
                ))}
              </div>
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
