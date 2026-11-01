package agent

import (
	"archive/tar"
	"archive/zip"
	"compress/gzip"
	"os"
	"path/filepath"
	"testing"
)

// 构造一个含单个文件的 zip，用于验证解压路径与内容。
func writeTestZip(t *testing.T, path string, name string, content string) {
	t.Helper()

	file, err := os.Create(path)
	if err != nil {
		t.Fatalf("create zip: %v", err)
	}
	defer file.Close()

	writer := zip.NewWriter(file)
	entry, err := writer.Create(name)
	if err != nil {
		t.Fatalf("create entry: %v", err)
	}
	if _, err := entry.Write([]byte(content)); err != nil {
		t.Fatalf("write entry: %v", err)
	}
	if err := writer.Close(); err != nil {
		t.Fatalf("close zip: %v", err)
	}
}

// 构造一个含单个文件的 tar.gz。
func writeTestTarGz(t *testing.T, path string, name string, content string) {
	t.Helper()

	file, err := os.Create(path)
	if err != nil {
		t.Fatalf("create tar.gz: %v", err)
	}
	defer file.Close()

	gz := gzip.NewWriter(file)
	writer := tar.NewWriter(gz)
	payload := []byte(content)
	header := &tar.Header{Name: name, Mode: 0o644, Size: int64(len(payload)), Typeflag: tar.TypeReg}
	if err := writer.WriteHeader(header); err != nil {
		t.Fatalf("write header: %v", err)
	}
	if _, err := writer.Write(payload); err != nil {
		t.Fatalf("write payload: %v", err)
	}
	if err := writer.Close(); err != nil {
		t.Fatalf("close tar: %v", err)
	}
	if err := gz.Close(); err != nil {
		t.Fatalf("close gzip: %v", err)
	}
}

// safeJoin 必须阻断 ../ 穿越，只允许目标目录内的相对路径。
func TestSafeJoinRejectsEscape(t *testing.T) {
	base := t.TempDir()

	if _, err := safeJoin(base, "../escape.txt"); err == nil {
		t.Fatal("../ 应被拒绝")
	}
	if _, err := safeJoin(base, "sub/../../escape.txt"); err == nil {
		t.Fatal("sub/../../ 应被拒绝")
	}

	target, err := safeJoin(base, "sub/file.txt")
	if err != nil {
		t.Fatalf("正常相对路径被拒绝: %v", err)
	}
	if want := filepath.Join(base, "sub", "file.txt"); target != want {
		t.Fatalf("路径不符: got %s want %s", target, want)
	}
}

// zip 解压后内容与目录结构保持完整。
func TestExtractZip(t *testing.T) {
	dir := t.TempDir()
	archive := filepath.Join(dir, "pkg.zip")
	writeTestZip(t, archive, "bin/StarAgent.dll", "zip-payload")

	target := filepath.Join(dir, "out")
	if err := os.MkdirAll(target, 0o755); err != nil {
		t.Fatalf("mkdir: %v", err)
	}
	if err := extractZip(archive, target); err != nil {
		t.Fatalf("extractZip: %v", err)
	}

	data, err := os.ReadFile(filepath.Join(target, "bin", "StarAgent.dll"))
	if err != nil {
		t.Fatalf("read extracted: %v", err)
	}
	if string(data) != "zip-payload" {
		t.Fatalf("内容不符: %s", string(data))
	}
}

// tar.gz 解压后内容与目录结构保持完整。
func TestExtractTarGz(t *testing.T) {
	dir := t.TempDir()
	archive := filepath.Join(dir, "pkg.tar.gz")
	writeTestTarGz(t, archive, "StarAgent.dll", "tar-payload")

	target := filepath.Join(dir, "out")
	if err := os.MkdirAll(target, 0o755); err != nil {
		t.Fatalf("mkdir: %v", err)
	}
	if err := extractTarGz(archive, target); err != nil {
		t.Fatalf("extractTarGz: %v", err)
	}

	data, err := os.ReadFile(filepath.Join(target, "StarAgent.dll"))
	if err != nil {
		t.Fatalf("read extracted: %v", err)
	}
	if string(data) != "tar-payload" {
		t.Fatalf("内容不符: %s", string(data))
	}
}
