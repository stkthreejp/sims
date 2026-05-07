import { useQuery } from '@tanstack/react-query'
import { AlertCircle, CheckCircle2, Database, RefreshCw, XCircle } from 'lucide-react'
import { adminDatabaseApi } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'

export function DatabaseStatusPage() {
  const { data, isLoading, isFetching, refetch, error } = useQuery({
    queryKey: ['admin', 'database-status'],
    queryFn: adminDatabaseApi.getStatus,
  })

  if (isLoading) return <LoadingSpinner />

  const allTablesExist = data?.expectedTables.every((table) => table.exists) ?? false
  const hasPendingMigrations = (data?.pendingMigrations.length ?? 0) > 0
  const healthy = Boolean(data?.canConnect && allTablesExist && !hasPendingMigrations)

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Database Status"
        subtitle="Live database diagnostics"
        action={
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700 disabled:opacity-50"
          >
            <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex gap-3 text-sm text-red-700">
          <AlertCircle className="h-5 w-5 flex-shrink-0" />
          <div>
            <div className="font-semibold">Could not load database status.</div>
            <div>Your login may not have system admin permission, or the backend deployment may not include the diagnostics endpoint yet.</div>
          </div>
        </div>
      )}

      {data && (
        <>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <StatusCard
              label="Connection"
              value={data.canConnect ? 'Connected' : 'Unavailable'}
              state={data.canConnect ? 'good' : 'bad'}
            />
            <StatusCard
              label="Pending Migrations"
              value={String(data.pendingMigrations.length)}
              state={hasPendingMigrations ? 'warn' : 'good'}
            />
            <StatusCard
              label="Loss History Tables"
              value={allTablesExist ? 'Present' : 'Missing'}
              state={allTablesExist ? 'good' : 'bad'}
            />
          </div>

          <div className="bg-white border rounded-lg overflow-hidden">
            <div className="px-5 py-4 border-b flex items-center gap-2">
              <Database className="h-4 w-4 text-slate-500" />
              <h3 className="font-semibold text-slate-800">Environment</h3>
            </div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-3 p-5 text-sm">
              <InfoRow label="Provider" value={data.providerName ?? 'Unknown'} />
              <InfoRow label="Database" value={data.databaseName ?? 'Unknown'} />
              <InfoRow label="Data Source" value={data.dataSource ?? 'Unknown'} />
              <InfoRow label="Latest Migration" value={data.latestAppliedMigration ?? 'None'} />
            </div>
          </div>

          <div className="bg-white border rounded-lg overflow-hidden">
            <div className="px-5 py-4 border-b">
              <h3 className="font-semibold text-slate-800">Expected Tables</h3>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-slate-50 text-xs text-slate-500 uppercase tracking-wide text-left">
                  <th className="px-4 py-3">Table</th>
                  <th className="px-4 py-3">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {data.expectedTables.map((table) => (
                  <tr key={table.name}>
                    <td className="px-4 py-3 font-medium text-slate-800">{table.name}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium ${table.exists ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'}`}>
                        {table.exists ? <CheckCircle2 className="h-3.5 w-3.5" /> : <XCircle className="h-3.5 w-3.5" />}
                        {table.exists ? 'Exists' : 'Missing'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {data.pendingMigrations.length > 0 && (
            <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 text-sm text-amber-800">
              <div className="font-semibold mb-2">Pending migrations</div>
              <ul className="space-y-1">
                {data.pendingMigrations.map((migration) => (
                  <li key={migration}>{migration}</li>
                ))}
              </ul>
            </div>
          )}

          <div className={`border rounded-lg p-4 text-sm ${healthy ? 'bg-green-50 border-green-200 text-green-700' : 'bg-slate-50 border-slate-200 text-slate-700'}`}>
            {healthy ? 'Database looks current for the loss history release.' : 'Database status loaded. Review the connection, pending migrations, and expected table results above.'}
          </div>
        </>
      )}
    </div>
  )
}

function StatusCard({ label, value, state }: { label: string; value: string; state: 'good' | 'warn' | 'bad' }) {
  const styles = {
    good: 'bg-green-50 border-green-200 text-green-700',
    warn: 'bg-amber-50 border-amber-200 text-amber-800',
    bad: 'bg-red-50 border-red-200 text-red-700',
  }[state]

  return (
    <div className={`border rounded-lg p-4 ${styles}`}>
      <div className="text-xs font-medium uppercase tracking-wide opacity-75">{label}</div>
      <div className="mt-2 text-lg font-semibold">{value}</div>
    </div>
  )
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs text-slate-500 mb-1">{label}</div>
      <div className="font-medium text-slate-800 break-words">{value}</div>
    </div>
  )
}
