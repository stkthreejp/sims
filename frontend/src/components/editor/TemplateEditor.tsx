import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { EditorContent, useEditor } from '@tiptap/react'
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
import {
  AlignCenter,
  AlignJustify,
  AlignLeft,
  AlignRight,
  Bold,
  ChevronDown,
  Image as ImageIcon,
  Italic,
  List,
  ListOrdered,
  Redo,
  Rows3,
  Table as TableIcon,
  Tag,
  Underline as UnderlineIcon,
  Undo,
  X,
} from 'lucide-react'
import type { TemplateEntityType, TagGroup } from '@/lib/templateTags'
import { TEMPLATE_TAGS } from '@/lib/templateTags'
import type { DocumentTag } from '@/types/policyForm.types'
import { TagNode } from './TagNode'

function ToolbarBtn({
  onClick,
  active,
  disabled,
  title,
  children,
}: {
  onClick: () => void
  active?: boolean
  disabled?: boolean
  title?: string
  children: ReactNode
}) {
  return (
    <button
      type="button"
      onMouseDown={(event) => {
        event.preventDefault()
        onClick()
      }}
      disabled={disabled}
      title={title}
      className="sims-icon-btn"
      style={
        active
          ? { background: 'var(--accent-soft)', color: 'var(--accent-ink)', borderColor: 'var(--accent-light)' }
          : undefined
      }
    >
      {children}
    </button>
  )
}

function ToolbarDivider() {
  return <div className="mx-1 h-5 w-px" style={{ background: 'var(--line)' }} />
}

