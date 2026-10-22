import test from 'node:test'
import assert from 'node:assert/strict'
import {
  filterOverallCategories,
  getOverallResult,
  getVisibleStandingsCategories,
  getWeightClassResult,
  sortOverallStandings,
  sortWeightClassStandings,
} from '../src/standingsUi.js'

const weightClasses = [
  {
    divisionId: 'division-a',
    standings: [{ athleteId: 'followed' }, { athleteId: 'other' }],
  },
  {
    divisionId: 'division-b',
    standings: [{ athleteId: 'followed' }],
  },
  {
    divisionId: 'division-c',
    standings: [{ athleteId: 'other' }],
  },
]

test('All Classes and My Classes switch categories using followed athlete IDs', () => {
  const standings = { weightClasses, overallCategories: [{ gender: 'male' }] }

  assert.equal(getVisibleStandingsCategories(standings, 'all-classes').length, 3)
  assert.deepEqual(
    getVisibleStandingsCategories(standings, 'my-classes', ['followed'])
      .map((category) => category.divisionId),
    ['division-a', 'division-b'],
  )
  assert.deepEqual(
    getVisibleStandingsCategories(standings, 'my-classes', []).map((category) => category.divisionId),
    [],
  )
  assert.deepEqual(
    getVisibleStandingsCategories(standings, 'overall'),
    standings.overallCategories,
  )
})

test('Overall filters keep gender, equipment, and event categories separate', () => {
  const categories = [
    { gender: 'male', equipmentCategory: 'classic', eventType: 'full-power' },
    { gender: 'female', equipmentCategory: 'classic', eventType: 'full-power' },
    { gender: 'male', equipmentCategory: 'equipped', eventType: 'full-power' },
    { gender: 'male', equipmentCategory: 'classic', eventType: 'bench-only' },
  ]

  assert.deepEqual(
    filterOverallCategories(categories, 'male', 'classic', 'full-power'),
    [categories[0]],
  )
  assert.equal(filterOverallCategories(categories, 'female', '', '').length, 1)
})

test('Confirmed and projected modes keep hypothetical values distinct', () => {
  const classStanding = {
    confirmedResultKg: 400,
    projectedResultKg: 420,
    confirmedRank: 2,
    confirmedStatus: 'ranked',
    projectedRank: 1,
    projectedStatus: 'ranked',
    hasPendingAttempts: true,
  }
  const overallStanding = {
    athleteId: 'athlete',
    hasPendingAttempts: true,
    pointsScores: {
      dots: { confirmedScore: 350, projectedScore: 370 },
    },
    rankings: {
      dots: {
        confirmedRank: 2,
        confirmedStatus: 'ranked',
        projectedRank: 1,
        projectedStatus: 'ranked',
      },
    },
  }

  assert.deepEqual(getWeightClassResult(classStanding, 'confirmed'), {
    value: 400,
    rank: 2,
    status: 'ranked',
    source: 'confirmed',
  })
  assert.equal(getWeightClassResult(classStanding, 'projected').source, 'hypothetical')
  assert.equal(getOverallResult(overallStanding, 'confirmed', 'dots').value, 350)
  assert.equal(getOverallResult(overallStanding, 'projected', 'dots').value, 370)
  assert.equal(getOverallResult(overallStanding, 'projected', 'dots').source, 'hypothetical')

  const classStandings = [
    {
      athleteId: 'a',
      athleteName: 'Athlete A',
      confirmedResultKg: 400,
      projectedResultKg: 420,
      confirmedRank: 1,
      confirmedStatus: 'ranked',
      projectedRank: 2,
      projectedStatus: 'ranked',
      hasPendingAttempts: true,
    },
    {
      athleteId: 'b',
      athleteName: 'Athlete B',
      confirmedResultKg: 390,
      projectedResultKg: 430,
      confirmedRank: 2,
      confirmedStatus: 'ranked',
      projectedRank: 1,
      projectedStatus: 'ranked',
      hasPendingAttempts: true,
    },
  ]
  assert.deepEqual(
    sortWeightClassStandings(classStandings, 'confirmed').map((standing) => standing.athleteId),
    ['a', 'b'],
  )
  assert.deepEqual(
    sortWeightClassStandings(classStandings, 'projected').map((standing) => standing.athleteId),
    ['b', 'a'],
  )
})

test('Overall standings use the chosen points system and preserve unavailable values', () => {
  const standings = [
    {
      athleteId: 'a',
      athleteName: 'Athlete A',
      hasPendingAttempts: false,
      pointsScores: {
        'ipf-gl': { confirmedScore: 70, projectedScore: null },
        dots: { confirmedScore: 60, projectedScore: null },
      },
      rankings: {
        'ipf-gl': { confirmedRank: 2, confirmedStatus: 'ranked', projectedRank: null, projectedStatus: 'no-projection' },
        dots: { confirmedRank: 1, confirmedStatus: 'ranked', projectedRank: null, projectedStatus: 'no-projection' },
      },
    },
    {
      athleteId: 'b',
      athleteName: 'Athlete B',
      hasPendingAttempts: false,
      pointsScores: {
        'ipf-gl': { confirmedScore: 80, projectedScore: null },
        dots: { confirmedScore: 55, projectedScore: null },
      },
      rankings: {
        'ipf-gl': { confirmedRank: 1, confirmedStatus: 'ranked', projectedRank: null, projectedStatus: 'no-projection' },
        dots: { confirmedRank: 2, confirmedStatus: 'ranked', projectedRank: null, projectedStatus: 'no-projection' },
      },
    },
    {
      athleteId: 'unavailable',
      athleteName: 'Unavailable',
      hasPendingAttempts: false,
      pointsScores: {
        'ipf-gl': { confirmedScore: null, projectedScore: null },
        dots: { confirmedScore: null, projectedScore: null },
      },
      rankings: {
        'ipf-gl': { confirmedRank: null, confirmedStatus: 'unavailable', projectedRank: null, projectedStatus: 'no-projection' },
        dots: { confirmedRank: null, confirmedStatus: 'unavailable', projectedRank: null, projectedStatus: 'no-projection' },
      },
    },
  ]

  assert.deepEqual(sortOverallStandings(standings, 'confirmed', 'ipf-gl').map((item) => item.athleteId), [
    'b',
    'a',
    'unavailable',
  ])
  assert.deepEqual(sortOverallStandings(standings, 'confirmed', 'dots').map((item) => item.athleteId), [
    'a',
    'b',
    'unavailable',
  ])
  assert.equal(getOverallResult(standings[2], 'confirmed', 'ipf-gl').value, null)
})
