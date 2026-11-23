/* 精确测量子进程的墙钟、CPU 时间与峰值 RSS。
 * 用法：bench-run <可执行文件> [参数...]
 * 输出：wall_ms cpu_ms max_rss_kb exit_code（空格分隔，单行）
 */
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <sys/wait.h>
#include <sys/resource.h>
#include <time.h>

int main(int argc, char** argv)
{
    if (argc < 2)
    {
        fprintf(stderr, "usage: bench-run <exe> [args...]\n");
        return 2;
    }

    struct timespec t0, t1;
    clock_gettime(CLOCK_MONOTONIC, &t0);

    const pid_t pid = fork();
    if (pid == 0)
    {
        /* 子进程输出与测量无关，丢弃，避免污染 CSV */
        (void)freopen("/dev/null", "w", stdout);
        (void)freopen("/dev/null", "w", stderr);
        execvp(argv[1], &argv[1]);
        _exit(127);
    }
    if (pid < 0)
    {
        perror("fork");
        return 2;
    }

    int status = 0;
    struct rusage ru;
    wait4(pid, &status, 0, &ru);

    clock_gettime(CLOCK_MONOTONIC, &t1);

    const double wall = (t1.tv_sec - t0.tv_sec) * 1000.0 + (t1.tv_nsec - t0.tv_nsec) / 1e6;
    const double cpu = (ru.ru_utime.tv_sec + ru.ru_stime.tv_sec) * 1000.0 +
                       (ru.ru_utime.tv_usec + ru.ru_stime.tv_usec) / 1000.0;
    const int code = WIFEXITED(status) ? WEXITSTATUS(status) : -1;

    printf("%.3f %.3f %ld %d\n", wall, cpu, (long)ru.ru_maxrss, code);
    return 0;
}
