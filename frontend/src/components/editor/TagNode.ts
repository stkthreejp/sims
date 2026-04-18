import { Node, mergeAttributes } from '@tiptap/core'

export const TagNode = Node.create({
  name: 'templateTag',
  group: 'inline',
  inline: true,
  atom: true, // treated as a single indivisible unit — one backspace removes it

  addAttributes() {
    return {
      tag: { default: null },
    }
  },

  parseHTML() {
    return [
      {
        tag: 'span[data-tag]',
        getAttrs: (el) => ({ tag: (el as HTMLElement).getAttribute('data-tag') }),
      },
    ]
  },

  renderHTML({ node, HTMLAttributes }) {
    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        'data-tag': node.attrs.tag,
        class: 'template-tag',
        contenteditable: 'false',
      }),
      `{{${node.attrs.tag}}}`,
    ]
  },

  addNodeView() {
    return ({ node }) => {
      const span = document.createElement('span')
      span.setAttribute('data-tag', node.attrs.tag)
      span.setAttribute('contenteditable', 'false')
      span.className =
        'template-tag inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800 border border-blue-200 cursor-default select-none mx-0.5'
      span.textContent = `{{${node.attrs.tag}}}`
      span.title = node.attrs.tag
      return { dom: span }
    }
  },
})
