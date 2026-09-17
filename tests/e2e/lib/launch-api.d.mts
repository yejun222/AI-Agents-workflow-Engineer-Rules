/**
 * `launch-api.mjs` 的类型声明。
 *
 * 存在的原因：启动模板必须是**唯一一份实现**，故写成 `.mjs` 以便 Playwright spec（TS）与
 * 其它 `.mjs` 脚本共用；而 TS 需要 `.d.mts` 才能拿到类型 —— 否则调用点会退化成隐式 `any`，
 * 违反 `CLAUDE.md`「禁止 any，所有变量/函数/参数显式声明类型」的前端红线。
 * 二者必须同步修改（改 `.mjs` 的导出即改此处）。
 */
import type { ChildProcess } from 'node:child_process';

export declare const REPO_ROOT: string;
export declare const PUBLISH_DIR: string;
export declare const PUBLISH_DLL: string;
export declare const LOGS_DIR: string;
export declare const TEST_DB: string;
export declare const CONNECTION_STRING: string;

export declare function launchApi(
  port: number,
  env: Record<string, string>,
  logName: string
): { child: ChildProcess; logPath: string };

/** 真实就绪检查（走签发令牌路径）；超时返回 false —— 此时后续一切"用例失败"结论无效。 */
export declare function waitReady(port: number, timeoutMs?: number): Promise<boolean>;

/** 进程退出等待；超时返回 null。 */
export declare function waitExit(child: ChildProcess, timeoutMs: number): Promise<number | null>;
