package agent

import (
	"archive/tar"
	"archive/zip"
	"compress/gzip"
	"context"
	"crypto/md5"
	"encoding/hex"
	"errors"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

// 安装目录与运行时目录，与 .NET 版 StarAgent 及官方安装脚本保持一致。
func agentDir() string {
	if runtime.GOOS == "windows" {
		return `C:\Program Files\StarAgent`
	}
	return "/opt/staragent"
}

func runtimeDir() string {
	switch runtime.GOOS {
	case "windows":
		return `C:\Program Files\dotnet`
	case "darwin":
		return "/usr/local/share/dotnet"
	default:
		return "/usr/share/dotnet"
	}
}

// Bootstrap 登录后取引导安装清单，安装 .NET 运行时与完整版 StarAgent。
// 原生 Agent 只做采集上报，需要完整能力时由运维在节点上执行 staragent bootstrap。
func (a *Agent) Bootstrap(ctx context.Context) error {
	if err := a.login(ctx); err != nil {
		return err
	}

	return a.installBootstrap(ctx)
}

// installBootstrap 假定已经登录，执行取包、下载、安装与事件上报。
func (a *Agent) installBootstrap(ctx context.Context) error {
	info, err := a.client.Bootstrap(ctx)
	if err != nil {
		return err
	}
	if info == nil {
		return errors.New("server returned no agent package")
	}

	if runtime.GOOS != "windows" && os.Geteuid() != 0 {
		return errors.New("bootstrap requires root privileges")
	}

	cfg := a.store.Snapshot()
	dir := filepath.Join(cfg.DataDir, "bootstrap")
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return fmt.Errorf("create bootstrap dir: %w", err)
	}

	a.log.Info("bootstrap plan", "agent", info.Version, "runtime", info.Runtime.Version)

	if info.Runtime.Source != "" {
		path, err := downloadPackage(ctx, info.Runtime, dir)
		if err != nil {
			return fmt.Errorf("runtime: %w", err)
		}
		if err := installRuntime(path, info.Runtime.FileName); err != nil {
			return fmt.Errorf("runtime: %w", err)
		}
		a.log.Info("bootstrap runtime installed", "version", info.Runtime.Version)
	}

	path, err := downloadPackage(ctx, info.Agent, dir)
	if err != nil {
		return fmt.Errorf("agent: %w", err)
	}

	server := info.Server
	if server == "" {
		server = cfg.Server
	}
	if err := installAgent(path, info.Agent.FileName, server); err != nil {
		return fmt.Errorf("agent: %w", err)
	}

	a.log.Info("bootstrap completed", "version", info.Version)
	a.enqueue("info", "Bootstrap", "installed StarAgent "+info.Version)
	a.flushEvents(ctx)

	return nil
}

// downloadPackage 下载安装包并校验 MD5（服务端只保证 32 位十六进制）。
func downloadPackage(ctx context.Context, pkg protocol.BootstrapPackage, dir string) (string, error) {
	name := pkg.FileName
	if name == "" {
		name = filepath.Base(strings.Split(pkg.Source, "?")[0])
	}
	path := filepath.Join(dir, name)

	client := &http.Client{Timeout: 30 * time.Minute}
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, pkg.Source, nil)
	if err != nil {
		return "", err
	}
	response, err := client.Do(request)
	if err != nil {
		return "", err
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusOK {
		return "", fmt.Errorf("download failed: %s", response.Status)
	}

	file, err := os.Create(path)
	if err != nil {
		return "", err
	}
	hash := md5.New()
	if _, err := io.Copy(io.MultiWriter(file, hash), response.Body); err != nil {
		file.Close()
		return "", err
	}
	if err := file.Close(); err != nil {
		return "", err
	}

	if len(pkg.FileHash) == 32 {
		actual := hex.EncodeToString(hash.Sum(nil))
		if !strings.EqualFold(actual, pkg.FileHash) {
			return "", fmt.Errorf("hash mismatch: expected %s actual %s", pkg.FileHash, actual)
		}
	}

	return path, nil
}

