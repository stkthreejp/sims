declare module 'mammoth/mammoth.browser' {
  interface ConvertResult {
    value: string
    messages: unknown[]
  }
  interface ConvertOptions {
    arrayBuffer: ArrayBuffer
  }
  interface HtmlOptions {
    styleMap?: string[]
  }
  export function convertToHtml(options: ConvertOptions, htmlOptions?: HtmlOptions): Promise<ConvertResult>
}
