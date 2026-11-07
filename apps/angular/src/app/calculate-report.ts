import { EmployeeSummary, TimeEntry, TimeReport } from './time-entry';

const millisecondsPerHour = 3_600_000;

export function calculateReport(entries: readonly (TimeEntry | null)[]): TimeReport {
  const durationByName = new Map<string, number>();
  const quality = {
    received: entries.length,
    accepted: 0,
    excluded: 0,
    deleted: 0,
    missingNames: 0,
    invalidTimestamps: 0,
    negativeDurations: 0,
  };

  for (const entry of entries) {
    if (entry === null) {
      quality.invalidTimestamps++;
      continue;
    }
    if (entry.DeletedOn != null) {
      quality.deleted++;
      continue;
    }
    const name = entry.EmployeeName?.trim();
    if (!name) {
      quality.missingNames++;
      continue;
    }
    const start = parseUtc(entry.StarTimeUtc);
    const end = parseUtc(entry.EndTimeUtc);
    if (!Number.isFinite(start) || !Number.isFinite(end)) {
      quality.invalidTimestamps++;
      continue;
    }
    if (end < start) {
      quality.negativeDurations++;
      continue;
    }
    durationByName.set(name, (durationByName.get(name) ?? 0) + (end - start));
    quality.accepted++;
  }

  quality.excluded = quality.received - quality.accepted;
  const totalDuration = [...durationByName.values()].reduce((sum, value) => sum + value, 0);
  const employees: EmployeeSummary[] = [...durationByName].map(([name, duration]) => ({
    name,
    totalHours: duration / millisecondsPerHour,
    sharePercent: totalDuration === 0 ? 0 : (duration / totalDuration) * 100,
    isBelowThreshold: duration < 100 * millisecondsPerHour,
  }));
  employees.sort((a, b) => b.totalHours - a.totalHours || compareNames(a.name, b.name));

  return {
    employees,
    totalHours: totalDuration / millisecondsPerHour,
    belowThresholdCount: employees.filter((employee) => employee.isBelowThreshold).length,
    quality,
  };
}

function parseUtc(value: string | null): number {
  if (!value) return Number.NaN;
  const match =
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?(Z|[+-](\d{2}):(\d{2}))?$/.exec(
      value,
    );
  if (!match) return Number.NaN;
  const [, yearText, monthText, dayText, hourText, minuteText, secondText] = match;
  const [year, month, day, hour, minute, second] = [
    yearText,
    monthText,
    dayText,
    hourText,
    minuteText,
    secondText,
  ].map(Number);
  const leapYear = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const daysInMonth = [31, leapYear ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  if (
    year < 1 ||
    month < 1 ||
    month > 12 ||
    day < 1 ||
    day > daysInMonth[month - 1] ||
    hour > 23 ||
    minute > 59 ||
    second > 59
  )
    return Number.NaN;
  const offsetHours = Number(match[9] ?? 0);
  const offsetMinutes = Number(match[10] ?? 0);
  if (offsetHours > 14 || offsetMinutes > 59 || (offsetHours === 14 && offsetMinutes !== 0)) {
    return Number.NaN;
  }
  const timestamp = Date.parse(match[8] ? value : `${value}Z`);
  const utcYear = new Date(timestamp).getUTCFullYear();
  return utcYear >= 1 && utcYear <= 9999 ? timestamp : Number.NaN;
}

function compareNames(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0;
}
