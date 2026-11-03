/** 1234 → "1.2K", 3400000 → "3.4M" (YouTube style). */
export function compactNumber(value: number): string {
  return new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 }).format(value);
}

/** 1 → "1 view", 1234 → "1.2K views". */
export function countLabel(value: number, singular: string, plural = `${singular}s`): string {
  return `${compactNumber(value)} ${value === 1 ? singular : plural}`;
}

/** Seconds → "4:05" or "1:02:09". */
export function duration(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = totalSeconds % 60;
  const ss = s.toString().padStart(2, '0');
  return h > 0 ? `${h}:${m.toString().padStart(2, '0')}:${ss}` : `${m}:${ss}`;
}

/** ISO date → "3 days ago". */
export function timeAgo(iso: string): string {
  const seconds = Math.max(1, (Date.now() - new Date(iso).getTime()) / 1000);
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 31536000],
    ['month', 2592000],
    ['week', 604800],
    ['day', 86400],
    ['hour', 3600],
    ['minute', 60],
  ];
  const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'always' });
  for (const [unit, size] of units) {
    if (seconds >= size) return rtf.format(-Math.floor(seconds / size), unit);
  }
  return 'just now';
}

/** Stable color for a string (channel avatars, placeholder thumbnails). */
export function hueFor(text: string): number {
  let hash = 0;
  for (const char of text) hash = (hash * 31 + char.charCodeAt(0)) | 0;
  return Math.abs(hash) % 360;
}
