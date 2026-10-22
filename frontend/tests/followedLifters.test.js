import assert from 'node:assert/strict'
import test from 'node:test'
import {
  filterFollowedLifters,
  getMissingFollowedLifterIds,
  loadFollowedLifterIds,
  saveFollowedLifterIds,
  toggleFollowedLifter,
} from '../src/followedLifters.js'

function createStorage() {
  const values = new Map()
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  }
}

test('following and unfollowing changes only the selected lifter ID', () => {
  const followed = toggleFollowedLifter(['lifter-1'], 'lifter-2')
  assert.deepEqual(followed, ['lifter-1', 'lifter-2'])
  assert.deepEqual(toggleFollowedLifter(followed, 'lifter-1'), ['lifter-2'])
})

test('saved selections restore independently for each competition', () => {
  const storage = createStorage()
  saveFollowedLifterIds(storage, 'competition-a', ['lifter-1', 'lifter-2', 'lifter-1'])
  saveFollowedLifterIds(storage, 'competition-b', ['lifter-3'])

  assert.deepEqual(loadFollowedLifterIds(storage, 'competition-a'), ['lifter-1', 'lifter-2'])
  assert.deepEqual(loadFollowedLifterIds(storage, 'competition-b'), ['lifter-3'])
})

test('missing lifters are excluded by document ID and can be identified for cleanup', () => {
  const lifters = [
    { documentId: 'lifter-1', name: 'Same Name' },
    { documentId: 'lifter-2', name: 'Same Name' },
  ]

  assert.deepEqual(
    filterFollowedLifters(lifters, ['lifter-2', 'removed-lifter']),
    [lifters[1]],
  )
  assert.deepEqual(
    getMissingFollowedLifterIds(['lifter-2', 'removed-lifter'], lifters),
    ['removed-lifter'],
  )
})
