import assert from 'node:assert/strict'
import test from 'node:test'
import {
  describeAttempt,
  getAttemptForSlot,
  getAttemptStatus,
} from '../src/attemptPresentation.js'

test('attempt slots match string numbers numerically and keep lift identity', () => {
  const lifter = {
    attempts: [
      { liftName: 'squat', attemptNumber: '3', weight: 140, result: 'good' },
      { liftName: 'bench', attemptNumber: '1', weight: 80, result: 'good' },
      { liftName: 'dead', attemptNumber: '2', weight: 170, result: null },
    ],
  }

  assert.equal(getAttemptForSlot(lifter, 'squat', 3)?.weight, 140)
  assert.equal(getAttemptForSlot(lifter, 'deadlift', 2)?.weight, 170)
  assert.equal(getAttemptForSlot(lifter, 'bench', 2), undefined)
})

test('completed, failed, pending and incomplete attempts have distinct display states', () => {
  const good = { weight: 100, result: 'good' }
  const bad = { weight: 105, result: 'bad' }
  const pending = { weight: 110, result: 'pending' }
  const incomplete = { weight: 115, result: null }

  assert.equal(getAttemptStatus(good), 'success')
  assert.equal(getAttemptStatus(bad), 'failure')
  assert.equal(getAttemptStatus(pending), 'pending')
  assert.equal(getAttemptStatus(incomplete), 'incomplete')
  assert.equal(describeAttempt(incomplete), '115 · No result')
  assert.equal(describeAttempt(), '—')
})
