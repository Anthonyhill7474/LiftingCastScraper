import assert from 'node:assert/strict'
import test from 'node:test'
import {
  getEligibleStrategyOpponents,
  resetScenarios,
  updateScenario,
} from '../src/strategyUi.js'

const rawDivision = {
  divisionId: 'division-open',
  weightClassId: 'weight-83',
  gender: 'MALE',
  rawOrEquipped: 'RAW',
  lifts: { squat: true, bench: true, dead: true },
}

test('opponent selection requires the exact division and weight-class IDs', () => {
  const lifters = [
    { documentId: 'primary', divisions: [rawDivision] },
    { documentId: 'same-division', divisions: [rawDivision] },
    {
      documentId: 'same-limit-other-division',
      divisions: [{ ...rawDivision, divisionId: 'division-other' }],
    },
    {
      documentId: 'equipped',
      divisions: [{ ...rawDivision, rawOrEquipped: 'EQUIPPED' }],
    },
    {
      documentId: 'multiple-divisions',
      divisions: [
        { ...rawDivision, divisionId: 'division-other' },
        rawDivision,
      ],
    },
  ]

  assert.deepEqual(
    getEligibleStrategyOpponents(lifters, 'primary', rawDivision)
      .map((lifter) => lifter.documentId),
    ['same-division', 'multiple-divisions'],
  )
  assert.deepEqual(
    getEligibleStrategyOpponents(lifters, 'primary', rawDivision, ['same-division'])
      .map((lifter) => lifter.documentId),
    ['multiple-divisions'],
  )
})

test('scenario edits remain separate and reset clears all hypothetical values', () => {
  const initial = {
    'lifter-a:attempt-a': { weight: '100', result: 'good' },
    'lifter-b:attempt-b': { weight: '120', result: 'bad' },
  }
  const edited = updateScenario(initial, 'lifter-a:attempt-a', {
    weight: '105',
    result: 'good',
  })

  assert.equal(initial['lifter-a:attempt-a'].weight, '100')
  assert.equal(edited['lifter-a:attempt-a'].weight, '105')
  assert.deepEqual(
    updateScenario(edited, 'lifter-b:attempt-b', null),
    { 'lifter-a:attempt-a': { weight: '105', result: 'good' } },
  )
  assert.deepEqual(resetScenarios(), {})
})
