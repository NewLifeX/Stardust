package scenario

import (
	"context"
	"encoding/json"
	"io"
	"os"
	"path/filepath"
	"testing"
	"time"

	"staragent-conformance/internal/fakeserver"
)

func TestAllScenariosWithMockAgent(t *testing.T) {
	for _, def := range Definitions() {
		def := def
		t.Run(def.Name, func(t *testing.T) {
			ctx, cancel := context.WithTimeout(context.Background(), 45*time.Second)
			defer cancel()

			server := fakeserver.New(fakeserver.Options{
				Period:           1,
				AutoPingInterval: 100 * time.Millisecond,
			})
			if err := server.Start(); err != nil {
				t.Fatalf("start fake server: %v", err)
			}
			defer server.Close()

			workDir := t.TempDir()
			configPath, dataDir, err := prepareConfig(server.URL(), workDir, "", 5)
			if err != nil {
				t.Fatalf("prepare config: %v", err)
			}
			sink := newLogSink(io.Discard)
			env := &Env{
				Ctx:        ctx,
				Options:    Options{MockAgent: true, Out: io.Discard},
				Server:     server,
				Control:    NewControl(server.URL()),
				WorkDir:    workDir,
				ConfigPath: configPath,
				DataDir:    dataDir,
				logs:       sink,
				out:        io.Discard,
			}
			defer env.StopAgent()

			if err := def.Run(ctx, env); err != nil {
				t.Fatalf("%v\nstate: %s\nlog:\n%s", err, summarizeState(server.State()), sink.String())
			}
		})
	}
}

func TestPrepareConfigTemplate(t *testing.T) {
	workDir := t.TempDir()
	template := filepath.Join("..", "..", "templates", "agent.json")
	configPath, dataDir, err := prepareConfig("http://127.0.0.1:45678", workDir, template, 5)
	if err != nil {
		t.Fatalf("prepareConfig: %v", err)
	}
	data, err := os.ReadFile(configPath)
	if err != nil {
		t.Fatalf("read config: %v", err)
	}
	var cfg map[string]any
	if err := json.Unmarshal(data, &cfg); err != nil {
		t.Fatalf("config is not JSON: %v\n%s", err, data)
	}
	if cfg["Server"] != "http://127.0.0.1:45678" {
		t.Fatalf("Server = %v", cfg["Server"])
	}
	if cfg["DataDir"] != dataDir {
		t.Fatalf("DataDir = %v, want %v", cfg["DataDir"], dataDir)
	}
	if cfg["Code"] != "" || cfg["Secret"] != "" {
		t.Fatalf("template must start with empty Code/Secret, got %v/%v", cfg["Code"], cfg["Secret"])
	}
	if cfg["ProductCode"] == "" {
		t.Fatal("ProductCode is empty")
	}
	if cfg["MetricsInterval"] != float64(5) {
		t.Fatalf("MetricsInterval = %v, want 5", cfg["MetricsInterval"])
	}
}

func TestPrepareConfigMetricsInterval(t *testing.T) {
	workDir := t.TempDir()
	templatePath := filepath.Join(workDir, "template.json")
	if err := os.WriteFile(templatePath, []byte(`{"Server":"x","DataDir":"y","ProductCode":"T","MetricsInterval":30}`), 0o600); err != nil {
		t.Fatal(err)
	}
	configPath, _, err := prepareConfig("http://127.0.0.1:1", workDir, templatePath, 5)
	if err != nil {
		t.Fatalf("prepareConfig: %v", err)
	}
	data, _ := os.ReadFile(configPath)
	var cfg map[string]any
	if err := json.Unmarshal(data, &cfg); err != nil {
		t.Fatal(err)
	}
	if cfg["MetricsInterval"] != float64(30) {
		t.Fatalf("template MetricsInterval = %v, want 30 (template must win)", cfg["MetricsInterval"])
	}

	configPath, _, err = prepareConfig("http://127.0.0.1:1", workDir+"/plain", "", 12)
	if err != nil {
		t.Fatalf("prepareConfig default: %v", err)
	}
	data, _ = os.ReadFile(configPath)
	cfg = nil
	if err := json.Unmarshal(data, &cfg); err != nil {
		t.Fatal(err)
	}
	if cfg["MetricsInterval"] != float64(12) {
		t.Fatalf("default MetricsInterval = %v, want 12", cfg["MetricsInterval"])
	}
}
