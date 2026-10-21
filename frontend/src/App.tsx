import { useState, useEffect } from 'react'

interface Competition {
  id: string
  name: string
  date?: string
  dateFormat?: string
  createDate?: string
  showOnHomePage: boolean
}

export default function App() {
  const [competitions, setCompetitions] = useState<Competition[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const fetchCompetitions = async () => {
      try {
        const apiBase = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7194'
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
              {competitions.map((comp) => (
                <div key={comp.id} className="card">
                  <h3>{comp.name}</h3>
                  {comp.date && (
                    <p className="date">
                      {comp.date} {comp.dateFormat && `(${comp.dateFormat})`}
                    </p>
                  )}
                  {comp.createDate && (
                    <p className="meta">Added: {new Date(comp.createDate).toLocaleDateString()}</p>
                  )}
                  {comp.showOnHomePage && <span className="badge">Featured</span>}
                </div>
              ))}
            </div>
          </div>
        )}
      </main>
    </div>
  )
}
