const WORD_IMPORT_STYLE_MAP = [
  'table => table.sims-imported-word-table:fresh',
]

export async function importWordDocument(file: File) {
  const mammoth = await import('mammoth/mammoth.browser')
  const arrayBuffer = await file.arrayBuffer()
  const result = await mammoth.convertToHtml(
    { arrayBuffer },
    { styleMap: WORD_IMPORT_STYLE_MAP }
  )

  return normalizeImportedWordHtml(result.value)
}

export function normalizeImportedWordHtml(html: string) {
  if (typeof DOMParser === 'undefined') return html

  const parser = new DOMParser()
  const doc = parser.parseFromString(`<div>${html}</div>`, 'text/html')
  const root = doc.body.firstElementChild
  if (!root) return html

  root.querySelectorAll('table').forEach((table) => {
    table.classList.add('sims-imported-word-table')
    const rows = Array.from(table.querySelectorAll('tr'))
    const columnCount = Math.max(0, ...rows.map(getRowColumnCount))

    rows.forEach((row) => {
      const cells = Array.from(row.querySelectorAll(':scope > th, :scope > td'))
      if (cells.length === 1 && columnCount > 1) {
        const currentSpan = Number(cells[0].getAttribute('colspan') ?? '1')
        if (currentSpan < columnCount) {
          cells[0].setAttribute('colspan', String(columnCount))
        }
      }
    })
  })

  return root.innerHTML
}

function getRowColumnCount(row: Element) {
  return Array.from(row.querySelectorAll(':scope > th, :scope > td'))
    .reduce((sum, cell) => sum + Number(cell.getAttribute('colspan') ?? '1'), 0)
}
