export interface TimeEntry {
  Id: string | null;
  EmployeeName: string | null;
  StarTimeUtc: string | null;
  EndTimeUtc: string | null;
  DeletedOn: string | null;
}

export interface EmployeeSummary {
  name: string;
  totalHours: number;
  sharePercent: number;
  isBelowThreshold: boolean;
}

export interface TimeReport {
  employees: EmployeeSummary[];
  totalHours: number;
  belowThresholdCount: number;
  quality: {
    received: number;
    accepted: number;
    excluded: number;
    deleted: number;
    missingNames: number;
    invalidTimestamps: number;
    negativeDurations: number;
  };
}
