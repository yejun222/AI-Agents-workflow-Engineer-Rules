/**
 * 面向用户的固定文案集中处：所有组件只引用此处常量，禁止在组件内散写中文串
 * （判分锚点文案与 PRD / 原型冻结一致，改动须同步 docs/20-prototype.html 与 docs/10-prd.md）。
 */
import {
  CONFIRM_PASSWORD_MESSAGE,
  PASSWORD_MESSAGE,
  USER_NAME_MESSAGE
} from '@/types/auth'

export const messages = {
  app: {
    brand: '转盘抽奖'
  },
  common: {
    systemBusy: '系统繁忙，请稍后重试',
    loadFailedTitle: '加载失败',
    retry: '重试',
    loading: '加载中…',
    logout: '登出',
    idempotencyConflict: '请勿重复提交',
    tooManyRequests: '操作过于频繁，请稍后重试',
    sessionExpired: '登录状态已失效，请重新登录'
  },
  authLayout: {
    tagline: '登录后即可参与每日抽奖'
  },
  register: {
    title: '注册',
    // 规则文案唯一来源 = types/auth 的冻结常量，禁止在组件内出现第二份副本（附录 A 第 10 条）
    subtitle: `${USER_NAME_MESSAGE}；${PASSWORD_MESSAGE}`,
    userNameLabel: '用户名',
    userNamePlaceholder: '请输入用户名',
    userNameHint: '4-20 位，仅字母、数字、下划线；全局唯一且不区分大小写',
    userNameTaken: '用户名已被占用',
    passwordLabel: '密码',
    passwordPlaceholder: '请输入密码',
    passwordHint: '8-20 位，须同时包含大写字母、小写字母与数字',
    confirmPasswordLabel: '确认密码',
    confirmPasswordPlaceholder: '请再次输入密码',
    submit: '注册',
    submitting: '注册中…',
    failedTitle: '注册失败',
    toLogin: '已有账号？去登录'
  },
  login: {
    title: '登录',
    subtitle: '使用用户名与密码登录',
    userNameLabel: '用户名',
    userNamePlaceholder: '请输入用户名',
    passwordLabel: '密码',
    passwordPlaceholder: '请输入密码',
    submit: '登录',
    submitting: '登录中…',
    failedTitle: '用户名或密码错误',
    failedDesc: '请检查后重试。为防账号枚举，此处不区分「用户名不存在」与「密码错误」。',
    guardTitle: '请先登录',
    guardDesc: '登录状态已失效或尚未登录，登录后将返回原页面。',
    toRegister: '还没有账号？去注册'
  },
  draw: {
    title: '幸运抽奖',
    quotaLabel: '今日剩余次数：',
    quotaUnit: '次',
    /** 次数未知（首载查询失败）时的占位符：不得渲染为 0（REV-01）。 */
    quotaUnknown: '—',
    /** 每日上限徽标；上限未知时由调用方隐藏（不得渲染 0）。 */
    dailyLimit: (limit: number): string => `每日上限 ${limit} 次`,
    quotaExhausted: '今日抽奖次数已用完，明日 0 点重置',
    /** Should-S2：次数用尽文案的精确重置时间引导（UTC+8）。 */
    quotaResetHint: (time: string): string => `重置时间：${time}（UTC+8）`,
    start: '开始抽奖',
    drawing: '抽奖中…',
    wheelLabel: '抽奖转盘',
    legendTitle: '奖池',
    poolEmptyTitle: '奖品已抽完，请稍后再来',
    poolEmptyDesc: '奖池暂无可用奖品，稍后再试或联系管理员。',
    loading: '正在加载奖池…',
    drawFailedTitle: '抽奖失败',
    drawFailedDesc: '本次抽奖未完成，可点击重试（沿用同一幂等键，不会重复扣次）。',
    winTitle: '恭喜获得',
    /**
     * 结果条目在重拉后的奖池快照中仍不可定位（条目被停用 / 重拉失败）时的兜底名称（BUG-04）：
     * 弹层不得渲染空名称（「恭喜获得」后必须始终有可读名称）。
     */
    prizeUnknown: '未知奖品',
    winDesc: '已记入我的中奖记录',
    loseTitle: '谢谢参与，再接再厉',
    loseDesc: '未中奖同样消耗 1 次机会，且不生成中奖记录。',
    viewRecords: '查看我的中奖记录',
    continueDraw: '继续抽奖',
    retryDraw: '重试'
  },
  prizes: {
    /**
     * 抽奖页奖池错误态标题（`page-draw--error`）：原型状态定义表抽奖页 `error` 行为「奖池加载失败」。
     * 与记录页的 `common.loadFailedTitle`（「加载失败」）是**两页各自的冻结口径**，不可合并为同一个键（BUG-03）。
     */
    loadFailedTitle: '奖池加载失败'
  },
  records: {
    title: '我的中奖记录',
    caption: '我的中奖记录（仅本人可见，按中奖时间倒序，每页 10 条）',
    prizeName: '奖品名称',
    winTime: '中奖时间',
    loading: '加载中…',
    emptyTitle: '还没有中奖记录，去试试手气吧',
    emptyDesc: '仅中奖记录入表；「谢谢参与」不生成记录。',
    goDraw: '去抽奖',
    prev: '上一页',
    next: '下一页',
    totalCount: (count: number): string => `共 ${count} 条记录`,
    pageIndicator: (page: number, total: number): string => `第 ${page} / ${total} 页`,
    paginationLabel: '分页'
  },
  prizeType: {
    physical: '实物',
    virtual: '虚拟',
    noPrize: '未中奖'
  },
  validation: {
    userNameRequired: '请输入用户名',
    userNameFormat: USER_NAME_MESSAGE,
    passwordRequired: '请输入密码',
    passwordFormat: PASSWORD_MESSAGE,
    confirmPasswordRequired: '请再次输入密码',
    confirmPasswordMismatch: CONFIRM_PASSWORD_MESSAGE
  }
} as const
