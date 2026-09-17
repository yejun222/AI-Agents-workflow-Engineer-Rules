<script setup lang="ts">
import AppBar from '@/components/business/AppBar.vue'
</script>

<template>
  <div class="min-h-screen bg-background">
    <AppBar />

    <main class="mx-auto w-full max-w-5xl px-6 py-8">
      <slot />
    </main>
  </div>
</template>
