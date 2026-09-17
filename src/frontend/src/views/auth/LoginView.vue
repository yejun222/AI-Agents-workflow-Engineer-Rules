<script setup lang="ts">
import { toTypedSchema } from '@vee-validate/zod'
import { useForm } from 'vee-validate'
import { computed, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle
} from '@/components/ui/card'
import { FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import AuthLayout from '@/layouts/AuthLayout.vue'
import { useAuthStore } from '@/stores/auth'
import { AUTH_ERROR_CODE, loginSchema, type LoginFormValues } from '@/types/auth'
import { resolveErrorCode, resolveErrorMessage } from '@/utils/error'
import { messages } from '@/utils/messages'

const authStore = useAuthStore()
const route = useRoute()
const router = useRouter()

/** 登录失败文案（1102 / 1103）：统一标题，防账号枚举（FR-02 / AC-04）。 */
const failDetail = ref<string>('')
const failed = ref<boolean>(false)

/** `?redirect=` 是「请先登录」Alert 的唯一触发条件（§2.6 page-login--guard）。 */
const guardRedirect = computed<string>(() => {
  const redirect = route.query.redirect
  return typeof redirect === 'string' ? redirect : ''
})

const { handleSubmit, isSubmitting } = useForm<LoginFormValues>({
  validationSchema: toTypedSchema(loginSchema)
})

const onSubmit = handleSubmit(async (values: LoginFormValues): Promise<void> => {
  failed.value = false
  failDetail.value = ''

  try {
    await authStore.login(values)
    await router.replace(guardRedirect.value.length > 0 ? guardRedirect.value : '/draw')
  } catch (error) {
    failed.value = true
    const code = resolveErrorCode(error)
    // 1102 统一提示「用户名或密码错误」；1103 等其他业务码展示后端文案
    failDetail.value =
      code === AUTH_ERROR_CODE.invalidCredentials ? messages.login.failedDesc : resolveErrorMessage(error)
  }
})
</script>

<template>
  <AuthLayout>
    <Card data-testid="page-login--default">
      <CardHeader>
        <CardTitle>{{ messages.login.title }}</CardTitle>
        <CardDescription>{{ messages.login.subtitle }}</CardDescription>
      </CardHeader>

      <CardContent class="space-y-4">
        <Alert
          v-if="failed"
          variant="destructive"
          data-testid="page-login--loginfail"
        >
          <AlertTitle>{{ messages.login.failedTitle }}</AlertTitle>
          <AlertDescription>{{ failDetail }}</AlertDescription>
        </Alert>

        <Alert
          v-else-if="guardRedirect.length > 0"
          data-testid="page-login--guard"
        >
          <AlertTitle>{{ messages.login.guardTitle }}</AlertTitle>
          <AlertDescription>{{ messages.login.guardDesc }}</AlertDescription>
        </Alert>

        <form
          class="space-y-5"
          novalidate
          @submit="onSubmit"
        >
          <FormField
            v-slot="{ componentField }"
            name="userName"
          >
            <FormItem>
              <FormLabel for="login-username">
                {{ messages.login.userNameLabel }}
              </FormLabel>
              <FormControl>
                <Input
                  id="login-username"
                  v-bind="componentField"
                  type="text"
                  autocomplete="username"
                  :placeholder="messages.login.userNamePlaceholder"
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField
            v-slot="{ componentField }"
            name="password"
          >
            <FormItem>
              <FormLabel for="login-password">
                {{ messages.login.passwordLabel }}
              </FormLabel>
              <FormControl>
                <Input
                  id="login-password"
                  v-bind="componentField"
                  type="password"
                  autocomplete="current-password"
                  :placeholder="messages.login.passwordPlaceholder"
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <Button
            type="submit"
            class="w-full"
            :disabled="isSubmitting"
            :data-testid="isSubmitting ? 'page-login--submit' : undefined"
          >
            <span
              v-if="isSubmitting"
              class="size-4 animate-spin rounded-full border-2 border-current border-t-transparent"
              aria-hidden="true"
            />
            {{ isSubmitting ? messages.login.submitting : messages.login.submit }}
          </Button>
        </form>
      </CardContent>

      <CardFooter class="justify-center">
        <RouterLink
          to="/register"
          class="text-sm text-muted-foreground hover:text-foreground"
        >
          {{ messages.login.toRegister }}
        </RouterLink>
      </CardFooter>
    </Card>
  </AuthLayout>
</template>
