<script setup lang="ts">
import { toTypedSchema } from '@vee-validate/zod'
import { useForm } from 'vee-validate'
import { computed, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'

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
import {
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import AuthLayout from '@/layouts/AuthLayout.vue'
import { useAuthStore } from '@/stores/auth'
import { AUTH_ERROR_CODE, registerSchema, type RegisterFormValues } from '@/types/auth'
import { resolveErrorCode, resolveErrorMessage } from '@/utils/error'
import { messages } from '@/utils/messages'

const authStore = useAuthStore()
const router = useRouter()

/** 非字段级错误（网络异常 / 系统繁忙）统一落此 Alert。 */
const submitError = ref<string>('')

const { handleSubmit, isSubmitting, errors, setFieldError } = useForm<RegisterFormValues>({
  validationSchema: toTypedSchema(registerSchema)
})

/** 存在任一字段错误即进入 page-register--invalid 态（Zod 本地校验或后端 1101 / 1002）。 */
const isInvalid = computed<boolean>(() => Object.keys(errors.value).length > 0)

const onSubmit = handleSubmit(async (values: RegisterFormValues): Promise<void> => {
  submitError.value = ''

  try {
    await authStore.register(values)
    // 注册成功即登录（FR-01）
    await router.replace('/draw')
  } catch (error) {
    const code = resolveErrorCode(error)
    if (code === AUTH_ERROR_CODE.userNameTaken) {
      setFieldError('userName', messages.register.userNameTaken)
    } else if (code === AUTH_ERROR_CODE.validationFailed) {
      // 后端校验文案（含 CR-01 密码规则）就地回填到密码字段
      setFieldError('password', resolveErrorMessage(error))
    } else {
      submitError.value = resolveErrorMessage(error)
    }
  }
})
</script>

<template>
  <AuthLayout>
    <Card data-testid="page-register--default">
      <CardHeader>
        <CardTitle>{{ messages.register.title }}</CardTitle>
        <CardDescription>{{ messages.register.subtitle }}</CardDescription>
      </CardHeader>

      <CardContent>
        <form
          class="space-y-5"
          novalidate
          :data-testid="isInvalid ? 'page-register--invalid' : undefined"
          @submit="onSubmit"
        >
          <FormField
            v-slot="{ componentField }"
            name="userName"
          >
            <FormItem>
              <FormLabel for="reg-username">
                {{ messages.register.userNameLabel }}
              </FormLabel>
              <FormControl>
                <Input
                  id="reg-username"
                  v-bind="componentField"
                  type="text"
                  autocomplete="username"
                  :placeholder="messages.register.userNamePlaceholder"
                />
              </FormControl>
              <FormDescription>{{ messages.register.userNameHint }}</FormDescription>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField
            v-slot="{ componentField }"
            name="password"
          >
            <FormItem>
              <FormLabel for="reg-password">
                {{ messages.register.passwordLabel }}
              </FormLabel>
              <FormControl>
                <Input
                  id="reg-password"
                  v-bind="componentField"
                  type="password"
                  autocomplete="new-password"
                  :placeholder="messages.register.passwordPlaceholder"
                />
              </FormControl>
              <FormDescription>{{ messages.register.passwordHint }}</FormDescription>
              <FormMessage />
            </FormItem>
          </FormField>

          <FormField
            v-slot="{ componentField }"
            name="confirmPassword"
          >
            <FormItem>
              <FormLabel for="reg-password2">
                {{ messages.register.confirmPasswordLabel }}
              </FormLabel>
              <FormControl>
                <Input
                  id="reg-password2"
                  v-bind="componentField"
                  type="password"
                  autocomplete="new-password"
                  :placeholder="messages.register.confirmPasswordPlaceholder"
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          </FormField>

          <Alert
            v-if="submitError.length > 0"
            variant="destructive"
            data-testid="register-submit-error"
          >
            <AlertTitle>{{ messages.register.failedTitle }}</AlertTitle>
            <AlertDescription>{{ submitError }}</AlertDescription>
          </Alert>

          <Button
            type="submit"
            class="w-full"
            :disabled="isSubmitting"
            :data-testid="isSubmitting ? 'page-register--submit' : undefined"
          >
            <span
              v-if="isSubmitting"
              class="size-4 animate-spin rounded-full border-2 border-current border-t-transparent"
              aria-hidden="true"
            />
            {{ isSubmitting ? messages.register.submitting : messages.register.submit }}
          </Button>
        </form>
      </CardContent>

      <CardFooter class="justify-center">
        <RouterLink
          to="/login"
          class="text-sm text-muted-foreground hover:text-foreground"
        >
          {{ messages.register.toLogin }}
        </RouterLink>
      </CardFooter>
    </Card>
  </AuthLayout>
</template>
