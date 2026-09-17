<script setup lang="ts">
import { FlexRender, getCoreRowModel, useVueTable, type ColumnDef } from '@tanstack/vue-table'
import { onMounted, ref, type Ref } from 'vue'
import { useRouter } from 'vue-router'

import { fetchWinningRecords } from '@/api/record'
import EmptyState from '@/components/business/EmptyState.vue'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow
} from '@/components/ui/table'
import { usePagination } from '@/composables/usePagination'
import AppLayout from '@/layouts/AppLayout.vue'
import type { WinningRecordDto } from '@/types/record'
import { formatUtc8DateTime } from '@/utils/datetime'
import { resolveErrorMessage } from '@/utils/error'
import { messages } from '@/utils/messages'

/** 列表状态：loading / ready / empty / error（§2.6 #17-#20）。 */
type RecordsState = 'loading' | 'ready' | 'empty' | 'error'

const router = useRouter()

const records: Ref<WinningRecordDto[]> = ref<WinningRecordDto[]>([])
const state = ref<RecordsState>('loading')
const errorMessage = ref<string>('')

const { pageIndex, pageSize, totalCount, totalPages, setTotal, goTo } = usePagination(10)

const columns: ColumnDef<WinningRecordDto>[] = [
  {
    accessorKey: 'prizeName',
    header: messages.records.prizeName
  },
  {
    accessorKey: 'winTime',
    header: messages.records.winTime,
    // 后端时间为 UTC，展示统一换算 UTC+8，精度按 PRD 4.1-3 取到分钟（REV-15）
    cell: ({ row }) => formatUtc8DateTime(row.original.winTime, 'minute')
  }
]

const table = useVueTable({
  get data() {
    return records.value
  },
  get columns() {
    return columns
  },
  getCoreRowModel: getCoreRowModel()
})

/** 拉取当前页记录；空页（如总数减少后停在末页之后）自动回退到最后一页。 */
async function loadRecords(): Promise<void> {
  state.value = 'loading'
  errorMessage.value = ''

  try {
    const response = await fetchWinningRecords({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value
    })

    records.value = response.items
    setTotal(response.totalCount)

    if (response.items.length === 0 && response.totalCount > 0 && pageIndex.value > 1) {
      goTo(totalPages.value)
      await loadRecords()
      return
    }

    state.value = response.items.length === 0 ? 'empty' : 'ready'
  } catch (error) {
    errorMessage.value = resolveErrorMessage(error)
    state.value = 'error'
  }
}

async function goToPage(page: number): Promise<void> {
  const previous = pageIndex.value
  goTo(page)

  if (pageIndex.value === previous) {
    return
  }

  await loadRecords()
}

function goDraw(): void {
  void router.push('/draw')
}

onMounted(async (): Promise<void> => {
  await loadRecords()
})
</script>

<template>
  <AppLayout>
    <div class="space-y-4">
      <h1 class="text-lg font-semibold">
        {{ messages.records.title }}
      </h1>

      <Card>
        <CardContent class="pt-6">
          <Table>
            <TableCaption>{{ messages.records.caption }}</TableCaption>
            <TableHeader>
              <TableRow>
                <TableHead>{{ messages.records.prizeName }}</TableHead>
                <TableHead class="w-56">
                  {{ messages.records.winTime }}
                </TableHead>
              </TableRow>
            </TableHeader>

            <!-- 加载态（#18）：表头保留 + 3 行骨架 -->
            <TableBody
              v-if="state === 'loading'"
              data-testid="page-records--loading"
            >
              <TableRow
                v-for="row in 3"
                :key="row"
              >
                <TableCell><Skeleton class="h-4 w-40" /></TableCell>
                <TableCell><Skeleton class="h-4 w-32" /></TableCell>
              </TableRow>
              <TableRow>
                <TableCell
                  colspan="2"
                  class="text-center text-sm text-muted-foreground"
                  role="status"
                >
                  {{ messages.records.loading }}
                </TableCell>
              </TableRow>
            </TableBody>

            <!-- 失败态（#19）：表头保留 + Alert + 重试 -->
            <TableBody
              v-else-if="state === 'error'"
              data-testid="page-records--error"
            >
              <TableRow>
                <TableCell colspan="2">
                  <Alert variant="destructive">
                    <AlertTitle>{{ messages.common.loadFailedTitle }}</AlertTitle>
                    <AlertDescription>{{ errorMessage }}</AlertDescription>
                  </Alert>
                  <div class="mt-3 flex justify-center">
                    <Button
                      variant="outline"
                      size="sm"
                      type="button"
                      data-testid="records-retry"
                      @click="loadRecords"
                    >
                      {{ messages.common.retry }}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            </TableBody>

            <!-- 空态（#20）：表内占位行 + EmptyState -->
            <TableBody
              v-else-if="state === 'empty'"
              data-testid="page-records--empty"
            >
              <TableRow>
                <TableCell
                  colspan="2"
                  class="text-center"
                >
                  <EmptyState
                    :title="messages.records.emptyTitle"
                    :description="messages.records.emptyDesc"
                    :action-label="messages.records.goDraw"
                    @action="goDraw"
                  />
                </TableCell>
              </TableRow>
            </TableBody>

            <!-- 常驻基础层（#17）：TanStack Table 渲染当前页行 -->
            <TableBody
              v-else
              data-testid="page-records--default"
            >
              <TableRow
                v-for="row in table.getRowModel().rows"
                :key="row.id"
                :data-testid="`records-row-${row.original.id}`"
              >
                <TableCell
                  v-for="cell in row.getVisibleCells()"
                  :key="cell.id"
                >
                  <FlexRender
                    :render="cell.column.columnDef.cell"
                    :props="cell.getContext()"
                  />
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div
        v-if="state === 'ready'"
        class="flex flex-wrap items-center justify-between gap-3"
      >
        <p class="text-sm text-muted-foreground">
          {{ messages.records.totalCount(totalCount) }}
        </p>

        <nav
          class="flex items-center gap-3"
          :aria-label="messages.records.paginationLabel"
        >
          <Button
            variant="outline"
            size="sm"
            type="button"
            data-testid="records-prev"
            :disabled="pageIndex <= 1"
            @click="goToPage(pageIndex - 1)"
          >
            {{ messages.records.prev }}
          </Button>
          <span class="text-sm text-muted-foreground">
            {{ messages.records.pageIndicator(pageIndex, totalPages) }}
          </span>
          <Button
            variant="outline"
            size="sm"
            type="button"
            data-testid="records-next"
            :disabled="pageIndex >= totalPages"
            @click="goToPage(pageIndex + 1)"
          >
            {{ messages.records.next }}
          </Button>
        </nav>
      </div>
    </div>
  </AppLayout>
</template>
