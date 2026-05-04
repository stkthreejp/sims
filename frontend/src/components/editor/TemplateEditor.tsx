import { useEditor, EditorContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import Underline from '@tiptap/extension-underline'
import TextAlign from '@tiptap/extension-text-align'
import Image from '@tiptap/extension-image'
import { Table } from '@tiptap/extension-table'
import TableRow from '@tiptap/extension-table-row'
import TableCell from '@tiptap/extension-table-cell'
import TableHeader from '@tiptap/extension-table-header'
import Color from '@tiptap/extension-color'
import { TextStyle } from '@tiptap/extension-text-style'
import Placeholder from '@tiptap/extension-placeholder'
import { TagNode } from './TagNode'
import { useEffect, useRef, useState } from 'react'
import {
  Bold, Italic, Underline as UnderlineIcon, AlignLeft, AlignCenter, AlignRight,
  AlignJustify, List, ListOrdered, Image as ImageIcon, Table as TableIcon,
  Undo, Redo, ChevronDown, Tag,
} from 'lucide-react'
import type { TemplateEntityType, TagGroup } from '@/lib/templateTags'
import { TEMPLATE_TAGS } from '@/lib/templateTags'

// ── Toolbar button ─────────────────────────────────────────────────────────────
function ToolbarBtn({
  onClick, active, disabled, title, children,
}: {
  onClick: () => void
  active?: boolean
  disabled?: boolean
  title?: string
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onMouseDown={(e) => { e.preventDefault(); onClick() }}
      disabled={disabled}
      title={title}
      className={`p-1.5 rounded text-sm transition-colors ${
        active
          ? 'bg-blue-100 text-blue-700'
          : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
      } disabled:opacity-30`}
    >
      {children}
    </button>
  )
}

function ToolbarDivider() {
  return <div className="w-px h-5 bg-slate-200 mx-1" />
}

// ── Tag picker dropdown ────────────────────────────────────────────────────────
function TagPicker({
  groups,
  onInsert,
}: {
  groups: TagGroup[]
  onInsert: (tag: string) => void
}) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const ref = useRef<HTMLDivElement>(null)

  // Close on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const filtered = groups.map((g) => ({
    ...g,
    tags: g.tags.filter(
      (t) =>
        search === '' ||
        t.name.toLowerCase().includes(search.toLowerCase()) ||
        t.description.toLowerCase().includes(search.toLowerCase())
    ),
  })).filter((g) => g.tags.length > 0)

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onMouseDown={(e) => { e.preventDefault(); setOpen((o) => !o) }}
        className="flex items-center gap-1 px-2 py-1.5 rounded text-sm text-blue-700 bg-blue-50 hover:bg-blue-100 border border-blue-200 font-medium"
      >
        <Tag className="h-3.5 w-3.5" />
        Insert Tag
        <ChevronDown className="h-3 w-3" />
      </button>

      {open && (
        <div className="absolute top-full left-0 mt-1 w-64 bg-white border border-slate-200 rounded-lg shadow-lg z-50">
          <div className="p-2 border-b border-slate-100">
            <input
              autoFocus
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search tags…"
              className="w-full px-2 py-1.5 text-xs border border-slate-200 rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
              onMouseDown={(e) => e.stopPropagation()}
            />
          </div>
          <div className="max-h-72 overflow-y-auto py-1">
            {filtered.length === 0 && (
              <p className="text-xs text-slate-400 px-3 py-4 text-center">No tags match</p>
            )}
            {filtered.map((group) => (
              <div key={group.label}>
                <p className="px-3 py-1 text-xs font-semibold text-slate-400 uppercase tracking-wide">
                  {group.label}
                </p>
                {group.tags.map((t) => (
                  <button
                    key={t.name}
                    type="button"
                    onMouseDown={(e) => {
                      e.preventDefault()
                      onInsert(t.name)
                      setOpen(false)
                      setSearch('')
                    }}
                    className="w-full flex items-center justify-between px-3 py-1.5 hover:bg-blue-50 text-left"
                  >
                    <span className="text-xs font-mono text-blue-700">{`{{${t.name}}}`}</span>
                    <span className="text-xs text-slate-400 ml-2 truncate">{t.description}</span>
                  </button>
                ))}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

// ── Main editor ────────────────────────────────────────────────────────────────
interface TemplateEditorProps {
  content: string
  onChange: (html: string) => void
  entityType: TemplateEntityType
}

export function TemplateEditor({ content, onChange, entityType }: TemplateEditorProps) {
  const tagGroups = TEMPLATE_TAGS[entityType] ?? TEMPLATE_TAGS.General

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ heading: { levels: [1, 2, 3] } }),
      Underline,
      TextAlign.configure({ types: ['heading', 'paragraph'] }),
      TextStyle,
      Color,
      Image.configure({ inline: false, allowBase64: true }),
      Table.configure({ resizable: true }),
      TableRow,
      TableHeader,
      TableCell,
      Placeholder.configure({ placeholder: 'Start typing your template…' }),
      TagNode,
    ],
    content,
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
  })

  // Sync content when it changes externally (e.g. after import)
  useEffect(() => {
    if (editor && content !== editor.getHTML()) {
      editor.commands.setContent(content, false)
    }
  }, [content]) // eslint-disable-line react-hooks/exhaustive-deps

  const insertTag = (tag: string) => {
    editor?.chain().focus().insertContent({
      type: 'templateTag',
      attrs: { tag },
    }).run()
  }

  const insertImage = () => {
    const url = prompt('Enter image URL (or paste a blob URL):')
    if (url) editor?.chain().focus().setImage({ src: url }).run()
  }

  if (!editor) return null

  return (
    <div className="border border-slate-300 rounded-lg overflow-hidden flex flex-col">
      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-0.5 px-2 py-1.5 border-b border-slate-200 bg-slate-50">
        {/* History */}
        <ToolbarBtn onClick={() => editor.chain().focus().undo().run()} disabled={!editor.can().undo()} title="Undo">
          <Undo className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().redo().run()} disabled={!editor.can().redo()} title="Redo">
          <Redo className="h-4 w-4" />
        </ToolbarBtn>

        <ToolbarDivider />

        {/* Heading */}
        <select
          value={
            editor.isActive('heading', { level: 1 }) ? '1'
              : editor.isActive('heading', { level: 2 }) ? '2'
              : editor.isActive('heading', { level: 3 }) ? '3'
              : '0'
          }
          onChange={(e) => {
            const level = parseInt(e.target.value)
            if (level === 0) editor.chain().focus().setParagraph().run()
            else editor.chain().focus().setHeading({ level: level as 1|2|3 }).run()
          }}
          className="text-sm border border-slate-200 rounded px-1.5 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-blue-500"
        >
          <option value="0">Paragraph</option>
          <option value="1">Heading 1</option>
          <option value="2">Heading 2</option>
          <option value="3">Heading 3</option>
        </select>

        <ToolbarDivider />

        {/* Formatting */}
        <ToolbarBtn onClick={() => editor.chain().focus().toggleBold().run()} active={editor.isActive('bold')} title="Bold">
          <Bold className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().toggleItalic().run()} active={editor.isActive('italic')} title="Italic">
          <Italic className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().toggleUnderline().run()} active={editor.isActive('underline')} title="Underline">
          <UnderlineIcon className="h-4 w-4" />
        </ToolbarBtn>

        <ToolbarDivider />

        {/* Alignment */}
        <ToolbarBtn onClick={() => editor.chain().focus().setTextAlign('left').run()} active={editor.isActive({ textAlign: 'left' })} title="Align left">
          <AlignLeft className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().setTextAlign('center').run()} active={editor.isActive({ textAlign: 'center' })} title="Align center">
          <AlignCenter className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().setTextAlign('right').run()} active={editor.isActive({ textAlign: 'right' })} title="Align right">
          <AlignRight className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().setTextAlign('justify').run()} active={editor.isActive({ textAlign: 'justify' })} title="Justify">
          <AlignJustify className="h-4 w-4" />
        </ToolbarBtn>

        <ToolbarDivider />

        {/* Lists */}
        <ToolbarBtn onClick={() => editor.chain().focus().toggleBulletList().run()} active={editor.isActive('bulletList')} title="Bullet list">
          <List className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().toggleOrderedList().run()} active={editor.isActive('orderedList')} title="Numbered list">
          <ListOrdered className="h-4 w-4" />
        </ToolbarBtn>

        <ToolbarDivider />

        {/* Table */}
        <ToolbarBtn
          onClick={() => editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()}
          title="Insert table"
        >
          <TableIcon className="h-4 w-4" />
        </ToolbarBtn>

        {/* Image */}
        <ToolbarBtn onClick={insertImage} title="Insert image">
          <ImageIcon className="h-4 w-4" />
        </ToolbarBtn>

        {/* Text color */}
        <div className="flex items-center gap-1 ml-0.5" title="Text color">
          <span className="text-xs text-slate-500">A</span>
          <input
            type="color"
            className="w-5 h-5 cursor-pointer rounded border-0 p-0 bg-transparent"
            onInput={(e) => editor.chain().focus().setColor((e.target as HTMLInputElement).value).run()}
          />
        </div>

        <ToolbarDivider />

        {/* Tag picker */}
        <TagPicker groups={tagGroups} onInsert={insertTag} />
      </div>

      {/* Editor area */}
      <EditorContent
        editor={editor}
        className="flex-1 overflow-y-auto prose prose-sm max-w-none p-6 min-h-[500px] focus-within:outline-none"
      />

      {/* Table context toolbar */}
      {editor.isActive('table') && (
        <div className="flex gap-2 px-3 py-2 border-t border-slate-200 bg-slate-50 text-xs">
          <button type="button" onMouseDown={(e) => { e.preventDefault(); editor.chain().focus().addColumnAfter().run() }} className="px-2 py-1 rounded bg-white border border-slate-200 hover:bg-slate-100">+ Col</button>
          <button type="button" onMouseDown={(e) => { e.preventDefault(); editor.chain().focus().addRowAfter().run() }} className="px-2 py-1 rounded bg-white border border-slate-200 hover:bg-slate-100">+ Row</button>
          <button type="button" onMouseDown={(e) => { e.preventDefault(); editor.chain().focus().deleteColumn().run() }} className="px-2 py-1 rounded bg-white border border-slate-200 hover:bg-slate-100 text-red-600">- Col</button>
          <button type="button" onMouseDown={(e) => { e.preventDefault(); editor.chain().focus().deleteRow().run() }} className="px-2 py-1 rounded bg-white border border-slate-200 hover:bg-slate-100 text-red-600">- Row</button>
          <button type="button" onMouseDown={(e) => { e.preventDefault(); editor.chain().focus().deleteTable().run() }} className="px-2 py-1 rounded bg-white border border-red-200 hover:bg-red-50 text-red-600">Delete Table</button>
        </div>
      )}
    </div>
  )
}
