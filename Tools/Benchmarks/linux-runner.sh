#!/usr/bin/env bash
# 在 Linux/WSL 目标机上执行基准测试。
# 用法：linux-runner.sh <工作目录> <迭代次数>
# 依赖：工作目录下的 bench-run（由 staragent-bench.ps1 编译/部署）与 manifest.tsv
# manifest.tsv 每行：variant<TAB>mode<TAB>exe<TAB>arg1<TAB>arg2
# 输出 CSV：variant,mode,run,wall_ms,cpu_ms,rss_kb,exit
set -u

workdir=${1:?workdir}
iters=${2:-15}

echo "variant,mode,run,wall_ms,cpu_ms,rss_kb,exit"

while IFS=$'\t' read -r variant mode exe a1 a2; do
    [ -z "${variant:-}" ] && continue

    cd "$workdir/$variant" || {
        echo "$variant,$mode,0,,,,2"
        continue
    }

    # Windows 打包的 tar 不带可执行位，部署后补上
    chmod +x ./StarAgent ./staragent 2>/dev/null || true

    # 预热一次，生成配置/日志，并触发文件系统缓存
    timeout 120 "$workdir/bench-run" "$exe" ${a1:-} ${a2:-} >/dev/null 2>&1

    for i in $(seq 1 "$iters"); do
        line=$(timeout 120 "$workdir/bench-run" "$exe" ${a1:-} ${a2:-} 2>/dev/null)
        rc=$?
        if [ $rc -ne 0 ] || [ -z "$line" ]; then
            echo "$variant,$mode,$i,,,,$rc"
            continue
        fi
        set -- $line
        echo "$variant,$mode,$i,$1,$2,$3,$4"
    done
done < "$workdir/manifest.tsv"
