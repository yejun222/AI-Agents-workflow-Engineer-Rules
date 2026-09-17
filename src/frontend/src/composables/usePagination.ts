import { computed, ref, type ComputedRef, type Ref } from 'vue'

/** 分页状态与方法：页码从 1 起，与后端 `PageQuery` 对齐。 */
export interface PaginationState {
  pageIndex: Ref<number>
  pageSize: Ref<number>
  totalCount: Ref<number>
  totalPages: ComputedRef<number>
  setTotal: (total: number) => void
  goTo: (page: number) => void
  reset: () => void
}

/** 通用分页状态（列表页复用，禁止在组件内散写翻页逻辑）。 */
export function usePagination(initialPageSize = 10): PaginationState {
  const pageIndex = ref<number>(1)
  const pageSize = ref<number>(initialPageSize)
  const totalCount = ref<number>(0)

  const totalPages = computed<number>(() => {
    if (totalCount.value === 0) {
      return 1
    }

    return Math.ceil(totalCount.value / pageSize.value)
  })

  function setTotal(total: number): void {
    totalCount.value = Math.max(0, total)
  }

  /** 跳页：越界时收敛到 [1, totalPages]，页码不变时返回 false 由调用方决定是否重新查询。 */
  function goTo(page: number): void {
    const next = Math.min(Math.max(1, page), totalPages.value)
    pageIndex.value = next
  }

  function reset(): void {
    pageIndex.value = 1
    totalCount.value = 0
  }

  return { pageIndex, pageSize, totalCount, totalPages, setTotal, goTo, reset }
}
