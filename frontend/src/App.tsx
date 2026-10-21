import { useState, useEffect } from 'react'

interface Competition {
  id: string
  name: string
  date?: string
  dateFormat?: string
  createDate?: string
  showOnHomePage: boolean
  parsedDate?: string | null
}

function getDateValue(date?: string | null): number | null {
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date)) return null

  const value = Date.parse(`${date}T00:00:00.000Z`)
  if (Number.isNaN(value) || new Date(value).toISOString().slice(0, 10) !== date) return null
  return value
}

function formatDate(value: number): string {
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    timeZone: 'UTC',
  })
}

export default function App() {
  const [competitions, setCompetitions] = useState<Competition[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
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
        const data = await response.json()
        setCompetitions(data)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to fetch competitions')
      } finally {
        setLoading(false)
      }
    }

    fetchCompetitions()
  }, [])

  return (
    <div className="app">
      <header className="header">
        <h1>LiftingCast Tracker</h1>
        <p>Discover and track powerlifting competitions</p>
      </header>

      <main className="main">
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
                  : formatDate(dateValue)

                return (
                  <div key={competition.id} className="card">
                    <h3>{competition.name || 'Unnamed competition'}</h3>
                    <p className="date">{dateLabel}</p>
                    <span className={`badge ${category === 'Upcoming' ? 'upcoming' : category === 'Past' ? 'past' : 'unavailable'}`}>
                      {category}
                    </span>
                    {competition.showOnHomePage && <span className="badge">Featured</span>}
                  </div>
                )
              })}
            </div>
          </div>
        )}
      </main>
    </div>
  )
}
