import assert from 'node:assert/strict'
import test from 'node:test'
import {
  DEFAULT_POINTS_SYSTEM,
  POINTS_SYSTEMS,
  loadPointsSystem,
  savePointsSystem,
} from '../src/pointsSystems.js'

function createStorage() {
  const values = new Map()
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  }
}

test('points system defaults to IPF GL and persists a valid selection', () => {
  const storage = createStorage()

  assert.equal(DEFAULT_POINTS_SYSTEM, 'ipf-gl')
  assert.equal(loadPointsSystem(storage), 'ipf-gl')
  assert.equal(savePointsSystem(storage, 'dots'), 'dots')
  assert.equal(loadPointsSystem(storage), 'dots')
  assert.equal(POINTS_SYSTEMS.length, 5)
})

test('invalid persisted and selected systems are handled explicitly', () => {
  const storage = createStorage()
  storage.setItem('liftingcast:points-system', 'unknown')
  assert.equal(loadPointsSystem(storage), DEFAULT_POINTS_SYSTEM)
  assert.throws(() => savePointsSystem(storage, 'unknown'), TypeError)
})
