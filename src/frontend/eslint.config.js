import js from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  // shadcn-vue 生成物为只读上游代码，不纳入本地规则约束
  { ignores: ['dist/**', 'node_modules/**', 'coverage/**', 'src/components/ui/**'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...pluginVue.configs['flat/recommended'],
  {
    files: ['**/*.vue'],
    languageOptions: {
      // .vue 文件不在 typescript-eslint 的 TS 规则作用域内，需自行声明浏览器全局（TS 侧由 vue-tsc 校验）；
      // 类型名（SVGGElement / TransitionEvent）在此仅为让 no-undef 认识（ES 侧是值、TS 侧是类型，同名不同空间）
      globals: {
        window: 'readonly',
        document: 'readonly',
        console: 'readonly',
        SVGGElement: 'readonly',
        TransitionEvent: 'readonly'
      },
      parserOptions: {
        parser: tseslint.parser,
        extraFileExtensions: ['.vue'],
        sourceType: 'module'
      }
    },
    rules: {
      'vue/multi-word-component-names': 'off'
    }
  },
  {
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }]
    }
  }
)
