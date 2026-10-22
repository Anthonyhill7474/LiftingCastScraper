import assert from 'node:assert/strict'
import test from 'node:test'
import {
  getCompatibleBestOverallDivision,
  getEligibleBestOverallOpponents,
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

test('Best Overall filters by gender, equipment, and event but includes other weight classes once', () => {
  const lifters = [
    { documentId: 'primary', divisions: [rawDivision] },
    {
      documentId: 'other-weight',
      divisions: [{ ...rawDivision, weightClassId: 'weight-93', gender: 'male', rawOrEquipped: 'classic' }],
    },
    {
      documentId: 'multiple-memberships',
      divisions: [
        { ...rawDivision, divisionId: 'other-open', weightClassId: 'weight-105' },
        { ...rawDivision, weightClassId: 'weight-120' },
      ],
    },
    {
      documentId: 'female',
      divisions: [{ ...rawDivision, weightClassId: 'female-83', gender: 'FEMALE' }],
    },
    {
      documentId: 'equipped',
      divisions: [{ ...rawDivision, weightClassId: 'equipped-83', rawOrEquipped: 'EQUIPPED' }],
    },
    {
      documentId: 'bench-only',
      divisions: [{
        ...rawDivision,
        weightClassId: 'bench-83',
        lifts: { squat: false, bench: true, dead: false },
      }],
    },
  ]

  assert.deepEqual(
    getEligibleBestOverallOpponents(lifters, 'primary', rawDivision)
      .map((lifter) => lifter.documentId),
    ['other-weight', 'multiple-memberships'],
  )
  assert.equal(
    getCompatibleBestOverallDivision(lifters[2], rawDivision).weightClassId,
    'weight-120',
  )
  assert.deepEqual(
    getEligibleBestOverallOpponents(lifters, 'primary', rawDivision, ['other-weight'])
      .map((lifter) => lifter.documentId),
    ['multiple-memberships'],
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
