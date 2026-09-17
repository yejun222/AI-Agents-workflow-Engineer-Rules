/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** 接口基地址（默认空串 = 同源，由 vite dev proxy / 生产网关转发） */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue'

  const component: DefineComponent<Record<string, unknown>, Record<string, unknown>, unknown>
  export default component
}
