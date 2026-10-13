#!/usr/bin/env bash
#
# 把一次发布的产物挂到 Gitea 的 Release 上：NuGet 包、符号包、画廊 exe。
#
# 为什么是 curl 而不是现成的 action：这台 Gitea 配了 `DEFAULT_ACTIONS_URL = self`，第三方 action
# 会被解析到内网同名仓库（`softprops/action-gh-release` 那种内网根本没有），而 runner 镜像里
# 自带 curl 与 jq，几十行就够，还能自己决定"重跑要收敛"。
#
# 用法（仓库根目录）：
#   GITHUB_TOKEN=xxx build/publish-release-assets.sh artifacts/*.nupkg artifacts/*.snupkg artifacts/release/AvChartsDemo.exe
#
# 环境变量：
#   GITHUB_REPOSITORY  owner/repo，Actions 自动提供（没有就按 ipaas/av-charts 走）
#   GITHUB_SERVER_URL  Gitea 站点根，Actions 自动提供（没有就按 https://git.cyber-stone.com 走）
#   GITHUB_REF_NAME    当前 ref 名，Actions 自动提供；打 tag 发布时就是 tag 名
#   RELEASE_TAG        可选：显式指定要挂到哪个 tag 的 Release
#   令牌               按 GITHUB_TOKEN → GH_WORKFLOW_TOKEN → RELEASE_TOKEN 的顺序取第一个非空的：
#                      Actions 自动发的那枚在有些实例上是环境变量 GITHUB_TOKEN，有些只放在
#                      `secrets.GITHUB_TOKEN`（workflow 取出来交给 GH_WORKFLOW_TOKEN），
#                      都没有就用仓库自己配的 RELEASE_TOKEN。
#
# 行为：
#   - 该 tag 上没有 Release 就建一个；已经有了就复用，**不动**它原来的说明（可能是人写的）；
#   - 同名的旧附件先删再传，所以整条流程重跑是幂等的（补挂历史版本也走这条路）；
#   - 附件的大小与类型走 Gitea 的 `[repository.release]`（`ALLOWED_TYPES` 空 = 不限，`FILE_MAX_SIZE`
#     默认 2048MB），所以几百 MB 的 exe 不用改服务端配置；`[attachment]` 那一节的 `MAX_SIZE` 只管
#     issue/PR 的附件。真被调小了会拿到 413，这条日志会把服务端的话原样打出来。

set -euo pipefail

die() {
    echo "错误：$*" >&2
    exit 1
}

[ "$#" -gt 0 ] || die "用法：$0 <产物文件> [更多产物…]"

for tool in curl jq basename du; do
    command -v "$tool" >/dev/null 2>&1 || die "runner 上找不到 $tool"
done

repo="${GITHUB_REPOSITORY:-ipaas/av-charts}"
tag="${RELEASE_TAG:-${GITHUB_REF_NAME:-}}"
api="${RELEASE_API_URL:-${GITHUB_SERVER_URL:-https://git.cyber-stone.com}/api/v1}"
api="${api%/}"

token=""
for candidate in "${GITHUB_TOKEN:-}" "${GH_WORKFLOW_TOKEN:-}" "${RELEASE_TOKEN:-}"; do
    if [ -n "${candidate}" ]; then
        token="${candidate}"
        break
    fi
done

[ -n "$tag" ] || die "不知道挂到哪个 tag：设 RELEASE_TAG，或让 GITHUB_REF_NAME 有值"
[ -n "$token" ] || die "没有令牌：GITHUB_TOKEN / GH_WORKFLOW_TOKEN / RELEASE_TOKEN 都是空的"

files=("$@")
for file in "${files[@]}"; do
    [ -f "$file" ] || die "找不到产物：$file"
done

# 令牌只进 header，不进命令行参数、不进日志。
call() {
    curl -fsS --retry 3 -H "Authorization: token ${token}" -H 'Accept: application/json' "$@"
}

echo "Release 目标：${api}/repos/${repo}/releases（tag ${tag}）"

# 先找现成的 Release：Gitea 网页上建过、或上次跑过，都不该再建第二个。
# 这里单独看状态码：404 是"还没有"，别的码（401/403/500…）要原样报出来，不能当成"没有"继续走。
probe_body="$(mktemp)"
probe_status="$(curl -sS --retry 3 -o "${probe_body}" -w '%{http_code}' \
    -H "Authorization: token ${token}" -H 'Accept: application/json' \
    "${api}/repos/${repo}/releases/tags/${tag}" || true)"

case "${probe_status}" in
200)
    release_json="$(cat "${probe_body}")"
    release_id="$(printf '%s' "${release_json}" | jq -r '.id // empty')"
    echo "复用已有的 Release #${release_id}"
    ;;
404)
    echo "为 ${tag} 新建 Release"
    body="AvCharts ${tag} —— 两个包、符号包与画廊 exe 见下方附件；包同时已推到 CyberStone NuGet 源。"
    payload="$(jq -n --arg tag "${tag}" --arg body "${body}" \
        '{tag_name: $tag, name: $tag, body: $body, draft: false, prerelease: false}')"
    release_json="$(call -X POST "${api}/repos/${repo}/releases" \
        -H 'Content-Type: application/json' --data "${payload}")"
    release_id="$(printf '%s' "${release_json}" | jq -r '.id // empty')"
    ;;
*)
    rm -f "${probe_body}"
    die "查询 Release 失败（HTTP ${probe_status}）：$(cat "${probe_body}" 2>/dev/null || echo '（无响应体）')"
    ;;
esac

rm -f "${probe_body}"
[ -n "${release_id}" ] || die "没拿到 Release id，服务端返回：${release_json}"

echo "Release 地址：$(printf '%s' "${release_json}" | jq -r '.html_url // empty')"

for file in "${files[@]}"; do
    name="$(basename "${file}")"

    # 同名附件在 Gitea 里是冲突的，重跑先把旧的删掉。
    for id in $(call "${api}/repos/${repo}/releases/${release_id}/assets" \
        | jq -r --arg name "${name}" '.[] | select(.name == $name) | .id'); do
        echo "删掉旧的同名附件 ${name}（id ${id}）"
        call -X DELETE "${api}/repos/${repo}/releases/${release_id}/assets/${id}" >/dev/null
    done

    echo "上传 ${name}（$(du -h "${file}" | cut -f1)）"
    # --fail-with-body：失败了也要把服务端的话（比如 413 的"文件太大"）打出来。
    curl -sS --retry 3 --fail-with-body \
        -H "Authorization: token ${token}" \
        -H 'Accept: application/json' \
        -X POST "${api}/repos/${repo}/releases/${release_id}/assets?name=${name}" \
        -F "attachment=@${file}" \
        | jq -r '"  已上传：\(.name)（\(.size) 字节）\(.browser_download_url)"'
done
