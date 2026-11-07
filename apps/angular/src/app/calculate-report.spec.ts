import { describe, expect, it } from 'vitest';
import fixture from '../../../../tests/fixtures/report-cases.json';
import { calculateReport } from './calculate-report';
import { TimeEntry } from './time-entry';

function entry(name: string | null, start: string | null, end: string | null): TimeEntry {
  return { Id: 'test', EmployeeName: name, StarTimeUtc: start, EndTimeUtc: end, DeletedOn: null };
}

describe('calculateReport', () => {
  it('agrees with shared calculation cases, including the exact threshold and tie order', () => {
    const report = calculateReport(fixture.entries);
    expect(report.employees).toHaveLength(fixture.expected.employees.length);
    fixture.expected.employees.forEach((expected, index) => {
      const actual = report.employees[index];
      expect(actual.name).toBe(expected.name);
      expect(actual.totalHours).toBeCloseTo(expected.totalHours, 9);
      expect(actual.isBelowThreshold).toBe(expected.isBelowThreshold);
    });
    expect(report.quality).toEqual(fixture.expected.quality);
    expect(report.employees.reduce((sum, employee) => sum + employee.sharePercent, 0)).toBeCloseTo(
      100,
      9,
    );
  });

  it('sums fractional-hour sessions to exactly 100 hours before applying the threshold', () => {
    const beginning = Date.parse('2026-01-01T00:00:00Z');
    const entries = Array.from({ length: 300 }, (_, index) => {
      const start = beginning + index * 20 * 60_000;
      return entry(
        'Alex',
        new Date(start).toISOString(),
        new Date(start + 20 * 60_000).toISOString(),
      );
    });
    const report = calculateReport(entries);
    expect(report.employees[0].totalHours).toBe(100);
    expect(report.employees[0].isBelowThreshold).toBe(false);
    expect(report.belowThresholdCount).toBe(0);
  });

  it('returns an empty report when there are no entries', () => {
    const report = calculateReport([]);
    expect(report.employees).toEqual([]);
    expect(report.totalHours).toBe(0);
    expect(report.belowThresholdCount).toBe(0);
  });

  it('includes zero-length records without creating NaN percentages', () => {
    const report = calculateReport([entry('Alex', '2026-01-01T00:00:00', '2026-01-01T00:00:00')]);
    expect(report.employees[0]).toEqual({
      name: 'Alex',
      totalHours: 0,
      sharePercent: 0,
      isBelowThreshold: true,
    });
  });

  it('keeps names case sensitive with deterministic ordinal tie ordering', () => {
    const report = calculateReport([
      entry('alex', '2026-01-01T00:00:00', '2026-01-01T01:00:00'),
      entry('Alex', '2026-01-01T00:00:00', '2026-01-01T01:00:00'),
    ]);
    expect(report.employees.map((employee) => employee.name)).toEqual(['Alex', 'alex']);
  });

  it('counts a deleted record once even when its name is missing', () => {
    const report = calculateReport([{ ...entry(null, null, null), DeletedOn: '2026-01-01' }]);
    expect(report.quality.deleted).toBe(1);
    expect(report.quality.excluded).toBe(1);
    expect(report.quality.missingNames).toBe(0);
  });

  it('treats an absent DeletedOn field like a null one', () => {
    const { DeletedOn, ...withoutField } = entry(
      'Alex',
      '2026-01-01T00:00:00',
      '2026-01-01T01:00:00',
    );
    expect(DeletedOn).toBeNull();
    const report = calculateReport([withoutField as TimeEntry]);
    expect(report.quality.deleted).toBe(0);
    expect(report.employees[0].totalHours).toBe(1);
  });

  it('excludes missing and invalid timestamps', () => {
    const report = calculateReport([
      entry('Alex', null, '2026-01-01T01:00:00'),
      entry('Blair', 'invalid', '2026-01-01T01:00:00'),
    ]);
    expect(report.employees).toEqual([]);
    expect(report.quality.invalidTimestamps).toBe(2);
  });

  it.each(['2026-02-30T00:00:00', '2026-01-01T24:00:00', '2026-01-01'])(
    'rejects an invalid ISO calendar time: %s',
    (start) => {
      const report = calculateReport([entry('Alex', start, '2026-03-01T01:00:00')]);
      expect(report.employees).toEqual([]);
      expect(report.quality.invalidTimestamps).toBe(1);
    },
  );
});
