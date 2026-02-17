import type { HolidayAlert } from '../types'

interface Props {
  alert: HolidayAlert
}

export function HolidayCard({ alert }: Props) {
  const holidayDate = new Date(alert.date)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const alertDate = new Date(holidayDate)
  alertDate.setHours(0, 0, 0, 0)
  
  const daysDiff = Math.floor((alertDate.getTime() - today.getTime()) / (1000 * 60 * 60 * 24))
  
  const date = holidayDate.toLocaleDateString(undefined, {
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
  const detected = new Date(alert.detectedAtUtc).toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
  })

  const getDayLabel = () => {
    if (daysDiff === 0) return 'Today'
    if (daysDiff === 1) return 'Tomorrow'
    if (daysDiff > 1) return `In ${daysDiff} days`
    return 'Past'
  }

  const getDayBadgeColor = () => {
    if (daysDiff === 0) return 'bg-red-500/20 text-red-400'
    if (daysDiff === 1) return 'bg-amber-500/20 text-amber-400'
    if (daysDiff > 1) return 'bg-blue-500/20 text-blue-400'
    return 'bg-slate-500/20 text-slate-400'
  }

  return (
    <li className="rounded-xl border border-slate-800 bg-slate-900/60 p-4 transition hover:border-slate-700">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-1">
            <p className="font-semibold text-white">{alert.name}</p>
            <span className={`rounded-md px-2 py-0.5 text-xs font-medium ${getDayBadgeColor()}`}>
              {getDayLabel()}
            </span>
          </div>
          <p className="text-sm text-slate-400">{alert.localName}</p>
          <p className="mt-1 text-xs text-slate-500">
            {alert.countryName} ({alert.countryCode}) · {date}
          </p>
        </div>
        <span className="rounded-md bg-emerald-500/20 px-2 py-1 text-xs font-medium text-emerald-400 whitespace-nowrap">
          Detected {detected}
        </span>
      </div>
    </li>
  )
}