function TagPicker({ groups, onInsert }: { groups: TagGroup[]; onInsert: (tag: string) => void }) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handler = (event: MouseEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const filtered = groups
    .map((group) => ({
      ...group,
      tags: group.tags.filter(
        (tag) =>
          search === '' ||
          tag.name.toLowerCase().includes(search.toLowerCase()) ||
          tag.description.toLowerCase().includes(search.toLowerCase())
      ),
    }))
    .filter((group) => group.tags.length > 0)

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onMouseDown={(event) => {
          event.preventDefault()
          setOpen((value) => !value)
        }}
        className="sd-btn outline sm"
      >
        <Tag className="h-3.5 w-3.5" />
        Insert Tag
        <ChevronDown className="h-3 w-3" />
      </button>

      {open && (
        <div
          className="absolute left-0 top-full z-50 mt-1 w-72 overflow-hidden rounded-lg"
          style={{ border: '1px solid var(--line)', background: 'var(--surface)', boxShadow: 'var(--shadow-lg)' }}
        >
          <div className="p-2" style={{ borderBottom: '1px solid var(--line-2)' }}>
            <input
              autoFocus
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search tags..."
              className="sims-input h-8 w-full"
              onMouseDown={(event) => event.stopPropagation()}
            />
          </div>
          <div className="max-h-72 overflow-y-auto py-1">
            {filtered.length === 0 && (
              <p className="px-3 py-4 text-center" style={{ margin: 0, color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                No tags match
              </p>
            )}
            {filtered.map((group) => (
              <div key={group.label}>
                <p className="sims-field-label px-3 py-1">{group.label}</p>
                {group.tags.map((tag) => (
                  <button
                    key={tag.name}
                    type="button"
                    onMouseDown={(event) => {
                      event.preventDefault()
                      onInsert(tag.name)
                      setOpen(false)
                      setSearch('')
                    }}
                    className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left hover:bg-[var(--hover)]"
                  >
                    <span className="truncate font-mono" style={{ color: 'var(--accent-ink)', fontSize: 'var(--fs-sm)' }}>
                      {`{{${tag.name}}}`}
                    </span>
                    <span className="truncate" style={{ color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                      {tag.description}
                    </span>
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

function RepeatBlockPicker({
  blocks,
  onInsert,
}: {
  blocks: Record<string, DocumentTag[]>
  onInsert: (blockName: string, tags: DocumentTag[]) => void
}) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const entries = Object.entries(blocks)

  useEffect(() => {
    const handler = (event: MouseEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  if (entries.length === 0) return null

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onMouseDown={(event) => {
          event.preventDefault()
          setOpen((value) => !value)
        }}
        className="sd-btn outline sm"
      >
        <Rows3 className="h-3.5 w-3.5" />
        Repeat Block
        <ChevronDown className="h-3 w-3" />
      </button>

      {open && (
        <div
          className="absolute left-0 top-full z-50 mt-1 w-80 overflow-hidden rounded-lg"
          style={{ border: '1px solid var(--line)', background: 'var(--surface)', boxShadow: 'var(--shadow-lg)' }}
        >
          <div className="max-h-72 overflow-y-auto py-1">
            {entries.map(([blockName, tags]) => (
              <button
                key={blockName}
                type="button"
                onMouseDown={(event) => {
                  event.preventDefault()
                  onInsert(blockName, tags)
                  setOpen(false)
                }}
                className="w-full px-3 py-2 text-left hover:bg-[var(--hover)]"
              >
                <p style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>{blockName}</p>
                <p className="truncate" style={{ margin: '2px 0 0', color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>
                  {tags.slice(0, 4).map((tag) => tag.label).join(', ')}
                  {tags.length > 4 ? '...' : ''}
                </p>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

interface TemplateEditorProps {
  content: string
  onChange: (html: string) => void
  entityType: TemplateEntityType
  approvedTags?: DocumentTag[]
}

export function TemplateEditor({ content, onChange, entityType, approvedTags = [] }: TemplateEditorProps) {
  const [imageModalOpen, setImageModalOpen] = useState(false)
  const [imageUrl, setImageUrl] = useState('')

  const tagGroups = useMemo<TagGroup[]>(() => {
    if (approvedTags.length === 0) return TEMPLATE_TAGS[entityType] ?? TEMPLATE_TAGS.General

    const groups = approvedTags
      .filter((tag) => !tag.isRepeatable)
      .reduce<Record<string, TagGroup>>((acc, tag) => {
        acc[tag.category] ??= { label: tag.category, tags: [] }
        acc[tag.category].tags.push({
          name: tag.defaultFormat ? `${tag.tag} | ${tag.defaultFormat}` : tag.tag,
          description: tag.label,
        })
        return acc
      }, {})

    return [...Object.values(groups), ...(TEMPLATE_TAGS.General ?? [])]
  }, [approvedTags, entityType])

  const repeatBlocks = useMemo(
    () =>
      approvedTags
        .filter((tag) => tag.isRepeatable && tag.repeatBlock)
        .reduce<Record<string, DocumentTag[]>>((acc, tag) => {
          acc[tag.repeatBlock!] = [...(acc[tag.repeatBlock!] ?? []), tag]
          return acc
        }, {}),
    [approvedTags]
  )

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
      Placeholder.configure({ placeholder: 'Start typing your template...' }),
      TagNode,
    ],
    content,
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
  })

  useEffect(() => {
    if (editor && content !== editor.getHTML()) {
      editor.commands.setContent(content)
    }
  }, [content, editor])

  const insertTag = useCallback(
    (tag: string) => {
      editor?.chain().focus().insertContent({ type: 'templateTag', attrs: { tag } }).run()
    },
    [editor]
  )

  const insertRepeatBlock = useCallback(
    (blockName: string, tags: DocumentTag[]) => {
      const visibleTags = tags.slice(0, 4)
      const body = visibleTags.map((tag) => `<span data-tag="${tag.tag}">{{${tag.tag}}}</span>`).join(' ')
      editor
        ?.chain()
        .focus()
        .insertContent(`
          <p>{{#${blockName}}}</p>
          <p>${body}</p>
          <p>{{/${blockName}}}</p>
        `)
        .run()
    },
    [editor]
  )

  const confirmInsertImage = () => {
    if (imageUrl.trim()) {
      editor?.chain().focus().setImage({ src: imageUrl.trim() }).run()
    }
    setImageUrl('')
    setImageModalOpen(false)
  }

  if (!editor) return null

  return (
    <div className="flex min-h-[620px] flex-col overflow-hidden rounded-lg" style={{ border: '1px solid var(--line)', background: 'var(--surface)' }}>
      <div
        className="flex flex-wrap items-center gap-1 px-3 py-2"
        style={{ borderBottom: '1px solid var(--line)', background: 'var(--surface-2)' }}
      >
        <ToolbarBtn onClick={() => editor.chain().focus().undo().run()} disabled={!editor.can().undo()} title="Undo">
          <Undo className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().redo().run()} disabled={!editor.can().redo()} title="Redo">
          <Redo className="h-4 w-4" />
        </ToolbarBtn>

        <ToolbarDivider />

        <select
          value={
            editor.isActive('heading', { level: 1 })
              ? '1'
              : editor.isActive('heading', { level: 2 })
                ? '2'
                : editor.isActive('heading', { level: 3 })
                  ? '3'
                  : '0'
          }
          onChange={(event) => {
            const level = Number.parseInt(event.target.value)
            if (level === 0) editor.chain().focus().setParagraph().run()
            else editor.chain().focus().setHeading({ level: level as 1 | 2 | 3 }).run()
          }}
          className="sims-select h-8 w-auto"
        >
          <option value="0">Paragraph</option>
          <option value="1">Heading 1</option>
          <option value="2">Heading 2</option>
          <option value="3">Heading 3</option>
        </select>

        <ToolbarDivider />

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

        <ToolbarBtn onClick={() => editor.chain().focus().toggleBulletList().run()} active={editor.isActive('bulletList')} title="Bullet list">
          <List className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => editor.chain().focus().toggleOrderedList().run()} active={editor.isActive('orderedList')} title="Numbered list">
          <ListOrdered className="h-4 w-4" />
        </ToolbarBtn>

        <ToolbarDivider />

        <ToolbarBtn onClick={() => editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()} title="Insert table">
          <TableIcon className="h-4 w-4" />
        </ToolbarBtn>
        <ToolbarBtn onClick={() => setImageModalOpen(true)} title="Insert image">
          <ImageIcon className="h-4 w-4" />
        </ToolbarBtn>

        <label className="sims-icon-btn" title="Text color">
          <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>A</span>
          <input
            type="color"
            className="h-5 w-5 cursor-pointer rounded border-0 bg-transparent p-0"
            onInput={(event) => editor.chain().focus().setColor((event.target as HTMLInputElement).value).run()}
          />
        </label>

        <ToolbarDivider />

        <TagPicker groups={tagGroups} onInsert={insertTag} />
        <RepeatBlockPicker blocks={repeatBlocks} onInsert={insertRepeatBlock} />
      </div>

      <EditorContent
        editor={editor}
        className="min-h-[500px] flex-1 overflow-y-auto p-7 focus-within:outline-none"
        style={{ color: 'var(--ink)', fontSize: 'var(--fs-body)', lineHeight: 1.55 }}
      />

      {imageModalOpen && (
        <div className="sims-modal-backdrop">
          <div className="sims-modal max-w-sm">
            <div className="sims-modal-head">
              <h2 className="sims-modal-title">Insert Image</h2>
              <button
                type="button"
                onClick={() => {
                  setImageUrl('')
                  setImageModalOpen(false)
                }}
                className="sims-icon-btn"
                aria-label="Close"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="sims-modal-body">
              <label className="sims-field-label">Image URL</label>
              <input
                autoFocus
                value={imageUrl}
                onChange={(event) => setImageUrl(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') confirmInsertImage()
                }}
                placeholder="https://... or blob:..."
                className="sims-input mt-1 w-full"
              />
            </div>
            <div className="sims-modal-foot">
              <button type="button" onClick={confirmInsertImage} disabled={!imageUrl.trim()} className="sd-btn primary sm">
                Insert
              </button>
              <button
                type="button"
                onClick={() => {
                  setImageUrl('')
                  setImageModalOpen(false)
                }}
                className="sd-btn outline sm"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {editor.isActive('table') && (
        <div className="flex flex-wrap gap-2 px-3 py-2" style={{ borderTop: '1px solid var(--line)', background: 'var(--surface-2)' }}>
          <button type="button" onMouseDown={(event) => { event.preventDefault(); editor.chain().focus().addColumnAfter().run() }} className="sd-btn outline sm">Add Col</button>
          <button type="button" onMouseDown={(event) => { event.preventDefault(); editor.chain().focus().addRowAfter().run() }} className="sd-btn outline sm">Add Row</button>
          <button type="button" onMouseDown={(event) => { event.preventDefault(); editor.chain().focus().deleteColumn().run() }} className="sd-btn outline sm">Remove Col</button>
          <button type="button" onMouseDown={(event) => { event.preventDefault(); editor.chain().focus().deleteRow().run() }} className="sd-btn outline sm">Remove Row</button>
          <button type="button" onMouseDown={(event) => { event.preventDefault(); editor.chain().focus().deleteTable().run() }} className="sd-btn danger sm">Delete Table</button>
        </div>
      )}
    </div>
  )
}