// installRuntime 安装 .NET 运行时：Windows 走官方 exe 静默安装，其余平台解压 tar.gz 并建软链。
func installRuntime(path, fileName string) error {
	if runtime.GOOS == "windows" {
		cmd := exec.Command(path, "/install", "/quiet", "/norestart")
		err := cmd.Run()
		if err == nil {
			return nil
		}
		var exitErr *exec.ExitError
		// 3010 表示安装成功但需要重启
		if errors.As(err, &exitErr) && exitErr.ExitCode() == 3010 {
			return nil
		}
		return fmt.Errorf("runtime installer failed: %w", err)
	}

	dir := runtimeDir()
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return err
	}
	if strings.HasSuffix(strings.ToLower(fileName), ".zip") {
		if err := extractZip(path, dir); err != nil {
			return err
		}
	} else if err := extractTarGz(path, dir); err != nil {
		return err
	}

	link := "/usr/bin/dotnet"
	_ = os.Remove(link)
	return os.Symlink(filepath.Join(dir, "dotnet"), link)
}

// installAgent 解压完整版 StarAgent 并调用其自身的 -install 注册服务。
func installAgent(path, fileName string, server string) error {
	dir := agentDir()
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return err
	}

	lower := strings.ToLower(fileName)
	if strings.HasSuffix(lower, ".zip") || (fileName == "" && strings.HasSuffix(strings.ToLower(path), ".zip")) {
		if err := extractZip(path, dir); err != nil {
			return err
		}
	} else if err := extractTarGz(path, dir); err != nil {
		return err
	}

	cmd := exec.Command("dotnet", "StarAgent.dll", "-install", "-server", server)
	cmd.Dir = dir
	output, err := cmd.CombinedOutput()
	if err != nil {
		return fmt.Errorf("agent install failed: %w %s", err, strings.TrimSpace(string(output)))
	}

	return nil
}

// extractZip 解压 zip 到目标目录，保留内部相对路径并阻断目录穿越。
func extractZip(path, dir string) error {
	reader, err := zip.OpenReader(path)
	if err != nil {
		return err
	}
	defer reader.Close()

	for _, item := range reader.File {
		target, err := safeJoin(dir, item.Name)
		if err != nil {
			return err
		}
		if item.FileInfo().IsDir() {
			if err := os.MkdirAll(target, 0o755); err != nil {
				return err
			}
			continue
		}
		if err := os.MkdirAll(filepath.Dir(target), 0o755); err != nil {
			return err
		}

		source, err := item.Open()
		if err != nil {
			return err
		}
		mode := item.Mode()
		if mode == 0 {
			mode = 0o644
		}
		file, err := os.OpenFile(target, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, mode)
		if err != nil {
			source.Close()
			return err
		}
		_, copyErr := io.Copy(file, source)
		file.Close()
		source.Close()
		if copyErr != nil {
			return copyErr
		}
	}

	return nil
}

// extractTarGz 解压 tar.gz 到目标目录，安全规则同 extractZip。
func extractTarGz(path, dir string) error {
	file, err := os.Open(path)
	if err != nil {
		return err
	}
	defer file.Close()

	unzipped, err := gzip.NewReader(file)
	if err != nil {
		return err
	}
	defer unzipped.Close()

	reader := tar.NewReader(unzipped)
	for {
		header, err := reader.Next()
		if errors.Is(err, io.EOF) {
			return nil
		}
		if err != nil {
			return err
		}

		target, err := safeJoin(dir, header.Name)
		if err != nil {
			return err
		}

		switch header.Typeflag {
		case tar.TypeDir:
			if err := os.MkdirAll(target, 0o755); err != nil {
				return err
			}
		case tar.TypeReg:
			if err := os.MkdirAll(filepath.Dir(target), 0o755); err != nil {
				return err
			}
			out, err := os.OpenFile(target, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, os.FileMode(header.Mode)&0o777)
			if err != nil {
				return err
			}
			_, copyErr := io.Copy(out, reader)
			out.Close()
			if copyErr != nil {
				return copyErr
			}
		}
	}
}

// safeJoin 阻止安装包内的绝对路径与 ../ 穿越到目标目录之外。
func safeJoin(base, name string) (string, error) {
	target := filepath.Join(base, name)
	if !strings.HasPrefix(target, filepath.Clean(base)+string(os.PathSeparator)) {
		return "", fmt.Errorf("invalid path in package: %s", name)
	}
	return target, nil
}
