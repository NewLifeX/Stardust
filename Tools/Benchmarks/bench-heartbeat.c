/* 测量代理从进程启动到「首次心跳」的墙钟、CPU 与峰值 RSS。
 * 用法：bench-heartbeat <timeout_ms> <marker> <logfile> <exe> [args...]
 *   marker 为空表示等待子进程自行退出（原生 once 模式）。
 * 输出：wall_ms cpu_ms max_rss_kb exit marker_found（空格分隔，单行）
 *   marker_found：1=命中，0=等待进程自行退出，-1=超时。
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <signal.h>
#include <sys/stat.h>
#include <sys/wait.h>
#include <sys/resource.h>
#include <time.h>

static double NowMs(void)
{
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return ts.tv_sec * 1000.0 + ts.tv_nsec / 1e6;
}

static int FileContains(const char* path, const char* marker, long* lastSize)
{
    struct stat st;
    if (stat(path, &st) != 0) return 0;
    if (st.st_size == *lastSize) return 0;
    *lastSize = st.st_size;

    FILE* f = fopen(path, "rb");
    if (f == NULL) return 0;

    const long size = st.st_size;
    const long start = size > 4 * 1024 * 1024 ? size - 4 * 1024 * 1024 : 0;
    if (fseek(f, start, SEEK_SET) != 0)
    {
        fclose(f);
        return 0;
    }

    const long len = size - start;
    char* buf = (char*)malloc((size_t)len + 1);
    if (buf == NULL)
    {
        fclose(f);
        return 0;
    }

    const size_t n = fread(buf, 1, (size_t)len, f);
    buf[n] = '\0';
    const int found = strstr(buf, marker) != NULL;

    free(buf);
    fclose(f);
    return found;
}

int main(int argc, char** argv)
{
    if (argc < 5)
    {
        fprintf(stderr, "usage: bench-heartbeat <timeout_ms> <marker> <logfile> <exe> [args...]\n");
        return 2;
    }

    const int timeout = atoi(argv[1]);
    const char* marker = argv[2];
    const char* logfile = argv[3];
    char** childArgs = &argv[4];
    const int hasMarker = marker != NULL && marker[0] != '\0';

    const double start = NowMs();

    /* 先清空旧日志，避免父进程在子进程 open(O_TRUNC) 之前读到上一次的心跳标记 */
    {
        FILE* f = fopen(logfile, "w");
        if (f != NULL) fclose(f);
    }

    const pid_t pid = fork();
    if (pid == 0)
    {
        setpgid(0, 0);
        const int fd = open(logfile, O_WRONLY | O_CREAT | O_TRUNC, 0644);
        if (fd >= 0)
        {
            dup2(fd, STDOUT_FILENO);
            dup2(fd, STDERR_FILENO);
            close(fd);
        }
        execvp(childArgs[0], childArgs);
        _exit(127);
    }
    if (pid < 0)
    {
        perror("fork");
        return 2;
    }

    int status = 0;
    int reaped = 0;
    int found = 0;
    int timedOut = 0;
    double markerTime = 0;
    long lastSize = -1;
    struct rusage ru;
    memset(&ru, 0, sizeof(ru));

    for (;;)
    {
        const pid_t r = wait4(pid, &status, WNOHANG, &ru);
        if (r == pid)
        {
            reaped = 1;
            break;
        }

        if (hasMarker && FileContains(logfile, marker, &lastSize))
        {
            found = 1;
            markerTime = NowMs();
            break;
        }

        if ((int)(NowMs() - start) > timeout)
        {
            timedOut = 1;
            break;
        }

        usleep(10 * 1000);
    }

    if (!reaped)
    {
        kill(-pid, SIGTERM);
        kill(pid, SIGTERM);
        for (int i = 0; i < 300; i++)
        {
            if (wait4(pid, &status, WNOHANG, &ru) == pid)
            {
                reaped = 1;
                break;
            }
            usleep(10 * 1000);
        }
        if (!reaped)
        {
            kill(-pid, SIGKILL);
            kill(pid, SIGKILL);
            wait4(pid, &status, 0, &ru);
            reaped = 1;
        }
    }

    const double wall = (found && markerTime > 0 ? markerTime : NowMs()) - start;
    const double cpu = (ru.ru_utime.tv_sec + ru.ru_stime.tv_sec) * 1000.0 +
                       (ru.ru_utime.tv_usec + ru.ru_stime.tv_usec) / 1000.0;
    const int code = WIFEXITED(status) ? WEXITSTATUS(status) : -1;

    printf("%.3f %.3f %ld %d %d\n", wall, cpu, (long)ru.ru_maxrss, code, found ? 1 : (timedOut ? -1 : 0));
    return 0;
}
