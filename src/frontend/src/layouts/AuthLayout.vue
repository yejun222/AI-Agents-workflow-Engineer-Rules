<script setup lang="ts">
import { messages } from '@/utils/messages'
</script>

<template>
  <div class="flex min-h-screen flex-col items-center justify-center gap-6 bg-background p-6">
    <header class="text-center">
      <h1 class="text-2xl font-semibold tracking-tight">
        {{ messages.app.brand }}
      </h1>
      <p class="mt-1 text-sm text-muted-foreground">
        {{ messages.authLayout.tagline }}
      </p>
    </header>

    <main class="w-full max-w-md">
      <slot />
    </main>
  </div>
</template>
