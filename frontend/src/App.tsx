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

interface Platform {
  documentId: string
  name?: string | null
  clockState?: string | null
  clockTimerLength?: JsonValue
  currentAttemptId?: string | null
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
  if (weight === '—') return result
  return result === '—' ? weight : `${weight} · ${result}`
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
  const [detailsLoading, setDetailsLoading] = useState(false)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [notSynchronized, setNotSynchronized] = useState(false)
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
    if (!selectedCompetition) {
      return
    }

    const controller = new AbortController()
    const fetchDetails = async () => {
      setDetails(null)
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
  }, [selectedCompetition])

  return (
    <div className="app">
      <header className="header">
        <h1>LiftingCast Tracker</h1>
        <p>Discover and track powerlifting competitions</p>
      </header>

      <main className="main">
        {selectedCompetition ? (
          <section className="competition-details">
            <button className="back-button" onClick={() => setSelectedCompetition(null)}>
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

            {detailsLoading && <div className="state">Loading competition details...</div>}
            {detailsError && <div className="state error">Error: {detailsError}</div>}
            {notSynchronized && (
              <div className="state empty">
                <strong>No synchronized data yet.</strong>
                <span>Run a manual Development synchronization for this competition, then reload this page.</span>
              </div>
            )}
            {details && details.platforms.length > 0 && (
              <div className="platform-list" aria-label="Platform status">
                {details.platforms.map((platform) => (
                  <div className="platform-card" key={platform.documentId}>
                    <strong>{platform.name || 'Platform'}</strong>
                    <span>Clock: {platform.clockState || '—'}</span>
                    <span>Current attempt: {platform.currentAttemptId || '—'}</span>
                  </div>
                ))}
              </div>
            )}
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
                                    {[1, 2, 3].map((attemptNumber) => (
                                      <td key={attemptNumber}>
                                        {describeAttempt(getAttemptForSlot(lifter, liftType, attemptNumber))}
                                      </td>
                                    ))}
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
                        onClick={() => setSelectedCompetition(competition)}
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
