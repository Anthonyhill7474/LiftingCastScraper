export function getLiftType(liftName) {
  const name = liftName?.toLocaleLowerCase()
  if (!name) return null
  if (name.includes('squat')) return 'squat'
  if (name.includes('bench')) return 'bench'
  if (name.includes('dead')) return 'deadlift'
  return null
}

export function getAttemptNumber(value) {
  if (typeof value !== 'string' && typeof value !== 'number') return null
  const number = Number(value)
  return Number.isInteger(number) ? number : null
}

export function getAttemptForSlot(lifter, lift, number) {
  return lifter.attempts.find((attempt) =>
    getLiftType(attempt.liftName) === lift
      && getAttemptNumber(attempt.attemptNumber) === number,
  )
}

export function describeAttempt(attempt) {
  if (!attempt) return '—'
  const weight = formatValue(attempt.weight)
  const result = formatValue(attempt.result)
  if (result === '—') return weight === '—' ? 'No result' : `${weight} · No result`
  if (weight === '—') return result
  return `${weight} · ${result}`
}

export function getAttemptStatus(attempt) {
  if (!attempt) return 'missing'
  if (typeof attempt.result !== 'string' || attempt.result.trim() === '') return 'incomplete'
  const result = attempt.result.trim().toLocaleLowerCase()
  if (result === 'good') return 'success'
  if (result === 'bad') return 'failure'
  if (result === 'pending') return 'pending'
  return 'incomplete'
}

function formatValue(value) {
  if (value === undefined || value === null || value === '') return '—'
  return typeof value === 'object' ? JSON.stringify(value) : String(value)
}
